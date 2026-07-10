using System;
using System.Collections.Generic;

using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Callouts.Core.Engine;
using Callouts.Sinks;
using Callouts.Sources;
using Callouts.Windows;

namespace Callouts;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/callouts";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;

    private readonly WindowSystem windowSystem = new("Callouts");
    private readonly RulesWindow rulesWindow;
    private readonly Configuration configuration;

    private readonly RuleEngine engine;
    private readonly ChatSource chatSource;
    private readonly Dictionary<AlertOutputKind, IAlertSink> sinks;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;

        this.configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(this.pluginInterface);

        // Core engine reads rules live from saved config.
        this.engine = new RuleEngine(() => this.configuration.Rules);

        // Output sinks, keyed by the action kind they execute.
        this.sinks = new Dictionary<AlertOutputKind, IAlertSink>
        {
            [AlertOutputKind.Echo] = new EchoSink(chatGui, this.log),
        };

        // Trigger sources feed normalized events into the engine.
        this.chatSource = new ChatSource(chatGui, this.log);
        this.chatSource.OnEvent += this.HandleTriggerEvent;
        this.chatSource.Start();

        this.rulesWindow = new RulesWindow(this.configuration, this.configuration.Save);
        this.windowSystem.AddWindow(this.rulesWindow);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Toggle the Callouts rules window.",
        });

        this.pluginInterface.UiBuilder.Draw += this.DrawUi;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenMainUi;

        this.log.Information("Callouts initialized.");
    }

    public void Dispose()
    {
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        this.pluginInterface.UiBuilder.Draw -= this.DrawUi;

        this.commandManager.RemoveHandler(CommandName);

        this.chatSource.OnEvent -= this.HandleTriggerEvent;
        this.chatSource.Dispose();

        this.windowSystem.RemoveAllWindows();
        this.rulesWindow.Dispose();
    }

    private void HandleTriggerEvent(TriggerEvent evt)
    {
        IReadOnlyList<AlertAction> actions;
        try
        {
            actions = this.engine.Process(evt);
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: rule engine failed while processing an event.");
            return;
        }

        foreach (var action in actions)
        {
            if (this.sinks.TryGetValue(action.Kind, out var sink))
            {
                sink.Execute(action);
            }
        }
    }

    private void OnCommand(string command, string args)
    {
        this.rulesWindow.IsOpen = !this.rulesWindow.IsOpen;
    }

    private void DrawUi()
    {
        try
        {
            this.windowSystem.Draw();
        }
        catch (Exception ex)
        {
            this.rulesWindow.IsOpen = false;
            this.log.Error(ex, "Unhandled UI exception in Callouts. Closed plugin windows to protect the host.");
        }
    }

    private void OpenMainUi()
    {
        this.rulesWindow.IsOpen = true;
    }
}
