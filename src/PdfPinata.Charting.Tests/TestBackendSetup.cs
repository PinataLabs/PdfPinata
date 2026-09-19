using System.Runtime.CompilerServices;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Charting.Tests;

/// <summary>
/// PdfPinata resolves fonts through a resolver the caller supplies, so the test assembly
/// supplies one. No backend is registered and none is referenced: a chart needs font bytes, which
/// is a seam of its own, rather than an imaging library.
/// </summary>
/// <remarks>
/// A module initializer rather than a fixture: the font resolver may only be set before the first
/// font is created, so it has to be in place before any test runs rather than before the first one
/// that asks for it.
///
/// No image source and no glyph outline provider. A chart draws lines, rectangles, wedges and
/// text, and reaches neither seam - leaving them unset means a renderer that grows a use for one
/// says so with the seam's own exception rather than quietly working here and nowhere else.
/// </remarks>
internal static class TestBackendSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Not the resolver the library ships: that one picks a font off the machine, which measures
        // a tick label differently on each of them. See PinnedFontResolver.
        GlobalFontSettings.FontResolver = new PinnedFontResolver();
    }
}
