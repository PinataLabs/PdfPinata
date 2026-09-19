using System.Runtime.CompilerServices;
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;

namespace PdfPinata.Test;

/// <summary>
/// PdfPinata no longer ships an imaging or font backend of its own, so the test assembly
/// registers one. Skia is used because it is the default backend; the ImageSharp backend is
/// exercised explicitly by the tests that construct its image source directly.
/// </summary>
internal static class TestBackendSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ImageSource.ImageSourceImpl = new SkiaImageSource();
        // Not the resolver the library ships: that one picks a font off the machine, which
        // lays a document out differently on each of them. See PinnedFontResolver.
        GlobalFontSettings.FontResolver = new PinnedFontResolver();
        // The third seam, which only XGraphicsPath.AddString needs. The test that covers what
        // happens when it is unset clears it and puts it back; see GlyphOutlineCollection.
        GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();
        GhostscriptSetup.Configure();
    }
}
