using System.Collections.Generic;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure matcher for status rules. Placeholders: {status}, {bearer}, {zone}.</summary>
public static class StatusTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt)
    {
        if (evt.Kind != TriggerKind.Status || rule.Source.Kind != TriggerKind.Status)
        {
            return null;
        }

        var s = rule.Source;

        if (s.StatusId != 0 && evt.StatusId != s.StatusId)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(s.StatusNameContains)
            && !TextMatch.Contains(evt.StatusName, s.StatusNameContains, caseSensitive: false))
        {
            return null;
        }

        var changeOk = s.StatusChange switch
        {
            StatusChangeFilter.Gained => evt.StatusGained,
            StatusChangeFilter.Removed => !evt.StatusGained,
            _ => true,
        };
        if (!changeOk)
        {
            return null;
        }

        var bearerOk = s.Bearer switch
        {
            BearerScope.Self => evt.BearerIsSelf,
            BearerScope.Party => evt.BearerInParty,
            BearerScope.Target => evt.BearerIsTarget,
            _ => true,
        };
        if (!bearerOk)
        {
            return null;
        }

        // Stack threshold only applies to a gained status (a removal has no stacks).
        if (s.MinStacks > 0 && evt.StatusGained && evt.Stacks < s.MinStacks)
        {
            return null;
        }

        return new MatchResult
        {
            Values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = evt.StatusName,
                ["bearer"] = evt.BearerName,
                ["zone"] = evt.Zone,
            },
        };
    }
}
