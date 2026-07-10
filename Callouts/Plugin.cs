using System;
using System.Collections.Generic;
using System.IO;

using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Callouts.Core.Config;
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
        IToastGui toastGui,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.log = log;

        // Migrate/back up the on-disk config before loading it (FR-9).
        this.configuration = this.LoadConfiguration(out var configNotice);

        // Core engine reads rules live from saved config; options seed the gates + master switch.
        var rate = this.configuration.Options.RateLimitPerSecond;
        this.engine = new RuleEngine(() => this.configuration.Rules, null, new RateLimiter(rate, rate))
        {
            MasterEnabled = this.configuration.Options.MasterEnabled,
        };

        // Output sinks, keyed by the action kind they execute.
        this.sinks = new Dictionary<AlertOutputKind, IAlertSink>
        {
            [AlertOutputKind.Echo] = new EchoSink(chatGui, this.log),
            [AlertOutputKind.Sound] = new SoundSink(this.log),
            [AlertOutputKind.Toast] = new ToastSink(toastGui, this.log),
        };

        // Trigger sources feed normalized events into the engine.
        this.chatSource = new ChatSource(chatGui, this.log);
        this.chatSource.OnEvent += this.HandleTriggerEvent;
        this.chatSource.Start();

        this.rulesWindow = new RulesWindow(this.configuration, this.engine, this.configuration.Save, this.ExecuteAction);
        this.windowSystem.AddWindow(this.rulesWindow);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Toggle the Callouts rules window.",
        });

        this.pluginInterface.UiBuilder.Draw += this.DrawUi;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenMainUi;

        if (!string.IsNullOrEmpty(configNotice))
        {
            this.log.Information(configNotice);
            this.ExecuteAction(new AlertAction { Kind = AlertOutputKind.Echo, Text = configNotice });
        }

        this.log.Information("Callouts initialized.");
    }

    private Configuration LoadConfiguration(out string? notice)
    {
        notice = null;

        string? raw = null;
        try
        {
            var file = this.pluginInterface.ConfigFile;
            if (file.Exists)
            {
                raw = File.ReadAllText(file.FullName);
            }
        }
        catch (Exception ex)
        {
            this.log.Error(ex, "Callouts: failed to read config file for migration.");
        }

        var plan = ConfigMigrator.Plan(raw, Configuration.CurrentVersion);

        // Unconditional pre-migration backup whenever the stored version differs.
        if (plan.NeedsBackup && raw is not null && plan.BackupFileName is not null)
        {
            try
            {
                var dir = this.pluginInterface.ConfigFile.Directory;
                if (dir is not null)
                {
                    File.WriteAllText(Path.Combine(dir.FullName, plan.BackupFileName), raw);
                }
            }
            catch (Exception ex)
            {
                this.log.Error(ex, "Callouts: failed to write config backup.");
            }
        }

        // Downgrade (stored newer than code) → refuse: load defaults, keep the backup.
        var config = plan.RefuseAsDowngrade
            ? new Configuration()
            : this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        config.Initialize(this.pluginInterface);
        config.Version = Configuration.CurrentVersion;
        notice = plan.Notice;
        return config;
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
            this.ExecuteAction(action);
        }
    }

    private void ExecuteAction(AlertAction action)
    {
        if (this.sinks.TryGetValue(action.Kind, out var sink))
        {
            sink.Execute(action);
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
