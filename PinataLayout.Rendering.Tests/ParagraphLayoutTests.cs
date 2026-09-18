using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   How paragraphs are set against one another down a page, and what happens when there are more
///   of them than the page holds.
/// </summary>
/// <remarks>
///   Promoted from MigraDoc 1.32's TestLayout, which set two paragraphs with a space after the
///   first and a larger space before the second and saved the result to look at, and then set a
///   thousand of them to see them break across pages.
///
///   The two-paragraph arrangement turns out to have been asking a real question. Two adjacent
///   spaces do not add up: the larger of them wins and the smaller is not applied at all, so the
///   two centimetres after the first paragraph are worth nothing beside the three before the
///   second. Nothing said so, and the harness could not have told you either way.
///
///   The thousand is scaled down. It was a stress run whose value was in seeing pages break, and
///   two hundred paragraphs break pages just as well in a fraction of the time.
/// </remarks>
public class ParagraphLayoutTests
{
    [Theory]
    [InlineData(2, 3, 3)]
    [InlineData(3, 2, 3)]
    [InlineData(0, 3, 3)]
    [InlineData(3, 0, 3)]
    [InlineData(1, 1, 1)]
    public void TheSpaceBetweenTwoParagraphsIsTheLargerOfTheirTwoAndNotTheSum(
        double after, double before, double expected)
    {
        // Set against a paragraph with no spacing at all, so that what is asserted is the space
        // that was added rather than the height of a line of ten point text.
        var gap = GapBetweenTwoParagraphs(after, before) - GapBetweenTwoParagraphs(0, 0);

        gap.Should().BeApproximately(Unit.FromCentimeter(expected).Point, 0.01);
    }

    [Fact]
    public void MoreParagraphsThanThePageHoldsAreCarriedOntoTheNextOne()
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.TopMargin = 0;
        section.PageSetup.BottomMargin = 0;

        for (var idx = 1; idx <= 200; ++idx)
            section.AddParagraph("Paragraph " + idx);

        var pdf = Rendered.Of(document);

        // Every page carries text. A break that opens a page and puts nothing on it still
        // finishes, and still counts, and is only visible by looking at what each page holds.
        pdf.PageCount.Should().BeGreaterThan(1);
        for (var page = 0; page < pdf.PageCount; page++)
        {
            TextOperators.ShownStrings(pdf.Pages[page])
                .Should().NotBeEmpty("page " + (page + 1) + " carries text");
        }
    }

    [Fact]
    public void APageBreaksAtTheSameLineWhicheverParagraphTheTextIsBrokenInto()
    {
        // The renderer fills a page a paragraph at a time but breaks it a line at a time, so a
        // page holding the same lines written as one paragraph and as many is the two paths
        // agreeing about where the foot of the page is.
        var asOne = LinesOnTheFirstPage(paragraphs: 1, linesEach: 200);
        var asMany = LinesOnTheFirstPage(paragraphs: 200, linesEach: 1);

        asMany.Should().Be(asOne);
    }

    static double GapBetweenTwoParagraphs(double after, double before)
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.TopMargin = 0;
        section.PageSetup.BottomMargin = 0;

        var first = section.AddParagraph("one");
        first.Format.SpaceAfter = Unit.FromCentimeter(after);

        var second = section.AddParagraph("two");
        second.Format.SpaceBefore = Unit.FromCentimeter(before);

        var baselines = TextBaselines.LinesOf(Rendered.FirstPageOf(document));
        return baselines[0] - baselines[1];
    }

    /// <summary>
    ///   How many lines of text the first page ends up carrying, for text broken into that many
    ///   paragraphs of that many lines each.
    /// </summary>
    static int LinesOnTheFirstPage(int paragraphs, int linesEach)
    {
        var document = new Document();
        var section = document.AddSection();
        // No spacing, so that the two arrangements differ in nothing but where the paragraph
        // boundaries fall. A space before a paragraph would give the many-paragraph version more
        // to fit and the comparison would be between two different documents.
        section.Document.Styles[StyleNames.Normal].ParagraphFormat.SpaceBefore = 0;
        section.Document.Styles[StyleNames.Normal].ParagraphFormat.SpaceAfter = 0;

        for (var paragraph = 0; paragraph < paragraphs; paragraph++)
        {
            var added = section.AddParagraph();
            for (var line = 0; line < linesEach; line++)
            {
                if (line > 0)
                    added.AddLineBreak();
                added.AddText("line");
            }
        }

        return TextBaselines.LinesOf(Rendered.FirstPageOf(document)).Count;
    }
}
