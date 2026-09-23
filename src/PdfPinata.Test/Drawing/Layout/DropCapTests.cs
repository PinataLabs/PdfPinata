using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   An initial letter set into the opening lines of a block, and the room those lines leave for
///   it.
/// </summary>
/// <remarks>
///   <para>
///     Doing this by hand meant drawing the letter separately and then adding one word at a time to
///     a probe rectangle until the answer stopped fitting — thirty lines that re-measure the text
///     once per word and go wrong whenever the cap's depth is not a whole multiple of the line
///     height.
///   </para>
///   <para>
///     Assertions are made on glyph codes rather than on characters. The fonts are embedded as
///     Identity-H, so what a content stream carries is glyph indices into the face; the test
///     resolver pins that face, which makes the codes stable and comparable between two renders of
///     the same text.
///   </para>
///   <para>
///     In the glyph-outline collection because the cap is placed by its ink where a provider is
///     registered, and the unregistered case is one of the things tested — which means clearing the
///     provider and putting it back.
///   </para>
/// </remarks>
[Collection(GlyphOutlineCollection.Name)]
public class DropCapTests
{
    private const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be, which takes rather longer than the jump did and is far less " +
        "impressive to watch from any distance at all, or indeed from close to, where the whole " +
        "business looks distinctly laboured and not at all the effortless bound the saying has " +
        "always promised its readers it would turn out to be on closer inspection.";

    private static readonly XRect Area = new(40, 40, 300, 300);

    /// <summary>The glyph a space is in the face the tests are pinned to.</summary>
    private const int SpaceGlyph = 3;

    /// <summary>The face and size the body text is set in unless a test says otherwise.</summary>
    private const string BodyFamily = "Arial";

    private const double BodySize = 10;

    /// <summary>
    ///   How far a placement may be out before the eye would call it misaligned. Wide enough to
    ///   absorb the rounding the content stream writes numbers with, narrow enough that the two
    ///   points a cap hung from the ascent stands clear by would fail it many times over.
    /// </summary>
    private const double Tolerance = 0.1;

    // ----- the room the cap reserves --------------------------------------------------------------

