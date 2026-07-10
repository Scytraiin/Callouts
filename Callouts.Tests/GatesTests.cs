using System;

using Callouts.Core.Engine;

using Xunit;

namespace Callouts.Tests;

public sealed class GatesTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Cooldown_BlocksUntilElapsed()
    {
        var gate = new CooldownGate();

        Assert.True(gate.IsReady("r", 2.0, T0));
        gate.Record("r", T0);

        Assert.False(gate.IsReady("r", 2.0, T0.AddSeconds(1)));
        Assert.True(gate.IsReady("r", 2.0, T0.AddSeconds(2)));
    }

    [Fact]
    public void Cooldown_ZeroOrNegative_AlwaysReady()
    {
        var gate = new CooldownGate();
        gate.Record("r", T0);

        Assert.True(gate.IsReady("r", 0, T0));
    }

    [Fact]
    public void RateLimiter_DropsOverCapacity_AndCountsDrops()
    {
        var limiter = new RateLimiter(capacity: 2, refillPerSecond: 0);

        Assert.True(limiter.TryAcquire(T0));
        Assert.True(limiter.TryAcquire(T0));
        Assert.False(limiter.TryAcquire(T0));
        Assert.Equal(1, limiter.DroppedCount);
    }

    [Fact]
    public void RateLimiter_RefillsOverTime()
    {
        var limiter = new RateLimiter(capacity: 1, refillPerSecond: 1);

        Assert.True(limiter.TryAcquire(T0));
        Assert.False(limiter.TryAcquire(T0));
        Assert.True(limiter.TryAcquire(T0.AddSeconds(1)));
    }
}
