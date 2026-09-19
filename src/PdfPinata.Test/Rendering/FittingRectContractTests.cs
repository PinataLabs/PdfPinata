using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   What <c>Area.GetFittingRect</c> answering null means, and that the renderer honours it.
/// </summary>
/// <remarks>
///   Null means "there is nowhere here to put a line of that height". A plain rectangle says it in
///   one situation only — the band runs off the bottom — which is why it has been rare enough to
///   leave half-handled: the code carried a standing <c>// BUG: Code removed because null is not
///   handled in caller</c>, four call sites dereferenced the result without looking, and a fifth
///   contained <c>if (fittingRect == null) GetType();</c>, which is a breakpoint rather than a
///   decision.
///   <para>
///     An area with something standing in it will answer null for a second reason, and far more
///     often: the band is there and every part of it is taken. These tests establish the contract
///     against the rectangle that exists today, so that a later failure in a new kind of area is
///     attributable to the new kind of area.
///   </para>
/// </remarks>
public class FittingRectContractTests
{
    const string Prose =
        "The quick brown fox jumps over the lazy dog, and having jumped it lands and looks about " +
        "for somewhere else to be, which takes rather longer than the jump did.";

    [Fact]
    public void ABandInsideTheAreaFitsAndKeepsTheAreasWidth()
    {
        var area = AreaProbe.Rectangle(x: 10, y: 20, width: 300, height: 200);

        var rect = area.FittingRect(yPosition: 50, height: 12);

        rect.Should().NotBeNull();
        rect.Bounds().Should().Be((10, 50, 300, 12));
    }

    [Fact]
    public void ABandRunningOffTheBottomHasNowhereToGo()
    {
        var area = AreaProbe.Rectangle(x: 10, y: 20, width: 300, height: 200);

        // The area ends at y = 220; a 12pt line starting at 215 would need to reach 227.
        area.FittingRect(yPosition: 215, height: 12).Should().BeNull();
    }

    [Fact]
    public void ABandEndingExactlyOnTheBottomStillFits()
    {
        var area = AreaProbe.Rectangle(x: 10, y: 20, width: 300, height: 200);

        // Flush with the bottom edge, which is inside the area rather than past it. The tolerance
        // is what keeps arithmetic that lands a thousandth of a point over from losing a line.
        area.FittingRect(yPosition: 208, height: 12).Should().NotBeNull();
    }

    [Fact]
    public void ATallerBandThanTheWholeAreaHasNowhereToGo()
    {
        var area = AreaProbe.Rectangle(x: 10, y: 20, width: 300, height: 40);

        area.FittingRect(yPosition: 20, height: 100).Should().BeNull();
    }

    // ----- what the renderer does with it ---------------------------------------------------------

    [Fact]
    public void AParagraphTallerThanThePageIsCarriedOnRatherThanLost()
    {
        // Every line after the first page's worth asks for a band the area cannot give, which is
        // the null path taken through the whole renderer rather than through one method.
        var pages = Render(document =>
        {
            var section = document.AddSection();
            for (var idx = 0; idx < 60; idx++)
                section.AddParagraph(Prose);
        });

        pages.Should().BeGreaterThan(1, "the text is carried onto further pages, not dropped");
    }

    [Fact]
    public void APageWithNoRoomForASingleLineDoesNotHangOrThrow()
    {
        // A text area shorter than one line: every request for a band comes back null, from the
        // very first one. Nothing may loop forever and nothing may throw.
        var render = () => Render(document =>
        {
            var section = document.AddSection();
            section.PageSetup.PageHeight = "6cm";
            section.PageSetup.TopMargin = "2.9cm";
            section.PageSetup.BottomMargin = "2.9cm";
            section.AddParagraph(Prose);
        });

        render.Should().NotThrow();
    }

    [Fact]
    public void NoPageIsLeftBlankByALineThatFoundNowhereToGo()
    {
        // A band coming back null at the foot of a page must move the line to the next page, not
        // consume a page putting nothing on it. A renderer that answered null by starting a fresh
        // area without placing anything would emit blanks and still finish.
        var document = new Document();
        var section = document.AddSection();
        for (var idx = 0; idx < 60; idx++)
            section.AddParagraph(Prose);

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        renderer.PdfDocument.PageCount.Should().BeGreaterThan(1);

        for (var page = 0; page < renderer.PdfDocument.PageCount; page++)
        {
            TextOperators.ShownStrings(renderer.PdfDocument.Pages[page])
                .Should().NotBeEmpty("page " + (page + 1) + " carries text");
        }
    }

    [Fact]
    public void AJustifiedParagraphAtAPageBreakIsRenderedWithoutFailing()
    {
        // Justification reads the fitting rect again in the rendering phase, at a point where the
        // line has already been placed and cannot decline. That is one of the call sites that used
        // to dereference the result without looking.
        var render = () => Render(document =>
        {
            var section = document.AddSection();
            for (var idx = 0; idx < 40; idx++)
            {
                var paragraph = section.AddParagraph(Prose);
                paragraph.Format.Alignment = ParagraphAlignment.Justify;
            }
        });

        render.Should().NotThrow();
    }

    [Fact]
    public void AParagraphWithTabsAtAPageBreakIsRenderedWithoutFailing()
    {
        // Tab alignment reads the fitting rect too, and was another unchecked dereference.
        var render = () => Render(document =>
        {
            var section = document.AddSection();
            for (var idx = 0; idx < 40; idx++)
            {
                var paragraph = section.AddParagraph();
                paragraph.Format.TabStops.AddTabStop("8cm", TabAlignment.Center);
                paragraph.AddText("Left");
                paragraph.AddTab();
                paragraph.AddText(Prose);
            }
        });

        render.Should().NotThrow();
    }

    // ----- rendering ------------------------------------------------------------------------------

    static int Render(Action<Document> build)
    {
        var document = new Document();
        build(document);

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        return renderer.PdfDocument.PageCount;
    }
}
