using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.IO;

/// <summary>
///   Documents whose objects lie more than 4 GiB into the file, which is where a byte offset no
///   longer fits an <see cref="int" /> nor four bytes of a cross-reference stream row.
/// </summary>
/// <remarks>
///   Nothing here is gigabytes in memory or on disk. Each file is a short head, a comment of one
///   repeated byte served by <see cref="SplicedStream" />, and a tail holding the real objects, so
///   every position written into the tail is counted from where the tail begins;
///   <see cref="SplicedPdf" /> does the counting.
/// </remarks>
public class LargeOffsetTests
{
    /// <summary>Where the first byte of a tail lies: past 4 GiB, so an offset takes five bytes.</summary>
    private const long Origin = (1L << 32) + 4096;

    private const string Content = "0 0 1 rg 20 20 100 100 re f";

    private const string RevisedContent = "1 0 0 rg 40 40 60 60 re f";

    // ----- Reading -------------------------------------------------------------------------------

    [Test]
    public void ObjectsAndAClassicTableBeyondFourGigabytesAreRead()
    {
        var pdf = new SplicedPdf(Origin);
        pdf.OnePage(Content);
        pdf.ClassicTable(firstId: 0, count: 5, trailer: "/Size 5 /Root 1 0 R");

        var document = Opened(pdf);

        document.PageCount.Should().Be(1);
        ContentOf(document).Should().Be(Content);
    }

    [Test]
    public void ObjectsBeyondFourGigabytesIndexedByACrossReferenceStreamAreRead()
    {
        var pdf = new SplicedPdf(Origin);
        pdf.OnePage(Content);

        // Five bytes for the offset, one for the generation; the stream's own entry is its last.
        var entries = new StringBuilder(SplicedPdf.Row(0, 0, 0));
        for (var id = 1; id <= 4; id++)
            entries.Append(SplicedPdf.Row(1, pdf.PositionOf(id), 0));
        var startxref = pdf.Here;
        entries.Append(SplicedPdf.Row(1, startxref, 0));

        pdf.Object(5, "<< /Type /XRef /Size 6 /W [1 5 1] /Root 1 0 R /Length " + entries.Length +
                      " >>stream\n" + entries + "\nendstream");
        pdf.Append("startxref\n" + startxref + "\n%%EOF\n");

        var document = Opened(pdf);

        document.PageCount.Should().Be(1);
        ContentOf(document).Should().Be(Content);
    }

    [Test]
    public void APrevBeyondTwoGigabytesIsFollowed()
    {
        // The second revision replaces the content and nothing else, so the page itself is found
        // only by following /Prev back to the first.
        var pdf = new SplicedPdf(Origin);
        pdf.OnePage(Content);
        var first = pdf.ClassicTable(firstId: 0, count: 5, trailer: "/Size 5 /Root 1 0 R");

        pdf.Object(4, SplicedPdf.Stream(RevisedContent));
        pdf.ClassicTable(firstId: 4, count: 1, trailer: "/Size 5 /Root 1 0 R /Prev " + first);
        first.Should().BeGreaterThan(int.MaxValue);

        var document = Opened(pdf);

        document.PageCount.Should().Be(1);
        ContentOf(document).Should().Be(RevisedContent);
    }

