using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Drawing.Layout.enums;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   The paragraph options <see cref="XTextFormatter"/> grew for parity with PDFKit: whether it
///   wraps at all, how far a paragraph is indented, how much room is left between lines and
///   between paragraphs, and what marks text that was cut off.
///   <para>
///   Read out of the content stream rather than compared against a reference image. Where a line
///   sits and what it says are exactly the things being asserted here, and the content says both
///   without a rasterizer having to agree about the edge of a glyph.
///   </para>
/// </summary>
public class XTextFormatterOptionsTests
{
    const double LineHeight = 20;

    /// <summary>
    ///   Asked for as WinAnsi so that the strings drawn can be read back out of the content.
    ///   A plain <c>new XFont(...)</c> takes its encoding from
    ///   <see cref="PdfPinata.Fonts.GlobalFontSettings.DefaultFontEncoding"/>, which is Unicode,
    ///   and a Unicode font writes glyph identifiers where the characters would be. Which encoding
    ///   is used changes nothing about the layout: the glyphs and their widths are the same either
    ///   way, and every test here but the ellipsis ones would pass with either.
    /// </summary>
    static XFont Font => new XFont("Arial", 12, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    /// <summary>A rectangle wide enough for two or three words of the text below.</summary>
    static XRect Narrow => new XRect(20, 20, 120, 400);

    const string ThreeLinesish = "The quick brown fox jumps over the lazy dog";

    /// <summary>
    ///   Lays the text out and hands back the page, with the formatter set up by the caller.
    /// </summary>
    static PdfPage PageShowing(string text, XRect layout, System.Action<XTextFormatter> setUp = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var formatter = new XTextFormatter(gfx);
            setUp?.Invoke(formatter);
            formatter.DrawString(text, Font, XBrushes.Black, layout, XUnit.FromPoint(LineHeight));
        }
        return page;
    }

    /// <summary>Where each drawn run starts, in the order it was drawn.</summary>
    static IReadOnlyList<(double X, double Y)> RunsOf(PdfPage page) => TextBaselines.PositionsOf(page);

    /// <summary>The distinct baselines used, from the top of the page down.</summary>
    static IReadOnlyList<double> LinesOf(PdfPage page) => TextBaselines.LinesOf(page);

    // ----- C1, line breaking ---------------------------------------------------------------------

