using System.Collections.Generic;

using Callouts.Core.Engine;
using Callouts.Core.Rules;
using Callouts.Core.Suggestions;

using Xunit;

namespace Callouts.Tests;

public sealed class SuggestionsTests
{
    private static readonly HashSet<string> NoneIgnored = [];
    private static readonly List<Rule> NoRules = [];

    private static TriggerEvent EnemyCast(int id = 100, string name = "Ultima", bool targetSelf = false)
        => new() { Kind = TriggerKind.Cast, ActionId = id, ActionName = name, CasterIsEnemy = true, TargetIsSelf = targetSelf };

    private static TriggerEvent StatusGain(int id = 50, string name = "Doom", bool self = true, bool party = false)
        => new() { Kind = TriggerKind.Status, StatusId = id, StatusName = name, StatusGained = true, BearerIsSelf = self, BearerInParty = party };

    [Fact]
    public void EnemyCast_Aggregates_WithCountAndCategory()
    {
        var c = new SuggestionCollector();
        c.Observe(EnemyCast());
        c.Observe(EnemyCast());
        c.Observe(EnemyCast(targetSelf: true));

        var s = Assert.Single(c.GetSuggestions(NoRules, NoneIgnored));
        Assert.Equal(3, s.Count);
        Assert.Equal(SuggestionCategory.EnemyCasts, s.Category);
        Assert.Contains("Ultima", s.Title);
        Assert.Equal(TriggerKind.Cast, s.ProposedSource.Kind);
        Assert.Equal(100, s.ProposedSource.ActionId);
    }

    [Fact]
    public void FriendlyOrOwnCast_IsIgnored()
    {
        var c = new SuggestionCollector();
        c.Observe(new TriggerEvent { Kind = TriggerKind.Cast, ActionId = 7, ActionName = "Attack", CasterIsEnemy = false });

        Assert.Empty(c.GetSuggestions(NoRules, NoneIgnored));
    }

    [Fact]
    public void StatusGain_Self_vs_Party_Categorized()
    {
        var c = new SuggestionCollector();
        c.Observe(StatusGain(self: true, party: false));
        c.Observe(StatusGain(id: 51, name: "Bleed", self: false, party: true));

        var suggestions = c.GetSuggestions(NoRules, NoneIgnored);
        Assert.Equal(2, suggestions.Count);
        Assert.Contains(suggestions, s => s.Category == SuggestionCategory.DebuffsOnYou);
        Assert.Contains(suggestions, s => s.Category == SuggestionCategory.PartyEffects);
    }

    [Fact]
    public void StatusRemoved_Or_OnStranger_IsIgnored()
    {
        var c = new SuggestionCollector();
        c.Observe(new TriggerEvent { Kind = TriggerKind.Status, StatusId = 9, StatusGained = false, BearerIsSelf = true });
        c.Observe(new TriggerEvent { Kind = TriggerKind.Status, StatusId = 9, StatusGained = true, BearerIsSelf = false, BearerInParty = false });

        Assert.Empty(c.GetSuggestions(NoRules, NoneIgnored));
    }

    [Fact]
    public void CoveredByExistingRule_IsFlagged()
    {
        var c = new SuggestionCollector();
        c.Observe(EnemyCast(id: 200));

        var rules = new List<Rule> { new() { Source = new SourceSpec { Kind = TriggerKind.Cast, ActionId = 200 } } };
        Assert.True(Assert.Single(c.GetSuggestions(rules, NoneIgnored)).Covered);

        // Same candidate, no matching rule → not covered.
        Assert.False(Assert.Single(c.GetSuggestions(NoRules, NoneIgnored)).Covered);
    }

    [Fact]
    public void IgnoredKey_IsExcluded()
    {
        var c = new SuggestionCollector();
        c.Observe(EnemyCast(id: 300));
        var key = c.GetSuggestions(NoRules, NoneIgnored)[0].Key;

        Assert.Empty(c.GetSuggestions(NoRules, new HashSet<string> { key }));
    }

    [Fact]
    public void Ranking_TargetedYou_And_HigherCount_First()
    {
        var c = new SuggestionCollector();
        // Candidate A: 2 sightings, never targeted you.
        c.Observe(EnemyCast(id: 1, name: "A"));
        c.Observe(EnemyCast(id: 1, name: "A"));
        // Candidate B: 2 sightings, both targeted you → should outrank A.
        c.Observe(EnemyCast(id: 2, name: "B", targetSelf: true));
        c.Observe(EnemyCast(id: 2, name: "B", targetSelf: true));

        var ranked = c.GetSuggestions(NoRules, NoneIgnored);
        Assert.Equal("B", ranked[0].ProposedSource.ActionId == 2 ? "B" : "A");
        Assert.True(ranked[0].Score >= ranked[1].Score);
    }

    [Fact]
    public void ToRule_ProducesUsableRule()
    {
        var c = new SuggestionCollector();
        c.Observe(StatusGain(id: 910, name: "Doom", self: true));

        var rule = c.GetSuggestions(NoRules, NoneIgnored)[0].ToRule();
        Assert.Equal(TriggerKind.Status, rule.Source.Kind);
        Assert.Equal(910, rule.Source.StatusId);
        Assert.True(rule.Outputs.Echo.Enabled);
        Assert.True(RuleValidator.IsValid(rule));
    }

    [Fact]
    public void Scorer_DangerSignals_RaiseScore()
    {
        var plain = new Candidate { Key = "k1", Kind = TriggerKind.Cast, Count = 2 };
        var dangerous = new Candidate { Key = "k2", Kind = TriggerKind.Cast, Count = 2, MaxCastTimeSeconds = 5, AoeShape = AoeShape.Circle };

        Assert.True(SuggestionScorer.Score(dangerous) > SuggestionScorer.Score(plain));
    }
}
