using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using TUnit.Core;

namespace PdfPinata.Test.Security;

/// <summary>
///   A stream can name a crypt filter of its own, in a <c>/Filter /Crypt</c> ahead of its other
///   filters (ISO 32000-1 7.4.10), and is decrypted with that filter rather than with
///   <c>/StmF</c>. At <c>/V</c> 4 and later, a missing <c>/StmF</c> or <c>/StrF</c> is
///   <c>/Identity</c> (Table 20). Issue #328.
/// </summary>
public class StreamCryptFilterTests
{
    private const string OwnCryptFilter = " /Filter /Crypt /DecodeParms << /Name /StdCF >>";

    [Test]
    [Arguments("/V2", OwnCryptFilter)]
    [Arguments("/AESV2", OwnCryptFilter)]
    [Arguments("/V2", " /Filter [/Crypt] /DecodeParms [<< /Name /StdCF >>]")]
    public void AStreamNamingACryptFilterIsDecryptedWithItThoughStreamsAreIdentity(string method, string entries)
    {
        var document = new CryptFilterDocument
        {
            Method = method, StreamFilter = "/Identity", MetadataUnderStdCF = true, MetadataEntries = entries
        };

        var read = Open(document.Build());

        var metadata = Metadata(read);
        metadata.Stream.Value.Should().Equal(document.Metadata);
        metadata.Elements.ContainsKey("/Filter").Should().BeFalse("the crypt filter has been applied");
        metadata.Elements.ContainsKey("/DecodeParms").Should().BeFalse();
        metadata.Stream.UnfilteredValue.Should().Equal(document.Metadata);
        ContentOf(read).Should().Equal(document.Content, "every other stream is under /Identity");
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void AStreamNamingTheCryptFilterStreamsAreUnderAlreadyLosesItsCryptEntry(string method)
    {
        var document = new CryptFilterDocument { Method = method, MetadataEntries = OwnCryptFilter };

        var read = Open(document.Build());

        var metadata = Metadata(read);
        metadata.Elements.ContainsKey("/Filter").Should().BeFalse();
        metadata.Stream.UnfilteredValue.Should().Equal(document.Metadata);
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void AMissingStmFMeansStreamsAreNotEncrypted(string method)
    {
        var document = new CryptFilterDocument { Method = method, StreamFilter = null };

        var read = Open(document.Build());

        ContentOf(read).Should().Equal(document.Content);
        Metadata(read).Stream.Value.Should().Equal(document.Metadata);
        read.Info.Title.Should().Be(document.Title, "strings are still under /StdCF");
    }

    [Test]
    [Arguments("/V2")]
    [Arguments("/AESV2")]
    public void AMissingStrFMeansStringsAreNotEncrypted(string method)
    {
        var document = new CryptFilterDocument { Method = method, StringFilter = null };

        var read = Open(document.Build());

        read.Info.Title.Should().Be(document.Title);
        ContentOf(read).Should().Equal(document.Content, "streams are still under /StdCF");
    }

    private static PdfDocument Open(byte[] bytes) =>
        Pdf.IO.PdfReader.Open(new MemoryStream(bytes), "", PdfDocumentOpenMode.Import);

    private static PdfDictionary Metadata(PdfDocument document) =>
        document.Internals.Catalog.Elements.GetDictionary("/Metadata");

    private static byte[] ContentOf(PdfDocument document) =>
        document.Pages[0].Elements.GetDictionary("/Contents").Stream.Value;
}
