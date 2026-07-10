using System;
using System.Collections.Generic;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;

using LuminaAction = Lumina.Excel.Sheets.Action;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

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
    private readonly Dictionary<uint, ActionInfo> actionCache = new();
    private bool started;

    private readonly record struct ActionInfo(string Name, AoeShape Shape, double Range);

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
        var info = this.ResolveAction(actionId);

        this.OnEvent?.Invoke(new TriggerEvent
        {
            Kind = TriggerKind.Cast,
            ActionId = (int)actionId,
            ActionName = info.Name,
            CasterName = chara.Name.TextValue,
            CasterIsEnemy = chara.ObjectKind == ObjectKind.BattleNpc,
            TargetIsSelf = targetId != 0 && targetId == localId,
            TargetInParty = !string.IsNullOrEmpty(targetName) && partyNames.Contains(targetName),
            CastTimeSeconds = chara.TotalCastTime,
            AoeShape = info.Shape,
            AoeRange = info.Range,
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

    private ActionInfo ResolveAction(uint actionId)
    {
        if (this.actionCache.TryGetValue(actionId, out var cached))
        {
            return cached;
        }

        var info = new ActionInfo(string.Empty, AoeShape.None, 0);
        if (this.dataManager.GetExcelSheet<LuminaAction>().TryGetRow(actionId, out var row))
        {
            info = new ActionInfo(row.Name.ExtractText(), MapShape(row.CastType), row.EffectRange);
        }

        this.actionCache[actionId] = info;
        return info;
    }

    // FFXIV Action.CastType → coarse AoE shape (best-effort; a maintained mapping).
    private static AoeShape MapShape(byte castType) => castType switch
    {
        1 => AoeShape.Single,
        2 or 5 or 7 => AoeShape.Circle,
        3 or 13 => AoeShape.Cone,
        4 or 8 or 12 => AoeShape.Line,
        10 => AoeShape.Donut,
        _ => AoeShape.None,
    };
}
