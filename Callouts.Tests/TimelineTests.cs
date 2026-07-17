using System.Collections.Generic;
using System.Linq;

using Callouts.Core.Engine;
using Callouts.Core.Timeline;

using Xunit;

namespace Callouts.Tests;

public sealed class TimelineRunnerTests
{
    private static TimelineDefinition Def(params TimelineEntry[] entries)
        => new() { Name = "t", Entries = entries.ToList() };

    private static TriggerEvent Cast(string name)
        => new() { Kind = TriggerKind.Cast, CasterIsEnemy = true, ActionName = name };

    [Fact]
    public void Upcoming_ListsFutureEntriesNearestFirst_WithinLookAhead()
    {
        var runner = new TimelineRunner();
        runner.Load(Def(
            new TimelineEntry { Time = 5, Label = "A" },
            new TimelineEntry { Time = 12, Label = "B" },
            new TimelineEntry { Time = 40, Label = "C" }));
        runner.Start();

        var upcoming = runner.Upcoming(wallElapsed: 2, lookAhead: 30);

        Assert.Equal(new[] { "A", "B" }, upcoming.Select(u => u.Entry.Label));
        Assert.Equal(3, upcoming[0].SecondsUntil, precision: 3);
        Assert.Equal(10, upcoming[1].SecondsUntil, precision: 3);
    }

    [Fact]
    public void Observe_SnapsClock_WhenSyncedMechanicSeenNearItsTime()
    {
        var runner = new TimelineRunner();
        runner.Load(Def(
            new TimelineEntry { Time = 10, Label = "Ultima", SyncPattern = "Ultima" },
            new TimelineEntry { Time = 20, Label = "Enrage" }));
        runner.Start();

        // The synced mechanic actually happens at wall-time 12 (2s of drift). It's within the window,
        // so the clock snaps: entry "Ultima" (t=10) is now "now", pushing Enrage to +10.
        var snapped = runner.Observe(Cast("Ultima"), wallElapsed: 12);

        Assert.True(snapped);
        Assert.Equal(10, runner.CurrentTime(12), precision: 3);
        var upcoming = runner.Upcoming(wallElapsed: 12, lookAhead: 30);
        var enrage = upcoming.Single(u => u.Entry.Label == "Enrage");
        Assert.Equal(10, enrage.SecondsUntil, precision: 3);
    }

    [Fact]
    public void Observe_IgnoresMatchOutsideWindow()
    {
        var runner = new TimelineRunner();
        runner.Load(Def(new TimelineEntry { Time = 10, SyncPattern = "Ultima", WindowBefore = 2.5, WindowAfter = 2.5 }));
        runner.Start();

        // now = 20 (wall), far past the window around t=10 -> no snap.
        Assert.False(runner.Observe(Cast("Ultima"), wallElapsed: 20));
    }

    [Fact]
    public void Advance_FiresPreAlertOnce_AtLeadTime()
    {
        var runner = new TimelineRunner();
        runner.Load(Def(new TimelineEntry
        {
            Time = 10,
            Label = "Stack",
            AlertSecondsBefore = 3,
            AlertText = "Stack soon",
            AlertSound = 6,
        }));
        runner.Start();

        Assert.Empty(runner.Advance(wallElapsed: 6));      // before lead time (fire at t=7)

        var fired = runner.Advance(wallElapsed: 7.5);       // within [7, 11]
        Assert.Contains(fired, a => a.Kind == AlertOutputKind.Toast && a.Text == "Stack soon");
        Assert.Contains(fired, a => a.Kind == AlertOutputKind.Echo && a.Text == "Stack soon");
        Assert.Contains(fired, a => a.Kind == AlertOutputKind.Sound && a.SoundEffectId == 6);

        Assert.Empty(runner.Advance(wallElapsed: 8));       // does not re-fire
    }

    [Fact]
    public void EmptyDefinition_DoesNotRun()
    {
        var runner = new TimelineRunner();
        runner.Load(Def());
        runner.Start();
        Assert.False(runner.Running);
        Assert.Empty(runner.Advance(1));
        Assert.Empty(runner.Upcoming(1, 30));
    }
}

public sealed class TimelineProposerTests
{
    private static TimedEvent Cast(double t, string name)
        => new(t, new TriggerEvent { Kind = TriggerKind.Cast, CasterIsEnemy = true, ActionName = name });

    private static TimedEvent Debuff(double t, string name, bool self = true)
        => new(t, new TriggerEvent { Kind = TriggerKind.Status, StatusGained = true, IsDebuff = true, BearerIsSelf = self, StatusName = name });

