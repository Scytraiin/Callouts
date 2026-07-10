using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class MarkerMappingTests
{
    [Fact]
    public void Lookup_KnownPath_ReturnsNamedMarker()
    {
        Assert.Equal("spread", MarkerMapping.Lookup("vfx/lockon/eff/target_ae0c.avfx"));
        Assert.Equal("flare", MarkerMapping.Lookup("vfx/lockon/eff/flare01.avfx"));
    }

    [Fact]
    public void Lookup_UnknownPath_ReturnsNull()
    {
        Assert.Null(MarkerMapping.Lookup("vfx/common/eff/whatever.avfx"));
    }

    [Fact]
    public void IsHeadMarkerPath_DetectsLockon()
    {
        Assert.True(MarkerMapping.IsHeadMarkerPath("vfx/lockon/eff/x.avfx"));
        Assert.False(MarkerMapping.IsHeadMarkerPath("vfx/common/eff/x.avfx"));
    }
}

public sealed class VfxMarkerMatcherTests
{
    [Fact]
    public void Vfx_PathContains_AndSelfScope()
    {
        var rule = new Rule { Source = new SourceSpec { Kind = TriggerKind.Vfx, VfxPathPattern = "lockon", ActorScope = BearerScope.Self } };

        Assert.NotNull(VfxTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "vfx/lockon/eff/x", TargetIsSelf = true }));
        Assert.Null(VfxTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "vfx/lockon/eff/x", TargetIsSelf = false }));
        Assert.Null(VfxTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "vfx/other", TargetIsSelf = true }));
    }

    [Fact]
    public void HeadMarker_MatchesNamedOrRaw()
    {
        var rule = new Rule { Source = new SourceSpec { Kind = TriggerKind.HeadMarker, MarkerKey = "spread", ActorScope = BearerScope.Anyone } };

        Assert.NotNull(HeadMarkerTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.HeadMarker, MarkerKey = "spread" }));
        Assert.Null(HeadMarkerTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.HeadMarker, MarkerKey = "stack" }));
    }

    [Fact]
    public void HeadMarker_SelfScope_Filters()
    {
        var rule = new Rule { Source = new SourceSpec { Kind = TriggerKind.HeadMarker, MarkerKey = "spread", ActorScope = BearerScope.Self } };

        Assert.NotNull(HeadMarkerTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.HeadMarker, MarkerKey = "spread", TargetIsSelf = true }));
        Assert.Null(HeadMarkerTriggerMatcher.Match(rule, new TriggerEvent { Kind = TriggerKind.HeadMarker, MarkerKey = "spread", TargetIsSelf = false }));
    }
}

public sealed class AvailabilityGatingTests
{
    private static Rule VfxRule() => new()
    {
        Source = new SourceSpec { Kind = TriggerKind.Vfx, VfxPathPattern = "lockon" },
        Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "x" } },
    };

    private static TriggerEvent VfxEvent() => new() { Kind = TriggerKind.Vfx, VfxPath = "vfx/lockon/eff/x" };

    [Fact]
    public void BlockedAdvancedOff_SkipsAndBadges()
    {
        var rule = VfxRule();
        var engine = new RuleEngine(() => new[] { rule })
        {
            AvailabilityProvider = _ => SourceAvailability.BlockedAdvancedOff,
        };

        Assert.Empty(engine.Process(VfxEvent()));
        Assert.Equal(RuleRuntimeState.BlockedAdvancedOff, engine.GetRuntimeState(rule));
    }

    [Fact]
    public void Failed_SkipsAndBadges()
    {
        var rule = VfxRule();
        var engine = new RuleEngine(() => new[] { rule })
        {
            AvailabilityProvider = _ => SourceAvailability.Failed,
        };

        Assert.Empty(engine.Process(VfxEvent()));
        Assert.Equal(RuleRuntimeState.SourceFailed, engine.GetRuntimeState(rule));
    }

    [Fact]
    public void Available_FiresNormally()
    {
        var rule = VfxRule();
        var engine = new RuleEngine(() => new[] { rule })
        {
            AvailabilityProvider = _ => SourceAvailability.Available,
        };

        Assert.Single(engine.Process(VfxEvent()));
        Assert.Equal(RuleRuntimeState.Active, engine.GetRuntimeState(rule));
    }

    [Fact]
    public void StableKinds_UnaffectedByProvider()
    {
        // A chat rule stays available even if the provider would block advanced kinds.
        var chat = new Rule
        {
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "pull" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "go" } },
        };
        var engine = new RuleEngine(() => new[] { chat })
        {
            AvailabilityProvider = kind => kind is TriggerKind.Vfx or TriggerKind.HeadMarker
                ? SourceAvailability.BlockedAdvancedOff
                : SourceAvailability.Available,
        };

        Assert.Single(engine.Process(new TriggerEvent { Kind = TriggerKind.Chat, Channel = 10, Message = "pull" }));
    }
}
