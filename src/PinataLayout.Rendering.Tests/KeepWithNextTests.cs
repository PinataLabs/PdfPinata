using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A paragraph asking to be kept with the next one must not be left alone at the foot of a page.
///   Deciding that means the formatter looks past the element it is placing to the ones after it,
///   and asks whether they would fit in what is left — a heading followed by a table that will not
///   fit has to go over with the table rather than end the page on its own.
/// </summary>
/// <remarks>
///   The look-ahead is bounded: it will drag at most ten elements along, so a run of paragraphs all
///   asking to be kept with the next one cannot empty a page between them. The tests below measure
///   the page rather than assert an absolute line count, since how many lines a page holds depends
///   on the font metrics and this project pins those precisely so that it can.
/// </remarks>
public class KeepWithNextTests
{
    /// <summary>Enough single-line paragraphs to run well past the foot of the first page.</summary>
    private const int Plenty = 200;

    private static Document Filled(Action<Paragraph, int> arrange)
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.TopMargin = 0;
        section.PageSetup.BottomMargin = 0;

        for (var idx = 1; idx <= Plenty; ++idx)
            arrange(section.AddParagraph("Paragraph " + idx), idx);

        return document;
    }

    private static int LinesOnTheFirstPage(Document document)
    {
        return TextBaselines.LinesOf(Rendered.FirstPageOf(document)).Count;
    }

    /// <summary>How many of these paragraphs a page holds when none of them asks for anything.</summary>
    private static int PageCapacity => LinesOnTheFirstPage(Filled((_, _) => { }));

    /// <summary>
    ///   The paragraph that would have been last on the page asks to be kept with the next one,
    ///   and the next one cannot fit beneath it, so it goes over too and the page ends one line
    ///   earlier.
    /// </summary>
    [Fact]
    public void TheLastParagraphOnAPageGoesOverWhenItMustStayWithTheNext()
    {
        var capacity = PageCapacity;

        var kept = LinesOnTheFirstPage(Filled((paragraph, idx) =>
        {
            if (idx == capacity)
                paragraph.Format.KeepWithNext = true;
        }));

        kept.Should().Be(capacity - 1, "the paragraph at the foot of the page left with the next one");
    }

    /// <summary>
    ///   A paragraph in the middle of a page has room beneath it for what follows, so asking to be
    ///   kept with the next one changes nothing. Without this the test above would pass for a
    ///   formatter that broke the page early whatever the circumstances.
    /// </summary>
    [Fact]
    public void AParagraphWithRoomBeneathItIsNotMoved()
    {
        var capacity = PageCapacity;

        var kept = LinesOnTheFirstPage(Filled((paragraph, idx) =>
        {
            if (idx == capacity / 2)
                paragraph.Format.KeepWithNext = true;
        }));

        kept.Should().Be(capacity, "there was room for the next paragraph beneath it");
    }

    /// <summary>
    ///   The two paragraphs at the foot go over together, so the page ends two lines early rather
    ///   than one. The look-ahead is a chain: the second one is only kept because the first asked
    ///   for it, and it in turn asks for the third.
    /// </summary>
    [Fact]
    public void ARunAtTheFootOfThePageGoesOverTogether()
    {
        var capacity = PageCapacity;

        var kept = LinesOnTheFirstPage(Filled((paragraph, idx) =>
        {
            if (idx >= capacity - 1)
                paragraph.Format.KeepWithNext = true;
        }));

        kept.Should().Be(capacity - 2, "both paragraphs at the foot left with what followed them");
    }

    /// <summary>
    ///   Every paragraph asking to be kept with the next one is a chain that reaches the end of the
    ///   document, and nothing can satisfy it. The look-ahead stops after ten elements rather than
    ///   following it, so the page still fills and the document still ends.
    /// </summary>
    [Fact]
    public void ADocumentThatAsksForTheImpossibleStillFills()
    {
        var document = Filled((paragraph, _) => paragraph.Format.KeepWithNext = true);

        var pdf = Rendered.Of(document);

        pdf.PageCount.Should().BeGreaterThan(1);
        TextOperators.ShownStrings(pdf.Pages[0])
            .Should().NotBeEmpty("the first page carries text rather than being left empty by the chain");
    }

    /// <summary>
    ///   The same run, held together as well as kept with the next, which is the other way into the
    ///   look-ahead — a paragraph that must not be split and must not be parted from what follows.
    /// </summary>
    [Fact]
    public void AParagraphHeldTogetherAndKeptWithTheNextAlsoGoesOver()
    {
        var capacity = PageCapacity;

        var kept = LinesOnTheFirstPage(Filled((paragraph, idx) =>
        {
            if (idx != capacity)
                return;

            paragraph.Format.KeepWithNext = true;
            paragraph.Format.KeepTogether = true;
        }));

        kept.Should().Be(capacity - 1);
    }

    /// <summary>
    ///   The last paragraph in the document has nothing to be kept with, so the request is met by
    ///   doing nothing rather than by opening a page for it to be lonely on.
    /// </summary>
    [Fact]
    public void TheLastParagraphInTheDocumentHasNothingToStayWith()
    {
        var document = new Document();
        var section = document.AddSection();
        var only = section.AddParagraph("The only paragraph");
        only.Format.KeepWithNext = true;

        var pdf = Rendered.Of(document);

        pdf.PageCount.Should().Be(1);
        TextOperators.ShownStrings(pdf.Pages[0]).Should().NotBeEmpty();
    }
}
