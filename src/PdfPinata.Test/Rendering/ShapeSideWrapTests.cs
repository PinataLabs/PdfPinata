using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   Text flowing beside a shape rather than above and below it.
/// </summary>
/// <remarks>
///   <para>
///     The failures here are quiet ones. A wrap that reserves the wrong room still puts every word
///     on the page; a wrap on the wrong side produces something that looks deliberate and is
///     backwards; a line drawn across the shape looks like a shape drawn over a line. So these read
///     the drawn positions rather than trusting that a page came out at all.
///   </para>
///   <para>
///     Two things about reading them. PinataLayout draws <b>one run per word</b>, so a line is every run
///     sharing a baseline and its left edge is the leftmost of them. And the shape's box is read off
///     the page from the rectangle its border draws, rather than worked out from the page setup — so
///     every number compared against here comes from where a reader's eye would take it.
///   </para>
/// </remarks>
public class ShapeSideWrapTests
{
    const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be, which takes rather longer than the jump did and is far less " +
        "impressive to watch from any distance at all, or indeed from close to, where the whole " +
        "business looks distinctly laboured and not at all the effortless bound the saying has " +
        "always promised its readers it would turn out to be on any closer inspection of it.";

    // ----- the room the shape reserves ------------------------------------------------------------

    [Fact]
    public void TextRunsBesideAWrappedShapeRatherThanBelowIt()
    {
        // Text on the left, so the shape goes on the right. Asking for the text on the side the
        // shape already occupies leaves it nowhere to go, which is a different case entirely.
        var beside = LaidOut(WrapStyle.Left, ShapePosition.Right);
        var below = LaidOut(WrapStyle.TopBottom, ShapePosition.Right);

        // A shape placed between its neighbours pushes what follows it down the page; one the text
        // wraps around does not.
        beside.Lines.First().Y.Should().BeGreaterThan(below.Lines.First().Y);
        beside.LevelWithTheShape.Should().NotBeEmpty();
        below.LevelWithTheShape.Should().BeEmpty();
    }

    [Fact]
    public void TheLinesLevelWithTheShapeAreShortenedAndTheOthersAreNot()
    {
        var laid = LaidOut(WrapStyle.Right);

        laid.LevelWithTheShape.Should().NotBeEmpty("some lines stand against the shape");
        laid.Lines.Count.Should().BeGreaterThan(laid.LevelWithTheShape.Count, "and some do not");

        var below = laid.Lines.Where(line => line.Y < laid.ShapeBottom).ToList();
        below.Should().NotBeEmpty();
        below.Should().OnlyContain(line => Math.Abs(line.X - LeftMargin) < 1,
            "the lines below it run the full measure");
    }

    [Fact]
    public void NoLineIsDrawnAcrossTheShape()
    {
        var laid = LaidOut(WrapStyle.Right, shapeWidth: "6cm");

        foreach (var line in laid.LevelWithTheShape)
        {
            line.X.Should().BeGreaterThanOrEqualTo(laid.ShapeRight - 1,
                "a line level with the shape begins clear of it, not across it");
        }
    }

    [Fact]
    public void NothingIsLostToTheWrapAndNothingIsRepeated()
    {
        var wrapped = GlyphsAcross(RenderDocument(WrapStyle.Left, ShapePosition.Right, "4cm", "4cm", 12, null));
        var plain = GlyphsAcross(RenderDocument(WrapStyle.None, ShapePosition.Right, "4cm", "4cm", 12, null));

        wrapped.Should().Equal(plain, "every word appears exactly once, in the order it was given");
    }

    // ----- which side ------------------------------------------------------------------------------

    [Fact]
    public void AskingForTheTextOnTheLeftPutsItOnTheLeft()
    {
        var laid = LaidOut(WrapStyle.Left, ShapePosition.Right);

        laid.LevelWithTheShape.Should().NotBeEmpty();
        laid.LevelWithTheShape.Should().OnlyContain(line => Math.Abs(line.X - LeftMargin) < 1,
            "the text begins at the margin and stops before the shape");
    }

