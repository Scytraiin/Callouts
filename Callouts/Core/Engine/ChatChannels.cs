namespace Callouts.Core.Engine;

/// <summary>
/// Chat-channel constants the Dalamud-free core needs. <see cref="Echo"/> is the numeric
/// value of XivChatType.Echo (verified against the Dalamud 15 source) and is the anchor of
/// the anti-loop guarantee: the Echo channel is never matchable, so the plugin's own Echo
/// output can never re-trigger a rule (DESIGN.md §3.1, PRD FR-6). This is enforced here in
/// the core (unit-testable) as well as dropped early by the ChatSource adapter.
/// </summary>
public static class ChatChannels
{
    public const int Echo = 56;
}
