using System;
using System.Globalization;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

using Callouts.Core.Timeline;

namespace Callouts.Windows;

/// <summary>
/// The Timeline window: a live Cactbot-style countdown of upcoming mechanics for the active
/// timeline, plus a management/editor section (create, edit, enable, import/export, and
/// "propose from last fight"). The live view only advances during combat; editing works any time.
/// </summary>
public sealed class TimelineWindow : Window, IDisposable
{
    private readonly Configuration configuration;
    private readonly TimelineRunner runner;
    private readonly FightRecorder recorder;
    private readonly Func<double> elapsedProvider;
    private readonly Func<(uint Id, string Name)> currentZone;
    private readonly Action save;
    private readonly Action reloadRunner;

    private string? selectedId;
    private string status = string.Empty;

    public TimelineWindow(
        Configuration configuration,
        TimelineRunner runner,
        FightRecorder recorder,
        Func<double> elapsedProvider,
        Func<(uint Id, string Name)> currentZone,
        Action save,
        Action reloadRunner)
        : base("Callouts — Timeline###CalloutsTimeline")
    {
        this.configuration = configuration;
        this.runner = runner;
        this.recorder = recorder;
        this.elapsedProvider = elapsedProvider;
        this.currentZone = currentZone;
        this.save = save;
        this.reloadRunner = reloadRunner;

        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        this.DrawLive();
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Manage timelines"))
        {
            this.DrawManage();
        }
    }

    private void DrawLive()
    {
        var lookAhead = this.configuration.Options.TimelineLookAheadSeconds;

        if (!this.runner.Running)
        {
            var name = this.runner.Definition?.Name;
            ImGui.TextDisabled(name is null
                ? "No active timeline. Pull the boss with a matching timeline, or open Manage below."
                : $"\"{name}\" is loaded — waiting for combat.");
            return;
        }

        var elapsed = this.elapsedProvider();
        var clock = this.runner.CurrentTime(elapsed);
        ImGui.Text($"{this.runner.Definition?.Name}  —  {FormatClock(clock)}");

        var upcoming = this.runner.Upcoming(elapsed, lookAhead);
        if (upcoming.Count == 0)
        {
            ImGui.TextDisabled("No upcoming entries in the look-ahead window.");
            return;
        }

        foreach (var item in upcoming)
        {
            var fraction = 1f - (float)Math.Clamp(item.SecondsUntil / lookAhead, 0, 1);
            var overlay = $"{item.SecondsUntil,5:0.0}s   {item.Entry.Label}";
            ImGui.ProgressBar(fraction, new Vector2(-1, 0), overlay);
        }
    }

