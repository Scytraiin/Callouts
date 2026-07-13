using System.Collections.Generic;

using Callouts.Core.Engine;

namespace Callouts.Core.Config;

/// <summary>
/// Plugin-wide options (persisted in <c>Configuration</c>). The full settings UI lands with
/// issue 013; the fields exist here so the config shape is stable from the start.
/// </summary>
public sealed class GlobalOptions
{
    /// <summary>Global kill switch (issue 013). When false the engine matches nothing.</summary>
    public bool MasterEnabled { get; set; } = true;

    /// <summary>Advanced (hook-based) sources master toggle, default off (issue 014).</summary>
    public bool AdvancedSourcesEnabled { get; set; }

    /// <summary>Global alert rate limit (alerts/second).</summary>
    public double RateLimitPerSecond { get; set; } = 10;

    /// <summary>Default cooldown (seconds) applied to newly created rules.</summary>
    public double DefaultCooldownSeconds { get; set; } = 2.0;

    /// <summary>Default per-category log limit — each event category keeps its own most-recent N.</summary>
    public int EventLogDefaultLimit { get; set; } = 20000;

    /// <summary>Optional per-category overrides for the log limit (empty = use the default).</summary>
    public Dictionary<EventCategory, int> EventCategoryLimits { get; set; } = [];

    /// <summary>Suggestion keys the user dismissed; never re-suggested (issue 020).</summary>
    public List<string> IgnoredSuggestionKeys { get; set; } = [];

    /// <summary>Open the Suggestions window automatically when combat ends (issue 020).</summary>
    public bool AutoOpenSuggestions { get; set; }
}
