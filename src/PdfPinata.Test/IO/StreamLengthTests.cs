using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

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

    // ── A stream handed to a dictionary rather than created in it ──────────────────────────────

    [Fact]
    public void ADictionaryGivenAnotherDictionarysStreamDeclaresItsLength()
    {
        // CreateStream writes /Length; the Stream setter used to write nothing, so a dictionary
        // given its stream that way reached the writer with no /Length at all. A Debug.Assert in
        // the writer was the only thing saying so, and a Release build has none.
        var data = "a stream with no /Length of its own"u8.ToArray();

        var bytes = SavedWith(document =>
        {
            var source = new PdfDictionary(document);
            source.CreateStream(data);
            return Given(document, source.Stream);
        }, out var number);

        LengthsOf(bytes, number).Should().Be((data.Length, data.Length));
        ReopenedStreamValue(bytes, null).Should().Equal(data);
    }

    [Fact]
    public void AStreamChangedThroughTheDictionaryItCameFromIsNotLeftWithAStaleLength()
    {
        // A stream keeps /Length current only in the dictionary that owns it. Shared with a second
        // one and then given longer data, it left the second declaring the old length.
        var before = "short"u8.ToArray();
        var after = "considerably longer than it was when it was shared"u8.ToArray();

        var bytes = SavedWith(document =>
        {
            var source = new PdfDictionary(document);
            source.CreateStream(before);
            var target = Given(document, source.Stream);
            source.Stream.Value = after;
            return target;
        }, out var number);

        LengthsOf(bytes, number).Should().Be((after.Length, after.Length));
        ReopenedStreamValue(bytes, null).Should().Equal(after);
    }

    [Fact]
    public void ACopiedStreamBelongsToTheDictionaryItIsGivenTo()
    {
        // PdfStream.Clone answers a stream belonging to no dictionary, and assigning its Value then
        // threw a NullReferenceException looking for one to write /Length into.
        var data = "copied"u8.ToArray();
        var replaced = "copied, then replaced"u8.ToArray();

        var bytes = SavedWith(document =>
        {
            var source = new PdfDictionary(document);
            source.CreateStream(data);
            var target = Given(document, source.Stream.Clone());
            target.Stream.Value = replaced;
            return target;
        }, out var number);

        LengthsOf(bytes, number).Should().Be((replaced.Length, replaced.Length));
        ReopenedStreamValue(bytes, null).Should().Equal(replaced);
    }

    [Fact]
    public void AStreamWhoseLengthEntryWasRemovedIsWrittenWithOneAgain()
    {
        var data = "its /Length taken away by hand"u8.ToArray();

        var bytes = SavedWith(document =>
        {
            var target = new PdfDictionary(document);
            document.Internals.AddObject(target);
            target.CreateStream(data);
            target.Elements.Remove(PdfDictionary.PdfStream.Keys.Length);
            document.Internals.Catalog.Elements[TargetKey] = target.Reference;
            return target;
        }, out var number);

        LengthsOf(bytes, number).Should().Be((data.Length, data.Length));
    }

    [Theory]
    [InlineData(PdfDocumentSecurityLevel.Encrypted40Bit)]
    [InlineData(PdfDocumentSecurityLevel.Encrypted128Bit)]
    public void AnEncryptedStreamDeclaresTheLengthOfTheBytesWritten(PdfDocumentSecurityLevel level)
    {
        // The writer encrypts a stream as it writes it, after /Length has been written. That is
        // sound only while the cipher keeps the length, which RC4 — the only one this library
        // writes with — does; this pins that the count and the bytes still agree.
        const string password = "owner";
        var data = "encrypted on the way out, and not a byte longer for it"u8.ToArray();

        var bytes = SavedWith(document =>
        {
            var settings = document.SecuritySettings;
            settings.DocumentSecurityLevel = level;
            settings.OwnerPassword = password;
            settings.UserPassword = "";

            var source = new PdfDictionary(document);
            source.CreateStream(data);
            return Given(document, source.Stream);
        }, out var number);

        LengthsOf(bytes, number).Should().Be((data.Length, data.Length));
        ReopenedStreamValue(bytes, password).Should().Equal(data);
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    private static byte[] Drawn(Action<PdfDocument> arrange)
    {
        var document = new PdfDocument();
        arrange(document);

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("Length", new XFont("Arial", 20), XBrushes.Black, 20, 40);

        return Saved.Bytes(document);
    }

    /// <summary>
    ///   Puts <paramref name="stream"/> in a new indirect dictionary through the public setter, and
    ///   makes that dictionary reachable from the catalog.
    /// </summary>
    private static PdfDictionary Given(PdfDocument document, PdfDictionary.PdfStream stream)
    {
        var target = new PdfDictionary(document);
        document.Internals.AddObject(target);
        target.Stream = stream;
        document.Internals.Catalog.Elements[TargetKey] = target.Reference;
        return target;
    }

    private const string TargetKey = "/PinataTestStream";

    private static byte[] SavedWith(Func<PdfDocument, PdfDictionary> arrange, out int objectNumber)
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var target = arrange(document);
        objectNumber = target.Reference.ObjectNumber;

        return Saved.Bytes(document);
    }

    /// <summary>
    ///   The <c>/Length</c> object <paramref name="number"/> declares in the raw file, or null when it
    ///   declares none, and the number of bytes actually written between <c>stream</c> and the
    ///   end-of-line marker before <c>endstream</c>. Read out of the bytes, because the reader
    ///   recovers a missing length by looking for <c>endstream</c> and so would not say.
    /// </summary>
    private static (int? Declared, int Written) LengthsOf(byte[] bytes, int number)
    {
        var text = Encoding.Latin1.GetString(bytes);
        var obj = Regex.Match(text, $@"(?<!\d){number} 0 obj((?:(?!endobj).)*?)stream\r?\n",
            RegexOptions.Singleline);
        obj.Success.Should().BeTrue($"object {number} is written as a stream");

        var start = obj.Index + obj.Length;
        var written = text.IndexOf("\nendstream", start, StringComparison.Ordinal) - start;

        var length = Regex.Match(obj.Groups[1].Value, @"/Length (\d+)");
        return (length.Success ? int.Parse(length.Groups[1].Value) : null, written);
    }

    private static byte[] ReopenedStreamValue(byte[] bytes, string password)
    {
        using var saved = new MemoryStream(bytes);
        var document = Reader.Open(saved, password, PdfDocumentOpenMode.Import);
        var target = (PdfDictionary)document.Internals.Catalog.Elements.GetObject(TargetKey);
        return target.Stream.Value;
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
    private static List<(string Object, int Declared, int Actual)> StreamsIn(byte[] bytes)
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
