using System.Runtime.CompilerServices;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace PdfPinata.ImageSharp4.Tests;

/// <summary>
/// Registers the ImageSharp 4.x backend for the whole assembly, where PdfPinata.Test registers
/// Skia. So a linked test that goes through a seam - <c>ImageSource.FromBinary</c>,
/// <c>XGraphicsPath.AddString</c> - exercises this backend here, and the tests that name a backend
/// explicitly compare it against Skia as they do there.
/// </summary>
internal static class TestBackendSetup
{
    // Not in the --coverlet controller, where loading PdfPinata.dll would lock it against
    // instrumentation on Windows. See CoverageController.OutsideController.
    [ModuleInitializer]
    internal static void Initialize() => CoverageController.OutsideController(Register);

    private static void Register()
    {
        ImageSource.ImageSourceImpl = new ImageSharpImageSource<Rgba32>();
        // The pinned resolver rather than ImageSharpFontResolver, for the same reason as in
        // PdfPinata.Test: a layout assertion needs the same font on every machine. The resolver
        // is exercised directly by FontResolverParityTest and TrueTypeCollectionTest.
        GlobalFontSettings.FontResolver = new PinnedFontResolver();
        GlobalFontSettings.GlyphOutlineProvider = new ImageSharpGlyphOutlineProvider();
        // For the one linked test that rasterizes a page, ImageSharpGrayscaleJpegTests.
        GhostscriptSetup.Configure();
    }
}
