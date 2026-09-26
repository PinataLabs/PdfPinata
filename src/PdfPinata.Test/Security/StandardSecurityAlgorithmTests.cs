using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Security;

/// <summary>
///   The algorithms of ISO 32000-1 7.6.3 that the standard security handler is built on, up to
///   revision 4: the file key (Algorithm 2), the /O entry (3), the /U entry (4 and 5) and the key
///   of each object (1). Judged against documents other producers wrote, since the spec gives no
///   test vectors of its own.
/// </summary>
/// <remarks>
///   <see cref="StandardSecurity"/>, the independent copy, is checked first against Ghostscript,
///   qpdf and three online services. Then what PdfPinata writes is checked against it, and against
///   Ghostscript byte for byte where the two can be given the same document identifier. What the
///   reader makes of the same files is in <see cref="StandardSecurityRoundTripTests"/>.
///   <para>
///     The four fixtures new with these tests are Ghostscript 10.08.0's <c>pdfwrite</c> with
///     <c>-sOwnerPassword=owner -sUserPassword=user</c> and <c>-dEncryptionR=2 -dKeyLength=40
///     -dPermissions=-64</c> or <c>-dEncryptionR=3 -dKeyLength=128 -dPermissions=-3904</c>, and
///     qpdf 12.4.1 with <c>--encrypt --user-password=user --owner-password=owner --bits=128
///     --cleartext-metadata</c> and <c>--use-aes=n</c> or <c>--use-aes=y</c>. Each is titled
///     "Standard security test".
///   </para>
/// </remarks>
public class StandardSecurityAlgorithmTests
{
    public static TheoryData<string, string, string> ProducerDocuments => new()
    {
        { "rc4-r2-40bit-ghostscript.pdf", "user", "owner" },
        { "rc4-r3-40bit-ghostscript.pdf", "user", "owner" },
        { "rc4-r3-56bit-ghostscript.pdf", "user", "owner" },
        { "rc4-r3-96bit-ghostscript.pdf", "user", "owner" },
        { "rc4-r3-128bit-ghostscript.pdf", "user", "owner" },
        { "rc4-r4-128bit-cleartext-metadata-qpdf.pdf", "user", "owner" },
        { "aes-r4-128bit-cleartext-metadata-qpdf.pdf", "user", "owner" },
        { "protected-ilovepdf.pdf", "test123", "test123" },
        { "protected-adobe.pdf", "test123", "test123" },
        { "protected-user-and-owner-password.pdf", "jinglebob8", "pigsfly2" },
        { "AesEncrypted.pdf", "", null }
    };

