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
///   That a radial gradient is drawn where and how its brush says: in its own colours, out to its
///   extended ends when asked, and through its own transform and the graphics' transform.
/// </summary>
/// <remarks>
///   empira/PDFsharp#321 reports radial gradients drawn black and <c>ExtendLeft</c> and
///   <c>ExtendRight</c> ignored. Here the radial gradient was already drawn in colour, but the
///   two extend properties did not exist and the brush's own transform was stored and never read.
///   Every assertion below reads pixels, because a shading dictionary with the right keys is no
///   proof that a reader paints anything with it.
/// </remarks>
[Collection(RasterizingCollection.Name)]
public sealed class RadialGradientRenderingTests : IDisposable
{
    private const string OutDir = "Out/RadialGradient";

    private readonly List<MagickImageCollection> _rasterized = [];

    public void Dispose()
    {
        foreach (var collection in _rasterized)
            collection.Dispose();

        _rasterized.Clear();
    }

    static RadialGradientRenderingTests()
    {
        GhostscriptSetup.Configure();
    }

    private static readonly XPoint Centre = new XPoint(300, 300);
    private static readonly XRect Square = new XRect(200, 200, 200, 200);

    [GoldenImageFact]
    public void ARadialGradientIsDrawnInItsColoursFromTheCentreOut()
    {
        var page = Rasterize("colours", gfx =>
            gfx.DrawRectangle(new XRadialGradientBrush(Centre, 0, 90, XColors.Red, XColors.Blue), Square));

        var atCentre = At(page, Centre.X, Centre.Y);
        var halfWay = At(page, Centre.X + 45, Centre.Y);
        var nearRim = At(page, Centre.X, Centre.Y - 85);

        atCentre.R.Should().BeGreaterThan(200);
        atCentre.B.Should().BeLessThan(60);

        nearRim.B.Should().BeGreaterThan(200);
        nearRim.R.Should().BeLessThan(60);

        // A blend between the two, not black and not either end.
        halfWay.R.Should().BeInRange(70, 190);
        halfWay.B.Should().BeInRange(70, 190);
    }

    [GoldenImageFact]
    public void OnlyExtendRightPaintsTheCornersBeyondTheOuterCircle()
    {
        var corner = new XPoint(Square.X + 5, Square.Y + 5);
        var page = Rasterize("extend_right", gfx =>
        {
            gfx.DrawRectangle(new XRadialGradientBrush(Centre, 0, 80, XColors.Red, XColors.Blue), Square);

            var extended = new XRadialGradientBrush(Centre + new XVector(0, 300), 0, 80, XColors.Red, XColors.Blue)
            {
                ExtendRight = true
            };
            gfx.DrawRectangle(extended, Square with { Y = Square.Y + 300 });
        });

        IsWhite(At(page, corner.X, corner.Y)).Should().BeTrue("nothing is painted beyond the outer circle by default");

        var extendedCorner = At(page, corner.X, corner.Y + 300);
        extendedCorner.B.Should().BeGreaterThan(200, "an extended end carries the outer colour to the edge");
        extendedCorner.R.Should().BeLessThan(60);
    }

    [GoldenImageFact]
    public void OnlyExtendLeftFillsTheHoleInsideTheInnerCircle()
    {
        var page = Rasterize("extend_left", gfx =>
        {
            gfx.DrawRectangle(new XRadialGradientBrush(Centre, 40, 90, XColors.Red, XColors.Blue), Square);

            var extended = new XRadialGradientBrush(Centre + new XVector(0, 300), 40, 90, XColors.Red, XColors.Blue)
            {
                ExtendLeft = true
            };
            gfx.DrawRectangle(extended, Square with { Y = Square.Y + 300 });
        });

        IsWhite(At(page, Centre.X, Centre.Y)).Should().BeTrue("nothing is painted inside the inner circle by default");

        var filled = At(page, Centre.X, Centre.Y + 300);
        filled.R.Should().BeGreaterThan(200, "an extended start carries the inner colour to the centre");
        filled.B.Should().BeLessThan(60);
    }

    [GoldenImageFact]
    public void ALinearGradientExtendsPastBothOfItsPoints()
    {
        var band = new XRect(100, 200, 400, 100);
        var page = Rasterize("linear_extend", gfx =>
        {
            var start = new XPoint(250, 0);
            var end = new XPoint(350, 0);
            gfx.DrawRectangle(new XLinearGradientBrush(start, end, XColors.Red, XColors.Blue), band);
            gfx.DrawRectangle(new XLinearGradientBrush(start, end, XColors.Red, XColors.Blue)
            {
                ExtendLeft = true,
                ExtendRight = true
            }, band with { Y = band.Y + 200 });
        });

        IsWhite(At(page, 110, 250)).Should().BeTrue();
        IsWhite(At(page, 490, 250)).Should().BeTrue();

        var before = At(page, 110, 450);
        before.R.Should().BeGreaterThan(200);
        before.B.Should().BeLessThan(60, "white is not red");

        var after = At(page, 490, 450);
        after.B.Should().BeGreaterThan(200);
        after.R.Should().BeLessThan(60, "white is not blue");
    }

