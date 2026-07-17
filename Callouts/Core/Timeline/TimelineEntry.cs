namespace Callouts.Core.Timeline;

/// <summary>
/// One scheduled line in a timeline: at <see cref="Time"/> seconds (relative to the sync clock),
/// the mechanic named by <see cref="Label"/> happens. If <see cref="SyncPattern"/> is set, observing
/// a matching event near this time re-anchors the clock (Cactbot-style drift correction). An optional
/// pre-alert fires <see cref="AlertSecondsBefore"/> seconds early through the normal output sinks.
/// Mutable POCO so it round-trips through the plugin configuration.
/// </summary>
public sealed class TimelineEntry
{
    /// <summary>Seconds from the sync clock's zero (combat start unless re-synced).</summary>
    public double Time { get; set; }

    public string Label { get; set; } = string.Empty;

    // --- Sync (re-anchor the clock when a matching event is observed near Time) ---

    /// <summary>Text/regex matched against an observed event; empty = this entry never syncs.</summary>
    public string SyncPattern { get; set; } = string.Empty;

    public bool SyncIsRegex { get; set; }

    /// <summary>How far before <see cref="Time"/> a sync match is still accepted.</summary>
    public double WindowBefore { get; set; } = 2.5;

    /// <summary>How far after <see cref="Time"/> a sync match is still accepted.</summary>
    public double WindowAfter { get; set; } = 2.5;

    // --- Optional pre-alert (routed through Echo/Toast/Sound sinks) ---

    /// <summary>Seconds before <see cref="Time"/> to fire the alert; 0 = no alert.</summary>
    public double AlertSecondsBefore { get; set; }

    public string AlertText { get; set; } = string.Empty;

    /// <summary>Sound effect 1..16, or 0 for none.</summary>
    public int AlertSound { get; set; }

    public TimelineEntry Clone() => new()
    {
        Time = this.Time,
        Label = this.Label,
        SyncPattern = this.SyncPattern,
        SyncIsRegex = this.SyncIsRegex,
        WindowBefore = this.WindowBefore,
        WindowAfter = this.WindowAfter,
        AlertSecondsBefore = this.AlertSecondsBefore,
        AlertText = this.AlertText,
        AlertSound = this.AlertSound,
    };

    public bool HasSync => !string.IsNullOrEmpty(this.SyncPattern);

    public bool HasAlert => this.AlertSecondsBefore > 0 && (!string.IsNullOrEmpty(this.AlertText) || this.AlertSound > 0);
}
