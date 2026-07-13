using System;
using System.Collections.Generic;
using System.IO;

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using Lumina.Excel.Sheets;

using Callouts.Core.Config;
using Callouts.Core.Engine;
using Callouts.Core.Rules;
using Callouts.Core.Suggestions;
using Callouts.Sinks;
using Callouts.Sources;
using Callouts.Windows;

namespace Callouts;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/callouts";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IClientState clientState;
    private readonly IDataManager dataManager;
    private readonly ICondition condition;
    private readonly IPluginLog log;

    private readonly WindowSystem windowSystem = new("Callouts");
    private readonly RulesWindow rulesWindow;
    private readonly LiveEventsWindow eventsWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly SuggestionsWindow suggestionsWindow;
    private readonly Configuration configuration;

    private readonly RuleEngine engine;
    private readonly EventBuffer eventBuffer;
    private readonly SuggestionCollector suggestionCollector = new();
    private readonly List<ITriggerSource> sources = [];
    private readonly VfxSource vfxSource;
    private readonly Dictionary<AlertOutputKind, IAlertSink> sinks;

    private string currentZone = string.Empty;
    private bool advancedFailureNotified;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chatGui,
        IToastGui toastGui,
        IObjectTable objectTable,
        IFramework framework,
        IPartyList partyList,
        ITargetManager targetManager,
        IDataManager dataManager,
        IClientState clientState,
        ICondition condition,
        IDutyState dutyState,
        IGameInteropProvider interop,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.clientState = clientState;
        this.dataManager = dataManager;
        this.condition = condition;
        this.log = log;

        // Migrate/back up the on-disk config before loading it (FR-9).
        this.configuration = this.LoadConfiguration(out var configNotice);

        var rate = this.configuration.Options.RateLimitPerSecond;
        this.engine = new RuleEngine(() => this.configuration.Rules, null, new RateLimiter(rate, rate))
        {
            MasterEnabled = this.configuration.Options.MasterEnabled,
        };

        this.sinks = new Dictionary<AlertOutputKind, IAlertSink>
        {
            [AlertOutputKind.Echo] = new EchoSink(chatGui, this.log),
            [AlertOutputKind.Sound] = new SoundSink(this.log),
            [AlertOutputKind.Toast] = new ToastSink(toastGui, this.log),
        };

        // Stable trigger sources — always on.
        this.sources.Add(new ChatSource(chatGui, this.log));
        this.sources.Add(new CastSource(objectTable, framework, partyList, dataManager, clientState, this.log));
        this.sources.Add(new StatusSource(objectTable, framework, partyList, targetManager, dataManager, clientState, this.log));
        this.sources.Add(new DutyEventSource(dutyState, this.log));

        foreach (var source in this.sources)
        {
            source.OnEvent += this.HandleTriggerEvent;
            source.Start();
        }

        // Advanced (hook-based) source — started only while the master toggle is on (FR-3).
        this.vfxSource = new VfxSource(interop, objectTable, partyList, this.log);
        this.vfxSource.OnEvent += this.HandleTriggerEvent;
        this.engine.AvailabilityProvider = this.GetAvailability;
        if (this.configuration.Options.AdvancedSourcesEnabled)
        {
            this.StartAdvancedSources();
        }

        this.currentZone = this.ResolveZoneName(this.clientState.TerritoryType);
        this.clientState.TerritoryChanged += this.OnTerritoryChanged;
        this.condition.ConditionChange += this.OnConditionChange;

        this.eventBuffer = new EventBuffer(this.configuration.Options.EventLogDefaultLimit, this.configuration.Options.EventCategoryLimits);

        this.eventsWindow = new LiveEventsWindow(this.eventBuffer, this.engine, this.CreateRuleFromEvent);
        this.settingsWindow = new SettingsWindow(this.configuration, this.engine, this.eventBuffer, this.configuration.Save, this.OnAdvancedToggled);
        this.settingsWindow.SetAdvancedHealthProvider(this.DescribeAdvancedHealth);
        this.suggestionsWindow = new SuggestionsWindow(this.suggestionCollector, this.configuration, this.CreateRuleFromSuggestion, this.configuration.Save);
        this.rulesWindow = new RulesWindow(
            this.configuration,
            this.engine,
            this.configuration.Save,
            this.ExecuteAction,
            () => this.eventsWindow.IsOpen = true,
            () => this.settingsWindow.IsOpen = true,
            () => this.suggestionsWindow.IsOpen = true,
            () => (this.clientState.TerritoryType, this.currentZone));

        this.windowSystem.AddWindow(this.rulesWindow);
        this.windowSystem.AddWindow(this.eventsWindow);
        this.windowSystem.AddWindow(this.settingsWindow);
        this.windowSystem.AddWindow(this.suggestionsWindow);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Callouts. Subcommands: on, off, config, events.",
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

    public void Dispose()
    {
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenMainUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenMainUi;
        this.pluginInterface.UiBuilder.Draw -= this.DrawUi;

        this.commandManager.RemoveHandler(CommandName);

        this.clientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.condition.ConditionChange -= this.OnConditionChange;

        foreach (var source in this.sources)
        {
            source.OnEvent -= this.HandleTriggerEvent;
            source.Dispose();
        }

        this.vfxSource.OnEvent -= this.HandleTriggerEvent;
        this.vfxSource.Dispose();

        this.windowSystem.RemoveAllWindows();
        this.rulesWindow.Dispose();
        this.eventsWindow.Dispose();
        this.settingsWindow.Dispose();
        this.suggestionsWindow.Dispose();
    }

    private void CreateRuleFromEvent(TriggerEvent evt) => this.rulesWindow.BeginCreateFromEvent(evt);

    private void CreateRuleFromSuggestion(Suggestion suggestion) => this.rulesWindow.BeginCreateFromSuggestion(suggestion);

    private void OnAdvancedToggled(bool enabled)
    {
        if (enabled)
        {
            this.StartAdvancedSources();
        }
        else
        {
            this.vfxSource.Stop();
        }

        this.log.Information("Callouts: advanced sources {State}.", enabled ? "enabled" : "disabled");
    }

    private void StartAdvancedSources()
    {
        this.vfxSource.Start();
        this.NotifyIfAdvancedFailed();
    }

    private void NotifyIfAdvancedFailed()
    {
        if (this.vfxSource.Status != SourceStatus.Failed || this.advancedFailureNotified)
        {
            return;
        }

        this.advancedFailureNotified = true;

        var affected = 0;
        foreach (var rule in this.configuration.Rules)
        {
            if (rule.Enabled && rule.Source.Kind is TriggerKind.Vfx or TriggerKind.HeadMarker)
            {
                affected++;
            }
        }

        var message = $"Callouts: advanced sources failed to start ({this.vfxSource.FailureReason}). {affected} rule(s) inactive — /callouts config.";
        this.log.Warning(message);
        this.ExecuteAction(new AlertAction { Kind = AlertOutputKind.Echo, Text = message });
        this.ExecuteAction(new AlertAction { Kind = AlertOutputKind.Toast, Text = message, ToastStyle = ToastStyle.Error });
    }

    private SourceAvailability GetAvailability(TriggerKind kind)
    {
        if (kind is not (TriggerKind.Vfx or TriggerKind.HeadMarker))
        {
            return SourceAvailability.Available;
        }

        if (!this.configuration.Options.AdvancedSourcesEnabled)
        {
            return SourceAvailability.BlockedAdvancedOff;
        }

        return this.vfxSource.Status == SourceStatus.Failed
            ? SourceAvailability.Failed
            : SourceAvailability.Available;
    }

    private string DescribeAdvancedHealth()
    {
        if (!this.configuration.Options.AdvancedSourcesEnabled)
        {
            return "VFX / head markers: off";
        }

        return this.vfxSource.Status == SourceStatus.Failed
            ? $"VFX / head markers: FAILED — {this.vfxSource.FailureReason}"
            : $"VFX / head markers: {this.vfxSource.Status}";
    }

    private void SetMasterEnabled(bool enabled)
    {
        this.engine.MasterEnabled = enabled;
        this.configuration.Options.MasterEnabled = enabled;
        this.configuration.Save();
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

        var config = plan.RefuseAsDowngrade
            ? new Configuration()
            : this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        config.Initialize(this.pluginInterface);
        config.Version = Configuration.CurrentVersion;
        notice = plan.Notice;
        return config;
    }

    private void HandleTriggerEvent(TriggerEvent evt)
    {
        var enriched = evt with
        {
            Zone = this.currentZone,
            TerritoryId = this.clientState.TerritoryType,
            InCombat = this.condition[ConditionFlag.InCombat],
            InDuty = this.condition[ConditionFlag.BoundByDuty],
        };

        this.eventBuffer.Add(new EventRecord
        {
            Kind = enriched.Kind,
            Display = EventFormatter.Describe(enriched),
            Time = DateTime.Now.ToString("HH:mm:ss"),
            Event = enriched,
        });

        this.suggestionCollector.Observe(enriched);

        IReadOnlyList<AlertAction> actions;
        try
        {
            actions = this.engine.Process(enriched);
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

    private void OnTerritoryChanged(uint territoryType)
    {
        this.currentZone = this.ResolveZoneName(territoryType);
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.InCombat)
        {
            return;
        }

        if (value)
        {
            // Entering combat starts a fresh encounter; the finished one stays under "This session".
            this.suggestionCollector.RollEncounter();
        }
        else if (this.configuration.Options.AutoOpenSuggestions)
        {
            this.suggestionsWindow.IsOpen = true;
        }
    }

    private string ResolveZoneName(uint territoryType)
    {
        if (territoryType == 0)
        {
            return string.Empty;
        }

        if (!this.dataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryType, out var territory))
        {
            return string.Empty;
        }

        var name = territory.PlaceName.ValueNullable?.Name.ExtractText();
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                this.SetMasterEnabled(true);
                break;
            case "off":
                this.SetMasterEnabled(false);
                break;
            case "config":
            case "settings":
                this.settingsWindow.IsOpen = true;
                break;
            case "events":
            case "debug":
                this.eventsWindow.IsOpen = true;
                break;
            case "suggestions":
            case "suggest":
                this.suggestionsWindow.IsOpen = true;
                break;
            default:
                this.rulesWindow.IsOpen = !this.rulesWindow.IsOpen;
                break;
        }
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
