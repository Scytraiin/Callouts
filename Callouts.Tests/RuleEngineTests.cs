using System.Collections.Generic;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class RuleEngineTests
{
    private static Rule EchoRule(string pattern, string echoText, bool enabled = true, bool echoEnabled = true)
        => new()
        {
            Name = "test",
            Enabled = enabled,
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = pattern },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = echoEnabled, Text = echoText } },
        };

    private static TriggerEvent ChatEvent(string message, int channel = 10)
        => new() { Kind = TriggerKind.Chat, Channel = channel, Sender = "Someone", Message = message };

    private static RuleEngine EngineWith(params Rule[] rules)
        => new(() => rules);

    [Fact]
    public void ActiveMatchingRule_ProducesEchoAction()
    {
        var engine = EngineWith(EchoRule("ready check", "Ready check!"));

        var actions = engine.Process(ChatEvent("has initiated a ready check"));

        var action = Assert.Single(actions);
        Assert.Equal(AlertOutputKind.Echo, action.Kind);
        Assert.Equal("Ready check!", action.Text);
    }

    [Fact]
    public void DisabledRule_ProducesNothing()
    {
        var engine = EngineWith(EchoRule("ready check", "Ready check!", enabled: false));

        Assert.Empty(engine.Process(ChatEvent("has initiated a ready check")));
    }

    [Fact]
    public void EchoDisabledOutput_ProducesNothing()
    {
        var engine = EngineWith(EchoRule("ready check", "Ready check!", echoEnabled: false));

        Assert.Empty(engine.Process(ChatEvent("has initiated a ready check")));
    }

    [Fact]
    public void NonMatchingEvent_ProducesNothing()
    {
        var engine = EngineWith(EchoRule("ready check", "Ready check!"));

        Assert.Empty(engine.Process(ChatEvent("pull in 5")));
    }

    [Fact]
    public void EchoOutput_OnEchoChannel_DoesNotReFire_AntiLoop()
    {
        // Simulates the plugin's own Echo output re-entering the pipeline: it must be dropped.
        var engine = EngineWith(EchoRule("Ready check!", "Ready check!"));

        Assert.Empty(engine.Process(ChatEvent("Ready check!", channel: ChatChannels.Echo)));
    }

    [Fact]
    public void MultipleRules_AllMatchingActiveOnesFire()
    {
        var engine = EngineWith(
            EchoRule("pull", "Pulling!"),
            EchoRule("pull", "Get ready!", enabled: false),
            EchoRule("pull", "Go go go!"));

        var actions = engine.Process(ChatEvent("pull in 5"));

        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, a => a.Text == "Pulling!");
        Assert.Contains(actions, a => a.Text == "Go go go!");
    }
}
