namespace Callouts.Core.Rules;

/// <summary>The Echo (local chat line) output.</summary>
public sealed class EchoOutput
{
    public bool Enabled { get; set; } = true;

    public string Text { get; set; } = string.Empty;

    public EchoOutput Clone() => new() { Enabled = this.Enabled, Text = this.Text };
}

/// <summary>A game sound effect (1..16, 1-based) played via UIGlobals.PlayChatSoundEffect.</summary>
public sealed class SoundOutput
{
    public const int MinEffectId = 1;
    public const int MaxEffectId = 16;

    public bool Enabled { get; set; }

    public int EffectId { get; set; } = 1;

    public SoundOutput Clone() => new() { Enabled = this.Enabled, EffectId = this.EffectId };
}

/// <summary>An on-screen toast.</summary>
public sealed class ToastOutput
{
    public bool Enabled { get; set; }

    public string Text { get; set; } = string.Empty;

    public ToastStyle Style { get; set; } = ToastStyle.Normal;

    public ToastOutput Clone() => new() { Enabled = this.Enabled, Text = this.Text, Style = this.Style };
}

/// <summary>Persisted per-rule output configuration — any combination of Echo, Sound, Toast.</summary>
public sealed class OutputSpec
{
    public EchoOutput Echo { get; set; } = new();

    public SoundOutput Sound { get; set; } = new();

    public ToastOutput Toast { get; set; } = new();

    /// <summary>True when at least one output is enabled. Used by validation and dispatch.</summary>
    public bool AnyEnabled => this.Echo.Enabled || this.Sound.Enabled || this.Toast.Enabled;

    public OutputSpec Clone() => new()
    {
        Echo = this.Echo.Clone(),
        Sound = this.Sound.Clone(),
        Toast = this.Toast.Clone(),
    };
}