    [Fact]
    public void TextTooWideForTheRectangleIsWrappedByDefault()
    {
        LinesOf(PageShowing(ThreeLinesish, Narrow)).Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void TurningLineBreakingOffKeepsItAllOnOneLine()
    {
        var page = PageShowing(ThreeLinesish, Narrow, f => f.LineBreak = false);

        LinesOf(page).Should().ContainSingle();
    }

    [Fact]
    public void ALineBreakWrittenIntoTheTextIsObeyedEvenWithWrappingOff()
    {
        // Wrapping and breaking are different things. Turning off the first must not silently
        // turn off the second, or a two-line address comes out as one.
        var page = PageShowing("first line\nsecond line", Narrow, f => f.LineBreak = false);

        LinesOf(page).Should().HaveCount(2);
    }

    // ----- C2, indents ---------------------------------------------------------------------------

    [Fact]
    public void TheFirstLineOfAParagraphIsIndented()
    {
        var plain = RunsOf(PageShowing(ThreeLinesish, Narrow));
        var indented = RunsOf(PageShowing(ThreeLinesish, Narrow, f => f.Indent = 20));

        indented[0].X.Should().BeApproximately(plain[0].X + 20, 0.01);
    }

    [Fact]
    public void AnIndentIsOffTheFirstLineOnly()
    {
        var indented = RunsOf(PageShowing(ThreeLinesish, Narrow, f => f.Indent = 20));

        indented.Count.Should().BeGreaterThan(1);
        indented[1].X.Should().BeApproximately(indented[0].X - 20, 0.01);
    }

    [Fact]
    public void IndentAllLinesIndentsEveryLineTheSame()
    {
        var indented = RunsOf(PageShowing(ThreeLinesish, Narrow, f =>
        {
            f.Indent = 20;
            f.IndentAllLines = true;
        }));

        indented.Count.Should().BeGreaterThan(1);
        indented.Select(run => run.X).Distinct().Should().ContainSingle();
    }

    [Fact]
    public void EachParagraphGetsItsFirstLineIndentAgain()
    {
        var page = PageShowing("alpha beta\ngamma delta", Narrow, f => f.Indent = 20);
        var runs = RunsOf(page);

        // Two paragraphs, each on one line, and both indented - the indent belongs to a paragraph
        // rather than to the text as a whole.
        runs.Should().HaveCount(2);
        runs[1].X.Should().BeApproximately(runs[0].X, 0.01);
    }

    [Fact]
    public void AnIndentedLineWrapsSooner()
    {
        // The indent takes its room off the line, so the same rectangle holds less of the text.
        var plain = LinesOf(PageShowing(ThreeLinesish, Narrow)).Count;
        var indented = LinesOf(PageShowing(ThreeLinesish, Narrow, f =>
        {
            f.Indent = 40;
            f.IndentAllLines = true;
        })).Count;

        indented.Should().BeGreaterThan(plain);
    }

    // ----- C3 and C4, the gaps -------------------------------------------------------------------

    [Fact]
    public void ALineGapAddsRoomBetweenEveryLine()
    {
        var tight = LinesOf(PageShowing(ThreeLinesish, Narrow));
        var loose = LinesOf(PageShowing(ThreeLinesish, Narrow, f => f.LineGap = 6));

        tight.Count.Should().BeGreaterThan(1);
        loose.Should().HaveCount(tight.Count);

        // Every step down the page grows by the gap, and by exactly the gap.
        (tight[0] - tight[1]).Should().BeApproximately(LineHeight, 0.01);
        (loose[0] - loose[1]).Should().BeApproximately(LineHeight + 6, 0.01);
    }

    [Fact]
    public void AParagraphGapAddsRoomOnlyWhereAParagraphEnds()
    {
        // Two paragraphs, the first of which wraps: one step is a wrap and one is a paragraph.
        var page = PageShowing(ThreeLinesish + "\nlast", Narrow, f => f.ParagraphGap = 10);
        var lines = LinesOf(page);

        lines.Count.Should().BeGreaterThan(2);

        var withinParagraph = lines[0] - lines[1];
        var acrossParagraphs = lines[lines.Count - 2] - lines[lines.Count - 1];

        withinParagraph.Should().BeApproximately(LineHeight, 0.01);
        acrossParagraphs.Should().BeApproximately(LineHeight + 10, 0.01);
    }

    [Fact]
    public void TheTwoGapsAddUpWhereAParagraphEnds()
    {
        var page = PageShowing("alpha\nbeta", Narrow, f =>
        {
            f.LineGap = 4;
            f.ParagraphGap = 10;
        });

        var lines = LinesOf(page);
        lines.Should().HaveCount(2);
        (lines[0] - lines[1]).Should().BeApproximately(LineHeight + 4 + 10, 0.01);
    }

    // ----- C5, the ellipsis ----------------------------------------------------------------------

    /// <summary>Two lines of room for text that needs more than two.</summary>
    static XRect TwoLinesDeep => new XRect(20, 20, 120, 2 * LineHeight);

    [Fact]
    public void TextThatDoesNotFitEndsWithTheEllipsis()
    {
        var page = PageShowing(ThreeLinesish, TwoLinesDeep, f => f.Ellipsis = "...");

        var shown = TextOperators.ShownStrings(page);
        shown.Should().NotBeEmpty();
        shown[shown.Count - 1].Should().EndWith("...");
    }

    [Fact]
    public void NothingIsMarkedWhenItAllFits()
    {
        var page = PageShowing("short", Narrow, f => f.Ellipsis = "...");

        TextOperators.ShownStrings(page).Should().NotContain(text => text.EndsWith("..."));
    }

    [Fact]
    public void TheEllipsisIsLeftOffWhenTheTextIsAllowedToOverflow()
    {
        var page = PageShowing(ThreeLinesish, TwoLinesDeep, f =>
        {
            f.Ellipsis = "...";
            f.AllowVerticalOverflow = true;
        });

        // Nothing was cut off, so there is nothing to stand in for.
        TextOperators.ShownStrings(page).Should().NotContain(text => text.EndsWith("..."));
    }

    [Fact]
    public void TheEllipsisStaysInsideTheRectangle()
    {
        var page = PageShowing(ThreeLinesish, TwoLinesDeep, f => f.Ellipsis = "...");

        // The word it is put on is trimmed until the two together fit, so the marked line is no
        // wider than the one it replaced would have been.
        var lastLine = TextOperators.ShownStrings(page)[^1];

        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        gfx.MeasureString(lastLine, Font).Width.Should().BeLessThanOrEqualTo(TwoLinesDeep.Width + 0.5);
    }

    [Fact]
    public void CuttingTextOffWithoutAnEllipsisStillWorks()
    {
        var marked = LinesOf(PageShowing(ThreeLinesish, TwoLinesDeep, f => f.Ellipsis = "..."));
        var plain = LinesOf(PageShowing(ThreeLinesish, TwoLinesDeep));

        // The ellipsis marks the cut; it does not move it.
        marked.Should().HaveCount(plain.Count);
    }

    // ----- C6, columns ---------------------------------------------------------------------------

    /// <summary>Wide enough for two columns, deep enough for two lines in each.</summary>
    static XRect TwoColumnsDeep => new XRect(20, 20, 260, 2 * LineHeight);

    [Fact]
    public void TextFlowsDownOneColumnAndOnIntoTheNext()
    {
        var page = PageShowing(ThreeLinesish, TwoColumnsDeep, f =>
        {
            f.Columns = 2;
            f.ColumnGap = 20;
        });

        var runs = RunsOf(page);
        runs.Count.Should().BeGreaterThan(2);

        // The column is (260 - 20) / 2 = 120 wide, so the second column starts 140 points in.
        var left = runs.Select(run => Math.Round(run.X, 3)).Distinct().OrderBy(x => x).ToList();
        left.Should().HaveCount(2);
        (left[1] - left[0]).Should().BeApproximately(140, 0.01);
    }

    [Fact]
    public void ALineInTheSecondColumnSitsLevelWithOneInTheFirst()
    {
        var page = PageShowing(ThreeLinesish, TwoColumnsDeep, f =>
        {
            f.Columns = 2;
            f.ColumnGap = 20;
        });

        var runs = RunsOf(page);

        // Each column starts again at the top, so the same heights are used twice over. The lines
        // must still be drawn as separate runs rather than joined into one by their height.
        var heights = runs.Select(run => Math.Round(run.Y, 3)).ToList();
        heights.Distinct().Count().Should().BeLessThan(heights.Count);
    }

    [Fact]
    public void ANarrowerColumnWrapsSoonerThanTheWholeRectangleWould()
    {
        var oneColumn = LinesOf(PageShowing(ThreeLinesish, new XRect(20, 20, 260, 400))).Count;
        var twoColumns = RunsOf(PageShowing(ThreeLinesish, new XRect(20, 20, 260, 400), f =>
        {
            f.Columns = 2;
            f.ColumnGap = 20;
        })).Count;

        twoColumns.Should().BeGreaterThan(oneColumn);
    }

    [Fact]
    public void TheColumnGapIsRoomTakenOffTheColumns()
    {
        var page = PageShowing(ThreeLinesish, TwoColumnsDeep, f =>
        {
            f.Columns = 2;
            f.ColumnGap = 60;
        });

        // (260 - 60) / 2 = 100 wide, so the second column starts 160 points in.
        var left = RunsOf(page).Select(run => Math.Round(run.X, 3)).Distinct().OrderBy(x => x).ToList();
        left.Should().HaveCount(2);
        (left[1] - left[0]).Should().BeApproximately(160, 0.01);
    }

    [Fact]
    public void TextRunsOutOfRoomOnlyWhenTheLastColumnIsFull()
    {
        var oneColumn = RunsOf(PageShowing(ThreeLinesish, TwoColumnsDeep)).Count;
        var threeColumns = RunsOf(PageShowing(ThreeLinesish, TwoColumnsDeep, f =>
        {
            f.Columns = 3;
            f.ColumnGap = 10;
        })).Count;

        // More columns, more room, so more of the text is placed before it is cut off.
        threeColumns.Should().BeGreaterThan(oneColumn);
    }

    [Fact]
    public void OneColumnIsWhatItAlwaysWas()
    {
        var implicitly1 = RunsOf(PageShowing(ThreeLinesish, Narrow));
        var explicitly1 = RunsOf(PageShowing(ThreeLinesish, Narrow, f => f.Columns = 1));

        explicitly1.Should().Equal(implicitly1);
    }

    [Fact]
    public void FewerThanOneColumnIsRejected()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        var formatter = new XTextFormatter(gfx);

        formatter.Invoking(f => f.Columns = 0).Should().Throw<ArgumentOutOfRangeException>();
        formatter.Invoking(f => f.Columns = -2).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TheEllipsisLandsInTheLastColumn()
    {
        // Long enough to fill both columns and still have text left over: two columns two lines
        // deep hold four lines, and the fox and the dog between them only need four.
        const string tooMuch = ThreeLinesish + " and then a good deal more besides, enough to fill "
                                             + "both of the columns and still be cut off at the end";

        var page = PageShowing(tooMuch, TwoColumnsDeep, f =>
        {
            f.Columns = 2;
            f.ColumnGap = 20;
            f.Ellipsis = "...";
        });

        var runs = RunsOf(page);
        var shown = TextOperators.ShownStrings(page);

        runs.Should().NotBeEmpty();
        shown.Should().NotBeEmpty();

        // Cut off at the bottom of the second column, not the first.
        shown[shown.Count - 1].Should().EndWith("...");
        runs[runs.Count - 1].X.Should().BeGreaterThan(runs[0].X);
    }

    // ----- the options leave the ordinary case alone ---------------------------------------------

    [Fact]
    public void NoneOfTheDefaultsChangeWhereALineSits()
    {
        var bare = RunsOf(PageShowing(ThreeLinesish, Narrow));
        var withDefaults = RunsOf(PageShowing(ThreeLinesish, Narrow, f =>
        {
            f.LineBreak = true;
            f.Indent = 0;
            f.IndentAllLines = false;
            f.LineGap = 0;
            f.ParagraphGap = 0;
            f.Ellipsis = null;
            f.Rotation = 0;
        }));

        withDefaults.Should().Equal(bare);
    }

    [Fact]
    public void AlignmentStillWorksWithAnIndent()
    {
        var left = RunsOf(PageShowing(ThreeLinesish, Narrow, f => f.Indent = 20));
        var right = RunsOf(PageShowing(ThreeLinesish, Narrow, f =>
        {
            f.Indent = 20;
            f.Alignment = XParagraphAlignment.Right;
        }));

        // An indent is off the left edge, so right-aligned text does not move with it - but the
        // two must not come out in the same place either, or the alignment was lost.
        right[0].X.Should().NotBe(left[0].X);
    }
}
