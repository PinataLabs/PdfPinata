using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   A document read from an encrypted file and saved without saying what its security should be.
///   Issue #318.
/// </summary>
/// <remarks>
///   A document read from a file has a security level of None whatever the file had, and a save at
///   None drops /Encrypt and writes every object in clear - so changing a protected file and saving
///   it removed the protection, and nothing said so. Keeping the encryption instead is not always
///   possible: the writer has RC4 alone, and for AES-256 the user password cannot be recovered from
///   the owner password. So the save is refused until the caller chooses: a password to keep it
///   encrypted, or <see cref="PdfDocumentSecurityLevel.None"/> to remove the protection.
/// </remarks>
public class ReadEncryptedSaveTests
{
    private const string Password = "test123";

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]  // AES-256, /V 5 /R 5
    [Arguments("protected-sodapdf.pdf")]  // AES-256, /V 5 /R 6
    [Arguments("protected-adobe.pdf")]    // AES-128, /V 4 /R 4
    [Arguments("protected-ilovepdf.pdf")] // RC4-128, /V 2 /R 3
    public void SavingWithoutChoosingTheSecurityIsRefused(string fileName)
    {
        var document = Read(fileName);

        var save = () => document.Save(new MemoryStream(), false);

        save.Should().Throw<PdfPinataException>()
            .WithMessage("*encrypted*UserPassword*PdfDocumentSecurityLevel.None*");
    }

    // Choosing None, and choosing a password, are NewPasswordOnReadDocumentTests'.

    [Test]
    public void ARefusedSaveToAPathLeavesTheFileThereAlone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"read-encrypted-{Guid.NewGuid():N}.pdf");
        File.Copy(PathHelper.GetInstance().GetAssetPath("protected-ilovepdf.pdf"), path);
        try
        {
            var original = File.ReadAllBytes(path);
            var document = Pdf.IO.PdfReader.Open(path, Password, PdfDocumentOpenMode.Modify);

            var save = () => document.Save(path);

            // Saving over the file it was read from is the usual way to edit one in place, and the
            // stream that save opens empties the file. Refused after that, it would leave nothing.
            save.Should().Throw<PdfPinataException>();
            File.ReadAllBytes(path).Should().Equal(original);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static PdfDocument Read(string fileName) =>
        Pdf.IO.PdfReader.Open(PathHelper.GetInstance().GetAssetPath(fileName), Password, PdfDocumentOpenMode.Modify);
}
