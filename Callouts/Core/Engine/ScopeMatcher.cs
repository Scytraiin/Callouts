using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure check of whether a rule's <see cref="RuleScope"/> allows it to fire for an event.</summary>
public static class ScopeMatcher
{
    public static bool InScope(RuleScope scope, TriggerEvent evt)
    {
        if (scope.OnlyInCombat && !evt.InCombat)
        {
            return false;
        }

        if (scope.OnlyInDuty && !evt.InDuty)
        {
            return false;
        }

        if (scope.TerritoryIds.Count > 0 && !scope.TerritoryIds.Contains(evt.TerritoryId))
        {
            return false;
        }

        return true;
    }
}
