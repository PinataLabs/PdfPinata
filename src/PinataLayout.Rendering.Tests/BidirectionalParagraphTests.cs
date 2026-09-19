using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using PdfPinata.Text;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A paragraph whose words have to change places to be read.
/// </summary>
/// <remarks>
///   <para>
///     <c>XGraphics.DrawString</c> turns a right-to-left string round on its own, so every word of
///     a Hebrew paragraph has always come out correctly. The words themselves did not: this
///     renderer draws one show-text operator per leaf and advances the pen by its width, so the
///     words stayed in the order they were written and the sentence read inside out.
///   </para>
///   <para>
///     Hebrew because Liberation Sans - which the tests pin so that layout is the same everywhere -
///     draws both Hebrew and Latin. A face answering <c>.notdef</c> for half the line would make a
///     wrong order indistinguishable from a right one. No shaper is registered: Hebrew does not
///     join, and reordering is not shaping.
///   </para>
/// </remarks>
public class BidirectionalParagraphTests
{
    // Three Hebrew words of two letters each. Escapes rather than literals, so that a source file
    // mixing right-to-left text with left-to-right code cannot be misread.
    const string First = "\u05D0\u05D1";
    const string Second = "\u05D2\u05D3";

    static Document Paragraph(string text,
        BidiParagraphDirection direction = BidiParagraphDirection.Automatic)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.TextDirection = direction;
        paragraph.AddText(text);

