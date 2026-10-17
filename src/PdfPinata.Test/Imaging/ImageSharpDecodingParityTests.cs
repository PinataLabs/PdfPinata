using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;
#if IMAGESHARP4
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
#endif

namespace PdfPinata.Test.Imaging;

/// <summary>
///   The two ImageSharp backends decode the same file to the same pixels. Spec #305 makes them equal
///   packages, and a document that came out a different colour after switching from one to the
///   other would be a reason not to switch.
///   <para>
///     ImageSharp 4 can apply an image's embedded ICC profile on decode, converting it to sRGB, which
///     2.x never did. The backend asks it not to, so an image with a profile decodes to the samples
///     in the file, as it always has. The profile-bearing images here are built from the plain ones,
///     so what the profile is worth is the whole of the difference between them.
///   </para>
///   <para>
///     A JPEG is the one thing that does not decode identically, and no decoder option makes it:
///     ImageSharp 3 rewrote the JPEG decoder, and its arithmetic rounds differently. Within two
///     levels in 255 is what spec #305's "the same pixels" comes to for a JPEG.
///   </para>
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it runs against both ImageSharp
///     backends. What it expects was taken from PdfPinata.ImageSharp on ImageSharp 2.1.13; that it
///     passes in PdfPinata.Test says it is still 2.x's, and that it passes in
///     PdfPinata.ImageSharp4.Tests says 4.x agrees. A call that differs between ImageSharp majors
///     goes behind IMAGESHARP4. See #305.
///   </para>
/// </summary>
public class ImageSharpDecodingParityTests
{
    /// <summary>
    ///   What ImageSharp 2.1.13 decodes the 128-pixel square at (175, 156) of frog-and-toad.jpg to,
    ///   written out losslessly. A square rather than the whole picture to keep the file small; it
    ///   is the middle of the picture, where the JPEG has the most detail to disagree about.
    /// </summary>
    private const string JpegReference = "Imaging/frog-and-toad.imagesharp2-decoded-175-156-128.png";

    private const int ReferenceX = 175, ReferenceY = 156, ReferenceSize = 128;

    [Test]
    // A plain RGB PNG, and a palette PNG with transparency in a tRNS chunk.
    [Arguments("lenna.png", "827DBB145A5C5B88513F07E2117444F9FC7B532549735EE673C372763EC36B97")]
    [Arguments("Drawing/indexed-trns.png", "17DEFCEEAA09C7C0F33780C3CC03EBB120E282AABE787E7BA726534C044964CC")]
    public void APngDecodesToExactlyThePixelsTheTwoXBackendDecodedItTo(string asset, string sha256)
    {
        Hash(Decode(Asset(asset))).Should().Be(sha256, "{0} must decode as it did on ImageSharp 2.x", asset);
    }

    [Test]
    public void AJpegDecodesToWithinTwoLevelsOfThePixelsTheTwoXBackendDecodedItTo()
    {
        var decoded = Square(Decode(Asset("frog-and-toad.jpg")), width: 478);
        var reference = Decode(Asset(JpegReference));

        decoded.Should().HaveCount(reference.Length);
        var furthest = decoded.Zip(reference, (a, b) => Math.Abs(a - b)).Max();
#if IMAGESHARP4
        furthest.Should().BeLessThanOrEqualTo(2, "ImageSharp 4's JPEG decoder rounds differently from 2.x's, and no more");
#else
        furthest.Should().Be(0, "the reference is what this backend decodes the square to");
#endif
    }

    [Test]
    public void APngsEmbeddedProfileIsKeptNotApplied()
    {
        var plain = Asset("lenna.png");
        var profiled = EmbeddedColourProfile.IntoPng(plain, EmbeddedColourProfile.WideGamutLinearRgb());

        Decode(profiled).Should().Equal(Decode(plain));
    }

