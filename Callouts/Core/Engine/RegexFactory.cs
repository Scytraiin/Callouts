using System;
using System.Text.RegularExpressions;

namespace Callouts.Core.Engine;

/// <summary>
/// Compiles user regex patterns with a hard <see cref="MatchTimeout"/> so a pathological
/// pattern can never hang the game thread (DESIGN.md §4.2). Compilation failures are returned
/// as messages, not thrown — the engine turns them into a per-rule RuleError.
/// </summary>
public static class RegexFactory
{
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

    public static bool TryCompile(string pattern, bool caseSensitive, out Regex? regex, out string? error)
    {
        regex = null;
        error = null;

        if (string.IsNullOrEmpty(pattern))
        {
            error = "Pattern is empty.";
            return false;
        }

        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (!caseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        try
        {
            regex = new Regex(pattern, options, MatchTimeout);
            return true;
        }
        catch (ArgumentException ex)
        {
            error = $"Invalid regex: {ex.Message}";
            return false;
        }
    }
}
