using System.Collections.Generic;

using Callouts.Core.Engine;

using Xunit;

namespace Callouts.Tests;

public sealed class EventCategorizerTests
{
    [Fact]
    public void Chat_IsChat()
        => Assert.Equal(EventCategory.Chat, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Chat }));

    [Fact]
    public void EnemyVsOtherCast()
    {
        Assert.Equal(EventCategory.EnemyCast, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Cast, CasterIsEnemy = true }));
        Assert.Equal(EventCategory.OtherCast, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Cast, CasterIsEnemy = false }));
    }

    [Fact]
    public void SelfDebuffVsBuff()
    {
        Assert.Equal(EventCategory.SelfDebuff, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Status, BearerIsSelf = true, IsDebuff = true }));
        Assert.Equal(EventCategory.SelfBuff, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Status, BearerIsSelf = true, IsDebuff = false }));
    }

    [Fact]
    public void PartyDebuffVsBuff()
    {
        Assert.Equal(EventCategory.PartyDebuff, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Status, BearerInParty = true, IsDebuff = true }));
        Assert.Equal(EventCategory.PartyBuff, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Status, BearerInParty = true, IsDebuff = false }));
    }

    [Fact]
    public void StatusOnStranger_IsOtherStatus()
        => Assert.Equal(EventCategory.OtherStatus, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Status, IsDebuff = true }));

    [Fact]
    public void DutyVfxMarker()
    {
        Assert.Equal(EventCategory.Duty, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.DutyEvent }));
        Assert.Equal(EventCategory.Vfx, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.Vfx }));
        Assert.Equal(EventCategory.HeadMarker, EventCategorizer.Categorize(new TriggerEvent { Kind = TriggerKind.HeadMarker }));
    }
}

public sealed class EventBufferCapacityTests
{
    private static EventRecord Rec(int n) => new() { Kind = TriggerKind.Chat, Display = $"e{n}", Event = new TriggerEvent { Kind = TriggerKind.Chat } };

    [Fact]
    public void HoldsLargeCapacity_AndCountReflectsIt()
    {
        var buffer = new EventBuffer(10_000);
        for (var i = 0; i < 5_000; i++)
        {
            buffer.Add(Rec(i));
        }

        Assert.Equal(5_000, buffer.Count);
    }

    [Fact]
    public void EnumerateNewestFirst_MatchesSnapshotOrder()
    {
        var buffer = new EventBuffer(10);
        buffer.Add(Rec(1));
        buffer.Add(Rec(2));

        var enumerated = new List<string>();
        foreach (var r in buffer.EnumerateNewestFirst())
        {
            enumerated.Add(r.Display);
        }

        Assert.Equal(new[] { "e2", "e1" }, enumerated);
    }
}
