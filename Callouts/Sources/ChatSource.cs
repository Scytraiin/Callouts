using System;

using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;

using Callouts.Core.Engine;
using Callouts.Util;

namespace Callouts.Sources;

/// <summary>
/// Observes the game's chat via <see cref="IChatGui.ChatMessage"/> and emits normalized chat
/// <see cref="TriggerEvent"/>s. Read-only: it never suppresses or alters a message (it does
/// not call <c>PreventOriginal</c>). The Echo channel is dropped before emitting — the
/// non-configurable anti-loop rule (DESIGN.md §3.1) that makes the plugin's own Echo output
/// impossible to re-trigger.
/// </summary>
public sealed class ChatSource : ITriggerSource
{
    private readonly IChatGui chatGui;
    private readonly IPluginLog log;
    private bool started;

    public ChatSource(IChatGui chatGui, IPluginLog log)
    {
        this.chatGui = chatGui;
        this.log = log;
    }

    public TriggerKind Kind => TriggerKind.Chat;

    public SourceStatus Status { get; private set; } = SourceStatus.Disabled;

    public event Action<TriggerEvent>? OnEvent;

    public void Start()
    {
        if (this.started)
        {
            return;
        }

        this.chatGui.ChatMessage += this.OnChatMessage;
        this.started = true;
        this.Status = SourceStatus.Active;
    }

    public void Stop()
    {
        if (!this.started)
        {
            return;
        }

        this.chatGui.ChatMessage -= this.OnChatMessage;
        this.started = false;
        this.Status = SourceStatus.Disabled;
    }

    public void Dispose() => this.Stop();

    private void OnChatMessage(IHandleableChatMessage message)
    {
        try
        {
            // Anti-loop hard rule: the Echo channel is never observed (our own output lands there).
            if (message.LogKind == XivChatType.Echo)
            {
                return;
            }

            var evt = new TriggerEvent
            {
                Kind = TriggerKind.Chat,
                Channel = (int)message.LogKind,
                Sender = SeStringText.Flatten(message.Sender),
                Message = SeStringText.Flatten(message.Message),
            };

            this.OnEvent?.Invoke(evt);
        }
        catch (Exception ex)
        {
            // A source must never throw back into the game's chat pipeline.
            this.log.Error(ex, "Callouts: failed to process a chat message.");
        }
    }
}
