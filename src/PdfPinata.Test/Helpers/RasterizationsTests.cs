using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   What <see cref="Rasterizations"/> does with the pages it draws: writes each one out where the
///   seventeen copies it replaced wrote them, hands them back, and frees every one of them when it
///   is disposed and not before.
/// </summary>
[Xunit.Collection(RasterizingCollection.Name)]
public class RasterizationsTests
{
    private const string OutDir = "Out/Rasterizations";

    private static PdfDocument TwoPages()
    {
        var document = new PdfDocument();
        for (var index = 0; index < 2; index++)
        {
            using var gfx = XGraphics.FromPdfPage(document.AddPage());
            gfx.DrawRectangle(XBrushes.Blue, 100, 100, 200, 200);
        }
        return document;
    }

    [GoldenImageFact]
    public void EveryPageIsWrittenOutAndHandedBack()
    {
        using var rasterized = new Rasterizations(OutDir);

        var pages = rasterized.Of(TwoPages(), "two");

        pages.Count.Should().Be(2);
        var directory = Path.Combine(PathHelper.GetInstance().RootDir, OutDir);
        File.Exists(Path.Combine(directory, "two_1.png")).Should().BeTrue();
        File.Exists(Path.Combine(directory, "two_2.png")).Should().BeTrue();
        PageInk.Count(pages[1], PageInk.IsBlue).Should().BeGreaterThan(10000);
    }

    [GoldenImageFact]
    public void TheFirstPageIsTheFirstOfThoseHandedBack()
    {
        using var rasterized = new Rasterizations(OutDir);

        var page = rasterized.FirstPageOf(TwoPages(), "first");

        PageInk.Count(page, PageInk.IsBlue).Should().BeGreaterThan(10000);
    }

    [GoldenImageFact]
    public void ThePagesAreHeldUntilDisposedAndFreedThen()
    {
        var rasterized = new Rasterizations(OutDir);
        var first = rasterized.Of(TwoPages(), "held_a");
        var second = rasterized.Of(TwoPages(), "held_b");

        // Still there after a second rasterization: nothing is freed while the test holds it.
        first.Count.Should().Be(2);
        PageInk.Count(first[0], PageInk.IsBlue).Should().BeGreaterThan(10000);

        rasterized.Dispose();

        // A disposed collection has disposed its images and let them go. Nothing here reads a
        // pixel of a disposed image, which is native memory already handed back.
        first.Should().BeEmpty();
        second.Should().BeEmpty();
    }
}
