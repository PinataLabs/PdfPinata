using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Skia;
using TUnit.Core;

namespace PdfPinata.Test.Imaging;

/// <summary>
///   <see cref="ImageTransparencyTests"/> against the Skia backend, which called an image transparent
///   only when it was a PNG for longer than the ImageSharp backends did. See #313.
/// </summary>
[InheritsTests]
public class SkiaTransparencyTests : ImageTransparencyTests
{
    protected override ImageSource.IImageSource Load(byte[] file) => new Probe().Load(file);

    /// <summary>
    ///   Skia is called through a subclass, as the ImageSharp backend is, so the test does not depend
    ///   on which backend the module registered.
    /// </summary>
    private sealed class Probe : SkiaImageSource
    {
        public ImageSource.IImageSource Load(byte[] file) => FromBinaryImpl("probe", () => file);
    }
}
