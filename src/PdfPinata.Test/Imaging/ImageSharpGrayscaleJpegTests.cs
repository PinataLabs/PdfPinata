using System;
using System.IO;
using AwesomeAssertions;
using ImageMagick;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;
using static PdfPinata.Test.Helpers.JpegFrame;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   A grayscale JPEG keeps its one component through both ImageSharp backends, which re-encode a
///   JPEG in the colour type it was decoded from, so the image XObject has to be labelled with a
///   colour space of one component too. It used to be labelled <c>/DeviceRGB</c> whatever the JPEG
///   held, which is an invalid image: a reader refuses it, garbles it, or draws a third of it. See
///   #327.
///   <para>
///     <c>Imaging/grey-halves.jpg</c> was made with ImageMagick 7.1.2:
///     <c>magick -size 8x16 "xc:gray(64)" ( -size 8x16 "xc:gray(192)" ) +append -colorspace Gray -type Grayscale -quality 95 grey-halves.jpg</c>,
///     a 16-pixel square whose left half is grey 64 and right half grey 192, written as a
///     one-component JPEG.
///   </para>
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it runs against both ImageSharp
///     backends.
///   </para>
/// </summary>
[Rasterizing]
public sealed class ImageSharpGrayscaleJpegTests : IDisposable
{
    private const string GreyAsset = "Imaging/grey-halves.jpg";
    private const string ColourAsset = "frog-and-toad.jpg";
    private const string OutDir = "Out/GrayscaleJpeg";

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    [Test]
    public void TheSourceReallyIsAOneComponentJpeg()
    {
        ComponentCount(Asset(GreyAsset)).Should().Be(1, "otherwise the tests below prove nothing");
    }

    [Test]
    public void AGrayscaleJpegIsLabelledWithAOneComponentColourSpace()
    {
        var image = Embed(GreyAsset);

        image.Elements.GetName(PdfImage.Keys.Filter).Should().Be("/DCTDecode");
        ComponentCount(image.Stream.Value).Should().Be(1);
        image.Elements.GetName(PdfImage.Keys.ColorSpace).Should().Be("/DeviceGray");
    }

    [Test]
    public void AColourJpegIsStillLabelledDeviceRgb()
    {
        var image = Embed(ColourAsset);

        ComponentCount(image.Stream.Value).Should().Be(3);
        image.Elements.GetName(PdfImage.Keys.ColorSpace).Should().Be("/DeviceRGB");
    }

    [Test, GoldenImage]
    public void AGrayscaleJpegIsDrawnInItsOwnGreys()
    {
        var source = new Probe().Load(Asset(GreyAsset));
        using var _ = source as IDisposable;
        var image = XImage.FromImageSource(source);

        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(image, Area.X, Area.Y, Area.Width, Area.Height);

        var raster = _rasterized.FirstPageOf(document, "grey_halves");

        // Each half's grey, sampled well inside it. A one-component image under /DeviceRGB reads
        // three samples per pixel and so draws neither grey where it belongs, if it draws at all.
        // Two lossy encodes lie between the fill and the page.
        const int tolerance = 12;
        AssertGrey(Within(raster, Area, across: 0.25), 64, tolerance);
        AssertGrey(Within(raster, Area, across: 0.75), 192, tolerance);
    }

    // ----- arrangements -----

    /// <summary>Where the image is drawn, in the space the drawing uses.</summary>
    private static readonly XRect Area = new(50, 50, 200, 200);

    private static void AssertGrey(IMagickColor<byte> colour, int grey, int tolerance)
    {
        ((int)colour.R).Should().BeCloseTo(grey, (uint)tolerance);
        ((int)colour.G).Should().BeCloseTo(grey, (uint)tolerance);
        ((int)colour.B).Should().BeCloseTo(grey, (uint)tolerance);
    }

    private static byte[] Asset(string name) =>
        File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(name.Split('/')));

    /// <summary>The image XObject <paramref name="asset"/> becomes, decoded by the ImageSharp backend.</summary>
    private static PdfImage Embed(string asset)
    {
        var source = new Probe().Load(Asset(asset));
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
