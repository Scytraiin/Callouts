namespace Callouts.Core.Engine;

/// <summary>
/// Fine-grained classification of an observed event, used by the Live events window filters
/// (enemy casts, self/party debuffs, etc.). Pure/derived — not persisted.
/// </summary>
public enum EventCategory
{
    Chat,
    EnemyCast,
    OtherCast,
    SelfDebuff,
    SelfBuff,
    PartyDebuff,
    PartyBuff,
    OtherStatus,
    Duty,
    Vfx,
    HeadMarker,
}

/// <summary>Pure mapping from a <see cref="TriggerEvent"/> to its <see cref="EventCategory"/>.</summary>
public static class EventCategorizer
{
    public static EventCategory Categorize(TriggerEvent e) => e.Kind switch
    {
        TriggerKind.Chat => EventCategory.Chat,
        TriggerKind.Cast => e.CasterIsEnemy ? EventCategory.EnemyCast : EventCategory.OtherCast,
        TriggerKind.Status => e.BearerIsSelf
            ? (e.IsDebuff ? EventCategory.SelfDebuff : EventCategory.SelfBuff)
            : e.BearerInParty
                ? (e.IsDebuff ? EventCategory.PartyDebuff : EventCategory.PartyBuff)
                : EventCategory.OtherStatus,
        TriggerKind.DutyEvent => EventCategory.Duty,
        TriggerKind.Vfx => EventCategory.Vfx,
        TriggerKind.HeadMarker => EventCategory.HeadMarker,
        _ => EventCategory.OtherStatus,
    };

    public static string Label(EventCategory category) => category switch
    {
        EventCategory.Chat => "Chat",
        EventCategory.EnemyCast => "Enemy casts",
        EventCategory.OtherCast => "Other casts",
        EventCategory.SelfDebuff => "Self debuffs",
        EventCategory.SelfBuff => "Self buffs",
        EventCategory.PartyDebuff => "Party debuffs",
        EventCategory.PartyBuff => "Party buffs",
        EventCategory.OtherStatus => "Other status",
        EventCategory.Duty => "Duty",
        EventCategory.Vfx => "VFX",
        EventCategory.HeadMarker => "Markers",
        _ => category.ToString(),
    };
}
