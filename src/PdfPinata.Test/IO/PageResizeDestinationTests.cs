using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A page carries no list of what points at it, so a resize has to go looking: through the
///   annotations of every page, the outline tree, the name tree of the catalog, the /Dests
///   dictionary PDF 1.1 used, and the action the document opens with.
///   <para>
///   Every test resizes the second page to exactly half its size, stretched, so the transform is
///   a plain halving and the expected numbers can be read off.
///   </para>
/// </summary>
public class PageResizeDestinationTests
{
    private const double A4Width = 595;
    private const double A4Height = 842;
    private const double Tolerance = 0.01;

    private sealed class Fixture
    {
        internal PdfDocument Document;
        internal PdfPage Source;
        internal PdfPage Target;
    }

    /// <summary>Two A4 pages, both drawn on; links are hung off the first, pointing at the second.</summary>
    private static Fixture TwoPages()
    {
        var document = new PdfDocument();
        var first = document.AddPage();
        var second = document.AddPage();

        foreach (var page in new[] { first, second })
        {
            page.Size = PageSize.A4;
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));
        }

        return new Fixture { Document = document, Source = first, Target = second };
    }

    private static void HalveThePage(PdfPage page)
    {
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(new XSize(A4Width / 2, A4Height / 2), options);
    }

    private static PdfArray Destination(PdfPage target, params PdfItem[] rest)
    {
        var destination = new PdfArray(target.Owner);
        destination.Elements.Add(target.Reference);
        foreach (var item in rest)
            destination.Elements.Add(item);
        return destination;
    }

    /// <summary>Hangs a link annotation carrying the destination off the page.</summary>
    private static void LinkOn(PdfPage page, string key, PdfItem destination)
    {
        var link = new PdfDictionary(page.Owner);
        link.Elements.SetName("/Type", "/Annot");
        link.Elements.SetName("/Subtype", "/Link");
        link.Elements.SetRectangle("/Rect", new PdfRectangle(new XPoint(0, 0), new XPoint(10, 10)));
        link.Elements[key] = destination;
        page.Owner.Internals.AddObject(link);

        var annotations = page.Elements.GetArray("/Annots");
        if (annotations == null)
        {
            annotations = new PdfArray(page.Owner);
            page.Elements["/Annots"] = annotations;
        }
        annotations.Elements.Add(link.Reference);
    }

    [Fact]
    public void ALinkFromAnotherPageFollowsTheContentItPointedAt()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfReal(1));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void TheZoomOfAnXyzDestinationIsNotTouchedWhenThePageShrinks()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfReal(1));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(4).Should().BeApproximately(1, Tolerance,
            "the zoom is a magnification the reader asked for, not a promise about text size");
    }

    [Fact]
    public void TheZoomIsNotTouchedWhenThePageGrowsEither()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfReal(1));
        LinkOn(fixture.Source, "/Dest", destination);

        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        fixture.Target.Resize(new XSize(A4Width * 2, A4Height * 2), options);

        destination.Elements.GetReal(4).Should().BeApproximately(1, Tolerance,
            "scaling the zoom the other way would undo the enlargement at the moment the " +
            "reader arrives, which is the opposite of what enlarging the document was for");
        destination.Elements.GetReal(2).Should().BeApproximately(200, Tolerance);
    }

    [Fact]
    public void AZoomOfZeroIsLeftAlone()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(4).Should().Be(0, "zero means the reader keeps its own zoom");
    }

    [Fact]
    public void ANullCoordinateIsLeftAlone()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), PdfNull.Value, new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements[2].Should().Be(PdfNull.Value);
        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void AFitDestinationHasNothingToMove()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target, new PdfName("/Fit"));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.Count.Should().Be(2);
        destination.Elements.GetName(1).Should().Be("/Fit");
    }

    [Fact]
    public void AFitRectangleMovesAllFourOfItsNumbers()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target, new PdfName("/FitR"),
            new PdfReal(100), new PdfReal(200), new PdfReal(300), new PdfReal(400));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        new[]
        {
            destination.Elements.GetReal(2), destination.Elements.GetReal(3),
            destination.Elements.GetReal(4), destination.Elements.GetReal(5)
        }.Should().Equal(50, 100, 150, 200);
    }

    [Fact]
    public void AFitHorizontalMovesItsLine()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target, new PdfName("/FitH"), new PdfReal(700));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void AFitHorizontalBecomesAFitVerticalWhenThePageIsTurned()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target, new PdfName("/FitH"), new PdfReal(700));
        LinkOn(fixture.Source, "/Dest", destination);

        var options = PageResizeOptions.Default;
        options.AutoRotate = true;
        fixture.Target.Resize(PageSize.A4, PageOrientation.Landscape, options);

        destination.Elements.GetName(1).Should().Be("/FitV",
            "a horizontal line is a vertical one after a quarter turn, and the destination has " +
            "to change form to go on meaning the same thing");
        destination.Elements.GetReal(2).Should().BeApproximately(700, Tolerance);
    }

    [Fact]
    public void AGoToActionIsFollowed()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var action = new PdfDictionary(fixture.Document);
        action.Elements.SetName("/S", "/GoTo");
        action.Elements["/D"] = destination;
        LinkOn(fixture.Source, "/A", action);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
    }

    [Fact]
    public void ARemoteGoToIsLeftAlone()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var action = new PdfDictionary(fixture.Document);
        action.Elements.SetName("/S", "/GoToR");
        action.Elements["/D"] = destination;
        LinkOn(fixture.Source, "/A", action);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(100, Tolerance,
            "a remote destination names a page in another file and is none of this resize's business");
    }

    [Fact]
    public void AnOutlineEntryIsMoved()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var bookmark = new PdfDictionary(fixture.Document);
        bookmark.Elements.SetString("/Title", "Chapter one");
        bookmark.Elements["/Dest"] = destination;
        fixture.Document.Internals.AddObject(bookmark);

        var outlines = new PdfDictionary(fixture.Document);
        outlines.Elements.SetName("/Type", "/Outlines");
        outlines.Elements["/First"] = bookmark.Reference;
        fixture.Document.Internals.AddObject(outlines);
        fixture.Document.Internals.Catalog.Elements["/Outlines"] = outlines.Reference;

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void ADestinationHeldInTheNameTreeIsMoved()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var names = new PdfArray(fixture.Document);
        names.Elements.Add(new PdfString("chapter.1"));
        names.Elements.Add(destination);

        var dests = new PdfDictionary(fixture.Document) { Elements = { ["/Names"] = names } };

        var namesDictionary = new PdfDictionary(fixture.Document) { Elements = { ["/Dests"] = dests } };
        fixture.Document.Internals.Catalog.Elements["/Names"] = namesDictionary;

        // The link names where it goes; the tree holds what the name stands for.
        LinkOn(fixture.Source, "/Dest", new PdfString("chapter.1"));

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    /// <summary>
    ///   A node whose /Kids name it twice doubles the walk at every level, so a depth cap alone lets
    ///   a few hundred bytes run to 2^32 visits: a hang rather than a failure. xUnit honours Timeout
    ///   only on an async test, hence the Task.Run.
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task ANameTreeWhoseKidsLeadBackToItselfDoesNotHangTheResize()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var names = new PdfArray(fixture.Document);
        names.Elements.Add(new PdfString("chapter.1"));
        names.Elements.Add(destination);

        var dests = new PdfDictionary(fixture.Document) { Elements = { ["/Names"] = names } };
        fixture.Document.Internals.AddObject(dests);
        var kids = new PdfArray(fixture.Document);
        kids.Elements.Add(dests.Reference);
        kids.Elements.Add(dests.Reference);
        dests.Elements["/Kids"] = kids;

        var namesDictionary = new PdfDictionary(fixture.Document) { Elements = { ["/Dests"] = dests.Reference } };
        fixture.Document.Internals.Catalog.Elements["/Names"] = namesDictionary;

        await Task.Run(() => HalveThePage(fixture.Target));

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance,
            "the destination is still moved, and moved once");
    }

    /// <summary>
    ///   The outline's twin of the case above: an item that is its own first child and its own next
    ///   sibling. The sibling cap bounds one level and the depth cap bounds the levels, but together
    ///   they allow 100,000^32 visits.
    /// </summary>
    [Fact(Timeout = 30000)]
    public async Task AnOutlineThatLeadsBackToItselfDoesNotHangTheResize()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var item = new PdfDictionary(fixture.Document) { Elements = { ["/Dest"] = destination } };
        fixture.Document.Internals.AddObject(item);
        item.Elements["/First"] = item.Reference;
        item.Elements["/Next"] = item.Reference;

        var outlines = new PdfDictionary(fixture.Document) { Elements = { ["/First"] = item.Reference } };
        fixture.Document.Internals.Catalog.Elements["/Outlines"] = outlines;

        await Task.Run(() => HalveThePage(fixture.Target));

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void ADestinationHeldInTheLegacyDestsDictionaryIsMoved()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        var dests = new PdfDictionary(fixture.Document) { Elements = { ["/chapter1"] = destination } };
        fixture.Document.Internals.Catalog.Elements["/Dests"] = dests;

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void TheOpenActionIsMoved()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        fixture.Document.Internals.Catalog.Elements["/OpenAction"] = destination;

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void ALinkToAPageThatWasNotResizedIsUntouched()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Source,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(100, Tolerance);
        destination.Elements.GetReal(3).Should().BeApproximately(700, Tolerance);
    }

    [Fact]
    public void ADestinationSharedByTwoLinksIsMovedOnceAndNotTwice()
    {
        var fixture = TwoPages();

        // One array, held indirectly, that two links both point at. Moving it once per link that
        // finds it would move it twice as far.
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        fixture.Document.Internals.AddObject(destination);

        LinkOn(fixture.Source, "/Dest", destination.Reference);
        LinkOn(fixture.Source, "/Dest", destination.Reference);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void TurningOffTheSweepLeavesEveryDestinationAlone()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        options.ScaleDestinations = false;
        fixture.Target.Resize(new XSize(A4Width / 2, A4Height / 2), options);

        destination.Elements.GetReal(2).Should().BeApproximately(100, Tolerance);
    }

    [Fact]
    public void ResizingEveryPageMovesEveryDestinationExactlyOnce()
    {
        var fixture = TwoPages();
        var toFirst = Destination(fixture.Source,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        var toSecond = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(200), new PdfReal(600), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", toFirst);
        LinkOn(fixture.Target, "/Dest", toSecond);

        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        fixture.Document.ResizePages(new XSize(A4Width / 2, A4Height / 2), options);

        toFirst.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
        toFirst.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
        toSecond.Elements.GetReal(2).Should().BeApproximately(100, Tolerance);
        toSecond.Elements.GetReal(3).Should().BeApproximately(300, Tolerance);
    }

    [Fact]
    public void ADestinationCoordinateHeldIndirectlyIsStillMoved()
    {
        // A destination coordinate is as entitled to be an indirect object as anything else.
        // Reading one with GetReal throws instead of following the reference, which used to
        // abort the resize after the content had already been wrapped.
        var fixture = TwoPages();

        var indirect = new PdfRealObject(fixture.Document, 700);
        fixture.Document.Internals.AddObject(indirect);

        var destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), indirect.Reference,
            new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void ADestinationWhoseCoordinatesAreNotNumbersIsLeftAlone()
    {
        var fixture = TwoPages();
        var destination = Destination(fixture.Target, new PdfName("/FitR"),
            new PdfReal(100), new PdfReal(200), new PdfName("/Nonsense"), new PdfReal(400));
        LinkOn(fixture.Source, "/Dest", destination);

        var act = () => HalveThePage(fixture.Target);

        act.Should().NotThrow();
        destination.Elements.GetReal(2).Should().Be(100, "none of it moves if not all of it can");
        destination.Elements.GetReal(3).Should().Be(200);
    }
}
