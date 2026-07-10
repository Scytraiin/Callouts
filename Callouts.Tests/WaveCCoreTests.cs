using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class ScopeMatcherTests
{
    private static TriggerEvent Evt(uint territory = 100, bool combat = false, bool duty = false)
        => new() { Kind = TriggerKind.Chat, TerritoryId = territory, InCombat = combat, InDuty = duty };

    [Fact]
    public void EmptyScope_AlwaysInScope()
    {
        Assert.True(ScopeMatcher.InScope(new RuleScope(), Evt()));
    }

    [Fact]
    public void OnlyInCombat_RequiresCombat()
    {
        var scope = new RuleScope { OnlyInCombat = true };
        Assert.False(ScopeMatcher.InScope(scope, Evt(combat: false)));
        Assert.True(ScopeMatcher.InScope(scope, Evt(combat: true)));
    }

    [Fact]
    public void OnlyInDuty_RequiresDuty()
    {
        var scope = new RuleScope { OnlyInDuty = true };
        Assert.False(ScopeMatcher.InScope(scope, Evt(duty: false)));
        Assert.True(ScopeMatcher.InScope(scope, Evt(duty: true)));
    }

    [Fact]
    public void TerritoryList_RestrictsToListedZones()
    {
        var scope = new RuleScope { TerritoryIds = { 100, 200 } };
        Assert.True(ScopeMatcher.InScope(scope, Evt(territory: 100)));
        Assert.False(ScopeMatcher.InScope(scope, Evt(territory: 300)));
    }

    [Fact]
    public void Engine_RespectsScope()
    {
        var rule = new Rule
        {
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "pull" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "go" } },
            Scope = new RuleScope { OnlyInCombat = true },
        };
        var engine = new RuleEngine(() => new[] { rule });

        Assert.Empty(engine.Process(new TriggerEvent { Kind = TriggerKind.Chat, Channel = 10, Message = "pull", InCombat = false }));
        Assert.Single(engine.Process(new TriggerEvent { Kind = TriggerKind.Chat, Channel = 10, Message = "pull", InCombat = true }));
    }
}

public sealed class EventBufferTests
{
    private static EventRecord Rec(TriggerKind kind, string display)
        => new() { Kind = kind, Display = display, Event = new TriggerEvent { Kind = kind } };

    [Fact]
    public void Snapshot_IsNewestFirst()
    {
        var buffer = new EventBuffer(10);
        buffer.Add(Rec(TriggerKind.Chat, "first"));
        buffer.Add(Rec(TriggerKind.Chat, "second"));

        var snapshot = buffer.Snapshot();
        Assert.Equal("second", snapshot[0].Display);
        Assert.Equal("first", snapshot[1].Display);
    }

    [Fact]
    public void OverCapacity_DropsOldest()
    {
        var buffer = new EventBuffer(2);
        buffer.Add(Rec(TriggerKind.Chat, "a"));
        buffer.Add(Rec(TriggerKind.Chat, "b"));
        buffer.Add(Rec(TriggerKind.Chat, "c"));

        var snapshot = buffer.Snapshot();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal("c", snapshot[0].Display);
        Assert.Equal("b", snapshot[1].Display);
    }

    [Fact]
    public void Paused_DropsNewEvents()
    {
        var buffer = new EventBuffer(10) { Paused = true };
        buffer.Add(Rec(TriggerKind.Chat, "x"));

        Assert.Empty(buffer.Snapshot());
    }
}

public sealed class RuleListViewTests
{
    private static Rule R(string name, TriggerKind kind = TriggerKind.Chat, bool enabled = true, params string[] tags)
        => new() { Name = name, Enabled = enabled, Source = new SourceSpec { Kind = kind }, Tags = [.. tags] };

    [Fact]
    public void Filter_BySearch_MatchesNameCaseInsensitive()
    {
        var rules = new[] { R("Ready check"), R("Pull timer") };
        var shown = RuleListView.Filter(rules, search: "ready");
        Assert.Single(shown);
        Assert.Equal("Ready check", shown[0].Name);
    }

    [Fact]
    public void Filter_BySource_And_EnabledOnly()
    {
        var rules = new[]
        {
            R("a", TriggerKind.Chat, enabled: true),
            R("b", TriggerKind.Cast, enabled: true),
            R("c", TriggerKind.Chat, enabled: false),
        };

        Assert.Equal(2, RuleListView.Filter(rules, sourceKind: TriggerKind.Chat).Count);
        Assert.Single(RuleListView.Filter(rules, sourceKind: TriggerKind.Chat, enabledOnly: true));
    }

    [Fact]
    public void Filter_ByTag()
    {
        var rules = new[] { R("a", tags: "raid"), R("b", tags: "solo") };
        var shown = RuleListView.Filter(rules, tag: "raid");
        Assert.Single(shown);
        Assert.Equal("a", shown[0].Name);
    }

    [Fact]
    public void AllTags_AreDistinctAndSorted()
    {
        var rules = new[] { R("a", tags: "b"), R("c", tags: "a"), R("d", tags: "b") };
        Assert.Equal(new[] { "a", "b" }, RuleListView.AllTags(rules));
    }
}
