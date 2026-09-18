using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Dropping a resource a page turns out to need is worse than keeping one it does not, and
///   the only way to tell the two apart is to look at the page. These render the documents that
///   come with the tests before and after pruning and compare what comes out.
/// </summary>
[Collection(RasterizingCollection.Name)]
public class PruneUnusedResourcesRenderingTests
{
    private const string OutDir = "Out/PruneUnusedResources";

    /// <summary>
    ///   Two runs of the same rasterizer over the same drawing agree exactly, so anything above
    ///   the noise floor of the comparison is a page that came out different.
    /// </summary>
    private const double MaxDifference = 0.001;

    [GoldenImageFact]
    public void FamilyTreeRendersTheSameAfterPruning() => PruningDoesNotChangeTheDrawing("FamilyTree.pdf");

    [GoldenImageFact]
    public void TestDocumentRendersTheSameAfterPruning() => PruningDoesNotChangeTheDrawing("test.pdf");

    [GoldenImageFact]
    public void Pdf20RendersTheSameAfterPruning() => PruningDoesNotChangeTheDrawing("Pdf20.pdf");

    private static void PruningDoesNotChangeTheDrawing(string asset)
    {
        var name = Path.GetFileNameWithoutExtension(asset);

        var before = Render(Open(asset), name + "_before");

        var pruned = Open(asset);
        pruned.PruneUnusedResources();
        var after = Render(pruned, name + "_after");

        after.Length.Should().Be(before.Length);
        for (var page = 0; page < before.Length; page++)
        {
            PdfHelper.Diff(after[page], before[page], OutDir, name + "_" + (page + 1))
                .DiffValue.Should().BeLessThan(MaxDifference, "page {0} of {1} must draw the same", page + 1, asset);
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
        using var rasterized = PdfHelper.Rasterize(document);
        return PdfHelper.WriteImageCollection(rasterized.ImageCollection, OutDir, prefix).ToArray();
    }
}
