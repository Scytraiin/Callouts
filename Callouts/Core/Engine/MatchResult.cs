using System.Collections.Generic;

namespace Callouts.Core.Engine;

/// <summary>
/// The outcome of a successful match. Carries the values a <see cref="PlaceholderRenderer"/>
/// substitutes into output text: named placeholders ({sender}, {caster}, …) in
/// <see cref="Values"/> and positional regex groups ($1..$9) in <see cref="Captures"/>.
/// </summary>
public sealed record MatchResult
{
    public IReadOnlyDictionary<string, string> Values { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Regex capture groups. Index 0 = $1. Empty for Contains matches.</summary>
    public IReadOnlyList<string> Captures { get; init; } = [];

    public static MatchResult FromValues(params (string Key, string Value)[] values)
    {
        var dict = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
        {
            dict[key] = value;
        }

        return new MatchResult { Values = dict };
    }
}
