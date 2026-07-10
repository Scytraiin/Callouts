using System.Collections.Generic;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure matcher for duty-event rules. Placeholders: {event}, {zone}.</summary>
public static class DutyTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt)
    {
        if (evt.Kind != TriggerKind.DutyEvent || rule.Source.Kind != TriggerKind.DutyEvent)
        {
            return null;
        }

        var filter = rule.Source.DutyEvent;
        if (filter != DutyEventFilter.Any && evt.DutyEvent != filter)
        {
            return null;
        }

        return new MatchResult
        {
            Values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["event"] = evt.DutyEvent.ToString(),
                ["zone"] = evt.Zone,
            },
        };
    }
}
