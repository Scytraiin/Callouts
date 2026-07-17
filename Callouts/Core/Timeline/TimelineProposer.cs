using System;
using System.Collections.Generic;

using Callouts.Core.Engine;

namespace Callouts.Core.Timeline;

/// <summary>
/// Builds a proposed <see cref="TimelineDefinition"/> from a recorded fight (see <see cref="FightRecorder"/>).
/// It keeps the "interesting" events — enemy casts and debuffs gained on you/your party — labels each
/// with its ability/status name, assigns the observed time, and generates a name-contains sync so the
/// clock re-anchors on that mechanic. Consecutive duplicates within a short window are collapsed so a
/// multi-hit mechanic becomes one entry. Pure and deterministic.
/// </summary>
public static class TimelineProposer
{
    /// <summary>Duplicates of the same label within this many seconds collapse into one entry.</summary>
    public const double DedupeWindowSeconds = 2.0;

    public static TimelineDefinition Propose(
        IReadOnlyList<TimedEvent> recorded,
        string zoneName,
        uint territoryId)
    {
        var definition = new TimelineDefinition
        {
            Name = string.IsNullOrWhiteSpace(zoneName) ? "Proposed timeline" : $"{zoneName} (proposed)",
            TerritoryId = territoryId,
        };

        // Last time we emitted each label, to collapse multi-hits / re-applications.
        var lastByLabel = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var timed in recorded)
        {
            if (!IsInteresting(timed.Event, out var label))
            {
                continue;
            }

            if (lastByLabel.TryGetValue(label, out var last) && timed.Elapsed - last < DedupeWindowSeconds)
            {
                lastByLabel[label] = timed.Elapsed;
                continue;
            }

            lastByLabel[label] = timed.Elapsed;

            definition.Entries.Add(new TimelineEntry
            {
                Time = Math.Round(timed.Elapsed, 1),
                Label = label,
                SyncPattern = label,
                SyncIsRegex = false,
            });
        }

        definition.Sort();
        return definition;
    }

    private static bool IsInteresting(TriggerEvent evt, out string label)
    {
        switch (evt.Kind)
        {
            case TriggerKind.Cast when evt.CasterIsEnemy && !string.IsNullOrWhiteSpace(evt.ActionName):
                label = evt.ActionName;
                return true;

            case TriggerKind.Status when evt.StatusGained && evt.IsDebuff
                                         && (evt.BearerIsSelf || evt.BearerInParty)
                                         && !string.IsNullOrWhiteSpace(evt.StatusName):
                label = evt.StatusName;
                return true;

            default:
                label = string.Empty;
                return false;
        }
    }
}
