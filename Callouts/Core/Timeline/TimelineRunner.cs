using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Core.Timeline;

/// <summary>One upcoming entry and how many seconds until it is due.</summary>
public sealed record UpcomingEntry(TimelineEntry Entry, double SecondsUntil);

/// <summary>
/// Pure, testable timeline clock. The caller supplies a monotonically increasing "wall elapsed since
/// combat start" (seconds); the runner maps that to timeline time via an offset that <see cref="Observe"/>
/// snaps whenever a synced mechanic is seen near its scheduled time (Cactbot-style drift correction).
/// <see cref="Advance"/> fires each entry's optional pre-alert exactly once. No Dalamud, no real clock.
/// </summary>
public sealed class TimelineRunner
{
    // A tiny grace window so an alert whose fire-time we stepped past in one frame still fires.
    private const double AlertLingerSeconds = 1.0;

    private readonly Dictionary<string, Regex?> regexCache = new(StringComparer.Ordinal);
    private readonly HashSet<int> firedAlerts = new();

    private List<TimelineEntry> entries = [];
    private double offset;

    public bool Running { get; private set; }

    public TimelineDefinition? Definition { get; private set; }

    /// <summary>Loads (and sorts a copy of) a definition. Does not start the clock.</summary>
    public void Load(TimelineDefinition? definition)
    {
        this.Definition = definition;
        this.entries = [];
        if (definition is not null)
        {
            foreach (var entry in definition.Entries)
            {
                this.entries.Add(entry);
            }

            this.entries.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        this.Reset();
    }

    /// <summary>Starts the clock: timeline time equals wall-elapsed until a sync re-anchors it.</summary>
    public void Start()
    {
        this.offset = 0;
        this.firedAlerts.Clear();
        this.Running = this.entries.Count > 0;
    }

    public void Stop() => this.Running = false;

    public void Reset()
    {
        this.Running = false;
        this.offset = 0;
        this.firedAlerts.Clear();
    }

    /// <summary>Current timeline time for a given wall-elapsed reading.</summary>
    public double CurrentTime(double wallElapsed) => wallElapsed - this.offset;

    /// <summary>
    /// Re-anchors the clock if <paramref name="evt"/> matches an entry's sync within its window.
    /// Picks the matching entry closest to the current timeline time. Returns true if it snapped.
    /// </summary>
    public bool Observe(TriggerEvent evt, double wallElapsed)
    {
        if (!this.Running || this.entries.Count == 0)
        {
            return false;
        }

        var now = this.CurrentTime(wallElapsed);
        var best = -1;
        var bestDelta = double.MaxValue;

        for (var i = 0; i < this.entries.Count; i++)
        {
            var entry = this.entries[i];
            if (!entry.HasSync)
            {
                continue;
            }

            if (now < entry.Time - entry.WindowBefore || now > entry.Time + entry.WindowAfter)
            {
                continue;
            }

            if (!TimelineMatcher.SyncMatches(entry, evt, this.GetRegex))
            {
                continue;
            }

            var delta = Math.Abs(entry.Time - now);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }

        if (best < 0)
        {
            return false;
        }

        this.offset = wallElapsed - this.entries[best].Time;
        return true;
    }

    /// <summary>Fires any pre-alerts that have come due since the last call (each fires once).</summary>
    public IReadOnlyList<AlertAction> Advance(double wallElapsed)
    {
        if (!this.Running)
        {
            return [];
        }

        var now = this.CurrentTime(wallElapsed);
        var actions = new List<AlertAction>();

        for (var i = 0; i < this.entries.Count; i++)
        {
            var entry = this.entries[i];
            if (!entry.HasAlert || this.firedAlerts.Contains(i))
            {
                continue;
            }

            var fireAt = entry.Time - entry.AlertSecondsBefore;
            if (now < fireAt || now > entry.Time + AlertLingerSeconds)
            {
                continue;
            }

            this.firedAlerts.Add(i);

            if (!string.IsNullOrEmpty(entry.AlertText))
            {
                actions.Add(new AlertAction { Kind = AlertOutputKind.Toast, Text = entry.AlertText, ToastStyle = ToastStyle.Normal });
                actions.Add(new AlertAction { Kind = AlertOutputKind.Echo, Text = entry.AlertText });
            }

            if (entry.AlertSound is >= 1 and <= 16)
            {
                actions.Add(new AlertAction { Kind = AlertOutputKind.Sound, SoundEffectId = entry.AlertSound });
            }
        }

        return actions;
    }

    /// <summary>Entries due within <paramref name="lookAhead"/> seconds, nearest first.</summary>
    public IReadOnlyList<UpcomingEntry> Upcoming(double wallElapsed, double lookAhead)
    {
        var now = this.CurrentTime(wallElapsed);
        var result = new List<UpcomingEntry>();

        foreach (var entry in this.entries)
        {
            var until = entry.Time - now;
            if (until >= 0 && until <= lookAhead)
            {
                result.Add(new UpcomingEntry(entry, until));
            }
        }

        result.Sort((a, b) => a.SecondsUntil.CompareTo(b.SecondsUntil));
        return result;
    }

    private Regex? GetRegex(string pattern)
    {
        if (this.regexCache.TryGetValue(pattern, out var cached))
        {
            return cached;
        }

        RegexFactory.TryCompile(pattern, caseSensitive: false, out var regex, out _);
        this.regexCache[pattern] = regex;
        return regex;
    }
}
