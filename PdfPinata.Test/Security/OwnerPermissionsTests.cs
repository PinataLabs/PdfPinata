using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   <see cref="PdfSecuritySettings.HasOwnerPermissions"/>, which says which of a document's two
///   passwords it was opened with.
/// </summary>
/// <remarks>
///   It never said anything. The field behind it was initialized to <c>true</c> and assigned
///   nowhere in the library, so the property answered "yes, owner" for every document however it
///   had been opened - including one opened with the user password, which is the only case anybody
///   would ask about. <c>ValidatePassword</c> has always worked the answer out and returned it as a
///   <see cref="PasswordValidity"/>; the reader used that to refuse a Modify and then dropped it.
///   <para>
///     Found by writing the demonstration app's Protect demo, whose second page reports this back
///     after a round trip and printed True under both passwords.
///   </para>
/// </remarks>
public class OwnerPermissionsTests
{
    const string User = "open-me";
    const string Owner = "owner-only";

    static MemoryStream Protected(string user = User, string owner = Owner)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        if (user != null)
            document.SecuritySettings.UserPassword = user;
        if (owner != null)
            document.SecuritySettings.OwnerPassword = owner;

        var buffer = new MemoryStream();
        document.Save(buffer, false);
        buffer.Position = 0;
        return buffer;
    }

    [Fact]
    public void TheOwnerPasswordBringsOwnerPermissions()
    {
        using var buffer = Protected();
        using var opened = Pdf.IO.PdfReader.Open(buffer, Owner, PdfDocumentOpenMode.Modify);

        opened.SecuritySettings.HasOwnerPermissions.Should().BeTrue();
    }

    [Fact]
    public void TheUserPasswordDoesNot()
    {
        using var buffer = Protected();
        using var opened = Pdf.IO.PdfReader.Open(buffer, User, PdfDocumentOpenMode.ReadOnly);

        opened.SecuritySettings.HasOwnerPermissions.Should().BeFalse(
            "the whole point of the property is to tell the two passwords apart");
    }

    [Fact]
    public void ADocumentWithOnlyAnOwnerPasswordOpensWithoutOneAndIsNotTheOwner()
    {
        // The arrangement most "protected" PDFs actually use: no user password, so anyone can
        // read it, and an owner password so the permissions cannot be lifted. Opening it with no
        // password at all validates as the *user*, empty, and must not confer owner rights.
        using var buffer = Protected(user: null);
        using var opened = Pdf.IO.PdfReader.Open(buffer, PdfDocumentOpenMode.ReadOnly);

        opened.SecuritySettings.HasOwnerPermissions.Should().BeFalse();
    }

    [Fact]
    public void ADocumentThatWasNeverEncryptedStillHasOwnerPermissions()
    {
        // Nothing to be shut out of, and the property is read by callers deciding whether they may
        // change something - so an unencrypted document has to answer yes rather than no.
        var document = new PdfDocument();
        document.AddPage();

        using var buffer = new MemoryStream();
        document.Save(buffer, false);
        buffer.Position = 0;

        using var opened = Pdf.IO.PdfReader.Open(buffer, PdfDocumentOpenMode.Modify);

        opened.SecuritySettings.HasOwnerPermissions.Should().BeTrue();
    }

    [Fact]
    public void ADocumentBeingWrittenHasOwnerPermissions()
    {
        // The document in hand is the one being created, so its creator is its owner. This is the
        // value the field was initialized to, and the only case in which that was ever right.
        new PdfDocument().SecuritySettings.HasOwnerPermissions.Should().BeTrue();
    }

    [Fact]
    public void ThePasswordProviderRouteReachesTheSameAnswer()
    {
        // The other way in. Whichever overload supplied the password, what it validated as has to
        // be recorded the same way.
        using var buffer = Protected();
        using var opened = Pdf.IO.PdfReader.Open(buffer, PdfDocumentOpenMode.ReadOnly,
            args => args.Password = User);

        opened.SecuritySettings.HasOwnerPermissions.Should().BeFalse();
    }

    [Fact]
    public void ThePermissionsThemselvesSurviveTheRoundTrip()
    {
        // The guard beside it: reading the flags back is what a caller does after checking
        // HasOwnerPermissions, and they have to be the flags that were written.
        var document = new PdfDocument();
        document.AddPage();
        document.SecuritySettings.UserPassword = User;
        document.SecuritySettings.OwnerPassword = Owner;
        document.SecuritySettings.PermitPrint = true;
        document.SecuritySettings.PermitExtractContent = false;
        document.SecuritySettings.PermitModifyDocument = false;

        using var buffer = new MemoryStream();
        document.Save(buffer, false);
        buffer.Position = 0;

        using var opened = Pdf.IO.PdfReader.Open(buffer, Owner, PdfDocumentOpenMode.Modify);

        opened.SecuritySettings.PermitPrint.Should().BeTrue();
        opened.SecuritySettings.PermitExtractContent.Should().BeFalse();
        opened.SecuritySettings.PermitModifyDocument.Should().BeFalse();
    }
}
