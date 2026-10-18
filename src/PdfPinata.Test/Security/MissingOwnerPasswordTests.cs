using System.IO;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   What the owner password of a saved document is when the caller set only a user password.
///   Issue #315.
/// </summary>
/// <remarks>
///   It used to be the user password: PDFsharp's <c>PrepareEncryption</c> used the user password
///   twice when no owner password was given. Anyone who could open the document then opened it as
///   its owner, and every permission it was saved with was written and meant nothing. A document
///   read with its owner password lost that password the same way, because the reader kept which
///   password had validated and not the password itself.
/// </remarks>
public class MissingOwnerPasswordTests
{
    private const string OwnerItWasReadWith = "test123";
    private const string NewUserPassword = "new-user";

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]  // AES-256, /R 5
    [Arguments("protected-sodapdf.pdf")]  // AES-256, /R 6
    [Arguments("protected-adobe.pdf")]    // AES-128, /R 4, printing and modifying restricted
    [Arguments("protected-ilovepdf.pdf")] // RC4-128, /R 3
    public void ANewUserPasswordAloneDoesNotOpenAReadDocumentAsItsOwner(string fileName)
    {
        var saved = SavedWithNewUserPassword(fileName);

        using var reopened = Pdf.IO.PdfReader.Open(saved, NewUserPassword, PdfDocumentOpenMode.Import);

        reopened.SecuritySettings.HasOwnerPermissions.Should().BeFalse(
            "the document had an owner password, and the caller entered it to open the document");
    }

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]
    [Arguments("protected-sodapdf.pdf")]
    [Arguments("protected-adobe.pdf")]
    [Arguments("protected-ilovepdf.pdf")]
    public void ItCannotBeModifiedWithTheNewUserPassword(string fileName)
    {
        var saved = SavedWithNewUserPassword(fileName);

        var open = () => Pdf.IO.PdfReader.Open(saved, NewUserPassword, PdfDocumentOpenMode.Modify);

        open.Should().Throw<PdfReaderException>().WithMessage("*owner password is required*");
    }

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]
    [Arguments("protected-sodapdf.pdf")]
    [Arguments("protected-adobe.pdf")]
    [Arguments("protected-ilovepdf.pdf")]
    public void TheOwnerPasswordItWasReadWithStillOpensItAsItsOwner(string fileName)
    {
        var saved = SavedWithNewUserPassword(fileName);

        using var reopened = Pdf.IO.PdfReader.Open(saved, OwnerItWasReadWith, PdfDocumentOpenMode.Modify);

        reopened.SecuritySettings.HasOwnerPermissions.Should().BeTrue(
            "changing only the user password must not change who the owner is");
    }

    [Test]
    [Arguments("protected-qpdf-r5.pdf")]
    [Arguments("protected-sodapdf.pdf")]
    [Arguments("protected-adobe.pdf")]
    [Arguments("protected-ilovepdf.pdf")]
    public void ANewOwnerPasswordSetOutrightReplacesIt(string fileName)
    {
        var saved = SavedWithNewUserPassword(fileName, owner: "new-owner");

        using (var reopened = Pdf.IO.PdfReader.Open(saved, "new-owner", PdfDocumentOpenMode.Modify))
            reopened.SecuritySettings.HasOwnerPermissions.Should().BeTrue();

        saved.Position = 0;
        var open = () => Pdf.IO.PdfReader.Open(saved, OwnerItWasReadWith, PdfDocumentOpenMode.Import);
        open.Should().Throw<PdfReaderException>().WithMessage("*password is invalid*");
    }

    [Test]
    public void AReadDocumentGivenOnlyASecurityLevelIsSavedUnderTheOwnerPasswordItWasReadWith()
    {
        // No password at all used to be refused here, because the one the caller opened it with had
        // not been kept. Now it is the owner password of what is saved, and the user password is empty.
        Stream saved;
        using (var document = Pdf.IO.PdfReader.Open(PathHelper.GetInstance().GetAssetPath("protected-adobe.pdf"),
                   OwnerItWasReadWith, PdfDocumentOpenMode.Modify))
        {
            document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            saved = new MemoryStream();
            document.Save(saved, false);
            saved.Position = 0;
        }

        using (var asReader = Pdf.IO.PdfReader.Open(saved, PdfDocumentOpenMode.Import))
            asReader.SecuritySettings.HasOwnerPermissions.Should().BeFalse();

        saved.Position = 0;
        using var asOwner = Pdf.IO.PdfReader.Open(saved, OwnerItWasReadWith, PdfDocumentOpenMode.Modify);
        asOwner.SecuritySettings.HasOwnerPermissions.Should().BeTrue();
    }

    [Test]
    [Arguments(PdfDocumentSecurityLevel.Encrypted40Bit)]  // RC4, /R 2
    [Arguments(PdfDocumentSecurityLevel.Encrypted128Bit)] // RC4, /R 3
    public void ANewDocumentGivenOnlyAUserPasswordIsNotOpenedAsItsOwnerByIt(PdfDocumentSecurityLevel level)
    {
        var saved = New(level, user: "open-me", owner: null);

        using var reopened = Pdf.IO.PdfReader.Open(saved, "open-me", PdfDocumentOpenMode.Import);

        reopened.SecuritySettings.HasOwnerPermissions.Should().BeFalse(
            "the owner password is one nobody was given, not the user password a second time");
    }

    [Test]
    [Arguments(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [Arguments(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void TheSamePasswordTwiceIsRefusedWhenItWouldRestrictAnything(PdfDocumentSecurityLevel level)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.SecuritySettings.DocumentSecurityLevel = level;
        document.SecuritySettings.UserPassword = "same";
        document.SecuritySettings.OwnerPassword = "same";
        document.SecuritySettings.PermitPrint = false;

        var save = () => document.Save(new MemoryStream(), false);

        // Anyone given that password would open the document as its owner, where no permission applies.
        document.CanSave().CanSave.Should().BeFalse();
        save.Should().Throw<PdfPinataException>().WithMessage("*owner password*user password*");
    }

    [Test]
    public void TheSamePasswordTwiceIsAllowedWhenNothingIsRestricted()
    {
        // Nothing is lost by it: there is no permission for owner access to lift.
        var saved = New(PdfDocumentSecurityLevel.Encrypted128Bit, user: "same", owner: "same");

        using var reopened = Pdf.IO.PdfReader.Open(saved, "same", PdfDocumentOpenMode.Import);

        reopened.SecuritySettings.HasOwnerPermissions.Should().BeTrue();
    }

    [Test]
    public void AReadDocumentGivenItsOwnerPasswordAsItsUserPasswordIsRefused()
    {
        // protected-adobe.pdf restricts printing and modifying, and test123 is the owner password it
        // keeps; giving every reader that password would lift both.
        using var document = Pdf.IO.PdfReader.Open(PathHelper.GetInstance().GetAssetPath("protected-adobe.pdf"),
            OwnerItWasReadWith, PdfDocumentOpenMode.Modify);
        document.SecuritySettings.UserPassword = OwnerItWasReadWith;

        document.CanSave().CanSave.Should().BeFalse();
    }

    [Test, Timeout(30000)]
    public async Task AProviderGivingTheUserPasswordAgainIsRefusedRatherThanAskedForever()
    {
        // Before #315 the user password of a document given no owner password was its owner
        // password too, so this opened it for modification. Now it is refused, and a provider that
        // only knows the one password would be asked again for as long as it answered.
        var saved = New(PdfDocumentSecurityLevel.Encrypted128Bit, user: "open-me", owner: null);

        var open = () => Interruptibly.Run(() => Pdf.IO.PdfReader.Open(saved, PdfDocumentOpenMode.Modify,
            args => args.Password = "open-me"));

        await open.Should().ThrowAsync<PdfReaderException>().WithMessage("*owner password is required*");
    }

    private static MemoryStream New(PdfDocumentSecurityLevel level, string user, string owner)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.SecuritySettings.DocumentSecurityLevel = level;
        if (user != null)
            document.SecuritySettings.UserPassword = user;
        if (owner != null)
            document.SecuritySettings.OwnerPassword = owner;

        var saved = new MemoryStream();
        document.Save(saved, false);
        saved.Position = 0;
        return saved;
    }

    private static MemoryStream SavedWithNewUserPassword(string fileName, string owner = null)
    {
        using var document = Pdf.IO.PdfReader.Open(PathHelper.GetInstance().GetAssetPath(fileName),
            OwnerItWasReadWith, PdfDocumentOpenMode.Modify);
        document.SecuritySettings.UserPassword = NewUserPassword;
        if (owner != null)
            document.SecuritySettings.OwnerPassword = owner;

        var saved = new MemoryStream();
        document.Save(saved, false);
        saved.Position = 0;
        return saved;
    }
}
