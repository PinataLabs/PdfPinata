using System;
using System.Collections.Generic;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   That a band drawn into the bleed actually puts ink on the outermost row of the bleed, and
///   that the crop marks land on the paper outside it.
/// </summary>
/// <remarks>
///   Reading the content stream proves the operators were written; only rasterizing proves
///   nothing clipped them away between the operator and the paper. A bleed that is silently
///   trimmed back to the page is exactly the failure this feature exists to prevent, and it is
///   invisible to every structural assertion.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class PageBleedRenderingTests : IDisposable
{
    private const string OutDir = "Out/PageBleed";

    private readonly List<MagickImageCollection> _rasterized = [];

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static PageBleedRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    private static readonly XUnit Bleed = XUnit.FromMillimeter(3);
    private static readonly XUnit Marks = XUnit.FromMillimeter(5);

    [GoldenImageFact]
    public void ABandBledOffTheTopReachesTheOutermostRowOfTheBleed()
    {
        var sheet = Rasterize("band_off_the_top", (gfx, page) =>
            gfx.DrawRectangle(XBrushes.Black,
                new XRect(-Bleed.Point, -Bleed.Point, page.Width.Point + 2 * Bleed.Point, 60)));

        // Two points inside the top edge of the bleed, which is as far out as artwork may go and
        // would be blank if anything had clipped the band back to the page.
        InkAt(sheet, 0.5, Marks.Point + 2).Should().Be("black");
        InkAt(sheet, 0.06, Marks.Point + 2).Should().Be("black");
        InkAt(sheet, 0.94, Marks.Point + 2).Should().Be("black");

        // ...and the paper still shows below it, so "black at the top" is not "black everywhere".
        InkAt(sheet, 0.5, sheet.Height / 2.0 * 72 / Dpi()).Should().Be("white");
    }

    [GoldenImageFact]
    public void ABandBledOffTheLeftReachesTheOutermostColumnOfTheBleed()
    {
        var sheet = Rasterize("band_off_the_left", (gfx, page) =>
            gfx.DrawRectangle(XBrushes.Black,
                new XRect(-Bleed.Point, -Bleed.Point, 60, page.Height.Point + 2 * Bleed.Point)));

        foreach (var down in new[] { 0.06, 0.5, 0.94 })
            InkDownAt(sheet, Marks.Point + 2, down).Should().Be("black");

        InkDownAt(sheet, sheet.Width / 2.0 * 72 / Dpi(), 0.5).Should().Be("white");
    }

    [GoldenImageFact]
    public void TheArtworkStopsAtTheBleedAndLeavesThePressItsMargin()
    {
        var sheet = Rasterize("band_off_the_top", (gfx, page) =>
            gfx.DrawRectangle(XBrushes.Black,
                new XRect(-Bleed.Point, -Bleed.Point, page.Width.Point + 2 * Bleed.Point, 60)));

        // Two points in from the sheet's edge is outside the bleed, in the room the marks go in.
        // Ink there would mean the artwork had run into the press's margin.
        InkAt(sheet, 0.5, 2).Should().Be("white");
        InkDownAt(sheet, 2, 0.5).Should().Be("white");
    }

    [GoldenImageFact]
    public void TheCropMarksAreDrawnOnThePaperOutsideTheBleed()
    {
        var sheet = Rasterize("crop_marks", (gfx, page) =>
            gfx.DrawRectangle(XBrushes.Black, new XRect(0, 0, page.Width.Point, page.Height.Point)));

        // The horizontal mark at the top-left corner lies on the top cut and runs from the sheet's
        // left edge out to the bleed - so it is somewhere in the strip of paper left of the bleed,
        // level with the trim.
        var cut = Marks.Point + Bleed.Point;
        AnyInkIn(sheet, new XRect(1, cut - 2, Marks.Point - 2, 4))
            .Should().BeTrue("a crop mark extends the top cut out to the left edge of the sheet");

        AnyInkIn(sheet, new XRect(cut - 2, 1, 4, Marks.Point - 2))
            .Should().BeTrue("a crop mark extends the left cut out to the top edge of the sheet");

        // And nothing is drawn in the corner itself, which is where a mark would be if the two
        // had been run all the way to the sheet's corner rather than stopped at the bleed.
        AnyInkIn(sheet, new XRect(1, 1, Marks.Point - 3, Marks.Point - 3))
            .Should().BeFalse("the corner of the sheet carries no mark");
    }

    [GoldenImageFact]
    public void WithoutABleedTheSameDrawingLeavesTheEdgeOfTheSheetBlank()
    {
        // The same band, on an untrimmed page of the same size, drawn from the same coordinates.
        // There is no sheet around it at all - no bleed, no mark allowance and no marks.
        var sheet = Rasterize("band_with_no_bleed", (gfx, page) =>
            gfx.DrawRectangle(XBrushes.Black, new XRect(20, 20, page.Width.Point - 40, 60)),
            trimmed: false);

        InkAt(sheet, 0.5, 2).Should().Be("white");
        InkDownAt(sheet, 2, 0.06).Should().Be("white");
    }

    // ----- rasterizing and reading pixels ---------------------------------------------------------

    private IMagickImage<byte> Rasterize(string name, Action<XGraphics, PdfPage> draw, bool trimmed = true)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PageSize.A5;
        if (trimmed)
            page.TrimMargins.All = Bleed;

        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx, page);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    /// <summary>What the sheet was drawn at, so points can be turned into pixels.</summary>
    private static double Dpi() => 300;

    private static double ToPixels(double points) => points * 300 / 72.0;

    /// <summary>
    ///   Whether the sheet is inked or blank a fraction of the way across it and a given number
    ///   of points down from its top edge.
    /// </summary>
    private static string InkAt(IMagickImage<byte> sheet, double across, double pointsDown)
    {
        return Read(sheet, (int)Math.Round(across * (sheet.Width - 1)), (int)Math.Round(ToPixels(pointsDown)));
    }

    /// <summary>
    ///   Whether the sheet is inked or blank a given number of points in from its left edge and a
    ///   fraction of the way down it.
    /// </summary>
    private static string InkDownAt(IMagickImage<byte> sheet, double pointsAcross, double down)
    {
        return Read(sheet, (int)Math.Round(ToPixels(pointsAcross)), (int)Math.Round(down * (sheet.Height - 1)));
    }

    private static string Read(IMagickImage<byte> sheet, int x, int y)
    {
        using var pixels = sheet.GetPixels();
        var colour = pixels.GetPixel(Math.Clamp(x, 0, (int)sheet.Width - 1),
                                     Math.Clamp(y, 0, (int)sheet.Height - 1)).ToColor();
        // ReSharper disable once PossibleNullReferenceException
        var luminance = 0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B;

        // Named rather than numeric so a failure reads as "white where black was wanted" rather
        // than as two numbers the reader has to interpret.
        return luminance < 128 ? "black" : "white";
    }

    /// <summary>
    ///   Whether anything at all is drawn within a rectangle of the sheet, measured in points from
    ///   its top-left corner.
    /// </summary>
    /// <remarks>
    ///   A crop mark is a quarter of a point wide, which is one pixel at 300 dpi and comes out
    ///   grey rather than black once the rasterizer has antialiased it. So this asks whether the
    ///   paper is marked at all, not whether it is black.
    /// </remarks>
    private static bool AnyInkIn(IMagickImage<byte> sheet, XRect area)
    {
        var left = (int)Math.Round(ToPixels(area.X));
        var top = (int)Math.Round(ToPixels(area.Y));
        var right = (int)Math.Round(ToPixels(area.X + area.Width));
        var bottom = (int)Math.Round(ToPixels(area.Y + area.Height));

        using var pixels = sheet.GetPixels();
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var colour = pixels.GetPixel(x, y).ToColor();
                // ReSharper disable PossibleNullReferenceException
                if (0.299 * colour.R + 0.587 * colour.G + 0.114 * colour.B < 230)
                    return true;
                // ReSharper restore PossibleNullReferenceException
            }
        }

        return false;
    }
}
