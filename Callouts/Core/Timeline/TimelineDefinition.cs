using System;
using System.Collections.Generic;
using System.Linq;

namespace Callouts.Core.Timeline;

/// <summary>
/// A named, ordered list of <see cref="TimelineEntry"/> for an encounter (Cactbot-style). Bound to a
/// zone via <see cref="TerritoryId"/> (0 = any). Mutable POCO for configuration persistence.
/// </summary>
public sealed class TimelineDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    /// <summary>Zone this timeline applies to; 0 = any zone.</summary>
    public uint TerritoryId { get; set; }

    public bool Enabled { get; set; } = true;

    public List<TimelineEntry> Entries { get; set; } = [];

    /// <summary>Sorts entries by time in place (the runner and UI assume ascending order).</summary>
    public void Sort() => this.Entries.Sort((a, b) => a.Time.CompareTo(b.Time));

    public TimelineDefinition Clone() => new()
    {
        Id = this.Id,
        Name = this.Name,
        TerritoryId = this.TerritoryId,
        Enabled = this.Enabled,
        Entries = this.Entries.Select(e => e.Clone()).ToList(),
    };
}
