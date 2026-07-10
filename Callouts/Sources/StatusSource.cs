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
    private readonly IPluginLog log;

    // actorId -> (statusId -> stacks) from the previous sweep.
    private readonly Dictionary<ulong, Dictionary<uint, int>> previous = new();
    private readonly HashSet<ulong> seenThisSweep = new();
    private readonly Dictionary<uint, string> statusNameCache = new();
    private bool started;

    public StatusSource(
        IObjectTable objectTable,
        IFramework framework,
        IPartyList partyList,
        ITargetManager targetManager,
        IDataManager dataManager,
        IPluginLog log)
    {
        this.objectTable = objectTable;
        this.framework = framework;
        this.partyList = partyList;
        this.targetManager = targetManager;
        this.dataManager = dataManager;
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
        this.previous.Clear();
        this.started = false;
        this.Status = SourceStatus.Disabled;
    }

    public void Dispose() => this.Stop();

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
        var current = new Dictionary<uint, int>();
        foreach (var status in chara.StatusList)
        {
            if (status is null || status.StatusId == 0)
            {
                continue;
            }

            current[status.StatusId] = status.Param;
        }

        if (!this.previous.TryGetValue(id, out var prior))
        {
            // First time we see this actor: seed baseline without firing (avoids a burst of
            // "gained" events for everything already applied).
            this.previous[id] = current;
            return;
        }

        foreach (var (statusId, stacks) in current)
        {
            if (!prior.ContainsKey(statusId))
            {
                this.Emit(chara, statusId, stacks, gained: true, isSelf, inParty, isTarget);
            }
        }

        foreach (var statusId in prior.Keys)
        {
            if (!current.ContainsKey(statusId))
            {
                this.Emit(chara, statusId, 0, gained: false, isSelf, inParty, isTarget);
            }
        }

        this.previous[id] = current;
    }

    private void Emit(IBattleChara chara, uint statusId, int stacks, bool gained, bool isSelf, bool inParty, bool isTarget)
    {
        this.OnEvent?.Invoke(new TriggerEvent
        {
            Kind = TriggerKind.Status,
            StatusId = (int)statusId,
            StatusName = this.ResolveStatusName(statusId),
            StatusGained = gained,
            Stacks = stacks,
            BearerName = chara.Name.TextValue,
            BearerIsSelf = isSelf,
            BearerInParty = inParty,
            BearerIsTarget = isTarget,
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

    private string ResolveStatusName(uint statusId)
    {
        if (this.statusNameCache.TryGetValue(statusId, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        if (this.dataManager.GetExcelSheet<Status>().TryGetRow(statusId, out var row))
        {
            name = row.Name.ExtractText();
        }

        this.statusNameCache[statusId] = name;
        return name;
    }
}
