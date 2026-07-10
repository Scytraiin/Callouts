using System;

using Dalamud.Plugin.Services;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

namespace Callouts.Sinks;

/// <summary>
/// Shows an on-screen toast via <see cref="IToastGui"/>. Style maps to the three native toast
/// presentations (Normal / Quest / Error). Local UI only; nothing is transmitted.
/// </summary>
public sealed class ToastSink : IAlertSink
{
    private readonly IToastGui toastGui;
    private readonly IPluginLog log;

    public ToastSink(IToastGui toastGui, IPluginLog log)
    {
        this.toastGui = toastGui;
        this.log = log;
    }

    public AlertOutputKind Kind => AlertOutputKind.Toast;

    public void Execute(AlertAction action)
    {
        try
        {
            switch (action.ToastStyle)
            {
                case ToastStyle.Quest:
                    this.toastGui.ShowQuest(action.Text);
                    break;

                case ToastStyle.Error:
                    this.toastGui.ShowError(action.Text);
                    break;

                default:
                    this.toastGui.ShowNormal(action.Text);
                    break;
            }
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to show toast.");
        }
    }
}
