using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   A justified list item is laid out around an automatic tab stop after its bullet, and a
///   justified paragraph holding a tab has its lines measured a second time when it is drawn.
///   That second pass was told where each line ends but not where it starts, and it was allowed
///   to break the line it was only supposed to be measuring. A soft hyphen on the first line of
///   such a paragraph therefore either threw or never finished.
///   See https://github.com/empira/PDFsharp/issues/339.
/// </summary>
public class SoftHyphenInJustifiedListTests
{
    // The text from the issue, hyphenated with U+00AD.
    private const string HyphenatedText =
        "This is a long text that should demon­strate the is­sue of Mi­graDoc. "
        + "It con­tains words like demon­stra­tion, which should be "
        + "hy­phen­ated cor­rect­ly. The text should be jus­ti­fied "
        + "and the hy­phen­ation should work as ex­pect­ed.";

    // A4 with PinataLayout's default 2.5cm margins.
    private static readonly double LeftEdge = Unit.FromCentimeter(2.5).Point;
    private static readonly double RightEdge = Unit.FromMillimeter(210).Point - Unit.FromCentimeter(2.5).Point;

    [Fact(Timeout = 60000)]
    public async Task AListItemWhoseFirstLineHoldsASoftHyphenCanBeDrawn()
    {
        // Measuring the line again asks whether the soft hyphen is the first thing on it. The
        // renderer drawing the paragraph had never been told where the line starts, so the
        // question was asked of a null and the whole document came to nothing.
        var render = () => Render(rightIndentMillimeters: 5);

        await render.Should().NotThrowAsync();
    }

