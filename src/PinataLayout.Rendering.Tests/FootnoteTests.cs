using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Footnote layout: the mark in the running text, the block at the foot of the page, the room set
///   aside for it, and the numbering.
/// </summary>
/// <remarks>
///   <para>
///     The document object model has carried footnotes since the fork began and this assembly drew
///     none of them. See <c>docs/specs/PinataLayout-footnotes.md</c> for the design and for what was
///     deliberately left out of it.
///   </para>
///   <para>
///     Positions are read back off the page as PDF measures them - points up from the bottom left -
///     so a bigger Y is further up the page. The separator rule is the easiest thing to find: it is
///     the only horizontal hairline any of these documents strokes.
///   </para>
/// </remarks>
public class FootnoteTests
{
    /// <summary>Set on every arrangement, so a test can assert against it.</summary>
    private static readonly Unit BottomMargin = Unit.FromCentimeter(2);

    private const string Prose =
        "Footnote layout reserves the room a note takes before the text carrying its mark is laid "
        + "out, or the page overflows. ";

    // ----- the block exists at all -----

    [Fact]
    public void AFootnoteIsDrawnRatherThanDropped()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        var page = Rendered.FirstPageOf(document);

        Glyphs.On(page).Should().ContainInOrder(Glyphs.For("The support."));
    }

    [Fact]
    public void TheNoteIsSeparatedFromTheBodyByARule()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");

        var page = Rendered.FirstPageOf(document);

        Separator(page).Should().NotBeNull("a footnote block is ruled off from the text above it");
    }

    [Fact]
    public void APageWithNoFootnoteOnItIsRuledOffFromNothing()
    {
        // The guard on the test above: a rule drawn on every page would be worse than none.
        var document = Document(out var section);
        section.AddParagraph("A claim with nothing to support it.");

        var page = Rendered.FirstPageOf(document);

        Separator(page).Should().BeNull();
    }

    [Fact]
    public void TheNoteSitsBelowTheBodyText()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("The support.");
        for (var block = 0; block < 3; block++)
            section.AddParagraph(Prose);

        var page = Rendered.FirstPageOf(document);
        // ReSharper disable once PossibleInvalidOperationException
        var rule = Separator(page).Value;

        // Everything the body draws is above the rule; everything the note draws is below it.
        // Y counts up from the foot of the page, so "below" is a smaller number.
        var noteText = Glyphs.For("The support.");
        BaselinesShowing(page, noteText).Should().OnlyContain(y => y < rule.Y1);
    }

    // ----- the room set aside for it -----

    [Fact]
    public void APageCarryingANoteHoldsLessBodyTextThanOneWithout()
    {
        // The whole of the reservation, in one assertion. The two documents are identical except
        // that one paragraph carries a note, so any difference in how much text fits on the first
        // page is the room that note took.
        var withNote = Filled(note: true);
        var without = Filled(note: false);

        LinesOfBodyOn(Rendered.FirstPageOf(withNote))
            .Should().BeLessThan(LinesOfBodyOn(Rendered.FirstPageOf(without)),
                "the note has to come out of the page somewhere");
    }

    [Fact]
    public void TheBodyTextNeverRunsIntoTheNote()
    {
        var document = Filled(note: true);

        var page = Rendered.FirstPageOf(document);
        // ReSharper disable once PossibleInvalidOperationException
        var rule = Separator(page).Value;

        // Every baseline on the page is either above the rule (body) or below it (note). None sits
        // on it, which is what a page that overflowed into its own footnotes would show.
        var baselines = TextBaselines.Of(page);
        baselines.Should().NotContain(y => Math.Abs(y - rule.Y1) < 1.0);
    }

    [Fact]
    public void TheBlockStaysInsideTheBottomMargin()
    {
        // The reservation is only as good as the height it reserves. If the block is taller than
        // the room set aside for it, it runs off the foot of the text area and into the margin -
        // and on a page with a footer, over the footer.
        var document = Filled(note: true);

        var page = Rendered.FirstPageOf(document);

        // Y counts up from the page's foot, so the text area's own floor is the bottom margin.
        // Set explicitly rather than taken from PageSetup's default, which is 2cm where the top
        // margin's is 2.5cm - a difference that would otherwise be buried in this number.
        TextBaselines.Of(page).Min().Should().BeGreaterThan(BottomMargin.Point,
            "nothing the page draws belongs below the text area");
    }

    [Fact]
    public void TwoNotesOnOnePageAreRuledOffOnceBetweenThem()
    {
        var document = Document(out var section);
        var paragraph = section.AddParagraph("A claim");
        paragraph.AddFootnote("The first support.");
        paragraph.AddText(" and another");
        paragraph.AddFootnote("The second support.");

        var page = Rendered.FirstPageOf(document);

        Rules(page).Should().HaveCount(1, "the separator belongs to the block, not to each note");
        Glyphs.On(page).Should().ContainInOrder(Glyphs.For("The first support."));
        Glyphs.On(page).Should().ContainInOrder(Glyphs.For("The second support."));
    }

    // ----- where the block goes -----

    [Fact]
    public void BottomOfPagePinsTheBlockToTheFootWhateverThePageHolds()
    {
        var document = Document(out var section);
        document.FootnoteLocation = FootnoteLocation.BottomOfPage;
        section.AddParagraph("A claim").AddFootnote("The support.");

        var page = Rendered.FirstPageOf(document);
        // ReSharper disable once PossibleInvalidOperationException
        var rule = Separator(page).Value;

        // A page holding one short paragraph, with the block at its foot: the rule is a long way
        // below the text that carries the mark.
        var bodyBaseline = TextBaselines.Of(page).Max();
        (bodyBaseline - rule.Y1).Should().BeGreaterThan(400,
            "the note is pinned to the foot of an almost empty page");
    }

    [Fact]
    public void BeneathTextPutsTheBlockUnderTheTextRatherThanAtTheFoot()
    {
        var document = Document(out var section);
        document.FootnoteLocation = FootnoteLocation.BeneathText;
        section.AddParagraph("A claim").AddFootnote("The support.");

        var page = Rendered.FirstPageOf(document);
        // ReSharper disable once PossibleInvalidOperationException
        var rule = Separator(page).Value;

        var bodyBaseline = TextBaselines.Of(page).Max();
        (bodyBaseline - rule.Y1).Should().BeLessThan(60,
            "the note follows the text it belongs to rather than the page it is on");
    }

    [Fact]
    public void BeneathTextStillSitsBelowAFullPageOfText()
    {
        // The two locations only differ on a page with room to spare. On a full one, "beneath the
        // text" and "at the foot of the page" are the same place, and neither may overlap the body.
        var document = Filled(note: true);
        document.FootnoteLocation = FootnoteLocation.BeneathText;

        var page = Rendered.FirstPageOf(document);
        // ReSharper disable once PossibleInvalidOperationException
        var rule = Separator(page).Value;

        var noteText = Glyphs.For("The support.");
        BaselinesShowing(page, noteText).Should().OnlyContain(y => y < rule.Y1);
    }

    // ----- numbering -----

    [Theory]
    [InlineData(FootnoteNumberStyle.Arabic, "1", "2", "3")]
    [InlineData(FootnoteNumberStyle.LowercaseLetter, "a", "b", "c")]
    [InlineData(FootnoteNumberStyle.UppercaseLetter, "A", "B", "C")]
    [InlineData(FootnoteNumberStyle.LowercaseRoman, "i", "ii", "iii")]
    [InlineData(FootnoteNumberStyle.UppercaseRoman, "I", "II", "III")]
    public void EachNumberStyleMarksTheNotesItsOwnWay(
        FootnoteNumberStyle style, string first, string second, string third)
    {
        var document = ThreeNotesOnAPage(style);

        var page = Rendered.FirstPageOf(document);
        var marks = MarksOn(page, first, second, third);

        marks.Should().Equal(new[] { first, second, third });
    }

    [Fact]
    public void ACallersOwnReferenceIsUsedInsteadOfANumber()
    {
        var document = Document(out var section);
        var paragraph = section.AddParagraph("A claim");
        paragraph.AddFootnote("The support.").Reference = "*";

        var page = Rendered.FirstPageOf(document);

        // Twice: once in the running text and once at the head of the note.
        CountOfMark(page, "*").Should().Be(2);
    }

    [Fact]
    public void ACallersOwnReferenceDoesNotAdvanceTheNumbering()
    {
        // A note the caller marked shows a symbol of their choosing. Letting it count would make
        // the numbers around it skip for a reason no reader could see.
        var document = Document(out var section);
        var paragraph = section.AddParagraph("A claim");
        paragraph.AddFootnote("First.");
        paragraph.AddText(" and");
        paragraph.AddFootnote("Starred.").Reference = "*";
        paragraph.AddText(" and");
        paragraph.AddFootnote("Second.");

        var page = Rendered.FirstPageOf(document);

        CountOfMark(page, "2").Should().Be(2, "the starred note is not counted");
    }

    [Fact]
    public void NumberingRestartsOnEveryPageByDefault()
    {
        // Worth pinning because it is a surprise. RestartPage is the first value of
        // FootnoteNumberingRule and therefore the enum's default, so a caller who sets nothing gets
        // notes numbered from one on every page - not the running sequence most documents want.
        var document = TwoPagesOfNotes();
        document.FootnoteNumberingRule.Should().Be(FootnoteNumberingRule.RestartPage);

        var pages = Rendered.Of(document);
        pages.PageCount.Should().BeGreaterThan(1);

        MarksOn(pages.Pages[0], "1", "2").Should().Equal(new[] { "1" });
        MarksOn(pages.Pages[1], "1", "2").Should().Equal(new[] { "1" });
    }

    [Fact]
    public void RestartContinuousCarriesTheNumberingAcrossPages()
    {
        var document = TwoPagesOfNotes();
        document.FootnoteNumberingRule = FootnoteNumberingRule.RestartContinuous;

        var pages = Rendered.Of(document);
        pages.PageCount.Should().BeGreaterThan(1);

        MarksOn(pages.Pages[0], "1", "2").Should().Equal(new[] { "1" });
        MarksOn(pages.Pages[1], "1", "2").Should().Equal(new[] { "2" });
    }

    [Fact]
    public void RestartSectionBeginsAgainInEverySectionAndNotOnEveryPage()
    {
        // A section spanning two pages, so the answer tells RestartSection apart from RestartPage.
        // Two sections each on their own page would give the same marks under either rule.
        var document = Document(out var first);
        document.FootnoteNumberingRule = FootnoteNumberingRule.RestartSection;
        first.AddParagraph("A claim").AddFootnote("First.");

        var second = document.AddSection();
        second.AddParagraph("Another claim").AddFootnote("Second.");
        for (var block = 0; block < 20; block++)
            second.AddParagraph(string.Concat(Prose, Prose, Prose));
        second.AddParagraph("A later claim").AddFootnote("Third.");

        var pages = Rendered.Of(document);
        pages.PageCount.Should().Be(3);

        MarksOn(pages.Pages[0], "1", "2", "3").Should().Equal(new[] { "1" });
        MarksOn(pages.Pages[1], "1", "2", "3").Should().Equal(new[] { "1" });
        MarksOn(pages.Pages[2], "1", "2", "3").Should().Equal(new[] { "2" },
            "the second section's notes count on from each other, not from the page");
    }

    [Fact]
    public void TheStartingNumberIsHonoured()
    {
        var document = ThreeNotesOnAPage(FootnoteNumberStyle.Arabic);
        document.FootnoteStartingNumber = 7;

        var page = Rendered.FirstPageOf(document);

        MarksOn(page, "7", "8", "9").Should().Equal(new[] { "7", "8", "9" });
    }

    [Fact]
    public void AnUnsetStartingNumberBeginsAtOneRatherThanZero()
    {
        // The property's default is zero, which is the unset value rather than a request. A first
        // footnote marked "0" would be a strange thing to ship.
        var document = ThreeNotesOnAPage(FootnoteNumberStyle.Arabic);

        document.FootnoteStartingNumber.Should().Be(0, "this is the property's own default");
        MarksOn(Rendered.FirstPageOf(document), "0", "1")[0].Should().Be("1");
    }

    // ----- the mark in the running text -----

    [Fact]
    public void TheMarkIsRaisedAboveTheLineItSitsOn()
    {
        var document = Document(out var section);
        var paragraph = section.AddParagraph("A claim");
        paragraph.AddFootnote("The support.");
        paragraph.AddText(" continues");

        var page = Rendered.FirstPageOf(document);

        // The mark and the words either side of it are on one line, and the mark is drawn higher
        // than they are. A mark left on the baseline would read as a numeral in the sentence.
        var baselines = TextBaselines.Of(page).Where(y => y > 700).OrderByDescending(y => y).ToList();
        baselines.Should().HaveCountGreaterThan(1);
        baselines.Distinct().Should().HaveCountGreaterThan(1, "the mark is not on the text baseline");
    }


    // ----- arrangements -----

    private static Document Document(out Section section)
    {
        var document = new Document();
        var normal = document.Styles[StyleNames.Normal];
        normal.Font.Name = "Liberation Sans";
        normal.Font.Size = 11;

        var footnote = document.Styles[StyleNames.Footnote];
        footnote.Font.Size = 8;

        section = document.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.5);
        section.PageSetup.BottomMargin = BottomMargin;
        return document;
    }

    /// <summary>A page filled to the brim, with or without a note on it.</summary>
    private static Document Filled(bool note)
    {
        var document = Document(out var section);

        var first = section.AddParagraph("A claim");
        if (note)
            first.AddFootnote("The support.");

        for (var block = 0; block < 18; block++)
            section.AddParagraph(string.Concat(Prose, Prose, Prose));

        return document;
    }

    private static Document ThreeNotesOnAPage(FootnoteNumberStyle style)
    {
        var document = Document(out var section);
        document.FootnoteNumberStyle = style;

        var paragraph = section.AddParagraph("A claim");
        paragraph.AddFootnote("First.");
        paragraph.AddText(" and another");
        paragraph.AddFootnote("Second.");
        paragraph.AddText(" and a third");
        paragraph.AddFootnote("Third.");

        return document;
    }

    private static Document TwoPagesOfNotes()
    {
        var document = Document(out var section);
        section.AddParagraph("A claim").AddFootnote("First.");

        for (var block = 0; block < 20; block++)
            section.AddParagraph(string.Concat(Prose, Prose, Prose));

        section.AddParagraph("A later claim").AddFootnote("Second.");
        return document;
    }

    // ----- reading the page -----

    /// <summary>The horizontal hairlines the page strokes, which is where a separator shows up.</summary>
    private static IReadOnlyList<StrokedLines.Line> Rules(PdfPage page) =>
        StrokedLines.Of(page).Where(line => line.IsHorizontal && line.Width <= 1).ToList();

    private static StrokedLines.Line? Separator(PdfPage page)
    {
        var rules = Rules(page);
        return rules.Count == 0 ? null : rules[0];
    }

    /// <summary>Roughly how much body text a page holds, counted in distinct baselines above the rule.</summary>
    private static int LinesOfBodyOn(PdfPage page)
    {
        var rule = Separator(page);
        var baselines = TextBaselines.Of(page).Select(y => Math.Round(y, 1)).Distinct();
        return rule is null
            ? baselines.Count()
            : baselines.Count(y => y > rule.Value.Y1);
    }

    /// <summary>The baselines of the runs whose glyphs match the given sequence.</summary>
    private static IReadOnlyList<double> BaselinesShowing(PdfPage page, IReadOnlyList<int> glyphs)
    {
        // The note's own words are drawn below the rule and nothing else on the page draws them,
        // so the lowest baselines on the page are the note's. Taking every baseline under the rule
        // is enough to say the note went there, and is not sensitive to how the words were split
        // into runs.
        var rule = Separator(page);
        if (rule is null)
            return Array.Empty<double>();

        glyphs.Should().NotBeEmpty();
        Glyphs.On(page).Should().ContainInOrder(glyphs);

        return TextBaselines.Of(page).Where(y => y < rule.Value.Y1).ToList();
    }

    /// <summary>
    ///   The generated marks the page shows, in the order the note block draws them.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Each mark is drawn twice - beside the claim and at the head of the note - so the
    ///     sequence is read off the note block, where they appear once each in order. The block is
    ///     rendered after the body, so the last time a mark is shown on the page is the block's
    ///     copy of it, and sorting the marks by that position puts them in the block's order.
    ///   </para>
    ///   <para>
    ///     Ordered by where each mark was actually drawn rather than by the order they were asked
    ///     for. Walking the candidates and keeping the ones that appear returns them in the
    ///     caller's own order however the page was drawn, so a page marked three, two, one would
    ///     have satisfied the assertion this feeds.
    ///   </para>
    /// </remarks>
    private static IReadOnlyList<string> MarksOn(PdfPage page, params string[] candidates)
    {
        var runs = Glyphs.RunsOn(page);

        var found = new List<(int At, string Mark)>();
        foreach (var mark in candidates)
        {
            var at = LastRunShowing(runs, Glyphs.For(mark));
            if (at >= 0)
                found.Add((at, mark));
        }

        found.Sort((left, right) => left.At.CompareTo(right.At));
        return found.Select(entry => entry.Mark).ToList();
    }

    /// <summary>
    ///   Which of the page's show-text runs last consists of exactly this mark, or -1 for none.
    /// </summary>
    /// <remarks>
    ///   Whole runs rather than a search through the glyphs: under roman numbering the marks are
    ///   prefixes of one another, so looking for the glyphs of <c>I</c> in a flattened sequence
    ///   finds the last one inside <c>III</c> and reports the first note as the last drawn. A mark
    ///   is shown as a run of its own, so equality against the run says where it really went.
    /// </remarks>
    private static int LastRunShowing(IReadOnlyList<IReadOnlyList<int>> runs, IReadOnlyList<int> wanted)
    {
        for (var at = runs.Count - 1; at >= 0; at--)
        {
            if (runs[at].Count != wanted.Count)
                continue;

            var matches = true;
            for (var idx = 0; idx < wanted.Count; idx++)
            {
                if (runs[at][idx] != wanted[idx])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                return at;
        }

        return -1;
    }

    /// <summary>How many times a mark is drawn on the page.</summary>
    /// <remarks>
    ///   Compared as glyphs rather than as characters: PinataLayout embeds Identity-H, so a show-text
    ///   operator carries glyph identifiers. See <see cref="Glyphs"/>.
    /// </remarks>
    private static int CountOfMark(PdfPage page, string mark)
    {
        var wanted = Glyphs.For(mark);
        var shown = Glyphs.On(page);

        var count = 0;
        for (var at = 0; at + wanted.Count <= shown.Count; at++)
        {
            var matches = true;
            for (var idx = 0; idx < wanted.Count; idx++)
            {
                if (shown[at + idx] != wanted[idx])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
                ++count;
        }

        return count;
    }
}
