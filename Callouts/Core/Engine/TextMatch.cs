namespace Callouts.Core.Engine;

// Dalamud-free core (compile-linked into Callouts.Tests). "Contains" matching
// used by chat rules; regex mode arrives with the rule engine.
public static class TextMatch
{
    public static bool Contains(string text, string pattern, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return text.Contains(pattern, comparison);
    }
}
