using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Filters;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   An XGraphics appends a content stream to the page the moment it is created, before anything
///   has been asked of it, so one closed without a mark being made leaves that stream behind
///   empty. Compressed, an empty stream becomes eight bytes of zlib framing around nothing, which
///   Acrobat reports as "An error exists on this page".
///   See https://github.com/empira/PDFsharp/issues/345.
/// </summary>
public class EmptyContentStreamTests
{
    [Fact]
    public void AnXGraphicsThatDrawsNothingLeavesTheContentStreamsAsTheyWere()
    {
        using var input = new MemoryStream(BuildDocumentWithThreeContentStreams());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        // The snippet on the issue: open an XGraphics and close it without drawing.
        using (XGraphics.FromPdfPage(document.Pages[0])) { }

        var saved = Save(document);

        ContentStreamsOf(saved).Should().HaveCount(3);
    }

    [Fact]
    public void NoContentStreamIsWrittenEmpty()
    {
        using var input = new MemoryStream(BuildDocumentWithThreeContentStreams());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        document.Options.CompressContentStreams = true;

        using (XGraphics.FromPdfPage(document.Pages[0])) { }

        var saved = Save(document);

        ContentStreamsOf(saved).Should().OnlyContain(stream => stream.Length > 0);
    }

    [Fact]
    public void AnEmptyStreamIsNotWrittenAsEightBytesOfCompressedNothing()
    {
        using var input = new MemoryStream(BuildDocumentWithThreeContentStreams());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);
        document.Options.CompressContentStreams = true;

        using (XGraphics.FromPdfPage(document.Pages[0])) { }

        var saved = Save(document);

