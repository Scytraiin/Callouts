using System.Collections.Generic;

using Callouts.Core.Engine;

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

        // Chat rules match on a text pattern; other kinds match on ids/filters.
        if (rule.Source.Kind == TriggerKind.Chat)
        {
            if (string.IsNullOrWhiteSpace(rule.Source.Pattern))
            {
                errors.Add("Add a pattern to enable Save.");
            }
            else if (rule.Source.MatchMode == MatchMode.Regex
                && !RegexFactory.TryCompile(rule.Source.Pattern, rule.Source.CaseSensitive, out _, out var regexError))
            {
                errors.Add(regexError ?? "Invalid regex.");
            }
        }

        if (!rule.Outputs.AnyEnabled)
        {
            errors.Add("Enable at least one output.");
        }

        if (rule.Outputs.Echo.Enabled && string.IsNullOrWhiteSpace(rule.Outputs.Echo.Text))
        {
            errors.Add("Echo text is required when Echo is enabled.");
        }

        if (rule.Outputs.Toast.Enabled && string.IsNullOrWhiteSpace(rule.Outputs.Toast.Text))
        {
            errors.Add("Toast text is required when Toast is enabled.");
        }

        if (rule.Outputs.Sound.Enabled
            && (rule.Outputs.Sound.EffectId < SoundOutput.MinEffectId || rule.Outputs.Sound.EffectId > SoundOutput.MaxEffectId))
        {
            errors.Add($"Sound effect must be between {SoundOutput.MinEffectId} and {SoundOutput.MaxEffectId}.");
        }

        return errors;
    }

    public static bool IsValid(Rule rule) => Validate(rule).Count == 0;
}
