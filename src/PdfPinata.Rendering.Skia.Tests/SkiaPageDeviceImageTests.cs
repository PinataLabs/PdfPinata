using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Rendering;
using SkiaSharp;
using TUnit.Core;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>
/// Images handed to the device directly, because page rendering decodes none yet: each sample is
/// drawn 10 pixels square, without smoothing, onto white.
/// </summary>
public class SkiaPageDeviceImageTests
{
    private static readonly XMatrix TenTimes = new(10, 0, 0, 10, 0, 0);

    [Test]
    public void AColourImageIsDrawnRowsFromTheTop()
    {
        byte[] samples =
        [
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 0, 0, 0, 0
        ];

        using var bitmap = Draw(PageImage.FromRgba32(2, 2, samples), new PagePaint(0, 0, 0));

        bitmap.GetPixel(5, 5).Should().Be(SKColors.Red);
        bitmap.GetPixel(15, 5).Should().Be(SKColors.Lime);
        bitmap.GetPixel(5, 15).Should().Be(SKColors.Blue);
        bitmap.GetPixel(15, 15).Should().Be(SKColors.White, "a sample with no alpha paints nothing");
    }

    [Test]
    public void AColourImageIsMultipliedByThePaintsAlphaAndNotItsColour()
    {
        using var bitmap = Draw(PageImage.FromRgba32(1, 1, new byte[] { 0, 0, 0, 255 }), new PagePaint(1, 0, 0, 0.5));

        bitmap.GetPixel(5, 5).Red.Should().BeCloseTo(128, 1);
        bitmap.GetPixel(5, 5).Green.Should().BeCloseTo(128, 1);
    }

    [Test]
    public void AStencilIsPaintedInThePaintsColourWhereItIsCovered()
    {
        using var bitmap = Draw(PageImage.FromAlpha8(2, 1, new byte[] { 255, 0 }), new PagePaint(0, 0, 1));

        bitmap.GetPixel(5, 5).Should().Be(SKColors.Blue);
        bitmap.GetPixel(15, 5).Should().Be(SKColors.White);
    }

    private static SKBitmap Draw(PageImage image, PagePaint paint)
    {
        var bitmap = new SKBitmap(20, 20);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var device = new SkiaPageDevice(canvas, antiAlias: false);
        device.DrawImage(image, TenTimes, paint);
        return bitmap;
    }
}
