using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Callouts.Core.Rules;

namespace Callouts.Core.Engine;

/// <summary>
/// The pure, synchronous heart of the plugin: given a <see cref="TriggerEvent"/>, it runs
/// every active rule's matcher, applies the cooldown and rate-limit gates, renders
/// placeholders, and returns the <see cref="AlertAction"/>s to execute. Rules are read live
/// from the supplied provider so the engine always sees saved config (DESIGN.md §7.2).
///
/// The engine never mutates <see cref="Rule.Enabled"/>: a bad regex records a per-rule
/// RuleError (surfaced to the UI) and skips that rule. Test-fires bypass both gates and the
/// fire counters (DESIGN.md §7.5).
/// </summary>
public sealed class RuleEngine
{
    private readonly Func<IReadOnlyList<Rule>> rulesProvider;
    private readonly Func<DateTime> clock;
    private readonly CooldownGate cooldownGate = new();

    private readonly Dictionary<string, CompiledRegex> regexCache = new();
    private readonly Dictionary<string, string> ruleErrors = new();
    private readonly Dictionary<string, long> fireCounts = new();

    public RuleEngine(
        Func<IReadOnlyList<Rule>> rulesProvider,
        Func<DateTime>? clock = null,
        RateLimiter? rateLimiter = null)
    {
        this.rulesProvider = rulesProvider;
        this.clock = clock ?? (() => DateTime.UtcNow);
        this.RateLimiter = rateLimiter ?? new RateLimiter();
    }

    public RateLimiter RateLimiter { get; }

    /// <summary>True while the whole plugin is off (master switch, issue 013).</summary>
    public bool MasterEnabled { get; set; } = true;

    /// <summary>
    /// Supplies per-kind availability (issue 014). Null = every kind available. Advanced kinds
    /// return BlockedAdvancedOff / Failed so their rules are skipped and badged accordingly.
    /// </summary>
    public Func<TriggerKind, SourceAvailability>? AvailabilityProvider { get; set; }

    public IReadOnlyList<AlertAction> Process(TriggerEvent evt)
    {
        var actions = new List<AlertAction>();
        if (!this.MasterEnabled)
        {
            return actions;
        }

        var now = this.clock();

        foreach (var rule in this.rulesProvider())
        {
            if (!rule.Enabled)
            {
                continue;
            }

            if (this.AvailabilityOf(rule.Source.Kind) != SourceAvailability.Available)
            {
                continue;
            }

            if (!ScopeMatcher.InScope(rule.Scope, evt))
            {
                continue;
            }

            var match = this.TryMatch(rule, evt);
            if (match is null)
            {
                continue;
            }

            // A rule with no enabled output must not consume a cooldown or a rate-limit token
            // (defends against imported rules that skipped editor validation).
            if (!rule.Outputs.AnyEnabled)
            {
                continue;
            }

            // Cooldown is per rule; the rate limit is global. Neither is consulted for the
            // synthetic test-fire path (see BuildActions / test-fire in issue 011).
            if (!this.cooldownGate.IsReady(rule.Id, rule.CooldownSeconds, now))
            {
                continue;
            }

            if (!this.RateLimiter.TryAcquire(now))
            {
                continue;
            }

            this.cooldownGate.Record(rule.Id, now);
            this.fireCounts[rule.Id] = this.GetFireCount(rule.Id) + 1;
            BuildActions(rule, match, actions);
        }

        return actions;
    }

    /// <summary>
    /// Renders a rule's outputs from a synthetic match, bypassing cooldown/rate gates and the
    /// fire counter. Used by the editor/list test-fire button (issue 011).
    /// </summary>
    public IReadOnlyList<AlertAction> BuildTestActions(Rule rule, MatchResult syntheticMatch)
    {
        var actions = new List<AlertAction>();
        BuildActions(rule, syntheticMatch, actions);
        return actions;
    }

