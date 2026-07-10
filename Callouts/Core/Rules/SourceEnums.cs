namespace Callouts.Core.Rules;

/// <summary>Which casters a cast rule accepts.</summary>
public enum CasterScope
{
    Anyone,
    Enemy,
}

/// <summary>Which status change a status rule reacts to.</summary>
public enum StatusChangeFilter
{
    Gained,
    Removed,
    Either,
}

/// <summary>Whose status/marker a rule watches (DESIGN.md §3.3 — "anyone" = self+party+target).</summary>
public enum BearerScope
{
    Self,
    Party,
    Target,
    Anyone,
}

/// <summary>Duty lifecycle events. <see cref="Any"/> matches all of them.</summary>
public enum DutyEventFilter
{
    Any,
    Started,
    Wiped,
    Recommenced,
    Completed,
}

/// <summary>Coarse AoE shape derived from the Lumina Action sheet (issue 019, Suggestions).</summary>
public enum AoeShape
{
    None,
    Single,
    Circle,
    Cone,
    Line,
    Donut,
}
