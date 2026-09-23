using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Fonts;
using PdfPinata.Text;
using Xunit;

namespace PdfPinata.Test.Text;

/// <summary>
///   Cutting mixed text into the runs a shaper can be handed: one direction and one script each,
///   in the order they are drawn.
/// </summary>
/// <remarks>
///   The bidirectional algorithm has its own conformance suite and is tested by it; what is left
///   here is script itemisation, which has no such suite, and the join between the two.
///   <para>
///   ScriptItemizer is internal - asked of a paragraph rather than of one bidirectional run it
///   gives a plausible answer that disagrees with the bidirectional algorithm about where a run
///   ends, so TextItemizer.Itemize is the only supported way in - and this repository carries no
///   InternalsVisibleTo, so it is reached by reflection the way CharacterScanningTests reaches
///   CharacterScanning. The script-only tests below keep asking it directly rather than going
///   through TextItemizer: several of their inputs are mixed-direction too, so routing them through
///   TextItemizer would let a script-itemisation regression hide behind correct bidi behaviour.
///   </para>
/// </remarks>
public class ItemizationTests
{
    // "arabi", four Arabic letters. Escapes rather than literals throughout, so that a source file
    // mixing right-to-left text with left-to-right code cannot be misread.
    private const string Arabic = "\u0639\u0631\u0628\u064A";
    private const string Hebrew = "\u05D0\u05D1";

    // Two Arabic letters with a ZERO WIDTH JOINER between them, and a ZERO WIDTH NO-BREAK SPACE.
    // Both are removed by rule X9 and neither is visible in a source file, which is why they are
    // written out here rather than inline.
    private const string Joined = "\u0639\u200D\u0631";
    private const string ByteOrderMark = "\uFEFF";

    // ----- script itemisation ----------------------------------------------------------------------

    [Fact]
    public void TextOfOneScriptIsOneRun()
    {
        var runs = ItemizeScripts("Hello");

        runs.Should().HaveCount(1);
        runs[0].Script.Should().Be(UnicodeScript.Latin);
        runs[0].Start.Should().Be(0);
        runs[0].Length.Should().Be(5);
    }

    [Fact]
    public void AChangeOfScriptStartsANewRun()
    {
        var runs = ItemizeScripts("Hi" + Arabic);

        runs.Select(run => run.Script).Should().Equal(UnicodeScript.Latin, UnicodeScript.Arabic);
        runs[0].Length.Should().Be(2);
        runs[1].Length.Should().Be(4);
    }

    [Fact]
    public void PunctuationAndSpacesGoWithWhateverTheyAreNextTo()
    {
        // A space and a full stop are script Common and say nothing about which script they are
        // in. Sweeping them into the preceding run is the whole of UAX #24's difficulty.
        var runs = ItemizeScripts("Hi. " + Arabic);

        runs.Should().HaveCount(2);
        runs[0].Length.Should().Be(4, "the full stop and the space belong to the Latin run");
        runs[1].Script.Should().Be(UnicodeScript.Arabic);
    }

    [Fact]
    public void LeadingPunctuationBelongsToTheScriptThatFollowsIt()
    {
        // Nothing has said what script the text is in yet, so the opening bracket waits for the
        // first character that does and is taken into that run rather than left in one of its own.
        var runs = ItemizeScripts("(Hello)");

        runs.Should().HaveCount(1);
        runs[0].Script.Should().Be(UnicodeScript.Latin);
        runs[0].Length.Should().Be(7);
    }

    [Fact]
    public void ACombiningMarkTakesTheScriptOfTheLetterItSitsOn()
    {
        // A combining acute is script Inherited, which is Common's counterpart for marks.
        var runs = ItemizeScripts("\u0065\u0301");

        runs.Should().HaveCount(1);
        runs[0].Script.Should().Be(UnicodeScript.Latin);
    }

    [Fact]
    public void TextWithNoLettersInItIsOneCommonRun()
    {
        var runs = ItemizeScripts("12 + 34!");

        runs.Should().HaveCount(1);
        runs[0].Script.Should().Be(UnicodeScript.Common);
        runs[0].ScriptCode.Should().Be("zyyy");
    }

