using System.Threading.Tasks;
using TUnit.Core;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Marks a test that compares rendered output against a checked-in reference image, and skips it
/// where there is nothing to rasterize with.
/// </summary>
/// <remarks>
/// These once ran on Linux alone, because the font was whatever the machine had installed and
/// the comparison was only meaningful where the reference images had been made. The tests now
/// bring their own font, so a document is laid out the same everywhere and the comparison
/// holds on any machine that can rasterize a PDF. What is left to differ is how a rasterizer
/// draws the edge of a glyph, which the tolerance on the comparison covers.
/// </remarks>
public sealed class GoldenImageAttribute() : SkipAttribute("Ghostscript is not available to rasterize PDFs on this platform.")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(!GhostscriptSetup.IsAvailable);
}
