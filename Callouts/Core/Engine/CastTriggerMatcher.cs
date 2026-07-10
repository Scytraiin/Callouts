using System.Collections.Generic;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure matcher for cast rules (enemy/action/target filters). Placeholders: {caster}, {action}, {zone}.</summary>
public static class CastTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt)
    {
        if (evt.Kind != TriggerKind.Cast || rule.Source.Kind != TriggerKind.Cast)
        {
            return null;
        }

        var s = rule.Source;

        if (s.ActionId != 0 && evt.ActionId != s.ActionId)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(s.ActionNameContains)
            && !TextMatch.Contains(evt.ActionName, s.ActionNameContains, caseSensitive: false))
        {
            return null;
        }

        if (s.CasterScope == CasterScope.Enemy && !evt.CasterIsEnemy)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(s.CasterNameContains)
            && !TextMatch.Contains(evt.CasterName, s.CasterNameContains, caseSensitive: false))
        {
            return null;
        }

        if (s.OnlyTargetingMe && !evt.TargetIsSelf)
        {
            return null;
        }

        if (s.OnlyTargetingParty && !evt.TargetInParty)
        {
            return null;
        }

        return new MatchResult
        {
            Values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["caster"] = evt.CasterName,
                ["action"] = evt.ActionName,
                ["zone"] = evt.Zone,
            },
        };
    }
}
