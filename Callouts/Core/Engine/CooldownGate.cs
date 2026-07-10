using System;
using System.Collections.Generic;

namespace Callouts.Core.Engine;

/// <summary>
/// Per-rule cooldown: after a rule fires it cannot fire again until its cooldown elapses.
/// Uses an injected clock so timing is deterministic in tests (DESIGN.md §4.3).
/// </summary>
public sealed class CooldownGate
{
    private readonly Dictionary<string, DateTime> lastFired = new();

    public bool IsReady(string ruleId, double cooldownSeconds, DateTime now)
    {
        if (cooldownSeconds <= 0)
        {
            return true;
        }

        if (!this.lastFired.TryGetValue(ruleId, out var last))
        {
            return true;
        }

        return (now - last).TotalSeconds >= cooldownSeconds;
    }

    public void Record(string ruleId, DateTime now) => this.lastFired[ruleId] = now;

    public void Forget(string ruleId) => this.lastFired.Remove(ruleId);
}
