using System.Collections.Generic;

using Dalamud.Game.Text;

namespace Callouts.Windows;

/// <summary>
/// A curated, human-readable list of chat channels for the rule editor's channel picker, so
/// users never type a raw XivChatType number. The stored value is the numeric channel
/// (SourceSpec.Channels); this only affects presentation. Echo is intentionally excluded —
/// it is never matchable (anti-loop rule).
/// </summary>
public static class ChatChannelCatalog
{
    public readonly record struct Entry(int Value, string Label);

    public static IReadOnlyList<Entry> Channels { get; } =
    [
        new((int)XivChatType.Say, "Say"),
        new((int)XivChatType.Shout, "Shout"),
        new((int)XivChatType.Yell, "Yell"),
        new((int)XivChatType.TellIncoming, "Tell (incoming)"),
        new((int)XivChatType.TellOutgoing, "Tell (outgoing)"),
        new((int)XivChatType.Party, "Party"),
        new((int)XivChatType.CrossParty, "Cross-world party"),
        new((int)XivChatType.Alliance, "Alliance"),
        new((int)XivChatType.FreeCompany, "Free Company"),
        new((int)XivChatType.NoviceNetwork, "Novice Network"),
        new((int)XivChatType.PvPTeam, "PvP Team"),
        new((int)XivChatType.Ls1, "Linkshell 1"),
        new((int)XivChatType.Ls2, "Linkshell 2"),
        new((int)XivChatType.Ls3, "Linkshell 3"),
        new((int)XivChatType.Ls4, "Linkshell 4"),
        new((int)XivChatType.Ls5, "Linkshell 5"),
        new((int)XivChatType.Ls6, "Linkshell 6"),
        new((int)XivChatType.Ls7, "Linkshell 7"),
        new((int)XivChatType.Ls8, "Linkshell 8"),
        new((int)XivChatType.CrossLinkShell1, "Cross-world Linkshell 1"),
        new((int)XivChatType.StandardEmote, "Emote"),
        new((int)XivChatType.CustomEmote, "Custom Emote"),
        new((int)XivChatType.NPCDialogue, "NPC Dialogue"),
        new((int)XivChatType.SystemMessage, "System Message"),
        new((int)XivChatType.Notice, "Notice"),
    ];

    public static string LabelFor(int value)
    {
        foreach (var entry in Channels)
        {
            if (entry.Value == value)
            {
                return entry.Label;
            }
        }

        return $"Channel {value}";
    }
}
