using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   Data an encrypted document says is not encrypted is read as it stands. Issue #328.
/// </summary>
/// <remarks>
///   Two ways a file says so. <c>/EncryptMetadata false</c> leaves the document's metadata stream in
///   plain, and a crypt filter named <c>/Identity</c> leaves whatever it covers in plain. The reader
///   used to decrypt both anyway: with RC4 that garbles them, and with AES they came through only
///   when the decryptor's padding check happened to fail on bytes that were never encrypted.
/// </remarks>
public class UnencryptedDataTests
{
    [Test]
    [Arguments("rc4-r4-128bit-cleartext-metadata-qpdf.pdf")] // /V 4, /CFM /V2
    [Arguments("aes-r4-128bit-cleartext-metadata-qpdf.pdf")] // /V 4, /CFM /AESV2
    public void UnencryptedMetadataReadsAsItStandsInTheFile(string fileName)
    {
        var path = PathHelper.GetInstance().GetAssetPath(fileName);

        var document = Pdf.IO.PdfReader.Open(path, "user", PdfDocumentOpenMode.Import);

        var metadata = document.Internals.Catalog.Elements.GetDictionary("/Metadata").Stream.Value;
        Encoding.ASCII.GetString(metadata, 0, 9).Should().Be("<?xpacket");
    }

