using System;

using Callouts.Core.Rules;

namespace Callouts.Core.Suggestions;

/// <summary>
/// Ranks candidates 0..100. Issue 018 uses recurrence + targeted-you; issue 019 folds in danger
/// signals (cast time, AoE, debuff, duration). Pure and unit-tested.
/// </summary>
public static class SuggestionScorer
{
    public static int Score(Candidate c)
    {
        // Recurrence: up to 60 points (saturates at 20 sightings).
        var score = Math.Min(c.Count, 20) * 3;

        // Targeted-you ratio: up to 40 points.
        if (c.Count > 0)
        {
            score += (int)Math.Round(40.0 * c.TargetedMeCount / c.Count, MidpointRounding.AwayFromZero);
        }

        // Danger signals (issue 019): long casts, AoE mechanics, and debuffs matter more.
        if (c.MaxCastTimeSeconds >= 3.0)
        {
            score += 12;
        }

        if (c.AoeShape is not (AoeShape.None or AoeShape.Single))
        {
            score += 10;
        }

        if (c.IsDebuff)
        {
            score += 10;
        }

        return Math.Clamp(score, 0, 100);
    }
}
