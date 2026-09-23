using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   The parts of <see cref="XTextSegmentFormatter"/> that <see cref="XTextSegmentFormatterTests"/>
///   does not reach: the two single-string overloads, what the formatter refuses, and the line
///   break — which it honours three ways over, because a caller's text may carry CR, LF or CRLF and
///   all three mean the same thing.
///   <para>
///   A line break is a block of its own rather than a property of the block beside it, which is
///   what lets a run of segments be cut into paragraphs and each paragraph aligned on its own.
///   </para>
/// </summary>
public class XTextSegmentFormatterLineBreakTests
{
    private static XFont Plain => new("Arial", 12, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);
    private static XFont Large => new("Arial", 24, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    private static readonly XRect Layout = new(20, 20, 300, 400);

    private static TextSegment Segment(string text, XFont font = null, XBrush brush = null)
    {
        return new TextSegment { Text = text, Font = font ?? Plain, Brush = brush ?? XBrushes.Black };
    }

    private static PdfPage PageShowing(Action<XTextSegmentFormatter> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(new XTextSegmentFormatter(gfx));
        return page;
    }

    /// <summary>The baseline of each line of the page, topmost first.</summary>
    private static double[] BaselinesOf(PdfPage page)
    {
        return [..TextBaselines.PositionsOf(page)
            .Select(run => Math.Round(run.Y, 3))
            .Distinct()
            .OrderByDescending(y => y)];
    }

    // ----- the single-string overloads ------------------------------------------------------------

    [Fact]
    public void AStringCanBeDrawnWithoutBuildingASegmentForIt()
    {
        var page = PageShowing(f => f.DrawString("Hello world", Plain, XBrushes.Black, Layout));

        TextBaselines.PositionsOf(page).Should().NotBeEmpty();
    }

    [Fact]
    public void TheSingleStringOverloadTakingAFormatLaysOutTheSameWay()
    {
        var withFormat = PageShowing(f =>
            f.DrawString("Hello world", Plain, XBrushes.Black, Layout, XStringFormats.TopLeft));
        var without = PageShowing(f => f.DrawString("Hello world", Plain, XBrushes.Black, Layout));

        BaselinesOf(withFormat).Should().Equal(BaselinesOf(without));
    }

    // ----- what the formatter refuses -------------------------------------------------------------

    [Fact]
    public void ASegmentWithoutAFontIsRefused()
    {
        var drawing = () => PageShowing(f =>
            f.DrawString([new TextSegment { Text = "x", Brush = XBrushes.Black }], Layout));

        drawing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ASegmentWithoutABrushIsRefused()
    {
        var drawing = () => PageShowing(f =>
            f.DrawString([new TextSegment { Text = "x", Font = Plain }], Layout));

        drawing.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("TopCenter")]
    [InlineData("BottomRight")]
    [InlineData("Center")]
    public void OnlyTopLeftAlignmentIsImplemented(string which)
    {
        var format = which switch
        {
            "TopCenter" => XStringFormats.TopCenter,
            "BottomRight" => XStringFormats.BottomRight,
            _ => XStringFormats.Center
        };

        var drawing = () => PageShowing(f => f.DrawString([Segment("x")], Layout, format));

        drawing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MeasuringAlsoRefusesAnAlignmentItCannotHonour()
    {
        var measuring = () => PageShowing(f =>
            f.CalculateTextSize("x", Plain, XBrushes.Black, 200, XStringFormats.Center));

        measuring.Should().Throw<ArgumentException>();
    }

    // ----- the line break -------------------------------------------------------------------------

    [Theory]
    [InlineData("first\nsecond")]
    [InlineData("first\rsecond")]
    [InlineData("first\r\nsecond")]
    public void EverySpellingOfALineBreakStartsANewLine(string text)
    {
        var page = PageShowing(f => f.DrawString([Segment(text)], Layout));

        BaselinesOf(page).Should().HaveCount(2, "the break ends the first line wherever it came from");
    }

    [Fact]
    public void ABreakAtTheEndOfTheTextStillEndsTheLine()
    {
        var page = PageShowing(f => f.DrawString([Segment("first\n")], Layout));

        BaselinesOf(page).Should().HaveCount(1);
    }

    [Fact]
    public void ABlankLineBetweenTwoParagraphsIsKept()
    {
        var page = PageShowing(f => f.DrawString([Segment("first\n\nsecond")], Layout));

        var baselines = BaselinesOf(page);

        baselines.Should().HaveCount(2);
        var gap = baselines[0] - baselines[1];
        var oneLine = BaselinesOf(PageShowing(f => f.DrawString([Segment("first\nsecond")], Layout)));
        (oneLine[0] - oneLine[1]).Should().BeLessThan(gap, "the blank line takes a line's height of its own");
    }

    [Fact]
    public void ABreakInOneSegmentEndsTheLineForTheSegmentsAfterIt()
    {
        var page = PageShowing(f => f.DrawString([Segment("first\n"), Segment("second")], Layout));

        BaselinesOf(page).Should().HaveCount(2);
    }

    [Fact]
    public void MeasuringCountsTheHeightOfEveryLineABreakMade()
    {
        XSize oneLine = default;
        XSize threeLines = default;
        PageShowing(f =>
        {
            oneLine = f.CalculateTextSize([Segment("first")], 300);
            threeLines = f.CalculateTextSize([Segment("first\nsecond\nthird")], 300);
        });

        threeLines.Height.Should().BeApproximately(oneLine.Height * 3, oneLine.Height * 0.2);
    }

    // ----- lines of differing height ---------------------------------------------------------------

    [Fact]
    public void ALineWhoseTallestRunIsNotItsFirstPushesTheLinesBelowItDown()
    {
        var mixed = PageShowing(f => f.DrawString(
            [Segment("small "), Segment("BIG", Large), Segment("\nnext line")], Layout));
        var even = PageShowing(f => f.DrawString(
            [Segment("small "), Segment("big"), Segment("\nnext line")], Layout));

        var mixedBaselines = BaselinesOf(mixed);
        var evenBaselines = BaselinesOf(even);

        mixedBaselines.Should().HaveCount(2);
        evenBaselines.Should().HaveCount(2);
        mixedBaselines[0].Should().BeLessThan(evenBaselines[0],
            "the taller run pushes its own line's baseline further down the page");
    }

    // ----- running out of room ---------------------------------------------------------------------

    /// <summary>
    ///   Wrapped text stops at the bottom of the rectangle it was given. The block that would sit
    ///   below it is marked <c>Stop</c> and nothing after it is drawn.
    /// </summary>
    [Fact]
    public void WrappedTextStopsAtTheBottomOfTheRectangle()
    {
        var text = string.Join(" ", Enumerable.Repeat("The quick brown fox jumps over the lazy dog", 20));
        var shallow = new XRect(20, 20, 300, 60);

        var page = PageShowing(f => f.DrawString([Segment(text)], shallow));

        var baselines = BaselinesOf(page);

        baselines.Should().NotBeEmpty();
        baselines.Min().Should().BeGreaterThan(700, "drawing stopped rather than running on down the page");
    }

    /// <summary>
    ///   Text broken by the caller does not stop, and that asymmetry is worth knowing before
    ///   trusting the rectangle as a clip. The height check marks the line break that overran, but
    ///   an explicit break also ends the block unit — and the check only stops the unit it is in,
    ///   so every later paragraph is laid out and drawn below the rectangle. Wrapped text is all
    ///   one unit, which is why <see cref="WrappedTextStopsAtTheBottomOfTheRectangle"/> holds.
    /// </summary>
    [Fact]
    public void TextBrokenByTheCallerRunsOnPastTheBottomOfTheRectangle()
    {
        var text = string.Join("\n", Enumerable.Range(0, 40).Select(index => "line " + index));
        var shallow = new XRect(20, 20, 300, 60);

        var page = PageShowing(f => f.DrawString([Segment(text)], shallow));

        BaselinesOf(page).Should().HaveCount(40);
    }

    // ----- the per-segment layout properties --------------------------------------------------------

    [Fact]
    public void ASegmentCanIndentTheLineItStarts()
    {
        var plain = PageShowing(f =>
        {
            f.Alignment = XParagraphAlignment.Left;
            f.DrawString([Segment("indented")], Layout);
        });
        var indented = PageShowing(f =>
        {
            f.Alignment = XParagraphAlignment.Left;
            f.DrawString(
                [new TextSegment { Text = "indented", Font = Plain, Brush = XBrushes.Black, LineIndent = 50 }],
                Layout);
        });

        var plainStart = TextBaselines.PositionsOf(plain)[0].X;
        var indentedStart = TextBaselines.PositionsOf(indented)[0].X;

        indentedStart.Should().BeApproximately(plainStart + 50, 0.01);
    }

    /// <summary>
    ///   Justifying a line spreads the gaps between its words, and a segment that asks to be left
    ///   alone is skipped over before the spreading starts — so a leading label keeps its place
    ///   while the words after it move.
    /// </summary>
    [Fact]
    public void AJustifiedLineLeavesASegmentThatAsksToBeSkippedWhereItIs()
    {
        var label = new TextSegment
        {
            Text = "Label",
            Font = Plain,
            Brush = XBrushes.Black,
            SkipParagraphAlignment = true
        };

        var page = PageShowing(f =>
        {
            f.Alignment = XParagraphAlignment.Justify;
            f.DrawString([label, Segment(" one two three four five six seven eight nine ten\nend")], Layout);
        });

        var runs = TextBaselines.PositionsOf(page);

        runs.Should().NotBeEmpty();
        runs[0].X.Should().BeApproximately(Layout.X, 0.01, "the skipped segment keeps the position it was laid out at");
    }
}