    [Fact]
    public void AskingForTheTextOnTheRightPutsItOnTheRight()
    {
        var laid = LaidOut(WrapStyle.Right);

        laid.LevelWithTheShape.Should().NotBeEmpty();
        laid.LevelWithTheShape.Should().OnlyContain(line => line.X >= laid.ShapeRight - 1,
            "the text begins after the shape");
    }

    [Fact]
    public void TheTwoSidesAreNotTheSameWayRound()
    {
        // The one failure a single page cannot show: a wrap that is consistently backwards.
        var left = LaidOut(WrapStyle.Left, ShapePosition.Right).LevelWithTheShape;
        var right = LaidOut(WrapStyle.Right).LevelWithTheShape;

        left.First().X.Should().BeLessThan(right.First().X - 50);
    }

    [Fact]
    public void AskingForEitherSideFillsTheRoomierOne()
    {
        // Against the left margin, so the room is on the right.
        var laid = LaidOut(WrapStyle.Both);

        laid.LevelWithTheShape.Should().NotBeEmpty();
        laid.LevelWithTheShape.Should().OnlyContain(line => line.X >= laid.ShapeRight - 1);
    }

    [Fact]
    public void AskingForTheLargestSideIsTheSameAsAskingForEitherSide()
    {
        // Documented on the enumeration and asserted here, so that the day they part company a
        // test says so rather than a reader noticing.
        var largest = LaidOut(WrapStyle.Largest).Lines;
        var both = LaidOut(WrapStyle.Both).Lines;

        largest.Select(line => (Math.Round(line.X, 3), Math.Round(line.Y, 3)))
            .Should().Equal(both.Select(line => (Math.Round(line.X, 3), Math.Round(line.Y, 3))));
    }

    // ----- the distances ---------------------------------------------------------------------------

    [Fact]
    public void AHorizontalDistanceHoldsTheTextOffTheShape()
    {
        var tight = LaidOut(WrapStyle.Right, arrange: wrap => wrap.DistanceRight = 0);
        var held = LaidOut(WrapStyle.Right, arrange: wrap => wrap.DistanceRight = "1cm");

        (held.LevelWithTheShape.First().X - tight.LevelWithTheShape.First().X)
            .Should().BeApproximately(Centimetres(1), 0.5,
                "DistanceRight is the gap between the shape and the text beside it");
    }

    [Fact]
    public void NoDistanceAtAllLetsTheTextRunUpToTheShape()
    {
        var laid = LaidOut(WrapStyle.Right,
            arrange: wrap => { wrap.DistanceLeft = 0; wrap.DistanceRight = 0; });

        laid.LevelWithTheShape.First().X.Should().BeApproximately(laid.ShapeRight, 1.0);
    }

    [Fact]
    public void AVerticalDistancePushesTheFirstClearLineFurtherDown()
    {
        var tight = LaidOut(WrapStyle.Right, arrange: wrap => wrap.DistanceBottom = 0);
        var held = LaidOut(WrapStyle.Right, arrange: wrap => wrap.DistanceBottom = "1cm");

        // DistanceBottom grows the obstacle downwards, so a line that would have cleared the shape
        // by a hair is pushed past it instead. That is the reading these distances were given for a
        // side-wrapped shape; for a TopBottom one they remain the element's own margins.
        //
        // "Clear" means running the full measure, not merely below the shape's own box: the whole
        // point of the distance is that the lines just below the shape are still held off it.
        var tightFirstClear = tight.Lines.First(line => line.X < LeftMargin + 1).Y;
        var heldFirstClear = held.Lines.First(line => line.X < LeftMargin + 1).Y;

        heldFirstClear.Should().BeLessThan(tightFirstClear,
            "a distance below the shape holds the full-measure lines further down the page");

        // And the lines in between are beside the shape rather than missing.
        held.Lines.Should().Contain(line => line.Y < held.ShapeBottom && line.X > held.ShapeRight,
            "the lines inside the distance are still laid out, just held off");
    }

    // ----- existing styles are untouched ----------------------------------------------------------

    [Fact]
    public void AShapePlacedBetweenItsNeighboursStillPushesThemDown()
    {
        var laid = LaidOut(WrapStyle.TopBottom);

        // Nothing level with it at all: the text starts below. That is what TopBottom has always
        // meant, and the pinned corpus says the same in bytes.
        laid.LevelWithTheShape.Should().BeEmpty();
    }

