using System;
using System.Collections.Generic;

namespace Callouts.Core.Rules;

/// <summary>
/// OQ-2 decision: head markers are exposed as friendly named keys mapped to lock-on VFX path
/// substrings (Option A — reuse the VFX hook, no extra signature). This table is the single
/// maintained data file the option calls for; the substrings are best-effort and are a
/// documented patch-day maintenance item (verify against live <c>vfx/lockon/eff/*</c> paths).
/// </summary>
public static class MarkerMapping
{
    private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["spread"] = ["lockon/eff/target_ae", "lockon/eff/m0244"],
        ["stack"] = ["lockon/eff/share", "lockon/eff/m0005sp"],
        ["flare"] = ["lockon/eff/flare"],
        ["defamation"] = ["lockon/eff/defamation"],
        ["chains"] = ["lockon/eff/m0906"],
        ["prey"] = ["lockon/eff/prey"],
        ["enumeration"] = ["lockon/eff/enum"],
    };

    public static IReadOnlyCollection<string> Names => Map.Keys;

    /// <summary>Returns the named marker whose path substrings match, or null if unmapped.</summary>
    public static string? Lookup(string vfxPath)
    {
        if (string.IsNullOrEmpty(vfxPath))
        {
            return null;
        }

        foreach (var (name, substrings) in Map)
        {
            foreach (var substring in substrings)
            {
                if (vfxPath.Contains(substring, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>True when the path looks like a lock-on head marker at all (Option A gate).</summary>
    public static bool IsHeadMarkerPath(string vfxPath)
        => !string.IsNullOrEmpty(vfxPath) && vfxPath.Contains("lockon", StringComparison.OrdinalIgnoreCase);
}
