using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;
using Xunit;
using static PinataLayout.DocumentObjectModel.Shapes.ImageSource;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   An image's <c>PictureFormat</c> crop used to shrink the space the image was given and nothing
///   else: the source rectangle it was drawn from was ignored, so the whole picture was squeezed into
///   the cropped size. It is now drawn at its own scale and clipped to what is left.
/// </summary>
public class ImageCropRenderingTests
{
    [Fact]
    public void AnUncroppedImageIsDrawnWholeAndUnclipped()
    {
        var page = Rendered(cropLeftPoints: null);

        var placed = PlacedOperators.Of(page);
        placed.Should().NotContain(op => op.Name == OpCodeName.W || op.Name == OpCodeName.Wx);
        placed.Should().ContainSingle(op => op.Name == OpCodeName.Do);
    }

    [Fact]
    public void CroppingTheLeftHalfKeepsTheImageAtItsOwnScaleAndClipsItToTheRightHalf()
    {
        var whole = ImageBox(Rendered(cropLeftPoints: null));
        var cropped = Rendered(cropLeftPoints: whole.Width / 2);

        var placed = PlacedOperators.Of(cropped);
        var drawn = ImageBox(cropped);
        var clip = placed.Single(op => op.Name == OpCodeName.W || op.Name == OpCodeName.Wx);

        // The image is as wide as it was uncropped, not squeezed into half the width, and it
        // starts half its width to the left of where the cropped image stands.
        drawn.Width.Should().BeApproximately(whole.Width, 0.5);
        drawn.Height.Should().BeApproximately(whole.Height, 0.5);
        drawn.X.Should().BeApproximately(whole.X - whole.Width / 2, 0.5);
        placed.IndexOf(clip).Should().BeLessThan(placed.FindIndex(op => op.Name == OpCodeName.Do));
    }

    private static XRect ImageBox(PdfPage page)
    {
        var ctm = PlacedOperators.Of(page).Single(op => op.Name == OpCodeName.Do).Ctm;
        var corners = new[] { ctm.Transform(new XPoint(0, 0)), ctm.Transform(new XPoint(1, 1)) };
        var left = corners.Min(p => p.X);
        var bottom = corners.Min(p => p.Y);
        return new XRect(left, bottom, corners.Max(p => p.X) - left, corners.Max(p => p.Y) - bottom);
    }

    private static PdfPage Rendered(double? cropLeftPoints)
    {
        var document = new Document();
        var section = document.AddSection();
        var image = section.AddImage(FromFile(PathHelper.GetInstance().GetAssetPath("lenna.png")));
        if (cropLeftPoints is { } crop)
            image.PictureFormat.CropLeft = Unit.FromPoint(crop);

        var failures = new List<ImageFailedEventArgs>();
        var renderer = new PdfDocumentRenderer { Document = document, TagContent = false };
        renderer.DocumentRenderer.ImageFailed += (_, e) => failures.Add(e);
        renderer.RenderDocument();

        failures.Should().BeEmpty();
        return renderer.PdfDocument.Pages[0];
    }
}
