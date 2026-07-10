using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class RuleValidatorTests
{
    private static Rule Valid()
        => new()
        {
            Name = "ok",
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "ready check" },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "Ready!" } },
        };

    [Fact]
    public void ValidRule_HasNoErrors()
    {
        Assert.True(RuleValidator.IsValid(Valid()));
        Assert.Empty(RuleValidator.Validate(Valid()));
    }

    [Fact]
    public void EmptyPattern_IsRejected()
    {
        var rule = Valid();
        rule.Source.Pattern = "   ";

        Assert.False(RuleValidator.IsValid(rule));
        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("pattern", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoOutputEnabled_IsRejected()
    {
        var rule = Valid();
        rule.Outputs.Echo.Enabled = false;

        Assert.False(RuleValidator.IsValid(rule));
        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("output", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EchoEnabledWithoutText_IsRejected()
    {
        var rule = Valid();
        rule.Outputs.Echo.Text = string.Empty;

        Assert.False(RuleValidator.IsValid(rule));
        Assert.Contains(RuleValidator.Validate(rule), e => e.Contains("Echo text", System.StringComparison.OrdinalIgnoreCase));
    }
}
