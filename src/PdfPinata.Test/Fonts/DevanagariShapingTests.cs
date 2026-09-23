using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Pdf;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   What a Devanagari face does that no other face in these assets can.
/// </summary>
/// <remarks>
///   <para>
///     Arabic exercises joining: a letter has initial, medial, final and isolated forms, and each is
///     still one glyph for one character. Devanagari breaks that assumption in both directions and is
///     the only face here that can.
///   </para>
///   <para>
///     <b>A cluster is longer than one character.</b> A conjunct joins consonants into a single
///     glyph — <c>क</c> + virama + <c>ष</c> is three characters and one glyph — so the character to
///     glyph map is genuinely many to one, which Arabic never makes it.
///   </para>
///   <para>
///     <b>A glyph is drawn before the character it was written after.</b> The vowel sign
///     <c>ि</c> is written after its consonant and drawn to the left of it, so shaping has to move
///     it. That is reordering inside a left-to-right run, which the bidirectional algorithm knows
///     nothing about and only a shaper can do.
///   </para>
///   <para>
///     Every expectation here was read off the face rather than assumed, because a wrong constant
///     would pass silently.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class DevanagariShapingTests
{
    // Noto Sans Devanagari's em, which is not Liberation's 2048.
    private const int UnitsPerEm = 1000;

    private const string Ka = "क";        // DEVANAGARI LETTER KA
    private const string Virama = "्";    // DEVANAGARI SIGN VIRAMA, which joins the two around it
    private const string Ssa = "ष";       // DEVANAGARI LETTER SSA
    private const string VowelI = "ि";    // DEVANAGARI VOWEL SIGN I, drawn to the left of its consonant

    private const string Conjunct = Ka + Virama + Ssa;   // three characters, one glyph
    private const string Namaste = "नमस्ते";

    private static ShapingFont Face(double emSize = 20)
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansDevanagari-Regular.ttf"));

        return new ShapingFont(PinnedFontResolver.DevanagariFamilyName, "NotoSansDevanagari-Regular",
            "DevanagariShapingTests/NotoSansDevanagari-Regular",
            isBold: false, isItalic: false, emSize, UnitsPerEm, bytes);
    }

    private static ShapedRun Shape(string text)
    {
        using var shaper = new HarfBuzzTextShaper();
        return shaper.Shape(text.AsSpan(), Face(), XTextDirection.LeftToRight, "deva", null);
    }

    private static XFont Font() => new(PinnedFontResolver.DevanagariFamilyName, 20);

    private sealed class Shaping : IDisposable
    {
        internal Shaping() => GlobalFontSettings.TextShaper = new HarfBuzzTextShaper();

        public void Dispose() => GlobalFontSettings.TextShaper = null;
    }

    // ----- a cluster longer than one character ------------------------------------------------------

    [Fact]
    public void AConjunctIsThreeCharactersDrawnAsOneGlyph()
    {
        var run = Shape(Conjunct);

        Conjunct.Should().HaveLength(3);
        run.Glyphs.Should().HaveCount(1,
            "the virama joins the consonants either side of it into a single conjunct glyph");
    }

    [Fact]
    public void TheConjunctIsNotEitherOfTheLettersItIsMadeFrom()
    {
        // Guards against a face or a shaper that quietly dropped the virama and drew the first
        // consonant on its own, which would also be one glyph.
        var conjunct = Shape(Conjunct).Glyphs.Single().GlyphId;

        conjunct.Should().NotBe(Shape(Ka).Glyphs.Single().GlyphId);
        conjunct.Should().NotBe(Shape(Ssa).Glyphs.Single().GlyphId);
    }

    [Fact]
    public void EveryGlyphOfAClusterPointsAtTheStartOfIt()
    {
        // The cluster is the character-to-glyph map and the only place the association exists.
        // Devanagari is where it stops being an identity: three characters, one cluster.
        Shape(Conjunct).Glyphs.Select(glyph => glyph.Cluster).Should().Equal(new[] { 0 });
    }

    // ----- a glyph drawn before the character it follows ---------------------------------------------

    [Fact]
    public void AVowelSignIsDrawnBeforeTheConsonantItIsWrittenAfter()
    {
        // The reordering case. The vowel sign is the second character and the first glyph.
        var run = Shape(Ka + VowelI);

        run.Glyphs.Should().HaveCount(2);
        run.Glyphs[1].GlyphId.Should().Be(Shape(Ka).Glyphs.Single().GlyphId,
            "the consonant is drawn second, after the vowel written after it");
        run.Glyphs[0].GlyphId.Should().NotBe(run.Glyphs[1].GlyphId);
    }

    [Fact]
    public void TheReorderedPairIsStillOneCluster()
    {
        // Both glyphs stand for the whole two characters, because neither can be attributed to one
        // of them: the pair was rearranged as a unit.
        Shape(Ka + VowelI).Glyphs.Select(glyph => glyph.Cluster).Should().Equal(new[] { 0, 0 });
    }

    [Fact]
    public void AConjunctAndAReorderedVowelHappenTogether()
    {
        var run = Shape(Conjunct + VowelI);

        (Conjunct + VowelI).Should().HaveLength(4);
        run.Glyphs.Should().HaveCount(2, "four characters, one conjunct glyph and one vowel glyph");
        run.Glyphs[1].GlyphId.Should().Be(Shape(Conjunct).Glyphs.Single().GlyphId,
            "the conjunct is unchanged and the vowel moved in front of it");
    }

    [Fact]
    public void AWordIsFewerGlyphsThanCharactersAndItsClustersSkip()
    {
        // "namaste": six characters, five glyphs, and the clusters are not 0..5 - a cluster covering
        // two characters means the next one starts two on. Anything reading clusters as indices into
        // the glyph list rather than into the text breaks here and nowhere else in these assets.
        var run = Shape(Namaste);

        Namaste.Should().HaveLength(6);
        run.Glyphs.Should().HaveCount(5);
        run.Glyphs.Select(glyph => glyph.Cluster).Should().BeInAscendingOrder();
        run.Glyphs.Select(glyph => glyph.Cluster).Distinct().Should().NotBeEquivalentTo(
            Enumerable.Range(0, 6), "some character is not the start of a cluster of its own");
    }

    // ----- and the same thing through the whole drawing path -------------------------------------

    [Fact]
    public void TheDrawnGlyphsAreTheShapedOnes()
    {
        using var _ = new Shaping();

        DrawnText.Glyphs(DrawnText.Page(Conjunct, Font()))
            .Should().Equal([(int)Shape(Conjunct).Glyphs.Single().GlyphId],
                "what the renderer writes is what the shaper chose, not a per-character lookup");
    }

    [Fact]
    public void MeasuringAgreesWithDrawing()
    {
        using var _ = new Shaping();

        // A conjunct is narrower than the letters it is made of, so a measurement that had not been
        // shaped would be wider than the page. This is the assertion that says both paths shape.
        var font = Font();
        var conjunct = DrawnText.MeasuredWidth(Conjunct, font);
        var separately = DrawnText.MeasuredWidth(Ka, font) + DrawnText.MeasuredWidth(Ssa, font);

        conjunct.Should().BeLessThan(separately);
    }

    [Fact]
    public void WithNoShaperTheCharactersAreDrawnOneByOne()
    {
        // What a consumer who takes no HarfBuzz dependency gets, written down rather than left to be
        // discovered. Reordering a right-to-left run is done in the core and needs no shaper;
        // Devanagari is the case that does need one, because nothing about a conjunct or a
        // prepended vowel can be worked out from the characters alone.
        GlobalFontSettings.TextShaper.Should().BeNull("this test relies on there being none");

        DrawnText.Glyphs(DrawnText.Page(Conjunct, Font()))
            .Should().HaveCount(3, "one cmap lookup per character, and no conjunct");
    }

    // ----- what a line breaker still does not know ------------------------------------------------

    [Fact]
    public void ALineIsBrokenBetweenConjunctsAndNotInsideOne()
    {
        // A conjunct is three characters and one glyph, so a break between its characters would be a
        // break inside a glyph. Nothing in the line breaker knows about cluster boundaries; what
        // this pins is that the widths in this case do not lead it into one, so that if it ever does
        // start splitting them the change is visible here rather than in somebody's document.
        using var _ = new Shaping();

        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var formatter = new XTextFormatter(gfx);
            var word = Conjunct + Conjunct + Conjunct;
            formatter.DrawString(string.Join(" ", Enumerable.Repeat(word, 8)), Font(),
                XBrushes.Black, new XRect(20, 20, 60, 400));
        }

        var conjunct = (int)Shape(Conjunct).Glyphs.Single().GlyphId;
        var drawn = DrawnText.Glyphs(page);

        drawn.Should().HaveCount(24, "eight words of three conjuncts, one glyph each");
        drawn.Should().OnlyContain(glyph => glyph == conjunct,
            "a conjunct split across a line break would shape as its parts instead");
    }
}
