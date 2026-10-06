using System.IO;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PdfPinata.Test;

public class PdfReader
{
    [Test]
    public void Should_beAbleToReadExistingPdf_When_inputIsStream()
    {
        var root = Path.GetDirectoryName(GetType().GetTypeInfo().Assembly.Location);
        // ReSharper disable once AssignNullToNotNullAttribute
        var existingPdfPath = Path.Combine(root, "Assets", "FamilyTree.pdf");

        var fs = File.OpenRead(existingPdfPath);
        Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);
        fs.Dispose();

        true.Should().BeTrue();
    }
}
