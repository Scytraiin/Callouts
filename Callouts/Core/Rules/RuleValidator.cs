using System.Collections.Generic;

namespace Callouts.Core.Rules;

/// <summary>
/// Validates a rule before it can be saved. Returns human-readable reasons that the editor
/// shows inline next to Save (DESIGN.md §7.2 — never a silently disabled button).
/// </summary>
public static class RuleValidator
{
    public static IReadOnlyList<string> Validate(Rule rule)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rule.Source.Pattern))
        {
            errors.Add("Add a pattern to enable Save.");
        }

        if (!rule.Outputs.AnyEnabled)
        {
            errors.Add("Enable at least one output.");
        }

        if (rule.Outputs.Echo.Enabled && string.IsNullOrWhiteSpace(rule.Outputs.Echo.Text))
        {
            errors.Add("Echo text is required when Echo is enabled.");
        }

        return errors;
    }

    public static bool IsValid(Rule rule) => Validate(rule).Count == 0;
}
