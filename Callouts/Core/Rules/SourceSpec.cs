using System.Collections.Generic;

using Callouts.Core.Engine;

namespace Callouts.Core.Rules;

/// <summary>
/// Persisted description of what a rule listens for. This is a mutable POCO so it can be
/// serialized by the Dalamud plugin configuration. Fields for the non-chat sources are
/// added by their respective issues; the chat fields are all that issue 002 needs.
/// </summary>
public sealed class SourceSpec
{
    public TriggerKind Kind { get; set; } = TriggerKind.Chat;

    // --- Chat ---

    /// <summary>Numeric XivChatType channels this rule accepts. Empty = any channel.</summary>
    public List<int> Channels { get; set; } = [];

    public MatchMode MatchMode { get; set; } = MatchMode.Contains;

    public string Pattern { get; set; } = string.Empty;

    public bool CaseSensitive { get; set; }

    /// <summary>Optional "contains" filter on the sender name. Null/empty = any sender.</summary>
    public string? SenderPattern { get; set; }

    public SourceSpec Clone() => new()
    {
        Kind = this.Kind,
        Channels = [.. this.Channels],
        MatchMode = this.MatchMode,
        Pattern = this.Pattern,
        CaseSensitive = this.CaseSensitive,
        SenderPattern = this.SenderPattern,
    };
}
