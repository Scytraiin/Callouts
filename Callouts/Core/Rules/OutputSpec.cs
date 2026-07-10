namespace Callouts.Core.Rules;

/// <summary>The Echo (local chat line) output. Sound and Toast outputs arrive in issue 006.</summary>
public sealed class EchoOutput
{
    public bool Enabled { get; set; } = true;

    public string Text { get; set; } = string.Empty;

    public EchoOutput Clone() => new() { Enabled = this.Enabled, Text = this.Text };
}

/// <summary>Persisted per-rule output configuration.</summary>
public sealed class OutputSpec
{
    public EchoOutput Echo { get; set; } = new();

    // Sound / Toast -> issue 006

    /// <summary>True when at least one output is enabled. Used by validation and dispatch.</summary>
    public bool AnyEnabled => this.Echo.Enabled;

    public OutputSpec Clone() => new() { Echo = this.Echo.Clone() };
}
