using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>The kind of game event a rule reacts to.</summary>
public enum TriggerKind
{
    Chat,
    Cast,
    Status,
    DutyEvent,
    Vfx,
    HeadMarker,
}

/// <summary>
/// A normalized, Dalamud-free game event produced by a trigger source and consumed by the
/// <see cref="RuleEngine"/>. It carries a superset of fields; each matcher reads only the ones
/// relevant to its kind. Sources translate their Dalamud-specific inputs into this plain record
/// so the engine and matchers stay unit-testable.
/// </summary>
public sealed record TriggerEvent
{
    public required TriggerKind Kind { get; init; }

    // --- Context (all kinds, enriched at dispatch) ---
    public string Zone { get; init; } = string.Empty;
    public uint TerritoryId { get; init; }
    public bool InCombat { get; init; }
    public bool InDuty { get; init; }

    // --- Chat ---
    public int Channel { get; init; }
    public string Sender { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;

    // --- Cast ---
    public int ActionId { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public string CasterName { get; init; } = string.Empty;
    public bool CasterIsEnemy { get; init; }
    public bool TargetIsSelf { get; init; }
    public bool TargetInParty { get; init; }
    public double CastTimeSeconds { get; init; }          // enrichment (issue 019)
    public AoeShape AoeShape { get; init; } = AoeShape.None;
    public double AoeRange { get; init; }

    // --- Status ---
    public int StatusId { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public bool StatusGained { get; init; }
    public int Stacks { get; init; }
    public string BearerName { get; init; } = string.Empty;
    public bool BearerIsSelf { get; init; }
    public bool BearerInParty { get; init; }
    public bool BearerIsTarget { get; init; }
    public bool IsDebuff { get; init; }                   // enrichment (issue 019)
    public double DurationSeconds { get; init; }

    // --- Duty ---
    public DutyEventFilter DutyEvent { get; init; } = DutyEventFilter.Any;

    // --- Advanced (VFX / head markers, issues 014/015) ---
    public string VfxPath { get; init; } = string.Empty;
    public string MarkerKey { get; init; } = string.Empty;
    public string RawValue { get; init; } = string.Empty;
}
