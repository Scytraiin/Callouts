using System;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Engine;

namespace Callouts.Windows;

/// <summary>
/// The Live events window (issue 010): a running feed of observed events with per-kind filters
/// and a [＋] button that creates a rule pre-filled from the exact event — so users never type an
/// action id, status id, or channel by hand. Session-only; nothing is persisted.
/// </summary>
public sealed class LiveEventsWindow : Window, IDisposable
{
    private readonly EventBuffer buffer;
    private readonly RuleEngine engine;
    private readonly Action<TriggerEvent> createFromEvent;

    private readonly Dictionary<TriggerKind, bool> shown = new()
    {
        [TriggerKind.Chat] = true,
        [TriggerKind.Cast] = true,
        [TriggerKind.Status] = true,
        [TriggerKind.DutyEvent] = true,
        [TriggerKind.Vfx] = true,
        [TriggerKind.HeadMarker] = true,
    };

    public LiveEventsWindow(EventBuffer buffer, RuleEngine engine, Action<TriggerEvent> createFromEvent)
        : base("Callouts — Live events###CalloutsEvents")
    {
        this.buffer = buffer;
        this.engine = engine;
        this.createFromEvent = createFromEvent;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
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
        ImGui.TextDisabled("Click ＋ to make a rule from any line.");

        this.DrawFilters();
        ImGui.Separator();
        this.DrawEvents();

        ImGui.Separator();
        ImGui.TextDisabled($"Dropped by rate limit this session: {this.engine.RateLimiter.DroppedCount}");
    }

    private void DrawFilters()
    {
        foreach (var kind in new[] { TriggerKind.Chat, TriggerKind.Cast, TriggerKind.Status, TriggerKind.DutyEvent, TriggerKind.Vfx, TriggerKind.HeadMarker })
        {
            var on = this.shown[kind];
            if (ImGui.Checkbox($"{KindLabel(kind)}##f{kind}", ref on))
            {
                this.shown[kind] = on;
            }

            ImGui.SameLine();
        }

        ImGui.NewLine();
    }

    private void DrawEvents()
    {
        if (!ImGui.BeginTable("events", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            return;
        }

        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableSetupColumn("Event");
        ImGui.TableSetupColumn("##add", ImGuiTableColumnFlags.WidthFixed, 32f);
        ImGui.TableHeadersRow();

        var index = 0;
        foreach (var record in this.buffer.Snapshot())
        {
            if (!this.shown.TryGetValue(record.Kind, out var visible) || !visible)
            {
                continue;
            }

            ImGui.PushID(index++);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(record.Time);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(KindLabel(record.Kind));

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
    }

    private static string KindLabel(TriggerKind kind) => kind switch
    {
        TriggerKind.Chat => "Chat",
        TriggerKind.Cast => "Cast",
        TriggerKind.Status => "Status",
        TriggerKind.DutyEvent => "Duty",
        TriggerKind.Vfx => "VFX",
        TriggerKind.HeadMarker => "Marker",
        _ => kind.ToString(),
    };
}
