using Callouts.Core.Engine;
using Callouts.Core.Rules;

using Xunit;

namespace Callouts.Tests;

public sealed class RuleCloneTests
{
    [Fact]
    public void Clone_IsDeep_AndPreservesId()
    {
        var original = new Rule
        {
            Name = "orig",
            Source = new SourceSpec { Kind = TriggerKind.Chat, Pattern = "a", Channels = [10, 14] },
            Outputs = new OutputSpec { Echo = new EchoOutput { Enabled = true, Text = "hi" } },
        };

        var clone = original.Clone();

        Assert.Equal(original.Id, clone.Id);

        // Mutating the clone must not affect the original (deep copy).
        clone.Name = "changed";
        clone.Source.Pattern = "b";
        clone.Source.Channels.Add(24);
        clone.Outputs.Echo.Text = "bye";

        Assert.Equal("orig", original.Name);
        Assert.Equal("a", original.Source.Pattern);
        Assert.Equal(new[] { 10, 14 }, original.Source.Channels);
        Assert.Equal("hi", original.Outputs.Echo.Text);
    }
}
