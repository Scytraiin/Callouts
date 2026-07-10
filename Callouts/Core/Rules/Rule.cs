using System;

namespace Callouts.Core.Rules;

/// <summary>
/// A user-defined trigger rule. <see cref="Enabled"/> is USER INTENT ONLY and is never
/// written by the engine (DESIGN.md §4.1) — whether a rule actually runs is a computed
/// runtime state, not a persisted flag.
/// </summary>
public sealed class Rule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public SourceSpec Source { get; set; } = new();

    public OutputSpec Outputs { get; set; } = new();

    public double CooldownSeconds { get; set; } = 2.0;

    /// <summary>Deep copy preserving <see cref="Id"/> (used for the editor's working copy).</summary>
    public Rule Clone() => new()
    {
        Id = this.Id,
        Name = this.Name,
        Enabled = this.Enabled,
        Source = this.Source.Clone(),
        Outputs = this.Outputs.Clone(),
        CooldownSeconds = this.CooldownSeconds,
    };
}
