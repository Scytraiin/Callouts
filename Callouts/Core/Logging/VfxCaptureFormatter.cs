using System;
using System.Globalization;

using Callouts.Core.Engine;

namespace Callouts.Core.Logging;

/// <summary>
/// Pure, side-effect-free formatting for the VFX capture file: one tab-separated line per observed
/// VFX spawn, plus the session banner and column header. Kept Dalamud-free so it is unit-testable;
/// the actual file writing lives in the plugin adapter (<c>VfxCaptureLog</c>).
/// </summary>
public static class VfxCaptureFormatter
{
    /// <summary>Tab-separated column header, written once when a fresh file is created.</summary>
    public const string Header = "time\tterritoryId\tzone\ttarget\tcombat\tpath";

    /// <summary>A comment line written whenever capture is (re)started, delimiting sessions.</summary>
    public static string SessionBanner(DateTime startedLocal)
        => "# Callouts VFX capture — session started "
           + startedLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Formats a single VFX event as one tab-separated line (no trailing newline).</summary>
    public static string FormatLine(TriggerEvent evt, DateTime timestampLocal)
    {
        var time = timestampLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var target = evt.TargetIsSelf ? "self" : evt.TargetInParty ? "party" : "other";
        var combat = evt.InCombat ? "combat" : "-";

        return string.Join(
            '\t',
            time,
            evt.TerritoryId.ToString(CultureInfo.InvariantCulture),
            Clean(evt.Zone),
            target,
            combat,
            Clean(evt.VfxPath));
    }

    // Keep every event on exactly one line: strip any tab/newline that would corrupt the columns.
    private static string Clean(string value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
