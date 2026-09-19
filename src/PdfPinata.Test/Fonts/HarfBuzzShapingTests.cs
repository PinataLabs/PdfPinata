using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   <see cref="HarfBuzzTextShaper"/> shaping real text against a real face. Where
///   <c>TextShapingSeamTests</c> asks whether the library believes a shaper, this asks whether the
///   shaper is right.
/// </summary>
/// <remarks>
///   <para>
///     Nearly all of it calls the shaper directly rather than registering it, both because the
///     shaper is worth testing without a document in the way and because a globally registered
///     shaper that answered every run would change the output of whichever test happened to be
///     drawing beside it. The one end-to-end test wraps it in a shaper selective on a sentinel
///     string, the same technique <c>TextShapingSeamTests</c> uses and for the same reason.
///   </para>
///   <para>
///     The face is Liberation Sans, which the tests already ship. It carries no Arabic and no
///     Devanagari, so what can be proved here is the machinery - that <c>GSUB</c> and <c>GPOS</c>
///     really ran, that characters and glyphs are not one to one, that a right-to-left run comes
///     back reordered. Joining and reordering scripts need a licensed face of their own; see
///     <c>docs/specs/text-shaping-and-bidi.md</c>.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class HarfBuzzShapingTests
{
    const int LiberationUnitsPerEm = 2048;

    static ShapingFont Liberation(double emSize = 20)
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Fonts", "LiberationSans-Regular.ttf"));

        return new ShapingFont("Liberation Sans", "LiberationSans-Regular",
            "HarfBuzzShapingTests/LiberationSans-Regular",
            isBold: false, isItalic: false, emSize, LiberationUnitsPerEm, bytes);
    }

    static ShapedRun Shape(string text, XTextDirection direction = XTextDirection.LeftToRight,
        string script = "latn")
    {
        using var shaper = new HarfBuzzTextShaper();
        return shaper.Shape(text.AsSpan(), Liberation(), direction, script, null);
    }

    static IEnumerable<int> Clusters(ShapedRun run) => run.Glyphs.Select(glyph => glyph.Cluster);

    // ----- GPOS really ran -----------------------------------------------------------------------

    [Theory]
    [InlineData("AV")]
    [InlineData("To")]
    public void APairThatKernsIsNarrowerTogetherThanApart(string pair)
    {
        var together = Shape(pair);
        var apart = Shape(pair[..1]).Width + Shape(pair[1..]).Width;

        together.Glyphs.Should().HaveCount(2, "kerning moves glyphs, it does not merge them");
        together.Width.Should().BeLessThan(apart,
            "the face has a kern pair for this and GPOS applied it - which is the whole "
            + "difference between shaped and summed per-character widths");
    }

    // ----- GSUB really ran -----------------------------------------------------------------------

    [Fact]
    public void TwoCharactersCanComeBackAsOneGlyph()
    {
        // "e" and a combining acute accent, written as escapes rather than as a literal so that
        // normalizing this file cannot quietly turn it into one character and the test into
        // nothing. The face has a precomposed e-acute and GSUB composes them into it, which is the
        // same many-to-one shape a ligature has.
        var run = Shape("\u0065\u0301");

        run.Glyphs.Should().HaveCount(1, "two characters, one glyph");
        Clusters(run).Should().Equal(new[] { 0 },
            "and the glyph belongs to the cluster that starts at the first of them, which is what "
            + "/ToUnicode and /ActualText read to say the glyph stands for both");
    }

    [Fact]
    public void ASurrogatePairIsOneCharacterAndSoOneCluster()
    {
        // The unshaped path looks up each UTF-16 code unit on its own and draws two .notdef; a
        // shaper knows the pair is one character. The face has no emoji, so the glyph is still
        // .notdef - but there is one of it, not two.
        var run = Shape("\U0001F600", script: null);

        run.Glyphs.Should().HaveCount(1);
        Clusters(run).Should().Equal(new[] { 0 });
    }

    // ----- order ---------------------------------------------------------------------------------

    [Fact]
    public void ALeftToRightRunComesBackInTheOrderItWasWritten()
    {
        var run = Shape("abc");

        Clusters(run).Should().Equal(new[] { 0, 1, 2 });
        run.Direction.Should().Be(XTextDirection.LeftToRight);
    }

    [Fact]
    public void ARightToLeftRunComesBackAlreadyReversed()
    {
        var ltr = Shape("abc");
        var rtl = Shape("abc", XTextDirection.RightToLeft);

        Clusters(rtl).Should().Equal(new[] { 2, 1, 0 },
            "the glyphs are in visual order, leftmost first, so a renderer draws every run the "
            + "same way and only the clusters record which way it was written");
        rtl.Glyphs.Select(glyph => glyph.GlyphId)
            .Should().Equal(ltr.Glyphs.Select(glyph => glyph.GlyphId).Reverse());
        rtl.Width.Should().Be(ltr.Width, "the same glyphs are the same width in either order");
    }

    // ----- units ---------------------------------------------------------------------------------

    [Fact]
    public void AdvancesComeBackInDesignUnitsSoOneShapingServesEverySize()
    {
        // Shaped at 20 points and at 200, the run is the same - which is what lets a shaped run be
        // cached and drawn at any size, and why advances are not in points.
        using var shaper = new HarfBuzzTextShaper();
        var small = shaper.Shape("Wave".AsSpan(), Liberation(), XTextDirection.LeftToRight, "latn", null);
        var large = shaper.Shape("Wave".AsSpan(), Liberation(200), XTextDirection.LeftToRight, "latn", null);

        large.Width.Should().Be(small.Width);
        small.UnitsPerEm.Should().Be(LiberationUnitsPerEm);
        small.WidthAt(20).Should().BeApproximately(small.Width * 20 / LiberationUnitsPerEm, 1e-9);
    }

    // ----- Arabic, which is what the whole gap exists for -----------------------------------------

    const int NotoUnitsPerEm = 1000;

    static ShapingFont Noto(double emSize = 20)
    {
        var bytes = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory, "Assets", "Fonts", "NotoSansArabic-Regular.ttf"));

        return new ShapingFont("Noto Sans Arabic", "NotoSansArabic-Regular",
            "HarfBuzzShapingTests/NotoSansArabic-Regular",
            isBold: false, isItalic: false, emSize, NotoUnitsPerEm, bytes);
    }

    static ShapedRun Arabic(string text)
    {
        using var shaper = new HarfBuzzTextShaper();
        return shaper.Shape(text.AsSpan(), Noto(), XTextDirection.RightToLeft, "arab", null);
    }

    // One letter, meem. Written as an escape rather than a literal so that a source file mixing
    // right-to-left text with left-to-right code cannot be misread.
    const string Meem = "\u0645";

    [Fact]
    public void OneLetterHasFourFormsAndTheShaperPicksBetweenThem()
    {
        // This is the defect the gap exists for, in one test. Arabic letters have initial, medial,
        // final and isolated forms; Unicode stores the letter and not the form. So three meems in
        // a row are three different glyphs - initial, medial, final - and the unshaped path, which
        // asks the cmap for each character on its own, draws the isolated form three times and the
        // letters never join.
        var alone = Arabic(Meem);
        var two = Arabic(Meem + Meem);
        var three = Arabic(Meem + Meem + Meem);

        alone.Glyphs.Should().HaveCount(1);
        two.Glyphs.Should().HaveCount(2);
        three.Glyphs.Should().HaveCount(3);

        var isolated = alone.Glyphs[0].GlyphId;
        var forms = three.Glyphs.Select(glyph => glyph.GlyphId).ToList();

        forms.Should().OnlyHaveUniqueItems(
            "the same letter three times over is an initial, a medial and a final form");
        forms.Should().NotContain(isolated, "and none of them is the isolated form");
        two.Glyphs.Select(glyph => glyph.GlyphId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void JoiningIsSomethingTheCmapCannotDoOnItsOwn()
    {
        const string salam = "\u0633\u0644\u0627\u0645";

        var joined = Arabic(salam).Glyphs.Select(glyph => glyph.GlyphId).ToList();

        // The same letters shaped one at a time, which is the best the cmap can manage and what
        // this library drew before there was a shaper.
        var separately = salam.Reverse()
            .SelectMany(letter => Arabic(letter.ToString()).Glyphs.Select(glyph => glyph.GlyphId))
            .ToList();

        joined.Should().HaveCount(4);
        joined.Should().NotEqual(separately, "letters in a word are not the letters on their own");
    }

    [Fact]
    public void AnAttachedMarkIsAGlyphOfItsOwnThatTakesNoRoomAndSitsOffThePen()
    {
        // The dots of these letters are separate glyphs that GPOS places against the letter they
        // belong to: no advance of their own, and an offset in both directions. Nothing in a Latin
        // face produces this, and it is what ShapedGlyph.OffsetX and OffsetY are for.
        var run = Arabic("\u0639\u0631\u0628\u064A");

        run.Glyphs.Count.Should().BeGreaterThan(4, "there are more glyphs here than characters");

        var marks = run.Glyphs.Where(glyph => glyph.Advance == 0).ToList();
        marks.Should().NotBeEmpty("a mark advances the pen by nothing - it hangs off its letter");
        marks.Should().Contain(glyph => !glyph.IsOnBaselineOrigin,
            "and it is displaced from the pen position, or it would sit on top of the letter");
        marks.Should().Contain(glyph => glyph.OffsetY != 0,
            "including vertically, which no TJ array can express");
    }

    [Fact]
    public void AMarkAndItsLetterShareOneCluster()
    {
        var run = Arabic("\u0639\u0631\u0628\u064A");

        // Six glyphs, four clusters: a mark belongs to the character it was drawn for, which is
        // what lets /ToUnicode and /ActualText say the pair means one letter.
        Clusters(run).Distinct().Should().HaveCount(4);
        Clusters(run).Should().BeInDescendingOrder("a right-to-left run is handed over reversed");
    }

    // ----- what it will not do -------------------------------------------------------------------

    [Fact]
    public void AFaceWithNoGlyphForACharacterDrawsNotdefAndSaysNothingAboutIt()
    {
        // Liberation Sans has no Arabic. Shaping is not fallback: the run comes back as .notdef,
        // and nothing warns - even though the Arabic face sitting beside it in the test assets
        // would have drawn it. Fixing that is item 5 of the spec, the fallback chain, and it is
        // not built.
        var run = Shape("\u0633\u0644\u0627\u0645", XTextDirection.RightToLeft, "arab");

        run.Glyphs.Should().OnlyContain(glyph => glyph.GlyphId == 0);
        Clusters(run).Should().Equal(new[] { 3, 2, 1, 0 });
    }

    [Fact]
    public void ShapingNothingIsAnEmptyRunAndNotANull()
    {
        var run = Shape(string.Empty);

        run.Should().NotBeNull();
        run.Glyphs.Should().BeEmpty();
        run.Width.Should().Be(0);
        run.UnitsPerEm.Should().Be(LiberationUnitsPerEm);
    }

    [Fact]
    public void RubbishWhereAFontShouldBeIsNotAllowedToBringAPageDown()
    {
        var rubbish = new ShapingFont("Nonsense", "Nonsense", "HarfBuzzShapingTests/nonsense",
            false, false, 12, 1000, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        using var shaper = new HarfBuzzTextShaper();
        var act = () => shaper.Shape("abc".AsSpan(), rubbish, XTextDirection.LeftToRight, "latn", null);

        // Either a declined run or a run of .notdef, but never an exception out of the middle of a
        // page being drawn - a caller who cannot shape is still entitled to a document.
        act.Should().NotThrow();
    }

    [Fact]
    public void AShaperThatHasBeenDisposedSaysSoRatherThanCrashingTheProcess()
    {
        var shaper = new HarfBuzzTextShaper();
        shaper.Shape("a".AsSpan(), Liberation(), XTextDirection.LeftToRight, "latn", null);
        shaper.Dispose();

        var act = () => shaper.Shape("a".AsSpan(), Liberation(), XTextDirection.LeftToRight, "latn", null);

        act.Should().Throw<ObjectDisposedException>(
            "the faces behind it are native handles, and using one after it is freed is the sort "
            + "of mistake that takes the process rather than the test");
    }

    [Fact]
    public void OneShaperServesSeveralThreadsAtOnce()
    {
        // A shaper is registered once for the application domain, so it is shared by whatever is
        // drawing. A HarfBuzz font cannot be shaped with from two threads at once, and this is the
        // test that the shaper knows it.
        using var shaper = new HarfBuzzTextShaper();
        var expected = Shape("Waverley");
        var results = new ShapedRun[64];

        Parallel.For(0, results.Length, idx =>
            results[idx] = shaper.Shape("Waverley".AsSpan(), Liberation(),
                XTextDirection.LeftToRight, "latn", null));

        foreach (var run in results)
        {
            run.Glyphs.Select(glyph => glyph.GlyphId)
                .Should().Equal(expected.Glyphs.Select(glyph => glyph.GlyphId));
            run.Width.Should().Be(expected.Width);
        }
    }

    // ----- end to end ----------------------------------------------------------------------------

    /// <summary>
    ///   HarfBuzz, but only for one string, so that registering it cannot change what any other
    ///   test running beside it measures or draws. See the remarks on this class.
    /// </summary>
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

    // Distinctive enough that nothing else in the suite draws it, and it holds the AV and To pairs
    // the face kerns.
    const string Sentinel = "HarfBuzz AVails To kern this";

    [Fact]
    public void RegisteringTheShaperNarrowsTextTheFaceKerns()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        var font = new XFont("Arial", 20);

        var unshaped = gfx.MeasureString(Sentinel, font).Width;

        using var shaper = new OnlyFor(Sentinel);
        GlobalFontSettings.TextShaper = shaper;
        try
        {
            var shaped = gfx.MeasureString(Sentinel, font).Width;

            shaped.Should().BeLessThan(unshaped,
                "measuring goes through the seam, so the kerning the face asks for is now in the "
                + "width - which is exactly the change that will move the golden images when a "
                + "shaper is registered by default");
        }
        finally
        {
            GlobalFontSettings.TextShaper = null;
        }
    }
}
