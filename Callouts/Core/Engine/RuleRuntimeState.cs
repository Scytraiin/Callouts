using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// Whether a rule actually runs — computed, never persisted (DESIGN.md §4.1). The engine
/// never mutates <see cref="Rule.Enabled"/>; a regex failure records a RuleError separately,
/// so user intent always survives sessions, patches, and errors.
/// </summary>
public enum RuleRuntimeState
{
    Active,
    DisabledByUser,
    RuleError,
    BlockedAdvancedOff,
    SourceFailed,
}

public static class RuleRuntimeStateEvaluator
{
    /// <summary>
    /// Evaluates runtime state from user intent, an optional engine error, and source
    /// availability. <paramref name="sourceAvailable"/> is true for all stable sources; the
    /// advanced tier passes false when its master toggle is off or the hook failed (issue 014).
    /// </summary>
    public static RuleRuntimeState Evaluate(
        Rule rule,
        bool hasError = false,
        bool sourceAvailable = true,
        bool blockedAdvancedOff = false)
    {
        if (!rule.Enabled)
        {
            return RuleRuntimeState.DisabledByUser;
        }

        if (blockedAdvancedOff)
        {
            return RuleRuntimeState.BlockedAdvancedOff;
        }

        if (!sourceAvailable)
        {
            return RuleRuntimeState.SourceFailed;
        }

        return hasError ? RuleRuntimeState.RuleError : RuleRuntimeState.Active;
    }
}
