using System;
using AwesomeAssertions;
using PdfPinata;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   <see cref="FormattedDocument"/> is what a document becomes once it has been laid out: a fixed
///   number of pages, each with a header, a footer and a content rectangle worked out from the
///   section's page setup.
///   <para>
///   Most of that arithmetic has an arm nobody meets in an ordinary document — a page setup that
///   mirrors its margins swaps left for right on alternate pages, a landscape section swaps the
///   page's width for its height, a section that must begin on an even page has an empty one put
///   in before it, and a section with a different first page has three header positions rather
///   than one. Each is a branch, and each changes where everything on the page is drawn.
///   </para>
/// </summary>
public class FormattedDocumentPageTests
{
    private static FormattedDocument LaidOut(Document document)
    {
        var renderer = new DocumentRenderer(document);
        renderer.PrepareDocument();
        return renderer.FormattedDocument;
    }

    private static Section ASectionOfSeveralPages(Document document, int paragraphs = 120)
    {
        var section = document.AddSection();
        for (var index = 0; index < paragraphs; index++)
            section.AddParagraph("Paragraph " + index);
        return section;
    }

    // ----- how many pages there are ------------------------------------------------------------------

    [Fact]
    public void ADocumentIsLaidOutIntoAsManyPagesAsItNeeds()
    {
        var document = new Document();
        ASectionOfSeveralPages(document);

        var formatted = LaidOut(document);

        formatted.PageCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public void APageTheDocumentDoesNotHaveIsRefusedRatherThanAnsweredWithNothing()
    {
        var document = new Document();
        document.AddSection().AddParagraph("one page");
        var formatted = LaidOut(document);

        ((Action)(() => formatted.GetPageInfo(0))).Should().Throw<ArgumentOutOfRangeException>();
        ((Action)(() => formatted.GetPageInfo(formatted.PageCount + 1)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EachPageKnowsTheSizeAndOrientationItWasLaidOutAt()
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A5;
        section.PageSetup.Orientation = Orientation.Landscape;
        section.AddParagraph("sideways");

        var info = LaidOut(document).GetPageInfo(1);

        // The orientation is recorded but does not swap the two measurements: Width and Height
        // stay the page format's own, and it is the renderer that turns the page.
        info.Orientation.Should().Be(PageOrientation.Landscape);
        info.Width.Point.Should().BeGreaterThan(0);
        info.Height.Point.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TheCurrentPositionIsTheBottomOfWhatWasLaidOutLast()
    {
        var document = new Document();
        document.AddSection().AddParagraph("something to measure");

        var position = LaidOut(document).GetCurrentPinataLayoutPosition();

        position.Should().BeGreaterThan(0);
    }

    // ----- margins that swap sides --------------------------------------------------------------------

    /// <summary>
    ///   Mirrored margins are for a document to be printed double sided: the wide margin is on the
    ///   inside, which is the left of a right-hand page and the right of a left-hand one. So the
    ///   content starts at a different x on odd and even pages, which nothing else in the layout
    ///   does.
    /// </summary>
    [Fact]
    public void MirroredMarginsPutTheWideMarginOnTheInsideOfEachPage()
    {
        var document = new Document();
        var section = ASectionOfSeveralPages(document);
        section.PageSetup.MirrorMargins = true;
        section.PageSetup.LeftMargin = "4cm";
        section.PageSetup.RightMargin = "1cm";

        var pages = Rendered.Of(document);

        pages.PageCount.Should().BeGreaterThan(1);

        var first = Glyphs.PlacedOn(pages.Pages[0]);
        var second = Glyphs.PlacedOn(pages.Pages[1]);

        first.Should().NotBeEmpty();
        second.Should().NotBeEmpty();
        second[0].X.Should().NotBeApproximately(first[0].X, 1,
            "the wide margin moves from one side to the other");
    }

    // ----- a section that must start on a particular page ----------------------------------------------

    [Fact]
    public void ASectionThatMustBeginOnAnEvenPageIsGivenAnEmptyOneBeforeIt()
    {
        var document = new Document();
        document.AddSection().AddParagraph("one page only");
        var second = document.AddSection();
        second.PageSetup.SectionStart = BreakType.BreakEvenPage;
        second.AddParagraph("the second section");

        var formatted = LaidOut(document);

        formatted.PageCount.Should().Be(2, "the first section's one page is odd, so the second starts on two");
    }

    [Fact]
    public void ASectionThatMustBeginOnAnOddPageIsGivenAnEmptyOneWhenItWouldNot()
    {
        var document = new Document();
        document.AddSection().AddParagraph("one page only");
        var second = document.AddSection();
        second.PageSetup.SectionStart = BreakType.BreakOddPage;
        second.AddParagraph("the second section");

        var formatted = LaidOut(document);

        formatted.PageCount.Should().Be(3,
            "an empty page two is put in so that the second section begins on page three");
    }

    // ----- headers and footers that differ ---------------------------------------------------------------

    [Fact]
    public void ASectionWithADifferentFirstPageDrawsThreeDifferentHeaders()
    {
        var document = new Document();
        var section = ASectionOfSeveralPages(document, 220);
        section.PageSetup.DifferentFirstPageHeaderFooter = true;
        section.PageSetup.OddAndEvenPagesHeaderFooter = true;
        section.Headers.FirstPage.AddParagraph("Alpha");
        section.Headers.Primary.AddParagraph("Bravo");
        section.Headers.EvenPage.AddParagraph("Charlie");
        section.Footers.FirstPage.AddParagraph("Delta");
        section.Footers.Primary.AddParagraph("Echo");
        section.Footers.EvenPage.AddParagraph("Foxtrot");

        var pages = Rendered.Of(document);

        pages.PageCount.Should().BeGreaterThan(2);

        Glyphs.RunsOn(pages.Pages[0]).Should().ContainEquivalentOf(Glyphs.For("Alpha"));
        Glyphs.RunsOn(pages.Pages[0]).Should().ContainEquivalentOf(Glyphs.For("Delta"));
        Glyphs.RunsOn(pages.Pages[1]).Should().ContainEquivalentOf(Glyphs.For("Charlie"));
        Glyphs.RunsOn(pages.Pages[1]).Should().ContainEquivalentOf(Glyphs.For("Foxtrot"));
        Glyphs.RunsOn(pages.Pages[2]).Should().ContainEquivalentOf(Glyphs.For("Bravo"));
        Glyphs.RunsOn(pages.Pages[2]).Should().ContainEquivalentOf(Glyphs.For("Echo"));
    }

    [Fact]
    public void AHeaderAndFooterAreDrawnOnALandscapePageToo()
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.Orientation = Orientation.Landscape;
        section.Headers.Primary.AddParagraph("Golf");
        section.Footers.Primary.AddParagraph("Hotel");
        section.AddParagraph("sideways");

        var page = Rendered.FirstPageOf(document);

        Glyphs.RunsOn(page).Should().ContainEquivalentOf(Glyphs.For("Golf"));
        Glyphs.RunsOn(page).Should().ContainEquivalentOf(Glyphs.For("Hotel"));
    }

    // ----- being told how far along the layout is ---------------------------------------------------------

    /// <summary>
    ///   A renderer with a progress handler counts the elements of every section up front so that
    ///   it has a maximum to report against. Attaching a handler is what turns that counting on.
    /// </summary>
    [Fact]
    public void ARendererWithAProgressHandlerReportsItsWayThroughTheDocument()
    {
        var document = new Document();
        ASectionOfSeveralPages(document, 20);
        var renderer = new DocumentRenderer(document);

        var maximum = 0;
        var reports = 0;
        renderer.PrepareDocumentProgress += (_, e) =>
        {
            maximum = e.Maximum;
            reports++;
        };

        renderer.HasPrepareDocumentProgress.Should().BeTrue();
        renderer.PrepareDocument();

        reports.Should().BeGreaterThan(0);
        maximum.Should().BeGreaterThanOrEqualTo(20);
    }
}
