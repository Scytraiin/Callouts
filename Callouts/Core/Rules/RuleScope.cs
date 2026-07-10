using System.Collections.Generic;

namespace Callouts.Core.Rules;

/// <summary>Where/when a rule is allowed to fire (DESIGN.md §4.1). Empty = everywhere, always.</summary>
public sealed class RuleScope
{
    /// <summary>Territory (zone) ids the rule is limited to. Empty = any zone.</summary>
    public List<uint> TerritoryIds { get; set; } = [];

    public bool OnlyInCombat { get; set; }

    public bool OnlyInDuty { get; set; }

    public RuleScope Clone() => new()
    {
        TerritoryIds = [.. this.TerritoryIds],
        OnlyInCombat = this.OnlyInCombat,
        OnlyInDuty = this.OnlyInDuty,
    };
}
