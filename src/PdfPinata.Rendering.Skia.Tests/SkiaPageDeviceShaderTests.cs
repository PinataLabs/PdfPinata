using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using PdfPinata.Rendering.Skia.Tests.Helpers;
using PdfPinata.Test.Helpers;
using SkiaSharp;
using TUnit.Core;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>
/// Shadings and patterns as the device paints them: the colour of a pixel here and there, on a
/// page of 200 by 150 drawn at one pixel to the point without smoothing, onto white.
/// </summary>
/// <remarks>A pixel (x, y) of the bitmap is the point (x + 0.5, 149.5 - y) of the page.</remarks>
public class SkiaPageDeviceShaderTests
{
    private const string RedToBlue = "<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>";

    private static PdfPage Page(string content, params (string Category, string Name, (string Key, object Value)[] Entries, string Stream)[] resources) =>
        HandWritten.Page(content, page: (document, page) =>
        {
            foreach (var (category, name, entries, stream) in resources)
            {
                HandWritten.Resource(document, page, category, name, stream == null
                    ? HandWritten.Object(document, entries)
                    : HandWritten.Stream(document, stream, entries));
            }
        }).Reopened().Pages[0];

    private static SKBitmap Draw(PdfPage page) => page.ToSKBitmap(new PageRenderOptions { AntiAlias = false });

    private static (string, object)[] Axial(string extend, params (string Key, object Value)[] more) =>
    [
        ("/ShadingType", "2"), ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[50 0 150 0]"), ("/Function", RedToBlue),
        ("/Extend", extend), .. more
    ];

    [Test]
    public void AnAxialShadingRunsFromItsStartToItsEndAndStopsAtAnEndThatIsNotExtended()
    {
        using var bitmap = Draw(Page("/Sh0 sh", ("/Shading", "/Sh0", Axial("[true false]"), null)));

        bitmap.GetPixel(10, 75).Should().Be(SKColors.Red, "the start is carried on before it");
        bitmap.GetPixel(100, 75).Red.Should().BeCloseTo(126, 3);
        bitmap.GetPixel(100, 75).Blue.Should().BeCloseTo(129, 3);
        bitmap.GetPixel(148, 75).Blue.Should().BeGreaterThan(245);
        bitmap.GetPixel(152, 75).Should().Be(SKColors.White, "the end is not carried on past it");
    }

    [Test]
    public void ABackgroundAndItsShadingAreOnePaintUnderOneAlpha()
    {
        using var bitmap = Draw(Page("/Half gs /Pattern cs /P0 scn 0 0 200 150 re f",
            ("/Pattern", "/P0", [("/PatternType", "2"), ("/Shading",
                "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [50 0 150 0] /Background [0 1 0] /Function " + RedToBlue + " >>")], null),
            ("/ExtGState", "/Half", [("/ca", "0.5")], null)));

        // Half the background over white where the shading is not, and half the shading over
        // white where it is: none of the background shows through the shading.
        var outside = bitmap.GetPixel(20, 75);
        ((int)outside.Red, (int)outside.Green, (int)outside.Blue).Should().Be((127, 255, 127));
        var inside = bitmap.GetPixel(51, 75);
        inside.Red.Should().BeGreaterThan(250);
        inside.Green.Should().BeCloseTo(128, 3);
    }

    [Test]
    public void ARadialShadingThatIsNotExtendedPaintsTheWholeOfItsLastCircle()
    {
        // The circles are not one inside the other, so the far side of the last one is reached
        // only by circles earlier than the last that passes through it.
        using var bitmap = Draw(Page("/Sh0 sh", ("/Shading", "/Sh0",
        [
            ("/ShadingType", "3"), ("/ColorSpace", "/DeviceRGB"), ("/Coords", "[40 75 5 140 75 40]"),
            ("/Function", RedToBlue)
        ], null)));

        bitmap.GetPixel(170, 75).Should().NotBe(SKColors.White);
        bitmap.GetPixel(140, 45).Should().NotBe(SKColors.White);
        bitmap.GetPixel(190, 75).Should().Be(SKColors.White);
        bitmap.GetPixel(20, 75).Should().Be(SKColors.White);
    }

    [Test]
    public void ATilingPatternRepeatsItsCellFromWhereItsBoxIs()
    {
        // The cell's box starts at (5, 5) and is red in its lower left quarter.
        using var bitmap = Draw(Page("/Pattern cs /T0 scn 0 0 200 150 re f",
            ("/Pattern", "/T0",
            [
                ("/PatternType", "1"), ("/PaintType", "1"), ("/TilingType", "1"), ("/BBox", "[5 5 25 25]"),
                ("/XStep", "20"), ("/YStep", "20"), ("/Resources", "<< >>")
            ], "1 0 0 rg 5 5 10 10 re f")));

        foreach (var (x, y) in new[] { (10, 10), (30, 10), (10, 50), (150, 110) })
        {
            bitmap.GetPixel(x, 149 - y).Should().Be(SKColors.Red, $"({x}, {y}) is in the red quarter of a tile");
            bitmap.GetPixel(x + 10, 149 - y).Should().Be(SKColors.White);
            bitmap.GetPixel(x, 149 - y - 10).Should().Be(SKColors.White);
        }
    }

