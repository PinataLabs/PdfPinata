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
        // ReSharper disable once AssignNullToNotNullAttribute
        var existingPdfPath = Path.Combine(root, "Assets", "FamilyTree.pdf");

        var fs = File.OpenRead(existingPdfPath);
        Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);
        fs.Dispose();

        Assert.True(true);
    }
}