    [Fact]
    public void Propose_KeepsEnemyCastsAndDebuffs_WithSyncAndTime()
    {
        var recorded = new List<TimedEvent>
        {
            Cast(3.04, "Ultima"),
            Debuff(8.2, "Doom"),
        };

        var tl = TimelineProposer.Propose(recorded, "Sigmascape V4.0", territoryId: 795);

        Assert.Equal(795u, tl.TerritoryId);
        Assert.Equal("Sigmascape V4.0 (proposed)", tl.Name);
        Assert.Equal(2, tl.Entries.Count);
        Assert.Equal(3.0, tl.Entries[0].Time, precision: 3);
        Assert.Equal("Ultima", tl.Entries[0].Label);
        Assert.Equal("Ultima", tl.Entries[0].SyncPattern);
        Assert.Equal("Doom", tl.Entries[1].Label);
    }

    [Fact]
    public void Propose_CollapsesRepeatsWithinDedupeWindow()
    {
        var recorded = new List<TimedEvent>
        {
            Cast(1.0, "Auto"),
            Cast(1.5, "Auto"),   // within 2s -> collapsed
            Cast(4.0, "Auto"),   // > 2s after the last kept -> new entry
        };

        var tl = TimelineProposer.Propose(recorded, "z", 1);

        Assert.Equal(2, tl.Entries.Count);
        Assert.Equal(1.0, tl.Entries[0].Time, precision: 3);
        Assert.Equal(4.0, tl.Entries[1].Time, precision: 3);
    }

    [Fact]
    public void Propose_IgnoresFriendlyCastsAndBuffsAndStrangerDebuffs()
    {
        var recorded = new List<TimedEvent>
        {
            new(1, new TriggerEvent { Kind = TriggerKind.Cast, CasterIsEnemy = false, ActionName = "Cure" }),
            new(2, new TriggerEvent { Kind = TriggerKind.Status, StatusGained = true, IsDebuff = false, BearerIsSelf = true, StatusName = "Well Fed" }),
            new(3, new TriggerEvent { Kind = TriggerKind.Status, StatusGained = true, IsDebuff = true, BearerIsSelf = false, BearerInParty = false, StatusName = "Stranger Debuff" }),
        };

        Assert.Empty(TimelineProposer.Propose(recorded, "z", 1).Entries);
    }
}

public sealed class TimelineCodecTests
{
    [Fact]
    public void ExportImport_RoundTrips()
    {
        var tl = new TimelineDefinition
        {
            Name = "My fight",
            TerritoryId = 42,
            Entries =
            {
                new TimelineEntry { Time = 5, Label = "A", SyncPattern = "A", AlertSecondsBefore = 3, AlertText = "soon" },
            },
        };

        var code = TimelineCodec.Export(tl);
        Assert.StartsWith("TL1|", code);

        var result = TimelineCodec.Import(code);
        Assert.True(result.Success);
        Assert.Equal("My fight", result.Timeline!.Name);
        Assert.Equal(42u, result.Timeline.TerritoryId);
        Assert.Single(result.Timeline.Entries);
        Assert.Equal("A", result.Timeline.Entries[0].SyncPattern);
    }

    [Fact]
    public void Import_RejectsGarbage()
    {
        Assert.False(TimelineCodec.Import(null).Success);
        Assert.False(TimelineCodec.Import("nope").Success);
        Assert.False(TimelineCodec.Import("TL1|not-base64!").Success);
    }
}

public sealed class TimelineSelectorTests
{
    private static TimelineDefinition Tl(uint zone, bool enabled = true, string? id = null)
        => new() { Id = id ?? $"z{zone}", TerritoryId = zone, Enabled = enabled };

    [Fact]
    public void AutoByZone_PrefersExactZoneOverAnyZone()
    {
        var timelines = new List<TimelineDefinition> { Tl(0), Tl(100) };

        var selected = TimelineSelector.Select(timelines, territoryId: 100, autoByZone: true, activeId: null);

        Assert.Equal(100u, selected!.TerritoryId);
    }

    [Fact]
    public void AutoByZone_FallsBackToAnyZone()
    {
        var timelines = new List<TimelineDefinition> { Tl(0) };
        Assert.Equal(0u, TimelineSelector.Select(timelines, 555, true, null)!.TerritoryId);
    }

    [Fact]
    public void AutoByZone_SkipsDisabled()
    {
        var timelines = new List<TimelineDefinition> { Tl(100, enabled: false) };
        Assert.Null(TimelineSelector.Select(timelines, 100, true, null));
    }

    [Fact]
    public void ManualMode_UsesActiveId()
    {
        var timelines = new List<TimelineDefinition> { Tl(1, id: "keep"), Tl(2, id: "other") };
        Assert.Equal("keep", TimelineSelector.Select(timelines, 999, autoByZone: false, activeId: "keep")!.Id);
    }
}
