using System.Collections.Generic;
using System.IO;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace PdfPinata.Test;

public class Merge
{
    private readonly ITestOutputHelper _output;
        
    public Merge(ITestOutputHelper output)
    {
        _output = output;
    }
        
    [Fact]
    public void CanMerge2Documents()
    {
        var pdf1Path =  PathHelper.GetInstance().GetAssetPath("FamilyTree.pdf");
        var pdf2Path = PathHelper.GetInstance().GetAssetPath("test.pdf");

        var outputDocument = MergeDocuments(new[] { pdf1Path, pdf2Path });

        var outFilePath = CreateOutFilePath("merge.pdf");
        outputDocument.Save(outFilePath);

        using var merged = Pdf.IO.PdfReader.Open(outFilePath, PdfDocumentOpenMode.Import);
        Assert.Equal(PageCountOf(pdf1Path) + PageCountOf(pdf2Path), merged.PageCount);
    }

    private static int PageCountOf(string pdfPath)
    {
        using var document = Pdf.IO.PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }

    [Fact]
    public void CanConsolidateImageDataInDocument()
    {
        var doc1 = CreateTestDocumentWithImage("lenna.png");
        var doc2 = CreateTestDocumentWithImage("frog-and-toad.jpg");

        var pdf1Path = CreateOutFilePath("image-doc1.pdf");
        doc1.Save(pdf1Path);

        var pdf2Path = CreateOutFilePath("image-doc2.pdf");
        doc2.Save(pdf2Path);

        var pdfPathsForMerge = Enumerable.Range(1, 50).SelectMany(_ => new[] { pdf1Path, pdf2Path });
        var outputDocument = MergeDocuments(pdfPathsForMerge);
            
        var mergedFilePath = CreateOutFilePath("images-merged.pdf");
        outputDocument.Save(mergedFilePath);

        outputDocument.ConsolidateImages();
        var consolidatedFilePath = CreateOutFilePath("images-merged-consolidated.pdf");
        outputDocument.Save(consolidatedFilePath);

        var mergedLength = new FileInfo(mergedFilePath).Length;
        var consolidatedLength = new FileInfo(consolidatedFilePath).Length;
        Assert.True(consolidatedLength < mergedLength / 4);
    }

    private static PdfDocument MergeDocuments(IEnumerable<string> pdfPaths)
    {
        var outputDocument = new PdfDocument();

        foreach (var pdfPath in pdfPaths)
        {
            using var fs = File.OpenRead(pdfPath);
            var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                
            var count = inputDocument.PageCount;
            for (var idx = 0; idx < count; idx++)
            {
                var page = inputDocument.Pages[idx];
                outputDocument.AddPage(page);
            }
        }

        return outputDocument;
    }

    private static string CreateOutFilePath(string filename)
    {
        var outFilePath = Path.Combine(PathHelper.GetInstance().RootDir, "Out", filename);
        var dir = Path.GetDirectoryName(outFilePath);
        if (!Directory.Exists(dir))
        {
            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(dir);
        }

        return outFilePath;
    }

    private static PdfDocument CreateTestDocumentWithImage(string imageFilename)
    {
        var document = new PdfDocument();

        var pageNewRenderer = document.AddPage();
        var renderer = XGraphics.FromPdfPage(pageNewRenderer);
        var textFormatter = new XTextFormatter(renderer);
            
        var layout = new XRect(12, 12, 400, 50);
        textFormatter.DrawString(imageFilename, new XFont("Arial", 12), XBrushes.Black, layout);
        renderer.DrawImage(XImage.FromFile(PathHelper.GetInstance().GetAssetPath(imageFilename)), new XPoint(12, 100));
            
        return document;
    }
}
