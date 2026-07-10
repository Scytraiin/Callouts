using System;

using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Callouts.Windows;

namespace Callouts;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/callouts";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly WindowSystem windowSystem = new("Callouts");
    private readonly MainWindow mainWindow;
    private readonly Configuration configuration;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;

        this.configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.configuration.Initialize(this.pluginInterface);

        this.mainWindow = new MainWindow();
        this.windowSystem.AddWindow(this.mainWindow);

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

        this.windowSystem.RemoveAllWindows();
        this.mainWindow.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        this.mainWindow.IsOpen = !this.mainWindow.IsOpen;
    }

    private void DrawUi()
    {
        try
        {
            this.windowSystem.Draw();
        }
        catch (Exception ex)
        {
            this.mainWindow.IsOpen = false;
            this.log.Error(ex, "Unhandled UI exception in Callouts. Closed plugin windows to protect the host.");
        }
    }

    private void OpenMainUi()
    {
        this.mainWindow.IsOpen = true;
    }
}