    [Theory(Timeout = 60000)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task AJustifiedListItemIsDrawnWhateverTheRightIndent(int rightIndentMillimeters)
    {
        // At a narrow right indent the second measuring pass finds the line no longer fits, and
        // it used to act on that: the soft hyphen moved the cursor back to the word before it,
        // the loop stepped forward onto the hyphen again, and no document ever came out.
        var document = await Render(rightIndentMillimeters);

        document.PageCount.Should().Be(1);
        TextBaselines.LinesOf(document.Pages[0]).Should().HaveCount(3);
    }

    [Theory(Timeout = 60000)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task NothingIsDrawnOutsideTheContentArea(int rightIndentMillimeters)
    {
        var document = await Render(rightIndentMillimeters);
        var right = RightEdge - Unit.FromMillimeter(rightIndentMillimeters).Point;

        foreach (var (x, _) in TextBaselines.PositionsOf(document.Pages[0]))
        {
            x.Should().BeGreaterThanOrEqualTo(LeftEdge - 0.001);

            // Where a run starts, not where it ends: a run beginning on the right edge itself has
            // nowhere left to draw, so it is already outside. Half a point is narrower than
            // anything the text has in it, the hyphen that ends a broken word included.
            x.Should().BeLessThanOrEqualTo(right - 0.5);
        }
    }

    [Theory(Timeout = 60000)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task TheWordsOfALineAreDrawnLeftToRight(int rightIndentMillimeters)
    {
        // A line measured against the wrong starting point puts its words back to front or on
        // top of one another, which is what the screenshot on the issue shows.
        var document = await Render(rightIndentMillimeters);

        foreach (var line in LinesOf(document.Pages[0]))
            line.Should().BeInAscendingOrder();
    }

    [Theory(Timeout = 60000)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task EveryLineButTheLastIsStretchedOutToTheRightEdge(int rightIndentMillimeters)
    {
        // Every line of a justified paragraph but the last is stretched to fill its width. The
        // first line is the one that goes through the second measuring pass with the bullet's
        // tab in front of it, so it is the one that used to come out wrong. A line measured
        // short of what it holds stops well before the edge.
        var lines = LinesOf(await Render(rightIndentMillimeters));
        var right = RightEdge - Unit.FromMillimeter(rightIndentMillimeters).Point;

        // The last run of a line starts one word - or one hyphen - short of the edge, so the
        // bound is the widest word the text has rather than nothing at all.
        foreach (var line in lines.Take(lines.Count - 1))
            line[^1].Should().BeGreaterThan(right - 40);
    }

    [Fact(Timeout = 60000)]
    public async Task TheBulletIsStillDrawnInFrontOfTheFirstLine()
    {
        var lines = LinesOf(await Render(rightIndentMillimeters: 2));

        // The bullet sits at the list's number position, ahead of the text's left indent.
        var numberPosition = LeftEdge + Unit.FromMillimeter(5).Point;
        lines[0][0].Should().BeApproximately(numberPosition, 0.1);
        lines[1][0].Should().BeApproximately(LeftEdge + Unit.FromMillimeter(10).Point, 0.1);
    }

    [Fact(Timeout = 60000)]
    public async Task AnElementThatMeasuresAsNothingDoesNotCutTheLineShort()
    {
        // A bookmark takes no room and asks to be ignored rather than counted. Stopping the
        // measurement there would leave the rest of the line uncounted, and the gaps worked out
        // from what little was counted would push the words off the edge.
        var withBookmark = LinesOf(await Render(rightIndentMillimeters: 2, bookmarkAfterWords: 6));
        var withoutBookmark = LinesOf(await Render(rightIndentMillimeters: 2));

        withBookmark[0].Should().HaveCount(withoutBookmark[0].Count);
        for (var idx = 0; idx < withBookmark[0].Count; idx++)
            withBookmark[0][idx].Should().BeApproximately(withoutBookmark[0][idx], 0.01);
    }

    [Fact(Timeout = 60000)]
    public async Task AJustifiedParagraphThatIsNotAListIsDrawnAsItAlwaysWas()
    {
        // No list means no automatic tab stop, so no second measuring pass. This is the control:
        // the same text and the same indents, down the path the change does not touch.
        var document = await Render(rightIndentMillimeters: 2, asList: false);

        document.PageCount.Should().Be(1);
        foreach (var line in LinesOf(document.Pages[0]))
            line.Should().BeInAscendingOrder();
    }

    /// <summary>
    ///   The horizontal position of every run of text, grouped into the lines they sit on, from
    ///   the top of the page downwards.
    /// </summary>
    private static IReadOnlyList<IReadOnlyList<double>> LinesOf(PdfDocument document)
    {
        return LinesOf(document.Pages[0]);
    }

    private static IReadOnlyList<IReadOnlyList<double>> LinesOf(PdfPage page)
    {
        var runs = TextBaselines.PositionsOf(page);
        return [..runs
            .GroupBy(run => Math.Round(run.Y, 3))
            .OrderByDescending(line => line.Key)
            .Select(line => (IReadOnlyList<double>)line.Select(run => run.X).ToList())];
    }

    private static async Task<PdfDocument> Render(int rightIndentMillimeters, bool asList = true,
        int bookmarkAfterWords = -1)
    {
        return await Interruptibly.Run(() =>
        {
            var document = new Document();
            var paragraph = document.AddSection().AddParagraph();

            if (asList)
            {
                paragraph.Format.ListInfo = new ListInfo
                {
                    ContinuePreviousList = false,
                    ListType = ListType.BulletList1,
                    NumberPosition = Unit.FromMillimeter(5)
                };
            }

            paragraph.Format.LeftIndent = Unit.FromMillimeter(10);
            paragraph.Format.RightIndent = Unit.FromMillimeter(rightIndentMillimeters);
            paragraph.Format.Alignment = ParagraphAlignment.Justify;

            if (bookmarkAfterWords < 0)
                paragraph.AddText(HyphenatedText);
            else
            {
                // Split the text so that a bookmark sits in the middle of the first line.
                var words = HyphenatedText.Split(' ');
                paragraph.AddText(string.Join(" ", words.Take(bookmarkAfterWords)) + " ");
                paragraph.AddBookmark("mark");
                paragraph.AddText(string.Join(" ", words.Skip(bookmarkAfterWords)));
            }

            var renderer = new PdfDocumentRenderer(true) { Document = document };
            renderer.RenderDocument();

            // Saving is what closes the content streams, so the page cannot be read before it.
            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);

            return renderer.PdfDocument;
        });
    }
}