    [Fact]
    public void NothingIsNoRuns()
    {
        ItemizeScripts(string.Empty).Should().BeEmpty();
    }

    [Fact]
    public void AnAstralCharacterIsOneCharacterAndNotTwo()
    {
        // U+1D400 MATHEMATICAL BOLD CAPITAL A is Common, and its two UTF-16 units must not be
        // read as two characters of possibly different scripts.
        var runs = ItemizeScripts("A\U0001D400B");

        runs.Should().HaveCount(1);
        runs[0].Script.Should().Be(UnicodeScript.Latin);
        runs[0].Length.Should().Be(4, "two Latin letters and one astral character of two units");
    }

    // ----- the window the itemiser is really asked about ----------------------------------------

    [Fact]
    public void TheWholeStringIsJustTheWidestWindow()
    {
        // The whole-string form is the range form with nothing left out, so generalising the loop
        // cannot have moved a boundary the eight tests above already pin.
        const string mixed = "Hi. " + Arabic + " 12 " + Hebrew + "!";

        ItemizeScripts(mixed, 0, mixed.Length).Should().Equal(ItemizeScripts(mixed));
    }

    [Fact]
    public void AWindowIsItemisedOnItsOwnAndIndexedIntoTheWholeString()
    {
        // What TextItemizer asks once per bidirectional run. The Hebrew is outside the window and
        // so cannot cut it, and the indices that come back are into the string rather than into the
        // window - a caller cutting the text at them has only the one set of numbers to get right.
        const string both = Hebrew + Arabic;

        var runs = ItemizeScripts(both, Hebrew.Length, Arabic.Length);

        runs.Should().HaveCount(1, "there is only one script inside the window");
        runs[0].Script.Should().Be(UnicodeScript.Arabic);
        runs[0].Start.Should().Be(2);
        runs[0].Length.Should().Be(4);
    }

    [Fact]
    public void AWindowSweepsPunctuationIntoItselfAndNotIntoWhatSurroundsIt()
    {
        // The space is Common and goes with whatever it is beside - and inside this window the only
        // thing it can be beside is the Arabic, which is the whole point of asking per run rather
        // than per paragraph. Asked of the whole string it would have gone with the Latin instead.
        const string mixed = "one " + Arabic;

        var runs = ItemizeScripts(mixed, 3, 1 + Arabic.Length);

        runs.Should().HaveCount(1);
        runs[0].Script.Should().Be(UnicodeScript.Arabic);
        runs[0].Start.Should().Be(3, "the space in front of the letters is inside the window");
        runs[0].Length.Should().Be(5);
    }

    // ----- the join with the bidirectional algorithm --------------------------------------------------

    [Fact]
    public void ARunIsBothOneDirectionAndOneScript()
    {
        var runs = TextItemizer.Itemize("Hi " + Arabic);

        runs.Should().HaveCount(2);
        runs[0].Direction.Should().Be(XTextDirection.LeftToRight);
        runs[0].ScriptCode.Should().Be("latn");
        runs[1].Direction.Should().Be(XTextDirection.RightToLeft);
        runs[1].ScriptCode.Should().Be("arab");
    }

    [Fact]
    public void TheRunsComeBackInTheOrderTheyAreDrawn()
    {
        // A right-to-left paragraph with Latin inside it: the Latin sits to the left of the Arabic
        // on the page although it was written after it, and a renderer drawing the runs in the
        // order they are handed over gets that right without knowing anything about bidi.
        var runs = TextItemizer.Itemize(Arabic + " Hi", BidiParagraphDirection.RightToLeft);

        runs.Should().HaveCount(2);
        runs[0].ScriptCode.Should().Be("latn", "the Latin is drawn leftmost");
        runs[1].ScriptCode.Should().Be("arab");
        runs[0].Start.Should().BeGreaterThan(runs[1].Start,
            "even though it was written last");
    }