    private void DrawManage()
    {
        if (ImGui.Button("New"))
        {
            var (id, name) = this.currentZone();
            var timeline = new TimelineDefinition { Name = string.IsNullOrEmpty(name) ? "New timeline" : name, TerritoryId = id };
            this.configuration.Timelines.Add(timeline);
            this.selectedId = timeline.Id;
            this.save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Propose from last fight"))
        {
            this.ProposeFromLastFight();
        }

        ImGui.SameLine();
        if (ImGui.Button("Import (clipboard)"))
        {
            this.ImportFromClipboard();
        }

        ImGui.SameLine();
        if (ImGui.Button("Apply now"))
        {
            this.reloadRunner();
            this.status = "Reloaded the active timeline.";
        }

        var auto = this.configuration.Options.TimelineAutoByZone;
        if (ImGui.Checkbox("Auto-pick timeline by zone", ref auto))
        {
            this.configuration.Options.TimelineAutoByZone = auto;
            this.save();
            this.reloadRunner();
        }

        if (!auto)
        {
            this.DrawActivePicker();
        }

        if (!string.IsNullOrEmpty(this.status))
        {
            ImGui.TextDisabled(this.status);
        }

        ImGui.Separator();
        this.DrawTimelineList();

        var selected = this.FindSelected();
        if (selected is not null)
        {
            ImGui.Separator();
            this.DrawEditor(selected);
        }
    }

    private void DrawActivePicker()
    {
        var active = this.FindById(this.configuration.Options.ActiveTimelineId);
        var label = active?.Name ?? "(none)";
        ImGui.SetNextItemWidth(240);
        if (ImGui.BeginCombo("Active timeline", label))
        {
            if (ImGui.Selectable("(none)", active is null))
            {
                this.configuration.Options.ActiveTimelineId = null;
                this.save();
                this.reloadRunner();
            }

            foreach (var timeline in this.configuration.Timelines)
            {
                if (ImGui.Selectable(timeline.Name, timeline.Id == this.configuration.Options.ActiveTimelineId))
                {
                    this.configuration.Options.ActiveTimelineId = timeline.Id;
                    this.save();
                    this.reloadRunner();
                }
            }

            ImGui.EndCombo();
        }
    }

    private void DrawTimelineList()
    {
        if (this.configuration.Timelines.Count == 0)
        {
            ImGui.TextDisabled("No timelines yet. Use \"Propose from last fight\" after a pull, or \"New\".");
            return;
        }

        if (!ImGui.BeginTable("timelines", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            return;
        }

        ImGui.TableSetupColumn("Name");
        ImGui.TableSetupColumn("Zone", ImGuiTableColumnFlags.WidthFixed, 70f);
        ImGui.TableSetupColumn("On", ImGuiTableColumnFlags.WidthFixed, 36f);
        ImGui.TableSetupColumn("Entries", ImGuiTableColumnFlags.WidthFixed, 60f);
        ImGui.TableSetupColumn("##ops", ImGuiTableColumnFlags.WidthFixed, 170f);
        ImGui.TableHeadersRow();

        TimelineDefinition? toDelete = null;

        for (var i = 0; i < this.configuration.Timelines.Count; i++)
        {
            var timeline = this.configuration.Timelines[i];
            ImGui.PushID(i);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            if (ImGui.Selectable(timeline.Name, this.selectedId == timeline.Id))
            {
                this.selectedId = timeline.Id;
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(timeline.TerritoryId == 0 ? "any" : timeline.TerritoryId.ToString(CultureInfo.InvariantCulture));

            ImGui.TableNextColumn();
            var enabled = timeline.Enabled;
            if (ImGui.Checkbox("##en", ref enabled))
            {
                timeline.Enabled = enabled;
                this.save();
                this.reloadRunner();
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(timeline.Entries.Count.ToString(CultureInfo.InvariantCulture));

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("Edit"))
            {
                this.selectedId = timeline.Id;
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Export"))
            {
                ImGui.SetClipboardText(TimelineCodec.Export(timeline));
                this.status = $"Copied \"{timeline.Name}\" to the clipboard.";
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Delete"))
            {
                toDelete = timeline;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (toDelete is not null)
        {
            this.configuration.Timelines.Remove(toDelete);
            if (this.selectedId == toDelete.Id)
            {
                this.selectedId = null;
            }

            this.save();
            this.reloadRunner();
        }
    }

    private void DrawEditor(TimelineDefinition timeline)
    {
        ImGui.TextDisabled($"Editing: {timeline.Name}");

        var name = timeline.Name;
        if (ImGui.InputText("Name", ref name, 128))
        {
            timeline.Name = name;
            this.save();
        }

        var zone = (int)timeline.TerritoryId;
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("Zone id (0 = any)", ref zone))
        {
            timeline.TerritoryId = (uint)Math.Max(0, zone);
            this.save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Use current zone"))
        {
            timeline.TerritoryId = this.currentZone().Id;
            this.save();
        }

        if (ImGui.Button("Add entry"))
        {
            timeline.Entries.Add(new TimelineEntry { Time = 0, Label = "New mechanic" });
            this.save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Sort by time"))
        {
            timeline.Sort();
            this.save();
        }

        ImGui.SameLine();
        ImGui.TextDisabled("Sync = event text that re-anchors the clock (name contains). Alert fires N s before.");

        this.DrawEntryTable(timeline);
    }

    private void DrawEntryTable(TimelineDefinition timeline)
    {
        if (timeline.Entries.Count == 0)
        {
            ImGui.TextDisabled("No entries. Add one, or propose a timeline from a fight.");
            return;
        }

        if (!ImGui.BeginTable("entries", 7, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            return;
        }

        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 72f);
        ImGui.TableSetupColumn("Label");
        ImGui.TableSetupColumn("Sync", ImGuiTableColumnFlags.WidthFixed, 140f);
        ImGui.TableSetupColumn("Alert s", ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableSetupColumn("Alert text", ImGuiTableColumnFlags.WidthFixed, 150f);
        ImGui.TableSetupColumn("Snd", ImGuiTableColumnFlags.WidthFixed, 52f);
        ImGui.TableSetupColumn("##x", ImGuiTableColumnFlags.WidthFixed, 28f);
        ImGui.TableHeadersRow();

        int? toRemove = null;
        var changed = false;

        for (var i = 0; i < timeline.Entries.Count; i++)
        {
            var entry = timeline.Entries[i];
            ImGui.PushID(i);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var time = entry.Time;
            if (ImGui.InputDouble("##t", ref time))
            {
                entry.Time = time;
                changed = true;
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var label = entry.Label;
            if (ImGui.InputText("##l", ref label, 128))
            {
                entry.Label = label;
                changed = true;
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var sync = entry.SyncPattern;
            if (ImGui.InputText("##s", ref sync, 128))
            {
                entry.SyncPattern = sync;
                changed = true;
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var alertS = entry.AlertSecondsBefore;
            if (ImGui.InputDouble("##as", ref alertS))
            {
                entry.AlertSecondsBefore = Math.Max(0, alertS);
                changed = true;
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var alertText = entry.AlertText;
            if (ImGui.InputText("##at", ref alertText, 128))
            {
                entry.AlertText = alertText;
                changed = true;
            }

            ImGui.TableNextColumn();
            ImGui.SetNextItemWidth(-1);
            var sound = entry.AlertSound;
            if (ImGui.InputInt("##snd", ref sound))
            {
                entry.AlertSound = Math.Clamp(sound, 0, 16);
                changed = true;
            }

            ImGui.TableNextColumn();
            if (ImGui.SmallButton("X"))
            {
                toRemove = i;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();

        if (toRemove is { } index)
        {
            timeline.Entries.RemoveAt(index);
            changed = true;
        }

        if (changed)
        {
            this.save();
        }
    }

    private void ProposeFromLastFight()
    {
        var recorded = this.recorder.Snapshot();
        if (recorded.Count == 0)
        {
            this.status = "No recorded fight yet — pull a boss first, then try again.";
            return;
        }

        var (id, name) = this.currentZone();
        var proposed = TimelineProposer.Propose(recorded, name, id);
        if (proposed.Entries.Count == 0)
        {
            this.status = "Nothing timeline-worthy was recorded (no enemy casts or debuffs).";
            return;
        }

        this.configuration.Timelines.Add(proposed);
        this.selectedId = proposed.Id;
        this.save();
        this.status = $"Proposed \"{proposed.Name}\" with {proposed.Entries.Count} entries — review below.";
    }

    private void ImportFromClipboard()
    {
        var result = TimelineCodec.Import(ImGui.GetClipboardText());
        if (!result.Success || result.Timeline is null)
        {
            this.status = $"Import failed: {result.Error}";
            return;
        }

        this.configuration.Timelines.Add(result.Timeline);
        this.selectedId = result.Timeline.Id;
        this.save();
        this.status = $"Imported \"{result.Timeline.Name}\".";
    }

    private TimelineDefinition? FindSelected() => this.FindById(this.selectedId);

    private TimelineDefinition? FindById(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var timeline in this.configuration.Timelines)
        {
            if (timeline.Id == id)
            {
                return timeline;
            }
        }

        return null;
    }

    private static string FormatClock(double seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var total = (int)seconds;
        return $"{total / 60:0}:{total % 60:00}";
    }
}
