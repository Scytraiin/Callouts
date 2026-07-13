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

    private static EventRecord Chat(int n) => new() { Kind = TriggerKind.Chat, Display = $"c{n}", Event = new TriggerEvent { Kind = TriggerKind.Chat } };

    private static EventRecord EnemyCast() => new() { Kind = TriggerKind.Cast, Display = "Ultima", Event = new TriggerEvent { Kind = TriggerKind.Cast, CasterIsEnemy = true } };

    [Fact]
    public void NoisyCategory_DoesNotEvictOthers()
    {
        var buffer = new EventBuffer(defaultLimit: 3);
        for (var i = 0; i < 20; i++)
        {
            buffer.Add(Chat(i)); // floods the Chat partition well past its limit
        }

        buffer.Add(EnemyCast());

        Assert.Equal(3, buffer.CountFor(EventCategory.Chat));      // chat trimmed to its own limit
        Assert.Equal(1, buffer.CountFor(EventCategory.EnemyCast)); // the cast survived the flood
    }

    [Fact]
    public void PerCategoryOverride_AppliesIndependentLimits()
    {
        var buffer = new EventBuffer(defaultLimit: 100);
        buffer.SetLimits(100, new Dictionary<EventCategory, int> { [EventCategory.Chat] = 2 });

        for (var i = 0; i < 5; i++)
        {
            buffer.Add(Chat(i));
            buffer.Add(EnemyCast());
        }

        Assert.Equal(2, buffer.CountFor(EventCategory.Chat));       // override
        Assert.Equal(5, buffer.CountFor(EventCategory.EnemyCast));  // default
    }

    [Fact]
    public void Global_NewestFirst_MergesAcrossCategories()
    {
        var buffer = new EventBuffer(100);
        buffer.Add(Chat(1));       // oldest
        buffer.Add(EnemyCast());   // middle
        buffer.Add(Chat(2));       // newest

        var order = new List<TriggerKind>();
        foreach (var r in buffer.EnumerateNewestFirst())
        {
            order.Add(r.Event.Kind);
        }

        Assert.Equal(new[] { TriggerKind.Chat, TriggerKind.Cast, TriggerKind.Chat }, order);
    }
}