    [Fact]
    public void AShapeTheTextIgnoresIsStillIgnored()
    {
        var through = LaidOut(WrapStyle.Through).Lines;
        var none = LaidOut(WrapStyle.None).Lines;

        through.Select(line => (Math.Round(line.X, 3), Math.Round(line.Y, 3)))
            .Should().Equal(none.Select(line => (Math.Round(line.X, 3), Math.Round(line.Y, 3))));
    }

    [Fact]
    public void AShapeTheTextIgnoresHasTextDrawnAcrossIt()
    {
        var laid = LaidOut(WrapStyle.Through);

        // The overlap is the point of Through, and it is what tells it apart from every side wrap.
        // A change that quietly made Through behave like a wrap would pass every other test here.
        laid.LevelWithTheShape.Should().NotBeEmpty();
        laid.LevelWithTheShape.Should().OnlyContain(line => Math.Abs(line.X - LeftMargin) < 1);
    }

    // ----- falling back rather than misplacing ----------------------------------------------------

    [Fact]
    public void AShapeTallerThanTheAreaIsPlacedBetweenItsNeighboursInstead()
    {
        // Taller than the text area, so it cannot stand in it: the obstacle would outlive the area
        // holding it, and the text after the break would be laid out around something no longer
        // there. A predictable degradation beats a wrong page.
        var render = () => Render(WrapStyle.Left, ShapePosition.Right, shapeHeight: "30cm");
        render.Should().NotThrow();

        GlyphsAcross(RenderDocument(WrapStyle.Left, ShapePosition.Right, "4cm", "30cm", 12, null))
            .Should().NotBeEmpty("the text is still laid out somewhere");
    }

    [Fact]
    public void AWrappedShapeOnADocumentThatBreaksKeepsAllOfItsText()
    {
        var wrapped = GlyphsAcross(RenderDocument(WrapStyle.Left, ShapePosition.Right, "4cm", "4cm", 40, null));
        var plain = GlyphsAcross(RenderDocument(WrapStyle.None, ShapePosition.Right, "4cm", "4cm", 40, null));

        wrapped.Should().Equal(plain, "a page break under a wrap loses nothing");
    }

    // ----- justified text beside a shape ----------------------------------------------------------

    [Fact]
    public void JustifiedTextBesideAShapeStaysInsideTheMeasure()
    {
        var document = RenderDocument(WrapStyle.Right, ShapePosition.Left, "4cm", "4cm", 12, null,
            justify: true);
        var laid = new Laid(document.Pages[0]);

        // Justification stretching a narrowed line to the column's measure rather than to its own
        // produces text that is subtly ragged rather than obviously broken - and pushes the last
        // blocks past the right margin, which is the part an assertion can see.
        var rightMargin = LeftMargin + Centimetres(16);
        TextBaselines.PositionsOf(document.Pages[0])
            .Should().OnlyContain(position => position.X <= rightMargin + 1);

        laid.LevelWithTheShape.Should().NotBeEmpty("the justified lines really are beside the shape");
    }

    // ----- building the page and reading it back ---------------------------------------------------

    static double Centimetres(double value) => value * 72 / 2.54;

    /// <summary>The text area's left edge, which is the page's left margin.</summary>
    const double LeftMargin = 2.5 * 72 / 2.54;

    static Document Build(WrapStyle style, ShapePosition position, string width, string height,
        int paragraphs, Action<WrapFormat> arrange, bool justify = false)
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.LeftMargin = "2.5cm";
        section.PageSetup.RightMargin = "2.5cm";
        section.PageSetup.TopMargin = "2.5cm";

        var frame = section.AddTextFrame();
        frame.Width = width;
        frame.Height = height;
        frame.RelativeVertical = RelativeVertical.Paragraph;
        frame.RelativeHorizontal = RelativeHorizontal.Margin;
        frame.Left = position;
        frame.WrapFormat.Style = style;

        // A visible edge, so the shape's box can be read off the page rather than worked out from
        // the page setup. The frame carries no text of its own: every run on the page is then body
        // text, and reading the frame's own words as a line was the first thing to go wrong here.
        frame.LineFormat.Width = 1;

