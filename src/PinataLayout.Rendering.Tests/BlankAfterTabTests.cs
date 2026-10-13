using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Text after a tab that begins with something the line sets no width for.
/// </summary>
/// <remarks>
///   A tab decides whether it breaks the line by measuring what follows it up to the next tab or
///   line break, and it broke the line whenever that measuring stopped before a word was found. A
///   blank straight after a tab is dropped rather than measured, and so is a bookmark, and the
///   measuring took either for the end of what would fit - so "before", a tab and " after" were set
///   on two lines on a page with room for a hundred such words. Ported from empira/PDFsharp#405.
/// </remarks>
public class BlankAfterTabTests
{
    [Test]
    [Arguments(TabAlignment.Left)]
    [Arguments(TabAlignment.Right)]
    [Arguments(TabAlignment.Center)]
    public void ABlankAfterATabDoesNotPushTheTextAfterItOntoTheNextLine(TabAlignment alignment)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(8), alignment);
        paragraph.AddText("before");
        paragraph.AddTab();
        paragraph.AddText(" after");

        BaselinesOf(document).Should().ContainSingle("both words fit on the one line");
    }

    [Test]
    public void ABookmarkAfterATabDoesNotPushTheTextAfterItOntoTheNextLine()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(8));
        paragraph.AddText("before");
        paragraph.AddTab();
        paragraph.AddBookmark("mark");
        paragraph.AddText("after");

        BaselinesOf(document).Should().ContainSingle("both words fit on the one line");
    }

    private static double[] BaselinesOf(Document document)
    {
        return [.. TextBaselines.Of(Rendered.FirstPageOf(document)).Select(y => Math.Round(y, 2)).Distinct()];
    }
}
