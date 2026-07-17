using System.Collections.Generic;

using Callouts.Core.Engine;

namespace Callouts.Core.Timeline;

/// <summary>An observed event tagged with its time (seconds) since combat start.</summary>
public sealed record TimedEvent(double Elapsed, TriggerEvent Event);

/// <summary>
/// Records the current fight's events with their elapsed time, so a timeline can be proposed from
/// what actually happened. Session-only, bounded (a runaway fight can't grow memory without limit).
/// Pure: the caller supplies elapsed seconds.
/// </summary>
public sealed class FightRecorder
{
    private const int MaxEvents = 20_000;

    private readonly List<TimedEvent> events = [];

    public bool Recording { get; private set; }

    public int Count => this.events.Count;

    public void Start()
    {
        this.events.Clear();
        this.Recording = true;
    }

    public void Stop() => this.Recording = false;

    public void Add(double elapsed, TriggerEvent evt)
    {
        if (!this.Recording || this.events.Count >= MaxEvents)
        {
            return;
        }

        this.events.Add(new TimedEvent(elapsed, evt));
    }

    /// <summary>A stable copy of everything recorded so far.</summary>
    public IReadOnlyList<TimedEvent> Snapshot() => this.events.ToArray();
}
