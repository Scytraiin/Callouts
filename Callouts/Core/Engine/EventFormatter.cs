using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>Pure one-line descriptions of events for the Live events window.</summary>
public static class EventFormatter
{
    public static string Describe(TriggerEvent evt) => evt.Kind switch
    {
        TriggerKind.Chat => $"[{ChatChannelName(evt.Channel)}] {evt.Sender}: {evt.Message}",
        TriggerKind.Cast => $"{Or(evt.CasterName, "Someone")} casts \"{Or(evt.ActionName, "?")}\" (id {evt.ActionId})",
        TriggerKind.Status => evt.StatusGained
            ? $"\"{Or(evt.StatusName, "?")}\" gained by {Or(evt.BearerName, "?")} (id {evt.StatusId})"
            : $"\"{Or(evt.StatusName, "?")}\" removed from {Or(evt.BearerName, "?")} (id {evt.StatusId})",
        TriggerKind.DutyEvent => $"Duty {evt.DutyEvent}",
        TriggerKind.Vfx => $"VFX {evt.VfxPath}",
        TriggerKind.HeadMarker => $"Marker \"{Or(evt.MarkerKey, evt.RawValue)}\"",
        _ => evt.Kind.ToString(),
    };

    private static string Or(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string ChatChannelName(int channel) => channel.ToString();
}