    [GoldenImageFact]
    public void TheBrushsOwnTransformPlacesAndStretchesTheGradient()
    {
        var page = Rasterize("brush_transform", gfx =>
        {
            // A circle about the origin, stretched to twice its width and moved to the centre.
            var brush = new XRadialGradientBrush(new XPoint(0, 0), 0, 50, XColors.Red, XColors.Blue);
            brush.Transform = new XMatrix(2, 0, 0, 1, Centre.X, Centre.Y);
            gfx.DrawRectangle(brush, new XRect(150, 200, 300, 200));
        });

        // Red at the centre the transform moved it to - drawn at the origin, it would be off the
        // shape and nothing would be painted here at all.
        At(page, Centre.X, Centre.Y).R.Should().BeGreaterThan(200);

        // 75 points out is inside a 100-point half-width, and outside a 50-point half-height.
        IsWhite(At(page, Centre.X + 75, Centre.Y)).Should().BeFalse("the ellipse reaches 100 points sideways");
        IsWhite(At(page, Centre.X, Centre.Y + 75)).Should().BeTrue("the ellipse reaches only 50 points down");
    }

    [GoldenImageFact]
    public void ALinearGradientTurnsWithItsBrushsTransform()
    {
        var band = new XRect(200, 200, 200, 100);
        var page = Rasterize("linear_brush_transform", gfx =>
        {
            // Left to right along the first hundred points of x, turned a quarter to run down
            // from the top of the band instead.
            var brush = new XLinearGradientBrush(new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);
            brush.Transform = new XMatrix(0, 1, -1, 0, 300, 200);
            gfx.DrawRectangle(brush, band);
        });

        var top = At(page, 250, 205);
        top.R.Should().BeGreaterThan(200);
        top.B.Should().BeLessThan(60);

        var bottom = At(page, 250, 295);
        bottom.B.Should().BeGreaterThan(200);
        bottom.R.Should().BeLessThan(60);

        // The bands run across, so one row is one colour from end to end.
        var left = At(page, 210, 250);
        var right = At(page, 390, 250);
        ((int)left.R).Should().BeCloseTo(right.R, 8);
        ((int)left.B).Should().BeCloseTo(right.B, 8);
    }

    [GoldenImageFact]
    public void AGraphicsTransformThatSquashesTheCircleDrawsAnEllipse()
    {
        var page = Rasterize("graphics_transform", gfx =>
        {
            // Twice as wide as it is drawn, so the centre in page space is (300, 300).
            gfx.ScaleTransform(2, 1);
            gfx.DrawRectangle(new XRadialGradientBrush(new XPoint(150, 300), 0, 50, XColors.Red, XColors.Blue),
                new XRect(75, 200, 150, 200));
        });

        At(page, Centre.X, Centre.Y).R.Should().BeGreaterThan(200);

        // The graphics' transform goes into the pattern matrix, so this held before the brush's
        // own transform was honoured, and it has to go on holding now that a squashing transform
        // is also handled in the shading. A circle of radius 100 - the horizontal scale applied
        // both ways - would reach 75 points down as well.
        IsWhite(At(page, Centre.X + 75, Centre.Y)).Should().BeFalse();
        IsWhite(At(page, Centre.X, Centre.Y + 75)).Should().BeTrue();
    }

    [GoldenImageFact]
    public void TheSoftMaskOfATranslucentGradientExtendsWithItsColours()
    {
        var corner = new XPoint(Square.X + 5, Square.Y + 5);
        var page = Rasterize("translucent_extend", gfx =>
        {
            gfx.DrawRectangle(new XRadialGradientBrush(Centre, 0, 80, XColors.Red, XColor.FromArgb(128, 0, 0, 255))
            {
                ExtendRight = true
            }, Square);
        });

        // Half-transparent blue over white is a light blue. Were the colour extended and the mask
        // not, the corner would be painted through a mask that is zero there, and stay white.
        var extendedCorner = At(page, corner.X, corner.Y);
        extendedCorner.B.Should().BeGreaterThan(200);
        extendedCorner.R.Should().BeInRange(80, 190);
        extendedCorner.G.Should().BeInRange(80, 190);
    }

    // ----- rasterizing and reading pixels ---------------------------------------------------------

    private IMagickImage<byte> Rasterize(string name, Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);

        var images = PdfHelper.Rasterize(document).ImageCollection;
        _rasterized.Add(images);
        PdfHelper.WriteImageCollection(images, OutDir, name);
        return images[0];
    }

    /// <summary>The colour at a point on the page, in points from the top left.</summary>
    private static IMagickColor<byte> At(IMagickImage<byte> page, double x, double y)
    {
        var scale = page.Width / 595.0;
        using var pixels = page.GetPixels();
        return pixels.GetPixel((int)(x * scale), (int)(y * scale)).ToColor();
    }

    private static bool IsWhite(IMagickColor<byte> colour) => colour.R > 245 && colour.G > 245 && colour.B > 245;
}
