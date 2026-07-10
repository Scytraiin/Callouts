using System;

using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

using Callouts.Core.Engine;

namespace Callouts.Sinks;

/// <summary>
/// Prints an alert to the local Echo channel via <see cref="IChatGui.Print(XivChatEntry)"/> —
/// the same channel and visibility as typing <c>/echo</c>. Local-only by game design; nothing
/// is transmitted. Because output goes to Echo, it can never re-trigger a rule (the ChatSource
/// drops that channel).
/// </summary>
public sealed class EchoSink : IAlertSink
{
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;

    public EchoSink(IChatGui chatGui, IPluginLog log)
    {
        this.chatGui = chatGui;
        this.log = log;
    }

    public AlertOutputKind Kind => AlertOutputKind.Echo;

    public void Execute(AlertAction action)
    {
        try
        {
            this.chatGui.Print(new XivChatEntry
            {
                Type = XivChatType.Echo,
                Message = new SeString(new TextPayload(action.Text)),
            });
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to print echo output.");
        }
    }
}
