using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class RuleValidatorExtraTests
{
    private static Rule Base()
        => new()
        {
            Name = "ok",
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "x" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "t" } },
        };

    [Fact]
    public void InvalidRegexPattern_IsRejected()
    {
        var rule = Base();
        rule.Source.MatchMode = MatchMode.Regex;
        rule.Source.Pattern = "(unclosed";

        Assert.False(RuleValidator.IsValid(rule));
    }

    [Fact]
    public void ValidRegexPattern_IsAccepted()
    {
        var rule = Base();
        rule.Source.MatchMode = MatchMode.Regex;
        rule.Source.Pattern = @"pull in (\d+)";

        Assert.True(RuleValidator.IsValid(rule));
    }

    [Fact]
    public void ToastEnabledWithoutText_IsRejected()
    {
        var rule = Base();
        rule.Outputs.Echo.Enabled = false;
        rule.Outputs.Toast.Enabled = true;
        rule.Outputs.Toast.Text = string.Empty;

        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("Toast", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SoundOutOfRange_IsRejected()
    {
        var rule = Base();
        rule.Outputs.Sound.Enabled = true;
        rule.Outputs.Sound.EffectId = 99;

        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("Sound", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SoundOnly_NoEcho_IsValid()
    {
        var rule = Base();
        rule.Outputs.Echo.Enabled = false;
        rule.Outputs.Sound.Enabled = true;
        rule.Outputs.Sound.EffectId = 6;

        Assert.True(RuleValidator.IsValid(rule));
    }
}