    [Test]
    public void AnUncolouredTilingPatternIsPaintedInTheColourItIsGiven()
    {
        using var bitmap = Draw(HandWritten.Page("/Tinted cs 0 0 1 /T0 scn 0 0 100 150 re f 0 1 0 /T0 scn 100 0 100 150 re f",
            page: (document, page) =>
            {
                HandWritten.Resource(document, page, "/ColorSpace", "/Tinted", new PdfLiteral("[/Pattern /DeviceRGB]"));
                HandWritten.Resource(document, page, "/Pattern", "/T0", HandWritten.Stream(document, "0 0 10 10 re f",
                    ("/PatternType", "1"), ("/PaintType", "2"), ("/TilingType", "1"), ("/BBox", "[0 0 20 20]"),
                    ("/XStep", "20"), ("/YStep", "20"), ("/Resources", "<< >>")));
            }).Reopened().Pages[0]);

        bitmap.GetPixel(45, 149 - 5).Should().Be(SKColors.Blue);
        bitmap.GetPixel(145, 149 - 5).Should().Be(SKColors.Lime);
        bitmap.GetPixel(55, 149 - 5).Should().Be(SKColors.White);
    }

    [Test]
    public void AMeshIsBlendedBetweenItsCornersAndPaintsNothingOutsideThem()
    {
        using var bitmap = Draw(Page("/Sh0 sh", ("/Shading", "/Sh0",
        [
            ("/ShadingType", "4"), ("/ColorSpace", "/DeviceRGB"), ("/BitsPerCoordinate", "16"), ("/BitsPerComponent", "8"),
            ("/BitsPerFlag", "8"), ("/Decode", "[0 65535 0 65535 0 1 0 1 0 1]")
        ], new MeshData()
            .Flag(0).Point(100, 20).Color(255, 0, 0)
            .Flag(0).Point(180, 20).Color(0, 0, 255)
            .Flag(0).Point(100, 100).Color(255, 0, 0).ToString())));

        bitmap.GetPixel(102, 149 - 60).Red.Should().BeGreaterThan(240);
        var middle = bitmap.GetPixel(140, 149 - 25);
        middle.Red.Should().BeCloseTo(127, 6);
        middle.Blue.Should().BeCloseTo(127, 6);
        middle.Green.Should().Be(0);
        bitmap.GetPixel(170, 149 - 90).Should().Be(SKColors.White);
        bitmap.GetPixel(50, 149 - 50).Should().Be(SKColors.White);
    }

    [Test]
    public void AStencilMaskIsPaintedWithTheShaderWhereItIsCovered()
    {
        // Page rendering decodes no image yet, so the shader is taken from a render and the mask
        // is handed to the device with it.
        var capture = new ShaderCapture();
        PageRendering.Render(Page("/Sh0 sh", ("/Shading", "/Sh0", Axial("[true true]"), null)), capture);
        var shader = capture.Paints.Single().Shader;

        using var bitmap = new SKBitmap(200, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var device = new SkiaPageDevice(canvas, antiAlias: false);
            var mask = PageImage.FromAlpha8(2, 1, new byte[] { 255, 0 });

            // The mask is 2 by 1 samples over the whole bitmap, and the gradient runs from 50 to 150 across it.
            device.TryDrawStencil(mask, new XMatrix(100, 0, 0, 100, 0, 0), new PageShaderPaint(shader, XMatrix.Identity))
                .Should().BeTrue();
        }

        bitmap.GetPixel(20, 50).Should().Be(SKColors.Red);
        bitmap.GetPixel(75, 50).Red.Should().BeCloseTo(191, 4);
        bitmap.GetPixel(75, 50).Blue.Should().BeCloseTo(64, 4);
        bitmap.GetPixel(125, 50).Should().Be(SKColors.White, "the mask does not cover the right half");
    }

    [Test]
    public void AGeometryWithNoAreaIsDrawnAsNothingAndNotHandedBack()
    {
        var capture = new ShaderCapture();
        PageRendering.Render(Page("/Sh0 sh", ("/Shading", "/Sh0", Axial("[true true]"), null)), capture);
        var box = new PagePathBuilder();
        box.AddRectangle(0, 0, 10, 10);

        using var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        using var device = new SkiaPageDevice(canvas);

        device.TryFill(box.ToPath(), new XMatrix(1, 0, 0, 0, 0, 0), XFillMode.Winding, capture.Paints.Single())
            .Should().BeTrue();
    }

    /// <summary>A device that keeps the shader paints it is handed and draws nothing.</summary>
    private sealed class ShaderCapture : PageDevice
    {
        public System.Collections.Generic.List<PageShaderPaint> Paints { get; } = [];

        public override void Save()
        {
        }

        public override void Restore()
        {
        }

        public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode)
        {
        }

        public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint)
        {
        }

        public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint)
        {
        }

        public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint)
        {
        }

        public override bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint)
        {
            Paints.Add(paint);
            return true;
        }
    }
}
