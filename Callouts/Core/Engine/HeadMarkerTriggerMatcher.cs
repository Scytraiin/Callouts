using System.Collections.Generic;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure matcher for head-marker rules. Placeholders: {marker}, {zone}.</summary>
public static class HeadMarkerTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt)
    {
        if (evt.Kind != TriggerKind.HeadMarker || rule.Source.Kind != TriggerKind.HeadMarker)
        {
            return null;
        }

        var s = rule.Source;

        // MarkerKey may be a named marker ("spread") or a raw value; match against either field.
        if (!string.IsNullOrEmpty(s.MarkerKey)
            && !Equals(s.MarkerKey, evt.MarkerKey)
            && !Equals(s.MarkerKey, evt.RawValue))
        {
            return null;
        }

        if (!VfxTriggerMatcher.ActorScopeMatch(s.ActorScope, evt))
        {
            return null;
        }

        var display = string.IsNullOrEmpty(evt.MarkerKey) ? evt.RawValue : evt.MarkerKey;
        return new MatchResult
        {
            Values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["marker"] = display,
                ["zone"] = evt.Zone,
            },
        };
    }

    private static bool Equals(string a, string b)
        => !string.IsNullOrEmpty(b) && string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);
}