    [Theory]
    [MemberData(nameof(ProducerDocuments))]
    public void TheIndependentCopyDerivesTheUserEntryEachProducerWrote(string file, string user, string owner)
    {
        _ = owner;
        var document = new StandardSecurity(Asset(file), user);

        document.DerivedKeyMatchesTheDocument.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(ProducerDocuments))]
    public void TheIndependentCopyDerivesTheOwnerEntryEachProducerWrote(string file, string user, string owner)
    {
        if (owner == null)
            return; // Nobody knows this one's owner password.
        var entries = EncryptionEntries.Read(Asset(file));

        var computed = StandardSecurity.OwnerEntry(owner, user, entries.Revision, entries.KeyLength);

        computed.Should().Equal(entries.Owner[..32]);
    }

    [Theory]
    [InlineData("rc4-r4-128bit-cleartext-metadata-qpdf.pdf")]
    [InlineData("aes-r4-128bit-cleartext-metadata-qpdf.pdf")]
    public void AtRevision4UnencryptedMetadataIsPartOfTheFileKey(string file)
    {
        // Algorithm 2 step (f). Only the reader needs it, because PdfPinata writes nothing past
        // revision 3, and this is what says the reader's branch is not dead code.
        var entries = EncryptionEntries.Read(Asset(file));
        entries.EncryptMetadata.Should().BeFalse();

        bool Matches(bool encryptMetadata)
        {
            var key = StandardSecurity.FileKey(StandardSecurity.Pad("user"), entries.Owner, entries.Permissions,
                entries.Id, entries.Revision, entries.KeyLength, encryptMetadata);
            return StandardSecurity.StartsWith(entries.User,
                StandardSecurity.UserEntry(key, entries.Id, entries.Revision), 16);
        }

        Matches(encryptMetadata: false).Should().BeTrue();
        Matches(encryptMetadata: true).Should().BeFalse();
    }

    /// <summary>
    ///   Given Ghostscript's document identifier, passwords and permissions, PdfPinata writes the
    ///   same /O, /U and /P that Ghostscript did - all 32 bytes at revision 2, and at revision 3
    ///   the 16 bytes of /U that are defined. The rest of /U is arbitrary padding, and PdfPinata's
    ///   is zeros.
    /// </summary>
    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit, "rc4-r2-40bit-ghostscript.pdf")]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit, "rc4-r3-128bit-ghostscript.pdf")]
    public void PdfPinataWritesTheEntriesGhostscriptWrote(PdfDocumentSecurityLevel level, string file)
    {
        var ghostscript = EncryptionEntries.Read(Asset(file));

        var document = new PdfDocument();
        _ = document.AddPage();
        document.SecuritySettings.DocumentSecurityLevel = level;
        document.SecuritySettings.UserPassword = "user";
        document.SecuritySettings.OwnerPassword = "owner";
        PermitNothing(document.SecuritySettings);
        document.Internals.FirstDocumentID = Encoding.Latin1.GetString(ghostscript.Id);

        var written = EncryptionEntries.Read(Save(document));

        written.Id.Should().Equal(ghostscript.Id);
        written.Revision.Should().Be(ghostscript.Revision);
        written.Permissions.Should().Be(ghostscript.Permissions);
        written.Owner.Should().Equal(ghostscript.Owner);
        if (written.Revision == 2)
        {
            written.User.Should().Equal(ghostscript.User);
        }
        else
        {
            written.User[..16].Should().Equal(ghostscript.User[..16]);
            written.User[16..].Should().OnlyContain(b => b == 0);
        }
    }

    public static TheoryData<PdfDocumentSecurityLevel, string, string> PasswordPairs()
    {
        var data = new TheoryData<PdfDocumentSecurityLevel, string, string>();
        foreach (var level in new[] { PdfDocumentSecurityLevel.Encrypted40Bit, PdfDocumentSecurityLevel.Encrypted128Bit })
        {
            data.Add(level, "user", "owner");
            data.Add(level, "same", "same");
            data.Add(level, "user", null);
            data.Add(level, null, "owner");
            data.Add(level, "ÀÉÎõü", "ÿþ");
            data.Add(level, new string('u', 40), new string('o', 33));
        }
        return data;
    }

    /// <summary>
    ///   Algorithms 2 to 5 as the writer carries them out, for each security level it offers and
    ///   passwords that are missing, the same as each other, above ASCII or longer than 32
    ///   characters.
    /// </summary>
    [Theory]
    [MemberData(nameof(PasswordPairs))]
    public void TheWritersEntriesAreTheOnesTheAlgorithmsGive(PdfDocumentSecurityLevel level, string user, string owner)
    {
        var bytes = SaveEncrypted(level, user, owner, "Algorithms");
        var entries = EncryptionEntries.Read(bytes);

        var strong = level == PdfDocumentSecurityLevel.Encrypted128Bit;
        entries.Version.Should().Be(strong ? 2 : 1);
        entries.Revision.Should().Be(strong ? 3 : 2);
        entries.LengthInBits.Should().Be(strong ? 128 : 40);
        entries.EncryptMetadata.Should().BeTrue();
        entries.Owner.Should().HaveCount(32);
        entries.User.Should().HaveCount(32);

        entries.Owner.Should().Equal(
            StandardSecurity.OwnerEntry(owner, user ?? "", entries.Revision, entries.KeyLength));

        var document = new StandardSecurity(bytes, user ?? "");
        document.DerivedKeyMatchesTheDocument.Should().BeTrue();
        document.DecryptInfoString("/Title").Should().Be("Algorithms");
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit, -64)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit, -3904)]
    public void PermittingNothingWritesOnlyTheBitsThatMustBeSet(PdfDocumentSecurityLevel level, int expected)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.SecuritySettings.DocumentSecurityLevel = level;
        document.SecuritySettings.UserPassword = "user";
        PermitNothing(document.SecuritySettings);

        EncryptionEntries.Read(Save(document)).Permissions.Should().Be(expected);
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit, -4)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit, -4)]
    public void PermittingEverythingWritesEveryBit(PdfDocumentSecurityLevel level, int expected)
    {
        var bytes = SaveEncrypted(level, "user", "owner", "Permissions");

        EncryptionEntries.Read(bytes).Permissions.Should().Be(expected);
    }

    /// <summary>
    ///   Algorithm 1 takes the low three bytes of the object number and the low two of the
    ///   generation number. Every object PdfPinata writes is generation 0 and numbered below 256,
    ///   so this document is built by hand, with its information dictionary numbered 66051
    ///   (0x010203) at generation 258 (0x0102), and encrypted by the independent copy. qpdf 12.4.1
    ///   decrypts the same title from it, with either password, once a comment has padded the file
    ///   past the size below which qpdf ignores an object number that high.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    public void AnObjectsKeyIsMadeFromItsNumberAndGeneration(string password)
    {
        var bytes = HandBuiltDocument(infoNumber: 0x010203, infoGeneration: 0x0102, title: "Generation 258");

        using var document = Pdf.IO.PdfReader.Open(new MemoryStream(bytes), password, PdfDocumentOpenMode.ReadOnly);

        document.Info.Title.Should().Be("Generation 258");
        document.SecuritySettings.HasOwnerPermissions.Should().Be(password == "owner");
    }

    internal static byte[] HandBuiltDocument(int infoNumber, int infoGeneration, string title)
    {
        var id = new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xBA, 0xDC, 0xFE, 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF };
        const int revision = 3, keyLength = 16, permissions = -3904;
        var ownerEntry = StandardSecurity.OwnerEntry("owner", "", revision, keyLength);
        var fileKey = StandardSecurity.FileKey(StandardSecurity.Pad(""), ownerEntry, permissions, id, revision, keyLength);
        var userEntry = new byte[32];
        StandardSecurity.UserEntry(fileKey, id, revision).CopyTo(userEntry, 0);
        var encryptedTitle = StandardSecurity.Rc4(
            StandardSecurity.ObjectKey(fileKey, infoNumber, infoGeneration), Encoding.Latin1.GetBytes(title));

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new Dictionary<int, int>();
        void Object(int number, int generation, string body)
        {
            offsets[number] = pdf.Length;
            pdf.Append($"{number} {generation} obj\n{body}\nendobj\n");
        }

        Object(1, 0, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, 0, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, 0, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>");
        Object(4, 0, $"<< /Filter /Standard /V 2 /R {revision} /Length 128 /P {permissions} " +
                     $"/O <{Hex(ownerEntry)}> /U <{Hex(userEntry)}> >>");
        Object(infoNumber, infoGeneration, $"<< /Title <{Hex(encryptedTitle)}> >>");

        var xref = pdf.Length;
        pdf.Append("xref\n0 5\n0000000000 65535 f \n");
        for (var n = 1; n <= 4; n++)
            pdf.Append($"{offsets[n]:D10} 00000 n \n");
        pdf.Append($"{infoNumber} 1\n{offsets[infoNumber]:D10} {infoGeneration:D5} n \n");
        pdf.Append($"trailer\n<< /Size {infoNumber + 1} /Root 1 0 R /Info {infoNumber} {infoGeneration} R " +
                   $"/Encrypt 4 0 R /ID [<{Hex(id)}> <{Hex(id)}>] >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private static string Hex(byte[] bytes) => System.Convert.ToHexString(bytes);

    internal static byte[] Asset(string file) => File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath(file));

    internal static byte[] SaveEncrypted(PdfDocumentSecurityLevel level, string user, string owner, string title)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        document.Info.Title = title;
        document.SecuritySettings.DocumentSecurityLevel = level;
        if (user != null)
            document.SecuritySettings.UserPassword = user;
        if (owner != null)
            document.SecuritySettings.OwnerPassword = owner;
        return Save(document);
    }

    private static byte[] Save(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    private static void PermitNothing(PdfSecuritySettings settings)
    {
        settings.PermitPrint = false;
        settings.PermitModifyDocument = false;
        settings.PermitExtractContent = false;
        settings.PermitAnnotations = false;
        settings.PermitFormsFill = false;
        settings.PermitAccessibilityExtractContent = false;
        settings.PermitAssembleDocument = false;
        settings.PermitFullQualityPrint = false;
    }
}
