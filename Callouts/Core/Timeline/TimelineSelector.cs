using System.Collections.Generic;

namespace Callouts.Core.Timeline;

/// <summary>Pure selection of which timeline should run, given the current zone and settings.</summary>
public static class TimelineSelector
{
    /// <summary>
    /// Picks the timeline to run. When <paramref name="autoByZone"/> is on, an enabled timeline whose
    /// <see cref="TimelineDefinition.TerritoryId"/> matches the current zone wins (a zone-specific match
    /// beats an any-zone one); otherwise the explicitly chosen <paramref name="activeId"/> is used.
    /// Returns null if nothing applies.
    /// </summary>
    public static TimelineDefinition? Select(
        IReadOnlyList<TimelineDefinition> timelines,
        uint territoryId,
        bool autoByZone,
        string? activeId)
    {
        if (autoByZone)
        {
            TimelineDefinition? anyZone = null;
            foreach (var timeline in timelines)
            {
                if (!timeline.Enabled)
                {
                    continue;
                }

                if (timeline.TerritoryId == territoryId && territoryId != 0)
                {
                    return timeline; // exact zone match wins immediately
                }

                if (timeline.TerritoryId == 0)
                {
                    anyZone ??= timeline;
                }
            }

            return anyZone;
        }

        if (!string.IsNullOrEmpty(activeId))
        {
            foreach (var timeline in timelines)
            {
                if (timeline.Enabled && timeline.Id == activeId)
                {
                    return timeline;
                }
            }
        }

        return null;
    }
}