        return document;
    }

    /// <summary>The glyph one character draws as, for reading an order back.</summary>
    static int GlyphOf(char letter) => Glyphs.For(letter.ToString()).Single();

    /// <summary>
    ///   The glyphs the given letters draw as, in the order given. Nothing separates the words:
    ///   PinataLayout puts the space between two words in the positioning rather than drawing one, so
    ///   no whitespace glyph is ever shown.
    /// </summary>
    static IReadOnlyList<int> Drawn(string letters) => letters.Select(GlyphOf).ToList();

    /// <summary>Several glyph sequences, concatenated - for a line with more than one segment.</summary>
    static IReadOnlyList<int> Joined(params IReadOnlyList<int>[] parts) => parts.SelectMany(part => part).ToList();

    // ----- the defect ------------------------------------------------------------------------------

    [Fact]
    public void AParagraphOfHebrewIsLaidOutInTheOrderItIsRead()
    {
        var page = Rendered.FirstPageOf(Paragraph(First + " " + Second));

        Glyphs.AcrossThePage(page).Should().Equal(Drawn("\u05D3\u05D2\u05D1\u05D0"),
            "the word written first is the rightmost, so it is drawn last - and each word's own "
            + "letters were already the right way round");
    }

    [Fact]
    public void AnEnglishPhraseInsideAHebrewParagraphKeepsItsOwnWordOrder()
    {
        // The case that says the words are really being ordered rather than the line being turned
        // round. Reversing a right-to-left line word by word is the obvious implementation and it
        // gets this wrong: two English words inside a Hebrew sentence run left to right between
        // themselves, and only their position in the sentence is right to left.
        var page = Rendered.FirstPageOf(Paragraph(First + " one two " + Second));

        Glyphs.AcrossThePage(page).Should().Equal(Drawn("\u05D3\u05D2" + "one" + "two" + "\u05D1\u05D0"));
    }

    [Fact]
    public void ADeclaredDirectionOverridesWhatTheFirstStrongCharacterSays()
    {
        // "one" is the first strong character, so the algorithm left to itself reads the whole
        // paragraph left to right and puts the Hebrew after it.
        var page = Rendered.FirstPageOf(
            Paragraph("one " + First, BidiParagraphDirection.RightToLeft));

        Glyphs.AcrossThePage(page).Should().Equal(Drawn("\u05D1\u05D0" + "one"),
            "the Hebrew is drawn leftmost although it was written last");
    }

    [Fact]
    public void LeavingItToBeGuessedIsStillWhatHappensByDefault()
    {
        var page = Rendered.FirstPageOf(Paragraph("one " + First));

        Glyphs.AcrossThePage(page).Should().Equal(Drawn("one" + "\u05D1\u05D0"),
            "the first strong character is Latin, so the paragraph reads left to right");
    }

    // ----- what it costs everything else -------------------------------------------------------------

    [Fact]
    public void AParagraphWithNothingRightToLeftInItIsPlacedExactlyAsItAlwaysWas()
    {
        // The guarantee that makes this safe to leave switched on. A line with nothing right to
        // left in it is not even measured twice: the scan that says so runs before anything else
        // does, and answers from the characters alone.
        var page = Rendered.FirstPageOf(Paragraph("one two three"));

        Glyphs.AcrossThePage(page).Should().Equal(Drawn("onetwothree"));
    }

    [Fact]
    public void ALineWithATabAndOneWordEitherSideOfItIsUnchangedBecauseThereIsNothingToSwap()
    {
        // What used to be the documented limitation, kept as the baseline it now is. A tab divides
        // this line into two segments of one word each, and a segment of one word has nothing to
        // reorder against - so the outcome is the same as when reordering was skipped outright, and
        // for a different reason: each word was already turned round inside itself by DrawString,
        // and there is only ever one leaf per segment here for the tab boundary to leave alone.
        var page = Rendered.FirstPageOf(Paragraph(First + "\t" + Second));

        Glyphs.AcrossThePage(page).Should().Equal(Drawn("\u05D1\u05D0\u05D3\u05D2"),
            "each word is turned round inside itself; a segment of one word has no order to fix");
    }

    // ----- the fix: a tab divides the line, and each segment reorders on its own -------------------

    [Fact]
    public void ARightToLeftLineWithOneTabReordersBothSegmentsIndependently()
    {
        // The case the old guard got backwards. Each side of the tab holds two words, so each side
        // has an order of its own to fix, and the tab itself must not move either segment into the
        // other's territory.
        var page = Rendered.FirstPageOf(Paragraph(First + " " + Second + "\t" + Second + " " + First));

        Glyphs.AcrossThePage(page).Should().Equal(
            Joined(Drawn("\u05D3\u05D2\u05D1\u05D0"), Drawn("\u05D1\u05D0\u05D3\u05D2")),
            "the segment before the tab reorders on its own, and so does the segment after it");
    }

    [Fact]
    public void SeveralTabsReorderEverySegmentIndependently()
    {
        var page = Rendered.FirstPageOf(Paragraph(
            First + " " + Second + "\t" + Second + " " + First + "\t" + First + " " + Second));

        Glyphs.AcrossThePage(page).Should().Equal(
            Joined(
                Drawn("\u05D3\u05D2\u05D1\u05D0"),
                Drawn("\u05D1\u05D0\u05D3\u05D2"),
                Drawn("\u05D3\u05D2\u05D1\u05D0")),
            "a three-column tabbed layout reorders every column, not only the first");
    }

    [Fact]
    public void ALeftToRightPhraseInsideATabbedRightToLeftSegmentKeepsItsOwnWordOrder()
    {
        // The tabbed sibling of AnEnglishPhraseInsideAHebrewParagraphKeepsItsOwnWordOrder: a segment
        // is ordered exactly as a whole line is, so an English phrase inside one still keeps its own
        // internal order rather than being reversed word by word.
        var page = Rendered.FirstPageOf(Paragraph(First + " one two " + Second + "\t" + First));

        Glyphs.AcrossThePage(page).Should().Equal(
            Joined(Drawn("\u05D3\u05D2" + "one" + "two" + "\u05D1\u05D0"), Drawn("\u05D1\u05D0")),
            "one and two keep their own order although the Hebrew around them is reordered");
    }

    [Fact]
    public void ALeftToRightTabbedLineIsUnaffected()
    {
        // The regression every existing document depends on: nothing right to left anywhere on the
        // line, so the cheap scan answers no and the tab is drawn exactly as it always was.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("abc");
        paragraph.AddTab();
        paragraph.AddText("def");

        var page = Rendered.FirstPageOf(document);

        Glyphs.On(page).Should().Equal(Glyphs.AcrossThePage(page),
            "written order and reading order are the same line when nothing on it is right to left");
        Glyphs.On(page).Should().Equal(Drawn("abc").Concat(Drawn("def")).ToList());
    }

    [Fact]
    public void TheMarksOfATabbedReorderedLineStayInTheOrderTheTextIsRead()
    {
        // The extension of TheMarksStayInTheOrderTheTextIsRead to a line with a tab in it. Only
        // where a word lands changed - the leaves are still walked, and so marked, in the order
        // they were written.
        var page = Rendered.FirstPageOf(Paragraph(First + " " + Second + "\t" + Second + " " + First));

        Glyphs.On(page).Should().Equal(
            Joined(Drawn("\u05D1\u05D0" + "\u05D3\u05D2"), Drawn("\u05D3\u05D2" + "\u05D1\u05D0")),
            "written in the order the words were written, tab or no tab");
        Glyphs.AcrossThePage(page).Should().Equal(
            Joined(Drawn("\u05D3\u05D2\u05D1\u05D0"), Drawn("\u05D1\u05D0\u05D3\u05D2")),
            "and placed in the order they are read");
    }

    [Fact]
    public void ADecimalTabStillAlignsOnTheSeparatorWithRightToLeftTextBeforeIt()
    {
        // The mechanical half of the fix: the tab width list has to be replayable rather than
        // consumed, because this line now gets walked twice (RTL label found before the tab), and a
        // decimal tab is the one kind whose position a corrupted read would visibly move. If the
        // probing walk left the list's read position consumed, the real walk would read the wrong
        // tab's width - or none at all - and the point would not land on the stop any more.
        double WhereTheNumberStarts(string number)
        {
            var document = new Document();
            var paragraph = document.AddSection().AddParagraph();
            paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(6), TabAlignment.Decimal);
            paragraph.AddText(First + " " + Second);
            paragraph.AddTab();
            paragraph.AddText(number);

            var runs = TextBaselines.PositionsOf(Rendered.FirstPageOf(document));
            return runs.Max(run => run.X);
        }

        WhereTheNumberStarts("1.5").Should().BeGreaterThan(WhereTheNumberStarts("1234.5"),
            "however many digits come before the point, the point itself lands on the stop");
    }

    [Fact]
    public void ADecimalTabInADeclaredRightToLeftParagraphWithNoActualRightToLeftTextStillAligns()
    {
        // A paragraph can declare a direction without holding anything that direction actually
        // affects. The declaration alone makes the cheap scan answer yes and starts the probing
        // walk - and because nothing in the line ever turns out to need reordering, the real walk
        // falls straight back to plain incremental positioning, which is exactly what a tab width
        // list left consumed rather than replayed would corrupt. The line above this one is probed
        // too, but every leaf's real position comes off the reordered array regardless of the tab's
        // own corrupted width; this is the line where nothing shields the bug.
        double WhereTheNumberStarts(string number)
        {
            var document = new Document();
            var paragraph = document.AddSection().AddParagraph();
            paragraph.Format.TextDirection = BidiParagraphDirection.RightToLeft;
            paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(6), TabAlignment.Decimal);
            paragraph.AddText("label");
            paragraph.AddTab();
            paragraph.AddText(number);

            var runs = TextBaselines.PositionsOf(Rendered.FirstPageOf(document));
            return runs.Max(run => run.X);
        }

        WhereTheNumberStarts("1.5").Should().BeGreaterThan(WhereTheNumberStarts("1234.5"),
            "a probed line that never actually reorders must still get the tab's width right");
    }

    [Fact]
    public void UnderlineOnAReorderedTabbedLineDrawsNoBackwardsRule()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        var left = paragraph.AddFormattedText(First + " " + Second);
        left.Font.Underline = Underline.Single;
        paragraph.AddTab();
        var right = paragraph.AddFormattedText(Second + " " + First);
        right.Font.Underline = Underline.Single;

        var page = Rendered.FirstPageOf(document);
        var rules = StrokedLines.Of(page).Where(line => line.IsHorizontal).ToList();

        rules.Should().NotBeEmpty();
        rules.Should().OnlyContain(line => line.X1 <= line.X2,
            "reordering a tabbed segment must not turn its underline into a rule that runs backwards");
    }

    [Fact]
    public void StrikethroughOnAReorderedTabbedLineDrawsNoBackwardsRule()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        var left = paragraph.AddFormattedText(First + " " + Second);
        left.Font.Strikethrough = Strikethrough.Single;
        paragraph.AddTab();
        var right = paragraph.AddFormattedText(Second + " " + First);
        right.Font.Strikethrough = Strikethrough.Single;

        var page = Rendered.FirstPageOf(document);
        var rules = StrokedLines.Of(page).Where(line => line.IsHorizontal).ToList();

        rules.Should().NotBeEmpty();
        rules.Should().OnlyContain(line => line.X1 <= line.X2,
            "reordering a tabbed segment must not turn its strikethrough into a rule that runs backwards");
    }

    [Fact]
    public void ATabLeaderOnAReorderedTabbedLineIsDrawnOnce()
    {
        // Every other leaf checks `probing` and returns before touching the page - RenderWord,
        // RenderBlank, RenderImage all do. RenderTab did not, because until this change a tab's
        // segment could never actually need reordering, so RenderTab was never called during a
        // probing walk at all. Now that a tab's own segment can, a leader tab inside one is exactly
        // AFootnoteMarkOnAReorderedLineIsDrawnOnce's defect again: drawn once for the probe and
        // once for real, at the same position, because a tab's own position never moves.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(6), TabLeader.Dots);
        paragraph.AddText(First + " " + Second);
        paragraph.AddTab();
        paragraph.AddText(Second + " " + First);

        var page = Rendered.FirstPageOf(document);
        var dot = GlyphOf('.');

        var leaderRuns = Glyphs.RunsOn(page).Where(run => run.Count > 0 && run.All(g => g == dot)).ToList();

        leaderRuns.Should().ContainSingle(
            "the probing walk must not draw the leader a second time when the segment it sits in "
            + "needs reordering");
    }

    [Fact]
    public void AHyperlinkInsideATabbedRightToLeftLineKeepsItsClickableAreaWhereTheTextIs()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("one two");
        paragraph.AddTab();
        paragraph.AddHyperlink("https://example.com", HyperlinkType.Web).AddText(Second);
        paragraph.AddText(" " + First);

        var page = Rendered.FirstPageOf(document);

        var linkGlyphs = Drawn("\u05D3\u05D2");
        var firstWordGlyphs = Drawn("\u05D1\u05D0");

        var placed = Glyphs.PlacedOn(page);
        var linkRun = placed.Single(run => run.Run.SequenceEqual(linkGlyphs));
        var firstWordRun = placed.Single(run => run.Run.SequenceEqual(firstWordGlyphs));

        linkRun.X.Should().BeGreaterThan(firstWordRun.X,
            "the word written first is rightmost once the segment is read right to left");

        var rect = page.Annotations[0].Elements.GetRectangle("/Rect");
        rect.X1.Should().BeApproximately(linkRun.X, 0.5,
            "the clickable area has to start where the reordered word was actually drawn, not where "
            + "it was written");
    }

    [Fact]
    public void ATabbedLineInATableCellReordersToo()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(8));
        var paragraph = table.AddRow().Cells[0].AddParagraph();
        paragraph.AddText(First + " " + Second);
        paragraph.AddTab();
        paragraph.AddText(Second + " " + First);

        var page = Rendered.FirstPageOf(document);

        Glyphs.AcrossThePage(page).Should().Equal(
            Joined(Drawn("\u05D3\u05D2\u05D1\u05D0"), Drawn("\u05D1\u05D0\u05D3\u05D2")),
            "the fix is not silently limited to body paragraphs");
    }

    [Fact]
    public void TheMarksStayInTheOrderTheTextIsRead()
    {
        // Only where a word lands changed. The leaves are still walked in the order they were
        // written, so the marked content - and the structure tree that points at it - are in
        // reading order, which is what a structure tree is for and what a screen reader announces.
        // The two orders now genuinely differ, which is the whole of this change in one assertion.
        var page = Rendered.FirstPageOf(Paragraph(First + " " + Second));

        Glyphs.On(page).Should().Equal(Drawn("\u05D1\u05D0\u05D3\u05D2"),
            "written in the order the words were written");
        Glyphs.AcrossThePage(page).Should().Equal(Drawn("\u05D3\u05D2\u05D1\u05D0"),
            "and placed in the order they are read");
    }

    [Fact]
    public void AFootnoteMarkOnAReorderedLineIsDrawnOnce()
    {
        // The probing walk is a walk of the same renderers with the drawing turned off, and every
        // one of them has to know it. RenderFootnote did not: it drew its mark during the probe, at
        // the position the probe had reached, and then again for real at the reordered position. So
        // a Hebrew paragraph carrying a footnote grew a second reference mark, in the wrong place,
        // that nothing in the structure tree accounted for.
        var document = new Document();
        var section = document.AddSection();
        var paragraph = section.AddParagraph();
        paragraph.Format.TextDirection = BidiParagraphDirection.RightToLeft;
        paragraph.AddText(First + " " + Second);
        paragraph.AddFootnote("The note.");

        var page = Rendered.FirstPageOf(document);
        var mark = Glyphs.For("1").Single();

        // Where each mark landed, not just how many there are. A count alone would be satisfied by
        // two marks on the paragraph, which is close to the defect being pinned: the duplicate was
        // drawn on the same line as the real one, at the position the probing walk had reached.
        var marks = Glyphs.PlacedOn(page)
            .Where(run => run.Run.Contains(mark))
            .Select(run => run.Y)
            .OrderByDescending(y => y)
            .ToList();

        // The paragraph's own baseline: the highest thing on the page that is not a mark. The other
        // text on the page is the note, which is at the foot of it.
        var line = Glyphs.PlacedOn(page)
            .Where(run => !run.Run.Contains(mark))
            .Max(run => run.Y);

        marks.Should().HaveCount(2, "one beside the text and one at the head of the note");
        marks[0].Should().BeApproximately(line, 6.0,
            "the reference mark is a superscript on the paragraph's own line");
        marks[1].Should().BeLessThan(line - 100,
            "and the other is at the head of the note, down at the foot of the page");
    }
}
