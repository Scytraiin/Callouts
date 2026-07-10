using System;

using Dalamud.Plugin.Services;

using FFXIVClientStructs.FFXIV.Client.UI;

using Callouts.Core.Engine;

namespace Callouts.Sinks;

/// <summary>
/// Plays one of the 16 built-in chat sound effects via UIGlobals.PlayChatSoundEffect. The ids
/// are 1-based (1..16); this is the same call Dalamud uses to preview the &lt;se.N&gt; macro.
/// Purely a local client sound; nothing is transmitted.
/// </summary>
public sealed class SoundSink : IAlertSink
{
    private readonly IPluginLog log;

    public SoundSink(IPluginLog log)
    {
        this.log = log;
    }

    public AlertOutputKind Kind => AlertOutputKind.Sound;

    public void Execute(AlertAction action)
    {
        try
        {
            UIGlobals.PlayChatSoundEffect((uint)action.SoundEffectId);
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to play sound effect {Id}.", action.SoundEffectId);
        }
    }
}
