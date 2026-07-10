using System.Collections.Generic;

using Callouts.Core.Engine;

namespace Callouts.Core.Rules;

/// <summary>
/// A small curated set of example rules for first-run / one-click import (issue 016, OQ-3). Ids
/// are fixed so re-importing an updated pack matches existing copies instead of duplicating.
/// Built in code and merged through the same import path users see.
/// </summary>
public static class StarterPack
{
    public static List<Rule> Create() =>
    [
        new Rule
        {
            Id = "starter-ready-check",
            Name = "Ready check",
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "has initiated a ready check" },
            Outputs = new OutputSpec
            {
                Echo = new EchoOutput { Enabled = true, Text = "Ready check!" },
                Sound = new SoundOutput { Enabled = true, EffectId = 6 },
            },
        },
        new Rule
        {
            Id = "starter-countdown",
            Name = "Countdown started",
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "Battle commencing in" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "Countdown!" } },
        },
        new Rule
        {
            Id = "starter-food-expiry",
            Name = "Food expired",
            Source = new SourceSpec
            {
                Kind = TriggerKind.Status,
                StatusNameContains = "Well Fed",
                StatusChange = StatusChangeFilter.Removed,
                Bearer = BearerScope.Self,
            },
            Outputs = new OutputSpec { Toast = new ToastOutput { Enabled = true, Text = "Food expired!", Style = ToastStyle.Error }, Echo = new EchoOutput { Enabled = false } },
        },
        new Rule
        {
            Id = "starter-wipe",
            Name = "Wipe",
            Source = new SourceSpec { Kind = TriggerKind.DutyEvent, DutyEvent = DutyEventFilter.Wiped },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "Wipe. Regroup." } },
        },
    ];
}
