using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <c>XGraphicsPath.AddString</c> used to report through <c>DiagnosticsHelper</c> and return,
///   leaving the path empty and the caller's title gone from the page with no exception and no
///   warning. It now produces real geometry, through a backend seam, because reading contours out
///   of a font means a <c>glyf</c> decoder for TrueType and a charstring interpreter for
///   PostScript outlines — and the core package carries no font dependency to do either with.
/// </summary>
[Collection(GlyphOutlineCollection.Name)]
public class GlyphOutlineTests
{
    const double EmSize = 48;
    const string Text = "Handles";
    const string TrueTypeFamily = "Arial";

    static readonly XRect Box = new XRect(100, 200, 400, 60);

    // ----- the seam ------------------------------------------------------------------------------

    [Fact]
    public void AddStringIsRefusedWhenNoProviderIsRegistered()
    {
        var path = new XGraphicsPath();
        var provider = GlobalFontSettings.GlyphOutlineProvider;
        try
        {
            GlobalFontSettings.GlyphOutlineProvider = null;

            var add = () => path.AddString(Text, new XFontFamily(TrueTypeFamily), XFontStyle.Regular,
                EmSize, Box, XStringFormats.TopLeft);

            // Named, in the manner of the two seams that were already there, rather than an empty
            // path and no word about why.
            add.Should().Throw<InvalidOperationException>()
                .WithMessage("*GlyphOutlineProvider*")
                .WithMessage("*PdfPinata.Skia*");

            // ...and nothing was added on the way to the throw.
            PageDrawing(path).Should().Be(0);
        }
        finally
        {
            GlobalFontSettings.GlyphOutlineProvider = provider;
        }
    }

    [Fact]
    public void IsGlyphOutlineProviderSetReflectsWhatIsRegisteredRightNow()
    {
        var provider = GlobalFontSettings.GlyphOutlineProvider;
        try
        {
            GlobalFontSettings.GlyphOutlineProvider.Should().NotBeNull(
                "TestBackendSetup registers one for the whole assembly");
            GlobalFontSettings.IsGlyphOutlineProviderSet.Should().BeTrue();

            GlobalFontSettings.GlyphOutlineProvider = null;
            GlobalFontSettings.IsGlyphOutlineProviderSet.Should().BeFalse();
        }
        finally
        {
            GlobalFontSettings.GlyphOutlineProvider = provider;
        }
    }

    [Fact]
    public void RegisteringAProviderLeavesTheOtherSeamsAlone()
    {
        var resolver = GlobalFontSettings.FontResolver;
        var imageSource = PinataLayout.DocumentObjectModel.Shapes
            .ImageSource.ImageSourceImpl;

        GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();

        GlobalFontSettings.FontResolver.Should().BeSameAs(resolver);
        PinataLayout.DocumentObjectModel.Shapes.ImageSource.ImageSourceImpl
            .Should().BeSameAs(imageSource);
    }

    // ----- what the path holds -------------------------------------------------------------------

    [Fact]
    public void AnEmptyStringAddsNothingAndThrowsNothing()
    {
        var path = new XGraphicsPath();

        var add = () => path.AddString("", new XFontFamily(TrueTypeFamily), XFontStyle.Regular,
            EmSize, Box, XStringFormats.TopLeft);

        add.Should().NotThrow();
        PageDrawing(path).Should().Be(0);
    }

