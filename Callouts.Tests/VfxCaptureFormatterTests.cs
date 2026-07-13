using System;

using Callouts.Core.Engine;
using Callouts.Core.Logging;

using Xunit;

namespace Callouts.Tests;

public sealed class VfxCaptureFormatterTests
{
    private static readonly DateTime Ts = new(2026, 7, 13, 14, 2, 11, 123);

    [Fact]
    public void FormatLine_IsTabSeparated_WithExpectedColumns()
    {
        var evt = new TriggerEvent
        {
            Kind = TriggerKind.Vfx,
            TerritoryId = 795,
            Zone = "Sigmascape V4.0",
            TargetIsSelf = true,
            InCombat = true,
            VfxPath = "vfx/lockon/eff/com_share3f.avfx",
        };

        var line = VfxCaptureFormatter.FormatLine(evt, Ts);

        Assert.Equal(
            "2026-07-13 14:02:11.123\t795\tSigmascape V4.0\tself\tcombat\tvfx/lockon/eff/com_share3f.avfx",
            line);
    }

    [Fact]
    public void FormatLine_TargetResolvesSelfThenPartyThenOther()
    {
        var basePath = new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "p" };

        Assert.Contains("\tself\t", VfxCaptureFormatter.FormatLine(basePath with { TargetIsSelf = true }, Ts));
        Assert.Contains("\tparty\t", VfxCaptureFormatter.FormatLine(basePath with { TargetInParty = true }, Ts));
        Assert.Contains("\tother\t", VfxCaptureFormatter.FormatLine(basePath, Ts));
    }

    [Fact]
    public void FormatLine_OutOfCombat_MarksDash()
        => Assert.Contains("\t-\t", VfxCaptureFormatter.FormatLine(new TriggerEvent { Kind = TriggerKind.Vfx, VfxPath = "p" }, Ts));

    [Fact]
    public void FormatLine_StripsTabsAndNewlines_SoEveryEventStaysOneLine()
    {
        var evt = new TriggerEvent
        {
            Kind = TriggerKind.Vfx,
            Zone = "a\tb",
            VfxPath = "line1\r\nline2",
        };

        var line = VfxCaptureFormatter.FormatLine(evt, Ts);

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.Contains("a b", line);            // zone tab -> space
        Assert.Contains("line1  line2", line);   // path CRLF -> two spaces (\r and \n each)
        Assert.Equal(5, CountTabs(line));      // exactly 6 columns => 5 separators
    }

    [Fact]
    public void SessionBanner_AndHeader_AreCommentAndTabbed()
    {
        Assert.StartsWith("# Callouts VFX capture", VfxCaptureFormatter.SessionBanner(Ts));
        Assert.Contains("2026-07-13 14:02:11", VfxCaptureFormatter.SessionBanner(Ts));
        Assert.Equal("time\tterritoryId\tzone\ttarget\tcombat\tpath", VfxCaptureFormatter.Header);
    }

    private static int CountTabs(string s)
    {
        var n = 0;
        foreach (var c in s)
        {
            if (c == '\t')
            {
                n++;
            }
        }

        return n;
    }
}
