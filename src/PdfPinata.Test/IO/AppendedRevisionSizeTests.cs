using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="PdfDocument.SaveIncremental"/> numbered what it appended from one past the highest
///   object number <em>in use</em>, and wrote <c>/Size</c> the same way. When the previous revision's
///   cross-reference section ended in free entries - its <c>/Size</c> larger than one past its
///   highest live object - the appended revision's <c>/Size</c> shrank, and a new object took a
///   number the previous revision had freed and still accounted for. ISO 32000-1 7.5.5 has
///   <c>/Size</c> count every object number the file has ever used, over all its revisions, so an
///   update may not make it smaller.
///
///   Both kinds of section had it: a classic table, whose number came from
///   <c>PdfDocument.SaveIncremental</c>, and a cross-reference stream, whose own number came from the
///   same count in <c>PdfCrossReferenceStreamWriter.WriteIncrementalSection</c>.
/// </summary>
public class AppendedRevisionSizeTests
{
    /// <summary>The <c>/Size</c> of the fixtures: objects 1 to 4 live, and 5 or 6 up to 11 free.</summary>
    private const int PreviousSize = 12;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheAppendedSizeDoesNotShrink(bool crossReferenceStream)
    {
        var original = OriginalDocument(crossReferenceStream);
        var updated = AppendChange(original, document => document.Info.Subject = "Changed");

        AppendedSize(Appended(updated, original.Length)).Should().BeGreaterThanOrEqualTo(PreviousSize,
            "an update may not make /Size smaller than the revision before it said");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANewObjectTakesANumberThePreviousRevisionNeverAccountedFor(bool crossReferenceStream)
    {
        var original = OriginalDocument(crossReferenceStream);
        var updated = AppendChange(original, document => _ = document.AddPage());

        var appended = Appended(updated, original.Length);
        var numbers = ObjectNumbersIn(appended);
        var fresh = numbers.Where(number => number > 4).ToList();

        fresh.Should().NotBeEmpty("a page added by the update is a new object");
        fresh.Should().OnlyContain(number => number >= PreviousSize,
            "a number the previous revision freed is still one it accounts for");
        AppendedSize(appended).Should().Be(numbers.Max() + 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheResultReadsBackTheChange(bool crossReferenceStream)
    {
        var updated = AppendChange(OriginalDocument(crossReferenceStream), document =>
        {
            document.Info.Subject = "Changed";
            _ = document.AddPage();
        });

        var reread = Reader.Open(new MemoryStream(updated), PdfDocumentOpenMode.Modify);
        reread.Info.Subject.Should().Be("Changed");
        reread.Info.Title.Should().Be("Original title");
        reread.PageCount.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoSuccessiveUpdatesKeepTheSize(bool crossReferenceStream)
    {
        var original = OriginalDocument(crossReferenceStream);
        var once = AppendChange(original, document => document.Info.Subject = "First");
        var twice = AppendChange(once, document => _ = document.AddPage());

        AppendedSize(Appended(twice, once.Length)).Should().BeGreaterThanOrEqualTo(AppendedSize(Appended(once, original.Length)));
        ObjectNumbersIn(Appended(twice, once.Length)).Where(number => number > 4)
            .Should().OnlyContain(number => number >= PreviousSize);

        var reread = Reader.Open(new MemoryStream(twice), PdfDocumentOpenMode.Modify);
        reread.Info.Subject.Should().Be("First");
        reread.PageCount.Should().Be(2);
    }

    [Fact]
    public void ASizeAnEarlierUpdateShrankIsNotBelievedOverTheOneBeforeIt()
    {
        // What this library's own SaveIncremental used to write: a revision whose /Size fell back to
        // one past the live objects. The larger /Size is then only in the revision before it, and
        // appending again from the newest trailer alone would reuse the numbers it freed.
        var original = WithShrunkenRevision(OriginalDocument(crossReferenceStream: false));
        var updated = AppendChange(original, document => _ = document.AddPage());

        var appended = Appended(updated, original.Length);
        ObjectNumbersIn(appended).Where(number => number > 4)
            .Should().OnlyContain(number => number >= PreviousSize);
        AppendedSize(appended).Should().BeGreaterThanOrEqualTo(PreviousSize);
        Reader.Open(new MemoryStream(updated), PdfDocumentOpenMode.Modify).PageCount.Should().Be(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASizeNoFileCouldHaveIsIgnored(bool crossReferenceStream)
    {
        // ISO 32000-1 Annex C allows 8,388,607 indirect objects. A /Size far beyond that is damage,
        // and believing it pushed the next number to int.MaxValue and the one after it negative.
        var original = OriginalDocument(crossReferenceStream, declaredSize: int.MaxValue);
        var updated = AppendChange(original, document => _ = document.AddPage());

        var appended = Appended(updated, original.Length);
        ObjectNumbersIn(appended).Should().OnlyContain(number => number > 0 && number < PreviousSize);
        AppendedSize(appended).Should().BeInRange(1, PreviousSize);
        Reader.Open(new MemoryStream(updated), PdfDocumentOpenMode.Modify).PageCount.Should().Be(2);
    }

    /// <summary>
    /// Appends to a classic fixture a hand-written revision that redefines the information
    /// dictionary and declares <c>/Size 5</c>, one past its live objects and below the
    /// <c>/Size 12</c> of the revision it points back at.
    /// </summary>
    private static byte[] WithShrunkenRevision(byte[] original)
    {
        var text = new StringBuilder(Encoding.Latin1.GetString(original));
        var previous = Regex.Match(text.ToString(), @"startxref\s+(\d+)", RegexOptions.RightToLeft).Groups[1].Value;

        var infoOffset = text.Length;
        text.Append("4 0 obj\n<< /Title (Original title) /Subject (Shrunk) >>\nendobj\n");
        var startxref = text.Length;
        text.Append($"xref\n4 1\n{infoOffset:0000000000} 00000 n \n");
        text.Append($"trailer\n<< /Size 5 /Root 1 0 R /Info 4 0 R /Prev {previous} ");
        text.Append("/ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>] >>\n");
        text.Append($"startxref\n{startxref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(text.ToString());
    }

    /// <summary>
    /// A one-page document whose only cross-reference section says <c>/Size 12</c> but whose live
    /// objects stop at 4 (or at 5, the cross-reference stream itself): the entries after that are
    /// free, as a writer leaves them after deleting the objects with the highest numbers.
    /// </summary>
    private static byte[] OriginalDocument(bool crossReferenceStream, int declaredSize = PreviousSize)
    {
        var bodies = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>",
            "<< /Title (Original title) >>"
        };

        var text = new StringBuilder("%PDF-1.5\n%âãÏÓ\n");
        var offsets = new List<int>();
        for (var index = 0; index < bodies.Length; index++)
        {
            offsets.Add(text.Length);
            text.Append($"{index + 1} 0 obj\n{bodies[index]}\nendobj\n");
        }

        var startxref = text.Length;
        const string ids = "/ID [<00112233445566778899AABBCCDDEEFF> <00112233445566778899AABBCCDDEEFF>]";
        if (crossReferenceStream)
        {
            // Object 5 is the stream; 6 to 11 are free. Rows of /W [1 4 2], uncompressed.
            offsets.Add(startxref);
            var rows = new List<byte>();
            void Row(int type, int field2, int field3)
            {
                rows.Add((byte)type);
                rows.AddRange(new[] { (byte)(field2 >> 24), (byte)(field2 >> 16), (byte)(field2 >> 8), (byte)field2 });
                rows.AddRange(new[] { (byte)(field3 >> 8), (byte)field3 });
            }
            Row(0, 0, 65535);
            foreach (var offset in offsets)
                Row(1, offset, 0);
            for (var number = offsets.Count + 1; number < PreviousSize; number++)
                Row(0, 0, 1);

            text.Append($"5 0 obj\n<< /Type /XRef /Size {declaredSize} /Index [0 {PreviousSize}] /W [1 4 2] /Root 1 0 R /Info 4 0 R {ids} /Length {rows.Count} >>\nstream\n");
            text.Append(Encoding.Latin1.GetString(rows.ToArray()));
            text.Append("\nendstream\nendobj\n");
        }
        else
        {
            text.Append($"xref\n0 {PreviousSize}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
                text.Append($"{offset:0000000000} 00000 n \n");
            for (var number = offsets.Count + 1; number < PreviousSize; number++)
                text.Append("0000000000 00001 f \n");
            text.Append($"trailer\n<< /Size {declaredSize} /Root 1 0 R /Info 4 0 R {ids} >>\n");
        }

        text.Append($"startxref\n{startxref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(text.ToString());
    }

    private static byte[] AppendChange(byte[] original, Action<PdfDocument> change)
    {
        using var source = new MemoryStream(original);
        var document = Reader.Open(source, PdfDocumentOpenMode.Append);

        change(document);

        using var output = new MemoryStream();
        document.SaveIncremental(output);
        return output.ToArray();
    }

    private static string Appended(byte[] updated, int originalLength) =>
        Encoding.Latin1.GetString(updated, originalLength, updated.Length - originalLength);

    /// <summary>The <c>/Size</c> the appended revision declares, in its trailer or its stream.</summary>
    private static int AppendedSize(string appended)
    {
        var matches = Regex.Matches(appended, @"/Size\s+(\d+)");
        matches.Should().HaveCount(1, "the appended revision declares its /Size once");
        return int.Parse(matches[0].Groups[1].Value);
    }

    /// <summary>The numbers of the objects the appended revision defines, its own index among them.</summary>
    private static List<int> ObjectNumbersIn(string appended) =>
        Regex.Matches(appended, @"(?m)^(\d+) \d+ obj\b").Select(match => int.Parse(match.Groups[1].Value)).ToList();
}
