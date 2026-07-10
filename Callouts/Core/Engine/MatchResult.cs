using System.Collections.Generic;

namespace Callouts.Core.Engine;

/// <summary>
/// The outcome of a successful match. Carries the values a placeholder renderer will need
/// (issue 004); issue 002 populates <see cref="Sender"/> and <see cref="Message"/> and
/// leaves <see cref="Captures"/> empty (regex capture groups arrive with regex mode).
/// </summary>
public sealed record MatchResult
{
    public string Sender { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Regex capture groups ($1..$9). Empty for Contains matches.</summary>
    public IReadOnlyList<string> Captures { get; init; } = [];
}
