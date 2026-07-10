using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// Whether a rule actually runs, computed (never persisted) from user intent and system
/// health. Issue 002 only distinguishes user intent; the blocked/failed/error states are
/// added by issues 004 and 014.
/// </summary>
public enum RuleRuntimeState
{
    Active,
    DisabledByUser,

    // BlockedAdvancedOff, SourceFailed, RuleError -> issues 004 / 014
}

public static class RuleRuntimeStateEvaluator
{
    public static RuleRuntimeState Evaluate(Rule rule)
        => rule.Enabled ? RuleRuntimeState.Active : RuleRuntimeState.DisabledByUser;

    public static bool IsActive(Rule rule) => Evaluate(rule) == RuleRuntimeState.Active;
}
