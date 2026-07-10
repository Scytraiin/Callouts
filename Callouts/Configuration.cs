using System;
using System.Collections.Generic;

using Dalamud.Configuration;
using Dalamud.Plugin;

using Callouts.Core.Rules;

namespace Callouts;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = 1;

    public List<Rule> Rules { get; set; } = [];

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.pluginInterface?.SavePluginConfig(this);
    }
}
