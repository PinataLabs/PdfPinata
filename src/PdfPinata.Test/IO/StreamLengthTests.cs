using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Every stream in a written file declares a <c>/Length</c>, and the bytes have to agree with it.
/// </summary>
/// <remarks>
///   <para>
///     ISO 32000-1 7.3.8.1 counts <c>/Length</c> over the stream data alone and puts an end-of-line
///     marker after those bytes and before <c>endstream</c>, outside the count. The writer used to
///     write that marker only when the data did not already end with a newline — which left the
///     data's own last byte serving as the separator, so the file declared one byte more than it
///     appeared to hold.
///   </para>
///   <para>
///     Nothing in this library noticed, because its own reader takes <c>/Length</c> at its word and
///     gets the right bytes either way. veraPDF does not: it failed every PDF/A document in the
///     conformance corpus under clause 6.1.7, and the XMP packet — which ends with a newline by
///     construction — was the stream that showed it up. See <c>docs/specs/verapdf-validation.md</c>.
///   </para>
/// </remarks>
public class StreamLengthTests
{
    [Fact]
    public void EveryStreamDeclaresTheNumberOfBytesItHolds()
    {
        var bytes = Drawn(document => document.Options.WriteXmpMetadata = true);

        var streams = StreamsIn(bytes);

        streams.Should().NotBeEmpty("a page with text on it has a content stream and a font at least");
        foreach (var (obj, declared, actual) in streams)
        {
            actual.Should().Be(declared,
                $"object {obj} says it holds {declared} bytes");
        }
    }

    [Fact]
    public void AStreamWhoseDataEndsWithANewlineIsNoDifferent()
    {
        // The case that was wrong, and it has to be provoked deliberately: most streams are
        // compressed and end on whatever byte the compressor stopped at, so the defect hid behind
        // the one uncompressed stream in a document that happened to end with a newline.
        var bytes = Drawn(document =>
        {
            document.Options.CompressContentStreams = false;
            document.Options.WriteXmpMetadata = true;
        });

        var text = Encoding.Latin1.GetString(bytes);
        text.Should().Contain("xpacket", "the XMP packet is the stream that ends with a newline");

        foreach (var (obj, declared, actual) in StreamsIn(bytes))
            actual.Should().Be(declared, $"object {obj} ends with a newline of its own");
    }

    [Fact]
    public void TheSeparatorIsNotCountedAsData()
    {
        // The other half of the same rule, and the reason this cannot be fixed by counting the
        // newline instead: the separator is outside /Length, so a reader taking exactly /Length
        // bytes from after the "stream" keyword has to land on it.
        var bytes = Drawn(document => document.Options.CompressContentStreams = false);
        var text = Encoding.Latin1.GetString(bytes);

        foreach (Match match in Regex.Matches(text, @"/Length (\d+)[^>]*>>\s*stream\r?\n"))
        {
            var declared = int.Parse(match.Groups[1].Value);
            var start = match.Index + match.Length;

            text[(start + declared)..].Should().StartWith("\n",
                "the byte after the data is the end-of-line marker, and endstream follows it");
        }
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    static byte[] Drawn(Action<PdfDocument> arrange)
    {
        var document = new PdfDocument();
        arrange(document);

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("Length", new XFont("Arial", 20), XBrushes.Black, 20, 40);

        using var stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    /// <summary>
    ///   Every stream in the file: its object number, the length it declares, and the bytes actually
    ///   between the keyword and the end-of-line marker before <c>endstream</c>.
    /// </summary>
    /// <remarks>
    ///   Read out of the raw bytes rather than through <c>PdfReader</c>, deliberately. The reader
    ///   believes <c>/Length</c> and hands back that many bytes, so it would agree with the file
    ///   whatever the file said — which is exactly how this went unnoticed.
    /// </remarks>
    static List<(string Object, int Declared, int Actual)> StreamsIn(byte[] bytes)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var found = new List<(string, int, int)>();

        foreach (Match match in Regex.Matches(text, @"(\d+) 0 obj(.{0,600}?)stream\r?\n", RegexOptions.Singleline))
        {
            var length = Regex.Match(match.Groups[2].Value, @"/Length (\d+)");
            if (!length.Success)
                continue;

            var start = match.Index + match.Length;
            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0)
                continue;

            // One end-of-line marker sits between the data and the keyword and is not data.
            var actual = end - start;
            if (actual > 0 && text[end - 1] == '\n')
                actual--;

            found.Add((match.Groups[1].Value, int.Parse(length.Groups[1].Value), actual));
        }

        return found;
    }
}