    [Test]
    public void AnXRefStmBeyondTwoGigabytesIsRead()
    {
        // ISO 32000-1 7.5.8.4: object 7 is free in the classic table and lives in object stream 5,
        // and only the cross-reference stream /XRefStm names says so.
        var pdf = new SplicedPdf(Origin);
        pdf.OnePage(Content, resources: "/Resources << /ExtGState << /GS1 7 0 R >> >>");

        var packed = "7 0\n<< /Type /ExtGState /CA 0.5 >>";
        pdf.Object(5, "<< /Type /ObjStm /N 1 /First 4 /Length " + packed.Length + " >>stream\n" +
                      packed + "\nendstream");
        pdf.Object(6, "<< /Type /XRef /Size 8 /W [1 2 1] /Index [7 1] /Root 1 0 R /Length 4 >>stream\n" +
                      "\u0002\u0000\u0005\u0000\nendstream");
        pdf.PositionOf(6).Should().BeGreaterThan(int.MaxValue);

        pdf.ClassicTable(firstId: 0, count: 7, trailer: "/Size 8 /Root 1 0 R /XRefStm " + pdf.PositionOf(6),
            free: [7]);

        var document = Opened(pdf);

        document.PageCount.Should().Be(1);
        var graphicsState = document.Pages[0].Elements.GetDictionary("/Resources")?
            .Elements.GetDictionary("/ExtGState")?
            .Elements.GetDictionary("/GS1");
        graphicsState.Should().NotBeNull("object 7 is found only through the stream");
        graphicsState!.Elements.GetReal("/CA").Should().Be(0.5);
    }

    // ----- Writing -------------------------------------------------------------------------------

    [Test]
    public void ACrossReferenceStreamIndexingObjectsBeyondFourGigabytesIsReadBack()
    {
        // The writer is told it is writing at Origin, so every offset it records is past 4 GiB; the
        // bytes it wrote are then served at that offset, behind a comment, and read back.
        var written = new OffsetStream(Origin);
        NewDocument().Save(written, closeStream: true);
        var bytes = written.Written;

        // Checked first because a truncated offset points into the comment, and reading from there
        // runs out of memory after minutes rather than failing.
        Encoding.Latin1.GetString(bytes).Should().Contain("/W [1 5 2]",
            "an offset past 4 GiB takes five bytes, and no more than it needs");

        var document = PdfPinata.Pdf.IO.PdfReader.Open(
            new SplicedStream(SplicedPdf.Head, Origin - SplicedPdf.Head.Length - 1, (byte)'A', [(byte)'\n', .. bytes]),
            PdfDocumentOpenMode.Import);

        document.PageCount.Should().Be(1);
        ContentOf(document).Should().Be(ContentOf(Reopened(Saved.Bytes(NewDocument()))));
    }

    [Test]
    public void AnOrdinaryDocumentKeepsFourBytesForTheOffset()
    {
        // Widening only when an offset needs it keeps every file that was written before
        // byte for byte what it was.
        Encoding.Latin1.GetString(Saved.Bytes(NewDocument())).Should().Contain("/W [1 4 2]");
    }

    // ----- The documents -------------------------------------------------------------------------

    private static PdfDocument Opened(SplicedPdf pdf) =>
        PdfPinata.Pdf.IO.PdfReader.Open(pdf.ToStream(), PdfDocumentOpenMode.Import);

    private static PdfDocument Reopened(byte[] bytes) =>
        PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);

    private static string ContentOf(PdfDocument document) =>
        Encoding.Latin1.GetString(document.Pages[0].Elements.GetDictionary("/Contents").Stream.UnfilteredValue);

    private static PdfDocument NewDocument()
    {
        var document = new PdfDocument();
        document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        var gfx = XGraphics.FromPdfPage(document.AddPage());
        gfx.DrawRectangle(XBrushes.Blue, 20, 20, 100, 40);
        return document;
    }

    /// <summary>
    ///   A stream that reports every position as <c>offset</c> further on than it is, so a writer
    ///   records offsets for a file it believes began that far before it.
    /// </summary>
    private sealed class OffsetStream(long offset) : Stream
    {
        private readonly MemoryStream _inner = new();

        internal byte[] Written => _inner.ToArray();

        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => offset + _inner.Length;

        public override long Position
        {
            get => offset + _inner.Position;
            set => _inner.Position = value - offset;
        }

        public override void Write(byte[] buffer, int index, int count) => _inner.Write(buffer, index, count);
        public override void Flush() { }
        public override int Read(byte[] buffer, int index, int count) => throw new NotSupportedException();
        public override long Seek(long position, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
