using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using System.IO;
using System.Text;
using TUnit.Core;

namespace PdfPinata.Test.IO;

public class PdfVersionTests
{
    [Test]
    [Arguments("%PDF-1.0\n", 10)]
    [Arguments("%PDF-1.4\n", 14)]
    [Arguments("%PDF-1.7\n", 17)]
    [Arguments("%PDF-2.0\n", 20)]
    // Acrobat also accepts a PostScript style header with the PDF version embedded.
    [Arguments("%!PS-Adobe-3.0 PDF-2.0\n", 20)]
    public void TestPdfFile_returnsTheVersionOfTheHeader(string header, int expected)
    {
        Pdf.IO.PdfReader.TestPdfFile(Encoding.ASCII.GetBytes(header)).Should().Be(expected);
    }

    [Test]
    [Arguments("Definitely not a PDF")]
    [Arguments("%PDF-0.9\n")]
    [Arguments("%PDF-1.A\n")]
    [Arguments("%PDF\n")]
    [Arguments("")]
    public void TestPdfFile_returnsZeroForANonPdfHeader(string header)
    {
        Pdf.IO.PdfReader.TestPdfFile(Encoding.ASCII.GetBytes(header)).Should().Be(0);
    }

    [Test]
    public void TestPdfFile_returnsZeroForNoBytesAtAll()
    {
        Pdf.IO.PdfReader.TestPdfFile((byte[])null).Should().Be(0);
    }

    [Test]
    public void Should_beAbleToReadAPdf20Document()
    {
        using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath("Pdf20.pdf"));
        var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);

        inputDocument.Should().NotBeNull();
        inputDocument.Version.Should().Be(20);
        inputDocument.PageCount.Should().BeGreaterThan(0);
    }

    [Test]
    public void Should_preserveThePdf20HeaderWhenSavingADocument()
    {
        using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath("Pdf20.pdf"));
        var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Modify);

        using var ms = new MemoryStream();
        inputDocument.Save(ms, false);

        Encoding.ASCII.GetString(ms.ToArray(), 0, 8).Should().Be("%PDF-2.0");
    }

    [Test]
    public void Version_acceptsPdf20()
    {
        var document = new PdfDocument { Version = 20 };
        document.Version.Should().Be(20);
    }
}
