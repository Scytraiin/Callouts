namespace Callouts.Core.Engine;

/// <summary>
/// The kind of game event a rule reacts to. Only <see cref="Chat"/> is wired in the
/// first release; the other kinds arrive with their respective trigger sources.
/// </summary>
public enum TriggerKind
{
    Chat,

    // Cast, Status, DutyEvent, Vfx, HeadMarker -> later issues (007/008/009/014/015)
}

/// <summary>
/// A normalized, Dalamud-free game event produced by a trigger source and consumed by
/// the <see cref="RuleEngine"/>. Sources translate their Dalamud-specific inputs into
/// this plain record so the engine and matchers stay unit-testable.
/// </summary>
public sealed record TriggerEvent
{
    public required TriggerKind Kind { get; init; }

    /// <summary>Chat channel as the numeric XivChatType value (e.g. Say = 10). 0 for non-chat events.</summary>
    public int Channel { get; init; }

    public string Sender { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
