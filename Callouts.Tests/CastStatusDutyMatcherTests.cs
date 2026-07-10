using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class CastStatusDutyMatcherTests
{
    // ---- Cast ----

    private static Rule CastRule(int actionId = 0, string? nameContains = null, CasterScope scope = CasterScope.Anyone, bool onlyMe = false)
        => new()
        {
            Source = new SourceSpec
            {
                Kind = TriggerKind.Cast,
                ActionId = actionId,
                ActionNameContains = nameContains,
                CasterScope = scope,
                OnlyTargetingMe = onlyMe,
            },
        };

    private static TriggerEvent CastEvent(int actionId = 123, string action = "Ultima", string caster = "Zenos", bool enemy = true, bool targetSelf = false)
        => new()
        {
            Kind = TriggerKind.Cast,
            ActionId = actionId,
            ActionName = action,
            CasterName = caster,
            CasterIsEnemy = enemy,
            TargetIsSelf = targetSelf,
        };

    [Fact]
    public void Cast_ByActionId_Matches()
    {
        Assert.NotNull(CastTriggerMatcher.Match(CastRule(actionId: 123), CastEvent(actionId: 123)));
        Assert.Null(CastTriggerMatcher.Match(CastRule(actionId: 999), CastEvent(actionId: 123)));
    }

    [Fact]
    public void Cast_ByActionName_Matches()
    {
        Assert.NotNull(CastTriggerMatcher.Match(CastRule(nameContains: "ulti"), CastEvent(action: "Ultima")));
        Assert.Null(CastTriggerMatcher.Match(CastRule(nameContains: "flare"), CastEvent(action: "Ultima")));
    }

    [Fact]
    public void Cast_EnemyScope_RequiresEnemy()
    {
        Assert.Null(CastTriggerMatcher.Match(CastRule(scope: CasterScope.Enemy), CastEvent(enemy: false)));
        Assert.NotNull(CastTriggerMatcher.Match(CastRule(scope: CasterScope.Enemy), CastEvent(enemy: true)));
    }

    [Fact]
    public void Cast_OnlyTargetingMe_Filters()
    {
        Assert.Null(CastTriggerMatcher.Match(CastRule(onlyMe: true), CastEvent(targetSelf: false)));
        Assert.NotNull(CastTriggerMatcher.Match(CastRule(onlyMe: true), CastEvent(targetSelf: true)));
    }

    [Fact]
    public void Cast_PopulatesPlaceholders()
    {
        var result = CastTriggerMatcher.Match(CastRule(), CastEvent(action: "Ultima", caster: "Zenos"));
        Assert.Equal("Ultima", result!.Values["action"]);
        Assert.Equal("Zenos", result.Values["caster"]);
    }

    // ---- Status ----

    private static Rule StatusRule(int id = 0, StatusChangeFilter change = StatusChangeFilter.Gained, BearerScope bearer = BearerScope.Self, int minStacks = 0)
        => new()
        {
            Source = new SourceSpec
            {
                Kind = TriggerKind.Status,
                StatusId = id,
                StatusChange = change,
                Bearer = bearer,
                MinStacks = minStacks,
            },
        };

    private static TriggerEvent StatusEvent(int id = 49, bool gained = true, int stacks = 0, bool self = true, bool party = false, bool target = false, string name = "Well Fed")
        => new()
        {
            Kind = TriggerKind.Status,
            StatusId = id,
            StatusGained = gained,
            Stacks = stacks,
            BearerIsSelf = self,
            BearerInParty = party,
            BearerIsTarget = target,
            StatusName = name,
        };

    [Fact]
    public void Status_GainedVsRemoved_Filters()
    {
        Assert.NotNull(StatusTriggerMatcher.Match(StatusRule(change: StatusChangeFilter.Gained), StatusEvent(gained: true)));
        Assert.Null(StatusTriggerMatcher.Match(StatusRule(change: StatusChangeFilter.Gained), StatusEvent(gained: false)));
        Assert.NotNull(StatusTriggerMatcher.Match(StatusRule(change: StatusChangeFilter.Removed), StatusEvent(gained: false)));
        Assert.NotNull(StatusTriggerMatcher.Match(StatusRule(change: StatusChangeFilter.Either), StatusEvent(gained: false)));
    }

    [Fact]
    public void Status_BearerScope_Filters()
    {
        Assert.Null(StatusTriggerMatcher.Match(StatusRule(bearer: BearerScope.Party), StatusEvent(self: true, party: false)));
        Assert.NotNull(StatusTriggerMatcher.Match(StatusRule(bearer: BearerScope.Party), StatusEvent(self: false, party: true)));
        Assert.NotNull(StatusTriggerMatcher.Match(StatusRule(bearer: BearerScope.Anyone), StatusEvent(self: false)));
    }

    [Fact]
    public void Status_MinStacks_AppliesToGainedOnly()
    {
        Assert.Null(StatusTriggerMatcher.Match(StatusRule(minStacks: 3), StatusEvent(gained: true, stacks: 2)));
        Assert.NotNull(StatusTriggerMatcher.Match(StatusRule(minStacks: 3), StatusEvent(gained: true, stacks: 3)));
    }

    // ---- Duty ----

    [Fact]
    public void Duty_FilterMatchesSpecificEvent()
    {
        var rule = new Rule { Source = new SourceSpec { Kind = TriggerKind.DutyEvent, DutyEvent = DutyEventFilter.Wiped } };

        Assert.NotNull(DutyTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.DutyEvent, DutyEvent = DutyEventFilter.Wiped }));
        Assert.Null(DutyTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.DutyEvent, DutyEvent = DutyEventFilter.Recommenced }));
    }

    [Fact]
    public void Duty_AnyFilterMatchesAll()
    {
        var rule = new Rule { Source = new SourceSpec { Kind = TriggerKind.DutyEvent, DutyEvent = DutyEventFilter.Any } };

        Assert.NotNull(DutyTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.DutyEvent, DutyEvent = DutyEventFilter.Completed }));
    }
}
