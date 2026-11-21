using System;
using System.IO;
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
/// What a caller of the Skia entry points sees: the size of what comes back, what it is painted
/// with, and what is left of the canvas they handed over.
/// </summary>
/// <remarks>Every page is 200 by 150 points and saved and read back before it is drawn.</remarks>
public class SkiaPageRenderingTests
{
    [Test]
    [Arguments(1, 200, 150)]
    [Arguments(2, 400, 300)]
    [Arguments(1.5, 300, 225)]
    [Arguments(0.1, 20, 15)]
    public void TheBitmapIsThePageAtTheScale(double scale, int width, int height)
    {
        using var bitmap = Page("").ToSKBitmap(new PageRenderOptions { Scale = scale });

        bitmap.Width.Should().Be(width);
        bitmap.Height.Should().Be(height);
    }

    [Test]
    public void TheBitmapIsTheRegionAtTheScaleRoundedUpToWholePixels()
    {
        using var bitmap = Page("").ToSKBitmap(new PageRenderOptions { Scale = 2, Region = new XRect(10, 20, 50.25, 30) });

        bitmap.Width.Should().Be(101);
        bitmap.Height.Should().Be(60);
    }

    [Test]
    [Arguments(0, 200, 150)]
    [Arguments(90, 150, 200)]
    [Arguments(180, 200, 150)]
    [Arguments(270, 150, 200)]
    public void TheBitmapIsThePageTheWayItIsDisplayed(int rotate, int width, int height)
    {
        using var bitmap = Page("", page => page.Rotate = rotate).ToSKBitmap();

        bitmap.Width.Should().Be(width);
        bitmap.Height.Should().Be(height);
    }

    [Test]
    public void ARegionIsTakenFromThePageAsDisplayed()
    {
        // The red square is at the bottom left of the page in user space, so at the top left once
        // the page is turned a quarter clockwise.
        using var bitmap = Page("1 0 0 rg 0 0 20 20 re f", page => page.Rotate = 90)
            .ToSKBitmap(new PageRenderOptions { Region = new XRect(0, 0, 20, 20) });

        bitmap.GetPixel(10, 10).Should().Be(SKColors.Red);
    }

    [Test]
    public void TheBackgroundIsPaintedWherePageDrawsNothing()
    {
        using var bitmap = Page("0 0 1 rg 0 0 100 150 re f")
            .ToSKBitmap(new PageRenderOptions { Background = SKColors.Yellow });

        bitmap.GetPixel(50, 75).Should().Be(SKColors.Blue);
        bitmap.GetPixel(150, 75).Should().Be(SKColors.Yellow);
    }

    [Test]
    public void AnnotationAppearancesAreDrawnUnlessTheOptionSaysNot()
    {
        var document = HandWritten.Page("", page: (doc, added) =>
        {
            var form = new PdfDictionary(doc);
            doc.Internals.AddObject(form);
            form.Elements.SetName("/Subtype", "/Form");
            form.Elements["/BBox"] = new PdfRectangle(new XPoint(0, 0), new XPoint(10, 10));
            form.CreateStream(System.Text.Encoding.Latin1.GetBytes("0 0 1 rg 0 0 10 10 re f"));
            var annotation = new PdfDictionary(doc);
            doc.Internals.AddObject(annotation);
            annotation.Elements.SetName("/Subtype", "/Stamp");
            annotation.Elements["/Rect"] = new PdfRectangle(new XPoint(0, 100), new XPoint(50, 150));
            var appearance = new PdfDictionary(doc);
            appearance.Elements["/N"] = form.Reference;
            annotation.Elements["/AP"] = appearance;
            var annotations = new PdfArray(doc);
            annotations.Elements.Add(annotation.Reference);
            added.Elements["/Annots"] = annotations;
        });

        using var with = document.ToSKBitmap(0);
        using var without = document.ToSKBitmap(0, new PageRenderOptions { Annotations = false });

        with.GetPixel(25, 25).Should().Be(SKColors.Blue);
        without.GetPixel(25, 25).Should().Be(SKColors.White);
    }

