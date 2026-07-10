using System;
using System.Collections.Generic;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// The pure, synchronous heart of the plugin: given a <see cref="TriggerEvent"/>, it runs
/// every active rule's matcher and returns the <see cref="AlertAction"/>s to execute.
/// Rules are read live from the supplied provider so the engine always sees saved config
/// (DESIGN.md §7.2). Cooldown/rate gates and placeholder rendering are layered on by later
/// issues (004/005); issue 002 dispatches literal output text.
/// </summary>
public sealed class RuleEngine
{
    private readonly Func<IReadOnlyList<Rule>> rulesProvider;

    public RuleEngine(Func<IReadOnlyList<Rule>> rulesProvider)
    {
        this.rulesProvider = rulesProvider;
    }

    public IReadOnlyList<AlertAction> Process(TriggerEvent evt)
    {
        var actions = new List<AlertAction>();

        foreach (var rule in this.rulesProvider())
        {
            if (!RuleRuntimeStateEvaluator.IsActive(rule))
            {
                continue;
            }

            var match = MatchRule(rule, evt);
            if (match is null)
            {
                continue;
            }

            AppendActions(rule, actions);
        }

        return actions;
    }

    private static MatchResult? MatchRule(Rule rule, TriggerEvent evt)
        => rule.Source.Kind switch
        {
            TriggerKind.Chat => ChatTriggerMatcher.Match(rule, evt),
            _ => null,
        };

    private static void AppendActions(Rule rule, List<AlertAction> actions)
    {
        var echo = rule.Outputs.Echo;
        if (echo.Enabled && !string.IsNullOrEmpty(echo.Text))
        {
            actions.Add(new AlertAction { Kind = AlertOutputKind.Echo, Text = echo.Text });
        }
    }
}
