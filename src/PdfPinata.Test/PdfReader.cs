using System.IO;
using System.Reflection;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test;

public class PdfReader
{
    [Fact]
    public void Should_beAbleToReadExistingPdf_When_inputIsStream()
    {
        var root = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
        var existingPdfPath = Path.Combine(root, "Assets", "FamilyTree.pdf");
// ReSharper disable once AssignNullToNotNullAttribute

        var fs = File.OpenRead(existingPdfPath);
        Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);
        fs.Dispose();

        Assert.True(true);
    }
}
