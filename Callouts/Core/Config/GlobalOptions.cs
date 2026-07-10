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

    /// <summary>Live-events ring buffer size (issue 010).</summary>
    public int EventBufferSize { get; set; } = 200;
}
