using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Skia;
using PdfPinata.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using Xunit;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   A non-JPEG image is written by copying the pixels a backend decoded straight into a FLATE
///   stream. Nothing here checks that the write succeeded - it checks that the samples in the
///   finished PDF are the samples that went in, row for row and channel for channel, which is the
///   only thing that says the row order and the channel order are right. A flipped row or a
///   swapped channel makes a perfectly valid, perfectly wrong document that veraPDF passes and
///   that every solid-fill test in this repository used to pass too.
/// </summary>
public class ImagePixelRoundTripTests
{
    const int Width = 3;
    const int Height = 2;

    /// <summary>
    ///   Red, green and blue all differ within every pixel and between every pair of pixels, so a
    ///   channel swap and any flip are each caught, and caught separately.
    /// </summary>
    static (byte R, byte G, byte B) Colour(int index)
        => ((byte)(10 + index * 10), (byte)(100 + index * 10), (byte)(200 + index * 10));

    /// <summary>
    ///   Alpha down one side of the 128 the monochrome mask splits at, and up the other.
    /// </summary>
    static readonly byte[] Alphas = { 255, 200, 128, 127, 64, 0 };

    [Fact]
    public void TheImageStreamHoldsTheSourcePixelsRowForRowAndChannelForChannel()
    {
        var placement = Draw(Skia(opaque: true));

        placement.XObject.Elements.GetInteger("/Width").Should().Be(Width);
        placement.XObject.Elements.GetInteger("/Height").Should().Be(Height);
        placement.XObject.Elements.GetInteger("/BitsPerComponent").Should().Be(8);
        placement.XObject.Elements.GetName("/ColorSpace").Should().Be("/DeviceRGB");

        placement.XObject.Stream.UnfilteredValue.Should().Equal(ExpectedRgb());
    }

    [Fact]
    public void AnOpaqueImageIsWrittenWithNoMaskOfEitherKind()
    {
        var placement = Draw(Skia(opaque: true));

        placement.XObject.Elements.ContainsKey("/Mask").Should().BeFalse();
        placement.XObject.Elements.ContainsKey("/SMask").Should().BeFalse();
    }

    [Fact]
    public void APartlyTransparentImageStillWritesEveryColourSample()
    {
        // The alpha byte moves out of the main stream into the masks; the colour samples beside it
        // are the same ones an opaque image would have written.
        var placement = Draw(Skia(opaque: false));

        placement.XObject.Stream.UnfilteredValue.Should().Equal(ExpectedRgb());
    }

    [Fact]
    public void ThePartlyTransparentImageCarriesItsAlphaInTheSoftMask()
    {
        var placement = Draw(Skia(opaque: false));

        var smask = placement.XObject.Elements.GetDictionary("/SMask");
        smask.Should().NotBeNull("some alpha is neither fully opaque nor fully clear");
        smask.Elements.GetInteger("/Width").Should().Be(Width);
        smask.Elements.GetInteger("/Height").Should().Be(Height);
        smask.Elements.GetInteger("/BitsPerComponent").Should().Be(8);
        smask.Elements.GetName("/ColorSpace").Should().Be("/DeviceGray");

        // One byte per pixel, top-down and in the order the pixels were given.
        smask.Stream.UnfilteredValue.Should().Equal(Alphas);
    }

    [Fact]
    public void ThePartlyTransparentImageAlsoCarriesTheOlderOnebitMask()
    {
        var placement = Draw(Skia(opaque: false));

        var mask = placement.XObject.Elements.GetDictionary("/Mask");
        mask.Should().NotBeNull();
        mask.Elements.GetInteger("/BitsPerComponent").Should().Be(1);
        mask.Elements.GetBoolean("/ImageMask").Should().BeTrue();

        // A bit is set where the pixel is transparent, meaning an alpha below 128, and the rows run
        // top-down like everything else. Row 0's alphas are 255, 200, 128 and row 1's are 127, 64, 0.
        mask.Stream.UnfilteredValue.Should().Equal(0x00, 0xE0);
    }

    [Fact]
    public void TheTwoBackendsWriteTheSameImage()
    {
        // The R/B swap used to be a side effect of BmpEncoder writing a valid BMP, which both
        // backends got for free from opposite directions. Now each does it for itself, so nothing
        // but this says they still agree.
        var skia = Draw(Skia(opaque: false));
        var imageSharp = Draw(ImageSharp());

        foreach (var key in new[] { "/Width", "/Height", "/BitsPerComponent" })
            imageSharp.XObject.Elements.GetInteger(key)
                .Should().Be(skia.XObject.Elements.GetInteger(key), "of " + key);

        imageSharp.XObject.Elements.GetName("/ColorSpace")
            .Should().Be(skia.XObject.Elements.GetName("/ColorSpace"));

        imageSharp.XObject.Stream.UnfilteredValue
            .Should().Equal(skia.XObject.Stream.UnfilteredValue);

        imageSharp.XObject.Elements.GetDictionary("/SMask").Stream.UnfilteredValue
            .Should().Equal(skia.XObject.Elements.GetDictionary("/SMask").Stream.UnfilteredValue);
    }

    [Fact]
    public void TheTwoBackendsHandBackTheSamePixels()
    {
        // The same thing one step earlier, at the seam itself, so a divergence names the backend
        // rather than only the document it produced.
        Skia(opaque: false).GetPixels().Pixels.ToArray()
            .Should().Equal(ImageSharp().GetPixels().Pixels.ToArray());
    }

    // ----- arrangements -----

    static byte[] ExpectedRgb()
    {
        var expected = new byte[Width * Height * 3];
        for (int i = 0; i < Width * Height; i++)
        {
            var (r, g, b) = Colour(i);
            expected[i * 3] = r;
            expected[i * 3 + 1] = g;
            expected[i * 3 + 2] = b;
        }
        return expected;
    }

    static ImageSource.IImageSource Skia(bool opaque)
    {
        var bitmap = new SKBitmap(
            new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));

        for (int i = 0; i < Width * Height; i++)
        {
            var (r, g, b) = Colour(i);
            bitmap.SetPixel(i % Width, i / Width, new SKColor(r, g, b, opaque ? (byte)255 : Alphas[i]));
        }

        return SkiaImageSource.FromSkiaBitmap(bitmap, transparent: true);
    }

    static ImageSource.IImageSource ImageSharp()
    {
        var image = new Image<Rgba32>(Width, Height);
        for (int i = 0; i < Width * Height; i++)
        {
            var (r, g, b) = Colour(i);
            image[i % Width, i / Width] = new Rgba32(r, g, b, Alphas[i]);
        }

        // PngFormat is what makes the backend call the image transparent, which is what sends it
        // down the FLATE path rather than the JPEG one.
        return ImageSharpImageSource<Rgba32>.FromImageSharpImage(image, PngFormat.Instance);
    }

    /// <summary>
    ///   Draws the image on a page, saves, reopens, and hands back the one placement on it - so
    ///   what is asserted on is a PDF that has been through the writer and the reader.
    /// </summary>
    static PdfImagePlacement Draw(ImageSource.IImageSource source)
    {
        var image = XImage.FromImageSource(source);

        var document = new PdfDocument();
        var page = document.AddPage();
        XGraphics.FromPdfPage(page).DrawImage(image, 0, 0, Width, Height);

        using var ms = new MemoryStream();
        document.Save(ms, false);
        ms.Position = 0;

        return Pdf.IO.PdfReader.Open(ms, PdfDocumentOpenMode.Modify)
            .Pages[0].GetImagePlacements().Single();
    }
}