    [Fact]
    public void TheOpeningLinesBeginToTheRightOfTheCapAndTheRestAtTheMargin()
    {
        var withCap = BodyLinesOf(Prose, cap: 3);
        var margin = BodyLinesOf(Prose, cap: 0)[0].X;

        withCap.Count.Should().BeGreaterThan(3, "there must be lines past the cap to compare");

        foreach (var line in withCap.Take(3))
            line.X.Should().BeGreaterThan(margin + 10, "a line beside the cap clears it");

        withCap[3].X.Should().BeApproximately(margin, 0.01, "the fourth line is back at the margin");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void ExactlyAsManyLinesAreNarrowedAsTheCapIsDeep(int depth)
    {
        var withCap = BodyLinesOf(Prose, cap: depth);
        var margin = BodyLinesOf(Prose, cap: 0)[0].X;

        withCap.TakeWhile(line => line.X > margin + 0.01).Should().HaveCount(depth);
    }

    [Fact]
    public void NothingIsLostToTheCapAndNothingIsRepeated()
    {
        // The cap takes the first character; everything after it is laid out in full and in order.
        var withCap = GlyphsOn(Render(Prose, cap: 3));
        var without = GlyphsOn(Render(Prose, cap: 0));

        withCap.Should().Equal(without, "the cap is the first glyph and the rest follow unchanged");
    }

    [Fact]
    public void ANarrowedLineCarriesFewerWordsThanAFullOne()
    {
        var withCap = BodyLinesOf(Prose, cap: 3);
        var without = BodyLinesOf(Prose, cap: 0);

        withCap[0].Glyphs.Should().BeLessThan(without[0].Glyphs,
            "the first line is shorter for having the cap beside it");
    }

    // ----- the cap itself --------------------------------------------------------------------------

    [Fact]
    public void TheFirstCharacterIsDrawnOnceAtTheCapsSizeAndNotAgainAtBodySize()
    {
        var page = Render(Prose, cap: 3);

        var runs = TextOperators.ShownStrings(page);
        GlyphCountOf(runs[0]).Should().Be(1, "the cap is one glyph drawn on its own");

        var sizes = FontSizesOn(page);
        sizes[0].Should().BeGreaterThan(sizes[1] * 2, "the cap is set far larger than the body");
    }

    [Fact]
    public void TheCapRestsOnTheBaselineOfTheLastLineItIsSetInto()
    {
        var page = Render(Prose, cap: 3);
        var positions = TextBaselines.PositionsOf(page);

        var cap = positions[0].Y;
        var bodyBaselines = positions.Skip(1).Select(p => Math.Round(p.Y, 3)).Distinct()
            .OrderByDescending(y => y).ToList();

        cap.Should().BeApproximately(bodyBaselines[2], 0.01, "the cap's foot is on the third line");
    }

    // ----- where the cap sits against the text ------------------------------------------------------

    [Theory]
    [InlineData("T", 2)]
    [InlineData("T", 3)]
    [InlineData("H", 2)]
    [InlineData("H", 3)]
    [InlineData("A", 3)]
    [InlineData("M", 3)]
    [InlineData("M", 4)]
    public void TheCapsHeadIsLevelWithTheHeadOfTheTextAndItsFootWithTheLastLinesBaseline(
        string initial, int depth)
    {
        var cap = GeometryOf(ProseBeginningWith(initial), depth, initial);

        cap.InkTop.Should().BeApproximately(cap.TextTop, Tolerance,
            "the top of the cap's ink is level with the top of the letter standing beside it");
        cap.Foot.Should().BeApproximately(cap.LastSpannedBaseline, Tolerance,
            "the foot of the cap rests on the baseline of the last line it is set into");
    }

    [Fact]
    public void TheCapIsHungFromTheHeadOfTheTextRatherThanTheTopOfTheLineBox()
    {
        // The regression this pins. A line's box reaches an ascent above its baseline and the
        // letters in it reach only a cap height, so a cap hung from the box stands clear of the
        // text it is set into by the difference - two points here, at the size of a body letter,
        // and the cap is drawn four times that size.
        var cap = GeometryOf(Prose, 3);

        cap.InkTop.Should().BeLessThan(cap.FirstLineBoxTop - 1.5,
            "the cap does not reach the top of the first line's box");
        (cap.FirstLineBoxTop - cap.TextTop).Should().BeGreaterThan(1.5,
            "and the two are far enough apart in this face for the distinction to be worth making");
    }

    [Theory]
    [InlineData(BodyFamily, BodyFamily)]
    [InlineData(PinnedFontResolver.CffFamilyName, PinnedFontResolver.CffFamilyName)]
    [InlineData(BodyFamily, PinnedFontResolver.CffFamilyName)]
    [InlineData(PinnedFontResolver.CffFamilyName, BodyFamily)]
    public void TheCapIsHungFromTheBodysCapHeightWhicheverFaceEitherIsSetIn(string body, string capFamily)
    {
        // Two faces whose ascent stands a different distance above their capitals - a fifth of the
        // body size in one and a third in the other - and the cap set in each of them against a
        // body set in each of them. What decides where the head of the cap goes is the *body's*
        // cap height; what has to reach it is the ink of the *cap's* glyph. Setting them in
        // different faces is what tells the two apart.
        var cap = GeometryOf(ProseBeginningWith("T"), 3, "T", body, capFamily);

        cap.InkTop.Should().BeApproximately(cap.TextTop, Tolerance);
        cap.Foot.Should().BeApproximately(cap.LastSpannedBaseline, Tolerance);
    }

    [Theory]
    [InlineData(BodyFamily)]
    [InlineData(PinnedFontResolver.CffFamilyName)]
    public void TheFacesTestedAgainstEachHaveAnAscentWellAboveTheirCapitals(string family)
    {
        // Without this the alignment tests above would pass just as well on a face whose cap
        // height is its ascent, where hanging the cap from either gives the same answer and
        // nothing has been tested.
        var font = new XFont(family, BodySize);

        (AscentOf(font) - CapHeightOf(font)).Should().BeGreaterThan(1.5,
            "the distinction only shows in a face that keeps room above its capitals");
    }

    [Fact]
    public void ADeeperCapIsSetLargerAndReservesMoreRoom()
    {
        var two = Render(Prose, cap: 2);
        var three = Render(Prose, cap: 3);

        FontSizesOn(three)[0].Should().BeGreaterThan(FontSizesOn(two)[0]);
        BodyLinesOf(Prose, cap: 3)[0].X.Should().BeGreaterThan(BodyLinesOf(Prose, cap: 2)[0].X);
    }

    [Fact]
    public void TheCapsInkStartsAtTheBlocksLeftEdgeRatherThanItsPen()
    {
        var page = Render(Prose, cap: 3);
        var capPen = TextBaselines.PositionsOf(page)[0].X;

        // Set flush by ink, so the pen sits a little to the left of the margin by the glyph's own
        // left side bearing. Set flush by pen it would sit exactly on the margin and look indented.
        capPen.Should().BeLessThan(Area.X);
        capPen.Should().BeGreaterThan(Area.X - 12, "the bearing is small, not a whole letter");
    }

    [Fact]
    public void AWiderGutterPushesTheTextFurtherFromTheCap()
    {
        var tight = BodyLinesOf(Prose, cap: 3, arrange: f => f.DropCap.Gutter = 0)[0].X;
        var loose = BodyLinesOf(Prose, cap: 3, arrange: f => f.DropCap.Gutter = 20)[0].X;

        (loose - tight).Should().BeApproximately(20, 0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(20)]
    public void TheRoomLeftBetweenTheCapsInkAndTheTextIsTheGutterAndNothingElse(double gutter)
    {
        // The horizontal half of the placement, pinned beside the vertical one: resizing the cap
        // moves its right edge, and the gap has to follow it rather than stay where the old size
        // left it.
        var page = Render(ProseBeginningWith("T"), 3, arrange: f => f.DropCap.Gutter = gutter);
        var positions = TextBaselines.PositionsOf(page);
        var capFont = new XFont(BodyFamily, FontSizesOn(page)[0]);

        var inkRight = positions[0].X + InkOf("T", capFont).Right;

        (positions[1].X - inkRight).Should().BeApproximately(gutter, Tolerance,
            "the text begins a gutter's width past the right edge of the cap's ink");
    }

    // ----- justification and truncation against the narrowed measure --------------------------------

    [Theory]
    [InlineData(XParagraphAlignment.Right)]
    [InlineData(XParagraphAlignment.Justify)]
    public void AnAlignedBlockBesideTheCapStillReachesTheColumnsRightEdge(XParagraphAlignment alignment)
    {
        // The laid-out bounds are the one place a line's real right edge is observable: they are
        // built from each block's position plus its measured width. Drawn positions alone are pen
        // positions, which for aligned text are the edge less a width the test cannot see.
        var withCap = LayoutOf(Prose, cap: 3, f => f.Alignment = alignment);
        var without = LayoutOf(Prose, cap: 0, f => f.Alignment = alignment);

        // The cap narrows its lines on the *left*. Where those lines end does not move, so text
        // aligned to the right edge reaches the same place with the cap as without it.
        (withCap.X + withCap.Width).Should().BeApproximately(Area.Width, 0.01);
        (without.X + without.Width).Should().BeApproximately(Area.Width, 0.01);
    }

    [Fact]
    public void NoJustifiedLineBesideTheCapRunsPastTheColumnsRightEdge()
    {
        var page = Render(Prose, cap: 3, arrange: f => f.Alignment = XParagraphAlignment.Justify);

        // A justified line is drawn one block at a time with room opened between them, so every
        // block's pen must still land inside the measure. Spacing a narrowed line to a measure it
        // does not have pushes the last blocks past the edge.
        var starts = TextBaselines.PositionsOf(page).Skip(1).Select(p => p.X).ToList();

        starts.Should().OnlyContain(x => x <= Area.X + Area.Width + 0.01);
        starts.Where(x => x > Area.X + 10).Should().NotBeEmpty("some blocks sit beside the cap");
    }

    [Fact]
    public void ATruncatedLineBesideTheCapKeepsItsEllipsisInsideTheColumn()
    {
        // Shallow enough that the text runs out of room while still beside the cap.
        var shallow = new XRect(40, 40, 300, 40);
        var page = Render(Prose, cap: 3, area: shallow,
            arrange: f => f.Ellipsis = XTextFormatter.DefaultEllipsis);

        var starts = TextBaselines.PositionsOf(page).Skip(1).Select(p => p.X).ToList();

        starts.Should().NotBeEmpty("the text was truncated, not dropped entirely");
        starts.Should().OnlyContain(x => x < shallow.X + shallow.Width);
    }

    // ----- a cap with no room beside it -----------------------------------------------------------

    /// <summary>
    ///   A rectangle narrower than the cap set into it. The cap is scaled to its own depth and
    ///   nothing holds its width to the measure, so a deep cap in a narrow column can leave the
    ///   lines beside it no room at all.
    /// </summary>
    private static readonly XRect NarrowerThanTheCap = new(40, 40, 30, 300);

    [Fact]
    public void TextIsKeptInsideAColumnTooNarrowToSetAnythingBesideTheCap()
    {
        // Before this was fixed the words went to the right of the column - one to a line, for as
        // many lines as the cap was deep - because a line that starts past its own right limit
        // reads to the layout loop as a narrow line rather than as no line, and the first block of
        // a line is placed whether it fits or not.
        var page = Render(Prose, cap: 5, area: NarrowerThanTheCap);

        var starts = TextBaselines.PositionsOf(page).Skip(1).Select(p => p.X).ToList();

        starts.Should().NotBeEmpty("the text is drawn, not dropped");
        starts.Should().OnlyContain(x => x <= NarrowerThanTheCap.X + NarrowerThanTheCap.Width + Tolerance);
    }

    [Fact]
    public void TextWithNoRoomBesideTheCapBeginsBelowItRatherThanLevelWithIt()
    {
        var page = Render(Prose, cap: 5, area: NarrowerThanTheCap);

        var positions = TextBaselines.PositionsOf(page);
        var cap = positions[0];
        var firstBodyLine = positions.Skip(1).Max(p => p.Y);

        // Down the page is down in y here, so the first line of body text sits below the cap's foot.
        firstBodyLine.Should().BeLessThan(cap.Y + Tolerance,
            "the text clears the cap in one move rather than being set beside it");
    }

    [Fact]
    public void ACapTooWideForItsColumnLosesNoTextOnTheWayPastIt()
    {
        // The skip moves the text down and a column this narrow then runs out of room for the end
        // of it, which is ordinary truncation. What must not happen is text going missing at the
        // point of the skip - so what is set has to be the beginning of the text and unbroken.
        var narrow = GlyphsOn(Render(Prose, cap: 5, area: NarrowerThanTheCap));
        var roomy = GlyphsOn(Render(Prose, cap: 5, area: new XRect(40, 40, 300, 300)));

        narrow.Should().NotBeEmpty();
        narrow.Should().HaveCountLessThan(roomy.Count, "a column this narrow cannot hold it all");
        roomy.Take(narrow.Count).Should().Equal(narrow, "what is set is the start of the text, in order");
    }

    [Fact]
    public void ACapWithNoRoomBesideItAndNoRoomBelowItDrawsTheCapAndStops()
    {
        // Nowhere for the text to go at all: too narrow to sit beside the cap and too short to sit
        // under it. The cap is still drawn; the text is dropped rather than the layout looping.
        var draw = () => Render(Prose, cap: 5, area: new XRect(40, 40, 30, 40));

        draw.Should().NotThrow();
        TextOperators.ShownStrings(Render(Prose, cap: 5, area: new XRect(40, 40, 30, 40)))
            .Should().ContainSingle("only the cap is drawn");
    }

    [Fact]
    public void TextWithNoRoomBesideTheCapMovesToTheNextColumnWhenThereIsNoRoomBelowIt()
    {
        // The cap belongs to the first column only, so clearing it can mean going across rather
        // than down - and the move past an obstruction has to be able to end up in a column that
        // the obstruction was never in.
        var page = Render(Prose, cap: 6, area: new XRect(40, 40, 120, 60),
            arrange: f => f.Columns = 2);

        var starts = TextBaselines.PositionsOf(page).Skip(1).Select(p => p.X).ToList();

        starts.Should().NotBeEmpty("the text is set in the column the cap is not in");
        starts.Should().OnlyContain(x => x > 40 + 50, "all of it is in the second column");
    }

    [Fact]
    public void ACapWithNoRoomBesideItStillEndsWhenOverflowIsAllowed()
    {
        // Overflow means no column ever fills, so nothing outside the skip can stop it. What stops
        // it is that the cap has a foot and the skip always moves at least a line towards it.
        var draw = () => Render(Prose, cap: 5, area: new XRect(40, 40, 30, 40),
            arrange: f => f.AllowVerticalOverflow = true);

        draw.Should().NotThrow();
    }

    // ----- the edges ------------------------------------------------------------------------------

    [Fact]
    public void TextShorterThanTheCapIsDeepIsDrawnAndDoesNotThrow()
    {
        var draw = () => Render("Two words", cap: 5);

        draw.Should().NotThrow();
        GlyphCountOf(TextOperators.ShownStrings(Render("Two words", cap: 5))[0]).Should().Be(1);
    }

    [Fact]
    public void AnEmptyStringDrawsNothingAndThrowsNothing()
    {
        var draw = () => Render("", cap: 3);

        draw.Should().NotThrow();
        TextOperators.ShownStrings(Render("", cap: 3)).Should().BeEmpty();
    }

    [Fact]
    public void ASingleCharacterBecomesTheCapAndLeavesNoBodyText()
    {
        var runs = TextOperators.ShownStrings(Render("T", cap: 3));

        GlyphCountOf(runs.Should().ContainSingle().Subject).Should().Be(1);
    }

    [Fact]
    public void TextBeginningWithASpaceIsLaidOutWithNoCapAtAll()
    {
        // A space is not an initial letter. Setting one as a cap would reserve room for nothing at
        // all and swallow a character of the caller's text.
        var withLeadingSpace = GlyphsOn(Render(" The quick brown fox", cap: 3));
        var plain = GlyphsOn(Render(" The quick brown fox", cap: 0));

        withLeadingSpace.Should().Equal(plain);
    }

    [Fact]
    public void NoDropCapLeavesEveryLineAtTheMargin()
    {
        var lines = BodyLinesOf(Prose, cap: 0);
        var margin = lines[0].X;

        lines.Should().OnlyContain(line => Math.Abs(line.X - margin) < 0.01);
    }

    [Fact]
    public void ADepthOfLessThanOneLineIsRefused()
    {
        var build = () => new XDropCap(new XFont("Arial", 12), 0);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ACapWithNoFontIsRefused()
    {
        var build = () => new XDropCap(null, 3);

        build.Should().Throw<ArgumentNullException>();
    }

    // ----- without an outline provider ------------------------------------------------------------

    [Fact]
    public void ACapIsStillDrawnAndRoomStillReservedWhenNoOutlineProviderIsRegistered()
    {
        WithoutOutlineProvider(() =>
        {
            var draw = () => Render(Prose, cap: 3);
            draw.Should().NotThrow("a drop cap must not need a backend seam nobody registered");

            GlyphCountOf(TextOperators.ShownStrings(Render(Prose, cap: 3))[0]).Should().Be(1);

            var margin = BodyLinesOf(Prose, cap: 0)[0].X;
            BodyLinesOf(Prose, cap: 3).Take(3).Should().OnlyContain(line => line.X > margin + 10);
        });
    }

    [Fact]
    public void TheCapSitsOnTheSameBaselineWhicheverWayItWasMeasured()
    {
        var withInk = TextBaselines.PositionsOf(Render(Prose, cap: 3))[0].Y;

        double byAdvance = 0;
        WithoutOutlineProvider(() => byAdvance = TextBaselines.PositionsOf(Render(Prose, cap: 3))[0].Y);

        // The foot goes on the third line's baseline either way. What differs is the size the cap
        // is scaled to, since the advance route takes the whole ascent for the letter's height.
        withInk.Should().BeApproximately(byAdvance, 0.01);
    }

    [Fact]
    public void TheCapsHeadIsLevelWithTheTextEvenWhenItWasSizedWithoutOutlines()
    {
        // Rendered with no provider registered, so the cap is sized from the face's declared cap
        // height rather than from the glyph's ink - and then measured against the ink anyway,
        // with the provider back. Sizing the cap by the ascent, as the fallback once did, leaves
        // the letter a quarter of its own height short of the line it should reach.
        PdfPage page = null;
        WithoutOutlineProvider(() => page = Render(ProseBeginningWith("T"), cap: 3));

        var cap = GeometryOf(page, "T", 3, new XFont(BodyFamily, BodySize), BodyFamily);

        cap.InkTop.Should().BeApproximately(cap.TextTop, Tolerance);
        cap.Foot.Should().BeApproximately(cap.LastSpannedBaseline, Tolerance);
    }

    [Fact]
    public void MeasuringByInkAndByMetricDifferInTheInsetRatherThanTheSize()
    {
        var byInk = Render(ProseBeginningWith("T"), cap: 3);

        PdfPage byAdvance = null;
        WithoutOutlineProvider(() => byAdvance = Render(ProseBeginningWith("T"), cap: 3));

        // Both routes hang the cap from the same two lines, so both arrive at the same size - the
        // declared cap height of this face is the height of its 'T' to within a rounding. What the
        // fallback cannot know is the left side bearing, so it sets the pen on the margin and the
        // ink a bearing's width inside it.
        FontSizesOn(byInk)[0].Should().BeApproximately(FontSizesOn(byAdvance)[0], 0.5);

        TextBaselines.PositionsOf(byAdvance)[0].X.Should().BeApproximately(Area.X, Tolerance,
            "with no outlines to read, the pen is all there is to place");
        TextBaselines.PositionsOf(byInk)[0].X.Should().BeLessThan(Area.X - Tolerance,
            "with outlines, the pen moves left so that the ink lands on the margin");
    }

    private static void WithoutOutlineProvider(Action test)
    {
        var provider = GlobalFontSettings.GlyphOutlineProvider;
        try
        {
            GlobalFontSettings.GlyphOutlineProvider = null;
            test();
        }
        finally
        {
            GlobalFontSettings.GlyphOutlineProvider = provider;
        }
    }

    // ----- rendering, and reading the page back ---------------------------------------------------

    /// <summary>
    ///   The bounds the formatter measures for the text, relative to the layout rectangle.
    /// </summary>
    private static XRect LayoutOf(string text, int cap, Action<XTextFormatter> arrange = null)
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());

        var formatter = new XTextFormatter(gfx);
        if (cap > 0)
            formatter.DropCap = new XDropCap(new XFont("Arial", 12), cap);
        arrange?.Invoke(formatter);

        return formatter.GetLayout(text, new XFont("Arial", 10), XBrushes.Black, Area);
    }

    private static PdfPage Render(string text, int cap, XRect? area = null, Action<XTextFormatter> arrange = null,
        string body = BodyFamily, string capFamily = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var formatter = new XTextFormatter(gfx);
            if (cap > 0)
                formatter.DropCap = new XDropCap(new XFont(capFamily ?? body, 12), cap);
            arrange?.Invoke(formatter);
            formatter.DrawString(text, new XFont(body, BodySize), XBrushes.Black, area ?? Area);
        }
        return page;
    }

