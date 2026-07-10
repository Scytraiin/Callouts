using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class ReviewFixesTests
{
    [Fact]
    public void Vfx_TargetScope_UsesBearerIsTarget_NotMatchAll()
    {
        var rule = new Rule { Source = new SourceSpec { Kind = TriggerKind.Vfx, VfxPathPattern = "lockon", ActorScope = BearerScope.Target } };

        // Target scope must require BearerIsTarget, not silently match every actor.
        Assert.NotNull(VfxTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "vfx/lockon/x", BearerIsTarget = true }));
        Assert.Null(VfxTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "vfx/lockon/x", BearerIsTarget = false }));
    }

    [Fact]
    public void Validator_CooldownOutOfRange_Rejected()
    {
        var rule = new Rule
        {
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "x" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "t" } },
        };

        rule.CooldownSeconds = 700;
        Assert.False(RuleValidator.IsValid(rule));

        rule.CooldownSeconds = -1;
        Assert.False(RuleValidator.IsValid(rule));

        rule.CooldownSeconds = 600;
        Assert.True(RuleValidator.IsValid(rule));

        rule.CooldownSeconds = 0;
        Assert.True(RuleValidator.IsValid(rule));
    }

    [Fact]
    public void RateLimiter_Reconfigure_AppliesNewRate()
    {
        var t0 = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);

        // Start at 1/sec: after draining, only ~1 token returns per second.
        var limiter = new RateLimiter(capacity: 1, refillPerSecond: 1);
        Assert.True(limiter.TryAcquire(t0));
        Assert.False(limiter.TryAcquire(t0));

        // Raise to 5/sec; after one second the higher refill grants multiple tokens.
        limiter.Reconfigure(5, 5);
        var t1 = t0.AddSeconds(1);
        Assert.True(limiter.TryAcquire(t1));
        Assert.True(limiter.TryAcquire(t1));
        Assert.True(limiter.TryAcquire(t1)); // >1 in the same instant proves the new rate took effect
    }

    [Fact]
    public void Engine_NoOutputRule_DoesNotConsumeCooldownOrToken()
    {
        var rule = new Rule
        {
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "pull" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = false } }, // nothing enabled
        };
        var engine = new RuleEngine(() => new[] { rule });

        var actions = engine.Process(new TriggerEvent { Kind = TriggerKind.Chat, Channel = 10, Message = "pull" });

        Assert.Empty(actions);
        Assert.Equal(0, engine.GetFireCount(rule.Id));
        Assert.Equal(0, engine.RateLimiter.DroppedCount); // never reached the limiter
    }
}
