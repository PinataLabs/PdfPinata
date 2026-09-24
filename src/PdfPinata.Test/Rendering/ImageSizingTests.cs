using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   The size an image is drawn at, from what the document says about its width, height, scale,
///   resolution, aspect ratio and crop - and from the image's own pixels for whatever it does not.
/// </summary>
/// <remarks>
///   Each case is one path through the renderer's sizing arithmetic, pinned at the extent the image
///   is drawn at today: the box the <c>Do</c> operator paints, in points, which is the laid-out size
///   stretched back out by whatever the crop took off. Several of the answers are worth knowing -
///   with the ratio locked, a width and a height both given keep only the one that makes the image
///   smaller, and a scale height wins over a scale width - and so are written down here.
/// </remarks>
public class ImageSizingTests
{
    [Theory]
    // Nothing said: the image's own pixels at its own resolution.
    [InlineData(null, null, null, null, null, null, false, "384.00 x 384.00")]
    // Ratio locked, the default: one extent given sets the other.
    [InlineData(null, 4.0, null, null, null, null, false, "113.39 x 113.39")]
    [InlineData(null, null, 3.0, null, null, null, false, "85.04 x 85.04")]
    [InlineData(true, 4.0, 3.0, null, null, null, false, "85.04 x 85.04")]
    [InlineData(true, 3.0, 4.0, null, null, null, false, "85.04 x 85.04")]
    [InlineData(true, null, null, 0.5, null, null, false, "192.00 x 192.00")]
    [InlineData(true, null, null, null, 0.25, null, false, "96.00 x 96.00")]
    [InlineData(true, null, null, 0.5, 0.25, null, false, "192.00 x 192.00")]
    [InlineData(true, 4.0, null, null, 0.5, null, false, "56.69 x 56.69")]
    // Ratio unlocked: each extent and each scale on its own terms.
    [InlineData(false, null, null, null, null, null, false, "384.00 x 384.00")]
    [InlineData(false, 4.0, 3.0, null, null, null, false, "113.39 x 85.04")]
    [InlineData(false, 4.0, null, null, null, null, false, "113.39 x 384.00")]
    [InlineData(false, null, 3.0, null, null, null, false, "384.00 x 85.04")]
    [InlineData(false, 4.0, 3.0, 0.5, 0.25, null, false, "28.35 x 42.52")]
    [InlineData(false, null, null, 0.5, null, null, false, "384.00 x 192.00")]
    // A resolution given is used in place of the image's own.
    [InlineData(null, null, null, null, null, 144.0, false, "256.00 x 256.00")]
    [InlineData(false, 4.0, null, null, null, 144.0, false, "113.39 x 256.00")]
    // A crop is taken off the laid-out size, and the image is drawn at its own scale behind it.
    [InlineData(null, null, null, null, null, null, true, "384.00 x 383.47")]
    [InlineData(null, 4.0, null, null, null, null, true, "113.39 x 113.23")]
    [InlineData(false, 4.0, 3.0, null, null, 144.0, true, "113.39 x 85.04")]
    public void AnImageIsDrawnAtTheSizeItsSettingsResolveTo(bool? lockAspectRatio, double? widthCm, double? heightCm,
        double? scaleHeight, double? scaleWidth, double? resolution, bool cropped, string expected)
    {
        var document = new Document();
        var image = document.AddSection().AddImage(FromFile(PathHelper.GetInstance().GetAssetPath("lenna.png")));
        if (lockAspectRatio is { } locked)
            image.LockAspectRatio = locked;
        if (widthCm is { } width)
            image.Width = Unit.FromCentimeter(width);
        if (heightCm is { } height)
            image.Height = Unit.FromCentimeter(height);
        if (scaleHeight is { } sh)
            image.ScaleHeight = sh;
        if (scaleWidth is { } sw)
            image.ScaleWidth = sw;
        if (resolution is { } dpi)
            image.Resolution = dpi;
        if (cropped)
        {
            image.PictureFormat.CropLeft = Unit.FromPoint(20);
            image.PictureFormat.CropRight = Unit.FromPoint(10);
            image.PictureFormat.CropTop = Unit.FromPoint(15);
            image.PictureFormat.CropBottom = Unit.FromPoint(5);
        }

        var failures = new List<ImageFailedEventArgs>();
        var renderer = new PdfDocumentRenderer { Document = document, TagContent = false };
        renderer.DocumentRenderer.ImageFailed += (_, e) => failures.Add(e);
        renderer.RenderDocument();

        failures.Should().BeEmpty();
        Describe(DrawnBox(renderer)).Should().Be(expected);
    }

    private static XRect DrawnBox(PdfDocumentRenderer renderer)
    {
        var ctm = renderer.PdfDocument.Pages.Cast<Pdf.PdfPage>()
            .SelectMany(PlacedOperators.Of)
            .Single(op => op.Name == OpCodeName.Do).Ctm;
        var corners = new[] { ctm.Transform(new XPoint(0, 0)), ctm.Transform(new XPoint(1, 1)) };
        var left = corners.Min(p => p.X);
        var bottom = corners.Min(p => p.Y);
        return new XRect(left, bottom, corners.Max(p => p.X) - left, corners.Max(p => p.Y) - bottom);
    }

    private static string Describe(XRect box) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.00} x {1:0.00}", box.Width, box.Height);
}
