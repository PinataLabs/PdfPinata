using System.Runtime.CompilerServices;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;

namespace PinataLayout.Rendering.Tests;

/// <summary>
/// PdfPinata carries no imaging or font backend of its own, so the test assembly registers
/// one. Skia because it is the default; nothing here is about which backend is in use.
/// </summary>
/// <remarks>
/// A module initializer rather than a fixture: the font resolver may only be set before the first
/// font is created, so it has to be in place before any test runs rather than before the first one
/// that asks for it.
/// </remarks>
internal static class TestBackendSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ImageSource.ImageSourceImpl = new SkiaImageSource();
        // Not the resolver the library ships: that one picks a font off the machine, which lays a
        // document out differently on each of them. See PinnedFontResolver.
        GlobalFontSettings.FontResolver = new PinnedFontResolver();
        // Only XGraphicsPath.AddString reads this one, which nothing here does. It is set so that
        // a test reaching a path that does gets a rendered document rather than the seam's
        // "no glyph outline provider" exception.
        GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();
    }
}
