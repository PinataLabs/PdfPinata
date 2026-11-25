using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   The /O, /U, /OE, /UE and /Perms entries of an encryption dictionary and the document
///   identifier are bytes, not text, and any of them can open with FE FF or FF FE by chance. The
///   lexer takes a string that opens that way for UTF-16 and decodes it, so the security handler
///   has to put the bytes back rather than read the decoded text, or the password is checked
///   against the wrong key (#416).
/// </summary>
/// <remarks>
///   Random bytes open with one of the two marks once in 32768, and an RC4 document carries three
///   such strings, so about one RC4 document in 11000 that PdfPinata wrote could not be opened
///   again. An AES-256 document carries five. The AES-256 builder writes hex strings, which the
///   lexer decodes only when they open with FE FF, so only that mark is tested there. The owner
///   passwords and file keys here were found by searching for ones whose entries open with each
///   mark.
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

    [Test]
    [Arguments(22814, "/OE")]
    [Arguments(53824, "/UE")]
    [Arguments(89424, "/Perms")]
    public void AnAes256EntryOpeningWithAByteOrderMarkIsReadAsTheBytesItIs(int keyPrefix, string key)
    {
        var fileKey = Enumerable.Range(0, 32).Select(i => (byte)(0x40 + 3 * i)).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(fileKey, keyPrefix);
        var fixture = new CryptFilterDocument { Method = "/AESV3", Aes256FileKey = fileKey };
        var bytes = fixture.Build();
        Regex.Match(Encoding.Latin1.GetString(bytes), key + @"\s*<(\w{4})").Groups[1].Value.ToUpperInvariant()
            .Should().Be("FEFF", "the point of this test is an entry that opens so");
        var password = key == "/OE" ? CryptFilterDocument.OwnerPassword : "";

        using var document = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), password, PdfDocumentOpenMode.Import);

        document.SecuritySettings.HasOwnerPermissions.Should().Be(key == "/OE");
        document.Pages[0].Elements.GetDictionary("/Contents").Stream.Value.Should()
            .Equal(fixture.Content, "the page is decrypted with the file key the entry holds");
    }

    [Test]
    public void AnIdentifierOfAnOddNumberOfBytesOpeningWithAByteOrderMarkIsReadAsTheBytesItIs()
    {
        var bytes = Encrypted("þÿA", "owner");
        Encoding.Latin1.GetString(bytes).Should().MatchRegex(@"/ID\s*\[<FEFF41>", "the point of this test is such an identifier");

        using var document = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), UserPassword, PdfDocumentOpenMode.Import);

        document.PageCount.Should().Be(1);
    }

    [Test]
    [Arguments(@"(\376\377)", "FEFF")]
    [Arguments("<FEFF41>", "FEFF41")]
    [Arguments(@"(\376\377A)", "FEFF41")]
    [Arguments(@"(\377\376A)", "FFFE41")]
    public void AnIdentifierReadAsTextIsWrittenAsTheBytesTheKeyIsDerivedFrom(string identifier, string bytesInTheFile)
    {
        var plain = new PdfDocument();
        plain.AddPage();
        var written = Encoding.Latin1.GetString(Saved.Bytes(plain));
        var patched = Regex.Replace(written, @"/ID\s*\[\s*<[0-9A-Fa-f]*>", _ => "/ID [" + identifier);
        patched.Should().Contain("/ID [" + identifier, "the point of this test is such an identifier");
        using var read = Saved.Open(Encoding.Latin1.GetBytes(patched));
        read.SecuritySettings.UserPassword = UserPassword;

        var bytes = Saved.Bytes(read);

        var security = new StandardSecurity(bytes, UserPassword);
        security.Entries.Id.Should().Equal(System.Convert.FromHexString(bytesInTheFile), "the identifier is written as the bytes it was read as");
        security.DerivedKeyMatchesTheDocument.Should().BeTrue();
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