    /// <summary>
    ///   Every glyph the page shows, in the order it was drawn, with the spaces taken out.
    /// </summary>
    /// <remarks>
    ///   Spaces go because a line break eats one, so two renders of the same text that break in
    ///   different places carry different numbers of them. What must not differ is the letters.
    /// </remarks>
    /// <summary>How many glyphs a drawn run holds. Identity-H writes two bytes for each.</summary>
    private static int GlyphCountOf(string run) => run.Length / 2;

    private static List<int> GlyphsOn(PdfPage page)
    {
        var glyphs = new List<int>();

        foreach (var run in TextOperators.ShownStrings(page))
        {
            // Identity-H writes two bytes per glyph and the string holds one byte per char, so a
            // glyph is a pair. Reading them singly leaves the high byte of a filtered space behind
            // and shifts everything after it by one.
            for (var idx = 0; idx + 1 < run.Length; idx += 2)
            {
                var glyph = (run[idx] << 8) | run[idx + 1];
                if (glyph != SpaceGlyph)
                    glyphs.Add(glyph);
            }
        }

        return glyphs;
    }

    /// <summary>Each line of body text, top of the page first. The cap is not one of them.</summary>
    private static List<(double X, double Y, int Glyphs)> BodyLinesOf(string text, int cap,
        Action<XTextFormatter> arrange = null)
    {
        var page = Render(text, cap, arrange: arrange);
        var positions = TextBaselines.PositionsOf(page);
        var runs = TextOperators.ShownStrings(page);

        var lines = new List<(double X, double Y, int Glyphs)>();
        for (var idx = cap > 0 ? 1 : 0; idx < positions.Count && idx < runs.Count; idx++)
            lines.Add((positions[idx].X, positions[idx].Y, runs[idx].Length));

        return lines.OrderByDescending(line => line.Y).ToList();
    }

