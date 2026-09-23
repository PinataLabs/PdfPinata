using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   The <c>/W</c> array of a descendant CIDFont. It used to be written as one <c>c [w]</c> entry
///   per glyph; ISO 32000-1 9.7.4.3 lets a run of consecutive CIDs share one entry,
///   <c>c [w1 w2 …]</c>, and a line of Latin text is almost nothing but such runs.
/// </summary>
public class CidWidthArrayTests
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789 .,;:!?";

    [Fact]
    public void EachRunOfConsecutiveCidsIsOneEntry()
    {
        var entries = Entries(WidthArrayOf(Saved(Alphabet)));

        entries.Should().NotBeEmpty();
        for (var idx = 1; idx < entries.Count; idx++)
        {
            var previous = entries[idx - 1];
            entries[idx].First.Should().BeGreaterThan(previous.First + previous.Widths.Count,
                "CIDs following on from the previous entry belong in that entry's bracket");
        }

        entries.Count.Should().BeLessThan(entries.Sum(entry => entry.Widths.Count),
            "the letters of the alphabet are consecutive glyphs in Liberation Sans, so at least one "
            + "entry has to hold more than one width");
    }

    [Fact]
    public void EveryCidKeepsTheWidthItsFontGivesIt()
    {
        var font = new TrueTypeGlyphs(File.ReadAllBytes(
            PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf")));

        var widths = new Dictionary<int, int>();
        foreach (var entry in Entries(WidthArrayOf(Saved(Alphabet))))
        {
            for (var offset = 0; offset < entry.Widths.Count; offset++)
                widths.Add(entry.First + offset, entry.Widths[offset]);
        }

        foreach (var character in Alphabet.Distinct())
            widths.Should().ContainKey(font.GlyphIndexOf(character),
                "the glyph for '{0}' is drawn, so /W has to give it a width", character);

        foreach (var (cid, width) in widths)
            width.Should().Be(font.AdvanceInThousandths(cid),
                "CID {0} is the glyph index of the same number, and its width is the font's", cid);
    }

    // ── Arranging and reading ───────────────────────────────────────────────────────────────────

    private sealed record Entry(int First, IReadOnlyList<int> Widths);

    /// <summary>
    ///   The entries of a <c>/W</c> array, every one of which this library writes in the
    ///   <c>c [w1 w2 …]</c> form; the <c>cFirst cLast w</c> form would fail the cast, as it should.
    /// </summary>
    private static List<Entry> Entries(PdfArray w)
    {
        var entries = new List<Entry>();
        for (var at = 0; at < w.Elements.Count; at += 2)
        {
            var first = w.Elements.GetInteger(at);
            var widths = (PdfArray)w.Elements[at + 1];
            entries.Add(new Entry(first,
                [..Enumerable.Range(0, widths.Elements.Count).Select(widths.Elements.GetInteger)]));
        }
        return entries;
    }

    private static PdfDocument Saved(string text)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 12, XFontStyle.Regular,
                new XPdfFontOptions(PdfFontEncoding.Unicode));
            gfx.DrawString(text, font, XBrushes.Black, 20, 40);
        }

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return Reader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    private static PdfArray WidthArrayOf(PdfDocument document)
    {
        var type0 = document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Single(dictionary => dictionary.Elements.GetName("/Subtype") == "/Type0");

        var descendant = type0.Elements.GetArray("/DescendantFonts").Elements.GetDictionary(0);
        var w = descendant.Elements.GetArray("/W");
        w.Should().NotBeNull("a descendant font this library writes always states its widths");
        return w;
    }
}
