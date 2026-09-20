using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The /Length entry of a stream dictionary is required, but AutoCAD writes the XMP metadata
///   of its documents without one, and those documents have to be readable all the same.
///   See https://github.com/ststeiger/PdfSharpCore/issues/469.
/// </summary>
public class StreamWithoutLengthTests
{
    private const string Metadata =
        "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF " +
        "xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\"/>" +
        "</rdf:RDF></x:xmpmeta>";

    [Theory]
    [InlineData(PdfReadAccuracy.Strict)]
    [InlineData(PdfReadAccuracy.Moderate)]
    public void ADocumentWithAStreamThatHasNoLengthCanBeRead(PdfReadAccuracy accuracy)
    {
        using var input = new MemoryStream(BuildDocumentWithAMetadataStreamWithoutLength());

        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import, accuracy);

        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void TheStreamThatHasNoLengthIsReadUpToTheEndOfTheStreamKeyword()
    {
        using var input = new MemoryStream(BuildDocumentWithAMetadataStreamWithoutLength());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var metadata = document.Internals.Catalog.Elements.GetDictionary("/Metadata");

        metadata.Stream.Value.Should().Equal(Encoding.Latin1.GetBytes(Metadata));
        // The recovered length has to be recorded, because the dictionary is written out again.
        metadata.Elements.GetInteger("/Length").Should().Be(Metadata.Length);
    }

    [Fact]
    public void ADocumentWithAStreamThatHasNoLengthCanBeMergedAndSaved()
    {
        // The way the documents in the issue were being combined.
        using var input = new MemoryStream(BuildDocumentWithAMetadataStreamWithoutLength());
        var inputDocument = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import, PdfReadAccuracy.Moderate);

        var merged = new PdfDocument();
        foreach (var page in inputDocument.Pages)
            _ = merged.AddPage(page);

        using var output = new MemoryStream();
        merged.Save(output, false);

        output.Position = 0;
        PdfPinata.Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Import).PageCount.Should().Be(1);
    }

    /// <summary>
    ///   Builds a single page document whose metadata stream is written the way AutoCAD writes
    ///   it: the dictionary holds the type of the stream, but not its length.
    /// </summary>
    private static byte[] BuildDocumentWithAMetadataStreamWithoutLength()
    {
        const string content = "0 0 1 RG 10 10 100 100 re S";
        var objects = new[]
        {
            "<</Type/Catalog/Pages 2 0 R/Metadata 5 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents 4 0 R>>",
            "<</Length " + content.Length + ">>stream\n" + content + "\nendstream",
            "<</Subtype/XML/Type/Metadata>>stream\n" + Metadata + "\nendstream"
        };

        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var startOfCrossReferenceTable = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Length + 1).Append('\n');
        pdf.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<</Size ").Append(objects.Length + 1).Append("/Root 1 0 R>>\n");
        pdf.Append("startxref\n").Append(startOfCrossReferenceTable).Append("\n%%EOF\n");

        // The document is plain ASCII, so a byte is a character and the offsets above hold.
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }
}