    /// <summary>The size given to each <c>Tf</c>, in the order they were written.</summary>
    private static List<double> FontSizesOn(PdfPage page)
    {
        var content = Encoding.ASCII.GetString(PageContent.Of(page));
        return Regex.Matches(content, @"/F\d+ ([\d.]+) Tf")
            .Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToList();
    }

    // ----- reading the vertical placement off a rendered page --------------------------------------

    /// <summary>The prose, with its first letter swapped for the one the cap is to be set in.</summary>
    private static string ProseBeginningWith(string initial) => string.Concat(initial, Prose.AsSpan(1));

    /// <summary>
    ///   The four heights a cap's vertical placement is judged by, in points up from the foot of
    ///   the page as PDF measures them.
    /// </summary>
    private sealed class CapGeometry
    {
        /// <summary>The top of the cap glyph's ink.</summary>
        internal double InkTop;

        /// <summary>The baseline the cap is drawn on.</summary>
        internal double Foot;

        /// <summary>
        ///   The cap height of the first line of body text: where the eye reads the top of the
        ///   text as being, and where the head of the cap belongs.
        /// </summary>
        internal double TextTop;

        /// <summary>
        ///   The top of the first line's box, an ascent above its baseline. Higher than
        ///   <see cref="TextTop"/> by the room the face keeps above its capitals, and the line the
        ///   cap used to be hung from.
        /// </summary>
        internal double FirstLineBoxTop;

