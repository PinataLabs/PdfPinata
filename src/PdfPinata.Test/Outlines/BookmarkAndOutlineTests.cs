using System;
using System.Collections.Generic;
using System.IO;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.Rendering;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Outlines;

/// <summary>
///   Where a bookmark and an outline entry take a reader.
/// </summary>
/// <remarks>
///   Both used to name a page and say nothing about where on it, so following either left the
///   reader wherever the page happened to be scrolled to rather than at the heading. A bookmark
///   put on a section rather than inside a paragraph was dropped without a word, which is what
///   https://github.com/ststeiger/PdfSharpCore/issues/321 was really about.
/// </remarks>
public class BookmarkAndOutlineTests
{
    /// <summary>
    /// A4 is 842pt tall and the default top margin is 2.5cm, so anything at the top of the
    /// text area sits about this far up the page.
    /// </summary>
    private const double TopOfTheTextArea = 771.0;

    [Fact]
    public void AnOutlineEntryPointsAtTheHeadingRatherThanAtThePage()
    {
        var pdf = Render(document =>
        {
            var section = document.AddSection();
            Heading(section, "Heading1", "Scones");
        });

        Destination(FirstOutline(pdf)).Top.Should().BeApproximately(TopOfTheTextArea, 2);
    }

    [Fact]
    public void AnOutlineEntryFurtherDownThePagePointsFurtherDown()
    {
        var pdf = Render(document =>
        {
            var section = document.AddSection();
            for (var i = 0; i < 12; i++)
                section.AddParagraph("Filler line " + i);
            Heading(section, "Heading1", "Scones");
        });

        // Twelve lines below the top of the text area, so well down the page but still on it.
        var top = Destination(FirstOutline(pdf)).Top;
        top.Should().BeLessThan(TopOfTheTextArea - 100);
        top.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ALocalHyperlinkPointsAtTheBookmarkRatherThanAtThePage()
    {
        var pdf = Render(TableOfContentsThenBookmark(BookmarkPlacement.OnTheParagraph));

        var dest = Destination(OnlyLink(pdf));
        dest.Page.Should().Be(2);
        dest.Top.Should().BeApproximately(TopOfTheTextArea, 2);
    }

    /// <summary>
    ///   The code on the issue: the bookmark goes on the section rather than into a paragraph.
    ///   It used to be dropped, so the link was never made and the page reference rendered as
    ///   "Bookmark 'x' is not defined within the document".
    /// </summary>
    [Fact]
    public void ABookmarkPutOnASectionIsNotDropped()
    {
        var pdf = Render(TableOfContentsThenBookmark(BookmarkPlacement.OnTheSection));

        var dest = Destination(OnlyLink(pdf));
        dest.Page.Should().Be(2);
        dest.Top.Should().BeApproximately(TopOfTheTextArea, 2);
    }

    [Fact]
    public void ABookmarkAddedThroughAddBookmarkIsNotDropped()
    {
        var pdf = Render(TableOfContentsThenBookmark(BookmarkPlacement.ThroughAddBookmark));

        Destination(OnlyLink(pdf)).Page.Should().Be(2);
    }

    /// <summary>
    ///   A landscape page is the page setup turned on its side, so the height to measure the
    ///   bookmark against is the width of the paper. Getting this the wrong way round puts the
    ///   destination off the page.
    /// </summary>
    [Fact]
    public void ABookmarkOnALandscapePageIsMeasuredAgainstTheShorterSide()
    {
        var pdf = Render(document =>
        {
            var section = document.AddSection();
            section.PageSetup.Orientation = Orientation.Landscape;
            Heading(section, "Heading1", "Scones");
        });

        // A4 landscape is 595pt tall, less the 2.5cm top margin.
        var top = Destination(FirstOutline(pdf)).Top;
        top.Should().BeApproximately(595 - 71, 3);
    }

    [Fact]
    public void AHyperlinkToABookmarkThatIsNotThereMakesNoLink()
    {
        var pdf = Render(document =>
        {
            var section = document.AddSection();
            var link = section.AddParagraph().AddHyperlink("nowhere");
            link.AddText("Go nowhere");
        });

        Links(pdf).Should().BeEmpty();
    }

    /// <summary>
    ///   A destination with no position is still what a link to an unknown place writes, so the
    ///   old shape has to keep working for callers using PdfPinata directly.
    /// </summary>
    [Fact]
    public void ADocumentLinkWithoutAPositionStillNamesOnlyThePage()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var page = document.AddPage();
        page.AddDocumentLink(new PdfRectangle(new XRect(10, 10, 50, 20)), 1);

        var dest = Destination(ReRead(document).Pages[1].Elements.GetArray("/Annots")
            .Elements.GetDictionary(0));
        dest.Page.Should().Be(1);
        double.IsNaN(dest.Top).Should().BeTrue();
    }

