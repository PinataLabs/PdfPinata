using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   A footer sits at the bottom of its area rather than the top, so its content is moved down
///   by however much of the area it leaves empty. Every element used to be moved to the same
///   place rather than by the same distance, which drew the paragraphs of a footer on top of
///   one another. Headers were never moved and so were never affected.
///   See https://github.com/ststeiger/PdfSharpCore/issues/414.
/// </summary>
public class HeaderFooterParagraphTests
{
    [Fact]
    public void TheParagraphsOfAFooterAreDrawnOnLinesOfTheirOwn()
    {
        var page = Render(document =>
        {
            var section = document.AddSection();
            section.Footers.Primary.AddParagraph("Hello world");
            section.Footers.Primary.AddParagraph("This overlaps the other line!");
        });

        // The snippet on the issue, which drew both paragraphs on one line.
        FooterLinesOf(page).Should().HaveCount(2);
    }

    [Fact]
    public void AFooterSpacesItsParagraphsTheWayAHeaderDoes()
    {
        var page = Render(document =>
        {
            var section = document.AddSection();
            section.Headers.Primary.AddParagraph("Header one");
            section.Headers.Primary.AddParagraph("Header two");
            section.Footers.Primary.AddParagraph("Footer one");
            section.Footers.Primary.AddParagraph("Footer two");
        });

        var lines = TextBaselines.LinesOf(page);
        var headerGap = lines[0] - lines[1];
        var footerGap = lines[^2] - lines[^1];

        // Same style, same font, so the same distance from one line to the next.
        headerGap.Should().BeGreaterThan(0);
        footerGap.Should().BeApproximately(headerGap, 0.001);
    }

    [Fact]
    public void EachFurtherParagraphOfAFooterGoesBelowTheOneBefore()
    {
        var page = Render(document =>
        {
            var section = document.AddSection();
            for (var i = 1; i <= 3; i++)
                section.Footers.Primary.AddParagraph("Footer line " + i);
        });

        var footer = FooterLinesOf(page);
        footer.Should().HaveCount(3);
        footer.Should().BeInDescendingOrder();

        // Evenly spaced, as three paragraphs of one style are.
        var gaps = footer.Zip(footer.Skip(1), (above, below) => above - below).ToList();
        gaps.Should().OnlyContain(gap => gap > 0);
        foreach (var gap in gaps)
            gap.Should().BeApproximately(gaps[0], 0.001);
    }

    [Fact]
    public void AFooterOfOneParagraphSitsWhereItAlwaysDid()
    {
        var page = Render(document =>
        {
            var section = document.AddSection();
            section.Footers.Primary.AddParagraph("Only one footer line");
            section.AddParagraph("Body");
        });

        // The whole of a footer is moved down by the height it leaves unused, so a footer of
        // one paragraph is unmoved by the fix and still sits within its own area.
        var footer = FooterLinesOf(page);
        footer.Should().ContainSingle();
        footer[0].Should().BeInRange(0, 72);
    }

    [Fact]
    public void TheParagraphsOfAFooterStayInTheOrderTheyWereAdded()
    {
        var page = Render(document =>
        {
            var section = document.AddSection();
            section.Footers.Primary.AddParagraph("First");
            section.Footers.Primary.AddParagraph("Second");
            section.Footers.Primary.AddParagraph("Third");
        });

        // Drawn in document order, and each one below the last.
        var drawn = TextBaselines.Of(page).Where(IsFooter).Distinct().ToList();
        drawn.Should().BeInDescendingOrder();
    }

    /// <summary>The lines of the footer, which is everything drawn near the foot of the page.</summary>
    private static System.Collections.Generic.IReadOnlyList<double> FooterLinesOf(PdfPage page)
    {
        return TextBaselines.LinesOf(page).Where(IsFooter).ToList();
    }

    private static bool IsFooter(double baseline)
    {
        // A4 is 842 points tall with a 2.5cm bottom margin, so nothing but the footer is drawn
        // this near the foot of the page.
        return baseline < 72;
    }

    private static PdfPage Render(System.Action<Document> build)
    {
        var document = new Document();
        build(document);

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }
}
