using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   A subset whose glyphs all have no outline. Its <c>glyf</c> table has no glyph data to hold,
///   and it used to be written with no bytes at all: a table of length zero, at the same offset as
///   the <c>head</c> table after it. On a Debug build that failed an assertion in the table
///   directory; on a Release build it wrote a font that Windows refuses to load, that FreeType
///   reads as having no <c>glyf</c> at all, and that the OpenType Sanitizer in Chrome and Firefox
///   rejects. PDF renderers draw it either way, which is why nothing noticed.
/// </summary>
/// <remarks>
///   Liberation Sans Italic and Bold Italic draw <c>.notdef</c> as a blank, where Regular and Bold
///   draw a box. So a run of italic text the face has no glyph for, with no
///   <see cref="Fonts.GlobalFontSettings.FontFallback"/> to find another face, keeps only glyph 0 -
///   and so does a run of nothing but spaces, which needs no missing glyph at all. Both are here.
///   <para>
///     <see cref="TextShaperSensitiveAttribute"/> because the fallback seam has to stay unset while
///     the text is drawn, and the only tests that set it carry that key.
///   </para>
/// </remarks>
[TextShaperSensitive]
public class BlankGlyphSubsetTests
{
    /// <summary>A Han character, which none of the Liberation faces has a glyph for.</summary>
    internal static readonly string NotInTheFace = char.ConvertFromUtf32(0x4E2D);

    [Test]
    public void TheItalicFaceHasNoGlyphForTheCharacterAndABlankNotdef()
    {
        // The premise the tests below stand on. If the face ever gained the glyph, or a .notdef
        // with an outline, they would pass for having nothing to do.
        var italic = new TrueTypeGlyphs(File.ReadAllBytes(
            PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Italic.ttf")));

        italic.GlyphIndexOf(NotInTheFace[0]).Should().Be(0);
        italic.LengthOf(0).Should().Be(0, ".notdef has no outline in this face");
        italic.LengthOf(italic.GlyphIndexOf(' ')).Should().Be(0, "nor has the space");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void ASubsetOfBlankGlyphsStillHasGlyphDataOfItsOwn(bool missingGlyph)
    {
        var program = new TrueTypeGlyphs(ItalicFontProgram(Drawn(TextFor(missingGlyph))));

        for (var glyph = 0; glyph < program.NumGlyphs; glyph++)
            program.LengthOf(glyph).Should().Be(0, "every glyph the subset keeps is blank, glyph {0} too", glyph);

        var glyf = program.TableRecords.Single(record => record.Tag == "glyf");
        glyf.Length.Should().BeGreaterThan(0,
            "Windows refuses a font whose glyf has no bytes, FreeType reads it as missing and the OpenType Sanitizer rejects it");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void TheTableDirectoryOfThatSubsetDescribesTheFile(bool missingGlyph)
    {
        var program = new TrueTypeGlyphs(ItalicFontProgram(Drawn(TextFor(missingGlyph))));
        var records = program.TableRecords.OrderBy(record => record.Offset).ToList();

        foreach (var record in records)
        {
            (record.Offset % 4).Should().Be(0, "table '{0}' has to begin on a four-byte boundary", record.Tag);
            (record.Offset + record.Length).Should().BeLessThanOrEqualTo(program.FileLength,
                "table '{0}' has to lie inside the file", record.Tag);

            // 'head' is the exception the specification makes: its checksum is taken with the
            // checksumAdjustment field zeroed, so it cannot be recomputed from the bytes as written.
            if (record.Tag != "head")
            {
                record.Checksum.Should().Be(program.ChecksumOf(record.Offset, record.Length),
                    "the directory's checksum for '{0}' has to be the table's", record.Tag);
            }
        }

        for (var idx = 1; idx < records.Count; idx++)
        {
            var previous = records[idx - 1];
            records[idx].Offset.Should().BeGreaterThan(previous.Offset,
                "'{0}' and '{1}' are two tables and cannot begin at the same byte", records[idx].Tag, previous.Tag);
            records[idx].Offset.Should().BeGreaterThanOrEqualTo(previous.Offset + previous.Length,
                "'{0}' must not overlap '{1}'", records[idx].Tag, previous.Tag);
        }
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    /// <summary>A character the italic face has no glyph for, or a run of spaces.</summary>
    private static string TextFor(bool missingGlyph) => missingGlyph ? NotInTheFace : "   ";

    /// <summary>
    ///   A page with <paramref name="text"/> set in Liberation Sans Italic and a word in Regular
    ///   below it, through Unicode fonts so that each subset is a CIDFont's, saved and read back.
    /// </summary>
    internal static PdfDocument Drawn(string text)
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            gfx.DrawString(text, Font(XFontStyle.Italic), XBrushes.Black, new XPoint(40, 80));
            gfx.DrawString("Ink", Font(XFontStyle.Regular), XBrushes.Black, new XPoint(40, 160));
        }

        return document.Reopened();
    }

    private static XFont Font(XFontStyle style) =>
        new("Arial", 24, style, new XPdfFontOptions(PdfFontEncoding.Unicode));

    /// <summary>The font program embedded for the italic face.</summary>
    private static byte[] ItalicFontProgram(PdfDocument document)
    {
        var descriptor = document.Internals.GetAllObjects()
            .OfType<PdfDictionary>()
            .Where(d => d.Elements.GetName("/Type") == "/FontDescriptor")
            .Single(d => d.Elements.GetName("/FontName").EndsWith("Italic", StringComparison.Ordinal));

        var program = descriptor.Elements["/FontFile2"];
        return ((PdfDictionary)(program is PdfReference reference ? reference.Value : program))
            .Stream.UnfilteredValue;
    }
}

/// <summary>
///   The same subset put in front of the rasterizer: the page has to be drawn, with ink where the
///   regular word is and none where the blank glyph is. A reader that refused the embedded italic
///   program would substitute a face of its own, whose <c>.notdef</c> is seldom blank.
/// </summary>
[Rasterizing]
public sealed class BlankGlyphSubsetRenderingTests : IDisposable
{
    private const string OutDir = "Out/BlankGlyphSubset";

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    [Test]
    public void ADocumentWhoseItalicSubsetIsAllBlankRasterizes()
    {
        var page = _rasterized.FirstPageOf(
            BlankGlyphSubsetTests.Drawn(BlankGlyphSubsetTests.NotInTheFace), "blank-italic-subset");

        Count(page, new XRect(35, 130, 60, 40), Dark).Should().BeGreaterThan(0,
            "the regular word below it is drawn, so the page was");
        Count(page, new XRect(35, 50, 60, 40), Dark).Should().Be(0,
            "the italic face draws .notdef as a blank, and that is what a reader has to show");
    }

    private static bool Dark(IMagickColor<byte> colour) => colour is { R: < 128, G: < 128, B: < 128 };
}