    [Test]
    public void UnencryptedMetadataIsNotRunThroughTheAesDecryptor()
    {
        // Plain metadata whose last block, taken for ciphertext, decrypts to valid padding: the one
        // case in which decrypting it anyway does not fail, and so changes it.
        var document = WithPlainMetadataThatDecryptsCleanly();

        var read = Open(document.Build());

        MetadataOf(read).Should().Equal(document.Metadata);
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void EncryptedMetadataIsStillDecrypted(string method)
    {
        var document = new CryptFilterDocument { Method = method };

        var read = Open(document.Build());

        MetadataOf(read).Should().Equal(document.Metadata);
        ContentOf(read).Should().Equal(document.Content);
        read.Info.Title.Should().Be(document.Title);
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void StreamsUnderTheIdentityFilterReadAsTheyStandInTheFile(string method)
    {
        var document = new CryptFilterDocument { Method = method, StreamFilter = "/Identity" };

        var read = Open(document.Build());

        ContentOf(read).Should().Equal(document.Content);
        MetadataOf(read).Should().Equal(document.Metadata);
        read.Info.Title.Should().Be(document.Title, "strings are still under the crypt filter /StdCF");
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void StringsUnderTheIdentityFilterReadAsTheyStandInTheFile(string method)
    {
        var document = new CryptFilterDocument { Method = method, StringFilter = "/Identity" };

        var read = Open(document.Build());

        read.Info.Title.Should().Be(document.Title);
        ContentOf(read).Should().Equal(document.Content, "streams are still under the crypt filter /StdCF");
    }

    [Test]
    [Arguments("/V2", " /Filter /Crypt /DecodeParms << /Name /Identity >>")]
    [Arguments("/AESV2", " /Filter /Crypt /DecodeParms << /Name /Identity >>")]
    [Arguments("/V2", " /Filter [/Crypt] /DecodeParms [<< /Name /Identity >>]")]
    [Arguments("/V2", " /Filter /Crypt")] // A /Crypt filter with no /Name is /Identity.
    [Arguments("/V2", " /Filter [/Crypt] /DecodeParms []")] // nor with no parameters at all
    public void AStreamItsOwnCryptFilterLeavesInPlainReadsAsItStands(string method, string entries)
    {
        var document = new CryptFilterDocument { Method = method, PlainMetadata = true, MetadataEntries = entries };

        var read = Open(document.Build());

        MetadataOf(read).Should().Equal(document.Metadata);
        ContentOf(read).Should().Equal(document.Content, "every other stream is still under /StdCF");
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void AnIdentityCryptFilterGivenByReferenceLeavesTheStreamInPlain(string method)
    {
        // /Filter, /DecodeParms and the parameters inside them are each an indirect object.
        var document = new CryptFilterDocument
        {
            Method = method, PlainMetadata = true, MetadataEntries = " /Filter 8 0 R /DecodeParms 9 0 R",
            ExtraObjects = ["[/Crypt]", "[10 0 R]", "<< /Type /CryptFilterDecodeParms /Name /Identity >>"]
        };

        var read = Open(document.Build());

        MetadataOf(read).Should().Equal(document.Metadata);
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void AnIdentityCryptFilterBeforeAnotherIsTakenOffAndTheOtherStillDecodes(string method)
    {
        var plain = new CryptFilterDocument().Metadata;
        var document = new CryptFilterDocument
        {
            Method = method, PlainMetadata = true, Metadata = Deflated(plain),
            MetadataEntries = " /Filter [/Crypt /FlateDecode] /DecodeParms [<< /Name /Identity >> null]"
        };

        var read = Open(document.Build());

        var metadata = read.Internals.Catalog.Elements.GetDictionary("/Metadata");
        metadata.Elements.GetArray("/Filter").Elements.Should().ContainSingle()
            .Which.Should().Be(new PdfName("/FlateDecode"), "only the crypt filter is taken off");
        metadata.Stream.UnfilteredValue.Should().Equal(plain);
    }

    [Test]
    [Arguments(" /Filter /Crypt /DecodeParms << /Name /StdCF >>")] // the document's own filter, by name
    [Arguments(" /Filter []")]                                    // no filter at all
    public void AStreamWhoseFiltersNameNoIdentityCryptFilterIsDecryptedWithTheDocument(string entries)
    {
        var document = new CryptFilterDocument { MetadataEntries = entries };

        var read = Open(document.Build());

        MetadataOf(read).Should().Equal(document.Metadata);
    }

    [Test]
    [Arguments(false, "")]
    [Arguments(true, " /Filter /Crypt /DecodeParms << /Name /Identity >>")]
    public void PlainMetadataIsEncryptedWithEverythingElseWhenTheDocumentIsSaved(bool encryptMetadata, string entries)
    {
        var document = new CryptFilterDocument
        {
            EncryptMetadata = encryptMetadata, PlainMetadata = true, MetadataEntries = entries
        };
        var read = Pdf.IO.PdfReader.Open(new MemoryStream(document.Build()), CryptFilterDocument.OwnerPassword,
            PdfDocumentOpenMode.Modify);
        read.SecuritySettings.UserPassword = "saved";
        var saved = new MemoryStream();
        read.Save(saved);

        var reopened = Pdf.IO.PdfReader.Open(new MemoryStream(saved.ToArray()), "saved", PdfDocumentOpenMode.Import);

        MetadataOf(reopened).Should().Equal(document.Metadata);
        Encoding.Latin1.GetString(saved.ToArray()).Should().NotContain("<?xpacket", "the writer encrypts every stream");
    }

    private static CryptFilterDocument WithPlainMetadataThatDecryptsCleanly()
    {
        var head = new CryptFilterDocument().Metadata;
        byte[] whitespace = [(byte)' ', (byte)'\n', (byte)'\r', (byte)'\t'];
        // Whitespace to a multiple of 16 and then a whole block more, the end of which is varied.
        var padded = head.Concat(Enumerable.Repeat((byte)' ', 16 - head.Length % 16 + 16)).ToArray();

        for (var attempt = 0; attempt < 1 << 16; attempt++)
        {
            for (var i = 0; i < 8; i++)
                padded[^(i + 1)] = whitespace[(attempt >> (2 * i)) & 3];

            var document = new CryptFilterDocument { Method = "/AESV2", EncryptMetadata = false, Metadata = [..padded] };
            if (document.LastByteIfDecrypted(CryptFilterDocument.MetadataObject, padded) == 1)
                return document;
        }
        throw new System.InvalidOperationException("No whitespace ending decrypts to valid padding.");
    }

    private static byte[] Deflated(byte[] data)
    {
        var compressed = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(compressed, System.IO.Compression.CompressionLevel.Optimal))
            zlib.Write(data);
        return compressed.ToArray();
    }

    private static PdfDocument Open(byte[] bytes) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(bytes), "", PdfDocumentOpenMode.Import);

    private static byte[] MetadataOf(PdfDocument document) =>
        document.Internals.Catalog.Elements.GetDictionary("/Metadata").Stream.Value;

    private static byte[] ContentOf(PdfDocument document) =>
        document.Pages[0].Elements.GetDictionary("/Contents").Stream.Value;
}
