using Callouts.Core.Engine;

using Xunit;

namespace Callouts.Tests;

public sealed class TextMatchTests
{
    [Fact]
    public void Contains_CaseInsensitive_MatchesDifferentCasing()
    {
        Assert.True(TextMatch.Contains("Krile has initiated a READY CHECK.", "ready check", caseSensitive: false));
    }

    [Fact]
    public void Contains_CaseSensitive_RequiresExactCasing()
    {
        Assert.False(TextMatch.Contains("Krile has initiated a READY CHECK.", "ready check", caseSensitive: true));
        Assert.True(TextMatch.Contains("Krile has initiated a ready check.", "ready check", caseSensitive: true));
    }

    [Fact]
    public void Contains_EmptyPattern_NeverMatches()
    {
        Assert.False(TextMatch.Contains("any text", "", caseSensitive: false));
    }

    [Fact]
    public void Contains_PatternAbsent_DoesNotMatch()
    {
        Assert.False(TextMatch.Contains("pull in 5", "ready check", caseSensitive: false));
    }
}
