using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   Text laid out around things the caller has put in the block.
/// </summary>
/// <remarks>
///   <para>
///     The geometry itself is tested in <c>FlowGeometryTests</c> and <c>TextFlowRegionTests</c> with
///     no page in sight. What is tested here is the wiring: that the formatter asks the right band,
///     clips to the right column, and turns the answer back into a line in the right place.
///   </para>
///   <para>
///     The block is 300 wide at x=40 throughout, so a line running its full measure begins at 40 and
///     a line beside something standing at the left begins further in.
///   </para>
///   <para>
///     In the glyph-outline collection because the drop cap tests share the provider and some of
///     them take it away.
///   </para>
/// </remarks>
[Collection(GlyphOutlineCollection.Name)]
public class FormatterObstacleTests
{
    private const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be, which takes rather longer than the jump did and is far less " +
        "impressive to watch from any distance at all, or indeed from close to, where the whole " +
        "business looks distinctly laboured and not at all the effortless bound the saying has " +
        "always promised its readers it would turn out to be on closer inspection.";

    private static readonly XRect Block = new(40, 40, 300, 300);

    /// <summary>
    ///   Short enough that the text runs past the bottom of the first column and into the second.
    ///   The full-height block holds all of it in one column, which makes a test of the second
    ///   column pass by finding nothing there.
    /// </summary>
    private static readonly XRect TwoColumnBlock = new(40, 40, 300, 120);

    /// <summary>Two columns across 300 with the default 18 gutter: 141 each, the second 159 in.</summary>
    private const double SecondColumnLeft = 159;

    private static List<(double X, double Y)> SecondColumnOf(IEnumerable<(double X, double Y)> lines)
    {
        return [..lines.Where(line => line.X > Block.X + SecondColumnLeft - 1)];
    }

    private static PdfPage Render(string text = Prose, XRect? area = null,
        Action<XTextFormatter> arrange = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        var formatter = new XTextFormatter(gfx);
        arrange?.Invoke(formatter);
        formatter.DrawString(text, new XFont("Arial", 10), XBrushes.Black, area ?? Block);
        return page;
    }

    /// <summary>Where each line begins and how far down it sits, top of the page first.</summary>
    private static List<(double X, double Y)> LinesOf(PdfPage page, int skip = 0)
    {
        return [..TextBaselines.PositionsOf(page)
            .Skip(skip)
            .Select(point => (point.X, point.Y))
            .OrderByDescending(line => line.Y)];
    }

    /// <summary>The glyph a space is in the face the tests are pinned to.</summary>
    private const int SpaceGlyph = 3;

    /// <summary>
    ///   Every glyph the page shows, in order, with the spaces taken out.
    /// </summary>
    /// <remarks>
    ///   Without them, because the formatter draws a line at a time and a line break swallows the
    ///   space it broke at. Two different breakings of one text therefore hold different numbers of
    ///   spaces, and comparing the words is the only way to ask whether the same text was set.
    /// </remarks>
    private static List<int> GlyphsOn(PdfPage page)
    {
        var glyphs = new List<int>();
        foreach (var run in TextOperators.ShownStrings(page))
        {
            for (var idx = 0; idx + 1 < run.Length; idx += 2)
            {
                var glyph = (run[idx] << 8) | run[idx + 1];
                if (glyph != SpaceGlyph)
                    glyphs.Add(glyph);
            }
        }
        return glyphs;
    }

    /// <summary>
    ///   How many glyphs the given run carries. The formatter draws a line at a time, so run 0 of a
    ///   page with a drop cap is the cap and run 1 is the first line of body text.
    /// </summary>
    private static int GlyphsOnLine(PdfPage page, int index)
    {
        // Two bytes per glyph: the fonts are embedded as Identity-H.
        return TextOperators.ShownStrings(page)[index].Length / 2;
    }

    /// <summary>An obstacle standing in the top of the block, in the block's own coordinates.</summary>
    private static RectangleObstacle Standing(double x, double width, double padding = 0)
    {
        return new RectangleObstacle(new XRect(x, 0, width, 60), padding);
    }

    // ----- nothing supplied -----------------------------------------------------------------------

    [Fact]
    public void ABlockWithNothingInItIsLaidOutExactlyAsBefore()
    {
        // The compatibility claim of the whole feature. FormatterLayoutPinTests pins it across
        // seventeen arrangements; this says it once more in terms of an empty obstacle list, which
        // is the case that would break if the region were built when it is not needed.
        var plain = LinesOf(Render());
        var withEmptyList = LinesOf(Render(arrange: f => f.Obstacles.Clear()));

        withEmptyList.Should().Equal(plain);
    }

    // ----- one obstacle, from each direction ------------------------------------------------------

