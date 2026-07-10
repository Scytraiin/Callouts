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

    private static TriggerEvent StatusGain(int id = 50, string name = "Doom", bool self = true, bool party = false, bool debuff = true)
        => new() { Kind = TriggerKind.Status, StatusId = id, StatusName = name, StatusGained = true, BearerIsSelf = self, BearerInParty = party, IsDebuff = debuff };

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
    public void RollEncounter_ThisFightResets_ThisSessionKeepsBoth()
    {
        var c = new SuggestionCollector();
        c.Observe(EnemyCast(id: 1, name: "A"));
        c.Observe(EnemyCast(id: 1, name: "A"));

        c.RollEncounter(); // fight 1 ends

        c.Observe(EnemyCast(id: 2, name: "B"));

        // This fight = only the current encounter (B).
        var fight = c.GetSuggestions(NoRules, NoneIgnored, EncounterScope.ThisFight);
        Assert.Single(fight);
        Assert.Equal(2, fight[0].ProposedSource.ActionId);

        // This session = both encounters (A + B).
        var session = c.GetSuggestions(NoRules, NoneIgnored, EncounterScope.ThisSession);
        Assert.Equal(2, session.Count);
    }

    [Fact]
    public void RollEncounter_MergesCountsForSameKeyAcrossEncounters()
    {
        var c = new SuggestionCollector();
        c.Observe(EnemyCast(id: 5, name: "Repeat"));
        c.RollEncounter();
        c.Observe(EnemyCast(id: 5, name: "Repeat"));
        c.Observe(EnemyCast(id: 5, name: "Repeat"));

        var session = c.GetSuggestions(NoRules, NoneIgnored, EncounterScope.ThisSession);
        Assert.Equal(3, Assert.Single(session).Count);
    }

    [Fact]
    public void CopyCode_RoundTripsToSameRule()
    {
        var c = new SuggestionCollector();
        c.Observe(StatusGain(id: 910, name: "Doom", self: true));
        var rule = c.GetSuggestions(NoRules, NoneIgnored)[0].ToRule();

        var result = RuleCodec.Import(RuleCodec.Export([rule]));
        Assert.True(result.Success);
        Assert.Equal(910, Assert.Single(result.Rules).Source.StatusId);
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

    private static TriggerEvent Marker(string key = "spread", string raw = "", bool self = true, bool party = false)
        => new() { Kind = TriggerKind.HeadMarker, MarkerKey = key, RawValue = raw, TargetIsSelf = self, TargetInParty = party };

    [Fact]
    public void HeadMarker_Named_Aggregates_AsAdvancedMarkerCategory()
    {
        var c = new SuggestionCollector();
        c.Observe(Marker(key: "spread", self: true));
        c.Observe(Marker(key: "spread", self: true));

        var s = Assert.Single(c.GetSuggestions(NoRules, NoneIgnored));
        Assert.Equal(SuggestionCategory.Markers, s.Category);
        Assert.True(s.Advanced);
        Assert.Equal(TriggerKind.HeadMarker, s.ProposedSource.Kind);
        Assert.Equal("spread", s.ProposedSource.MarkerKey);
        Assert.Contains("spread", s.Title);
    }

    [Fact]
    public void HeadMarker_RawFallback_WhenNoNamedKey()
    {
        var c = new SuggestionCollector();
        c.Observe(Marker(key: string.Empty, raw: "vfx/lockon/eff/unknown", self: true));

        var s = Assert.Single(c.GetSuggestions(NoRules, NoneIgnored));
        Assert.Equal("vfx/lockon/eff/unknown", s.ProposedSource.MarkerKey);
    }

    [Fact]
    public void HeadMarker_OnStranger_IsIgnored()
    {
        var c = new SuggestionCollector();
        c.Observe(Marker(key: "spread", self: false, party: false));

        Assert.Empty(c.GetSuggestions(NoRules, NoneIgnored));
    }

    [Fact]
    public void HeadMarker_CoveredByExistingRule()
    {
        var c = new SuggestionCollector();
        c.Observe(Marker(key: "spread", self: true));

        var rules = new List<Rule> { new() { Source = new SourceSpec { Kind = TriggerKind.HeadMarker, MarkerKey = "spread" } } };
        Assert.True(Assert.Single(c.GetSuggestions(rules, NoneIgnored)).Covered);
    }

    [Fact]
    public void Scorer_DangerSignals_RaiseScore()
    {
        var plain = new Candidate { Key = "k1", Kind = TriggerKind.Cast, Count = 2 };
        var dangerous = new Candidate { Key = "k2", Kind = TriggerKind.Cast, Count = 2, MaxCastTimeSeconds = 5, AoeShape = AoeShape.Circle };

        Assert.True(SuggestionScorer.Score(dangerous) > SuggestionScorer.Score(plain));
    }

    [Fact]
    public void SelfBuff_IsExcluded_OnlyDebuffsSuggested()
    {
        var c = new SuggestionCollector();
        c.Observe(StatusGain(id: 48, name: "Well Fed", self: true, debuff: false));

        Assert.Empty(c.GetSuggestions(NoRules, NoneIgnored));
    }

    [Fact]
    public void Enrichment_LongAoeCast_OutranksInstantSingleTarget_AndSetsHint()
    {
        var c = new SuggestionCollector();
        // Dangerous: long cast, big circle.
        c.Observe(new TriggerEvent { Kind = TriggerKind.Cast, ActionId = 1, ActionName = "Flare", CasterIsEnemy = true, CastTimeSeconds = 5, AoeShape = AoeShape.Circle, AoeRange = 8 });
        // Minor: instant single-target.
        c.Observe(new TriggerEvent { Kind = TriggerKind.Cast, ActionId = 2, ActionName = "Poke", CasterIsEnemy = true, CastTimeSeconds = 0, AoeShape = AoeShape.Single });

        var ranked = c.GetSuggestions(NoRules, NoneIgnored);
        Assert.Equal(1, ranked[0].ProposedSource.ActionId);
        Assert.Contains("Circle", ranked[0].Hint);
        Assert.Contains("cast", ranked[0].Hint);
    }
}
