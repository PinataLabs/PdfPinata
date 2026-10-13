using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.PageInk;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   A subset whose glyphs all have no outline. Its <c>glyf</c> table has no glyph data to hold,
///   and it used to be written with no bytes at all: a table of length zero, at the same offset as
///   the <c>head</c> table after it. On a Debug build that failed an assertion in the table
///   directory; on a Release build it wrote a <c>glyf</c> that FreeType reads as missing, and
///   that by itself makes Windows and the OpenType Sanitizer in Chrome and Firefox refuse a font
///   they otherwise load. PDF renderers draw it either way, which is why nothing noticed.
/// </summary>
/// <remarks>
///   Liberation Sans Italic and Bold Italic draw <c>.notdef</c> as a blank, where Regular and Bold
///   draw a box. So a run of italic text the face has no glyph for, with no
///   <see cref="GlobalFontSettings.FontFallback"/> to find another face, keeps only glyph 0 -
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
    [Arguments(true, PdfFontEncoding.Unicode)]
    [Arguments(false, PdfFontEncoding.Unicode)]
    [Arguments(false, PdfFontEncoding.WinAnsi)]
    public void ASubsetOfBlankGlyphsStillHasGlyphDataOfItsOwn(bool missingGlyph, PdfFontEncoding encoding)
    {
        var program = new TrueTypeGlyphs(ItalicFontProgram(Drawn(TextFor(missingGlyph), encoding)));

        for (var glyph = 0; glyph < program.NumGlyphs; glyph++)
            program.LengthOf(glyph).Should().Be(0, "every glyph the subset keeps is blank, glyph {0} too", glyph);

        var glyf = program.TableRecords.Single(record => record.Tag == "glyf");
        glyf.Length.Should().BeGreaterThan(0,
            "Windows refuses a font whose glyf has no bytes, FreeType reads it as missing and the OpenType Sanitizer rejects it");
    }

    [Test]
    [Arguments(true, PdfFontEncoding.Unicode)]
    [Arguments(false, PdfFontEncoding.Unicode)]
    [Arguments(false, PdfFontEncoding.WinAnsi)]
    public void TheTableDirectoryOfThatSubsetDescribesTheFile(bool missingGlyph, PdfFontEncoding encoding)
    {
        var program = new TrueTypeGlyphs(ItalicFontProgram(Drawn(TextFor(missingGlyph), encoding)));

        // The empty table used to begin at the same byte as 'head', the next one.
        program.DirectoryProblems().Should().BeEmpty();
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///   A character the italic face has no glyph for, or a run of spaces. Only the spaces are
    ///   drawn through WinAnsi too, as WinAnsi has no code for the character.
    /// </summary>
    private static string TextFor(bool missingGlyph) => missingGlyph ? NotInTheFace : "   ";

    /// <summary>
    ///   A page with <paramref name="text"/> set in Liberation Sans Italic and a word in Regular
    ///   below it, saved and read back. Through <see cref="PdfFontEncoding.Unicode"/> each subset is
    ///   a CIDFont's and has no <c>cmap</c>; through <see cref="PdfFontEncoding.WinAnsi"/> it is a
    ///   simple TrueType font's and keeps one, so the table directory is not the same.
    /// </summary>
    internal static PdfDocument Drawn(string text, PdfFontEncoding encoding = PdfFontEncoding.Unicode)
    {
        // With a fallback, the missing character would be drawn from another face and the italic
        // subset would no longer be what these tests are about.
        GlobalFontSettings.FontFallback.Should().BeNull("these tests need the italic face's own .notdef");

        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
        {
            gfx.DrawString(text, Font(XFontStyle.Italic, encoding), XBrushes.Black, new XPoint(40, 80));
            gfx.DrawString("Ink", Font(XFontStyle.Regular, encoding), XBrushes.Black, new XPoint(40, 160));
        }

        return document.Reopened();
    }

    private static XFont Font(XFontStyle style, PdfFontEncoding encoding) =>
        new("Arial", 24, style, new XPdfFontOptions(encoding));

    /// <summary>The font program embedded for the italic face.</summary>
    private static byte[] ItalicFontProgram(PdfDocument document) =>
        TrueTypeGlyphs.EmbeddedIn(document, name => name.EndsWith("Italic", StringComparison.Ordinal));
}

/// <summary>
///   The same subset put in front of the rasterizer: the page has to be drawn, with ink where the
///   regular word is and none where the blank glyph is. A reader that refused the embedded italic
///   program would substitute a face of its own, whose <c>.notdef</c> is seldom blank.
/// </summary>
/// <remarks>
///   This does not pin the fault: Ghostscript drew the zero-length table too, as PDFium and MuPDF
///   do. The tests above pin it. This one says that the table written instead still draws.
///   <see cref="RasterizingAttribute"/> carries no key, so this test runs alone, and the fallback
///   seam <see cref="BlankGlyphSubsetTests"/> needs unset cannot be set beside it.
/// </remarks>
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
