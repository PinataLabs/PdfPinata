using System.IO;
using AwesomeAssertions;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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
    [Fact]
    public void ImageSharpStaysOnTheLineTheBackendIsCompiledAgainst()
    {
        var version = typeof(Image).Assembly.GetName().Version;

        version.Should().NotBeNull();
        version.Major.Should().Be(2,
            "PdfPinata.ImageSharp calls APIs that only exist in the ImageSharp 2.1.x line. "
            + "Raising the dependency past 3.0 needs ImageSharpImageSource rewritten, not just the "
            + "version bumped - see issue #348. Consumers who need a newer ImageSharp should use "
            + "the PdfPinata.Skia backend instead.");
    }

    [Fact]
    public void ImageSharpBackendEncodesWhatItLoaded()
    {
        var path = PathHelper.GetInstance().GetAssetPath("lenna.png");

        // The out-parameter overload this backend depends on. It does not exist in ImageSharp 3.x,
        // so a dependency bump stops the test assembly compiling as well as failing the test above.
        var image = Image.Load<Rgba32>(path, out IImageFormat format);

        // Goes through the public factory rather than ImageSource.ImageSourceImpl, which is a global
        // the rest of the suite has pointed at Skia.
        var source = ImageSharpImageSource<Rgba32>.FromImageSharpImage(image, format);

        source.Width.Should().Be(512);
        source.Height.Should().Be(512);
        source.Transparent.Should().BeTrue("lenna.png is a PNG, which the backend reports as transparent");

        // The JPEG encoder sets an encoder property, the other half of the 3.x incompatibility.
        using var jpeg = new MemoryStream();
        source.SaveAsJpeg(jpeg);
        jpeg.Length.Should().BeGreaterThan(0);

        // The FLATE path takes no encoder at all any more, only ImageSharp's own bulk pixel
        // conversion - which is the other API a version bump would move under this backend.
        var pixels = source.GetPixels();
        pixels.Width.Should().Be(512);
        pixels.Height.Should().Be(512);
        pixels.Pixels.Length.Should().Be(512 * 512 * 4);
    }
}
