using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Rendering;
using SkiaSharp;
using TUnit.Core;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>Paths and clips handed to the device directly.</summary>
public class SkiaPageDeviceTests
{
    // A clip under the fill's own edges: a right triangle whose slanted side crosses pixels, filled
    // over the whole canvas in black. Smoothing the clip leaves grey pixels along that side.
    [Test]
    [Arguments(false, 2)]
    [Arguments(true, 3)]
    public void AClipIsSmoothedOnlyWhereTheDeviceIsToAntiAlias(bool antiAlias, int atLeastColours)
    {
        using var bitmap = new SKBitmap(100, 100);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var device = new SkiaPageDevice(canvas, antiAlias);
            var triangle = new PagePathBuilder();
            triangle.MoveTo(0, 0);
            triangle.LineTo(100, 0);
            triangle.LineTo(0, 73.3);
            triangle.Close();
            var everything = new PagePathBuilder();
            everything.AddRectangle(0, 0, 100, 100);

            device.Save();
            device.Clip(triangle.ToPath(), XMatrix.Identity, XFillMode.Winding);
            device.Fill(everything.ToPath(), XMatrix.Identity, XFillMode.Winding, new PagePaint(0, 0, 0));
            device.Restore();
        }

        var colours = bitmap.Pixels.Distinct().Count();
        if (antiAlias)
            colours.Should().BeGreaterThanOrEqualTo(atLeastColours);
        else
            colours.Should().Be(atLeastColours);
    }

    // A path the device is handed again is kept with its fill rule set on every use. It is drawn
    // here through a picture, which holds what was drawn after the device is gone, so a fill rule
    // left from the first use or a path freed with the device would show.
    [Test]
    public void ASharedPathFilledNonzeroAndThenEvenOddKeepsEachRuleInARecordedPicture()
    {
        var builder = new PagePathBuilder();
        builder.AddRectangle(0, 0, 1, 1);
        builder.AddRectangle(0.25, 0.25, 0.5, 0.5);
        var shared = (PagePath)typeof(PagePath)
            .GetMethod("Shared", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(builder.ToPath(), null);
        shared.IsShared.Should().BeTrue();

        SKPicture picture;
        using (var recorder = new SKPictureRecorder())
        {
            var canvas = recorder.BeginRecording(SKRect.Create(100, 100));
            using (var device = new SkiaPageDevice(canvas))
            {
                device.Fill(shared, new XMatrix(40, 0, 0, 40, 0, 0), XFillMode.Winding, new PagePaint(0, 0, 0));
                device.Fill(shared, new XMatrix(40, 0, 0, 40, 50, 0), XFillMode.Alternate, new PagePaint(0, 0, 0));
            }

            picture = recorder.EndRecording();
        }

        using (picture)
        using (var bitmap = new SKBitmap(100, 100))
        {
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawPicture(picture);
            }

            bitmap.GetPixel(20, 20).Should().Be(SKColors.Black, "the inner square is filled by the nonzero rule");
            bitmap.GetPixel(70, 20).Should().Be(SKColors.White, "and is a hole by the even-odd rule");
            bitmap.GetPixel(55, 5).Should().Be(SKColors.Black, "with the outer square filled either way");
        }
    }
}
