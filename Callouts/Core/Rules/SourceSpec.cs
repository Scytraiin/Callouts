using System.Collections.Generic;

using Callouts.Core.Engine;

namespace Callouts.Core.Rules;

/// <summary>
/// Persisted description of what a rule listens for. A mutable POCO so it can be serialized by
/// the Dalamud plugin configuration. Fields are grouped by the source <see cref="Kind"/>; only
/// the fields for the selected kind are meaningful.
/// </summary>
public sealed class SourceSpec
{
    public TriggerKind Kind { get; set; } = TriggerKind.Chat;

    // --- Chat ---
    public List<int> Channels { get; set; } = [];
    public MatchMode MatchMode { get; set; } = MatchMode.Contains;
    public string Pattern { get; set; } = string.Empty;
    public bool CaseSensitive { get; set; }
    public string? SenderPattern { get; set; }

    // --- Cast ---
    public int ActionId { get; set; }                 // 0 = any
    public string? ActionNameContains { get; set; }
    public CasterScope CasterScope { get; set; } = CasterScope.Anyone;
    public string? CasterNameContains { get; set; }
    public bool OnlyTargetingMe { get; set; }
    public bool OnlyTargetingParty { get; set; }

    // --- Status ---
    public int StatusId { get; set; }                 // 0 = any
    public string? StatusNameContains { get; set; }
    public StatusChangeFilter StatusChange { get; set; } = StatusChangeFilter.Gained;
    public BearerScope Bearer { get; set; } = BearerScope.Self;
    public int MinStacks { get; set; }

    /// <summary>Only match a gained status whose applied timer is at least this many seconds (0 = no lower bound).</summary>
    public double MinDurationSeconds { get; set; }

    /// <summary>Only match a gained status whose applied timer is at most this many seconds (0 = no upper bound).</summary>
    public double MaxDurationSeconds { get; set; }

    // --- Duty ---
    public DutyEventFilter DutyEvent { get; set; } = DutyEventFilter.Any;

    // --- Advanced (issues 014/015) ---
    public string? VfxPathPattern { get; set; }
    public MatchMode VfxMatchMode { get; set; } = MatchMode.Contains;
    public BearerScope ActorScope { get; set; } = BearerScope.Anyone;
    public string? MarkerKey { get; set; }

    public SourceSpec Clone() => new()
    {
        Kind = this.Kind,
        Channels = [.. this.Channels],
        MatchMode = this.MatchMode,
        Pattern = this.Pattern,
        CaseSensitive = this.CaseSensitive,
        SenderPattern = this.SenderPattern,
        ActionId = this.ActionId,
        ActionNameContains = this.ActionNameContains,
        CasterScope = this.CasterScope,
        CasterNameContains = this.CasterNameContains,
        OnlyTargetingMe = this.OnlyTargetingMe,
        OnlyTargetingParty = this.OnlyTargetingParty,
        StatusId = this.StatusId,
        StatusNameContains = this.StatusNameContains,
        StatusChange = this.StatusChange,
        Bearer = this.Bearer,
        MinStacks = this.MinStacks,
        MinDurationSeconds = this.MinDurationSeconds,
        MaxDurationSeconds = this.MaxDurationSeconds,
        DutyEvent = this.DutyEvent,
        VfxPathPattern = this.VfxPathPattern,
        VfxMatchMode = this.VfxMatchMode,
        ActorScope = this.ActorScope,
        MarkerKey = this.MarkerKey,
    };
}
