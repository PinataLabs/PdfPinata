using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.IO.enums;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The /Length of a stream may be an indirect reference, and the object it points to is allowed
///   to live inside an object stream. Such an object has no position of its own in the file, so it
///   can only be read through the object stream that holds it.
///   See https://github.com/ststeiger/PdfSharpCore/issues/456.
/// </summary>
public class IndirectStreamLengthTests
{
    // The content carries the bytes that end a stream inside one of its own strings. A reader
    // that knows the declared length reads all of it; one that recovers the length by looking
    // for the end of the stream stops at those bytes and comes up short. That is what separates
    // the length taken from the object stream from the length guessed by scanning.
    private const string Content = "BT /F1 12 Tf 20 40 Td (Issue 456) Tj ET\n(\nendstream) Tj\n";

    [Theory]
    [InlineData(PdfDocumentOpenMode.Modify)]
    [InlineData(PdfDocumentOpenMode.Import)]
    [InlineData(PdfDocumentOpenMode.ReadOnly)]
    public void ADocumentWhoseStreamLengthLivesInAnObjectStreamCanBeRead(PdfDocumentOpenMode openMode)
    {
        using var input = new MemoryStream(BuildDocumentWithTheStreamLengthInAnObjectStream());

        var document = Pdf.IO.PdfReader.Open(input, openMode, PdfReadAccuracy.Strict);

        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void TheStreamIsReadWithTheLengthTakenFromTheObjectStream()
    {
        using var input = new MemoryStream(BuildDocumentWithTheStreamLengthInAnObjectStream());
        var document = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        var streams = new List<byte[]>();
        foreach (var content in document.Pages[0].Contents)
            streams.Add(content.Stream.Value);

        streams.Should().ContainSingle()
            .Which.Should().Equal(Encoding.Latin1.GetBytes(Content));
    }

    [Fact]
    public void ADocumentWhoseStreamLengthLivesInAnObjectStreamCanBeSavedAndReadBack()
    {
        using var input = new MemoryStream(BuildDocumentWithTheStreamLengthInAnObjectStream());
        var inputDocument = Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Import);

        var merged = new PdfDocument();
        foreach (var page in inputDocument.Pages)
            merged.AddPage(page);

        using var output = new MemoryStream();
        merged.Save(output, false);

        output.Position = 0;
        Pdf.IO.PdfReader.Open(output, PdfDocumentOpenMode.Import).PageCount.Should().Be(1);
    }

    /// <summary>
    ///   Builds a single page document that declares the length of its content stream as "6 0 R",
    ///   where object 6 is an integer stored inside object stream 7. The document is addressed by
    ///   a cross-reference stream, because object streams require one.
    /// </summary>
    private static byte[] BuildDocumentWithTheStreamLengthInAnObjectStream()
    {
        var pdf = new MemoryStream();
        var offsets = new Dictionary<int, long>();

        void Write(string text) => pdf.Write(Encoding.Latin1.GetBytes(text));

        void WriteObject(int number, string body)
        {
            offsets[number] = pdf.Position;
            Write(number + " 0 obj\n" + body + "\nendobj\n");
        }

        Write("%PDF-1.5\n");
        WriteObject(1, "<</Type/Catalog/Pages 2 0 R>>");
        WriteObject(2, "<</Type/Pages/Kids[3 0 R]/Count 1>>");
        WriteObject(3, "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 100]" +
                       "/Resources<</Font<</F1 4 0 R>>>>/Contents 5 0 R>>");
        WriteObject(4, "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");
        // The length of this stream is object 6, which is not in the file but in object stream 7.
        WriteObject(5, "<</Length 6 0 R>>stream\n" + Content + "\nendstream");

        // Object stream 7 holds a single object: the integer 6, the length of the stream above.
        const string objectStreamHeader = "6 0 ";
        var objectStream = objectStreamHeader + Content.Length;
        WriteObject(7, "<</Type/ObjStm/N 1/First " + objectStreamHeader.Length +
                       "/Length " + objectStream.Length + ">>stream\n" + objectStream + "\nendstream");

        // The cross-reference stream, uncompressed, with three fields of 1, 4 and 2 bytes.
        var startOfCrossReferenceStream = pdf.Position;
        offsets[8] = startOfCrossReferenceStream;
        var xref = new MemoryStream();

        void WriteEntry(int type, long field2, int field3)
        {
            xref.WriteByte((byte)type);
            xref.WriteByte((byte)(field2 >> 24));
            xref.WriteByte((byte)(field2 >> 16));
            xref.WriteByte((byte)(field2 >> 8));
            xref.WriteByte((byte)field2);
            xref.WriteByte((byte)(field3 >> 8));
            xref.WriteByte((byte)field3);
        }

        WriteEntry(0, 0, 65535);
        for (var number = 1; number <= 5; number++)
            WriteEntry(1, offsets[number], 0);
        WriteEntry(2, 7, 0);    // Object 6 is the object at index 0 of object stream 7.
        WriteEntry(1, offsets[7], 0);
        WriteEntry(1, offsets[8], 0);
        var xrefBytes = xref.ToArray();

        Write("8 0 obj\n<</Type/XRef/Size 9/Root 1 0 R/W[1 4 2]/Length " + xrefBytes.Length + ">>stream\n");
        pdf.Write(xrefBytes);
        Write("\nendstream\nendobj\n");
        Write("startxref\n" + startOfCrossReferenceStream + "\n%%EOF\n");

        return pdf.ToArray();
    }
}
