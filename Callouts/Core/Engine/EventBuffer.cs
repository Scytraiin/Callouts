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
/// Session-only ring buffer of recent events (never persisted). Newest-first snapshots feed the
/// Live events window; over-capacity entries drop oldest-first (DESIGN.md §7.3).
/// </summary>
public sealed class EventBuffer
{
    private readonly LinkedList<EventRecord> items = new();
    private int capacity;

    public EventBuffer(int capacity = 200)
    {
        this.capacity = capacity < 1 ? 1 : capacity;
    }

    public bool Paused { get; set; }

    public int Capacity => this.capacity;

    public int Count => this.items.Count;

    public void SetCapacity(int value)
    {
        this.capacity = value < 1 ? 1 : value;
        this.Trim();
    }

    public void Add(EventRecord record)
    {
        if (this.Paused)
        {
            return;
        }

        this.items.AddLast(record);
        this.Trim();
    }

    /// <summary>Newest-first snapshot for display.</summary>
    public IReadOnlyList<EventRecord> Snapshot()
    {
        var result = new List<EventRecord>(this.items.Count);
        for (var node = this.items.Last; node is not null; node = node.Previous)
        {
            result.Add(node.Value);
        }

        return result;
    }

    /// <summary>
    /// Lazily enumerates newest-first without materializing a list — lets the UI filter a large
    /// log and render only the top matches. Safe because Add and the UI Draw run on the same
    /// (main) thread, so no enumeration ever interleaves with a mutation.
    /// </summary>
    public IEnumerable<EventRecord> EnumerateNewestFirst()
    {
        for (var node = this.items.Last; node is not null; node = node.Previous)
        {
            yield return node.Value;
        }
    }

    public void Clear() => this.items.Clear();

    private void Trim()
    {
        while (this.items.Count > this.capacity)
        {
            this.items.RemoveFirst();
        }
    }
}
