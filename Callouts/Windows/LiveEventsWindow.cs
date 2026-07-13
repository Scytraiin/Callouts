using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Engine;

namespace Callouts.Windows;

/// <summary>
/// The Live events window (issue 010): a running feed of observed events with category filters,
/// a search box, and a [＋] button that creates a rule pre-filled from the exact event. The log can
/// hold many thousands of entries; the window renders only the newest matches (capped) so it stays
/// responsive. Session-only; nothing is persisted.
/// </summary>
public sealed class LiveEventsWindow : Window, IDisposable
{
    // Cap on rendered rows — the full log is still searched, only the drawn matches are limited.
    private const int DisplayCap = 1000;

    private static readonly EventCategory[] CastCategories =
        [EventCategory.EnemyCast, EventCategory.OtherCast];

    private static readonly EventCategory[] StatusCategories =
        [EventCategory.SelfDebuff, EventCategory.SelfBuff, EventCategory.PartyDebuff, EventCategory.PartyBuff, EventCategory.OtherStatus];

    private static readonly EventCategory[] MiscCategories =
        [EventCategory.Chat, EventCategory.Duty, EventCategory.Vfx, EventCategory.HeadMarker];

    private readonly EventBuffer buffer;
    private readonly RuleEngine engine;
    private readonly Action<TriggerEvent> createFromEvent;

    private readonly Dictionary<EventCategory, bool> shown = new();
    private string search = string.Empty;

    public LiveEventsWindow(EventBuffer buffer, RuleEngine engine, Action<TriggerEvent> createFromEvent)
        : base("Callouts — Live events###CalloutsEvents")
    {
        this.buffer = buffer;
        this.engine = engine;
        this.createFromEvent = createFromEvent;

        foreach (EventCategory category in Enum.GetValues<EventCategory>())
        {
            this.shown[category] = true;
        }

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(620, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        this.DrawToolbar();
        this.DrawFilters();
        ImGui.Separator();

        var (matched, rendered) = this.DrawEvents();

        ImGui.Separator();
        var capNote = matched > rendered ? $" (showing newest {rendered} — narrow with search)" : string.Empty;
        ImGui.TextDisabled($"{matched} matched of {this.buffer.Count} logged{capNote} · dropped by rate limit: {this.engine.RateLimiter.DroppedCount}");
    }

    private void DrawToolbar()
    {
        var paused = this.buffer.Paused;
        if (ImGui.Button(paused ? "Resume" : "Pause"))
        {
            this.buffer.Paused = !paused;
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            this.buffer.Clear();
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(260);
        var searchText = this.search;
        if (ImGui.InputTextWithHint("##eventsearch", "Search events (name, text)…", ref searchText, 128))
        {
            this.search = searchText;
        }

        ImGui.SameLine();
        ImGui.TextDisabled("＋ makes a rule from a line.");
    }

    private void DrawFilters()
    {
        if (ImGui.SmallButton("All"))
        {
            this.SetAll(true);
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("None"))
        {
            this.SetAll(false);
        }

        ImGui.SameLine();
        this.DrawCategoryChecks(CastCategories);

        this.DrawCategoryChecks(StatusCategories);
        this.DrawCategoryChecks(MiscCategories);
    }

    private void DrawCategoryChecks(EventCategory[] categories)
    {
        for (var i = 0; i < categories.Length; i++)
        {
            var category = categories[i];
            var on = this.shown[category];
            if (ImGui.Checkbox($"{EventCategorizer.Label(category)} ({this.buffer.CountFor(category)})##cat{category}", ref on))
            {
                this.shown[category] = on;
            }

            if (i < categories.Length - 1)
            {
                ImGui.SameLine();
            }
        }
    }

    private (int Matched, int Rendered) DrawEvents()
    {
        if (!ImGui.BeginTable("events", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            return (0, 0);
        }

        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, 96f);
        ImGui.TableSetupColumn("Event");
        ImGui.TableSetupColumn("##add", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableHeadersRow();

        var hasSearch = !string.IsNullOrWhiteSpace(this.search);
        var matched = 0;
        var rendered = 0;
        var id = 0;

        foreach (var record in this.buffer.EnumerateNewestFirst())
        {
            var category = EventCategorizer.Categorize(record.Event);
            if (!this.shown[category])
            {
                continue;
            }

            if (hasSearch && !record.Display.Contains(this.search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matched++;
            if (rendered >= DisplayCap)
            {
                continue; // keep counting matches, but stop drawing rows
            }

            rendered++;
            ImGui.PushID(id++);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(record.Time);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(EventCategorizer.Label(category));

            ImGui.TableNextColumn();
            ImGui.TextWrapped(record.Display);

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("＋"))
            {
                this.createFromEvent(record.Event);
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
        return (matched, rendered);
    }

    private void SetAll(bool value)
    {
        foreach (EventCategory category in Enum.GetValues<EventCategory>())
        {
            this.shown[category] = value;
        }
    }
}
