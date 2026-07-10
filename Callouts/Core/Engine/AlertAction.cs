using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Which output sink should execute an alert.</summary>
public enum AlertOutputKind
{
    Echo,
    Sound,
    Toast,
}

/// <summary>
/// A single output the engine wants executed for a matched rule, with placeholders already
/// rendered. Sinks read only the fields relevant to their kind. Sinks are the only code that
/// turns these into Dalamud/game calls.
/// </summary>
public sealed record AlertAction
{
    public required AlertOutputKind Kind { get; init; }

    /// <summary>Rendered text for Echo/Toast. Unused for Sound.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>1..16 for Sound. Unused otherwise.</summary>
    public int SoundEffectId { get; init; }

    /// <summary>Presentation style for Toast. Unused otherwise.</summary>
    public ToastStyle ToastStyle { get; init; }
}
