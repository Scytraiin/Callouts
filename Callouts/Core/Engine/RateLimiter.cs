using System;

namespace Callouts.Core.Engine;

/// <summary>
/// Global token bucket that caps how many alerts fire per second across all rules, so a busy
/// pull can never spam the player (DESIGN.md §4.3). Drops are counted and surfaced in the Live
/// events window.
/// </summary>
public sealed class RateLimiter
{
    private readonly double capacity;
    private readonly double refillPerSecond;

    private double tokens;
    private DateTime lastRefill;
    private bool initialized;

    public RateLimiter(double capacity = 10, double refillPerSecond = 10)
    {
        this.capacity = capacity;
        this.refillPerSecond = refillPerSecond;
        this.tokens = capacity;
    }

    public long DroppedCount { get; private set; }

    public bool TryAcquire(DateTime now)
    {
        if (!this.initialized)
        {
            this.lastRefill = now;
            this.initialized = true;
        }

        var elapsed = (now - this.lastRefill).TotalSeconds;
        if (elapsed > 0)
        {
            this.tokens = Math.Min(this.capacity, this.tokens + (elapsed * this.refillPerSecond));
            this.lastRefill = now;
        }

        if (this.tokens >= 1)
        {
            this.tokens -= 1;
            return true;
        }

        this.DroppedCount++;
        return false;
    }
}
