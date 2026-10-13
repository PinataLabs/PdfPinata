using System;
using AwesomeAssertions;
using PdfPinata.Text;
using TUnit.Core;

namespace PdfPinata.Test.Text;

/// <summary>
///   What <see cref="LineBreakAlgorithm"/> answers beyond what the conformance suite asks of it:
///   the shape of the answer, surrogate pairs, the question asked of two pieces of text, and the
///   tailorings for the scripts of South East Asia and for Korean.
/// </summary>
public class LineBreakAlgorithmTests
{
    [Test]
    public void EveryPositionOfATextIsAnswered()
    {
        var answer = LineBreakAlgorithm.BreakOpportunities("ab cd");

        answer.Should().Equal(
            LineBreakOpportunity.None,      // LB2: never at the start
            LineBreakOpportunity.None,      // LB28: a × b
            LineBreakOpportunity.None,      // LB7: × SP
            LineBreakOpportunity.Allowed,   // LB18: SP ÷
            LineBreakOpportunity.None,      // LB28
            LineBreakOpportunity.Mandatory); // LB3: always at the end
    }

    [Test]
    public void AHardLineBreakIsMandatory()
    {
        var answer = LineBreakAlgorithm.BreakOpportunities("a\r\nb");

        answer[2].Should().Be(LineBreakOpportunity.None, "CR × LF");
        answer[3].Should().Be(LineBreakOpportunity.Mandatory, "a line ends after CR LF");
    }

    [Test]
    public void APositionInsideASurrogatePairIsNeverABreak()
    {
        // U+20000 and U+20001, two ideographs from plane 2: a line may be broken between them, and
        // never between the halves of either.
        var text = char.ConvertFromUtf32(0x20000) + char.ConvertFromUtf32(0x20001);

        var answer = LineBreakAlgorithm.BreakOpportunities(text);

        answer[1].Should().Be(LineBreakOpportunity.None);
        answer[2].Should().Be(LineBreakOpportunity.Allowed);
        answer[3].Should().Be(LineBreakOpportunity.None);
        LineBreakAlgorithm.BreakBefore(text, 1).Should().Be(LineBreakOpportunity.None);
        LineBreakAlgorithm.IsBreakAllowed(text, 2).Should().BeTrue();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(4)]
    public void APositionOutsideTheTextIsRefused(int index)
    {
        var act = () => LineBreakAlgorithm.BreakBefore("abc", index);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments("bbbb", "cccc", false, "LB28: two letters")]
    [Arguments("bbbb-", "cccc", true, "after a hyphen")]
    [Arguments("bbbb\u2014", "cccc", true, "after an em dash")]
    [Arguments("bbbb\u2014", "\u00A0cccc", false, "LB12a: an em dash before a no-break space")]
    [Arguments("bbbb\u2013", "\u00A0cccc", true, "LB12a: an en dash, HH, before a no-break space")]
    [Arguments("bbbb\u2014", "\u300Dcccc", false, "LB13: a dash before a closing bracket")]
    [Arguments("bbbb-", ")", false, "LB13: a hyphen before a closing parenthesis")]
    [Arguments("\u6F22", "\u3005", false, "LB21: an iteration mark is a nonstarter")]
    [Arguments("\u6F22", "\u3041", false, "LB1 and LB21: a small kana is CJ, and CJ is NS")]
    [Arguments("\u6F22", "\u6F22", true, "LB31: between two ideographs")]
    [Arguments("abc", "\u0301", false, "LB9: a combining mark stays with its letter")]
    [Arguments("1", "-2", false, "LB25: no break before a hyphen")]
    public void TwoPiecesOfTextAreAskedAboutWhereTheyMeet(string before, string after, bool allowed, string why)
    {
        LineBreakAlgorithm.IsBreakAllowedBetween(before, after).Should().Be(allowed, why);
        LineBreakAlgorithm.IsBreakAllowed(before + after, before.Length).Should().Be(allowed, why);
    }

    [Test]
    public void TheTwoPiecesAreReadAsOneTextAcrossTheSeam()
    {
        // LB25 reads back to the start of the number: "1,2" and "3" meet inside it, and the same
        // number cut one place further on still joins.
        LineBreakAlgorithm.IsBreakAllowedBetween("1,2", "3").Should().BeFalse();
        LineBreakAlgorithm.IsBreakAllowedBetween("1,", "23").Should().BeFalse();

        // LB14 reads back over spaces to an opening bracket.
        LineBreakAlgorithm.IsBreakAllowedBetween("(  ", "a").Should().BeFalse();
    }

