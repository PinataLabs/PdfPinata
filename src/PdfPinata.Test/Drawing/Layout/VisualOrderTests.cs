using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Drawing.Layout;
using PdfPinata.Text;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   The rule both layout engines learned separately: a unit is ordered by the leftmost position
///   any of its characters ends up at. Tested here as the arithmetic it is - spans and a resolved
///   <see cref="BidiResult"/> in, a permutation out - with no font, no page and no engine involved.
///   <see cref="BidirectionalLayoutTests"/> and <c>BidirectionalParagraphTests</c> pin the two
///   engines that call this and must keep passing unchanged.
/// </summary>
public class VisualOrderTests
{
    // Three Hebrew words of two letters each, and an English word - escapes rather than literals,
    // so that a source file mixing right-to-left text with left-to-right code cannot be misread.
    const string First = "אב";
    const string Second = "גד";
    const string Third = "הו";

    static (int Start, int Length)[] WordSpans(params string[] words)
    {
        var spans = new (int Start, int Length)[words.Length];
        int at = 0;
        for (int idx = 0; idx < words.Length; idx++)
        {
            spans[idx] = (at, words[idx].Length);
            at += words[idx].Length + 1; // one space between words, as both callers join lines
        }

        return spans;
    }

    static string Joined(params string[] words) => string.Join(" ", words);

    [Fact]
    public void ARightToLeftLineOfTwoWordsReversesTheWordsToo()
    {
        var words = new[] { First, Second };
        var resolved = BidiAlgorithm.Resolve(Joined(words));

        var order = VisualOrder.Of(resolved, WordSpans(words));

        order.Should().Equal(new[] { 1, 0 },
            "the word written first ends up rightmost, so it is placed last");
    }

    [Fact]
    public void ALeftToRightPhraseInsideARightToLeftLineKeepsItsOwnWordOrder()
    {
        // The case a naive "reverse the line" implementation gets wrong: "one" and "two" have to
        // stay in their own order even though the sentence around them is right to left.
        var words = new[] { First, "one", "two", Second };
        var resolved = BidiAlgorithm.Resolve(Joined(words), BidiParagraphDirection.RightToLeft);

        var order = VisualOrder.Of(resolved, WordSpans(words));

        order.Should().Equal(new[] { 3, 1, 2, 0 },
            "Second is placed first (rightmost), then one and two in their own order, then First");
    }

    [Fact]
    public void ALeftToRightLineIsLeftInTheOrderItWasWritten()
    {
        var words = new[] { "one", "two", "three" };
        var resolved = BidiAlgorithm.Resolve(Joined(words));

        var order = VisualOrder.Of(resolved, WordSpans(words));

        order.Should().Equal(new[] { 0, 1, 2 });
    }

    [Fact]
    public void AUnitWithNoCharactersTakesItsPredecessorsKey()
    {
        // A bookmark or line break sitting between two right-to-left words: it contributed no
        // characters of its own and must stay beside the word it followed rather than drift to
        // either end of the reordered line.
        var words = new[] { First, Second };
        var resolved = BidiAlgorithm.Resolve(Joined(words));
        var spans = new List<(int Start, int Length)>(WordSpans(words));
        spans.Insert(1, (spans[0].Start + spans[0].Length, 0));

        var order = VisualOrder.Of(resolved, spans);

        order.Should().Equal(new[] { 2, 0, 1 },
            "the empty unit takes the same key as the word at index 0 that it followed, and a "
            + "stable sort keeps it right after that word rather than before it");
    }

    [Fact]
    public void ALeadingUnitWithNoCharactersHasNoPredecessorToStayBeside()
    {
        // An empty unit at the very start of the line has nothing before it to take the key of, so
        // it keeps the key nothing resolves to - int.MaxValue - and sorts after every unit that has
        // a real position. There being no predecessor is the edge case, not the ordering.
        var words = new[] { First, Second };
        var resolved = BidiAlgorithm.Resolve(Joined(words));
        var spans = new List<(int Start, int Length)>(WordSpans(words));
        spans.Insert(0, (0, 0));

        var order = VisualOrder.Of(resolved, spans);

        order.Should().Equal(new[] { 2, 1, 0 });
    }

    [Fact]
    public void AUnitWhoseCharactersAreNonContiguousInVisualOrderTakesTheLeftmost()
    {
        // The case the rule exists for: a unit spanning a right-to-left word followed by a
        // left-to-right one has its characters scattered by reordering, and has to be placed by the
        // leftmost of them rather than by its first character.
        const string mixed = First + "ab";
        var words = new[] { mixed, Second };
        var resolved = BidiAlgorithm.Resolve(Joined(words), BidiParagraphDirection.RightToLeft);

        var order = VisualOrder.Of(resolved, WordSpans(words));

        order.Should().Equal(new[] { 1, 0 },
            "Second is wholly right of the mixed unit's leftmost character");
    }
}