        // What the deflater writes for no input at all: a header, an empty final block and a
        // checksum of nothing. That is the stream Acrobat objects to, and the file must not
        // hold it anywhere.
        var deflatedNothing = Filtering.FlateDecode.Encode(Array.Empty<byte>(), document.Options.FlateEncodeMode);
        deflatedNothing.Length.Should().BeGreaterThan(0, "the point of the test is that deflating nothing gives something");
        IndexOf(saved, deflatedNothing).Should().Be(-1);
    }

    [Fact]
    public void TheContentThatWasAlreadyOnThePageKeepsEveryMarkAndGainsOnlyItsQAndQ()
    {
        using var input = new MemoryStream(BuildDocumentWithThreeContentStreams());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        using (XGraphics.FromPdfPage(document.Pages[0])) { }

        var streams = ContentStreamsOf(Save(document));

        // Byte for byte what BuildDocumentWithThreeContentStreams put there, in the same order,
        // save for the "q" and " Q\n" that PdfContents.SetModified wraps the whole run in so that
        // anything drawn after it starts from the graphics state the page started from. Asserted
        // whole rather than by the operators looked for, so that dropping the empty stream cannot
        // quietly take a mark with it.
        streams.Should().Equal(
            "q\n0 0 1 RG 10 10 100 100 re S",
            "0 1 0 RG 20 20 100 100 re S",
            "1 0 0 RG 30 30 100 100 re S Q\n");
    }

    [Fact]
    public void AnXGraphicsThatDoesDrawKeepsItsContentStream()
    {
        using var input = new MemoryStream(BuildDocumentWithThreeContentStreams());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        using (var gfx = XGraphics.FromPdfPage(document.Pages[0]))
            gfx.DrawRectangle(XPens.Red, 40, 40, 100, 100);

        var streams = ContentStreamsOf(Save(document));

        streams.Should().HaveCount(4);
        streams[3].Should().Contain("re");
    }

    [Fact]
    public void AnXGraphicsThatIsNeverDisposedLeavesNoEmptyStreamEither()
    {
        using var input = new MemoryStream(BuildDocumentWithThreeContentStreams());
        var document = PdfPinata.Pdf.IO.PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        // Not disposed, so the content stream is still open when the document is written.
        XGraphics.FromPdfPage(document.Pages[0]);

        ContentStreamsOf(Save(document)).Should().HaveCount(3);
    }

    [Fact]
    public void APageWhoseOnlyContentIsEmptyIsStillAPage()
    {
        var document = new PdfDocument();
        _ = document.AddPage();

        using (XGraphics.FromPdfPage(document.Pages[0])) { }

        var saved = Save(document);

        var reopened = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(saved), PdfDocumentOpenMode.Import);
        reopened.PageCount.Should().Be(1);
        ContentStreamsOf(saved).Should().BeEmpty();
    }

    [Fact]
    public void AContentStreamTooShortToCompressIsWrittenAsItIs()
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = true;
        var page = document.AddPage();
        page.Contents.AppendContent().CreateStream(Bytes("q Q\n"));

        var saved = Save(document);

        // Deflating four bytes would make them twelve. The stream stays as it is, and without a
        // /FlateDecode filter to say otherwise.
        RawObjectsOf(saved).Should().Contain(body => body.Contains("q Q") && !body.Contains("FlateDecode"));
    }

    [Fact]
    public void AContentStreamWorthCompressingStillIs()
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = true;
        var page = document.AddPage();
        page.Contents.AppendContent().CreateStream(Bytes(string.Concat(Enumerable.Repeat("0 0 1 RG 10 10 100 100 re S\n", 100))));

        var saved = Save(document);

        RawObjectsOf(saved).Should().Contain(body => body.Contains("FlateDecode"));
        // And the whole point of compressing it: what came out is smaller than what went in.
        saved.Length.Should().BeLessThan(2700);
    }

    static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    /// <summary>
    ///   The unfiltered content of every content stream of the first page, in order.
    /// </summary>
    static IReadOnlyList<string> ContentStreamsOf(byte[] pdf)
    {
        var document = PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(pdf), PdfDocumentOpenMode.Modify);
        var contents = document.Pages[0].Contents;

        var streams = new List<string>();
        for (var idx = 0; idx < contents.Elements.Count; idx++)
        {
            var bytes = contents.Elements.GetDictionary(idx).Stream?.UnfilteredValue ?? Array.Empty<byte>();
            streams.Add(new string(bytes.Select(b => (char)b).ToArray()));
        }
        return streams;
    }

    /// <summary>
    ///   The indirect objects of the file as they were written, so that a test can see the filter
    ///   an object carries rather than the one reading it takes off again.
    /// </summary>
    static IReadOnlyList<string> RawObjectsOf(byte[] pdf)
    {
        var raw = new string(pdf.Select(b => (char)b).ToArray());
        var objects = new List<string>();
        for (var idx = 0; ;)
        {
            var start = raw.IndexOf(" 0 obj", idx, StringComparison.Ordinal);
            if (start < 0)
                return objects;
            var end = raw.IndexOf("endobj", start, StringComparison.Ordinal);
            if (end < 0)
                return objects;
            objects.Add(raw.Substring(start, end - start));
            idx = end + 6;
        }
    }

    static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var idx = 0; idx <= haystack.Length - needle.Length; idx++)
        {
            var found = true;
            for (var offset = 0; offset < needle.Length && found; offset++)
                found = haystack[idx + offset] == needle[offset];
            if (found)
                return idx;
        }
        return -1;
    }

    static byte[] Bytes(string text)
    {
        return text.Select(ch => (byte)ch).ToArray();
    }

    /// <summary>
    ///   A single page whose /Contents is an array of three streams, the way "Microsoft Print to
    ///   PDF" writes one.
    /// </summary>
    static byte[] BuildDocumentWithThreeContentStreams()
    {
        return RawPdf.Build([
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]/Contents[4 0 R 5 0 R 6 0 R]>>",
            RawPdf.Stream("", "0 0 1 RG 10 10 100 100 re S"),
            RawPdf.Stream("", "0 1 0 RG 20 20 100 100 re S"),
            RawPdf.Stream("", "1 0 0 RG 30 30 100 100 re S")
        ]);
    }
}
