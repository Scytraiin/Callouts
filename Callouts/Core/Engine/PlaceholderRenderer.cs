using System;
using System.Text.RegularExpressions;

namespace Callouts.Core.Engine;

/// <summary>
/// Substitutes placeholders in an output template using a <see cref="MatchResult"/>:
/// <c>{name}</c> named values (case-insensitive) and <c>$1</c>..<c>$9</c> regex captures.
/// Unknown placeholders render as an empty string (DESIGN.md §4.4). Pure and side-effect-free.
/// </summary>
public static class PlaceholderRenderer
{
    private static readonly Regex Token = new(
        @"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}|\$(?<idx>[1-9])",
        RegexOptions.Compiled);

    public static string Render(string template, MatchResult match)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template ?? string.Empty;
        }

        return Token.Replace(template, m =>
        {
            if (m.Groups["name"].Success)
            {
                return match.Values.TryGetValue(m.Groups["name"].Value, out var value)
                    ? value
                    : string.Empty;
            }

            var index = int.Parse(m.Groups["idx"].Value, System.Globalization.CultureInfo.InvariantCulture);
            return index >= 1 && index <= match.Captures.Count
                ? match.Captures[index - 1]
                : string.Empty;
        });
    }
}