    [Fact]
    public void APathBuiltFromTrueTypeOutlinesIsNotEmpty()
    {
        var page = PageWithPath(PathOf(Text, TrueTypeFamily));

        // "Handles" has seven letters, and every one of them draws at least one contour.
        PathGeometry.FigureCountOf(page).Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void APathBuiltFromPostScriptOutlinesIsNotEmpty()
    {
        // The case that justifies the seam: a CFF font's contours live in Type 2 charstrings,
        // which an in-library glyf decoder would have produced nothing at all from.
        var page = PageWithPath(PathOf(Text, PinnedFontResolver.CffFamilyName));

        PathGeometry.FigureCountOf(page).Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void ThePathIsAboutTheSizeMeasureStringReports()
    {
        var measured = Measure(Text, TrueTypeFamily);
        var bounds = PathGeometry.BoundsOf(PageWithPath(PathOf(Text, TrueTypeFamily)));

        // The ink of a string is a little narrower than its advance, by the side bearing of the
        // last letter, and a little shorter than its line height. Bounds and emptiness only:
        // asserting on points or coordinates would pin one backend's idea of a curve.
        bounds.Width.Should().BeInRange(measured.Width * 0.85, measured.Width * 1.02);
        bounds.Height.Should().BeInRange(EmSize * 0.4, EmSize * 1.2);
    }

    [Fact]
    public void AGlyphWithNoOutlineOfItsOwnAddsNothing()
    {
        // A space is a glyph with an advance and no contours, and it is what a font is asked for
        // most often. It moves the pen and draws nothing, which is what DrawString does with it.
        var withSpace = PathGeometry.FigureCountOf(PageWithPath(PathOf("a a", TrueTypeFamily)));
        var withoutSpace = PathGeometry.FigureCountOf(PageWithPath(PathOf("aa", TrueTypeFamily)));

        withSpace.Should().Be(withoutSpace);
    }

    [Fact]
    public void ACharacterTheFontHasNoGlyphForIsOutlinedTheWayItIsDrawn()
    {
        // Liberation Sans has no Han characters, so both of these map to glyph zero - .notdef -
        // which is exactly what DrawString writes for them. The path therefore holds whatever the
        // font draws for an unknown character, and holds it once per character, rather than
        // quietly skipping it and leaving the line short.
        var one = PathGeometry.FigureCountOf(PageWithPath(PathOf("漢", TrueTypeFamily)));
        var another = PathGeometry.FigureCountOf(PageWithPath(PathOf("字", TrueTypeFamily)));

        one.Should().Be(another);

        var pair = PathGeometry.FigureCountOf(PageWithPath(PathOf("漢字", TrueTypeFamily)));
        pair.Should().Be(one * 2);
    }

    // ----- where the path lands ------------------------------------------------------------------

    [Fact]
    public void AlignmentWithinTheRectangleMovesThePathAcross()
    {
        var measured = Measure(Text, TrueTypeFamily);

        var left = PathGeometry.BoundsOf(PageWithPath(PathOf(Text, TrueTypeFamily, XStringFormats.TopLeft)));
        var right = PathGeometry.BoundsOf(PageWithPath(PathOf(Text, TrueTypeFamily, XStringFormats.TopRight)));

        // Moved by exactly the room the text leaves in the rectangle.
        (right.X - left.X).Should().BeApproximately(Box.Width - measured.Width, 0.5);
    }

    [Fact]
    public void ThePathLandsWhereDrawStringWouldHaveDrawnIt()
    {
        var format = XStringFormats.TopLeft;
        var pathBounds = PathGeometry.BoundsOf(PageWithPath(PathOf(Text, TrueTypeFamily, format)));

        var drawn = PageShowing(gfx => gfx.DrawString(Text, new XFont(TrueTypeFamily, EmSize),
            XBrushes.Black, Box, format));
        var baseline = TextBaselines.PositionsOf(drawn)[0];
        var measured = Measure(Text, TrueTypeFamily);

        // The ink of the path sits inside the box the drawn text advances through, starting no
        // sooner than the pen does and ending no later. It does not start exactly at the pen:
        // the first letter has a side bearing, which is room the glyph leaves rather than draws.
        pathBounds.X.Should().BeGreaterThanOrEqualTo(baseline.X - 0.5);
        (pathBounds.X + pathBounds.Width).Should().BeLessThanOrEqualTo(baseline.X + measured.Width + 0.5);

        // ...and it sits on the baseline the text would have been drawn on. Both are read in PDF
        // coordinates, which measure up the page.
        pathBounds.Y.Should().BeApproximately(baseline.Y, 1.0, "nothing of 'Handles' hangs below the baseline");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(200)]
    public void ABaseLineFormatPlacesTheBaselineOnTheTopEdgeWhateverTheHeight(double height)
    {
        var rectangle = new XRect(Box.X, Box.Y, Box.Width, height);
        var path = new XGraphicsPath();

        var add = () => path.AddString(Text, new XFontFamily(TrueTypeFamily), XFontStyle.Regular,
            EmSize, rectangle, XStringFormats.BaseLineLeft);

        add.Should().NotThrow();

        var page = PageWithPath(path);
        // The foot of the ink sits on the rectangle's top edge, which in PDF coordinates is
        // measured up from the foot of the page.
        PathGeometry.BoundsOf(page).Y.Should().BeApproximately(page.Height.Point - Box.Y, 1.0);
    }

    // ----- both backends -------------------------------------------------------------------------

    [Theory]
    [InlineData(Text)]
    // A capital carrying an accent rises above the font's ascender, which one of the two backends
    // has to allow for by hand. Nothing but a glyph like this says whether it did.
    [InlineData("ÄÖÜ")]
    public void TheTwoShippedBackendsAgreeAboutWhereTheGlyphsGo(string text)
    {
        var skia = OutlineBoundsFrom(new SkiaGlyphOutlineProvider(), text);
        var imageSharp = OutlineBoundsFrom(new ImageSharpGlyphOutlineProvider(), text);

        // Bounds, not points: the two subdivide curves differently, and an assertion on
        // coordinates would pin one backend's arithmetic rather than the agreement that matters.
        //
        // A quarter of a point, not a whole one. Both read the same font file through the same
        // resolver, so they agree to within the last digit of the curve arithmetic - and a
        // tolerance of a whole point is wide enough to swallow an advance width rounded to the
        // nearest point, which is exactly the disagreement this test exists to catch.
        imageSharp.X.Should().BeApproximately(skia.X, 0.25);
        imageSharp.Y.Should().BeApproximately(skia.Y, 0.25);
        imageSharp.Width.Should().BeApproximately(skia.Width, 0.25);
        imageSharp.Height.Should().BeApproximately(skia.Height, 0.25);
    }

    /// <summary>
    ///   Each backend, against the advance widths the library measures text with.
    /// </summary>
    /// <remarks>
    ///   The pen has to move by the font's own advance and nothing else. Skia's default is to hint
    ///   the outline and round the advance to whole pixels, which is right for a glyph being fitted
    ///   to a grid and wrong for one becoming a path in a PDF: it drew the same document differently
    ///   on Linux and on Windows, and it moved a path built by <c>AddString</c> away from text drawn
    ///   by <c>DrawString</c>, which measures the font file directly and never rounds.
    ///   <para>
    ///     Repeated characters, so that the distance from one glyph's outline to the next is the
    ///     advance exactly, with the side bearings cancelling out.
    ///   </para>
    /// </remarks>
    [Theory]
    [InlineData("HHHH")]
    [InlineData("llll")]
    [InlineData("oooo")]
    public void EachBackendMovesThePenByTheFontsOwnAdvanceWidth(string repeated)
    {
        var one = repeated.Substring(0, 1);
        var advance = Measure(one + one, TrueTypeFamily).Width - Measure(one, TrueTypeFamily).Width;

        foreach (var provider in new IGlyphOutlineProvider[]
                 { new SkiaGlyphOutlineProvider(), new ImageSharpGlyphOutlineProvider() })
        {
            // Close carries no point of its own, so it would contribute an origin the glyph
            // never went near.
            var lefts = provider.GetOutlines(repeated, TrueTypeFamily, false, false, EmSize)
                .Select(outline => outline.Segments
                    .Where(segment => segment.Kind != XGlyphSegmentKind.Close)
                    .Min(segment => segment.End.X))
                .ToList();

            lefts.Should().HaveCount(repeated.Length, "one outline per glyph");

            for (var glyph = 1; glyph < lefts.Count; glyph++)
            {
                (lefts[glyph] - lefts[glyph - 1]).Should().BeApproximately(advance, 0.001,
                    provider.GetType().Name + " must advance by the font's own width, unrounded");
            }
        }
    }

    static XRect OutlineBoundsFrom(IGlyphOutlineProvider provider, string text)
    {
        var was = GlobalFontSettings.GlyphOutlineProvider;
        try
        {
            GlobalFontSettings.GlyphOutlineProvider = provider;
            return PathGeometry.BoundsOf(PageWithPath(PathOf(text, TrueTypeFamily)));
        }
        finally
        {
            GlobalFontSettings.GlyphOutlineProvider = was;
        }
    }

    // ----- building the page ---------------------------------------------------------------------

    static XSize Measure(string text, string family)
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        return gfx.MeasureString(text, new XFont(family, EmSize), XStringFormats.Default);
    }

    static XGraphicsPath PathOf(string text, string family, XStringFormat format = null)
    {
        var path = new XGraphicsPath();
        path.AddString(text, new XFontFamily(family), XFontStyle.Regular, EmSize, Box,
            format ?? XStringFormats.TopLeft);
        return path;
    }

    /// <summary>How many path points a page drawing this path holds - zero for an empty path.</summary>
    static int PageDrawing(XGraphicsPath path) => PathGeometry.PointsOf(PageWithPath(path)).Count;

    static PdfPage PageWithPath(XGraphicsPath path) => PageShowing(gfx => gfx.DrawPath(XBrushes.Black, path));

    static PdfPage PageShowing(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);
        return page;
    }
}
