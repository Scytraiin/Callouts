using System;
using System.Collections.Generic;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class RuleEngineAdvancedTests
{
    private sealed class TestClock
    {
        public DateTime Current;

        public TestClock(DateTime start) => this.Current = start;

        public DateTime Now() => this.Current;

        public void Advance(double seconds) => this.Current = this.Current.AddSeconds(seconds);
    }

    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Rule ChatRule(
        string pattern,
        MatchMode mode = MatchMode.Contains,
        double cooldown = 0,
        Action<OutputSpec>? outputs = null)
    {
        var rule = new Rule
        {
            Name = "test",
            CooldownSeconds = cooldown,
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = pattern, MatchMode = mode },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "hit" } },
        };
        outputs?.Invoke(rule.Outputs);
        return rule;
    }

    private static TriggerEvent Chat(string message, int channel = 10, string sender = "Someone")
        => new() { Kind = TriggerKind.Chat, Channel = channel, Sender = sender, Message = message };

    [Fact]
    public void Regex_CapturesFlowIntoPlaceholders()
    {
        var rule = ChatRule(@"pull in (\d+)", MatchMode.Regex, outputs: o => o.Echo.Text = "Pulling in $1!");
        var engine = new RuleEngine(() => new[] { rule });

        var actions = engine.Process(Chat("pull in 5"));

        var action = Assert.Single(actions);
        Assert.Equal("Pulling in 5!", action.Text);
    }

    [Fact]
    public void NamedPlaceholder_Sender_IsRendered()
    {
        var rule = ChatRule("ready check", outputs: o => o.Echo.Text = "Ready from {sender}!");
        var engine = new RuleEngine(() => new[] { rule });

        var actions = engine.Process(Chat("has initiated a ready check", sender: "Krile"));

        Assert.Equal("Ready from Krile!", Assert.Single(actions).Text);
    }

    [Fact]
    public void InvalidRegex_RecordsRuleError_AndDoesNotThrow()
    {
        var rule = ChatRule("(unclosed", MatchMode.Regex);
        var engine = new RuleEngine(() => new[] { rule });

        var actions = engine.Process(Chat("anything"));

        Assert.Empty(actions);
        Assert.Equal(RuleRuntimeState.RuleError, engine.GetRuntimeState(rule));
        Assert.True(engine.TryGetError(rule.Id, out var message));
        Assert.Contains("regex", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FixingRegex_ClearsRuleError()
    {
        var rule = ChatRule("(unclosed", MatchMode.Regex);
        var engine = new RuleEngine(() => new[] { rule });
        engine.Process(Chat("x"));
        Assert.Equal(RuleRuntimeState.RuleError, engine.GetRuntimeState(rule));

        rule.Source.Pattern = "closed";
        engine.Process(Chat("closed door"));

        Assert.Equal(RuleRuntimeState.Active, engine.GetRuntimeState(rule));
    }

    [Fact]
    public void Cooldown_PreventsRefireUntilElapsed()
    {
        var clock = new TestClock(Start);
        var rule = ChatRule("pull", cooldown: 2.0);
        var engine = new RuleEngine(() => new[] { rule }, clock.Now);

        Assert.Single(engine.Process(Chat("pull")));
        Assert.Empty(engine.Process(Chat("pull")));  // within cooldown
        clock.Advance(2.0);
        Assert.Single(engine.Process(Chat("pull")));
    }

    [Fact]
    public void RateLimit_DropsExcessFires()
    {
        var rules = new[]
        {
            ChatRule("pull"),
            ChatRule("pull"),
            ChatRule("pull"),
        };
        var engine = new RuleEngine(() => rules, null, new RateLimiter(capacity: 2, refillPerSecond: 0));

        var actions = engine.Process(Chat("pull"));

        Assert.Equal(2, actions.Count);
        Assert.Equal(1, engine.RateLimiter.DroppedCount);
    }

    [Fact]
    public void MasterDisabled_ProducesNothing()
    {
        var engine = new RuleEngine(() => new[] { ChatRule("pull") }) { MasterEnabled = false };

        Assert.Empty(engine.Process(Chat("pull")));
    }

    [Fact]
    public void AllThreeOutputs_ProduceThreeActions()
    {
        var rule = ChatRule("pull", outputs: o =>
        {
            o.Echo.Text = "go";
            o.Sound.Enabled = true;
            o.Sound.EffectId = 6;
            o.Toast.Enabled = true;
            o.Toast.Text = "PULL";
            o.Toast.Style = ToastStyle.Error;
        });
        var engine = new RuleEngine(() => new[] { rule });

        var actions = engine.Process(Chat("pull"));

        Assert.Equal(3, actions.Count);
        Assert.Contains(actions, a => a.Kind == AlertOutputKind.Echo && a.Text == "go");
        Assert.Contains(actions, a => a.Kind == AlertOutputKind.Sound && a.SoundEffectId == 6);
        Assert.Contains(actions, a => a.Kind == AlertOutputKind.Toast && a.Text == "PULL" && a.ToastStyle == ToastStyle.Error);
    }

    [Fact]
    public void FireCount_IncrementsOnRealFire_NotOnTestFire()
    {
        var rule = ChatRule("pull");
        var engine = new RuleEngine(() => new[] { rule });

        engine.Process(Chat("pull"));
        Assert.Equal(1, engine.GetFireCount(rule.Id));

        // Test-fire bypasses gates and does not touch the counter.
        var test = engine.BuildTestActions(rule, MatchResult.FromValues(("sender", "T")));
        Assert.NotEmpty(test);
        Assert.Equal(1, engine.GetFireCount(rule.Id));
    }

    [Fact]
    public void TestFire_BypassesCooldown()
    {
        var rule = ChatRule("pull", cooldown: 999, outputs: o => o.Echo.Text = "go");
        var engine = new RuleEngine(() => new[] { rule });

        engine.Process(Chat("pull")); // starts cooldown
        var test = engine.BuildTestActions(rule, MatchResult.FromValues());

        Assert.Equal("go", Assert.Single(test).Text);
    }
}
