using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   What happens to a character the chosen face has no glyph for.
/// </summary>
/// <remarks>
///   <para>
///     Before this, nothing: <c>Drawing/XGlyphTypeface.cs</c> said "No fallback - just stop", and a
///     character the face could not draw became <c>.notdef</c> - an empty box - with no warning at
///     any layer, even when a face that could draw it was registered in the same process. That is
///     what these tests are about, and the Arabic face sitting beside Liberation Sans in the test
///     assets is exactly the situation.
///   </para>
///   <para>
///     Every fallback installed here answers for its own handful of characters and nothing else,
///     for the same reason the stub shapers answer for one sentinel string each:
///     <see cref="GlobalFontSettings.FontFallback"/> is one setting for the whole application
///     domain, and a fallback answering for every character would quietly redraw whatever else the
///     suite happened to be drawing beside it.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class FontFallbackTests
{
    // Served by PinnedFontResolver itself rather than registered here. It used to be registered on
    // first use, and a family registered on first use means whatever the first caller made it mean:
    // anything else in the assembly drawing this family beforehand got Liberation Sans, which has no
    // Arabic at all, and the library cached that answer for the rest of the run. These tests then
    // drew four .notdef boxes and blamed the shaper.
    private const string ArabicFamily = PinnedFontResolver.ArabicFamilyName;

    // "arabi" - four Arabic letters, none of which Liberation Sans has any glyph for.
    private const string Arabic = "\u0639\u0631\u0628\u064A";

    // U+200D ZERO WIDTH JOINER, written as a code point because it is invisible in a source file.
    private const string Joiner = "\u200D";

    private static XFont Latin() => new("Arial", 20);

    private static XFont ArabicFont() => new(ArabicFamily, 20);

    /// <summary>
    ///   A fallback that answers only for the characters given to it, so that installing it cannot
    ///   change what any other test draws.
    /// </summary>
    private sealed class Only : IFontFallback
    {
        private readonly HashSet<int> _mine;
        private readonly string[] _families;

        /// <param name="characters">
        ///   The characters to answer for, written as a string. Taken by code point rather than by
        ///   <c>char</c>, so that an astral character written as a surrogate pair in the source is
        ///   one entry here and matches the one code point the seam is asked about.
        /// </param>
        /// <param name="families">The families this fallback answers with, in the order given.</param>
        internal Only(string characters, params string[] families)
        {
            _mine = new HashSet<int>();
            for (var idx = 0; idx < characters.Length; idx++)
            {
                if (char.IsHighSurrogate(characters[idx]) && idx + 1 < characters.Length
                    && char.IsLowSurrogate(characters[idx + 1]))
                {
                    _mine.Add(char.ConvertToUtf32(characters[idx], characters[idx + 1]));
                    idx++;
                }
                else
                {
                    _mine.Add(characters[idx]);
                }
            }

            _families = families;
        }

        public IEnumerable<string> FamiliesFor(int codePoint, bool isBold, bool isItalic)
            => _mine.Contains(codePoint) ? _families : Enumerable.Empty<string>();
    }

    private sealed class Installed : IDisposable
    {
        internal Installed(IFontFallback fallback) => GlobalFontSettings.FontFallback = fallback;

        public void Dispose() => GlobalFontSettings.FontFallback = null;
    }

    // ----- the seam ---------------------------------------------------------------------------------

    [Fact]
    public void NothingIsFallenBackToUntilSomethingIsRegistered()
    {
        // Like the shaper and unlike the other three seams, reading this one unset is not an error:
        // there is a working behaviour behind it, and it is the one this library always had.
        GlobalFontSettings.FontFallback.Should().BeNull();
    }

    [Fact]
    public void AFallbackCanBeTakenAwayAgain()
    {
        var fallback = new FontFallbackList("Whatever");

        using (new Installed(fallback))
            GlobalFontSettings.FontFallback.Should().BeSameAs(fallback);

        GlobalFontSettings.FontFallback.Should().BeNull();
    }

    [Fact]
    public void AListWithNoNameInItIsRefused()
    {
        Action naming = () => _ = new FontFallbackList("Noto Sans Arabic", "  ");

        naming.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsFontFallbackSetAnswersWhetherAnythingWouldBeTried()
    {
        GlobalFontSettings.IsFontFallbackSet.Should().BeFalse(
            "nothing is registered before this test registers something, and PinnedFontResolver "
            + "does not implement IFontFallback itself");

        using (new Installed(new FontFallbackList("Whatever")))
            GlobalFontSettings.IsFontFallbackSet.Should().BeTrue();

        GlobalFontSettings.IsFontFallbackSet.Should().BeFalse("and clearing it answers false again");
    }

    [Fact]
    public void FontFallbackLifecycleSaysItMayBeSetAtAnyTime()
    {
        GlobalFontSettings.FontFallbackLifecycle.Should().Be(SeamLifecycle.SetAnytime,
            "null is a working default and installing, replacing or clearing a fallback is always "
            + "allowed");
    }

    // ----- the defect it exists for -------------------------------------------------------------------

    [Fact]
    public void WithoutAFallbackACharacterTheFaceLacksIsDrawnAsNothing()
    {
        // The starting position, pinned so that the test below is measured against it: four
        // characters, four glyph zero - .notdef - and no complaint from anywhere.
        DrawnText.Glyphs(DrawnText.Page(Arabic, Latin()))
            .Should().OnlyContain(glyph => glyph == 0)
            .And.HaveCount(4);
    }

    [Fact]
    public void WithAFallbackItIsDrawnByTheFaceThatHasIt()
    {
        var arabic = ArabicFont();
        var expected = DrawnText.Glyphs(DrawnText.Page(Arabic, arabic));

        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        DrawnText.Glyphs(DrawnText.Page(Arabic, Latin())).Should().Equal(expected,
            "the same glyphs the Arabic face draws when it is the face that was asked for");
    }

    [Fact]
    public void OnlyThePartTheFaceCannotDrawChangesFace()
    {
        var latin = Latin();
        var hi = DrawnText.Glyphs(DrawnText.Page("Hi", latin));
        var arabic = DrawnText.Glyphs(DrawnText.Page(Arabic, ArabicFont()));

        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        DrawnText.Glyphs(DrawnText.Page("Hi " + Arabic, latin))
            .Should().StartWith(hi)
            .And.EndWith(arabic);
    }

    [Fact]
    public void BothFacesAreEmbeddedAndBothAreSelected()
    {
        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        var content = DrawnText.ContentOf(DrawnText.Page("Hi " + Arabic, Latin()));
        var selections = Regex.Matches(content, @"/F\d+ [\d.]+ Tf");

        selections.Count.Should().BeGreaterThan(1,
            "selecting a font does not move the pen, so the two faces are selected in turn "
            + "between the show operators and the second carries on where the first stopped");
        selections.Select(match => match.Value).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void TheFaceTheCallerAskedForIsSelectedAgainAtTheEnd()
    {
        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        var content = DrawnText.ContentOf(DrawnText.Page("Hi " + Arabic, Latin()));
        var selections = Regex.Matches(content, @"/F\d+ [\d.]+ Tf")
            .Select(match => match.Value)
            .ToList();

        selections.Last().Should().Be(selections.First(),
            "otherwise the graphics state believes one font is selected and another is, and the "
            + "next string drawn comes out in the wrong face");
    }

    // ----- measuring agrees with drawing ----------------------------------------------------------------

    [Fact]
    public void TextIsMeasuredAgainstTheFaceThatWillDrawIt()
    {
        var arabic = ArabicFont();
        var own = DrawnText.MeasuredWidth(Arabic, arabic);

        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        DrawnText.MeasuredWidth(Arabic, Latin()).Should().BeApproximately(own, 1e-9,
            "a width measured against the face that cannot draw the text is a width the drawing "
            + "path disagrees with, and every line break below it lands in the wrong place");
    }

    // ----- and with a shaper on top of it -----------------------------------------------------------

    /// <summary>A real shaper, wired up for one string only. See ShapedFontEmbeddingTests.</summary>
    private sealed class ShapesOnly : ITextShaper, IDisposable
    {
        private readonly string _mine;
        private readonly HarfBuzzTextShaper _shaper = new();

        internal ShapesOnly(string mine) => _mine = mine;

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
            => text.SequenceEqual(_mine.AsSpan())
                ? _shaper.Shape(text, font, direction, script, language)
                : null;

        public void Dispose() => _shaper.Dispose();
    }

    [Fact]
    public void TheShaperIsHandedTheFallbackFaceAndNotTheOneThatCouldNotDrawIt()
    {
        // The whole chain at once: Liberation Sans cannot draw this, the Arabic face is found for
        // it, and the shaper has to be given *that* face's bytes - shaping Arabic against the
        // bytes of a font with no Arabic in it would answer glyph numbers belonging to the wrong
        // file, which is the worst of the three possible failures because the page would look
        // plausible and be nonsense.

        using var shaper = new ShapesOnly(Arabic);
        GlobalFontSettings.TextShaper = shaper;
        try
        {
            var joined = DrawnText.Glyphs(DrawnText.Page(Arabic, new XFont(ArabicFamily, 20)));

            using var _ = new Installed(new Only(Arabic, ArabicFamily));

            DrawnText.Glyphs(DrawnText.Page(Arabic, Latin())).Should().Equal(joined,
                "the same six joined and marked glyphs the Arabic face draws for itself");
            joined.Should().HaveCount(6, "four letters and the two marks GPOS places for them");
        }
        finally
        {
            GlobalFontSettings.TextShaper = null;
        }
    }

    // ----- what it declines to do -----------------------------------------------------------------------

    [Fact]
    public void AFamilyWithoutTheCharacterInItIsPassedOver()
    {
        var expected = DrawnText.Glyphs(DrawnText.Page(Arabic, ArabicFont()));

        // Both of the first two are Liberation Sans, which has no Arabic in it: this suite's
        // resolver answers every family with the same face, on purpose, so that a document asking
        // for a font that is not shipped lays out the same way everywhere. Which also means the
        // other way a candidate can fail - a family that resolves to nothing at all and throws
        // from the XFont constructor - is a branch nothing here can produce.
        using var _ = new Installed(
            new Only(Arabic, "No Such Family Exists", "Times New Roman", ArabicFamily));

        DrawnText.Glyphs(DrawnText.Page(Arabic, Latin())).Should().Equal(expected,
            "a family is only taken if it really has a glyph for the character, and the list is "
            + "walked until one does");
    }

    [Fact]
    public void ACharacterNothingCanDrawIsLeftWhereItWas()
    {
        // Nothing offered covers it, so there is nothing to be gained by cutting the run there -
        // and the .notdef drawn is the same .notdef either way.
        using var _ = new Installed(new Only(Arabic, "No Such Family Exists"));

        DrawnText.GlyphRuns(DrawnText.Page(Arabic, Latin())).Should().HaveCount(1);
    }

    [Fact]
    public void SpacesDoNotCutTheRunTheySitIn()
    {

        // Liberation Sans has a space and the Arabic face has a space, so a space could be claimed
        // by either - and claiming it would break a sentence of Arabic into one run per word,
        // losing the shaping across every one of the boundaries.
        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        DrawnText.GlyphRuns(DrawnText.Page(Arabic + " " + Arabic, Latin()))
            .Should().HaveCount(1, "one face, one direction, one script, one run");
    }

    [Fact]
    public void AJoiningControlDoesNotCutTheRunEither()
    {

        // The control is there to say how the letters on either side of it join, and it is read by
        // the face those letters are drawn from. Giving it a face of its own would put the
        // instruction in one run and the letters it is about in another, which is the one
        // arrangement that certainly cannot work.
        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        DrawnText.GlyphRuns(DrawnText.Page(Arabic + Joiner + Arabic, Latin()))
            .Should().HaveCount(1);
    }

    [Fact]
    public void ASurrogatePairIsNotSplitBetweenTwoFaces()
    {

        // The two halves of a supplementary character are not characters. Asked about separately -
        // which is what a loop over UTF-16 code units does - each is a lone surrogate, and a face
        // boundary falling between them would draw one character out of two files. What is pinned
        // here is that the pair stays whole on the way past: it is asked about once, as the one
        // code point it spells, and it gets one glyph whether or not anything can draw it.
        using var _ = new Installed(new Only(Arabic, ArabicFamily));

        // MATHEMATICAL BOLD CAPITAL A, U+1D400, between two runs of Arabic. It is bidi class L
        // between two right-to-left runs, so it is a run of its own however it is drawn - and one
        // character in a run of its own is one glyph. It used to be two, one .notdef per surrogate,
        // which is the bug this now pins the absence of.
        var runs = DrawnText.GlyphRuns(DrawnText.Page(Arabic + "\U0001D400" + Arabic, Latin()));

        runs.Should().ContainSingle(run => run.Length == 1,
            "the pair is one character and gets one glyph, not one for each half")
            .Which.Should().Equal(new[] { 0 },
                "Liberation Sans has no format 12 subtable, so it cannot draw it - once");

        runs.Where(run => run.Length != 1).Should().OnlyContain(run => run.Length == Arabic.Length,
            "the Arabic either side is untouched");
    }

    [Fact]
    public void AFallbackWithNothingToSayAboutTheTextChangesNothingAboutIt()
    {
        // The guarantee that makes this safe to leave switched on: a page that needed no fallback
        // is byte for byte the page it was before fallback existed. Registered here with an
        // opinion about the space character alone - which is one of the three kinds of character
        // the library never asks about, so the opinion is never even collected.
        var latin = Latin();
        var before = DrawnText.ContentOf(DrawnText.Page("Hi " + Arabic, latin));

        using (new Installed(new Only(" ", ArabicFamily)))
            DrawnText.ContentOf(DrawnText.Page("Hi " + Arabic, latin)).Should().Be(before);

        DrawnText.ContentOf(DrawnText.Page("Hi " + Arabic, latin)).Should().Be(before,
            "and taking it away again puts things back");
    }
}
