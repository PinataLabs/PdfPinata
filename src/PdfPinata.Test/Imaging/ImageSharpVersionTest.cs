using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Test.Helpers;
using ImageSource = PinataLayout.DocumentObjectModel.Shapes.ImageSource;
using PdfPinata.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PdfPinata.Test.Imaging;

/// <summary>
/// PdfPinata.ImageSharp is compiled against the SixLabors.ImageSharp 2.1.x line and is not
/// binary compatible with 3.x: the Load(..., out IImageFormat) overloads are gone and the encoder
/// properties became init-only, which changes their setter signature. Against 3.x the backend
/// throws MissingMethodException at runtime, which is what issue #348 reported.
///
/// These tests pin that assumption, so raising the dependency fails here rather than in a
/// consumer's application.
/// </summary>
public class ImageSharpVersionTest
{
    private const string ImageAsset = "lenna.png";
    private const int Size = 512;

    [Fact]
    public void ImageSharpStaysOnTheLineTheBackendIsCompiledAgainst()
    {
        var version = typeof(Image).Assembly.GetName().Version;

        version.Should().NotBeNull();
        // ReSharper disable PossibleNullReferenceException
        version.Major.Should().Be(2,
            "PdfPinata.ImageSharp calls APIs that only exist in the ImageSharp 2.1.x line. "
            + "Raising the dependency past 3.0 needs ImageSharpImageSource rewritten, not just the "
            + "version bumped - see issue #348. Consumers who need a newer ImageSharp should use "
            + "the PdfPinata.Skia backend instead.");
        // ReSharper restore PossibleNullReferenceException
    }

    [Fact]
    public void ImageSharpBackendLoadsThePngAtItsDimensions()
    {
        var source = LoadSource();
        using var _ = source as IDisposable;

        source.Width.Should().Be(Size);
        source.Height.Should().Be(Size);
        source.Transparent.Should().BeTrue($"{ImageAsset} is a PNG, which the backend reports as transparent");
    }

    [Fact]
    public void ImageSharpBackendEncodesWhatItLoadedAsJpeg()
    {
        var source = LoadSource();
        using var _ = source as IDisposable;

        // The JPEG encoder sets an encoder property, the other half of the 3.x incompatibility.
        using var jpeg = new MemoryStream();
        source.SaveAsJpeg(jpeg);

        var bytes = jpeg.ToArray();
        bytes.Should().StartWith(new byte[] { 0xFF, 0xD8 }, "every JPEG opens with the SOI marker");

        using var reloaded = Image.Load<Rgba32>(bytes);
        reloaded.Width.Should().Be(Size);
        reloaded.Height.Should().Be(Size);
    }

    [Fact]
    public void ImageSharpBackendConvertsWhatItLoadedToPixels()
    {
        var source = LoadSource();
        using var _ = source as IDisposable;

        // The FLATE path takes no encoder at all anymore, only ImageSharp's own bulk pixel
        // conversion - which is the other API a version bump would move under this backend.
        var pixels = source.GetPixels();
        pixels.Width.Should().Be(Size);
        pixels.Height.Should().Be(Size);
        pixels.Pixels.Length.Should().Be(Size * Size * 4);
    }

    /// <summary>
    /// <see cref="ImageSource.IImageSource"/> is not itself disposable, but the ImageSharp implementation is, and
    /// it disposes the image it wraps, so callers dispose the source and not the image.
    /// </summary>
    private static ImageSource.IImageSource LoadSource()
    {
        var path = PathHelper.GetInstance().GetAssetPath(ImageAsset);

        // The out-parameter overload this backend depends on. It does not exist in ImageSharp 3.x,
        // so a dependency bump stops the test assembly compiling as well as failing the tests above.
        var image = Image.Load<Rgba32>(path, out var format);

        // Goes through the public factory rather than ImageSource.ImageSourceImpl, which is a global
        // the rest of the suite has pointed at Skia.
        return ImageSharpImageSource<Rgba32>.FromImageSharpImage(image, format);
    }
}
