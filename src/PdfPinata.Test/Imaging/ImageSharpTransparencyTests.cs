using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Utils;
using SixLabors.ImageSharp.PixelFormats;
using TUnit.Core;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   <see cref="ImageTransparencyTests"/> against the ImageSharp backend.
///   <para>
///     Shared: PdfPinata.ImageSharp4.Tests links this file, so it runs against both ImageSharp
///     backends. See #305.
///   </para>
/// </summary>
[InheritsTests]
public class ImageSharpTransparencyTests : ImageTransparencyTests
{
    protected override ImageSource.IImageSource Load(byte[] file) => new Probe().Load(file);

    /// <summary>
    ///   The test module in PdfPinata.Test registers Skia, so the ImageSharp backend is called through
    ///   a subclass, as the public <see cref="ImageSource"/> methods call it.
    /// </summary>
    private sealed class Probe : ImageSharpImageSource<Rgba32>
    {
        public ImageSource.IImageSource Load(byte[] file) => FromBinaryImpl("probe", () => file);
    }
}
