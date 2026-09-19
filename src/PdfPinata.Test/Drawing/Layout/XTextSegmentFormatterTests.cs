using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   <see cref="XTextSegmentFormatter"/> lays out runs of differing font and colour as one flow of
///   text. It is what stands in this library for PDFKit's <c>continued</c> option, which exists so
///   that the styling can be changed in the middle of a paragraph: there the runs are chained by
///   calling <c>text</c> again, here they are handed over together.
///   <para>
///   The class shipped without a test of any kind. These are here because the parity checklist
///   closes <c>continued</c> on the claim that this covers it, and a claim like that is worth no
///   more than what holds it up.
///   </para>
/// </summary>
public class XTextSegmentFormatterTests
{
    static XFont Plain => new XFont("Arial", 12, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);
    static XFont Bold => new XFont("Arial", 12, XFontStyle.Bold, XPdfFontOptions.WinAnsiDefault);

    static PdfPage PageShowing(params TextSegment[] segments)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            new XTextSegmentFormatter(gfx).DrawString(segments, new XRect(20, 20, 220, 200));
        return page;
    }

    static TextSegment Segment(string text, XFont font, XBrush brush)
    {
        return new TextSegment { Text = text, Font = font, Brush = brush };
    }

    [Fact]
    public void TwoSegmentsOfDifferentStyleRunOnAsOneLine()
    {
        var page = PageShowing(
            Segment("Hello", Plain, XBrushes.Black),
            Segment("world", Bold, XBrushes.Red));

        var runs = TextBaselines.PositionsOf(page);

        // The second run picks up where the first left off rather than starting a line of its own.
        // That is the whole of what continued is for.
        runs.Should().HaveCountGreaterThanOrEqualTo(2);
        runs[1].Y.Should().BeApproximately(runs[0].Y, 0.01);
        runs[1].X.Should().BeGreaterThan(runs[0].X);
    }

    [Fact]
    public void EachSegmentIsDrawnInItsOwnColour()
    {
        var page = PageShowing(
            Segment("Hello", Plain, XBrushes.Black),
            Segment("world", Bold, XBrushes.Red));

        // rg is the non-stroking colour, which is what a brush sets.
        var colours = TextOperators.OperandsGivenTo(page, OpCodeName.rg);

        colours.Should().ContainEquivalentOf(new[] { 0d, 0d, 0d });
        colours.Should().ContainEquivalentOf(new[] { 1d, 0d, 0d });
    }

    [Fact]
    public void SegmentsWrapTogetherRatherThanEachToItself()
    {
        var page = PageShowing(
            Segment("The quick brown fox jumps over", Plain, XBrushes.Black),
            Segment("the lazy dog and keeps on going", Bold, XBrushes.Black));

        // One flow of text, so the wrapping happens where the words run out of room and not at the
        // seam between the two segments. A formatter that started each segment on a line of its own
        // would also give more than one line, so the seam is what has to be checked: the run that
        // opens the second segment must sit on a line that already has something on it.
        var runs = TextBaselines.PositionsOf(page);
        TextBaselines.LinesOf(page).Count.Should().BeGreaterThan(1);

        var lineOfEachRun = runs.Select(run => Math.Round(run.Y, 3)).ToList();
        lineOfEachRun.Distinct().Count().Should().BeLessThan(runs.Count,
            "some line must carry more than one run, or every segment began a line of its own");
    }

    [Fact]
    public void ASingleSegmentLaysOutLikePlainText()
    {
        var page = PageShowing(Segment("The quick brown fox jumps over the lazy dog", Plain, XBrushes.Black));

        TextBaselines.PositionsOf(page).Should().NotBeEmpty();
    }

    // ----- alignment ------------------------------------------------------------------------------

    const double LayoutLeft = 20;
    const double LayoutWidth = 220;

    /// <summary>
    ///   The same text laid out under a given alignment, in a rectangle whose left edge is at
    ///   <see cref="LayoutLeft"/>. Alignment is a property of the formatter rather than an
    ///   argument, which is why this cannot go through <see cref="PageShowing(TextSegment[])"/>.
    /// </summary>
    static PdfPage PageShowing(XParagraphAlignment alignment, params TextSegment[] segments)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            new XTextSegmentFormatter(gfx) { Alignment = alignment }
                .DrawString(segments, new XRect(LayoutLeft, 20, LayoutWidth, 200));
        return page;
    }

    /// <summary>Where each line of the page begins, topmost first.</summary>
    static double[] LineStartsOf(PdfPage page)
    {
        return TextBaselines.PositionsOf(page)
            .GroupBy(run => Math.Round(run.Y, 3))
            .OrderByDescending(line => line.Key)
            .Select(line => line.Min(run => run.X))
            .ToArray();
    }

    /// <summary>Where the last run of each line begins, topmost first.</summary>
    static double[] LineEndsOf(PdfPage page)
    {
        return TextBaselines.PositionsOf(page)
            .GroupBy(run => Math.Round(run.Y, 3))
            .OrderByDescending(line => line.Key)
            .Select(line => line.Max(run => run.X))
            .ToArray();
    }

    const string TwoLinesOfWords =
        "The quick brown fox jumps over the lazy dog and then it keeps on running";

    [Fact]
    public void LeftAlignedTextStartsAtTheLeftEdgeOfTheRectangle()
    {
        var page = PageShowing(XParagraphAlignment.Left,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black));

        LineStartsOf(page).Should().AllSatisfy(start =>
            start.Should().BeApproximately(LayoutLeft, 0.5));
    }

    [Fact]
    public void RightAlignedTextIsPushedAwayFromTheLeftEdge()
    {
        var page = PageShowing(XParagraphAlignment.Right,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black));

        // Every line ends flush right instead, so every line starts somewhere different and none
        // of them starts where a left aligned one would.
        LineStartsOf(page).Should().AllSatisfy(start =>
            start.Should().BeGreaterThan(LayoutLeft + 0.5));
    }

    [Fact]
    public void CentredTextSitsBetweenTheTwoEdges()
    {
        var page = PageShowing(XParagraphAlignment.Center,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black));

        var centred = LineStartsOf(page);
        var left = LineStartsOf(PageShowing(XParagraphAlignment.Left,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black)));
        var right = LineStartsOf(PageShowing(XParagraphAlignment.Right,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black)));

        centred.Should().HaveSameCount(left);
        for (var line = 0; line < centred.Length; line++)
        {
            centred[line].Should().BeGreaterThan(left[line] - 0.5);
            centred[line].Should().BeLessThan(right[line] + 0.5);
        }
    }

    /// <summary>
    ///   Justified text is spread to both edges by widening the spaces between its words, so the
    ///   words of a full line no longer sit where they would if they simply followed one another.
    ///   The last line of a paragraph is left alone, which is what stops the final few words of a
    ///   paragraph being stretched across the page.
    /// </summary>
    [Fact]
    public void JustifiedTextSpreadsEveryLineButTheLast()
    {
        var justified = LineEndsOf(PageShowing(XParagraphAlignment.Justify,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black)));
        var ragged = LineEndsOf(PageShowing(XParagraphAlignment.Left,
            Segment(TwoLinesOfWords, Plain, XBrushes.Black)));

        justified.Should().HaveSameCount(ragged);
        justified.Length.Should().BeGreaterThan(1, "there has to be a last line to leave alone");

        justified[0].Should().BeGreaterThan(ragged[0] + 0.5,
            "the first line is stretched towards the right edge");
        justified[^1].Should().BeApproximately(ragged[^1], 0.5,
            "and the last line is left as it fell");
    }

    [Fact]
    public void AlignmentDoesNotChangeWhichWordsAreOnWhichLine()
    {
        // Alignment moves a line, it does not re-wrap it. The count of runs and of lines has to
        // come out the same whichever way the text is aligned.
        var counts = new[]
        {
            XParagraphAlignment.Left, XParagraphAlignment.Right,
            XParagraphAlignment.Center, XParagraphAlignment.Justify
        }.Select(alignment => LineStartsOf(
            PageShowing(alignment, Segment(TwoLinesOfWords, Plain, XBrushes.Black))).Length);

        counts.Distinct().Should().ContainSingle();
    }

    /// <summary>
    ///   Both of these say the layout does not loop, and a layout that looped would hang the test
    ///   host rather than fail a test. xUnit honours Timeout only on an async test, which is why
    ///   they are written this way - the same shape <c>CLexerTests</c> uses for its malformed
    ///   input, and for the same reason.
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ASingleWordTooLongForTheLineIsStillDrawn()
    {
        // A block that cannot be broken and does not fit is the case the layout has to place
        // somewhere rather than loop over.
        var page = await Task.Run(() => PageShowing(XParagraphAlignment.Justify,
            Segment(new string('W', 80), Plain, XBrushes.Black)));

        TextBaselines.PositionsOf(page).Should().NotBeEmpty();
    }

    [Fact(Timeout = 30000)]
    public async Task TextThatRunsPastTheBottomOfTheRectangleDoesNotLoop()
    {
        var page = await Task.Run(() => PageShowing(XParagraphAlignment.Justify,
            Segment(string.Join(" ", Enumerable.Repeat(TwoLinesOfWords, 20)), Plain, XBrushes.Black)));

        TextBaselines.PositionsOf(page).Should().NotBeEmpty();
    }

    [Fact]
    public void AnEmptySegmentDrawsNothingAndDoesNotThrow()
    {
        var draw = () => PageShowing(XParagraphAlignment.Justify, Segment("", Plain, XBrushes.Black));

        draw.Should().NotThrow();
    }

    // ---------------------------------------------------------------------------------------------
    // CalculateTextSize - the measuring half of the class, which lays the text out into a rectangle
    // of unbounded height and reports what it filled. All four overloads had never been executed.
    // ---------------------------------------------------------------------------------------------

    const string Sentence = "A sentence long enough that it has to wrap when the width is small.";

    static XSize Measured(Func<XTextSegmentFormatter, XSize> measure)
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        return measure(new XTextSegmentFormatter(gfx));
    }

    [Fact]
    public void MeasuringTextReportsSomethingWithinTheWidthItWasGiven()
    {
        var size = Measured(f => f.CalculateTextSize(Sentence, Plain, XBrushes.Black, 400));

        size.Width.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(400);
        size.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NarrowingTheWidthMakesTheSameTextTaller()
    {
        // The assertion that says it is really laying the text out rather than measuring one line:
        // the same words in a narrower column have to wrap onto more of them.
        var wide = Measured(f => f.CalculateTextSize(Sentence, Plain, XBrushes.Black, 400));
        var narrow = Measured(f => f.CalculateTextSize(Sentence, Plain, XBrushes.Black, 80));

        narrow.Height.Should().BeGreaterThan(wide.Height);
        narrow.Width.Should().BeLessThanOrEqualTo(80);
    }

    [Fact]
    public void TheOverloadWithoutAFormatMeasuresTheSameAsTopLeft()
    {
        var byDefault = Measured(f => f.CalculateTextSize(Sentence, Plain, XBrushes.Black, 200));
        var explicitly = Measured(f =>
            f.CalculateTextSize(Sentence, Plain, XBrushes.Black, 200, XStringFormats.TopLeft));

        byDefault.Width.Should().Be(explicitly.Width);
        byDefault.Height.Should().Be(explicitly.Height);
    }

    [Fact]
    public void OneSegmentMeasuresTheSameAsTheStringItHolds()
    {
        // The string overloads build a single segment and hand it to the segment overload, so the
        // two routes have to agree - and if they ever stop agreeing, one of them has grown a step
        // the other has not.
        var asString = Measured(f => f.CalculateTextSize(Sentence, Plain, XBrushes.Black, 200));
        var asSegment = Measured(f =>
            f.CalculateTextSize(new[] { Segment(Sentence, Plain, XBrushes.Black) }, 200));

        asSegment.Width.Should().BeApproximately(asString.Width, 0.01);
        asSegment.Height.Should().BeApproximately(asString.Height, 0.01);
    }

    [Fact]
    public void TheSegmentOverloadWithoutAFormatAlsoMeasuresAsTopLeft()
    {
        var segments = new[] { Segment(Sentence, Plain, XBrushes.Black) };

        var byDefault = Measured(f => f.CalculateTextSize(segments, 200));
        var explicitly = Measured(f => f.CalculateTextSize(segments, 200, XStringFormats.TopLeft));

        byDefault.Width.Should().Be(explicitly.Width);
        byDefault.Height.Should().Be(explicitly.Height);
    }

    [Fact]
    public void MoreTextMeasuresTaller()
    {
        var one = Measured(f => f.CalculateTextSize(
            new[] { Segment(Sentence, Plain, XBrushes.Black) }, 200));
        var two = Measured(f => f.CalculateTextSize(
            new[] { Segment(Sentence, Plain, XBrushes.Black), Segment(Sentence, Bold, XBrushes.Red) }, 200));

        two.Height.Should().BeGreaterThan(one.Height);
    }

    [Fact]
    public void MeasuringNothingReportsNoHeightButTheWholeWidthItWasOffered()
    {
        // Worth pinning rather than assuming, because it is the surprising half of the pair: with no
        // blocks to measure there is no height, and the width falls back to the width the caller
        // offered rather than to nothing. A caller sizing a box to its content gets the box it
        // started with.
        var size = Measured(f => f.CalculateTextSize("", Plain, XBrushes.Black, 250));

        size.Height.Should().Be(0);
        size.Width.Should().Be(250);
    }
}
