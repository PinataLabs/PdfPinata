using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   Whole files, each wrong in one way, that the reader used to answer with an exception about
///   something else - or with a document that failed later for no stated reason. Each is now
///   either read tolerantly or refused with a message naming what is wrong. See #335.
/// </summary>
/// <remarks>
///   Reading input the parser does not expect can hang rather than fail, so every test that opens
///   one of these files does it on a thread a timeout can abandon.
/// </remarks>
public class MalformedFileReadingTests
{
    // ----- A cross-reference stream with no usable /Size -----------------------------------------

    [Test, Timeout(10000)]
    [Arguments("")]
    [Arguments(" /Size /Five")]
    public async Task ACrossReferenceStreamWithNoUsableSizeAndNoIndexStillOpens(string sizeEntry)
    {
        // Without /Index, /Size is the only thing saying how many entries there are. Missing, it
        // was read as zero entries, nothing reached the table, and the document failed later with
        // no catalog. The entries are a whole number of /W-wide records, so the data says how many.
        var file = WithCrossReferenceStream(sizeEntry, [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] >>"
        ]);

        var pageCount = await Interruptibly.Run(() => Open(file).PageCount);

        pageCount.Should().Be(1);
    }

    // ----- /PageMode and /PageLayout -------------------------------------------------------------

    [Test]
    public void APageModeGivenByReferenceIsTheNameItRefersTo()
    {
        // The getter did not follow the reference, so it parsed " 0 R" as the name of a mode.
        var document = Open(RawPdf.Build([
            "<< /Type /Catalog /Pages 2 0 R /PageMode 4 0 R /PageLayout 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] >>",
            "/FullScreen",
            "/TwoColumnLeft"
        ]));

        document.PageMode.Should().Be(PdfPageMode.FullScreen);
        document.PageLayout.Should().Be(PdfPageLayout.TwoColumnLeft);
    }

    [Test]
    [Arguments("/PageMode /Bogus /PageLayout /Bogus")]
    [Arguments("/PageMode (UseOutlines) /PageLayout 3")]
    [Arguments("/PageMode /2 /PageLayout /1")]
    [Arguments("/PageMode 9 0 R /PageLayout 9 0 R")]
    public void APageModeOrLayoutThatNamesNoneReadsAsTheDefault(string entries)
    {
        // A name the enumeration does not have, something that is not a name, a number spelled as
        // a name - which Enum.Parse would have taken as a value - and a reference to nothing.
        var document = Open(RawPdf.Build([
            "<< /Type /Catalog /Pages 2 0 R " + entries + " >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] >>"
        ]));

        document.PageMode.Should().Be(PdfPageMode.UseNone);
        document.PageLayout.Should().Be(PdfPageLayout.SinglePage);
    }

    // ----- An object stream with no stream ----------------------------------------------------------

    [Test, Timeout(10000)]
    [Arguments("<< /Type /ObjStm /N 1 /First 4 >>")]
    [Arguments("42")]
    public async Task AnObjectStreamThatIsNotAStreamIsRefusedByName(string objectStream)
    {
        // Object 4 is what the cross-reference stream says object 5 is compressed in. A dictionary
        // with no data threw a NullReferenceException; anything else, an InvalidCastException.
        var file = WithCrossReferenceStream(" /Size 7", [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /ExtGState << /GS1 5 0 R >> >> >>",
            objectStream
        ], compressed: (5, 4));

        var open = () => Interruptibly.Run(() => Open(file));

        await open.Should().ThrowAsync<PdfReaderException>().WithMessage("*object stream*4*");
    }

    [Test, Timeout(10000)]
    public async Task AnObjectStreamTheFileDoesNotHaveIsRefusedByName()
    {
        // Object 5 is said to be compressed in object stream 9, and there is no object 9.
        var file = WithCrossReferenceStream(" /Size 7", [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /ExtGState << /GS1 5 0 R >> >> >>",
            "<< /Kind /Unrelated >>"
        ], compressed: (5, 9));

        var open = () => Interruptibly.Run(() => Open(file));

        await open.Should().ThrowAsync<PdfReaderException>().WithMessage("*object stream 9*no such object*");
    }

    [Test, Timeout(10000)]
    public async Task AnObjectStreamWithoutItsTypeIsStillReadAsOne()
    {
        // /Type /ObjStm is required, and some writers leave it out. "5 0 " is the header the
        // stream's /First skips: object 5 at offset 0.
        const string compressed = "<< /Type /ExtGState /CA 0.5 >>";
        var file = WithCrossReferenceStream(" /Size 7", [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /ExtGState << /GS1 5 0 R >> >> >>",
            $"<< /N 1 /First 4 /Length {4 + compressed.Length} >>\nstream\n5 0 {compressed}\nendstream"
        ], compressed: (5, 4));

        var graphicsState = await Interruptibly.Run(() => Open(file).Pages[0].Resources.Elements
            .GetDictionary("/ExtGState")!.Elements.GetDictionary("/GS1"));

        graphicsState.Should().NotBeNull();
        graphicsState!.Elements.GetName("/Type").Should().Be("/ExtGState");
        graphicsState.Elements.GetReal("/CA").Should().Be(0.5);
    }

    // ----- the documents ------------------------------------------------------------------------------

    private static PdfDocument Open(byte[] file) =>
        PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(file), PdfDocumentOpenMode.Modify);

    /// <summary>
    ///   The objects given, numbered from one, described by a cross-reference stream alone: no
    ///   classic table, and no trailer but the stream's own dictionary. The stream is the object
    ///   after the last one given, or after the compressed one when there is one, with
    ///   <paramref name="sizeEntry"/> written into it as it is and no /Index.
    /// </summary>
    /// <param name="compressed">
    ///   An object the stream describes as the first object in an object stream, by type 2 entry,
    ///   and the number of that object stream.
    /// </param>
    private static byte[] WithCrossReferenceStream(string sizeEntry, IReadOnlyList<string> objects,
        (int Number, int InStream)? compressed = null)
    {
        var pdf = new StringBuilder("%PDF-1.5\n");
        var entries = new StringBuilder();

        // Object 0 is free; then one type 1 entry per object, a two-byte offset and generation 0.
        entries.Append(Entry(0, 0, 255));
        for (var number = 1; number <= objects.Count; number++)
        {
            entries.Append(Entry(1, pdf.Length, 0));
            pdf.Append(number).Append(" 0 obj\n").Append(objects[number - 1]).Append("\nendobj\n");
        }

        var streamNumber = objects.Count + 1;
        if (compressed is { } entry)
        {
            entries.Append(Entry(2, entry.InStream, 0));
            streamNumber = entry.Number + 1;
        }

        var startOfStream = pdf.Length;
        entries.Append(Entry(1, startOfStream, 0));

        pdf.Append(streamNumber).Append(" 0 obj\n<< /Type /XRef").Append(sizeEntry)
            .Append(" /W [1 2 1] /Root 1 0 R /Length ").Append(entries.Length).Append(" >>\nstream\n")
            .Append(entries).Append("\nendstream\nendobj\n");
        pdf.Append("startxref\n").Append(startOfStream).Append("\n%%EOF\n");

        // One character is one byte, entries included, so the offsets written above hold.
        return Encoding.Latin1.GetBytes(pdf.ToString());

        static string Entry(int type, int field2, int field3) =>
            new([(char)type, (char)(field2 >> 8), (char)(field2 & 0xFF), (char)field3]);
    }
}
