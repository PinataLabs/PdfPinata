using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Moving a page's content into a form XObject and drawing it under a transform ought to leave
///   the page looking exactly as it did. Nothing short of rasterizing it says whether it really
///   does - the content walk in the other tests says where a rectangle went, not whether a font,
///   a shading or a clipping path survived the move.
///   <para>
///   Each document is put through the two cases where the drawing has to come out <b>unchanged</b>,
///   so that any difference at all is a fault and no reference images are needed:
///   </para>
///   <list type="bullet">
///     <item>resized to the size it already is, which changes nothing about how big the page is
///       and everything about how it is put together;</item>
///     <item>down to half and back again, which also exercises the second resize rewriting the
///       transform of the wrapper rather than wrapping it again.</item>
///   </list>
///   <para>
///   Both cases share one rendering of the original, because rasterizing is the expensive part.
///   </para>
/// </summary>
[Collection(RasterizingCollection.Name)]
public class PageResizeRenderingTests
{
    private const string OutDir = "Out/PageResize";

    /// <summary>
    ///   Two runs of the same rasterizer over the same drawing agree exactly, so anything above
    ///   the noise floor of the comparison is a page that came out different.
    /// </summary>
    private const double MaxDifference = 0.001;

    [GoldenImageFact]
    public void FamilyTreeSurvivesBeingResized() => ResizingDoesNotChangeTheDrawing("FamilyTree.pdf");

    [GoldenImageFact]
    public void TestDocumentSurvivesBeingResized() => ResizingDoesNotChangeTheDrawing("test.pdf");

    [GoldenImageFact]
    public void Pdf20SurvivesBeingResized() => ResizingDoesNotChangeTheDrawing("Pdf20.pdf");

    private static void ResizingDoesNotChangeTheDrawing(string asset)
    {
        var name = Path.GetFileNameWithoutExtension(asset);

        var before = Render(Open(asset), name + "_before");

        // Resized to the size it already is. The content still ends up in a form with the page
        // drawing that instead, so everything but the size has changed.
        var wrapped = Open(asset);
        foreach (var page in wrapped.Pages)
            page.Resize(new XSize(page.Width, page.Height), Exactly());

        Compare(Render(wrapped, name + "_wrapped"), before, asset, "wrapped");

        // Down to half and back up. The second resize works its transform out afresh from the
        // rectangle the content started in rather than composing one onto another, so the page
        // has to come back exactly where it began.
        var returned = Open(asset);
        foreach (var page in returned.Pages)
        {
            var was = new XSize(page.Width, page.Height);
            page.Resize(new XSize(was.Width / 2, was.Height / 2), Exactly());
            page.Resize(was, Exactly());
        }

        Compare(Render(returned, name + "_returned"), before, asset, "returned to its own size");
    }

    /// <summary>
    ///   Stretch with no margin, so the content lands exactly on the box and no rounding creeps
    ///   in from slack being shared between two sides.
    /// </summary>
    private static PageResizeOptions Exactly()
    {
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        return options;
    }

    private static void Compare(string[] after, string[] before, string asset, string what)
    {
        after.Length.Should().Be(before.Length);
        for (var page = 0; page < before.Length; page++)
        {
            PdfHelper.Diff(after[page], before[page], OutDir, asset + "_" + what + "_" + (page + 1))
                .DiffValue.Should().BeLessThan(MaxDifference,
                    "page {0} of {1} must draw the same once {2}", page + 1, asset, what);
        }
    }

    private static PdfDocument Open(string asset)
    {
        var path = PathHelper.GetInstance().GetAssetPath(asset);

        // PdfReader.Open reads the whole document in and does not keep the stream - its own
        // path overload closes the file the moment Open returns - so the handle can go now.
        // Left open, one leaks per call and the tests here call this several times each.
        using var stream = File.OpenRead(path);
        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    private static string[] Render(PdfDocument document, string prefix)
    {
        // Disposed as soon as the pages are on disk: the bitmaps are unmanaged and the collector
        // has no idea how big they are, so holding them is what kills the test host.
        using var rasterized = PdfHelper.Rasterize(document);
        return PdfHelper.WriteImageCollection(rasterized.ImageCollection, OutDir, prefix).ToArray();
    }
}