    [Fact]
    public void ADocumentLinkGivenAPositionCarriesIt()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var page = document.AddPage();
        page.AddDocumentLink(new PdfRectangle(new XRect(10, 10, 50, 20)), 1, 456.5);

        Destination(ReRead(document).Pages[1].Elements.GetArray("/Annots")
            .Elements.GetDictionary(0)).Top.Should().BeApproximately(456.5, 0.01);
    }

    private enum BookmarkPlacement { OnTheParagraph, OnTheSection, ThroughAddBookmark }

    private static Action<Document> TableOfContentsThenBookmark(BookmarkPlacement placement)
    {
        return document =>
        {
            var contents = document.AddSection();
            contents.AddParagraph("Contents");
            var link = contents.AddParagraph().AddHyperlink("recipe");
            link.AddText("Scones, page ");
            link.AddPageRefField("recipe");

            var section = document.AddSection();
            switch (placement)
            {
                case BookmarkPlacement.OnTheSection:
                    section.Elements.Add(new BookmarkField("recipe"));
                    section.AddParagraph("Scones");
                    break;
                case BookmarkPlacement.ThroughAddBookmark:
                    section.Elements.AddBookmark("recipe");
                    section.AddParagraph("Scones");
                    break;
                default:
                    var heading = section.AddParagraph();
                    heading.AddBookmark("recipe");
                    heading.AddText("Scones");
                    break;
            }
        };
    }

    private static void Heading(Section section, string style, string text)
    {
        var paragraph = section.AddParagraph();
        paragraph.Style = style;
        paragraph.AddText(text);
    }

    private static PdfDocument Render(Action<Document> build)
    {
        var document = new Document();
        build(document);

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        return ReRead(renderer.PdfDocument);
    }

    private static PdfDocument ReRead(PdfDocument document)
    {
        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        // Fully qualified: the test assembly has a PdfReader of its own.
        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
    }

    private static PdfDictionary FirstOutline(PdfDocument pdf)
    {
        var root = pdf.Internals.Catalog.Elements.GetDictionary("/Outlines");
        root.Should().NotBeNull("the document should have an outline");
        return root.Elements.GetDictionary("/First");
    }

    private static PdfDictionary OnlyLink(PdfDocument pdf)
    {
        var links = Links(pdf);
        links.Should().HaveCount(1);
        return links[0];
    }

    private static List<PdfDictionary> Links(PdfDocument pdf)
    {
        var links = new List<PdfDictionary>();
        foreach (var page in pdf.Pages)
        {
            var annots = page.Elements.GetArray("/Annots");
            if (annots == null)
                continue;
            for (var i = 0; i < annots.Elements.Count; i++)
            {
                var annot = annots.Elements.GetDictionary(i);
                if (annot != null && annot.Elements.GetName("/Subtype") == "/Link")
                    links.Add(annot);
            }
        }
        return links;
    }

    /// <summary>
    ///   Reads a /Dest of the form [page /XYZ left top zoom], answering the one-based page
    ///   number and how far up it the destination sits.
    /// </summary>
    private static (int Page, double Top) Destination(PdfDictionary annotationOrOutline)
    {
        var dest = annotationOrOutline.Elements.GetArray("/Dest");
        dest.Should().NotBeNull("the entry should carry an explicit destination");

        // Matched on object number rather than on instance: asking a document for a page can
        // hand back a fresh wrapper around the same dictionary.
        var target = dest.Elements.GetReference(0);
        target.Should().NotBeNull("the destination should name a page");
        var pageNumber = 0;
        var owner = annotationOrOutline.Owner;
        for (var i = 0; i < owner.PageCount; i++)
        {
            if (owner.Pages[i].Reference != null &&
                owner.Pages[i].Reference.ObjectNumber == target.ObjectNumber)
                pageNumber = i + 1;
        }

        // [page /XYZ left top zoom] -- the top is the fourth entry, after the left.
        var top = dest.Elements.Count < 4 || dest.Elements[3] == null || dest.Elements[3] is PdfNull
            ? double.NaN
            : dest.Elements.GetReal(3);
        return (pageNumber, top);
    }
}
