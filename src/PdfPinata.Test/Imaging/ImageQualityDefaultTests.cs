using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Skia;
using PdfPinata.Utils;
using PinataLayout.DocumentObjectModel.Shapes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using Xunit;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   Every way of making an image takes <c>int? quality</c>, so null is a value a caller may pass,
///   and it means the default JPEG quality of 75. Both backends used to cast it straight to
///   <c>int</c> and throw. Each test compares against an explicit 75, so it pins what null means as
///   well as that it is accepted. The encoders are deterministic, so equal settings give equal
///   bytes.
/// </summary>
public class ImageQualityDefaultTests
{
    [Fact]
    public void TheRegisteredBackendTakesNullAsTheDefaultFromBytes()
    {
        var png = Png();

        var jpeg = Jpeg(ImageSource.FromBinary("probe", () => png, quality: null));

        jpeg.Should().Equal(Jpeg(ImageSource.FromBinary("probe", () => png, quality: 75)));
    }

    [Fact]
    public void TheRegisteredBackendTakesNullAsTheDefaultFromAStream()
    {
        var png = Png();

        var jpeg = Jpeg(ImageSource.FromStream("probe", () => new MemoryStream(png), quality: null));

        jpeg.Should().Equal(Jpeg(ImageSource.FromStream("probe", () => new MemoryStream(png), quality: 75)));
    }

    [Fact]
    public void TheRegisteredBackendTakesNullAsTheDefaultFromAFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quality-default-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Png());
        try
        {
            var jpeg = Jpeg(ImageSource.FromFile(path, quality: null));

            jpeg.Should().Equal(Jpeg(ImageSource.FromFile(path, quality: 75)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ASkiaBitmapTakesNullAsTheDefault()
    {
        var jpeg = Jpeg(SkiaImageSource.FromSkiaBitmap(Bitmap(), transparent: false, quality: null));

        jpeg.Should().Equal(Jpeg(SkiaImageSource.FromSkiaBitmap(Bitmap(), transparent: false, quality: 75)));
    }

    [Fact]
    public void AnImageSharpImageTakesNullAsTheDefault()
    {
        var png = Png();

        var jpeg = Jpeg(ImageSharpImageSource<Rgba32>.FromImageSharpImage(
            SixLabors.ImageSharp.Image.Load<Rgba32>(png), PngFormat.Instance, quality: null));

        jpeg.Should().Equal(Jpeg(ImageSharpImageSource<Rgba32>.FromImageSharpImage(
            SixLabors.ImageSharp.Image.Load<Rgba32>(png), PngFormat.Instance, quality: 75)));
    }

    /// <summary>
    ///   The test module registers Skia, and swapping the seam would affect every other test that
    ///   runs alongside this one. So the ImageSharp backend's decoding path is called through a
    ///   subclass, as the public <see cref="ImageSource"/> methods call it.
    /// </summary>
    [Fact]
    public void TheImageSharpBackendTakesNullAsTheDefault()
    {
        var png = Png();
        var backend = new ImageSharpProbe();

        backend.FromBytes(png, null).Should().Equal(backend.FromBytes(png, 75));
        backend.FromStream(png, null).Should().Equal(backend.FromStream(png, 75));
    }

    private sealed class ImageSharpProbe : ImageSharpImageSource<Rgba32>
    {
        public byte[] FromBytes(byte[] png, int? quality) => Jpeg(FromBinaryImpl("probe", () => png, quality));

        public byte[] FromStream(byte[] png, int? quality) =>
            Jpeg(FromStreamImpl("probe", () => new MemoryStream(png), quality));
    }

    /// <summary>
    ///   Encodes <paramref name="image"/> and then releases it. Each backend's source owns the
    ///   bitmap or image it decoded and is <see cref="IDisposable"/>, and every source here is
    ///   encoded once.
    /// </summary>
    private static byte[] Jpeg(ImageSource.IImageSource image)
    {
        using var _ = image as IDisposable;
        using var ms = new MemoryStream();
        image.SaveAsJpeg(ms);
        ms.Length.Should().BePositive();
        return ms.ToArray();
    }

    private static SKBitmap Bitmap()
    {
        var bitmap = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            bitmap.SetPixel(x, y, new SKColor((byte)(x * 30), (byte)(y * 30), 128, 255));
        return bitmap;
    }

    private static byte[] Png()
    {
        using var bitmap = Bitmap();
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
