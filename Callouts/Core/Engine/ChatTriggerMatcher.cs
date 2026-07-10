using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// Pure matcher for chat rules. Issue 002 implements Contains mode only; Regex mode and
/// capture groups arrive in issue 004.
/// </summary>
public static class ChatTriggerMatcher
{
    public static MatchResult? Match(Rule rule, TriggerEvent evt)
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

        // Optional sender filter (contains).
        if (!string.IsNullOrEmpty(rule.Source.SenderPattern)
            && !TextMatch.Contains(evt.Sender, rule.Source.SenderPattern, rule.Source.CaseSensitive))
        {
            return null;
        }

        if (!TextMatch.Contains(evt.Message, rule.Source.Pattern, rule.Source.CaseSensitive))
        {
            return null;
        }

        return new MatchResult { Sender = evt.Sender, Message = evt.Message };
    }
}
