using System;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   An image the backend calls transparent is written with its alpha, as a mask beside FLATE
///   samples; any other is written as a JPEG, which has no alpha. The backend used to call an image
///   transparent when it was a PNG and never otherwise, so a GIF, WebP or TIFF with transparent
///   pixels went down the JPEG path and its transparent pixels were drawn black. Now any image with
///   a pixel less than opaque is transparent, and an image with none keeps the path it had.
///   <para>
///     The assets were made with ImageMagick 7.1.2, each an 8-pixel square whose left half is
///     opaque:
///     <c>transparent-right-half.gif</c>: <c>magick -size 8x8 xc:none -fill red -draw "rectangle 0,0 3,7" transparent-right-half.gif</c>;
///     <c>translucent-right-half.webp</c>: <c>magick -size 8x8 "xc:rgba(0,0,255,0.5)" -fill blue -draw "rectangle 0,0 3,7" -define webp:lossless=true translucent-right-half.webp</c>;
///     <c>opaque.gif</c>: <c>magick -size 8x8 xc:red opaque.gif</c>;
///     <c>opaque.webp</c>: <c>magick -size 8x8 xc:blue -define webp:lossless=true opaque.webp</c>.
///   </para>
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it runs against both ImageSharp
///     backends. See #305.
///   </para>
/// </summary>
public class ImageSharpTransparencyTests
{
    [Test]
    public void AGifWithTransparentPixelsIsWrittenWithAStencilMask()
    {
        var image = Embed("Imaging/transparent-right-half.gif");

        // Its transparency is binary, which a 1-bit stencil states exactly.
        image.Elements.ContainsKey(PdfImage.Keys.Mask).Should().BeTrue();
        image.Elements.ContainsKey(PdfImage.Keys.Filter).Should().BeTrue();
        image.Elements.GetName(PdfImage.Keys.Filter).Should().NotBe("/DCTDecode");
    }

    [Test]
    public void AWebpWithTranslucentPixelsIsWrittenWithASoftMask()
    {
        var image = Embed("Imaging/translucent-right-half.webp");

        image.Elements.ContainsKey(PdfImage.Keys.SMask).Should().BeTrue();
    }

    [Test]
    [Arguments("Imaging/opaque.gif")]
    [Arguments("Imaging/opaque.webp")]
    [Arguments("frog-and-toad.jpg")]
    public void AnImageWithNoTransparentPixelIsStillWrittenAsAJpeg(string asset)
    {
        var image = Embed(asset);

        image.Elements.GetName(PdfImage.Keys.Filter).Should().Be("/DCTDecode");
        image.Elements.ContainsKey(PdfImage.Keys.Mask).Should().BeFalse();
        image.Elements.ContainsKey(PdfImage.Keys.SMask).Should().BeFalse();
    }

    [Test]
    public void APngIsWrittenWithoutJpegEvenWhenItIsOpaque()
    {
        // lenna.png has no alpha channel at all. A PNG has always been written losslessly, and an
        // image with no alpha keeping the path it had is what keeps existing documents unchanged.
        var image = Embed("lenna.png");

        image.Elements.GetName(PdfImage.Keys.Filter).Should().NotBe("/DCTDecode");
        image.Elements.ContainsKey(PdfImage.Keys.Mask).Should().BeFalse();
        image.Elements.ContainsKey(PdfImage.Keys.SMask).Should().BeFalse();
    }

    // ----- arrangements -----

    /// <summary>The image XObject <paramref name="asset"/> becomes, decoded by the ImageSharp backend.</summary>
    private static PdfImage Embed(string asset)
    {
        var file = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(asset.Split('/')));
        var source = new Probe().Load(file);
        using var _ = source as IDisposable;

        return new PdfImage(new PdfDocument(), XImage.FromImageSource(source));
    }

    /// <summary>
    ///   The test module in PdfPinata.Test registers Skia, so the ImageSharp backend is called through
    ///   a subclass, as the public <see cref="ImageSource"/> methods call it.
    /// </summary>
    private sealed class Probe : ImageSharpImageSource<Rgba32>
    {
        public ImageSource.IImageSource Load(byte[] file) => FromBinaryImpl("probe", () => file);
    }
}
