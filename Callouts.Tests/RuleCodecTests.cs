using System.Collections.Generic;

using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class RuleCodecTests
{
    private static Rule Sample(string id, string name)
        => new()
        {
            Id = id,
            Name = name,
            Source = new SourceSpec { Kind = TriggerKind.Cast, ActionId = 42, ActionNameContains = "Ultima", Channels = [10, 14] },
            Outputs = new OutputSpec
            {
                Echo = new EchoOutput { Enabled = true, Text = "{caster}!" },
                Sound = new SoundOutput { Enabled = true, EffectId = 8 },
                Toast = new ToastOutput { Enabled = true, Text = "T", Style = ToastStyle.Quest },
            },
            Scope = new RuleScope { OnlyInCombat = true, TerritoryIds = { 1000 } },
            CooldownSeconds = 5,
        };

    [Fact]
    public void RoundTrip_PreservesRulesAndIds()
    {
        var original = new List<Rule> { Sample("id-1", "one"), Sample("id-2", "two") };

        var code = RuleCodec.Export(original);
        var result = RuleCodec.Import(code);

        Assert.True(result.Success);
        Assert.Equal(2, result.Rules.Count);
        Assert.Equal("id-1", result.Rules[0].Id);
        Assert.Equal(42, result.Rules[0].Source.ActionId);
        Assert.Equal("Ultima", result.Rules[0].Source.ActionNameContains);
        Assert.Equal(new[] { 10, 14 }, result.Rules[0].Source.Channels);
        Assert.Equal(8, result.Rules[0].Outputs.Sound.EffectId);
        Assert.Equal(ToastStyle.Quest, result.Rules[0].Outputs.Toast.Style);
        Assert.True(result.Rules[0].Scope.OnlyInCombat);
        Assert.Equal(5, result.Rules[0].CooldownSeconds);
    }

    [Fact]
    public void Import_WrongPrefix_Rejected()
    {
        Assert.False(RuleCodec.Import("ZZ1|abc").Success);
        Assert.False(RuleCodec.Import("not a code").Success);
        Assert.False(RuleCodec.Import(null).Success);
    }

    [Fact]
    public void Import_CorruptBase64_Rejected()
    {
        Assert.False(RuleCodec.Import("CO1|!!!not base64!!!").Success);
    }

    [Fact]
    public void Import_GarbageAfterPrefix_Rejected()
    {
        // Valid base64 but not gzip.
        Assert.False(RuleCodec.Import("CO1|" + System.Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })).Success);
    }

    [Fact]
    public void Merge_Replace_UpdatesInPlace_NoDuplication()
    {
        var existing = new List<Rule> { Sample("id-1", "old") };
        var incoming = new List<Rule> { Sample("id-1", "new") };

        var report = RuleCodec.Merge(existing, incoming, CollisionChoice.Replace);

        Assert.Single(existing);
        Assert.Equal("new", existing[0].Name);
        Assert.Equal(1, report.Replaced);
    }

    [Fact]
    public void Merge_Skip_KeepsExisting()
    {
        var existing = new List<Rule> { Sample("id-1", "old") };
        var report = RuleCodec.Merge(existing, new List<Rule> { Sample("id-1", "new") }, CollisionChoice.Skip);

        Assert.Single(existing);
        Assert.Equal("old", existing[0].Name);
        Assert.Equal(1, report.Skipped);
    }

    [Fact]
    public void Merge_KeepBoth_AddsWithNewId()
    {
        var existing = new List<Rule> { Sample("id-1", "old") };
        var report = RuleCodec.Merge(existing, new List<Rule> { Sample("id-1", "new") }, CollisionChoice.KeepBoth);

        Assert.Equal(2, existing.Count);
        Assert.NotEqual(existing[0].Id, existing[1].Id);
        Assert.Equal(1, report.Added);
    }

    [Fact]
    public void Merge_NewRule_IsAdded()
    {
        var existing = new List<Rule> { Sample("id-1", "one") };
        var report = RuleCodec.Merge(existing, new List<Rule> { Sample("id-2", "two") }, CollisionChoice.Replace);

        Assert.Equal(2, existing.Count);
        Assert.Equal(1, report.Added);
    }

    [Fact]
    public void ReimportSamePack_WithReplace_KeepsCountStable()
    {
        var existing = StarterPack.Create();
        var count = existing.Count;

        var report = RuleCodec.Merge(existing, StarterPack.Create(), CollisionChoice.Replace);

        Assert.Equal(count, existing.Count);
        Assert.Equal(count, report.Replaced);
        Assert.Equal(0, report.Added);
    }

    [Fact]
    public void StarterPack_RoundTripsThroughCodec()
    {
        var code = RuleCodec.Export(StarterPack.Create());
        var result = RuleCodec.Import(code);

        Assert.True(result.Success);
        Assert.Equal(StarterPack.Create().Count, result.Rules.Count);
        Assert.Contains(result.Rules, r => r.Id == "starter-ready-check");
    }
}