    [Fact]
    public void TwoRightToLeftScriptsSideBySideAreTwoRuns()
    {
        // Same direction, different scripts, so the bidirectional algorithm sees one run and
        // itemisation has to cut it - which is the case that says the two are really composed
        // rather than one merely following the other.
        var runs = TextItemizer.Itemize(Hebrew + Arabic, BidiParagraphDirection.RightToLeft);

        runs.Should().HaveCount(2);
        // BeEquivalentTo rather than Equal, because what order they come back in is the next
        // test's question and this one is only about there being two of them. That was once the
        // only assertion here, and it is why the order being wrong went unnoticed.
        runs.Select(run => run.ScriptCode).Should().BeEquivalentTo("hebr", "arab");
        runs.Should().OnlyContain(run => run.Direction == XTextDirection.RightToLeft);
    }

    [Fact]
    public void TwoRightToLeftScriptsComeBackInTheOrderTheyAreDrawn()
    {
        // Not just cut into two runs - cut and then turned round. A right-to-left run whose script
        // changes half way along is still one direction, so the piece written first is the
        // rightmost and has to be handed over last. Nothing said so until a line of Hebrew
        // followed by Arabic came out with the Hebrew on the left.
        var runs = TextItemizer.Itemize(Hebrew + Arabic, BidiParagraphDirection.RightToLeft);

        runs.Select(run => run.ScriptCode).Should().Equal("arab", "hebr");
    }

    [Fact]
    public void ASpaceInsideARightToLeftRunDoesNotCutIt()
    {
        // The space is script Common and takes the script of what it is beside - but "beside" is
        // decided inside the bidirectional run, not across the paragraph. Asked of the paragraph,
        // this space goes with the Latin that precedes it in the text; and the algorithm then puts
        // it in the middle of the Arabic. Cutting there would have been a boundary that is not one,
        // and it would have left the real boundary uncut.
        var runs = TextItemizer.Itemize("one " + Arabic, BidiParagraphDirection.RightToLeft);

        runs.Should().HaveCount(2);
        runs[0].ScriptCode.Should().Be("arab", "the space belongs to the run it lands in");
        runs[0].Length.Should().Be(5, "which is four letters and the space in front of them");
        runs[1].ScriptCode.Should().Be("latn");
    }

    [Fact]
    public void AJoiningControlStaysInsideTheRunItSitsIn()
    {
        // U+200D ZERO WIDTH JOINER is bidirectional class BN, so rule X9 takes it out before
        // anything is resolved and it appears in no visual order. That is right for ordering and
        // was wrong for shaping: the run stopped at it and started again after it, so the shaper
        // was handed one Arabic letter, then another, and never the word. It could not obey the
        // joiner - it could not even see it - and drew the isolated form of both letters.
        var runs = TextItemizer.Itemize(Joined, BidiParagraphDirection.RightToLeft);

        runs.Should().HaveCount(1, "a joiner is inside a run, not between two");
        runs[0].Start.Should().Be(0);
        runs[0].Length.Should().Be(3, "two letters and the joiner between them");
    }

    [Fact]
    public void AJoiningControlIsStillNotDrawn()
    {
        // Kept in the run and kept out of the order: the run reaches over it, and nothing puts a
        // glyph on the page for it. Both halves matter - a joiner that appeared in the visual order
        // would be a character the renderer draws, and it is zero width by definition.
        var result = BidiAlgorithm.Resolve(Joined, BidiParagraphDirection.RightToLeft);

        result.Removed[1].Should().BeTrue();
        result.VisualOrder.Should().Equal(2, 0);
    }

    [Fact]
    public void AnyOtherRemovedCharacterStillEndsTheRun()
    {
        // U+FEFF is class BN and removed as well, and unlike a joiner it says nothing a shaper
        // could act on. Reaching a run over it would put a character into the span that the
        // unshaped path then looks a glyph up for and draws a box where nothing belongs. Only the
        // two joining controls are worth the exception.
        var runs = TextItemizer.Itemize("ab" + ByteOrderMark + "cd");

        runs.Should().HaveCount(2);
        runs.Select(run => run.Length).Should().Equal(2, 2);
    }

