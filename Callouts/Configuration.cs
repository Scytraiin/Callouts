using System;
using System.Collections.Generic;

using Dalamud.Configuration;
using Dalamud.Plugin;

using Callouts.Core.Config;
using Callouts.Core.Rules;

namespace Callouts;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    /// <summary>The config schema version this build of the plugin writes.</summary>
    public const int CurrentVersion = 1;

    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = CurrentVersion;

    public List<Rule> Rules { get; set; } = [];

    public GlobalOptions Options { get; set; } = new();

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
