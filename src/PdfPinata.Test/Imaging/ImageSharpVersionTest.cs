using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Test.Helpers;
using ImageSource = PinataLayout.DocumentObjectModel.Shapes.ImageSource;
using PdfPinata.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace PdfPinata.Test.Imaging;

/// <summary>
/// PdfPinata.ImageSharp is compiled against the SixLabors.ImageSharp 2.1.x line and is not
/// binary compatible with 3.x: the Load(..., out IImageFormat) overloads are gone and the encoder
/// properties became init-only, which changes their setter signature. Against 3.x the backend
/// throws MissingMethodException at runtime, which is what issue #348 reported.
/// PdfPinata.ImageSharp4 compiles the same source against the 4.x line, with the IMAGESHARP4
/// constant defined, and is held to 4.x the same way.
///
/// These tests pin that assumption, so raising the dependency fails here rather than in a
/// consumer's application. Shared: PdfPinata.ImageSharp4.Tests links this file. See #305.
/// </summary>
public class ImageSharpVersionTest
{
    private const string ImageAsset = "lenna.png";
    private const int Size = 512;

#if IMAGESHARP4
    private const int ExpectedMajor = 4;

    private const string WhyThatMajor =
        "PdfPinata.ImageSharp4 is compiled against the ImageSharp 4.x line, and the next major is "
        + "not binary compatible with it. Raising the dependency needs the IMAGESHARP4 branches of "
        + "the shared backend reviewed, not just the version bumped - see #305.";
#else
    private const int ExpectedMajor = 2;

    private const string WhyThatMajor =
        "PdfPinata.ImageSharp calls APIs that only exist in the ImageSharp 2.1.x line. "
        + "Raising the dependency past 3.0 needs ImageSharpImageSource rewritten, not just the "
        + "version bumped - see issue #348. Consumers who need a newer ImageSharp should use "
        + "the PdfPinata.Skia backend instead.";
#endif

    [Test]
    public void ImageSharpStaysOnTheLineTheBackendIsCompiledAgainst()
    {
        var version = typeof(Image).Assembly.GetName().Version;

        version.Should().NotBeNull();
        // ReSharper disable PossibleNullReferenceException
        version.Major.Should().Be(ExpectedMajor, WhyThatMajor);
        // ReSharper restore PossibleNullReferenceException
    }

    [Test]
    public void ImageSharpBackendLoadsThePngAtItsDimensions()
    {
        var source = LoadSource();
        using var _ = source as IDisposable;

        source.Width.Should().Be(Size);
        source.Height.Should().Be(Size);
        source.Transparent.Should().BeTrue($"{ImageAsset} is a PNG, which the backend reports as transparent");
    }

    [Test]
    public void ImageSharpBackendEncodesWhatItLoadedAsJpeg()
    {
        var source = LoadSource();
        using var _ = source as IDisposable;

        // The JPEG encoder sets an encoder property, the other half of the 3.x incompatibility.
        using var jpeg = new MemoryStream();
        source.SaveAsJpeg(jpeg);

        var bytes = jpeg.ToArray();
        bytes.Should().StartWith([0xFF, 0xD8], "every JPEG opens with the SOI marker");

        using var reloaded = Image.Load<Rgba32>(bytes);
        reloaded.Width.Should().Be(Size);
        reloaded.Height.Should().Be(Size);
    }

    [Test]
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

#if IMAGESHARP4
        // From 3.0 the format an image was decoded from is on its metadata, not an out parameter.
        var image = Image.Load<Rgba32>(path);
        var format = image.Metadata.DecodedImageFormat;
#else
        // The out-parameter overload this backend depends on. It does not exist in ImageSharp 3.x,
        // so a dependency bump stops the test assembly compiling as well as failing the tests above.
        var image = Image.Load<Rgba32>(path, out var format);
#endif

        // Goes through the public factory rather than ImageSource.ImageSourceImpl, which is a global
        // the rest of the suite has pointed at Skia.
        return ImageSharpImageSource<Rgba32>.FromImageSharpImage(image, format);
    }
}