    [Fact]
    public void LinesBesideAnObstacleAtTheLeftBeginToTheRightOfIt()
    {
        var lines = LinesOf(Render(arrange: f => f.Obstacles.Add(Standing(0, 80))));

        lines.Take(3).Should().OnlyContain(line => line.X > Block.X + 79,
            "a line standing beside it clears it");
        lines.Last().X.Should().BeApproximately(Block.X, 0.01,
            "and a line below it is back at the margin");
    }

    [Fact]
    public void AnObstacleAtTheRightShortensTheLinesItStandsBeside()
    {
        var plain = Render();
        var narrowed = Render(arrange: f => f.Obstacles.Add(Standing(220, 80)));

        // Same text, broken sooner: more lines are needed for it, and a block of a fixed height
        // therefore holds slightly less of it. What is set has to be the start of the text, in
        // order - text going missing in the middle is the failure worth catching.
        var shortened = GlyphsOn(narrowed);
        var full = GlyphsOn(plain);

        LinesOf(narrowed).Count.Should().BeGreaterThan(LinesOf(plain).Count);
        full.Take(shortened.Count).Should().Equal(shortened, "the same words, only broken differently");
    }

    [Fact]
    public void TextTakesTheRoomierSideOfAnObstacleStandingInTheMiddle()
    {
        // 100 wide starting 60 in: 60 free to its left, 140 to its right. The right wins.
        var lines = LinesOf(Render(arrange: f => f.Obstacles.Add(Standing(60, 100))));

        lines[0].X.Should().BeApproximately(Block.X + 160, 0.01);
    }

    // ----- no room at all -------------------------------------------------------------------------

    [Fact]
    public void TextWithNoRoomBesideAnObstacleBeginsBelowIt()
    {
        // Measured against the same text with nothing in the way, because the page counts y upwards
        // and the block counts it downwards, and comparing the two renders sidesteps the conversion
        // entirely: further down the page is a smaller number, by however deep the obstacle is.
        var unobstructed = LinesOf(Render());
        var lines = LinesOf(Render(arrange: f => f.Obstacles.Add(Standing(0, 300))));

        lines.Should().NotBeEmpty("the text is drawn, not dropped");
        lines.Should().OnlyContain(line => line.X < Block.X + 1, "at the margin, below the obstacle");
        (unobstructed[0].Y - lines[0].Y).Should().BeGreaterThanOrEqualTo(60,
            "the first line has moved down past the whole depth of the obstacle");
    }

    [Fact]
    public void ALineMovesPastTheNearerObstacleAndNoFurtherThanItHasTo()
    {
        // Neither of these covers the block alone; together they leave the top of it with nothing.
        // The shallow one ends at 40 and the deep one goes on to 80 — and below 40 there is room to
        // the left of the deep one, so that is where the text goes.
        //
        // This is the reason the move is to the *nearest* foot rather than the furthest. Moving to
        // the deepest would have stepped over every band between 40 and 80, all of which have room.
        var unobstructed = LinesOf(Render());
        var lines = LinesOf(Render(arrange: f =>
        {
            f.Obstacles.Add(new RectangleObstacle(new XRect(0, 0, 200, 40)));
            f.Obstacles.Add(new RectangleObstacle(new XRect(180, 0, 120, 80)));
        }));

        lines.Should().NotBeEmpty();

        var moved = unobstructed[0].Y - lines[0].Y;
        moved.Should().BeGreaterThanOrEqualTo(40, "it clears the nearer obstacle");
        moved.Should().BeLessThan(80, "and stops there, rather than stepping over usable bands");

        lines[0].X.Should().BeApproximately(Block.X, 0.01,
            "and the line it lands on runs from the margin, beside the deeper obstacle");
    }

    // ----- padding --------------------------------------------------------------------------------

    [Fact]
    public void PaddingHoldsTheTextOffTheObstacle()
    {
        var touching = LinesOf(Render(arrange: f => f.Obstacles.Add(Standing(0, 80))));
        var heldOff = LinesOf(Render(arrange: f => f.Obstacles.Add(Standing(0, 80, padding: 12))));

        heldOff[0].X.Should().BeApproximately(touching[0].X + 12, 0.01);
    }

    // ----- columns --------------------------------------------------------------------------------

    [Fact]
    public void AnObstacleStandingInOneColumnLeavesTheOtherAlone()
    {
        // An obstacle 100 wide at the left is wholly inside the first column.
        var lines = LinesOf(Render(area: TwoColumnBlock, arrange: f =>
        {
            f.Columns = 2;
            f.Obstacles.Add(Standing(0, 100));
        }));

        SecondColumnOf(lines).Should().NotBeEmpty("the second column is used")
            .And.OnlyContain(line => Math.Abs(line.X - (Block.X + SecondColumnLeft)) < 0.01,
                "and every line of it begins at its own left edge, untouched by the obstacle");
    }

