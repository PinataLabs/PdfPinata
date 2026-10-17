using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   An encrypted document opened for modification, given a new user password and saved. Issue #304.
/// </summary>
/// <remarks>
///   The issue was filed against AES-256, and the cipher was never the cause: what the failing files
///   had in common was a cross-reference stream. The trailer of such a file is that stream's
///   dictionary, parsed before the entries the stream holds are in the table, so its /Encrypt was a
///   stand-in reference belonging to no document, and the reader hung the security handler on it.
///   Modify's compaction walks only references that belong to the document, so it dropped the
///   handler from the table; the handler kept the number it was read with, renumbering gave that
///   number to another object, and the save wrote no encryption dictionary at all and a trailer
///   whose /Encrypt was the catalog. Reopening then refused a /Filter that was not /Standard.
///   <para>
///     Saving without a new password never showed it, because that save writes the document
///     unencrypted and drops /Encrypt; and a classic trailer's references are the table's own, which
///     is why the RC4 file from iLovePDF never failed either.
///   </para>
/// </remarks>
public class NewPasswordOnReadDocumentTests
{
    private const string OldPassword = "test123";
    private const string NewPassword = "a new one";

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]  // AES-256, /V 5 /R 5, cross-reference stream
    [Arguments("protected-sodapdf.pdf")]  // AES-256, /V 5 /R 6, cross-reference stream
    [Arguments("protected-adobe.pdf")]    // AES-128, /V 4 /R 4, cross-reference stream
    [Arguments("protected-ilovepdf.pdf")] // RC4-128, /V 2 /R 3, classic trailer: never failed
    public void TheNewPasswordOpensTheSavedDocumentAndItsContentSurvives(string fileName)
    {
        var content = ContentOf(Read(fileName));

        var reopened = Pdf.IO.PdfReader.Open(SavedWithNewPassword(Read(fileName)), NewPassword, PdfDocumentOpenMode.Import);

        ContentOf(reopened).Should().Equal(content);
    }

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]
    [Arguments("protected-sodapdf.pdf")]
    [Arguments("protected-adobe.pdf")]
    [Arguments("protected-ilovepdf.pdf")]
    public void TheOldPasswordNoLongerOpensIt(string fileName)
    {
        var saved = SavedWithNewPassword(Read(fileName));

        var open = () => Pdf.IO.PdfReader.Open(saved, OldPassword, PdfDocumentOpenMode.Import);

        open.Should().Throw<PdfReaderException>().WithMessage("*password is invalid*");
    }

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]
    [Arguments("protected-sodapdf.pdf")]
    [Arguments("protected-adobe.pdf")]
    [Arguments("protected-ilovepdf.pdf")]
    public void SavingWithoutANewPasswordStillWorks(string fileName)
    {
        var content = ContentOf(Read(fileName));

        var saved = new MemoryStream();
        Read(fileName).Save(saved, false);
        saved.Position = 0;

        // A password given for a document that is not encrypted is ignored, so this holds whether
        // or not the save kept the encryption.
        ContentOf(Pdf.IO.PdfReader.Open(saved, OldPassword, PdfDocumentOpenMode.Import)).Should().Equal(content);
    }

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]
    [Arguments("protected-sodapdf.pdf")]
    [Arguments("protected-adobe.pdf")]
    public void TheSavedDictionaryDescribesOnlyTheEncryptionItWasWrittenWith(string fileName)
    {
        var saved = SavedWithNewPassword(Read(fileName));

        var dictionary = Pdf.IO.PdfReader.Open(saved, NewPassword, PdfDocumentOpenMode.Import).SecurityHandler;

        // The writer encrypts with RC4 alone. The crypt filters are read only at /V 4 and 5, and
        // /OE, /UE and /Perms only at revisions 5 and 6, so what the AES dictionary said of them
        // describes an encryption the saved file does not have.
        dictionary.Elements.GetInteger("/V").Should().Be(2);
        dictionary.Elements.Keys.Should().NotContain(
            ["/CF", "/StmF", "/StrF", "/EFF", "/OE", "/UE", "/Perms", "/EncryptMetadata"]);
    }

    [Test]
    public void TheCipherIsNotWhatMatters()
    {
        // RC4, written by this library, indexed by a cross-reference stream: no AES anywhere.
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.Black, 10, 10, 50, 50);
        document.Info.Title = "Indexed by a stream";
        document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        document.SecuritySettings.UserPassword = OldPassword;
        var written = new MemoryStream();
        document.Save(written, false);
        written.Position = 0;

        var saved = SavedWithNewPassword(Pdf.IO.PdfReader.Open(written, OldPassword, PdfDocumentOpenMode.Modify));
        var reopened = Pdf.IO.PdfReader.Open(saved, NewPassword, PdfDocumentOpenMode.Import);

        reopened.Info.Title.Should().Be("Indexed by a stream");
        ContentReader.ReadContent(reopened.Pages[0]).Count.Should().BeGreaterThan(0);
    }

    private static PdfDocument Read(string fileName) =>
        Pdf.IO.PdfReader.Open(PathHelper.GetInstance().GetAssetPath(fileName), OldPassword, PdfDocumentOpenMode.Modify);

    private static MemoryStream SavedWithNewPassword(PdfDocument document)
    {
        document.SecuritySettings.UserPassword = NewPassword;
        var saved = new MemoryStream();
        document.Save(saved, false);
        saved.Position = 0;
        return saved;
    }

    /// <summary>
    /// What every page draws: its operators and their operands, read out of the decrypted,
    /// decompressed content and written back out. A wrong key would leave nothing to parse, and a
    /// change to a string, a coordinate or a colour on any page would show.
    /// </summary>
    private static List<string> ContentOf(PdfDocument document)
    {
        var pages = new List<string>();
        foreach (var page in document.Pages)
            pages.Add(Encoding.Latin1.GetString(ContentReader.ReadContent(page).ToContent()));
        return pages;
    }
}
