namespace Callouts.Core.Engine;

/// <summary>Which output sink should execute an alert. Sound and Toast arrive in issue 006.</summary>
public enum AlertOutputKind
{
    Echo,

    // Sound, Toast -> issue 006
}

/// <summary>
/// A single output the engine wants executed for a matched rule. Sinks are the only code
/// that turns these into Dalamud calls.
/// </summary>
public sealed record AlertAction
{
    public required AlertOutputKind Kind { get; init; }

    public required string Text { get; init; }
}
