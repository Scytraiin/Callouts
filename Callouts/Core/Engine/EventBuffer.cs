using System;
using System.Collections.Generic;

namespace Callouts.Core.Engine;

/// <summary>A single observed event, retained for the Live events window (issue 010).</summary>
public sealed record EventRecord
{
    public required TriggerKind Kind { get; init; }

    /// <summary>Human-readable one-line description (from <see cref="EventFormatter"/>).</summary>
    public required string Display { get; init; }

    /// <summary>Wall-clock time string, stamped by the caller ("21:14:02").</summary>
    public string Time { get; init; } = string.Empty;

    /// <summary>The original event, used to pre-fill a rule via the [＋] button.</summary>
    public required TriggerEvent Event { get; init; }
}

/// <summary>
/// Session-only event log, <b>partitioned by <see cref="EventCategory"/></b> so each category keeps
/// its own most-recent entries: a noisy category (chat, other-status) can never evict the ones you
/// care about (enemy casts, your debuffs). Each partition is an independent ring buffer with its own
/// limit (a shared default plus optional per-category overrides). A global sequence number lets the
/// UI merge the partitions back into one newest-first timeline. Never persisted (DESIGN.md §7.3).
///
/// Not thread-safe by design: Add and the UI Draw both run on the game's main thread, so no
/// enumeration ever interleaves with a mutation.
/// </summary>
public sealed class EventBuffer
{
    private const int MinLimit = 1;
    private const int MaxLimit = 100_000;

    private readonly Dictionary<EventCategory, LinkedList<Entry>> byCategory = new();
    private readonly Dictionary<EventCategory, int> overrides = new();
    private int defaultLimit;
    private long sequence;

    public EventBuffer(int defaultLimit = 2000, IReadOnlyDictionary<EventCategory, int>? categoryOverrides = null)
    {
        this.defaultLimit = Clamp(defaultLimit);
        if (categoryOverrides is not null)
        {
            foreach (var (category, limit) in categoryOverrides)
            {
                this.overrides[category] = Clamp(limit);
            }
        }

        foreach (EventCategory category in Enum.GetValues<EventCategory>())
        {
            this.byCategory[category] = new LinkedList<Entry>();
        }
    }

    public bool Paused { get; set; }

    /// <summary>Total entries across all categories.</summary>
    public int Count
    {
        get
        {
            var total = 0;
            foreach (var list in this.byCategory.Values)
            {
                total += list.Count;
            }

            return total;
        }
    }

    public int CountFor(EventCategory category) => this.byCategory[category].Count;

    public int LimitFor(EventCategory category)
        => this.overrides.TryGetValue(category, out var limit) ? limit : this.defaultLimit;

    /// <summary>Applies a new default + per-category overrides at runtime and re-trims each partition.</summary>
    public void SetLimits(int defaultLimit, IReadOnlyDictionary<EventCategory, int> categoryOverrides)
    {
        this.defaultLimit = Clamp(defaultLimit);
        this.overrides.Clear();
        foreach (var (category, limit) in categoryOverrides)
        {
            this.overrides[category] = Clamp(limit);
        }

        foreach (var category in this.byCategory.Keys)
        {
            this.Trim(category);
        }
    }

    public void Add(EventRecord record)
    {
        if (this.Paused)
        {
            return;
        }

        var category = EventCategorizer.Categorize(record.Event);
        this.byCategory[category].AddLast(new Entry(this.sequence++, record));
        this.Trim(category);
    }

    /// <summary>Newest-first snapshot across all categories.</summary>
    public IReadOnlyList<EventRecord> Snapshot()
    {
        var result = new List<EventRecord>();
        foreach (var record in this.EnumerateNewestFirst())
        {
            result.Add(record);
        }

        return result;
    }

    /// <summary>
    /// Lazily merges the per-category partitions into one newest-first stream (k-way merge on the
    /// global sequence number). Lets the UI take only the top matches without materializing a list.
    /// </summary>
    public IEnumerable<EventRecord> EnumerateNewestFirst()
    {
        var cursors = new List<LinkedListNode<Entry>>();
        foreach (var list in this.byCategory.Values)
        {
            if (list.Last is not null)
            {
                cursors.Add(list.Last);
            }
        }

        while (cursors.Count > 0)
        {
            var maxIndex = 0;
            for (var i = 1; i < cursors.Count; i++)
            {
                if (cursors[i].Value.Seq > cursors[maxIndex].Value.Seq)
                {
                    maxIndex = i;
                }
            }

            var node = cursors[maxIndex];
            yield return node.Value.Record;

            var previous = node.Previous;
            if (previous is null)
            {
                cursors.RemoveAt(maxIndex);
            }
            else
            {
                cursors[maxIndex] = previous;
            }
        }
    }

    public void Clear()
    {
        foreach (var list in this.byCategory.Values)
        {
            list.Clear();
        }
    }

    private void Trim(EventCategory category)
    {
        var list = this.byCategory[category];
        var limit = this.LimitFor(category);
        while (list.Count > limit)
        {
            list.RemoveFirst();
        }
    }

    private static int Clamp(int value) => value < MinLimit ? MinLimit : (value > MaxLimit ? MaxLimit : value);

    private sealed record Entry(long Seq, EventRecord Record);
}
