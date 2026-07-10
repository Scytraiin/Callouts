using System.Collections.Generic;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure matcher for VFX rules (path contains + actor scope). Placeholders: {vfx}, {zone}.</summary>
public static class VfxTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt)
    {
        if (evt.Kind != TriggerKind.Vfx || rule.Source.Kind != TriggerKind.Vfx)
        {
            return null;
        }

        var s = rule.Source;

        if (!string.IsNullOrEmpty(s.VfxPathPattern)
            && !TextMatch.Contains(evt.VfxPath, s.VfxPathPattern, caseSensitive: false))
        {
            return null;
        }

        if (!ActorScopeMatch(s.ActorScope, evt))
        {
            return null;
        }

        return new MatchResult
        {
            Values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["vfx"] = evt.VfxPath,
                ["zone"] = evt.Zone,
            },
        };
    }

    internal static bool ActorScopeMatch(BearerScope scope, TriggerEvent evt) => scope switch
    {
        BearerScope.Self => evt.TargetIsSelf,
        BearerScope.Party => evt.TargetInParty,
        _ => true, // Anyone / Target
    };
}
