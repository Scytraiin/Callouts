using System;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

using Lumina.Excel.Sheets;

using Callouts.Core.Engine;

namespace Callouts.Sources;

/// <summary>
/// Polls status effects each framework tick for self + party members + current target (the
/// definition of scope "anyone" — DESIGN.md §3.3) and emits Gained/Removed events by diffing
/// each actor's status-id set between sweeps. Status lists are read once per actor per sweep.
/// </summary>
public sealed class StatusSource : ITriggerSource
{
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IPartyList partyList;
    private readonly ITargetManager targetManager;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IPluginLog log;

    // actorId -> (statusId -> snapshot) from the previous sweep.
    private readonly Dictionary<ulong, Dictionary<uint, StatusSnap>> previous = new();
    private readonly HashSet<ulong> seenThisSweep = new();
    private readonly Dictionary<uint, StatusInfo> statusCache = new();
    private bool started;

    private readonly record struct StatusSnap(int Stacks, float Remaining);

    private readonly record struct StatusInfo(string Name, bool IsDebuff);

    public StatusSource(
        IObjectTable objectTable,
        IFramework framework,
        IPartyList partyList,
        ITargetManager targetManager,
        IDataManager dataManager,
        IClientState clientState,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.framework = framework;
        this.partyList = partyList;
        this.targetManager = targetManager;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.log = log;
    }

    public TriggerKind Kind => TriggerKind.Status;

    public SourceStatus Status { get; private set; } = SourceStatus.Disabled;

    public event Action<TriggerEvent>? OnEvent;

    public void Start()
    {
        if (this.started)
        {
            return;
        }

        this.framework.Update += this.OnUpdate;
        this.clientState.TerritoryChanged += this.OnTerritoryChanged;
        this.started = true;
        this.Status = SourceStatus.Active;
    }

    public void Stop()
    {
        if (!this.started)
        {
            return;
        }

        this.framework.Update -= this.OnUpdate;
        this.clientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.previous.Clear();
        this.started = false;
        this.Status = SourceStatus.Disabled;
    }

    public void Dispose() => this.Stop();

    // Actors all despawn on a zone change; drop baselines so re-entry re-seeds cleanly.
    private void OnTerritoryChanged(uint territoryType) => this.previous.Clear();

    private void OnUpdate(IFramework framework)
    {
        try
        {
            this.seenThisSweep.Clear();

            var local = this.objectTable.LocalPlayer;
            var localId = local?.GameObjectId ?? 0;
            var targetId = this.targetManager.Target?.GameObjectId ?? 0;
            var partyNames = this.BuildPartyNameSet();

            for (var i = 0; i < this.objectTable.Length; i++)
            {
                if (this.objectTable[i] is not IBattleChara chara)
                {
                    continue;
                }

                var id = chara.GameObjectId;
                var isSelf = id == localId;
                var isTarget = targetId != 0 && id == targetId;
                var inParty = partyNames.Contains(chara.Name.TextValue);

                if (!isSelf && !isTarget && !inParty)
                {
                    continue; // out of scope "anyone" (self + party + target)
                }

                this.seenThisSweep.Add(id);
                this.DiffActor(chara, id, isSelf, inParty, isTarget);
            }

            this.PruneDespawned();
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: status source sweep failed.");
        }
    }

    private void DiffActor(IBattleChara chara, ulong id, bool isSelf, bool inParty, bool isTarget)
    {
        var current = new Dictionary<uint, StatusSnap>();
        foreach (var status in chara.StatusList)
        {
            if (status is null || status.StatusId == 0)
            {
                continue;
            }

            current[status.StatusId] = new StatusSnap(status.Param, status.RemainingTime);
        }

        if (!this.previous.TryGetValue(id, out var prior))
        {
            // First time we see this actor: seed baseline without firing (avoids a burst of
            // "gained" events for everything already applied).
            this.previous[id] = current;
            return;
        }

        foreach (var (statusId, snap) in current)
        {
            if (!prior.ContainsKey(statusId))
            {
                this.Emit(chara, statusId, snap.Stacks, snap.Remaining, gained: true, isSelf, inParty, isTarget);
            }
        }

        foreach (var statusId in prior.Keys)
        {
            if (!current.ContainsKey(statusId))
            {
                this.Emit(chara, statusId, 0, 0, gained: false, isSelf, inParty, isTarget);
            }
        }

        this.previous[id] = current;
    }

    private void Emit(IBattleChara chara, uint statusId, int stacks, float remaining, bool gained, bool isSelf, bool inParty, bool isTarget)
    {
        var info = this.ResolveStatus(statusId);
        this.OnEvent?.Invoke(new TriggerEvent
        {
            Kind = TriggerKind.Status,
            StatusId = (int)statusId,
            StatusName = info.Name,
            StatusGained = gained,
            Stacks = stacks,
            BearerName = chara.Name.TextValue,
            BearerIsSelf = isSelf,
            BearerInParty = inParty,
            BearerIsTarget = isTarget,
            IsDebuff = info.IsDebuff,
            DurationSeconds = remaining,
        });
    }

    private void PruneDespawned()
    {
        if (this.previous.Count == 0)
        {
            return;
        }

        var stale = new List<ulong>();
        foreach (var id in this.previous.Keys)
        {
            if (!this.seenThisSweep.Contains(id))
            {
                stale.Add(id);
            }
        }

        foreach (var id in stale)
        {
            this.previous.Remove(id);
        }
    }

    private HashSet<string> BuildPartyNameSet()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < this.partyList.Length; i++)
        {
            var member = this.partyList[i];
            if (member is not null)
            {
                names.Add(member.Name.TextValue);
            }
        }

        return names;
    }

    private StatusInfo ResolveStatus(uint statusId)
    {
        if (this.statusCache.TryGetValue(statusId, out var cached))
        {
            return cached;
        }

        var info = new StatusInfo(string.Empty, false);
        if (this.dataManager.GetExcelSheet<Status>().TryGetRow(statusId, out var row))
        {
            // StatusCategory: 1 = beneficial (buff), 2 = detrimental (debuff).
            info = new StatusInfo(row.Name.ExtractText(), row.StatusCategory == 2);
        }

        this.statusCache[statusId] = info;
        return info;
    }
}
