using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.Revisions;

namespace PdfPinata.Test.IO;

/// <summary>
///   An incremental update cannot touch the header, so a revision that needs a later PDF version than
///   the header names has to say so in the catalog's <c>/Version</c> (ISO 32000-1 7.7.2), and a reader
///   has to take the later of the two. Neither happened: the in-memory version went up, the appended
///   bytes said nothing, and the file reopened at the header's version. Issue #395.
/// </summary>
public class IncrementalSaveVersionTests
{
    [Test]
    public void ARevisionThatRaisesTheVersionReopensAtTheRaisedVersion()
    {
        var original = Pdf14Document();

        var updated = AppendChange(original, document => document.Pages[0].UserUnit = 2);

        Saved.Open(updated, PdfDocumentOpenMode.Import).Version.Should().Be(16,
            "a /UserUnit needs PDF 1.6, and the revision that added it has to say so");
    }

    [Test]
    public void TheRaisedVersionIsDeclaredInTheAppendedCatalogAndTheHeaderIsLeftAlone()
    {
        var original = Pdf14Document();

        var updated = AppendChange(original, document => document.Pages[0].UserUnit = 2);

        Encoding.ASCII.GetString(updated, 0, 8).Should().Be("%PDF-1.4");
        Appended(updated, original.Length).Should().Contain("/Version /1.6");
    }

    [Test]
    public void ARevisionThatNeedsNoLaterVersionDeclaresNone()
    {
        var original = Pdf14Document();

        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        Appended(updated, original.Length).Should().NotContain("/Version");
        Saved.Open(updated, PdfDocumentOpenMode.Import).Version.Should().Be(14);
    }

    [Test]
    public void TheReaderTakesTheCatalogsVersionWhenItIsLaterThanTheHeaders()
    {
        var file = WithHeader("%PDF-1.4", RawPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /Version /1.6 >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>"
        ]));

        Saved.Open(file, PdfDocumentOpenMode.Import).Version.Should().Be(16);
    }

    [Test]
    public void TheReaderKeepsTheHeadersVersionWhenTheCatalogsIsEarlier()
    {
        var file = RawPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /Version /1.4 >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>"
        ]);

        Saved.Open(file, PdfDocumentOpenMode.Import).Version.Should().Be(17,
            "the header wins when it names the later version (ISO 32000-1 7.7.2)");
    }

    private static byte[] Pdf14Document()
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Version.Should().Be(14);
        var bytes = Saved.Bytes(document);
        Encoding.ASCII.GetString(bytes, 0, 8).Should().Be("%PDF-1.4");
        return bytes;
    }

    /// <summary>The same document under another header of the same length, so every offset still holds.</summary>
    private static byte[] WithHeader(string header, byte[] file)
    {
        var bytes = (byte[])file.Clone();
        Encoding.ASCII.GetBytes(header).CopyTo(bytes, 0);
        return bytes;
    }
}
