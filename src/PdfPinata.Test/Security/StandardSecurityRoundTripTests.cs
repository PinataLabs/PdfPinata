using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   What the reader makes of an encrypted document: one PdfPinata wrote, at each security level
///   it offers, and one another producer wrote, at each revision and key length this library
///   reads through the RC4-era key algorithms. Each password is tried, and a wrong one.
/// </summary>
/// <remarks>
///   Opening proves the password check. Reading the title back proves the file key and the object
///   keys, because a wrong key decrypts a string to noise rather than failing.
/// </remarks>
public class StandardSecurityRoundTripTests
{
    public static TheoryData<PdfDocumentSecurityLevel, string, string> PasswordPairs() =>
        StandardSecurityAlgorithmTests.PasswordPairs();

    [Theory]
    [MemberData(nameof(PasswordPairs))]
    public void EachPasswordOpensWhatPdfPinataWrote(PdfDocumentSecurityLevel level, string user, string owner)
    {
        var bytes = StandardSecurityAlgorithmTests.SaveEncrypted(level, user, owner, "Round trip");
        var ownerPassword = string.IsNullOrEmpty(owner) ? user : owner;

        // The owner password is tried first, so a user password equal to it brings owner permissions.
        OpenedWith(bytes, user ?? "").Should().Be(("Round trip", (user ?? "") == ownerPassword));
        OpenedWith(bytes, ownerPassword).Should().Be(("Round trip", true));
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void AWrongPasswordIsRefused(PdfDocumentSecurityLevel level)
    {
        var bytes = StandardSecurityAlgorithmTests.SaveEncrypted(level, "user", "owner", "Round trip");

        var open = () => OpenedWith(bytes, "neither");

        open.Should().Throw<PdfReaderException>().WithMessage("*password is invalid*");
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void OnlyTheFirst32CharactersOfAPasswordCount(PdfDocumentSecurityLevel level)
    {
        var user = new string('u', 32) + "ignored";
        var owner = new string('o', 32) + "ignored too";
        var bytes = StandardSecurityAlgorithmTests.SaveEncrypted(level, user, owner, "Round trip");

        OpenedWith(bytes, new string('u', 32)).Should().Be(("Round trip", false));
        OpenedWith(bytes, new string('o', 32) + "anything").Should().Be(("Round trip", true));
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void TheStreamsAreDecryptedAsWellAsTheStrings(PdfDocumentSecurityLevel level)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.Black, 10, 10, 50, 50);
        document.SecuritySettings.DocumentSecurityLevel = level;
        document.SecuritySettings.UserPassword = "user";
        document.SecuritySettings.OwnerPassword = "owner";
        using var stream = new MemoryStream();
        document.Save(stream, false);

        foreach (var password in new[] { "user", "owner" })
        {
            using var opened = Pdf.IO.PdfReader.Open(new MemoryStream(stream.ToArray()), password,
                PdfDocumentOpenMode.Import);
            ContentReader.ReadContent(opened.Pages[0]).Count.Should().BeGreaterThan(0);
        }
    }

    public static TheoryData<string> NewProducerDocuments => new()
    {
        "rc4-r2-40bit-ghostscript.pdf",
        "rc4-r3-128bit-ghostscript.pdf",
        "rc4-r4-128bit-cleartext-metadata-qpdf.pdf",
        "aes-r4-128bit-cleartext-metadata-qpdf.pdf"
    };

    [Theory]
    [MemberData(nameof(NewProducerDocuments))]
    public void EachPasswordOpensWhatAnotherProducerWrote(string file)
    {
        var bytes = StandardSecurityAlgorithmTests.Asset(file);

        OpenedWith(bytes, "user").Should().Be(("Standard security test", false));
        OpenedWith(bytes, "owner").Should().Be(("Standard security test", true));
        var wrong = () => OpenedWith(bytes, "neither");
        wrong.Should().Throw<PdfReaderException>();
    }

    [Theory]
    [MemberData(nameof(NewProducerDocuments))]
    public void TheStreamsAnotherProducerEncryptedAreDecrypted(string file)
    {
        using var opened = Pdf.IO.PdfReader.Open(new MemoryStream(StandardSecurityAlgorithmTests.Asset(file)),
            "user", PdfDocumentOpenMode.Import);

        ContentReader.ReadContent(opened.Pages[0]).Count.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("protected-ilovepdf.pdf", "test123", true)]
    [InlineData("protected-adobe.pdf", "test123", true)]
    [InlineData("protected-user-and-owner-password.pdf", "jinglebob8", false)]
    [InlineData("protected-user-and-owner-password.pdf", "pigsfly2", true)]
    [InlineData("AesEncrypted.pdf", "", false)]
    public void TheOnlineServicesDocumentsOpenWithTheRightPermissions(string file, string password, bool owner)
    {
        using var opened = Pdf.IO.PdfReader.Open(new MemoryStream(StandardSecurityAlgorithmTests.Asset(file)),
            password, PdfDocumentOpenMode.Import);

        opened.SecuritySettings.HasOwnerPermissions.Should().Be(owner);
    }

    /// <summary>
    ///   At revision 2 the whole of /U is the padding string encrypted under the file key
    ///   (Algorithm 4), and Algorithm 6 accepts a user password only when all 32 bytes match. The
    ///   owner password is no way round it: Algorithm 7 recovers the user password from /O and
    ///   then validates it with Algorithm 6, against the same damaged /U. qpdf and pdf.js refuse
    ///   both passwords too.
    /// </summary>
    [Fact]
    public void ARevision2DocumentWhoseUserEntryIsDamagedInItsSecondHalfOpensWithNeitherPassword()
    {
        var bytes = WithLastByteOfUserEntryChanged(StandardSecurityAlgorithmTests.Asset("rc4-r2-40bit-ghostscript.pdf"));
        EncryptionEntries.Read(bytes).Revision.Should().Be(2);

        foreach (var password in new[] { "user", "owner" })
        {
            var open = () => OpenedWith(bytes, password);
            open.Should().Throw<PdfReaderException>().WithMessage("*password is invalid*", password);
        }
    }

    /// <summary>
    ///   From revision 3 on only the first 16 bytes of /U are defined (Algorithm 5) and the rest
    ///   is arbitrary padding, which Algorithm 6 does not compare. Damage there changes nothing.
    /// </summary>
    [Theory]
    [InlineData("rc4-r3-40bit-ghostscript.pdf")]
    [InlineData("rc4-r3-128bit-ghostscript.pdf")]
    public void ARevision3DocumentWhoseUserEntryIsDamagedInItsSecondHalfStillOpens(string file)
    {
        var original = StandardSecurityAlgorithmTests.Asset(file);
        var bytes = WithLastByteOfUserEntryChanged(original);
        EncryptionEntries.Read(bytes).Revision.Should().Be(3);
        var title = OpenedWith(original, "user").Title;

        OpenedWith(bytes, "user").Should().Be((title, false));
        OpenedWith(bytes, "owner").Should().Be((title, true));
    }

    /// <summary>
    ///   Changes the last byte of the /U entry in place, so that every offset in the
    ///   cross-reference table still holds. The entry must be a literal string that ends in a
    ///   byte standing for itself rather than in an escape.
    /// </summary>
    private static byte[] WithLastByteOfUserEntryChanged(byte[] document)
    {
        var pdf = System.Text.Encoding.Latin1.GetString(document);
        var start = pdf.IndexOf("/U (", System.StringComparison.Ordinal);
        start.Should().BePositive().And.Be(pdf.LastIndexOf("/U (", System.StringComparison.Ordinal));
        var last = pdf.IndexOf(")>>", start, System.StringComparison.Ordinal) - 1;
        // Not the end of an escape, which is a backslash and up to three octal digits.
        pdf.Substring(last - 3, 3).Should().NotContain("\\");
        "()\\\r\n".Should().NotContain(pdf[last].ToString());

        var damaged = (byte[])document.Clone();
        damaged[last] ^= 0x01;
        "()\\\r\n".Should().NotContain(((char)damaged[last]).ToString());

        var before = EncryptionEntries.Read(document).User;
        var after = EncryptionEntries.Read(damaged).User;
        after.Should().HaveCount(32);
        after[..31].Should().Equal(before[..31]);
        after[31].Should().NotBe(before[31]);
        return damaged;
    }

    private static (string Title, bool Owner) OpenedWith(byte[] bytes, string password)
    {
        using var document = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), password, PdfDocumentOpenMode.Import);
        return (document.Info.Title, document.SecuritySettings.HasOwnerPermissions);
    }
}