    [Test]
    public void OptionalContentIsDrawnByTheDefaultConfigurationUnlessAnotherIsChosen()
    {
        // A page 100 by 150, as Page makes it: the group is off in /D and on in the configuration "Show".
        PdfDictionary group = null;
        var document = HandWritten.Page("/OC /L1 BDC 0 0 1 rg 0 0 100 150 re f EMC", page: (doc, added) =>
        {
            group = new PdfDictionary(doc);
            doc.Internals.AddObject(group);
            group.Elements.SetName("/Type", "/OCG");
            var off = new PdfDictionary(doc);
            off.Elements["/OFF"] = new PdfArray(doc, group.Reference);
            var show = new PdfDictionary(doc);
            show.Elements.SetString("/Name", "Show");
            var properties = new PdfDictionary(doc);
            properties.Elements["/OCGs"] = new PdfArray(doc, group.Reference);
            properties.Elements["/D"] = off;
            properties.Elements["/Configs"] = new PdfArray(doc, show);
            doc.Internals.Catalog.Elements["/OCProperties"] = properties;

            var names = new PdfDictionary(doc);
            names.Elements["/L1"] = group.Reference;
            var resources = new PdfDictionary(doc);
            resources.Elements["/Properties"] = names;
            added.Elements["/Resources"] = resources;
        });

        using var hidden = document.ToSKBitmap(0);
        using var named = document.ToSKBitmap(0, new PageRenderOptions { OptionalContent = new PageOptionalContent { Configuration = "Show" } });
        using var switched = document.ToSKBitmap(0, new PageRenderOptions { OptionalContent = new PageOptionalContent { On = [group] } });

        hidden.GetPixel(50, 75).Should().Be(SKColors.White);
        named.GetPixel(50, 75).Should().Be(SKColors.Blue);
        switched.GetPixel(50, 75).Should().Be(SKColors.Blue);
    }

    [Test]
    public void TheBackgroundIsWhiteUnlessItIsSaid()
    {
        using var bitmap = Page("").ToSKBitmap();

        bitmap.Pixels.Distinct().Should().Equal(SKColors.White);
    }

    [Test]
    public void ATransparentBackgroundLeavesWhatThePageDoesNotDrawTransparent()
    {
        using var bitmap = Page("0 0 1 rg 0 0 100 150 re f")
            .ToSKBitmap(new PageRenderOptions { Background = SKColors.Transparent });

        bitmap.GetPixel(150, 75).Alpha.Should().Be(0);
        bitmap.GetPixel(50, 75).Should().Be(SKColors.Blue);
    }

    [Test]
    public void ARegionReachingOffThePageIsBackgroundThere()
    {
        using var bitmap = Page("0 g 0 0 200 150 re f")
            .ToSKBitmap(new PageRenderOptions { Region = new XRect(150, 0, 100, 150), Background = SKColors.Yellow });

        bitmap.Width.Should().Be(100);
        bitmap.GetPixel(25, 75).Should().Be(SKColors.Black);
        bitmap.GetPixel(75, 75).Should().Be(SKColors.Yellow);
    }

    // Ghostscript manages DeviceCMYK through a profile, so this is where the colours of the device
    // spaces are checked rather than in the comparison with it.
    [Test]
    [Arguments("1 0 0 rg", 255, 0, 0)]
    [Arguments("0.5 g", 128, 128, 128)]
    [Arguments("1 0 0 0 k", 0, 255, 255)]
    [Arguments("0 1 1 0 k", 255, 0, 0)]
    [Arguments("0 0 0 0.5 k", 128, 128, 128)]
    [Arguments("0 0 0 1 k", 0, 0, 0)]
    public void ADeviceColourIsPaintedAsItsRgb(string colour, int red, int green, int blue)
    {
        using var bitmap = Page(colour + " 0 0 200 150 re f").ToSKBitmap();

        bitmap.GetPixel(100, 75).Should().Be(new SKColor((byte)red, (byte)green, (byte)blue));
    }

