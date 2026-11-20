using System.Runtime.CompilerServices;
using PdfPinata.Rendering.Skia.Tests.Helpers;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Rendering.Skia.Tests;

/// <summary>
/// Points Magick.NET at the Ghostscript this project ships. Nothing else: rendering a page needs
/// no backend, so none is registered, and a page that reached for one would say so with the seam's
/// own exception.
/// </summary>
internal static class TestBackendSetup
{
    // Not in the --coverlet controller. See CoverageController.OutsideController.
    [ModuleInitializer]
    internal static void Initialize() => CoverageController.OutsideController(Ghostscript.Configure);
}
