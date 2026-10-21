using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   The <c>/Contents</c> of a signature dictionary is never encrypted. Issue #325.
/// </summary>
/// <remarks>
///   ISO 32000-1 7.6.2 leaves it out because it is the signature value over the bytes of the file,
///   and the file's bytes include it. The security handler walked every string of every dictionary,
///   both reading and writing, so with RC4 the value was XOR-garbled on read, with AES it came
///   through only when the padding check on bytes that were never encrypted happened to fail, and a
///   save wrote it encrypted.
///   <para>
///     The three files were written by qpdf 12.4.2, which leaves a signature's /Contents in plain, from
///     one source: a signature field whose /V has /ByteRange, a 64-byte /Contents, a /Reason and a
///     /Name, and beside it a text annotation whose /Contents is an ordinary encrypted string. Both
///     passwords are given below; object streams were disabled, so every dictionary is encrypted on
///     its own. In the two AES files the last block of the signature value was then chosen so that,
///     run through the decryptor as though it were ciphertext, it ends in valid padding — the case in
///     which the old reading did not throw and so changed the value.
///   </para>
/// </remarks>
public class SignatureContentsEncryptionTests
{
    private const string UserPassword = "user325";
    // Modifying a document takes its owner password.
    private const string OwnerPassword = "owner325";
    private const string NewPassword = "a new one";
    private const string Reason = "Testing issue 325";
    private const string Note = "A note that is not a signature";

    [Test]
    [Arguments("signed-rc4-qpdf.pdf")]    // RC4-128, /V 2 /R 3
    [Arguments("signed-aes128-qpdf.pdf")] // AES-128, /V 4 /R 4
    [Arguments("signed-aes256-qpdf.pdf")] // AES-256, /V 5 /R 6
    public void TheSignatureValueReadsAsItStandsInTheFile(string fileName)
    {
        var path = PathHelper.GetInstance().GetAssetPath(fileName);

        var signature = PdfSignatures.InDocument(Pdf.IO.PdfReader.Open(path, UserPassword, PdfDocumentOpenMode.Import)).Single();

        signature.Contents.Should().Equal(ContentsInFile(File.ReadAllBytes(path)));
    }

    [Test]
    [Arguments("signed-rc4-qpdf.pdf")]
    [Arguments("signed-aes128-qpdf.pdf")]
    [Arguments("signed-aes256-qpdf.pdf")]
    public void EveryOtherStringIsStillDecrypted(string fileName)
    {
        var document = Pdf.IO.PdfReader.Open(PathHelper.GetInstance().GetAssetPath(fileName), UserPassword,
            PdfDocumentOpenMode.Import);

        var signature = PdfSignatures.InDocument(document).Single();
        signature.Reason.Should().Be(Reason);
        signature.SignerName.Should().Be("A. Signer");
        NoteOf(document).Should().Be(Note, "a /Contents without a /ByteRange beside it is no signature value");
    }

    [Test]
    [Arguments("signed-rc4-qpdf.pdf")]
    [Arguments("signed-aes128-qpdf.pdf")]
    [Arguments("signed-aes256-qpdf.pdf")]
    public void ASaveWritesTheSignatureValueInPlainAndEverythingElseEncrypted(string fileName)
    {
        var path = PathHelper.GetInstance().GetAssetPath(fileName);
        var document = Pdf.IO.PdfReader.Open(path, OwnerPassword, PdfDocumentOpenMode.Modify);
        document.SecuritySettings.UserPassword = NewPassword;

        using var output = new MemoryStream();
        document.Save(output, false);
        var saved = new StandardSecurity(output.ToArray(), NewPassword);

        saved.DerivedKeyMatchesTheDocument.Should()
            .BeTrue("this test can only judge the strings if it agrees with the document about the key");
        var signature = saved.ObjectNumberWhere("/ByteRange").Value;
        saved.RawObjectBytes(signature, "/Contents").Should().Equal(ContentsInFile(File.ReadAllBytes(path)));
        Latin1(saved.DecryptedObjectBytes(signature, "/Reason")).Should().Be(Reason);
        Latin1(saved.RawObjectBytes(signature, "/Reason")).Should().NotBe(Reason);

        var note = saved.ObjectNumberWhere("/Subtype\\s*/Text").Value;
        Latin1(saved.DecryptedObjectBytes(note, "/Contents")).Should().Be(Note);
    }

    [Test]
    [Arguments("signed-rc4-qpdf.pdf")]
    [Arguments("signed-aes128-qpdf.pdf")]
    [Arguments("signed-aes256-qpdf.pdf")]
    public void TheSavedDocumentReadsBackTheSameSignatureValue(string fileName)
    {
        var path = PathHelper.GetInstance().GetAssetPath(fileName);
        var document = Pdf.IO.PdfReader.Open(path, OwnerPassword, PdfDocumentOpenMode.Modify);
        document.SecuritySettings.UserPassword = NewPassword;
        using var output = new MemoryStream();
        document.Save(output, false);
        output.Position = 0;

        var reopened = Pdf.IO.PdfReader.Open(output, NewPassword, PdfDocumentOpenMode.Import);

        var signature = PdfSignatures.InDocument(reopened).Single();
        signature.Contents.Should().Equal(ContentsInFile(File.ReadAllBytes(path)));
        signature.Reason.Should().Be(Reason);
        NoteOf(reopened).Should().Be(Note);
    }

    /// <summary>The bytes between the angle brackets of the one /Contents written as a hexadecimal string beside /ByteRange.</summary>
    private static byte[] ContentsInFile(byte[] file)
    {
        var signature = Regex.Match(Latin1(file), @"/ByteRange[^>]*?/Contents\s*<([0-9A-Fa-f\s]*)>", RegexOptions.Singleline);
        signature.Success.Should().BeTrue("the signature dictionary is written with /ByteRange before /Contents");
        return StandardSecurity.FromHex(Regex.Replace(signature.Groups[1].Value, @"\s", ""));
    }

    private static string NoteOf(PdfDocument document) =>
        document.Pages[0].Annotations.OfType<PdfAnnotation>()
            .Single(annotation => annotation.Elements.GetName("/Subtype") == "/Text")
            .Contents;

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
