using System;
using System.Collections.Generic;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   A glyph that swallowed several characters is a lie to anything reading the page: the glyph run
///   says one thing and the text said another. <c>/ToUnicode</c> has always told a text extractor
///   the truth about it. This is the other half — <c>/ActualText</c> on a marked-content sequence
///   around the glyph, which is what assistive technology reads and what PDF/UA asks for.
/// </summary>
/// <remarks>
///   <para>
///     Item 6c of <c>docs/specs/tagged-pdf-accessibility.md</c>, which could not be done until
///     shaping landed because until then a string went to the page one character at a time and there
///     was never anything to disagree about.
///   </para>
///   <para>
///     The shaper is registered for one sentinel string only, the technique the rest of the shaping
///     tests use: a globally registered shaper answering every run would change what any test
///     drawing beside it produces.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class LigatureActualTextTests
{
    /// <summary>
    ///   U+0065 LATIN SMALL LETTER E followed by U+0301 COMBINING ACUTE ACCENT. Liberation Sans has a
    ///   precomposed e-acute and GSUB composes the pair into it, so two characters come back as one
    ///   glyph — the same many-to-one shape a ligature has, in the one face the suite ships.
    /// </summary>
    /// <remarks>
    ///   Built from character codes rather than written as text, because the whole test rests on
    ///   these being two characters. Anything that normalizes this file — an editor, a tool, a
    ///   well-meaning format pass — would compose them into the single character U+00E9, and then
    ///   the face draws one glyph for one character, there is no disagreement to report, and every
    ///   assertion below passes while testing nothing at all.
    /// </remarks>
    static readonly string Composed = new string(new[] { (char)0x0065, (char)0x0301 });

    /// <summary>Distinctive enough that nothing else in the suite draws it.</summary>
    static readonly string Sentinel = "ActualText " + Composed + " here";

    [Fact]
    public void AGlyphStandingForTwoCharactersSaysWhichTwo()
    {
        var content = Latin1(Draw(Sentinel));

        content.Should().Contain("/ActualText",
            "the glyph run says one glyph where the text said two characters, and nothing else on "
            + "the page says what it stands for");
        content.Should().Contain("EMC", "a sequence that is opened has to be closed");
    }

    [Fact]
    public void TheSpanNamesTheCharactersTheGlyphSwallowed()
    {
        var content = Latin1(Draw(Sentinel));

        // Read back rather than compared to a hand-written literal: what matters is that the bytes
        // in the file decode to the two characters, not how they were escaped getting there.
        ActualTextIn(content).Should().Be(Composed);
    }

    [Fact]
    public void TheSequenceStaysInsideTheTextObject()
    {
        // The opposite of what a structural BDC does, and deliberately. This one claims something
        // about a single glyph, so it has to wrap one show-text operator - where a structural
        // sequence has to be able to wrap the whole text object and is written in graphic mode. An
        // ET between the BT and the BDC would mean the pen had been sent back to the origin.
        var content = Latin1(Draw(Sentinel));

        var bdc = content.IndexOf("/ActualText", StringComparison.Ordinal);
        var textObject = content.LastIndexOf("BT\n", bdc, StringComparison.Ordinal);
        var ended = content.IndexOf("ET", textObject, StringComparison.Ordinal);

        textObject.Should().BeGreaterThan(-1, "the glyphs are shown inside a text object");
        ended.Should().BeGreaterThan(bdc, "and the sequence is inside it rather than around it");
    }

    [Fact]
    public void TheGlyphsEitherSideOfTheLigatureAreStillDrawn()
    {
        // The run is cut into three to wrap the middle, and losing either end would be the kind of
        // defect that only shows up as missing text on a page nobody looked at.
        var page = Page(Sentinel);

        var shown = TextOperators.ShowTextOperators(page);

        shown.Should().HaveCount(3,
            "what precedes the ligature, the ligature itself, and what follows it");
    }

    [Fact]
    public void AStringWithNoLigatureInItIsWrittenExactlyAsBefore()
    {
        // The guard that keeps every existing document byte-identical. A run where every glyph
        // stands for one character - which is nearly every run ever written - never reaches the
        // partitioning at all.
        const string plain = "ActualText plain here";

        var content = Latin1(Draw(plain, plain));

        content.Should().NotContain("/ActualText").And.NotContain("BDC");
        TextOperators.ShowTextOperators(Page(plain, plain)).Should().HaveCount(1, "one Tj, as always");
    }

    [Fact]
    public void OnlyTheLigatureInTheSentinelIsWrapped()
    {
        // The composed glyph is the only thing in the sentinel that swallowed a character, so
        // exactly one span - the rest of the run is ordinary text and says nothing about itself.
        var content = Latin1(Draw(Sentinel));

        Occurrences(content, "/ActualText").Should().Be(1);
    }

    [Fact]
    public void AMarkAttachedToALetterIsNotALigature()
    {
        // A base and the mark riding on it: two characters in one cluster, drawn with two glyphs.
        // That is how every shaper reports Devanagari and an Arabic letter carrying a vowel sign, so
        // it is not a curiosity - it is most of the text on those pages. Nothing swallowed anything,
        // /ToUnicode already says what each glyph stands for, and a span here would announce a
        // ligature that is not there, once per syllable, cutting a run that used to be a single Tj
        // into one operator per cluster.
        var glyphs = OneGlyphEach(Clustered);
        glyphs[9] = new ShapedGlyph(glyphs[9].GlyphId, glyphs[8].Cluster, 0);

        var page = Fixed(glyphs);

        Latin1(PageContent.Of(page)).Should().NotContain("/ActualText",
            "no glyph stands for more characters than it was drawn with");
        TextOperators.ShowTextOperators(page).Should().HaveCount(1,
            "and so the run is shown in one operator, as it was before there was a shaper");
    }

    [Fact]
    public void FewerGlyphsThanCharactersInTheSameClusterStillIs()
    {
        // The other side of the same fixture, so that neither test passes because the assertion was
        // unreachable. The same cluster over the same two characters, and this time one glyph drawn
        // for them - which is the only difference between a ligature and a mark on a letter.
        var glyphs = OneGlyphEach(Clustered);
        glyphs.RemoveAt(9);

        var page = Fixed(glyphs);

        Latin1(PageContent.Of(page)).Should().Contain("/ActualText");
        ActualTextIn(Latin1(PageContent.Of(page))).Should().Be(Clustered.Substring(8, 2),
            "the two characters the surviving glyph was left standing for");
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///   The sentinel for the two tests that state the shape of a cluster rather than shape text.
    /// </summary>
    /// <remarks>
    ///   Character 8 is the <c>m</c> and character 9 the <c>a</c>, which is the pair those two
    ///   fixtures merge into one cluster.
    /// </remarks>
    const string Clustered = "cluster mark";

    /// <summary>
    ///   One glyph per character, each in a cluster of its own — a run in which nothing was shaped.
    /// </summary>
    /// <remarks>
    ///   What the two cluster tests start from and each change in one line, so that the difference
    ///   between them is the only thing either is testing. Handing the glyphs over rather than
    ///   shaping text keeps the subject the shape of the cluster instead of what one particular face
    ///   does with one particular string: two glyphs against two characters is the case, and a
    ///   fixture states it exactly where a face only demonstrates it.
    /// </remarks>
    static List<ShapedGlyph> OneGlyphEach(string text)
    {
        var glyphs = new List<ShapedGlyph>(text.Length);
        for (int idx = 0; idx < text.Length; idx++)
            glyphs.Add(new ShapedGlyph((ushort)(40 + idx), idx, 500));

        return glyphs;
    }

    /// <summary>
    ///   <see cref="Clustered"/> drawn with exactly the glyphs given.
    /// </summary>
    static PdfPage Fixed(IReadOnlyList<ShapedGlyph> glyphs)
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = false;

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            GlobalFontSettings.TextShaper = new Handed(Clustered, glyphs);
            try
            {
                gfx.DrawString(Clustered, new XFont("Arial", 20), XBrushes.Black, 20, 40);
            }
            finally
            {
                GlobalFontSettings.TextShaper = null;
            }
        }

        return page;
    }

    static byte[] Draw(string sentinel, string text = null)
    {
        var document = new PdfDocument();

        // Left readable so the assertions can look at what was written rather than at what a
        // decompressor makes of it.
        document.Options.CompressContentStreams = false;

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            using var shaper = new OnlyFor(sentinel);
            GlobalFontSettings.TextShaper = shaper;
            try
            {
                gfx.DrawString(text ?? sentinel, new XFont("Arial", 20), XBrushes.Black, 20, 40);
            }
            finally
            {
                GlobalFontSettings.TextShaper = null;
            }
        }

        return PageContent.Of(page);
    }

    static PdfPage Page(string sentinel, string text = null)
    {
        var document = new PdfDocument();
        document.Options.CompressContentStreams = false;

        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            using var shaper = new OnlyFor(sentinel);
            GlobalFontSettings.TextShaper = shaper;
            try
            {
                gfx.DrawString(text ?? sentinel, new XFont("Arial", 20), XBrushes.Black, 20, 40);
            }
            finally
            {
                GlobalFontSettings.TextShaper = null;
            }
        }

        return page;
    }

    /// <summary>
    ///   The characters the first <c>/ActualText</c> in the content stream names.
    /// </summary>
    /// <remarks>
    ///   Written as a hex string rather than as a literal, because a text string holding anything
    ///   outside PDFDocEncoding goes out as UTF-16 and <c>PdfEncoders</c> writes UTF-16 in hex. That
    ///   is the better shape for a content stream anyway: no byte in it needs escaping, so nothing
    ///   inside the string can be mistaken for the end of it.
    /// </remarks>
    static string ActualTextIn(string content)
    {
        var at = content.IndexOf("/ActualText", StringComparison.Ordinal);
        if (at < 0)
            return null;

        var open = content.IndexOf('<', at);
        var close = content.IndexOf('>', open);
        if (open < 0 || close < 0)
            return null;

        var digits = content.Substring(open + 1, close - open - 1);
        var bytes = new byte[digits.Length / 2];
        for (int idx = 0; idx < bytes.Length; idx++)
            bytes[idx] = Convert.ToByte(digits.Substring(idx * 2, 2), 16);

        // UTF-16 big-endian behind a byte-order mark, which is how a PDF text string says it is not
        // PDFDocEncoded.
        return bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF
            ? Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
            : Encoding.Latin1.GetString(bytes);
    }

    static int Occurrences(string text, string what)
    {
        int count = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0;
             at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>
    ///   HarfBuzz, but only for one string, so that registering it cannot change what any other test
    ///   running beside it measures or draws.
    /// </summary>
    /// <summary>
    ///   A shaper that answers one string with glyphs it was handed, and declines everything else.
    /// </summary>
    sealed class Handed : ITextShaper
    {
        readonly string _mine;
        readonly IReadOnlyList<ShapedGlyph> _glyphs;

        internal Handed(string mine, IReadOnlyList<ShapedGlyph> glyphs)
        {
            _mine = mine;
            _glyphs = glyphs;
        }

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
            => text.SequenceEqual(_mine.AsSpan())
                ? new ShapedRun(_glyphs, font.UnitsPerEm, XTextDirection.LeftToRight)
                : null;
    }

    sealed class OnlyFor : ITextShaper, IDisposable
    {
        readonly string _mine;
        readonly HarfBuzzTextShaper _shaper = new HarfBuzzTextShaper();

        internal OnlyFor(string mine) => _mine = mine;

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
            => text.SequenceEqual(_mine.AsSpan())
                ? _shaper.Shape(text, font, direction, script, language)
                : null;

        public void Dispose() => _shaper.Dispose();
    }
}