        arrange?.Invoke(frame.WrapFormat);

        for (var idx = 0; idx < paragraphs; idx++)
        {
            var paragraph = section.AddParagraph(Prose);
            if (justify)
                paragraph.Format.Alignment = ParagraphAlignment.Justify;
        }

        return document;
    }

    static PdfDocument RenderDocument(WrapStyle style, ShapePosition shapePosition,
        string shapeWidth, string shapeHeight, int paragraphs, Action<WrapFormat> arrange,
        bool justify = false)
    {
        var document = Build(style, shapePosition, shapeWidth, shapeHeight, paragraphs, arrange, justify);

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        return renderer.PdfDocument;
    }

    static PdfPage Render(WrapStyle style, ShapePosition shapePosition = ShapePosition.Left,
        string shapeWidth = "4cm", string shapeHeight = "4cm", int paragraphs = 12,
        Action<WrapFormat> arrange = null)
    {
        return RenderDocument(style, shapePosition, shapeWidth, shapeHeight, paragraphs, arrange).Pages[0];
    }

    private static Laid LaidOut(WrapStyle style, ShapePosition shapePosition = ShapePosition.Left,
        string shapeWidth = "4cm", string shapeHeight = "4cm", Action<WrapFormat> arrange = null)
    {
        return new Laid(Render(style, shapePosition, shapeWidth, shapeHeight, 12, arrange));
    }

    /// <summary>
    ///   A rendered page, the shape's box on it, and where each line of text begins.
    /// </summary>
    sealed class Laid
    {
        internal Laid(PdfPage page)
        {
            var content = Encoding.ASCII.GetString(PageContent.Of(page));
            var re = Regex.Match(content, @"([\d.]+) ([\d.]+) ([\d.]+) ([\d.]+) re");

            re.Success.Should().BeTrue("the frame draws its border, which is how it is found");

            double Number(int group) => double.Parse(re.Groups[group].Value, CultureInfo.InvariantCulture);
            ShapeLeft = Number(1);
            ShapeBottom = Number(2);
            ShapeRight = ShapeLeft + Number(3);
            ShapeTop = ShapeBottom + Number(4);

            // PinataLayout draws one run per word, so a line is every run sharing a baseline and its
            // left edge is the leftmost of them.
            Lines = TextBaselines.PositionsOf(page)
                .GroupBy(position => Math.Round(position.Y, 3))
                .Select(line => (X: line.Min(position => position.X), Y: line.Key))
                .OrderByDescending(line => line.Y)
                .ToList();
        }

        internal double ShapeLeft { get; }
        internal double ShapeRight { get; }
        internal double ShapeTop { get; }
        internal double ShapeBottom { get; }
        internal List<(double X, double Y)> Lines { get; }

        /// <summary>The lines whose baseline falls within the shape's own depth.</summary>
        internal List<(double X, double Y)> LevelWithTheShape =>
            Lines.Where(line => line.Y > ShapeBottom && line.Y < ShapeTop).ToList();
    }

    static List<int> GlyphsAcross(PdfDocument document)
    {
        var glyphs = new List<int>();
        for (var idx = 0; idx < document.PageCount; idx++)
            glyphs.AddRange(GlyphsOn(document.Pages[idx]));
        return glyphs;
    }

    static List<int> GlyphsOn(PdfPage page)
    {
        var glyphs = new List<int>();

        foreach (var run in TextOperators.ShownStrings(page))
        {
            // Identity-H writes two bytes per glyph rather than one byte per character, so a run
            // has to be read a pair at a time. Reading it a byte at a time shifts everything by
            // half a glyph and produces two sequences that differ everywhere.
            //
            // Nothing is filtered out. MigraDoc draws one run per word and puts the spaces between
            // them in the positioning, so no whitespace glyph is ever shown - which is also why
            // comparing the sequences works at all across two different line breakings.
            for (var idx = 0; idx + 1 < run.Length; idx += 2)
                glyphs.Add((run[idx] << 8) | run[idx + 1]);
        }

        return glyphs;
    }
}
