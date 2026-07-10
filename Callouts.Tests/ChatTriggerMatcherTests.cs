using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class ChatTriggerMatcherTests
{
    private static Rule ChatRule(string pattern, bool caseSensitive = false, string? sender = null, params int[] channels)
        => new()
        {
            Name = "test",
            Source = new SourceSpec
            {
                Kind = TriggerKind.Chat,
                Pattern = pattern,
                CaseSensitive = caseSensitive,
                SenderPattern = sender,
                Channels = [.. channels],
            },
        };

    private static TriggerEvent ChatEvent(string message, int channel = 10, string senderName = "Someone")
        => new() { Kind = TriggerKind.Chat, Channel = channel, Sender = senderName, Message = message };

    [Fact]
    public void Contains_Matches_CaseInsensitiveByDefault()
    {
        var result = ChatTriggerMatcher.Match(
            ChatRule("ready check"),
            ChatEvent("Krile has initiated a READY CHECK."));

        Assert.NotNull(result);
        Assert.Equal("Krile has initiated a READY CHECK.", result!.Message);
    }

    [Fact]
    public void Contains_CaseSensitive_RequiresExactCasing()
    {
        Assert.Null(ChatTriggerMatcher.Match(
            ChatRule("ready check", caseSensitive: true),
            ChatEvent("A READY CHECK.")));

        Assert.NotNull(ChatTriggerMatcher.Match(
            ChatRule("ready check", caseSensitive: true),
            ChatEvent("a ready check.")));
    }

    [Fact]
    public void NonMatchingText_DoesNotMatch()
    {
        Assert.Null(ChatTriggerMatcher.Match(ChatRule("ready check"), ChatEvent("pull in 5")));
    }

    [Fact]
    public void ChannelFilter_Empty_MatchesAnyChannel()
    {
        Assert.NotNull(ChatTriggerMatcher.Match(ChatRule("pull"), ChatEvent("pull in 5", channel: 14)));
    }

    [Fact]
    public void ChannelFilter_RestrictsToListedChannels()
    {
        var rule = ChatRule("pull", false, null, 14); // Party only

        Assert.NotNull(ChatTriggerMatcher.Match(rule, ChatEvent("pull in 5", channel: 14)));
        Assert.Null(ChatTriggerMatcher.Match(rule, ChatEvent("pull in 5", channel: 10))); // Say
    }

    [Fact]
    public void SenderFilter_RestrictsBySenderContains()
    {
        var rule = ChatRule("pull", false, "Krile");

        Assert.NotNull(ChatTriggerMatcher.Match(rule, ChatEvent("pull in 5", senderName: "Krile Baldesion")));
        Assert.Null(ChatTriggerMatcher.Match(rule, ChatEvent("pull in 5", senderName: "Alphinaud")));
    }

    [Fact]
    public void EchoChannel_IsNeverMatchable_AntiLoop()
    {
        // Even a rule that would otherwise match must not fire on the Echo channel.
        var rule = ChatRule("ready check");
        var echoEvent = ChatEvent("Ready check reminder!", channel: ChatChannels.Echo);

        Assert.Null(ChatTriggerMatcher.Match(rule, echoEvent));
    }

    [Fact]
    public void NonChatEvent_DoesNotMatchChatRule()
    {
        var rule = ChatRule("x");
        var evt = new TriggerEvent { Kind = (TriggerKind)999, Channel = 10, Message = "x" };

        Assert.Null(ChatTriggerMatcher.Match(rule, evt));
    }
}
