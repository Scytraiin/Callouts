using System;

using Callouts.Core.Rules;

namespace Callouts.Core.Suggestions;

/// <summary>Category buckets shown as sections in the Suggestions window.</summary>
public static class SuggestionCategory
{
    public const string EnemyCasts = "Enemy casts";
    public const string DebuffsOnYou = "Debuffs on you";
    public const string PartyEffects = "Party effects";
    public const string Markers = "Markers / VFX";
}

/// <summary>Which aggregate window to draw suggestions from (issue 020).</summary>
public enum EncounterScope
{
    ThisFight,
    ThisSession,
}

/// <summary>
/// A ranked, ready-to-adopt trigger proposal derived from observed combat data. Immutable; the UI
/// turns <see cref="ToRule"/> into an editor draft (Create rule) or an export code (Copy import code).
/// </summary>
public sealed record Suggestion
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string SuggestedName { get; init; }

    public string Rationale { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public int Score { get; init; }

    public int Count { get; init; }

    public bool Covered { get; init; }

    public bool Advanced { get; init; }

    public required SourceSpec ProposedSource { get; init; }

    public required OutputSpec ProposedOutputs { get; init; }

    public int Stars => Math.Clamp((int)Math.Round(this.Score / 20.0, MidpointRounding.AwayFromZero), 1, 5);

    /// <summary>Builds a concrete rule (fresh id) for the editor or an export code.</summary>
    public Rule ToRule() => new()
    {
        Name = this.SuggestedName,
        Source = this.ProposedSource.Clone(),
        Outputs = this.ProposedOutputs.Clone(),
    };
}