        /// <summary>The baseline of the last line the cap is set into.</summary>
        internal double LastSpannedBaseline;
    }

    private static CapGeometry GeometryOf(string text, int depth, string initial = "T",
        string body = BodyFamily, string capFamily = null)
    {
        return GeometryOf(Render(text, depth, body: body, capFamily: capFamily), initial, depth,
            new XFont(body, BodySize), capFamily ?? body);
    }

    /// <summary>
    ///   Reads the placement back off a drawn page: the cap's pen and size from the content, the
    ///   glyph's ink from the outlines of the face it was set in, and the body's lines from the
    ///   baselines the runs after it were drawn on.
    /// </summary>
    /// <remarks>
    ///   The font metrics are read here from the face's own tables rather than through the code
    ///   that places the cap, so that the two have to agree rather than being the same arithmetic
    ///   written twice.
    /// </remarks>
    private static CapGeometry GeometryOf(PdfPage page, string initial, int depth, XFont bodyFont, string capFamily)
    {
        var positions = TextBaselines.PositionsOf(page);
        var capFont = new XFont(capFamily, FontSizesOn(page)[0]);

        // The cap is drawn first and the body follows it, one run or more per line.
        var bodyBaselines = positions.Skip(1).Select(position => Math.Round(position.Y, 3))
            .Distinct().OrderByDescending(y => y).ToList();

        bodyBaselines.Count.Should().BeGreaterThanOrEqualTo(depth,
            "there must be as many lines as the cap is deep for the placement to be readable");

        return new CapGeometry
        {
            Foot = positions[0].Y,
            InkTop = positions[0].Y - InkOf(initial, capFont).Top,
            TextTop = bodyBaselines[0] + CapHeightOf(bodyFont),
            FirstLineBoxTop = bodyBaselines[0] + AscentOf(bodyFont),
            LastSpannedBaseline = bodyBaselines[depth - 1]
        };
    }

