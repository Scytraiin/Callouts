using Callouts.Core.Engine;

using Xunit;

namespace Callouts.Tests;

public sealed class PlaceholderRendererTests
{
    [Fact]
    public void NamedPlaceholder_IsSubstituted_CaseInsensitive()
    {
        var match = MatchResult.FromValues(("sender", "Krile"));

        Assert.Equal("Ready from Krile!", PlaceholderRenderer.Render("Ready from {sender}!", match));
        Assert.Equal("Ready from Krile!", PlaceholderRenderer.Render("Ready from {SENDER}!", match));
    }

    [Fact]
    public void UnknownPlaceholder_RendersEmpty()
    {
        var match = MatchResult.FromValues(("sender", "Krile"));

        Assert.Equal("x  y", PlaceholderRenderer.Render("x {nope} y", match));
    }

    [Fact]
    public void PositionalCaptures_AreSubstituted()
    {
        var match = new MatchResult { Captures = ["5", "north"] };

        Assert.Equal("pull in 5 to the north", PlaceholderRenderer.Render("pull in $1 to the $2", match));
    }

    [Fact]
    public void CaptureOutOfRange_RendersEmpty()
    {
        var match = new MatchResult { Captures = ["5"] };

        Assert.Equal("a  b", PlaceholderRenderer.Render("a $2 b", match));
    }

    [Fact]
    public void EmptyTemplate_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, PlaceholderRenderer.Render(string.Empty, MatchResult.FromValues()));
    }
}
