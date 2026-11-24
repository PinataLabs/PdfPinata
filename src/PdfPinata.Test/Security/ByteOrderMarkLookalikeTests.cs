using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   The /O and /U entries of an encryption dictionary and the document identifier are bytes, not
///   text, and any of them can open with FE FF or FF FE by chance. The lexer takes a string that
///   opens that way for UTF-16 and decodes it, so the security handler has to put the bytes back
///   rather than read the decoded text, or the password is checked against the wrong key (#416).
/// </summary>
/// <remarks>
///   RC4 output opens with one of the two marks about once in 32768, and a document carries both
///   entries, so roughly one RC4 document in 16000 that PdfPinata wrote could not be opened again.
///   That is what failed <c>LazyStreamTests.AStreamCopiedIntoAnEncryptedDocumentIsReadBackAsItWas</c>
///   now and then. The owner passwords here were found by searching for one whose entries open
///   with each mark, under <see cref="Identifier" />.
/// </remarks>
public class ByteOrderMarkLookalikeTests
{
    private const string Identifier = "0123456789abcdef";
    private const string UserPassword = "user";

    [Test]
    [Arguments("owner3170", "/U", 0xFE, 0xFF)]
    [Arguments("owner39045", "/U", 0xFF, 0xFE)]
    [Arguments("owner71151", "/O", 0xFE, 0xFF)]
    [Arguments("owner5034", "/O", 0xFF, 0xFE)]
    public void AnEntryOpeningWithAByteOrderMarkIsReadAsTheBytesItIs(string ownerPassword, string key, int first,
        int second)
    {
        var bytes = Encrypted(Identifier, ownerPassword);
        var entries = new StandardSecurity(bytes, UserPassword).Entries;
        var entry = key == "/U" ? entries.User : entries.Owner;
        entry.Take(2).Should().Equal([(byte)first, (byte)second], "the point of this test is an entry that opens so");

        using var asUser = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), UserPassword, PdfDocumentOpenMode.Import);
        using var asOwner = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), ownerPassword, PdfDocumentOpenMode.Import);

        asUser.SecuritySettings.HasOwnerPermissions.Should().BeFalse();
        asOwner.SecuritySettings.HasOwnerPermissions.Should().BeTrue();
    }

    [Test]
    public void AnIdentifierOpeningWithAByteOrderMarkIsReadAsTheBytesItIs()
    {
        var bytes = Encrypted("þÿ" + Identifier[2..], "owner");
        new StandardSecurity(bytes, UserPassword).Entries.Id.Take(2).Should().Equal([(byte)0xFE, (byte)0xFF]);

        using var document = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), UserPassword, PdfDocumentOpenMode.Import);

        document.PageCount.Should().Be(1);
    }

    [Test]
    public void ADocumentReadWithSuchAnIdentifierIsEncryptedWithIt()
    {
        var plain = new PdfDocument();
        plain.AddPage();
        plain.Internals.FirstDocumentID = "þÿ" + Identifier[2..];
        using var read = Saved.Open(Saved.Bytes(plain));
        read.SecuritySettings.UserPassword = UserPassword;

        var bytes = Saved.Bytes(read);

        var security = new StandardSecurity(bytes, UserPassword);
        security.Entries.Id.Take(2).Should().Equal([(byte)0xFE, (byte)0xFF]);
        security.DerivedKeyMatchesTheDocument.Should().BeTrue("the key is derived from the identifier as written");
        using var reopened = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), UserPassword, PdfDocumentOpenMode.Import);
        reopened.PageCount.Should().Be(1);
    }

    private static byte[] Encrypted(string identifier, string ownerPassword)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Internals.FirstDocumentID = identifier;
        document.SecuritySettings.UserPassword = UserPassword;
        document.SecuritySettings.OwnerPassword = ownerPassword;
        return Saved.Bytes(document);
    }
}
