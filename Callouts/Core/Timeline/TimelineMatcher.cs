using System;
using System.Text.RegularExpressions;

using Callouts.Core.Engine;

namespace Callouts.Core.Timeline;

/// <summary>
/// Pure helpers for deciding whether an observed event satisfies a timeline entry's sync. The
/// caller supplies a compiled-regex provider so regex compilation/caching stays out of Core state.
/// </summary>
public static class TimelineMatcher
{
    /// <summary>The text a sync pattern is matched against, per event kind.</summary>
    public static string MatchText(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Cast => evt.ActionName,
        TriggerKind.Status => evt.StatusName,
        TriggerKind.Chat => evt.Message,
        TriggerKind.DutyEvent => evt.DutyEvent.ToString(),
        TriggerKind.Vfx => evt.VfxPath,
        TriggerKind.HeadMarker => string.IsNullOrEmpty(evt.MarkerKey) ? evt.RawValue : evt.MarkerKey,
        _ => string.Empty,
    };

    /// <summary>
    /// True when the entry has a sync pattern that matches the event. <paramref name="regexProvider"/>
    /// returns a compiled regex for a pattern (or null if it failed to compile).
    /// </summary>
    public static bool SyncMatches(TimelineEntry entry, TriggerEvent evt, Func<string, Regex?> regexProvider)
    {
        if (!entry.HasSync)
        {
            return false;
        }

        var text = MatchText(evt);
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (!entry.SyncIsRegex)
        {
            return text.Contains(entry.SyncPattern, StringComparison.OrdinalIgnoreCase);
        }

        var regex = regexProvider(entry.SyncPattern);
        if (regex is null)
        {
            return false;
        }

        try
        {
            return regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