    [Test]
    public void LongPiecesAreAnsweredTheSameAsShortOnes()
    {
        // Past the length decided on the stack, the same answer from a rented buffer.
        var word = new string('a', 300);

        LineBreakAlgorithm.IsBreakAllowedBetween(word, word).Should().BeFalse();
        LineBreakAlgorithm.IsBreakAllowedBetween(word + " ", word).Should().BeTrue();
    }

    [Test]
    public void AnEmptyPieceIsTheStartOrTheEndOfTheText()
    {
        LineBreakAlgorithm.IsBreakAllowedBetween("", "a").Should().BeFalse("LB2: never at the start");
        LineBreakAlgorithm.IsBreakAllowedBetween("a", "").Should().BeTrue("LB3: always at the end");
    }

    [Test]
    public void TwoEmptyPiecesAreTheStartOfTheText()
    {
        // LB2 comes before LB3: an empty text has no break in it.
        LineBreakAlgorithm.IsBreakAllowedBetween("", "").Should().BeFalse();
        LineBreakAlgorithm.BreakBefore("", 0).Should().Be(LineBreakOpportunity.None);
    }

    // ----- Korean ------------------------------------------------------------------------------------

    /// <summary>The Hangul syllables ga and na.</summary>
    private const string Ga = "\uAC00";
    private const string Na = "\uB098";

    [Test]
    public void ByDefaultALineMayBeBrokenBetweenTwoHangulSyllables()
    {
        // LB26 and LB27 keep a syllable block together and say nothing against a break after it.
        LineBreakAlgorithm.IsBreakAllowedBetween(Ga, Na).Should().BeTrue();
    }

    [Test]
    public void TailoredTwoHangulSyllablesAreNotBrokenBetween()
    {
        LineBreakAlgorithm.IsBreakAllowedBetween(Ga, Na, LineBreakTailoring.HangulAsAlphabetic)
            .Should().BeFalse("LB28: a syllable is a letter, and a word is broken only at a space");
        LineBreakAlgorithm.IsBreakAllowedBetween(Ga + " ", Na, LineBreakTailoring.HangulAsAlphabetic)
            .Should().BeTrue("LB18: after the space between two words");
    }

    // ----- South East Asian scripts ------------------------------------------------------------------

    /// <summary>Thai ko kai, kho khai and the vowel sign sara i, a mark.</summary>
    private const string KoKai = "\u0E01";
    private const string KhoKhai = "\u0E02";
    private const string SaraI = "\u0E34";

    [Test]
    public void ByDefaultTwoThaiLettersAreNotBrokenBetween()
    {
        // LB1 resolves SA to AL where there is no dictionary, and LB28 joins two ALs: the default
        // algorithm keeps a Thai sentence together as one word.
        LineBreakAlgorithm.IsBreakAllowedBetween(KoKai, KhoKhai).Should().BeFalse();
    }

    [Test]
    public void TailoredTwoThaiLettersMayBeBrokenBetweenAsTwoIdeographsMay()
    {
        LineBreakAlgorithm.IsBreakAllowedBetween(KoKai, KhoKhai, LineBreakTailoring.SouthEastAsianAsIdeographs)
            .Should().BeTrue();
    }

    [Test]
    public void TailoredAThaiMarkStillStaysWithItsLetter()
    {
        LineBreakAlgorithm.IsBreakAllowedBetween(KoKai, SaraI, LineBreakTailoring.SouthEastAsianAsIdeographs)
            .Should().BeFalse("LB1 makes an SA mark CM, and LB9 keeps it with its letter");
        LineBreakAlgorithm.IsBreakAllowedBetween(KoKai + SaraI, KhoKhai, LineBreakTailoring.SouthEastAsianAsIdeographs)
            .Should().BeTrue("the letter and its mark are an ID, and a line may be broken after one");
    }

    [Test]
    public void TailoredAThaiLetterIsStillNotSetBeforeAClosingBracket()
    {
        LineBreakAlgorithm.IsBreakAllowedBetween(KoKai, ")", LineBreakTailoring.SouthEastAsianAsIdeographs)
            .Should().BeFalse("LB13: × CP");
    }
}
