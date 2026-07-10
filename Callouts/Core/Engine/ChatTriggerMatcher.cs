using System.Collections.Generic;
using System.Text.RegularExpressions;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// Pure matcher for chat rules. Contains and Regex modes. For Regex the caller supplies a
/// pre-compiled <see cref="Regex"/> (the engine owns compilation + caching + error handling);
/// a regex match here may throw <see cref="RegexMatchTimeoutException"/>, which the engine
/// catches and turns into a RuleError.
/// </summary>
public static class ChatTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt, Regex? compiledPattern = null)
    {
        if (evt.Kind != TriggerKind.Chat || rule.Source.Kind != TriggerKind.Chat)
        {
            return null;
        }

        // Anti-loop hard rule (non-configurable): the Echo channel is never matchable.
        if (evt.Channel == ChatChannels.Echo)
        {
            return null;
        }

        // Channel filter: empty list = any channel.
        if (rule.Source.Channels.Count > 0 && !rule.Source.Channels.Contains(evt.Channel))
        {
            return null;
        }

        // Optional sender filter (always "contains", never regex — FR-2.1).
        if (!string.IsNullOrEmpty(rule.Source.SenderPattern)
            && !TextMatch.Contains(evt.Sender, rule.Source.SenderPattern, rule.Source.CaseSensitive))
        {
            return null;
        }

        IReadOnlyList<string> captures = [];

        if (rule.Source.MatchMode == MatchMode.Regex)
        {
            if (compiledPattern is null)
            {
                return null;
            }

            var match = compiledPattern.Match(evt.Message);
            if (!match.Success)
            {
                return null;
            }

            captures = ExtractCaptures(match);
        }
        else if (!TextMatch.Contains(evt.Message, rule.Source.Pattern, rule.Source.CaseSensitive))
        {
            return null;
        }

        var values = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["sender"] = evt.Sender,
            ["message"] = evt.Message,
        };

        return new MatchResult { Values = values, Captures = captures };
    }

    private static List<string> ExtractCaptures(Match match)
    {
        var captures = new List<string>();
        for (var i = 1; i < match.Groups.Count; i++)
        {
            captures.Add(match.Groups[i].Value);
        }

        return captures;
    }
}
