using System;
using System.Collections.Generic;
using System.Linq;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// Pure filtering/sorting for the Rules window so a 50+ rule collection stays navigable and the
/// "shown" set for bulk operations is well-defined and testable (DESIGN.md §7.1).
/// </summary>
public static class RuleListView
{
    public static IReadOnlyList<Rule> Filter(
        IEnumerable<Rule> rules,
        string? search = null,
        TriggerKind? sourceKind = null,
        bool enabledOnly = false,
        string? tag = null)
    {
        IEnumerable<Rule> query = rules;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => r.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (sourceKind is not null)
        {
            query = query.Where(r => r.Source.Kind == sourceKind.Value);
        }

        if (enabledOnly)
        {
            query = query.Where(r => r.Enabled);
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            query = query.Where(r => r.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    public static IReadOnlyList<string> AllTags(IEnumerable<Rule> rules)
        => rules.SelectMany(r => r.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
