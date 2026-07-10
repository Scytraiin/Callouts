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
    private Dictionary<string, Candidate> previous = new();

    /// <summary>
    /// Ends the current encounter and starts a fresh one, keeping the just-finished encounter
    /// available under <see cref="EncounterScope.ThisSession"/> (the plugin calls this on combat-enter).
    /// </summary>
    public void RollEncounter()
    {
        if (this.current.Count == 0)
        {
            return; // nothing to roll (avoids wiping "previous" on repeated combat pulses)
        }

        this.previous = new Dictionary<string, Candidate>(this.current);
        this.current.Clear();
    }

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

    public IReadOnlyList<Suggestion> GetSuggestions(
        IReadOnlyList<Rule> existingRules,
        ISet<string> ignored,
        EncounterScope scope = EncounterScope.ThisFight)
    {
        var candidates = scope == EncounterScope.ThisSession ? this.MergeSession() : this.current.Values;

        var list = new List<Suggestion>();
        foreach (var candidate in candidates)
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

    public void Clear()
    {
        this.current.Clear();
        this.previous.Clear();
    }

    private IEnumerable<Candidate> MergeSession()
    {
        var merged = new Dictionary<string, Candidate>();
        foreach (var source in new[] { this.previous, this.current })
        {
            foreach (var c in source.Values)
            {
                if (!merged.TryGetValue(c.Key, out var m))
                {
                    m = new Candidate { Key = c.Key, Kind = c.Kind, Id = c.Id, IsSelf = c.IsSelf, InParty = c.InParty };
                    merged[c.Key] = m;
                }

                m.Count += c.Count;
                m.TargetedMeCount += c.TargetedMeCount;
                m.Name = string.IsNullOrEmpty(m.Name) ? c.Name : m.Name;
                m.MaxCastTimeSeconds = Math.Max(m.MaxCastTimeSeconds, c.MaxCastTimeSeconds);
                if (c.AoeShape != AoeShape.None)
                {
                    m.AoeShape = c.AoeShape;
                    m.AoeRange = c.AoeRange;
                }

                m.IsDebuff |= c.IsDebuff;
                m.DurationSeconds = Math.Max(m.DurationSeconds, c.DurationSeconds);
            }
        }

        return merged.Values;
    }

    // ---- keying ----

    private static string? KeyFor(TriggerEvent evt) => evt.Kind switch
    {
        // Only enemy casts are worth suggesting (this also excludes the player's own casts).
        TriggerKind.Cast when evt.CasterIsEnemy => $"cast:{evt.ActionId}",

        // Only debuff gains on self / party members (excludes buffs like Well Fed).
        TriggerKind.Status when evt.StatusGained && evt.IsDebuff && (evt.BearerIsSelf || evt.BearerInParty)
            => $"status:{evt.StatusId}:{(evt.BearerIsSelf ? "self" : "party")}",

        // Advanced tier (issue 021): head markers on self / party. Only reached when the advanced
        // source is running, so advanced-off/failed naturally produces no advanced suggestions.
        TriggerKind.HeadMarker when (evt.TargetIsSelf || evt.TargetInParty) && MarkerValue(evt).Length > 0
            => $"marker:{MarkerValue(evt)}:{(evt.TargetIsSelf ? "self" : "party")}",

        _ => null,
    };

    private static string MarkerValue(TriggerEvent evt)
        => string.IsNullOrEmpty(evt.MarkerKey) ? evt.RawValue : evt.MarkerKey;

    private static Candidate NewCandidate(string key, TriggerEvent evt) => new()
    {
        Key = key,
        Kind = evt.Kind,
        Id = evt.Kind switch { TriggerKind.Cast => evt.ActionId, TriggerKind.Status => evt.StatusId, _ => 0 },
        IsSelf = evt.Kind == TriggerKind.Status ? evt.BearerIsSelf : evt.TargetIsSelf,
        InParty = evt.Kind == TriggerKind.Status ? evt.BearerInParty : evt.TargetInParty,
    };

    private static bool TargetedMe(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Cast => evt.TargetIsSelf,
        TriggerKind.Status => evt.BearerIsSelf,
        TriggerKind.HeadMarker => evt.TargetIsSelf,
        _ => false,
    };

    private static void Enrich(Candidate candidate, TriggerEvent evt)
    {
        if (evt.Kind == TriggerKind.Cast)
        {
            candidate.Name = string.IsNullOrEmpty(evt.ActionName) ? candidate.Name : evt.ActionName;
            candidate.MaxCastTimeSeconds = Math.Max(candidate.MaxCastTimeSeconds, evt.CastTimeSeconds);
            if (evt.AoeShape != AoeShape.None)
            {
                candidate.AoeShape = evt.AoeShape;
                candidate.AoeRange = evt.AoeRange;
            }
        }
        else if (evt.Kind == TriggerKind.Status)
        {
            candidate.Name = string.IsNullOrEmpty(evt.StatusName) ? candidate.Name : evt.StatusName;
            candidate.IsDebuff = evt.IsDebuff;
            if (evt.DurationSeconds > 0)
            {
                candidate.DurationSeconds = evt.DurationSeconds;
            }
        }
        else if (evt.Kind == TriggerKind.HeadMarker)
        {
            candidate.MarkerKey = MarkerValue(evt);
            candidate.Name = candidate.MarkerKey;
        }
    }

    // ---- building ----

    private static Suggestion Build(Candidate c, IReadOnlyList<Rule> rules)
    {
        var name = ResolveName(c);
        var category = c.Kind switch
        {
            TriggerKind.Cast => SuggestionCategory.EnemyCasts,
            TriggerKind.HeadMarker or TriggerKind.Vfx => SuggestionCategory.Markers,
            _ => c.IsSelf ? SuggestionCategory.DebuffsOnYou : SuggestionCategory.PartyEffects,
        };

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
            Hint = HintFor(c),
            ProposedSource = SourceFor(c),
            ProposedOutputs = OutputsFor(c, name),
        };
    }

    private static string HintFor(Candidate c)
    {
        var parts = new List<string>();
        if (c.Kind == TriggerKind.Cast)
        {
            if (c.MaxCastTimeSeconds >= 0.1)
            {
                parts.Add($"{c.MaxCastTimeSeconds:0.0}s cast");
            }

            if (c.AoeShape is not (AoeShape.None or AoeShape.Single))
            {
                parts.Add(c.AoeRange > 0 ? $"{c.AoeShape} {c.AoeRange:0}y" : c.AoeShape.ToString());
            }
        }
        else if (c.Kind == TriggerKind.Status)
        {
            parts.Add(c.IsDebuff ? "debuff" : "buff");
            if (c.DurationSeconds > 0)
            {
                parts.Add($"{c.DurationSeconds:0}s");
            }
        }

        return string.Join(" · ", parts);
    }

    private static string ResolveName(Candidate c)
    {
        if (!string.IsNullOrEmpty(c.Name))
        {
            return c.Name;
        }

        return c.Kind switch
        {
            TriggerKind.Cast => $"Action {c.Id}",
            TriggerKind.Status => $"Status {c.Id}",
            _ => string.IsNullOrEmpty(c.MarkerKey) ? "Marker" : c.MarkerKey,
        };
    }

    private static string TitleFor(Candidate c, string name) => c.Kind switch
    {
        TriggerKind.Cast => $"Enemy casts \"{name}\"",
        TriggerKind.Status when c.IsSelf => $"You gain \"{name}\"",
        TriggerKind.Status => $"Party gains \"{name}\"",
        TriggerKind.HeadMarker when c.IsSelf => $"Marker \"{name}\" on you",
        TriggerKind.HeadMarker => $"Marker \"{name}\" on party",
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
        TriggerKind.HeadMarker => new SourceSpec
        {
            Kind = TriggerKind.HeadMarker,
            MarkerKey = c.MarkerKey,
            ActorScope = c.IsSelf ? BearerScope.Self : BearerScope.Party,
        },
        _ => new SourceSpec { Kind = c.Kind },
    };

    private static OutputSpec OutputsFor(Candidate c, string name)
    {
        var echoText = c.Kind switch
        {
            TriggerKind.Cast => "{caster} casts {action}!",
            TriggerKind.Status => "{status} on {bearer}!",
            TriggerKind.HeadMarker => "{marker}!",
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
                TriggerKind.HeadMarker => string.IsNullOrEmpty(rule.Source.MarkerKey)
                    || string.Equals(rule.Source.MarkerKey, c.MarkerKey, StringComparison.OrdinalIgnoreCase),
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
