using System;
using System.Collections.Generic;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Core.Suggestions;

/// <summary>A per-candidate aggregate accumulated during an encounter.</summary>
public sealed class Candidate
{
    public required string Key { get; init; }

    public required TriggerKind Kind { get; init; }

    public int Id { get; init; }

    public string Name { get; set; } = string.Empty;

    public string MarkerKey { get; set; } = string.Empty;

    public bool IsSelf { get; init; }

    public bool InParty { get; init; }

    public int Count { get; set; }

    public int TargetedMeCount { get; set; }

    // --- Danger signals (issue 019) ---
    public double MaxCastTimeSeconds { get; set; }

    public AoeShape AoeShape { get; set; } = AoeShape.None;

    public double AoeRange { get; set; }

    public bool IsDebuff { get; set; }

    public double DurationSeconds { get; set; }
}

/// <summary>
/// Watches the normalized event stream and aggregates the "interesting" observations of an encounter
/// into ranked <see cref="Suggestion"/>s. Dalamud-free and unit-tested: it consumes the same
/// <see cref="TriggerEvent"/>s that drive the engine (the plugin tees them in). Session-only — nothing
/// is persisted (the ignore list lives in config and is passed to <see cref="GetSuggestions"/>).
/// </summary>
public sealed class SuggestionCollector
{
    private readonly Dictionary<string, Candidate> current = new();

    public void Observe(TriggerEvent evt)
    {
        var key = KeyFor(evt);
        if (key is null)
        {
            return;
        }

        if (!this.current.TryGetValue(key, out var candidate))
        {
            candidate = NewCandidate(key, evt);
            this.current[key] = candidate;
        }

        candidate.Count++;
        if (TargetedMe(evt))
        {
            candidate.TargetedMeCount++;
        }

        Enrich(candidate, evt);
    }

    public IReadOnlyList<Suggestion> GetSuggestions(IReadOnlyList<Rule> existingRules, ISet<string> ignored)
    {
        var list = new List<Suggestion>();
        foreach (var candidate in this.current.Values)
        {
            if (ignored.Contains(candidate.Key))
            {
                continue;
            }

            list.Add(Build(candidate, existingRules));
        }

        list.Sort((a, b) => b.Score.CompareTo(a.Score));
        return list;
    }

    public int CandidateCount => this.current.Count;

    public void Clear() => this.current.Clear();

    // ---- keying ----

    private static string? KeyFor(TriggerEvent evt) => evt.Kind switch
    {
        // Only enemy casts are worth suggesting (this also excludes the player's own casts).
        TriggerKind.Cast when evt.CasterIsEnemy => $"cast:{evt.ActionId}",

        // Only status gains on self / party members.
        TriggerKind.Status when evt.StatusGained && (evt.BearerIsSelf || evt.BearerInParty)
            => $"status:{evt.StatusId}:{(evt.BearerIsSelf ? "self" : "party")}",

        _ => null, // advanced kinds handled in issue 021
    };

    private static Candidate NewCandidate(string key, TriggerEvent evt) => new()
    {
        Key = key,
        Kind = evt.Kind,
        Id = evt.Kind == TriggerKind.Cast ? evt.ActionId : evt.StatusId,
        IsSelf = evt.Kind == TriggerKind.Cast ? evt.TargetIsSelf : evt.BearerIsSelf,
        InParty = evt.Kind == TriggerKind.Cast ? evt.TargetInParty : evt.BearerInParty,
    };

    private static bool TargetedMe(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Cast => evt.TargetIsSelf,
        TriggerKind.Status => evt.BearerIsSelf,
        _ => false,
    };

    private static void Enrich(Candidate candidate, TriggerEvent evt)
    {
        if (evt.Kind == TriggerKind.Cast)
        {
            candidate.Name = string.IsNullOrEmpty(evt.ActionName) ? candidate.Name : evt.ActionName;
        }
        else if (evt.Kind == TriggerKind.Status)
        {
            candidate.Name = string.IsNullOrEmpty(evt.StatusName) ? candidate.Name : evt.StatusName;
        }
    }

    // ---- building ----

    private static Suggestion Build(Candidate c, IReadOnlyList<Rule> rules)
    {
        var name = ResolveName(c);
        var category = c.Kind == TriggerKind.Cast
            ? SuggestionCategory.EnemyCasts
            : c.IsSelf ? SuggestionCategory.DebuffsOnYou : SuggestionCategory.PartyEffects;

        return new Suggestion
        {
            Key = c.Key,
            Title = TitleFor(c, name),
            SuggestedName = name,
            Rationale = RationaleFor(c),
            Category = category,
            Score = SuggestionScorer.Score(c),
            Count = c.Count,
            Covered = IsCovered(c, rules),
            Advanced = c.Kind is TriggerKind.Vfx or TriggerKind.HeadMarker,
            ProposedSource = SourceFor(c),
            ProposedOutputs = OutputsFor(c, name),
        };
    }

    private static string ResolveName(Candidate c)
    {
        if (!string.IsNullOrEmpty(c.Name))
        {
            return c.Name;
        }

        return c.Kind == TriggerKind.Cast ? $"Action {c.Id}" : $"Status {c.Id}";
    }

    private static string TitleFor(Candidate c, string name) => c.Kind switch
    {
        TriggerKind.Cast => $"Enemy casts \"{name}\"",
        TriggerKind.Status when c.IsSelf => $"You gain \"{name}\"",
        TriggerKind.Status => $"Party gains \"{name}\"",
        _ => name,
    };

    private static string RationaleFor(Candidate c)
    {
        var text = $"seen {c.Count}×";
        if (c.TargetedMeCount > 0 && c.Kind == TriggerKind.Cast)
        {
            text += $" · hit you {c.TargetedMeCount}×";
        }

        return text;
    }

    private static SourceSpec SourceFor(Candidate c) => c.Kind switch
    {
        TriggerKind.Cast => new SourceSpec { Kind = TriggerKind.Cast, ActionId = c.Id, CasterScope = CasterScope.Enemy },
        TriggerKind.Status => new SourceSpec
        {
            Kind = TriggerKind.Status,
            StatusId = c.Id,
            StatusChange = StatusChangeFilter.Gained,
            Bearer = c.IsSelf ? BearerScope.Self : BearerScope.Party,
        },
        _ => new SourceSpec { Kind = c.Kind },
    };

    private static OutputSpec OutputsFor(Candidate c, string name)
    {
        var echoText = c.Kind switch
        {
            TriggerKind.Cast => "{caster} casts {action}!",
            TriggerKind.Status => "{status} on {bearer}!",
            _ => $"{name}!",
        };

        return new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = echoText } };
    }

    public static bool IsCovered(Candidate c, IReadOnlyList<Rule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.Source.Kind != c.Kind)
            {
                continue;
            }

            var covers = c.Kind switch
            {
                TriggerKind.Cast => rule.Source.ActionId == 0 || rule.Source.ActionId == c.Id,
                TriggerKind.Status => rule.Source.StatusId == 0 || rule.Source.StatusId == c.Id,
                _ => false,
            };

            if (covers)
            {
                return true;
            }
        }

        return false;
    }
}