    [Test]
    public void AJpegsEmbeddedProfileIsKeptNotApplied()
    {
        var plain = Asset("frog-and-toad.jpg");
        var profiled = EmbeddedColourProfile.IntoJpeg(plain, EmbeddedColourProfile.WideGamutLinearRgb());

        Decode(profiled).Should().Equal(Decode(plain));
    }

    [Test]
    public void AFileAStreamAndBytesDecodeAlike()
    {
        // Each way in has its own load call in the backend, so each has to be told the same thing
        // about colour profiles.
        var profiled = EmbeddedColourProfile.IntoJpeg(Asset("frog-and-toad.jpg"), EmbeddedColourProfile.WideGamutLinearRgb());
        var backend = new Probe();
        var path = Path.Combine(Path.GetTempPath(), $"profiled-{Guid.NewGuid():N}.jpg");
        File.WriteAllBytes(path, profiled);
        try
        {
            var fromBytes = backend.FromBytes(profiled);

            backend.FromStream(profiled).Should().Equal(fromBytes);
            backend.FromFile(path).Should().Equal(fromBytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

#if IMAGESHARP4
    [Test]
    [Arguments("lenna.png")]
    [Arguments("frog-and-toad.jpg")]
    public void TheProfileIsOneImageSharp4WouldOtherwiseApply(string asset)
    {
        // Without this the tests above could pass because ImageSharp ignored the profile, not
        // because the backend asked it to: asked to convert, ImageSharp 4 changes the pixels.
        var plain = Asset(asset);
        var profile = EmbeddedColourProfile.WideGamutLinearRgb();
        var profiled = asset.EndsWith(".png", StringComparison.Ordinal)
            ? EmbeddedColourProfile.IntoPng(plain, profile)
            : EmbeddedColourProfile.IntoJpeg(plain, profile);

        using var converted = Image.Load<Rgba32>(
            new DecoderOptions { ColorProfileHandling = ColorProfileHandling.Convert }, profiled);
        using var original = Image.Load<Rgba32>(plain);

        converted.Metadata.IccProfile.Should().BeNull("a converted image no longer carries its profile");
        converted[100, 100].Should().NotBe(original[100, 100]);
    }
#endif

    // ----- arrangements -----

    private static byte[] Asset(string name) =>
        File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(name.Split('/')));

    /// <summary>The pixels the backend hands the PDF writer, as <see cref="PixelBuffer"/> lays them out.</summary>
    private static byte[] Decode(byte[] file) => new Probe().FromBytes(file);

    /// <summary>The reference square, cut out of a whole image's pixels.</summary>
    private static byte[] Square(byte[] pixels, int width)
    {
        var square = new byte[ReferenceSize * ReferenceSize * PixelBuffer.BytesPerPixel];
        var row = ReferenceSize * PixelBuffer.BytesPerPixel;
        for (var y = 0; y < ReferenceSize; y++)
        {
            Buffer.BlockCopy(pixels, ((ReferenceY + y) * width + ReferenceX) * PixelBuffer.BytesPerPixel,
                square, y * row, row);
        }

        return square;
    }

    private static string Hash(byte[] pixels) => Convert.ToHexString(SHA256.HashData(pixels));

    /// <summary>
    ///   The test module in PdfPinata.Test registers Skia, so the ImageSharp backend is called through
    ///   a subclass, as the public <see cref="ImageSource"/> methods call it.
    /// </summary>
    private sealed class Probe : ImageSharpImageSource<Rgba32>
    {
        public byte[] FromBytes(byte[] file) => Pixels(FromBinaryImpl("probe", () => file));

        public byte[] FromStream(byte[] file) => Pixels(FromStreamImpl("probe", () => new MemoryStream(file)));

        public byte[] FromFile(string path) => Pixels(FromFileImpl(path));

        private static byte[] Pixels(ImageSource.IImageSource source)
        {
            using var _ = source as IDisposable;
            return source.GetPixels().Pixels.ToArray();
        }
    }
}