    [Test]
    public void WithoutAntiAliasingAnEdgeIsOneColourOrTheOther()
    {
        const string diagonal = "0 g 0 0 m 200 150 l 0 150 l h f";

        using var smooth = Page(diagonal).ToSKBitmap();
        using var sharp = Page(diagonal).ToSKBitmap(new PageRenderOptions { AntiAlias = false });

        smooth.Pixels.Distinct().Count().Should().BeGreaterThan(2);
        sharp.Pixels.Distinct().Should().BeEquivalentTo([SKColors.Black, SKColors.White]);
    }

    [Test]
    public void ThePageIsDrawnUnderTheCanvasMatrixAndTheCanvasIsLeftAsItWas()
    {
        using var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Gray);
        canvas.Translate(50, 100);
        var matrix = canvas.TotalMatrix;
        var saves = canvas.SaveCount;

        var result = Page("1 0 0 rg 0 140 10 10 re f").Render(canvas);

        result.IsComplete.Should().BeTrue();
        canvas.TotalMatrix.Should().Be(matrix);
        canvas.SaveCount.Should().Be(saves);
        bitmap.GetPixel(55, 105).Should().Be(SKColors.Red);
        bitmap.GetPixel(150, 200).Should().Be(SKColors.White);
        bitmap.GetPixel(25, 50).Should().Be(SKColors.Gray, "nothing is painted outside the region");
    }

    [Test]
    public void ThePictureIsCulledToTheRegionInPixels()
    {
        using var picture = Page("").ToSKPicture(new PageRenderOptions { Scale = 2, Region = new XRect(0, 0, 50, 40) });

        picture.CullRect.Should().Be(SKRect.Create(100, 80));
    }

    [Test]
    public void TheEncodedImageIsThePage()
    {
        using var stream = new MemoryStream();
        var result = Page("0 0 1 rg 0 0 200 150 re f").ToImage(stream, SKEncodedImageFormat.Png,
            options: new PageRenderOptions { Scale = 0.5 });

        result.IsComplete.Should().BeTrue();
        using var decoded = SKBitmap.Decode(stream.ToArray());
        decoded.Width.Should().Be(100);
        decoded.Height.Should().Be(75);
        decoded.GetPixel(50, 37).Should().Be(SKColors.Blue);
    }

    [Test]
    public void APageOfADocumentIsNamedByItsIndex()
    {
        var document = HandWritten.Page("1 0 0 rg 0 0 200 150 re f");
        var second = document.AddPage();
        second.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(100, 50));
        second.Contents.AppendContent().CreateStream("0 0 1 rg 0 0 100 50 re f"u8.ToArray());
        var reopened = document.Reopened();

        using var bitmap = reopened.ToSKBitmap(1);
        using var picture = reopened.ToSKPicture(1);
        using var stream = new MemoryStream();
        reopened.ToImage(1, stream, SKEncodedImageFormat.Png);
        using var canvasBitmap = new SKBitmap(100, 50);
        using (var canvas = new SKCanvas(canvasBitmap))
            reopened.Render(1, canvas);

        bitmap.Width.Should().Be(100);
        bitmap.GetPixel(50, 25).Should().Be(SKColors.Blue);
        picture.CullRect.Should().Be(SKRect.Create(100, 50));
        stream.Length.Should().BeGreaterThan(0);
        canvasBitmap.GetPixel(50, 25).Should().Be(SKColors.Blue);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public void AnIndexThatNamesNoPageIsRefused(int index)
    {
        var document = HandWritten.Page("").Reopened();

        var render = () => document.ToSKBitmap(index);

        render.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageIndex");
    }

    [Test]
    public void AScaleThatIsNotPositiveIsRefused()
    {
        var render = () => Page("").ToSKBitmap(new PageRenderOptions { Scale = 0 });

        render.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("scale");
    }

    [Test]
    public void WhatCouldNotBeDrawnIsReported()
    {
        using var stream = new MemoryStream();
        var result = Page("0 0 1 rg /Nowhere Do 0 0 10 10 re f").ToImage(stream, SKEncodedImageFormat.Png);

        result.Diagnostics.Should().ContainSingle().Which.Problem.Should().Be(PageRenderProblem.MissingResource);
    }

    [Test]
    public void ARegionIsClippedOnACanvasLargerThanIt()
    {
        var page = Page("1 0 0 rg 0 0 200 150 re f");
        using var bitmap = new SKBitmap(300, 300);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Gray);

        page.Render(canvas, new PageRenderOptions { Region = new XRect(0, 0, 50, 50) });

        bitmap.GetPixel(25, 25).Should().Be(SKColors.Red);
        bitmap.GetPixel(100, 100).Should().Be(SKColors.Gray, "the page's red reaches no further than the region");
    }

    [Test]
    [Arguments(-1)]
    [Arguments(101)]
    public void AQualityOutsideZeroToOneHundredIsRefused(int quality)
    {
        using var stream = new MemoryStream();

        var act = () => Page("").ToImage(stream, SKEncodedImageFormat.Jpeg, quality);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("quality");
        stream.Length.Should().Be(0);
    }

    [Test]
    public void AGlyphDrawnAgainIsPaintedAgain()
    {
        // Each glyph is the same shared outline: a square 0.8 em wide, painted three times. Then
        // one is used as a clip, which is not the shared instance but a new path the clip builds
        // from it, and the red fill shows through that.
        var page = Page("0 0 1 rg BT /F1 40 Tf 10 100 Td (AAA) Tj ET " +
                        "BT 7 Tr /F1 40 Tf 10 20 Td (A) Tj ET 1 0 0 rg 0 0 200 80 re f",
            (document, added) => Font(document, added));
        using var bitmap = new SKBitmap(200, 150);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        using (var device = new SkiaPageDevice(canvas))
            PageRendering.Render(page, device, glyphSources: _ => new SquareGlyph());

        // Advancing a full em of 40 points each, from x 10, 30 points above the baseline at y 100.
        foreach (var x in new[] { 26, 66, 106 })
            bitmap.GetPixel(x, 30).Should().Be(SKColors.Blue);
        bitmap.GetPixel(50, 30).Should().Be(SKColors.White);
        bitmap.GetPixel(26, 110).Should().Be(SKColors.Red, "the red fill shows through the glyph it is clipped to");
        bitmap.GetPixel(100, 110).Should().Be(SKColors.White, "and nowhere else");
    }

    private static PdfPage Page(string content, Action<PdfPage> page = null) =>
        Page(content, (_, added) => page?.Invoke(added));

    private static PdfPage Page(string content, Action<PdfDocument, PdfPage> page) =>
        HandWritten.Page(content, page: page).Reopened().Pages[0];

    private static void Font(PdfDocument document, PdfPage page)
    {
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/TrueType");
        font.Elements.SetInteger("/FirstChar", 65);
        font.Elements.SetInteger("/LastChar", 65);
        font.Elements["/Widths"] = new PdfArray(document, new PdfReal(1000));
        document.Internals.AddObject(font);

        var resources = new PdfDictionary(document);
        var fonts = new PdfDictionary(document);
        fonts.Elements["/F1"] = font.Reference;
        resources.Elements["/Font"] = fonts;
        page.Elements["/Resources"] = resources;
    }

    private sealed class SquareGlyph : PageGlyphSource
    {
        private static readonly PagePath Square = Make();

        public override PagePath GetOutline(int code) => Square;

        private static PagePath Make()
        {
            var builder = new PagePathBuilder();
            builder.MoveTo(0.1, 0.1);
            builder.LineTo(0.9, 0.1);
            builder.LineTo(0.9, 0.9);
            builder.LineTo(0.1, 0.9);
            builder.Close();
            return builder.ToPath();
        }
    }
}