    public RuleRuntimeState GetRuntimeState(Rule rule)
    {
        var availability = this.AvailabilityOf(rule.Source.Kind);
        return RuleRuntimeStateEvaluator.Evaluate(
            rule,
            hasError: this.ruleErrors.ContainsKey(rule.Id),
            sourceAvailable: availability != SourceAvailability.Failed,
            blockedAdvancedOff: availability == SourceAvailability.BlockedAdvancedOff);
    }

    private SourceAvailability AvailabilityOf(TriggerKind kind)
        => this.AvailabilityProvider?.Invoke(kind) ?? SourceAvailability.Available;

    public bool TryGetError(string ruleId, out string message)
        => this.ruleErrors.TryGetValue(ruleId, out message!);

    public long GetFireCount(string ruleId)
        => this.fireCounts.TryGetValue(ruleId, out var count) ? count : 0;

    private MatchResult? TryMatch(Rule rule, TriggerEvent evt)
    {
        Regex? compiled = null;
        if (rule.Source.Kind == TriggerKind.Chat && rule.Source.MatchMode == MatchMode.Regex)
        {
            if (!this.TryGetRegex(rule, out compiled))
            {
                return null; // error already recorded
            }
        }

        try
        {
            var result = rule.Source.Kind switch
            {
                TriggerKind.Chat => ChatTriggerMatcher.Match(rule, evt, compiled),
                TriggerKind.Cast => CastTriggerMatcher.Match(rule, evt),
                TriggerKind.Status => StatusTriggerMatcher.Match(rule, evt),
                TriggerKind.DutyEvent => DutyTriggerMatcher.Match(rule, evt),
                TriggerKind.Vfx => VfxTriggerMatcher.Match(rule, evt),
                TriggerKind.HeadMarker => HeadMarkerTriggerMatcher.Match(rule, evt),
                _ => null,
            };

            this.ruleErrors.Remove(rule.Id);
            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            this.ruleErrors[rule.Id] = "Regex timed out (>50 ms) — simplify the pattern.";
            return null;
        }
    }

    private bool TryGetRegex(Rule rule, out Regex? regex)
    {
        var pattern = rule.Source.Pattern;
        var caseSensitive = rule.Source.CaseSensitive;

        if (this.regexCache.TryGetValue(rule.Id, out var cached)
            && cached.Pattern == pattern
            && cached.CaseSensitive == caseSensitive)
        {
            regex = cached.Regex;
            if (cached.Error is not null)
            {
                this.ruleErrors[rule.Id] = cached.Error;
            }

            return cached.Regex is not null;
        }

        if (RegexFactory.TryCompile(pattern, caseSensitive, out var compiled, out var error))
        {
            this.regexCache[rule.Id] = new CompiledRegex(pattern, caseSensitive, compiled, null);
            regex = compiled;
            return true;
        }

        this.regexCache[rule.Id] = new CompiledRegex(pattern, caseSensitive, null, error);
        this.ruleErrors[rule.Id] = error!;
        regex = null;
        return false;
    }

    private static void BuildActions(Rule rule, MatchResult match, List<AlertAction> actions)
    {
        var outputs = rule.Outputs;

        if (outputs.Echo.Enabled && !string.IsNullOrEmpty(outputs.Echo.Text))
        {
            actions.Add(new AlertAction
            {
                Kind = AlertOutputKind.Echo,
                Text = PlaceholderRenderer.Render(outputs.Echo.Text, match),
            });
        }

        if (outputs.Sound.Enabled)
        {
            actions.Add(new AlertAction
            {
                Kind = AlertOutputKind.Sound,
                SoundEffectId = outputs.Sound.EffectId,
            });
        }

        if (outputs.Toast.Enabled && !string.IsNullOrEmpty(outputs.Toast.Text))
        {
            actions.Add(new AlertAction
            {
                Kind = AlertOutputKind.Toast,
                Text = PlaceholderRenderer.Render(outputs.Toast.Text, match),
                ToastStyle = outputs.Toast.Style,
            });
        }
    }

    private readonly record struct CompiledRegex(string Pattern, bool CaseSensitive, Regex? Regex, string? Error);
}
