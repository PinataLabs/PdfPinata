using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   What happens to a string on its way to the page now that it is cut into runs first: one
///   direction and one script each, in the order they are drawn.
/// </summary>
/// <remarks>
///   <para>
///     Nothing here registers a shaper. That is deliberate - reordering is not shaping, and a
///     consumer who takes no HarfBuzz dependency should still get right-to-left text in the right
///     order. The oldest complaint against this library is that <c>"&#x0633;&#x0644;&#x0627;&#x0645;"</c>
///     draws as <c>"&#x0645; &#x0627; &#x0644; &#x0633;"</c>, and that is a reordering failure
///     rather than a shaping one. It is fixed here, in the core, for nothing.
///   </para>
///   <para>
///     The Arabic face is used rather than Liberation Sans because these tests read glyph
///     identifiers back and compare them, and a face with no Arabic in it answers <c>.notdef</c>
///     for every Arabic character alike - which would make a wrong order indistinguishable from a
///     right one.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class ItemizedTextTests
{
    // Served by PinnedFontResolver itself. See the note on ArabicFamilyName: a family registered on
    // first use means whatever the first caller in the assembly made it mean.
    const string ArabicFamily = PinnedFontResolver.ArabicFamilyName;

    // "salam", four letters, none of them carrying a mark - so with no shaper registered it is
    // four characters and four glyphs, and the only thing that can differ is their order.
    // Escapes rather than literals, so that a source file mixing right-to-left text with
    // left-to-right code cannot be misread.
    const string Salam = "\u0633\u0644\u0627\u0645";

    // Greek, which is left to right like Latin and a different script from it.
    const string Greek = "\u03B1\u03B2";

    // U+200D ZERO WIDTH JOINER, which asks the letters on either side of it to join.
    const string Joiner = "\u200D";

    static XFont Latin() => new XFont("Arial", 20);

    static XFont Arabic() => new XFont(ArabicFamily, 20);

    static int[] Glyphs(string text, XFont font) => DrawnText.Glyphs(DrawnText.Page(text, font));

    static IReadOnlyList<int[]> Runs(string text, XFont font)
        => DrawnText.GlyphRuns(DrawnText.Page(text, font));

    // ----- the complaint this whole gap exists for -------------------------------------------------

    [Fact]
    public void RightToLeftTextIsDrawnLastLetterFirst()
    {
        var font = Arabic();

        // Each letter on its own, to find out which glyph it is. One character is one run
        // whichever way round it is read, so this says nothing about order and only about identity.
        var letters = Salam.Select(letter => Glyphs(letter.ToString(), font).Single()).ToArray();

        Glyphs(Salam, font).Should().Equal(Enumerable.Reverse(letters),
            "the glyphs go on the page left to right, and the leftmost letter of a right-to-left "
            + "word is the last one written");
    }

    [Fact]
    public void LatinBesideArabicIsDrawnOnEachSideOfTheBoundary()
    {
        var font = Arabic();
        var a = Glyphs("A", font).Single();
        var letters = Salam.Select(letter => Glyphs(letter.ToString(), font).Single()).ToArray();

        // A left-to-right paragraph, because "A" is the first strong character in it. So the Latin
        // stays where it was written and the Arabic beside it turns round.
        Glyphs("A" + Salam, font).Should().Equal(new[] { a }.Concat(Enumerable.Reverse(letters)),
            "one run each, and the right-to-left one reversed inside itself rather than the "
            + "whole string reversed");
    }

    [Fact]
    public void WhichWayTheParagraphRunsDecidesWhichRunIsDrawnFirst()
    {
        var font = Arabic();
        var a = Glyphs("A", font).Single();

        // Now the Arabic is the first strong character, so the paragraph is right to left and the
        // Latin sits to the left of it - drawn first, although it was written last.
        var drawn = Glyphs(Salam + "A", font);

        drawn[0].Should().Be(a, "the Latin is leftmost in a right-to-left paragraph");
        drawn.Should().HaveCount(5);
    }

    // ----- what it costs everything else ------------------------------------------------------------

    [Fact]
    public void OrdinaryLatinTextIsStillOneRunDrawnInOneGo()
    {
        // The case that must not have been made slower or longer: no reordering, no script change,
        // one Tj, exactly the bytes this library has always written.
        var content = DrawnText.ContentOf(DrawnText.Page("Hello, world!", Latin()));

        content.Should().Contain(" Tj").And.NotContain("TJ");
        Runs("Hello, world!", Latin()).Should().HaveCount(1);
    }

    [Fact]
    public void DigitsAndPunctuationDoNotStartARunOfTheirOwn()
    {
        // Script Common and bidirectional class European Number, neither of which is Latin - and
        // both of which have to be swept into the run beside them or every price in every document
        // would be shaped apart from the words around it.
        Runs("Item 42 (of 99).", Latin()).Should().HaveCount(1);
    }

    [Fact]
    public void AChangeOfScriptIsARunBoundaryEvenWithoutAChangeOfDirection()
    {
        // Both left to right, so nothing is reordered - but a face applies one script's rules at a
        // time, and asking it to apply Greek's to Latin characters is how a shaper is made to
        // answer nonsense.
        Runs("Hi " + Greek, Latin()).Should().HaveCount(2);
    }

    [Fact]
    public void APrivateUseCharacterIsARunOfItsOwn()
    {
        // Nobody knows anything about a private-use character - it is script Unknown - so it gets
        // a run to itself and the text around it keeps its own script. Worth pinning because it is
        // invisible in a source file and surprising in a diff: an icon glyph dropped into a
        // sentence quietly stops the words either side of it from being shaped together.
        Runs("a\uE000b", Latin()).Should().HaveCount(3);
    }

    [Fact]
    public void AJoiningControlIsInTheRunAndNotOnThePage()
    {
        var font = Arabic();

        // Both halves of what a joining control has to be. It is inside the run, because that is
        // the only way a shaper can read it and it exists to be read - and nothing is drawn for it,
        // because it is zero width and the glyph this face maps it to is not one to put on a page.
        // Before, it was neither: the run stopped dead at it and started again after it.
        var joined = Glyphs(Salam.Substring(0, 2) + Joiner + Salam.Substring(2), font);

        Runs(Salam.Substring(0, 2) + Joiner + Salam.Substring(2), font).Should().HaveCount(1);
        joined.Should().Equal(Glyphs(Salam, font),
            "with no shaper registered the joiner cannot change which glyphs are chosen, so the "
            + "only thing it could do is add one");
    }

    // ----- measuring and drawing still agree ---------------------------------------------------------

    [Fact]
    public void AStringOfSeveralRunsIsAsWideAsItsRunsAddUpTo()
    {
        var font = Arabic();

        DrawnText.MeasuredWidth("A" + Salam, font).Should().BeApproximately(
            DrawnText.MeasuredWidth("A", font) + DrawnText.MeasuredWidth(Salam, font), 1e-9,
            "the same glyphs are drawn whichever order they are drawn in");
    }

    [Fact]
    public void TurningTextRoundDoesNotChangeHowWideItIs()
    {
        var font = Arabic();

        DrawnText.MeasuredWidth(Salam, font).Should().BeApproximately(
            Salam.Sum(letter => DrawnText.MeasuredWidth(letter.ToString(), font)), 1e-9);
    }

    // ----- what the runs are handed to the shaper as ---------------------------------------------------

    /// <summary>
    ///   A shaper that shapes nothing and writes down what it was asked. Declining every run means
    ///   it cannot disturb anything drawing beside it, which is what makes it safe to install while
    ///   the rest of the suite runs.
    /// </summary>
    sealed class Recorder : ITextShaper
    {
        readonly List<(string Text, XTextDirection Direction, string Script)> _runs = new();

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
        {
            lock (_runs)
                _runs.Add((text.ToString(), direction, script));

            return null;
        }

        /// <summary>
        ///   What it was asked about the given runs, once each.
        /// </summary>
        /// <remarks>
        ///   Two reasons this is not simply everything it saw. Measuring and drawing both ask the
        ///   seam, so every run turns up more than once. And the seam is one setting for the whole
        ///   application domain, so a recorder installed here sees every string the rest of the
        ///   suite draws while it is installed - which is why the runs wanted are named exactly
        ///   rather than matched loosely. A test asking for the runs of "A" got somebody else's
        ///   "A" and a third entry it could not account for.
        /// </remarks>
        internal IReadOnlyList<(string Text, XTextDirection Direction, string Script)> Of(
            params string[] texts)
        {
            lock (_runs)
                return _runs.Where(run => texts.Contains(run.Text, StringComparer.Ordinal))
                    .Distinct()
                    .ToList();
        }
    }

    [Fact]
    public void EachRunIsHandedOverWithItsOwnScriptAndDirection()
    {
        var font = Arabic();
        var recorder = new Recorder();

        // A distinctive word rather than a letter, so that the run wanted back is one no other
        // test could be drawing at the same moment.
        const string latin = "recorded";

        GlobalFontSettings.TextShaper = recorder;
        try
        {
            DrawnText.Page(latin + Salam, font);
        }
        finally
        {
            GlobalFontSettings.TextShaper = null;
        }

        recorder.Of(latin, Salam).Should().BeEquivalentTo(new[]
        {
            (latin, XTextDirection.LeftToRight, "latn"),
            (Salam, XTextDirection.RightToLeft, "arab"),
        }, "a shaper is told what it is shaping, because a face applies one script's rules at a "
           + "time and cannot work out from the characters alone which way the run reads");
    }
}
