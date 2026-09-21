using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace PdfPinata.Test.IO;

/// <summary>
///   The case empira/PDFsharp#275 was raised about: many documents merged into one by importing
///   their pages, each bringing its own copy of the same fonts and images. These measure what
///   <see cref="PdfDocumentOptions.DeduplicateResources" /> saves on that, and render the merged
///   file with and without it to show that it draws the same.
/// </summary>
[Collection(RasterizingCollection.Name)]
public class ResourceDeduplicationRenderingTests(ITestOutputHelper output)
{
    private const string OutDir = "Out/ResourceDeduplication";

    /// <summary>
    ///   Two runs of the same rasterizer over the same drawing agree exactly, so anything above
    ///   the noise floor of the comparison is a page that came out different.
    /// </summary>
    private const double MaxDifference = 0.001;

    private const int Copies = 20;

    [Fact]
    public void MergingOneDocumentManyTimesWeighsLittleMoreThanOneCopy()
    {
        var single = ADocumentWithTextAndAnImage();

        var plain = SizeOf(Merged(Enumerable.Repeat(single, Copies), deduplicate: false));
        var deduplicated = SizeOf(Merged(Enumerable.Repeat(single, Copies), deduplicate: true));

        output.WriteLine($"one copy {single.Length:N0} bytes; {Copies} copies merged {plain:N0}; deduplicated {deduplicated:N0}");

        plain.Should().BeGreaterThan(Copies * single.Length * 9 / 10, "without the option every copy is written out");

        // One copy of everything, and a page dictionary and little else for each further copy.
        deduplicated.Should().BeLessThan(single.Length * 2);
    }

    [GoldenImageFact]
    public void AMergedDocumentDrawsTheSameDeduplicated()
    {
        var single = ADocumentWithTextAndAnImage();
        DrawsTheSame(Enumerable.Repeat(single, 3).ToList(), "generated");
    }

    [GoldenImageFact]
    public void MergedAssetsDrawTheSameDeduplicated()
    {
        var assets = new[] { "FamilyTree.pdf", "test.pdf", "Pdf20.pdf" }
            .Select(asset => File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(asset)))
            .ToList();

        var documents = assets.Concat(assets).ToList();
        var plain = SizeOf(Merged(documents, deduplicate: false));
        var deduplicated = SizeOf(Merged(documents, deduplicate: true));
        output.WriteLine($"assets merged twice {plain:N0} bytes; deduplicated {deduplicated:N0}");
        deduplicated.Should().BeLessThan(plain);

        DrawsTheSame(documents, "assets");
    }

    private void DrawsTheSame(IReadOnlyList<byte[]> documents, string name)
    {
        var before = Render(Merged(documents, deduplicate: false), name + "_plain");
        var after = Render(Merged(documents, deduplicate: true), name + "_deduplicated");

        after.Length.Should().Be(before.Length);
        for (var page = 0; page < before.Length; page++)
        {
            PdfHelper.Diff(after[page], before[page], OutDir, name + "_" + (page + 1))
                .DiffValue.Should().BeLessThan(MaxDifference, "page {0} must draw the same", page + 1);
        }
    }

    /// <summary>
    ///   A page of text in an embedded, subsetted font and a photograph: the two things a merged
    ///   report repeats in every file it was made of.
    /// </summary>
    private static byte[] ADocumentWithTextAndAnImage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 14);
            gfx.DrawString("The same letterhead, on every document of the batch.", font, XBrushes.Black, 40, 60);
            gfx.DrawString("0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ", font, XBrushes.Black, 40, 90);
            gfx.DrawImage(XImage.FromFile(PathHelper.GetInstance().GetAssetPath("lenna.png")), 40, 120, 200, 200);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static PdfDocument Merged(IEnumerable<byte[]> documents, bool deduplicate)
    {
        var merged = new PdfDocument();
        merged.Options.DeduplicateResources = deduplicate;
        foreach (var bytes in documents)
        {
            using var stream = new MemoryStream(bytes);
            var source = Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Import);
            foreach (var page in source.Pages)
                merged.AddPage(page);
        }

        return merged;
    }

    private static long SizeOf(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.Length;
    }

    private static string[] Render(PdfDocument document, string prefix)
    {
        using var rasterized = PdfHelper.Rasterize(document);
        return PdfHelper.WriteImageCollection(rasterized.ImageCollection, OutDir, prefix).ToArray();
    }
}