    [Fact]
    public void AnObstacleStraddlingTheGutterNarrowsBothColumns()
    {
        var lines = LinesOf(Render(area: TwoColumnBlock, arrange: f =>
        {
            f.Columns = 2;
            // From 120 to 200: into the right of the first column and the left of the second.
            f.Obstacles.Add(Standing(120, 80));
        }));

        var second = SecondColumnOf(lines);

        second.Should().NotBeEmpty();
        second[0].X.Should().BeApproximately(Block.X + 200, 0.01,
            "the top of the second column begins past the part of the obstacle standing in it");
        second.Last().X.Should().BeApproximately(Block.X + SecondColumnLeft, 0.01,
            "and its lower lines, clear of the obstacle, begin at its left edge");
    }

    // ----- with a drop cap ------------------------------------------------------------------------

    [Fact]
    public void ACapAndAnObstacleNarrowTheSameLineTogether()
    {
        // Both reservations have to land on one line, so the test has to fail if either is
        // missing. The cap is checked by where the line starts and the obstacle by how much fits
        // on it — asserting only the first would pass on a formatter that ignored the second.
        var capOnly = Render(arrange: f => f.DropCap = new XDropCap(new XFont("Arial", 12), 3));
        var both = Render(arrange: f =>
        {
            f.DropCap = new XDropCap(new XFont("Arial", 12), 3);
            f.Obstacles.Add(Standing(240, 60));
        });

        // The cap is the first run drawn on either page; the body lines follow it.
        LinesOf(both, skip: 1)[0].X.Should().BeGreaterThan(Block.X + 10, "the cap moved it right");

        GlyphsOnLine(both, 1).Should().BeLessThan(GlyphsOnLine(capOnly, 1),
            "and the obstacle took the right-hand end of the same line");
    }

    [Fact]
    public void ACapStillNarrowsOnlyTheFirstColumn()
    {
        // The rule used to be a test on the column index and is now a consequence of where the cap
        // stands. It has to survive the change of mechanism.
        var lines = LinesOf(Render(area: TwoColumnBlock, arrange: f =>
        {
            f.Columns = 2;
            f.DropCap = new XDropCap(new XFont("Arial", 12), 3);
        }), skip: 1);

        SecondColumnOf(lines).Should().NotBeEmpty()
            .And.OnlyContain(line => Math.Abs(line.X - (Block.X + SecondColumnLeft)) < 0.01,
                "every line of the second column begins at its own left edge");
    }

    // ----- truncation -----------------------------------------------------------------------------

    [Fact]
    public void AnEllipsisOnANarrowedLastLineStaysInsideTheLine()
    {
        // Shallow enough that the text runs out while still beside the obstacle, so the last line
        // is a narrowed one. Measured against that line's own limit, not the column's.
        var shallow = new XRect(40, 40, 300, 46);
        var page = Render(area: shallow, arrange: f =>
        {
            f.Ellipsis = XTextFormatter.DefaultEllipsis;
            f.Obstacles.Add(new RectangleObstacle(new XRect(200, 0, 100, 60)));
        });

        var lines = LinesOf(page);

        lines.Should().NotBeEmpty("the text was truncated, not dropped");
        lines.Should().OnlyContain(line => line.X < shallow.X + 200 + 0.01);
    }

    // ----- rotation -------------------------------------------------------------------------------

    [Fact]
    public void AnObstacleSuppliedWhileTheTextIsRotatedIsRefused()
    {
        // The two readings - the turned frame and the page frame - put text in visibly different
        // places, and nothing in the call says which was meant.
        var draw = () => Render(arrange: f =>
        {
            f.Rotation = 30;
            f.Obstacles.Add(Standing(0, 80));
        });

        draw.Should().Throw<InvalidOperationException>()
            .WithMessage("*Rotation*");
    }

    [Fact]
    public void AGapInTheObstacleListIsRefused()
    {
        // Skipping it instead would drop an obstacle without saying so, and the page would look
        // deliberate with text running over the thing it was given to avoid. Refused whether or
        // not the text is rotated, so the rotation check below can trust the count it reads.
        var draw = () => Render(arrange: f => f.Obstacles.Add(null));

        draw.Should().Throw<InvalidOperationException>().WithMessage("*Obstacles[0]*");
    }

    [Fact]
    public void RotationWithNoObstacleIsUntouched()
    {
        var draw = () => Render(arrange: f => f.Rotation = 30);

        draw.Should().NotThrow();
    }

    [Fact]
    public void ADropCapUnderRotationIsNotRefused()
    {
        // The cap is an obstacle the formatter makes for itself, in the frame it lays out in. It
        // has always worked with rotation and has to go on working.
        var draw = () => Render(arrange: f =>
        {
            f.Rotation = 30;
            f.DropCap = new XDropCap(new XFont("Arial", 12), 3);
        });

        draw.Should().NotThrow();
    }
}
