using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A stream whose data is in another file. ISO 32000-1 Table 5 gives a stream dictionary an
///   optional /F naming the file the data is really in; the bytes between the keywords are then
///   to be ignored, the filters are named by /FFilter rather than /Filter, and /Length goes on
///   counting the bytes that are in this file - usually none.
/// </summary>
/// <remarks>
///   The reader used to refuse any such document outright, with "File streams are not yet
///   implemented" - the report behind empira/PDFsharp#389, where a catalog's /Metadata says its
///   XML is in a file beside the document and the whole file becomes unreadable for it. Nothing
///   here goes and fetches that file: what the reader owes the caller is the document as written,
///   with the entries saying where the data is left intact for whoever wants to follow them.
/// </remarks>
public class ExternalFileStreamTests
{
    /// <summary>
    ///   The document of the report: a catalog whose /Metadata is a stream that says its data is
    ///   in a file beside the document, and the file specification it names.
    /// </summary>
    static byte[] DocumentWhoseMetadataIsInAnotherFile(string streamEntries, string data = "") =>
        RawPdf.Build(new[]
        {
            "<</Type/Catalog/Pages 2 0 R/Metadata 3 0 R>>",
            "<</Type/Pages/Kids[5 0 R]/Count 1>>",
            "<<" + streamEntries + ">>stream\n" + data + "\nendstream",
            "<</Type/Filespec/F(manifest.c2pa)/UF(manifest.c2pa)>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>",
        });

    static PdfDictionary MetadataOf(byte[] pdf, PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify) =>
        (PdfDictionary)PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(pdf), mode)
            .Internals.Catalog.Elements.GetObject("/Metadata");

    [Fact]
    public void AStreamSayingItsDataIsInAnotherFileIsRead()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Subtype/XML/Length 0/F 4 0 R");

        var metadata = MetadataOf(pdf);

        metadata.Elements.GetString("/Subtype").Should().Be("/XML");
        metadata.Stream.Value.Should().BeEmpty("the document holds none of the data itself");
    }

    [Fact]
    public void TheFileSpecificationSurvivesBeingRead()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 0/F 4 0 R/FFilter/FlateDecode");

        var metadata = MetadataOf(pdf);

        // What a caller needs to go and get the data is the whole point of reading the document
        // rather than refusing it, so both entries have to come through untouched.
        var file = metadata.Elements.GetDictionary("/F");
        file.Elements.GetString("/F").Should().Be("manifest.c2pa");
        metadata.Elements.GetName("/FFilter").Should().Be("/FlateDecode");
    }

    [Fact]
    public void BytesInTheDocumentAreStillCountedByLengthAndStillFound()
    {
        // Table 5: the bytes between the keywords are to be ignored, but /Length still says how
        // many there are. Reading them is what puts the reader on "endstream" rather than in the
        // middle of them, and it is what lets the document be written out again unchanged.
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 7/F 4 0 R", "ignored");

        var metadata = MetadataOf(pdf);

        metadata.Stream.Value.Should().HaveCount(7);
        metadata.Elements.ContainsKey("/F").Should().BeTrue();
    }

    [Fact]
    public void AStreamSayingNeitherHowLongItIsNorWhereItEndsIsStillRecovered()
    {
        // The /Length entry is required and real documents leave it out; an external stream is no
        // different, and falls back to the same search for the end of the stream.
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/F 4 0 R", "ignored");

        var metadata = MetadataOf(pdf);

        metadata.Stream.Value.Should().HaveCount(7);
        metadata.Elements.GetInteger("/Length").Should().Be(7);
    }

    [Fact]
    public void TheDocumentCanBeWrittenOutAgain()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Subtype/XML/Length 0/F 4 0 R");
        var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);

        var written = new MemoryStream();
        document.Save(written, closeStream: false);

        MetadataOf(written.ToArray()).Elements.ContainsKey("/F")
            .Should().BeTrue("a document read for its own sake should come back as it went in");
    }

    [Fact]
    public void EveryOpenModeReadsIt()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 0/F 4 0 R");

        foreach (var mode in new[]
                 {
                     PdfDocumentOpenMode.Modify, PdfDocumentOpenMode.Import,
                     PdfDocumentOpenMode.ReadOnly, PdfDocumentOpenMode.Append
                 })
            MetadataOf(pdf, mode).Elements.ContainsKey("/F").Should().BeTrue($"opened for {mode}");
    }

    // ----- Reaching the file specification through the stream --------------------------------------

    [Fact]
    public void AStreamThatNamesNoOtherFileHasNoExternalFile()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 7", "ordinary");

        MetadataOf(pdf).Stream.ExternalFile.Should().BeNull();
    }

    [Fact]
    public void TheExternalFileIsAnsweredForASpecificationTheDocumentHolds()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 0/F 4 0 R");

        MetadataOf(pdf).Stream.ExternalFile.FileName.Should().Be("manifest.c2pa");
    }

    [Fact]
    public void TheExternalFileIsAnsweredForANameGivenAsAPlainString()
    {
        // ISO 32000-1 7.11.2 lets a file specification be a bare string as readily as a
        // dictionary, and a stream saying where its data is usually needs no more than the name.
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 0/F(manifest.c2pa)");

        MetadataOf(pdf).Stream.ExternalFile.FileName.Should().Be("manifest.c2pa");
    }

    [Fact]
    public void ASpecificationTheDocumentHoldsIsTheSameOneEachTimeItIsAsked()
    {
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 0/F 4 0 R");
        var stream = MetadataOf(pdf).Stream;

        stream.ExternalFile.Should().BeSameAs(stream.ExternalFile,
            "a caller who writes to it is writing to the document");
    }

    [Fact]
    public void ASpecificationWrittenOutInsideTheStreamDictionaryIsTooAndIsNotEmptied()
    {
        // A specification that is not an object of its own has no reference to re-point, which is
        // how the one type transformation in this library keeps its identity. It is put back under
        // the key instead - and the entry has to still describe the file afterwards.
        var pdf = DocumentWhoseMetadataIsInAnotherFile(
            "/Type/Metadata/Length 0/F<</Type/Filespec/F(manifest.c2pa)/UF(manifest.c2pa)>>");
        var stream = MetadataOf(pdf).Stream;

        stream.ExternalFile.Should().BeSameAs(stream.ExternalFile);
        stream.ExternalFile.FileName.Should().Be("manifest.c2pa");
    }

    [Fact]
    public void ANameGivenAsAPlainStringIsReadWithoutRewritingTheDocument()
    {
        // The specification answered for a bare string is made on the spot and stands outside the
        // document: turning the string into a dictionary to hold it would change what the file
        // says. Reading is what the property is for, and this is the edge of what it promises.
        var pdf = DocumentWhoseMetadataIsInAnotherFile("/Type/Metadata/Length 0/F(manifest.c2pa)");
        var metadata = MetadataOf(pdf);

        metadata.Stream.ExternalFile.Should().NotBeSameAs(metadata.Stream.ExternalFile);
        metadata.Elements["/F"].Should().BeOfType<PdfString>("the entry is left as the file wrote it");
    }
}
