using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
/// What happens to an AES-encrypted document on a platform whose runtime provides no AES, which is
/// .NET on WebAssembly: <c>Aes.Create()</c> answers an instance there, and asking it for an encryptor
/// or a decryptor throws a <see cref="PlatformNotSupportedException"/>, with a message that names
/// neither the document's encryption nor a way round it. Both that and a runtime whose
/// <c>Aes.Create()</c> throws outright are simulated.
/// </summary>
/// <remarks>
/// The refusal is simulated through the security handler's one AES factory, set for the running
/// test's flow alone, so the other encrypted-document tests running beside these are untouched.
/// The qpdf document opens with <c>user</c> or <c>owner</c>, and the other two with <c>test123</c>,
/// which is the owner password of the revision 6 one and the user password of the revision 5 one.
/// The revision 5 one therefore cannot be opened to modify, and is only read.
/// </remarks>
public class AesUnavailableTests
{
    [Test]
    [Arguments("aes-r4-128bit-cleartext-metadata-qpdf.pdf", "user", "AES-128")] // /V 4 /R 4
    [Arguments("protected-pdfencrypt.pdf", "test123", "AES-256")]               // /V 5 /R 5
    [Arguments("protected-sodapdf.pdf", "test123", "AES-256")]                  // /V 5 /R 6
    public void ReadingAnAesDocumentWhereThereIsNoAesNamesTheEncryption(string file, string password, string encryption)
    {
        using var _ = AesRefusal.Begin();

        var ex = FluentActions.Invoking(() => Pdf.IO.PdfReader.Open(AssetStream(file), password, PdfDocumentOpenMode.Import))
            .Should().ThrowExactly<NotSupportedException>().Which;

        ex.Message.Should().StartWith(encryption + " encryption is not supported on this platform, ");
        ex.Message.Should().Contain("RC4");
        ex.InnerException.Should().BeOfType<PlatformNotSupportedException>();
    }

    /// <summary>
    /// What the browser really does: .NET 10 on WebAssembly answers <c>Aes.Create()</c> with an
    /// instance, and refuses only when an encryptor or decryptor is asked of it - which a document
    /// reaches first while validating its password, at a call site of its own.
    /// </summary>
    [Test]
    [Arguments("aes-r4-128bit-cleartext-metadata-qpdf.pdf", "user", "AES-128")] // /V 4 /R 4
    [Arguments("protected-pdfencrypt.pdf", "test123", "AES-256")]               // /V 5 /R 5
    [Arguments("protected-sodapdf.pdf", "test123", "AES-256")]                  // /V 5 /R 6
    public void ReadingAnAesDocumentWhereAesRefusesItsTransformsNamesTheEncryption(string file, string password, string encryption)
    {
        using var _ = AesRefusal.BeginAtTransform();

        var ex = FluentActions.Invoking(() => Pdf.IO.PdfReader.Open(AssetStream(file), password, PdfDocumentOpenMode.Import))
            .Should().ThrowExactly<NotSupportedException>().Which;

        ex.Message.Should().StartWith(encryption + " encryption is not supported on this platform, ");
        ex.InnerException.Should().BeOfType<PlatformNotSupportedException>();
    }

    [Test]
    [Arguments("aes-r4-128bit-cleartext-metadata-qpdf.pdf", "owner", "AES-128")]
    [Arguments("protected-sodapdf.pdf", "test123", "AES-256")]
    public void SavingAnAesDocumentWhereThereIsNoAesNamesTheEncryption(string file, string password, string encryption)
    {
        using var _ = AesRefusal.Begin();

        // A document is saved after opening it to modify, and opening is where its objects are
        // decrypted - so that is where the refusal comes, before there is anything to save.
        var ex = FluentActions.Invoking(() =>
            {
                var document = Pdf.IO.PdfReader.Open(AssetStream(file), password, PdfDocumentOpenMode.Modify);
                document.Save(new MemoryStream());
            })
            .Should().ThrowExactly<NotSupportedException>().Which;

        ex.Message.Should().StartWith(encryption + " encryption is not supported on this platform, ");
    }

    [Test]
    [Arguments("aes-r4-128bit-cleartext-metadata-qpdf.pdf", "owner")]
    [Arguments("protected-sodapdf.pdf", "test123")]
    public void SavingADocumentAlreadyDecryptedNeedsNoAes(string file, string password)
    {
        var document = Pdf.IO.PdfReader.Open(AssetStream(file), password, PdfDocumentOpenMode.Modify);
        var pageCount = document.PageCount;

        // This library writes RC4 alone, so nothing after the reader asks for AES.
        using var saved = new MemoryStream();
        using (AesRefusal.Begin())
            document.Save(saved, false);

        saved.Position = 0;
        Pdf.IO.PdfReader.Open(saved, PdfDocumentOpenMode.Import).PageCount.Should().Be(pageCount);
    }

    private static MemoryStream AssetStream(string file) => new(StandardSecurityAlgorithmTests.Asset(file));
}