    [Fact]
    public void EveryCharacterEndsUpInExactlyOneRun()
    {
        const string mixed = "Hi " + Arabic + " 12 " + Hebrew + "!";

        var runs = TextItemizer.Itemize(mixed);

        runs.Sum(run => run.Length).Should().Be(mixed.Length);
        runs.SelectMany(run => Enumerable.Range(run.Start, run.Length))
            .OrderBy(index => index)
            .Should().Equal(Enumerable.Range(0, mixed.Length),
                "nothing is dropped and nothing is covered twice");
    }

    [Fact]
    public void ALeftToRightParagraphOfPlainTextIsOneRun()
    {
        // The overwhelmingly common case, and the one that must not have been made expensive:
        // no reordering, no script change, one run.
        var runs = TextItemizer.Itemize("The quick brown fox.");

        runs.Should().HaveCount(1);
        runs[0].Start.Should().Be(0);
        runs[0].Length.Should().Be(20);
        runs[0].Direction.Should().Be(XTextDirection.LeftToRight);
    }

    [Fact]
    public void NothingIsNoTextRuns()
    {
        TextItemizer.Itemize(string.Empty).Should().BeEmpty();
    }

    // ----- what the bidirectional algorithm answers for a caller who only wants the direction -----

    [Fact]
    public void AParagraphReadsItsOwnDirectionOffItsFirstStrongCharacter()
    {
        BidiAlgorithm.Resolve("Hello " + Arabic).IsRightToLeft.Should().BeFalse();
        BidiAlgorithm.Resolve(Arabic + " Hello").IsRightToLeft.Should().BeTrue();
        BidiAlgorithm.Resolve("123 " + Arabic).IsRightToLeft.Should().BeTrue(
            "a number is not a strong character, so the Arabic is the first thing that counts");
    }

    [Fact]
    public void ACallerCanOverrideWhatTheTextSaysAboutItself()
    {
        BidiAlgorithm.Resolve(Arabic, BidiParagraphDirection.LeftToRight)
            .IsRightToLeft.Should().BeFalse();
        BidiAlgorithm.Resolve("Hello", BidiParagraphDirection.RightToLeft)
            .IsRightToLeft.Should().BeTrue();
    }

    [Fact]
    public void RightToLeftTextComesBackReversed()
    {
        // The documented complaint this whole gap exists for: "salam" drawn as "m a l s".
        var result = BidiAlgorithm.Resolve(Hebrew);

        result.VisualOrder.Should().Equal(1, 0);
    }

    // ----- reaching the internal itemiser --------------------------------------------------------

    private static readonly Type ItemizerType = typeof(TextItemizer).Assembly
        .GetType("PdfPinata.Text.ScriptItemizer", throwOnError: true);

    /// <summary>
    ///   One ScriptRun read off the internal struct, so that an assertion can be written about it
    ///   without naming a type this assembly cannot see.
    /// </summary>
    private sealed record ScriptRunView(int Start, int Length, UnicodeScript Script, string ScriptCode);

    private static IReadOnlyList<ScriptRunView> ItemizeScripts(string text)
        => Read(Invoke([typeof(string)], text));

    private static IReadOnlyList<ScriptRunView> ItemizeScripts(string text, int start, int length)
        => Read(Invoke([typeof(string), typeof(int), typeof(int)], text, start, length));

    private static object Invoke(Type[] signature, params object[] args)
    {
        var method = ItemizerType.GetMethod(
            "Itemize", BindingFlags.Public | BindingFlags.Static, binder: null,
            types: signature, modifiers: null);

        // ReSharper disable once PossibleNullReferenceException
        return method.Invoke(null, args);
    }

    private static IReadOnlyList<ScriptRunView> Read(object runs)
    {
        var read = new List<ScriptRunView>();
        foreach (var run in (IEnumerable)runs)
        {
            var type = run.GetType();
            // ReSharper disable PossibleNullReferenceException
            read.Add(new ScriptRunView(
                (int)type.GetProperty("Start").GetValue(run),
                (int)type.GetProperty("Length").GetValue(run),
                (UnicodeScript)type.GetProperty("Script").GetValue(run),
                (string)type.GetProperty("ScriptCode").GetValue(run)));
            // ReSharper restore PossibleNullReferenceException
        }

        return read;
    }
}
