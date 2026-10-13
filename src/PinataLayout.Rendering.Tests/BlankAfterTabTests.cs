using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Text after a tab with something in it the line sets no width for.
/// </summary>
/// <remarks>
///   A tab decides whether it breaks the line, and where a right or center aligned stop sets the
///   text after it, by measuring that text up to the next tab or line break. The measuring stopped
///   at anything the line ignores - a blank straight after the tab, or a bookmark - and took that
///   for the end of what would fit. Stopping before any word was reached broke the line, so
///   "before", a tab and " after" were set on two lines on a page with room for a hundred such
///   words; stopping part way through measured only the first part, so a right aligned stop set
///   the rest past itself. Ported from empira/PDFsharp#405.
///
///   Each arrangement is checked against the same paragraph without the ignored element, which
///   is where it should have put the text: on the first line, and starting at the same place.
/// </remarks>
public class BlankAfterTabTests
{
    [Test]
    [Arguments(TabAlignment.Left)]
    [Arguments(TabAlignment.Right)]
    [Arguments(TabAlignment.Center)]
    public void ABlankAfterATabIsSetAsIfItWereNotThere(TabAlignment alignment)
    {
        var withBlank = TextAfterTab(alignment, paragraph => paragraph.AddText(" after"));
        var without = TextAfterTab(alignment, paragraph => paragraph.AddText("after"));

        withBlank.Should().BeEquivalentTo(without, options => options.WithStrictOrdering()
            .Using<double>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.01)).WhenTypeIs<double>());
    }

    [Test]
    [Arguments(TabAlignment.Left)]
    [Arguments(TabAlignment.Right)]
    [Arguments(TabAlignment.Center)]
    public void ABookmarkAfterATabIsSetAsIfItWereNotThere(TabAlignment alignment)
    {
        var withBookmark = TextAfterTab(alignment, paragraph =>
        {
            paragraph.AddBookmark("mark");
            paragraph.AddText("after");
        });
        var without = TextAfterTab(alignment, paragraph => paragraph.AddText("after"));

        withBookmark.Should().BeEquivalentTo(without, options => options.WithStrictOrdering()
            .Using<double>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 0.01)).WhenTypeIs<double>());
    }

    [Test]
    [Arguments(TabAlignment.Right)]
    [Arguments(TabAlignment.Center)]
    public void ABookmarkInsideTheTextAfterATabDoesNotCutShortWhatTheStopMeasures(TabAlignment alignment)
    {
        // "af" and "ter" are drawn as two runs, so it is where the first of them begins that says
        // whether the stop measured the whole of "after" or only the part before the bookmark.
        var split = TextAfterTab(alignment, paragraph =>
        {
            paragraph.AddText("af");
            paragraph.AddBookmark("mark");
            paragraph.AddText("ter");
        });
        var whole = TextAfterTab(alignment, paragraph => paragraph.AddText("after"));

        split[0].Y.Should().BeApproximately(whole[0].Y, 0.01, "the text stays on the first line");
        split[0].X.Should().BeApproximately(whole[0].X, 0.01, "the stop sets the whole word against itself");
    }

    /// <summary>
    ///   Where the runs drawn after a tab begin, with the text before the tab and the tab stop at
    ///   8cm fixed and what follows the tab added by the caller.
    /// </summary>
    private static IReadOnlyList<(double X, double Y)> TextAfterTab(TabAlignment alignment, Action<Paragraph> after)
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.LeftMargin = 0;
        section.PageSetup.RightMargin = 0;

        var paragraph = section.AddParagraph();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(8), alignment);
        paragraph.AddText("before");
        paragraph.AddTab();
        after(paragraph);

        var runs = TextBaselines.PositionsOf(Rendered.FirstPageOf(document));
        runs[0].Y.Should().BeApproximately(runs[^1].Y, 0.01, "every word fits on the first line");

        return [.. runs.Skip(1)];
    }
}
