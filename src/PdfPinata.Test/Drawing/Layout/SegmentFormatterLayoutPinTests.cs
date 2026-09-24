using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Drawing.Layout.enums;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   Every way <see cref="XTextSegmentFormatter"/> can be asked to lay segments out, pinned to the
///   bytes it wrote and the sizes it measured, so that a change to the loop that breaks its lines
///   is an observation rather than an intention - as <see cref="FormatterLayoutPinTests"/> is for
///   <see cref="XTextFormatter"/>.
/// </summary>
/// <remarks>
///   To re-capture after a deliberate change, write <see cref="OfEveryArrangement"/> to
///   <c>Assets/Layout/segment-formatter-baseline.txt</c> and read the diff before believing it.
/// </remarks>
public class SegmentFormatterLayoutPinTests
{
    [Fact]
    public void SegmentsAreLaidOutExactlyAsTheyWere()
    {
        var written = SplitByArrangement(Normalized(OfEveryArrangement()));
        var pinned = SplitByArrangement(Normalized(File.ReadAllText(BaselinePath)));

        written.Keys.Should().BeEquivalentTo(pinned.Keys);

        foreach (var arrangement in pinned.Keys)
            written[arrangement].Should().Be(pinned[arrangement],
                "the '" + arrangement + "' arrangement must lay out as it did before");
    }

    private static string BaselinePath =>
        Path.Combine(PathHelper.GetInstance().GetAssetPath("Layout"), "segment-formatter-baseline.txt");

    private static XFont Plain => new("Arial", 12, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);
    private static XFont Bold => new("Arial", 12, XFontStyle.Bold, XPdfFontOptions.WinAnsiDefault);
    private static XFont Large => new("Arial", 20, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);
    private static XFont Small => new("Arial", 8, XFontStyle.Italic, XPdfFontOptions.WinAnsiDefault);

    private static TextSegment Segment(string text, XFont font, double lineIndent = 0, bool skip = false)
        => new() { Text = text, Font = font, Brush = XBrushes.Black, LineIndent = lineIndent, SkipParagraphAlignment = skip };

    private const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be.";

    private static List<TextSegment> MixedSegments() =>
    [
        Segment("Mixed ", Plain),
        Segment("sizes", Large),
        Segment(" run on", Bold),
        Segment(" as one flow, " + Prose, Plain),
        Segment("tiny", Small),
        Segment("joined", Bold),
        Segment(" and then a line break\nafter which the text carries on\r\nacross a CRLF\rand a CR.", Plain),
    ];

    /// <summary>
    ///   Every arrangement, named so a failure says which one moved.
    /// </summary>
    private static IEnumerable<(string Name, XParagraphAlignment Alignment, XRect Area, Func<List<TextSegment>> Segments)> Arrangements()
    {
        var area = new XRect(40, 40, 260, 400);

        foreach (var alignment in new[]
                 {
                     XParagraphAlignment.Default, XParagraphAlignment.Left, XParagraphAlignment.Center,
                     XParagraphAlignment.Right, XParagraphAlignment.Justify
                 })
        {
            yield return ("mixed, " + alignment, alignment, area, MixedSegments);

            yield return ("indented and skipped, " + alignment, alignment, area, () =>
            [
                Segment("1.", Bold, skip: true),
                Segment(" " + Prose, Plain, lineIndent: 14),
                Segment("\n\nA new paragraph " + Prose, Plain, lineIndent: 6),
            ]);

            yield return ("narrow, " + alignment, alignment, new XRect(10, 10, 60, 400), () =>
            [
                Segment("Incomprehensibilities", Large),
                Segment(" a b ", Plain),
                Segment("glued", Bold),
                Segment("together", Plain),
                Segment(" word", Small),
            ]);

            yield return ("overflowing, " + alignment, alignment, new XRect(40, 40, 200, 60), MixedSegments);
        }

        yield return ("trailing breaks", XParagraphAlignment.Justify, area, () =>
        [
            Segment("Ends with line breaks\n\n", Plain),
            Segment("", Plain),
            Segment("  leading spaces here", Large),
            Segment("\n", Small),
        ]);
    }

    /// <summary>
    ///   One page per arrangement and the measured size of each, as one report.
    /// </summary>
    internal static string OfEveryArrangement()
    {
        var document = new PdfDocument();
        var sizes = new List<string>();

        foreach (var (_, alignment, area, segments) in Arrangements())
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);

            var formatter = new XTextSegmentFormatter(gfx) { Alignment = alignment };
            formatter.DrawString(segments(), area);

            var size = formatter.CalculateTextSize(segments(), area.Width);
            sizes.Add(size.Width.ToString("R", CultureInfo.InvariantCulture) + " x "
                      + size.Height.ToString("R", CultureInfo.InvariantCulture));
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        var saved = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfPinata.Pdf.IO.PdfDocumentOpenMode.Modify);

        var report = new StringBuilder();
        var index = 0;
        foreach (var (name, _, _, _) in Arrangements())
        {
            report.Append("--- ").Append(name).Append(" ---\n");
            report.Append("size ").Append(sizes[index]).Append('\n');
            report.Append(Encoding.ASCII.GetString(PageContent.Of(saved.Pages[index])).Replace("\r\n", "\n"));
            report.Append('\n');
            index++;
        }

        return report.ToString();
    }

    private static Dictionary<string, string> SplitByArrangement(string report)
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        string current = null;
        var body = new StringBuilder();

        foreach (var line in report.Split('\n'))
        {
            if (line.StartsWith("--- ", StringComparison.Ordinal) && line.EndsWith(" ---", StringComparison.Ordinal))
            {
                if (current != null)
                    pages[current] = body.ToString();

                current = line.Substring(4, line.Length - 8);
                body.Clear();
                continue;
            }

            body.Append(line).Append('\n');
        }

        if (current != null)
            pages[current] = body.ToString();

        return pages;
    }

    private static string Normalized(string text) => text.Replace("\r\n", "\n");
}
