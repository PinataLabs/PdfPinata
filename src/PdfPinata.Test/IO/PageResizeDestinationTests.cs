using System;
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
    const double A4Width = 595;
    const double A4Height = 842;
    const double Tolerance = 0.01;

    sealed class Fixture
    {
        internal PdfDocument Document;
        internal PdfPage Source;
        internal PdfPage Target;
    }

    /// <summary>Two A4 pages, both drawn on; links are hung off the first, pointing at the second.</summary>
    static Fixture TwoPages()
    {
        PdfDocument document = new PdfDocument();
        PdfPage first = document.AddPage();
        PdfPage second = document.AddPage();

        foreach (PdfPage page in new[] { first, second })
        {
            page.Size = PageSize.A4;
            using XGraphics gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));
        }

        return new Fixture { Document = document, Source = first, Target = second };
    }

    static void HalveThePage(PdfPage page)
    {
        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(new XSize(A4Width / 2, A4Height / 2), options);
    }

    static PdfArray Destination(PdfPage target, params PdfItem[] rest)
    {
        PdfArray destination = new PdfArray(target.Owner);
        destination.Elements.Add(target.Reference);
        foreach (PdfItem item in rest)
            destination.Elements.Add(item);
        return destination;
    }

    /// <summary>Hangs a link annotation carrying the destination off the page.</summary>
    static PdfDictionary LinkOn(PdfPage page, string key, PdfItem destination)
    {
        PdfDictionary link = new PdfDictionary(page.Owner);
        link.Elements.SetName("/Type", "/Annot");
        link.Elements.SetName("/Subtype", "/Link");
        link.Elements.SetRectangle("/Rect", new PdfRectangle(new XPoint(0, 0), new XPoint(10, 10)));
        link.Elements[key] = destination;
        page.Owner.Internals.AddObject(link);

        PdfArray annotations = page.Elements.GetArray("/Annots");
        if (annotations == null)
        {
            annotations = new PdfArray(page.Owner);
            page.Elements["/Annots"] = annotations;
        }
        annotations.Elements.Add(link.Reference);

        return link;
    }

    [Fact]
    public void ALinkFromAnotherPageFollowsTheContentItPointedAt()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfReal(1));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void TheZoomOfAnXyzDestinationIsNotTouchedWhenThePageShrinks()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfReal(1));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(4).Should().BeApproximately(1, Tolerance,
            "the zoom is a magnification the reader asked for, not a promise about text size");
    }

    [Fact]
    public void TheZoomIsNotTouchedWhenThePageGrowsEither()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfReal(1));
        LinkOn(fixture.Source, "/Dest", destination);

        PageResizeOptions options = PageResizeOptions.Default;
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
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(4).Should().Be(0, "zero means the reader keeps its own zoom");
    }

    [Fact]
    public void ANullCoordinateIsLeftAlone()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), PdfNull.Value, new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements[2].Should().Be(PdfNull.Value);
        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void AFitDestinationHasNothingToMove()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target, new PdfName("/Fit"));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.Count.Should().Be(2);
        destination.Elements.GetName(1).Should().Be("/Fit");
    }

    [Fact]
    public void AFitRectangleMovesAllFourOfItsNumbers()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target, new PdfName("/FitR"),
            new PdfReal(100), new PdfReal(200), new PdfReal(300), new PdfReal(400));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        new[]
        {
            destination.Elements.GetReal(2), destination.Elements.GetReal(3),
            destination.Elements.GetReal(4), destination.Elements.GetReal(5),
        }.Should().Equal(new double[] { 50, 100, 150, 200 });
    }

    [Fact]
    public void AFitHorizontalMovesItsLine()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target, new PdfName("/FitH"), new PdfReal(700));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void AFitHorizontalBecomesAFitVerticalWhenThePageIsTurned()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target, new PdfName("/FitH"), new PdfReal(700));
        LinkOn(fixture.Source, "/Dest", destination);

        PageResizeOptions options = PageResizeOptions.Default;
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
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        PdfDictionary action = new PdfDictionary(fixture.Document);
        action.Elements.SetName("/S", "/GoTo");
        action.Elements["/D"] = destination;
        LinkOn(fixture.Source, "/A", action);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(50, Tolerance);
    }

    [Fact]
    public void ARemoteGoToIsLeftAlone()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        PdfDictionary action = new PdfDictionary(fixture.Document);
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
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        PdfDictionary bookmark = new PdfDictionary(fixture.Document);
        bookmark.Elements.SetString("/Title", "Chapter one");
        bookmark.Elements["/Dest"] = destination;
        fixture.Document.Internals.AddObject(bookmark);

        PdfDictionary outlines = new PdfDictionary(fixture.Document);
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
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        PdfArray names = new PdfArray(fixture.Document);
        names.Elements.Add(new PdfString("chapter.1"));
        names.Elements.Add(destination);

        PdfDictionary dests = new PdfDictionary(fixture.Document);
        dests.Elements["/Names"] = names;

        PdfDictionary namesDictionary = new PdfDictionary(fixture.Document);
        namesDictionary.Elements["/Dests"] = dests;
        fixture.Document.Internals.Catalog.Elements["/Names"] = namesDictionary;

        // The link names where it goes; the tree holds what the name stands for.
        LinkOn(fixture.Source, "/Dest", new PdfString("chapter.1"));

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void ADestinationHeldInTheLegacyDestsDictionaryIsMoved()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));

        PdfDictionary dests = new PdfDictionary(fixture.Document);
        dests.Elements["/chapter1"] = destination;
        fixture.Document.Internals.Catalog.Elements["/Dests"] = dests;

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void TheOpenActionIsMoved()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        fixture.Document.Internals.Catalog.Elements["/OpenAction"] = destination;

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(3).Should().BeApproximately(350, Tolerance);
    }

    [Fact]
    public void ALinkToAPageThatWasNotResizedIsUntouched()
    {
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Source,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        HalveThePage(fixture.Target);

        destination.Elements.GetReal(2).Should().BeApproximately(100, Tolerance);
        destination.Elements.GetReal(3).Should().BeApproximately(700, Tolerance);
    }

    [Fact]
    public void ADestinationSharedByTwoLinksIsMovedOnceAndNotTwice()
    {
        Fixture fixture = TwoPages();

        // One array, held indirectly, that two links both point at. Moving it once per link that
        // finds it would move it twice as far.
        PdfArray destination = Destination(fixture.Target,
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
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", destination);

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        options.ScaleDestinations = false;
        fixture.Target.Resize(new XSize(A4Width / 2, A4Height / 2), options);

        destination.Elements.GetReal(2).Should().BeApproximately(100, Tolerance);
    }

    [Fact]
    public void ResizingEveryPageMovesEveryDestinationExactlyOnce()
    {
        Fixture fixture = TwoPages();
        PdfArray toFirst = Destination(fixture.Source,
            new PdfName("/XYZ"), new PdfReal(100), new PdfReal(700), new PdfPinata.Pdf.PdfInteger(0));
        PdfArray toSecond = Destination(fixture.Target,
            new PdfName("/XYZ"), new PdfReal(200), new PdfReal(600), new PdfPinata.Pdf.PdfInteger(0));
        LinkOn(fixture.Source, "/Dest", toFirst);
        LinkOn(fixture.Target, "/Dest", toSecond);

        PageResizeOptions options = PageResizeOptions.Default;
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
        Fixture fixture = TwoPages();

        PdfRealObject indirect = new PdfRealObject(fixture.Document, 700);
        fixture.Document.Internals.AddObject(indirect);

        PdfArray destination = Destination(fixture.Target,
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
        Fixture fixture = TwoPages();
        PdfArray destination = Destination(fixture.Target, new PdfName("/FitR"),
            new PdfReal(100), new PdfReal(200), new PdfName("/Nonsense"), new PdfReal(400));
        LinkOn(fixture.Source, "/Dest", destination);

        Action act = () => HalveThePage(fixture.Target);

        act.Should().NotThrow();
        destination.Elements.GetReal(2).Should().Be(100, "none of it moves if not all of it can");
        destination.Elements.GetReal(3).Should().Be(200);
    }
}