    /// <summary>
    ///   The box the glyph's ink fills, relative to the pen, with y measured down from the
    ///   baseline as the formatter measures it.
    /// </summary>
    private static XRect InkOf(string text, XFont font)
    {
        double left = double.MaxValue, right = double.MinValue;
        double top = double.MinValue, bottom = double.MaxValue;

        foreach (var outline in GlobalFontSettings.GlyphOutlineProvider.GetOutlines(text,
                     font.FontFamily.Name, (font.Style & XFontStyle.Bold) != 0,
                     (font.Style & XFontStyle.Italic) != 0, font.Size))
        {
            foreach (var segment in outline.Segments)
            {
                if (segment.Kind == XGlyphSegmentKind.Close)
                    continue;

                left = Math.Min(left, segment.End.X);
                right = Math.Max(right, segment.End.X);
                top = Math.Max(top, segment.End.Y);
                bottom = Math.Min(bottom, segment.End.Y);
            }
        }

        return new XRect(left, -top, right - left, top - bottom);
    }

    /// <summary>How tall a capital stands above the baseline, in points.</summary>
    private static double CapHeightOf(XFont font) => font.GetHeight() * font.Metrics.CapHeight / font.CellSpace;

    /// <summary>How tall the line's box stands above the baseline, in points.</summary>
    private static double AscentOf(XFont font) => font.GetHeight() * font.CellAscent / font.CellSpace;
}
