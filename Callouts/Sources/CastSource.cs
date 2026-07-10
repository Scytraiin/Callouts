using System;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

using LuminaAction = Lumina.Excel.Sheets.Action;

using Callouts.Core.Engine;

namespace Callouts.Sources;

/// <summary>
/// Polls the object table each framework tick for casting battle characters and emits a Cast
/// event when a new cast begins. Diffing is keyed on the cast <b>session</b>, not just the
/// action id: an actor's entry is cleared when it stops casting, so back-to-back recasts of the
/// same action fire twice (DESIGN.md §3.2). Entries for despawned actors are pruned each sweep.
/// </summary>
public sealed class CastSource : ITriggerSource
{
    private readonly IObjectTable objectTable;
    private readonly IFramework framework;
    private readonly IPartyList partyList;
    private readonly IDataManager dataManager;
    private readonly IClientState clientState;
    private readonly IPluginLog log;

    private readonly Dictionary<ulong, uint> activeCasts = new();
    private readonly HashSet<ulong> seenThisSweep = new();
    private readonly Dictionary<uint, string> actionNameCache = new();
    private bool started;

    public CastSource(IObjectTable objectTable, IFramework framework, IPartyList partyList, IDataManager dataManager, IClientState clientState, IPluginLog log)
    {
        this.objectTable = objectTable;
        this.framework = framework;
        this.partyList = partyList;
        this.dataManager = dataManager;
        this.clientState = clientState;
        this.log = log;
    }

    public TriggerKind Kind => TriggerKind.Cast;

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
        this.activeCasts.Clear();
        this.started = false;
        this.Status = SourceStatus.Disabled;
    }

    public void Dispose() => this.Stop();

    private void OnTerritoryChanged(uint territoryType) => this.activeCasts.Clear();

    private void OnUpdate(IFramework framework)
    {
        try
        {
            this.seenThisSweep.Clear();
            var localId = this.objectTable.LocalPlayer?.GameObjectId ?? 0;
            var partyNames = this.BuildPartyNameSet();

            for (var i = 0; i < this.objectTable.Length; i++)
            {
                if (this.objectTable[i] is not IBattleChara chara)
                {
                    continue;
                }

                var id = chara.GameObjectId;
                this.seenThisSweep.Add(id);

                if (!chara.IsCasting)
                {
                    this.activeCasts.Remove(id);
                    continue;
                }

                var actionId = chara.CastActionId;
                if (this.activeCasts.TryGetValue(id, out var previous) && previous == actionId)
                {
                    continue; // same ongoing cast — already emitted
                }

                this.activeCasts[id] = actionId;
                this.Emit(chara, actionId, localId, partyNames);
            }

            this.PruneDespawned();
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: cast source sweep failed.");
        }
    }

    private void Emit(IBattleChara chara, uint actionId, ulong localId, HashSet<string> partyNames)
    {
        var targetId = chara.CastTargetObjectId;
        var targetName = targetId != 0 ? this.objectTable.SearchById(targetId)?.Name.TextValue ?? string.Empty : string.Empty;

        this.OnEvent?.Invoke(new TriggerEvent
        {
            Kind = TriggerKind.Cast,
            ActionId = (int)actionId,
            ActionName = this.ResolveActionName(actionId),
            CasterName = chara.Name.TextValue,
            CasterIsEnemy = chara.ObjectKind == ObjectKind.BattleNpc,
            TargetIsSelf = targetId != 0 && targetId == localId,
            TargetInParty = !string.IsNullOrEmpty(targetName) && partyNames.Contains(targetName),
        });
    }

    private void PruneDespawned()
    {
        if (this.activeCasts.Count == 0)
        {
            return;
        }

        var stale = new List<ulong>();
        foreach (var id in this.activeCasts.Keys)
        {
            if (!this.seenThisSweep.Contains(id))
            {
                stale.Add(id);
            }
        }

        foreach (var id in stale)
        {
            this.activeCasts.Remove(id);
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

    private string ResolveActionName(uint actionId)
    {
        if (this.actionNameCache.TryGetValue(actionId, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        if (this.dataManager.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row))
        {
            name = row.Name.ExtractText();
        }

        this.actionNameCache[actionId] = name;
        return name;
    }
}
