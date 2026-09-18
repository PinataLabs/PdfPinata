using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   Resizing a page has to move what is drawn on it into the new size rather than crop it. The
///   page is drawn on edge to edge, resized, and the content walked to find where the drawing
///   really ended up - which is the only way to tell a page that was scaled from one that was
///   merely given a smaller box.
/// </summary>
public class PageResizeTests
{
    const double A4Width = 595;
    const double A4Height = 842;
    const double A5Width = 420;
    const double A5Height = 595;

    const double Tolerance = 0.01;

    /// <summary>
    ///   The private key PdfPinata marks a resize wrapper with. Spelled out rather than
    ///   referred to, because it is internal to the library and the test assembly cannot see it.
    /// </summary>
    const string ResizeWrapperKey = "/PdfPinataResizeWrapper";

    /// <summary>
    ///   A page of the size given with a rectangle drawn over the whole of it, so that where the
    ///   content went afterwards can be read off.
    /// </summary>
    static PdfDocument DocumentWithAFilledPage(PageSize size = PageSize.A4,
        PageOrientation orientation = PageOrientation.Portrait)
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Size = size;
        page.Orientation = orientation;

        using (XGraphics gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));

        return document;
    }

    /// <summary>
    ///   Writes the document out and reads it back, so that the page arrives the way an imported
    ///   one does rather than as the object that was just built.
    /// </summary>
    static PdfDocument RoundTripped(PdfDocument document)
    {
        using MemoryStream stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    static void ShouldBeAbout(XRect actual, double x, double y, double width, double height)
    {
        actual.X.Should().BeApproximately(x, Tolerance);
        actual.Y.Should().BeApproximately(y, Tolerance);
        actual.Width.Should().BeApproximately(width, Tolerance);
        actual.Height.Should().BeApproximately(height, Tolerance);
    }

    // ----------------------------------------------------------------- 8.1 the content moves

    [Fact]
    public void AnA4PageShrunkToA5StillDrawsAllOfItself()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A5);

        page.Width.Point.Should().BeApproximately(A5Width, Tolerance);
        page.Height.Point.Should().BeApproximately(A5Height, Tolerance);

        double scale = Math.Min(A5Width / A4Width, A5Height / A4Height);
        double slack = (A5Height - A4Height * scale) / 2;

        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, slack, A5Width, A4Height * scale);
    }

    [Fact]
    public void AnA5PageGrownToA4StillDrawsAllOfItself()
    {
        PdfDocument document = DocumentWithAFilledPage(PageSize.A5);
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A4);

        double scale = Math.Min(A4Width / A5Width, A4Height / A5Height);
        double slack = (A4Width - A5Width * scale) / 2;

        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), slack, 0, A5Width * scale, A4Height);
    }

    [Fact]
    public void StretchPutsTheContentExactlyOnTheNewPage()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(PageSize.A5, PageOrientation.Portrait, options);

        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, 0, A5Width, A5Height);
    }

    [Fact]
    public void FillCoversTheNewPageAndLetsTheRestHangOff()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Fill;
        page.Resize(PageSize.A5, PageOrientation.Portrait, options);

        XRect bounds = ResizedContentProbe.DrawnBounds(page);
        bounds.Width.Should().BeGreaterThanOrEqualTo(A5Width - Tolerance);
        bounds.Height.Should().BeGreaterThanOrEqualTo(A5Height - Tolerance);
    }

    [Fact]
    public void CropKeepsTheContentAtItsOwnSizeAgainstTheTopLeft()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A5, PageOrientation.Portrait, PageResizeOptions.Crop);

        XRect bounds = ResizedContentProbe.DrawnBounds(page);

        bounds.Width.Should().BeApproximately(A4Width, Tolerance, "cropping does not scale");
        bounds.Height.Should().BeApproximately(A4Height, Tolerance);

        // Against the top of the A5 page, so it is the foot of the A4 page that hangs off the
        // bottom - the opposite of what the Size setter used to do.
        (bounds.Y + bounds.Height).Should().BeApproximately(A5Height, Tolerance);
        bounds.Y.Should().BeApproximately(A5Height - A4Height, Tolerance);
    }

    [Fact]
    public void AMarginLeavesABorderRoundTheContent()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        // Stretched, so that the margin is the only thing between the content and the edge.
        // Fitting instead would leave slack of its own on one axis - an A4 page inset by 20 on
        // every side is no longer A4 shaped - and the assertion would be about the slack rather
        // than about the margin.
        PageResizeOptions options = PageResizeOptions.Default;
        options.Margin = 20;
        options.Fit = PageFitMode.Stretch;
        page.Resize(PageSize.A4, PageOrientation.Portrait, options);

        XRect bounds = ResizedContentProbe.DrawnBounds(page);
        bounds.X.Should().BeApproximately(20, Tolerance);
        bounds.Y.Should().BeApproximately(20, Tolerance);
        (bounds.X + bounds.Width).Should().BeApproximately(A4Width - 20, Tolerance);
        (bounds.Y + bounds.Height).Should().BeApproximately(A4Height - 20, Tolerance);
    }

    [Fact]
    public void AMarginIsTakenOffBeforeTheContentIsFitted()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PageResizeOptions options = PageResizeOptions.Default;
        options.Margin = 20;
        page.Resize(PageSize.A4, PageOrientation.Portrait, options);

        // Fitting an A4 page into an A4 page inset by 20 leaves it a little slack vertically,
        // because the inset box is not quite the shape the page is. What matters is that nothing
        // reaches into the margin.
        XRect bounds = ResizedContentProbe.DrawnBounds(page);
        bounds.X.Should().BeGreaterThanOrEqualTo(20 - Tolerance);
        bounds.Y.Should().BeGreaterThanOrEqualTo(20 - Tolerance);
        (bounds.X + bounds.Width).Should().BeLessThanOrEqualTo(A4Width - 20 + Tolerance);
        (bounds.Y + bounds.Height).Should().BeLessThanOrEqualTo(A4Height - 20 + Tolerance);
    }

    [Fact]
    public void APageIsResizedByItsCropBoxWhenItHasOne()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        // The bottom left quarter of the page is all the reader is shown, so that is what "the
        // page" means and what has to end up filling the new one.
        page.CropBox = new PdfRectangle(new XPoint(0, 0), new XPoint(A4Width / 2, A4Height / 2));

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(PageSize.A5, PageOrientation.Portrait, options);

        // The content was twice the crop box in each direction, so stretching the crop box onto
        // the A5 page leaves the drawing twice the size of that page, hanging off the top right.
        XRect bounds = ResizedContentProbe.DrawnBounds(page);
        bounds.X.Should().BeApproximately(0, Tolerance);
        bounds.Y.Should().BeApproximately(0, Tolerance);
        bounds.Width.Should().BeApproximately(A5Width * 2, Tolerance);
        bounds.Height.Should().BeApproximately(A5Height * 2, Tolerance);
    }

    [Fact]
    public void APageTurnedByAQuarterKeepsItsRotateEntryAndReportsTheSizeTheReaderSees()
    {
        PdfDocument document = DocumentWithAFilledPage();
        document.Pages[0].Rotate = 90;
        PdfDocument reopened = RoundTripped(document);
        PdfPage page = reopened.Pages[0];

        page.Resize(PageSize.A5);

        page.Rotate.Should().Be(90, "the turn belongs to the reader and the resize took it into account");
        page.Width.Point.Should().BeApproximately(A5Width, Tolerance);
        page.Height.Point.Should().BeApproximately(A5Height, Tolerance);

        // Stored across the way it is shown, so the media box holds the two the other way round.
        page.MediaBox.Width.Should().BeApproximately(A5Height, Tolerance);
        page.MediaBox.Height.Should().BeApproximately(A5Width, Tolerance);
    }

    [Fact]
    public void APageWhoseMediaBoxIsAwayFromTheOriginIsBroughtOntoTheNewPage()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];
        page.MediaBox = new PdfRectangle(new XPoint(20, 30), new XPoint(20 + A4Width, 30 + A4Height));

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(PageSize.A5, PageOrientation.Portrait, options);

        page.MediaBox.X1.Should().BeApproximately(0, Tolerance);
        page.MediaBox.Y1.Should().BeApproximately(0, Tolerance);
    }

    [Fact]
    public void AutoRotateTurnsAPortraitPageIntoALandscapeOneRatherThanShrinkingIt()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PageResizeOptions options = PageResizeOptions.Default;
        options.AutoRotate = true;
        page.Resize(PageSize.A4, PageOrientation.Landscape, options);

        page.Width.Point.Should().BeApproximately(A4Height, Tolerance);
        page.Height.Point.Should().BeApproximately(A4Width, Tolerance);

        // Turned rather than letterboxed, so it fills the landscape page completely.
        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, 0, A4Height, A4Width);
    }

    [Fact]
    public void WithoutAutoRotateTheSamePageIsShrunkAndLeftWithSlackDownTheSides()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A4, PageOrientation.Landscape);

        XRect bounds = ResizedContentProbe.DrawnBounds(page);
        bounds.Width.Should().BeLessThan(A4Height - 1, "there is slack at the left and right");
        bounds.Height.Should().BeApproximately(A4Width, Tolerance);
    }

    // --------------------------------------------------------- 8.2 the graphics state survives

    [Fact]
    public void ContentThatLeavesAQUnmatchedIsStillScaledAllTheWayThrough()
    {
        // The reason the content is moved into a form rather than given a cm in front of it. A
        // form is a graphics state of its own, so an unbalanced q inside it cannot swallow the Q
        // that would have closed the resize transform.
        PdfDocument document = DocumentWithUnbalancedContent("q 1 0 0 1 0 0 cm 0 0 100 100 re f");
        PdfPage page = document.Pages[0];

        page.Resize(new XSize(A4Width / 2, A4Height / 2),
            new PageResizeOptions { Fit = PageFitMode.Stretch });

        // Half the size in each direction, transform intact.
        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, 0, 50, 50);
    }

    [Fact]
    public void ContentWithOneQTooManyIsStillScaled()
    {
        PdfDocument document = DocumentWithUnbalancedContent("0 0 100 100 re f Q Q");
        PdfPage page = document.Pages[0];

        page.Resize(new XSize(A4Width / 2, A4Height / 2),
            new PageResizeOptions { Fit = PageFitMode.Stretch });

        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, 0, 50, 50);
    }

    /// <summary>
    ///   A page whose content stream is exactly the bytes given, however unbalanced.
    /// </summary>
    static PdfDocument DocumentWithUnbalancedContent(string content)
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(A4Width, A4Height));

        PdfContent stream = new PdfContent(document);
        stream.CreateStream(System.Text.Encoding.ASCII.GetBytes(content));
        document.Internals.AddObject(stream);
        page.Elements["/Contents"] = stream.Reference;

        return document;
    }

    [Fact]
    public void ATransparencyGroupTravelsWithTheContent()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PdfDictionary group = new PdfDictionary(document);
        group.Elements.SetName("/S", "/Transparency");
        group.Elements.SetName("/CS", "/DeviceRGB");
        page.Elements["/Group"] = group;

        page.Resize(PageSize.A5);

        PdfDictionary form = TheWrapperOf(page);
        form.Elements["/Group"].Should().NotBeNull(
            "a group left on the page would no longer wrap the content that needed it");
    }

    [Fact]
    public void CompressedContentIsMovedWithoutBeingRecompressed()
    {
        PdfDocument document = DocumentWithAFilledPage();
        document.Options.CompressContentStreams = true;
        PdfDocument reopened = RoundTripped(document);
        PdfPage page = reopened.Pages[0];

        byte[] before = TheSingleContentStreamOf(page).Stream.Value;
        page.Resize(PageSize.A5);

        PdfDictionary form = TheWrapperOf(page);
        form.Elements.GetName("/Filter").Should().Be("/FlateDecode", "the filter came across with the bytes");
        form.Stream.Value.Should().Equal(before, "the bytes were moved, not decoded and encoded again");
    }

    [Fact]
    public void ResizingOnePageOfTwoThatShareResourcesLeavesTheOtherAlone()
    {
        PdfDocument document = new PdfDocument();

        PdfResources shared = new PdfResources(document);
        document.Internals.AddObject(shared);

        PdfPage first = document.AddPage();
        PdfPage second = document.AddPage();
        foreach (PdfPage page in new[] { first, second })
        {
            page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(A4Width, A4Height));
            page.Elements["/Resources"] = shared.Reference;

            PdfContent content = new PdfContent(document);
            content.CreateStream(System.Text.Encoding.ASCII.GetBytes("0 0 100 100 re f"));
            document.Internals.AddObject(content);
            page.Elements["/Contents"] = content.Reference;
        }

        XRect before = ResizedContentProbe.DrawnBounds(second);

        first.Resize(PageSize.A5);

        XRect after = ResizedContentProbe.DrawnBounds(second);
        after.Should().Be(before, "the other page's drawing must not move");

        second.Elements["/Resources"].Should().BeSameAs(shared.Reference,
            "the shared dictionary was handed to the form, not altered");
    }

    [Fact]
    public void APageAskedForItsResourcesBeforeTheResizeAnswersWithTheNewOnesAfterwards()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PdfResources before = page.Resources;

        page.Resize(PageSize.A5);

        page.Resources.Should().NotBeSameAs(before,
            "the page caches its resources, so replacing them has to clear the cache");
        page.Resources.Elements.GetDictionary("/XObject").Should().NotBeNull();
    }

    [Fact]
    public void APageAskedForItsContentBeforeTheResizeAnswersWithTheNewContentAfterwards()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        PdfContents before = page.Contents;

        page.Resize(PageSize.A5);

        page.Contents.Should().NotBeSameAs(before, "the page caches its content in the same way");
    }

    // ------------------------------------------------------------------- 8.6 doing it twice

    [Fact]
    public void ResizingToA5AndBackToA4LeavesThePageAsItStarted()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        XRect before = ResizedContentProbe.DrawnBounds(page);

        page.Resize(PageSize.A5);
        page.Resize(PageSize.A4);

        XRect after = ResizedContentProbe.DrawnBounds(page);
        ShouldBeAbout(after, before.X, before.Y, before.Width, before.Height);

        ResizedContentProbe.FormCount(page).Should().Be(1,
            "the second resize rewrote the transform rather than wrapping the wrapper");
    }

    [Fact]
    public void ThreeResizesLeaveOneWrapper()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A5);
        page.Resize(PageSize.A6);
        page.Resize(PageSize.A4);

        ResizedContentProbe.FormCount(page).Should().Be(1);
    }

    [Fact]
    public void DrawingBetweenTwoResizesStillGivesTheRightAnswer()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A5);

        // Now the page is no longer just a wrapper, so the second resize has to wrap again
        // rather than rewrite. Either way the answer has to be right.
        using (XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append))
            gfx.DrawRectangle(XBrushes.Red, new XRect(0, 0, 10, 10));

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(PageSize.A4, PageOrientation.Portrait, options);

        // Stretching maps the A5 page onto the A4 one, and what was drawn on the A5 page did not
        // quite fill it - the first resize left it a third of a point of slack top and bottom -
        // so the drawing comes back very nearly, but not exactly, edge to edge.
        XRect bounds = ResizedContentProbe.DrawnBounds(page);
        bounds.X.Should().BeApproximately(0, 1);
        bounds.Y.Should().BeApproximately(0, 1);
        (bounds.X + bounds.Width).Should().BeApproximately(A4Width, 1);
        (bounds.Y + bounds.Height).Should().BeApproximately(A4Height, 1);

        page.Width.Point.Should().BeApproximately(A4Width, Tolerance);
    }

    // -------------------------------------------------------------------- 8.7 what is refused

    [Fact]
    public void SettingTheSizeOfABlankPageStillWorks()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        page.Size = PageSize.A4;

        page.Width.Point.Should().BeApproximately(A4Width, Tolerance);
    }

    [Fact]
    public void SettingTheSizeOfAPageWithContentThrowsAndNamesResize()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];
        PdfRectangle before = page.MediaBox;

        Action act = () => page.Size = PageSize.A5;

        act.Should().Throw<InvalidOperationException>().WithMessage("*Resize*");
        page.MediaBox.Width.Should().Be(before.Width);
    }

    [Fact]
    public void SettingTheWidthOrHeightOfAPageWithContentThrows()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        ((Action)(() => page.Width = 100)).Should().Throw<InvalidOperationException>().WithMessage("*Resize*");
        ((Action)(() => page.Height = 100)).Should().Throw<InvalidOperationException>().WithMessage("*Resize*");
    }

    [Fact]
    public void AskingWhetherThePageHasContentDoesNotDisturbIt()
    {
        PdfDocument document = DocumentWithUnbalancedContent("0 0 100 100 re f");
        PdfPage page = document.Pages[0];

        try
        {
            page.Size = PageSize.A5;
        }
        catch (InvalidOperationException)
        {
            // Expected. What matters is what the failed attempt left behind.
        }

        page.Elements["/Contents"].Should().BeOfType<PdfReference>(
            "the test for content must not rewrite /Contents into an array");
    }

    [Fact]
    public void AnImportedPageIsRefusedTheSizeSetterToo()
    {
        PdfDocument reopened = RoundTripped(DocumentWithAFilledPage());

        Action act = () => reopened.Pages[0].Size = PageSize.A5;

        act.Should().Throw<InvalidOperationException>().WithMessage("*Resize*");
    }

    [Fact]
    public void AReadOnlyDocumentIsRefused()
    {
        using MemoryStream stream = new MemoryStream();
        DocumentWithAFilledPage().Save(stream, false);
        stream.Position = 0;

        PdfDocument readOnly = PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);

        Action act = () => readOnly.Pages[0].Resize(PageSize.A5);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ATaggedDocumentIsRefusedAndLeftAlone()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfDictionary structure = new PdfDictionary(document);
        structure.Elements.SetName("/Type", "/StructTreeRoot");
        document.Internals.AddObject(structure);
        document.Internals.Catalog.Elements["/StructTreeRoot"] = structure.Reference;

        PdfPage page = document.Pages[0];
        XRect before = ResizedContentProbe.DrawnBounds(page);

        Action act = () => page.Resize(PageSize.A5);

        act.Should().Throw<InvalidOperationException>().WithMessage("*tagged*");
        ResizedContentProbe.DrawnBounds(page).Should().Be(before, "nothing may have been touched");
    }

    [Fact]
    public void ASignedDocumentIsRefused()
    {
        PdfDocument document = DocumentWithAFilledPage();

        PdfDictionary field = new PdfDictionary(document);
        field.Elements.SetName("/FT", "/Sig");
        document.Internals.AddObject(field);

        PdfArray fields = new PdfArray(document);
        fields.Elements.Add(field.Reference);

        PdfDictionary acroForm = new PdfDictionary(document);
        acroForm.Elements["/Fields"] = fields;
        document.Internals.AddObject(acroForm);
        document.Internals.Catalog.Elements["/AcroForm"] = acroForm.Reference;

        Action act = () => document.Pages[0].Resize(PageSize.A5);

        act.Should().Throw<InvalidOperationException>().WithMessage("*signed*");
    }

    [Fact]
    public void ARefusedResizePagesLeavesEveryPageUntouched()
    {
        PdfDocument document = DocumentWithAFilledPage();
        document.AddPage().Size = PageSize.A4;

        PdfDictionary structure = new PdfDictionary(document);
        structure.Elements.SetName("/Type", "/StructTreeRoot");
        document.Internals.AddObject(structure);
        document.Internals.Catalog.Elements["/StructTreeRoot"] = structure.Reference;

        XRect before = ResizedContentProbe.DrawnBounds(document.Pages[0]);

        Action act = () => document.ResizePages(PageSize.A5);

        act.Should().Throw<InvalidOperationException>();
        ResizedContentProbe.DrawnBounds(document.Pages[0]).Should().Be(before,
            "the check has to come before the first page is touched");
    }

    [Fact]
    public void ResizingWithAnOpenXGraphicsIsRefused()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

        Action act = () => page.Resize(PageSize.A5);

        act.Should().Throw<InvalidOperationException>().WithMessage("*XGraphics*");
    }

    // ------------------------------------------------------------------ resizing every page

    [Fact]
    public void ResizePagesBringsEveryPageToTheSameSize()
    {
        PdfDocument document = new PdfDocument();
        foreach (PageSize size in new[] { PageSize.A3, PageSize.A5, PageSize.Letter })
        {
            PdfPage page = document.AddPage();
            page.Size = size;
            using XGraphics gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, page.Width, page.Height));
        }

        document.ResizePages(PageSize.A4);

        foreach (PdfPage page in document.Pages)
        {
            page.Width.Point.Should().BeApproximately(A4Width, Tolerance);
            page.Height.Point.Should().BeApproximately(A4Height, Tolerance);
        }
    }

    [Fact]
    public void AResizedDocumentCanBeSavedAndReadBack()
    {
        PdfDocument document = DocumentWithAFilledPage();
        document.Pages[0].Resize(PageSize.A5);

        PdfDocument reopened = RoundTripped(document);
        PdfPage page = reopened.Pages[0];

        page.Width.Point.Should().BeApproximately(A5Width, Tolerance);

        double scale = Math.Min(A5Width / A4Width, A5Height / A4Height);
        double slack = (A5Height - A4Height * scale) / 2;
        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, slack, A5Width, A4Height * scale);
    }

    static PdfDictionary TheWrapperOf(PdfPage page)
    {
        PdfDictionary xObjects = page.Resources.Elements.GetDictionary("/XObject");
        xObjects.Should().NotBeNull();

        foreach (PdfName name in xObjects.Elements.KeyNames)
        {
            if (xObjects.Elements.GetDictionary(name.Value) is { } form &&
                form.Elements.GetBoolean(ResizeWrapperKey))
                return form;
        }

        throw new InvalidOperationException("The page carries no resize wrapper.");
    }

    static PdfDictionary TheSingleContentStreamOf(PdfPage page)
    {
        PdfItem item = page.Elements["/Contents"];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfArray array)
        {
            array.Elements.Count.Should().Be(1);
            item = array.Elements[0];
            if (item is PdfReference elementReference)
                item = elementReference.Value;
        }

        return (PdfDictionary)item;
    }

    [Fact]
    public void TheWrapperContentIsFarShorterThanTheCapThatSkipsDecodingIt()
    {
        // Finding out whether a page is already a wrapper must not decode a whole content
        // stream, so the resizer measures the stored bytes first and gives up on anything too
        // long to be one. That cap is only safe while the content it writes stays short. If the
        // format ever grows past it, wrapper detection stops working - silently, because the
        // fallback is to wrap again, which is correct and merely wasteful.
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];

        page.Resize(PageSize.A5);

        int written = TheSingleContentStreamOf(page).Stream.Value.Length;

        written.Should().BeLessThan(512,
            "the resizer skips decoding any content stream longer than 1024 bytes, so a wrapper " +
            "has to stay well inside that");
    }

    [Fact]
    public void APageWithALongContentStreamIsStillResizedCorrectly()
    {
        // The other side of the cap: ordinary content is longer than any wrapper and must be
        // wrapped rather than mistaken for one.
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();
        page.Size = PageSize.A4;

        using (XGraphics gfx = XGraphics.FromPdfPage(page))
        {
            for (int index = 0; index < 200; index++)
                gfx.DrawRectangle(XBrushes.LightGray, new XRect(index, index, 10, 10));
        }

        TheSingleContentStreamOf(page).Stream.Value.Length.Should().BeGreaterThan(1024);

        page.Resize(PageSize.A5, PageOrientation.Portrait,
            new PageResizeOptions { Fit = PageFitMode.Stretch });

        page.Width.Point.Should().BeApproximately(A5Width, Tolerance);
        ResizedContentProbe.FormCount(page).Should().Be(1);
    }

    // --------------------------------------------- what counts as a page having content

    [Fact]
    public void APageOpenedForDrawingButNeverDrawnOnCanStillHaveItsSizeSet()
    {
        // XGraphics appends a content stream before anything is drawn, so the /Contents array is
        // not empty even though the page is. Counting the entries rather than looking in them
        // would refuse a page that is blank.
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        using (XGraphics unused = XGraphics.FromPdfPage(page))
        {
            // Nothing drawn.
        }

        page.Size = PageSize.A5;

        page.Width.Point.Should().BeApproximately(A5Width, Tolerance);
    }

    [Fact]
    public void ASizeSetterIsRefusedOnceSomethingHasBeenDrawn()
    {
        PdfDocument document = new PdfDocument();
        PdfPage page = document.AddPage();

        using (XGraphics gfx = XGraphics.FromPdfPage(page))
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(0, 0, 10, 10));

        ((Action)(() => page.Size = PageSize.A5)).Should().Throw<InvalidOperationException>();
    }

    // ------------------------------------------------------------------- page boxes

    [Fact]
    public void ACropBoxReachingOutsideTheMediaBoxIsTakenAsThePartInside()
    {
        // A crop box is not allowed outside the media box, and a reader takes the intersection.
        // Resizing from the whole of an oversized crop box would make everything come out small.
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];
        page.CropBox = new PdfRectangle(new XPoint(0, 0), new XPoint(A4Width * 2, A4Height * 2));

        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;
        page.Resize(PageSize.A5, PageOrientation.Portrait, options);

        // Intersected back to the media box, so the whole A4 page stretches onto the A5 one.
        ShouldBeAbout(ResizedContentProbe.DrawnBounds(page), 0, 0, A5Width, A5Height);
    }

    [Fact]
    public void TheOtherBoxesAreKeptInsideTheNewMediaBox()
    {
        PdfDocument document = DocumentWithAFilledPage();
        PdfPage page = document.Pages[0];
        page.CropBox = new PdfRectangle(new XPoint(0, 0), new XPoint(A4Width, A4Height));

        // Fill overflows the new page on purpose, and a crop box that travelled with the content
        // would overflow with it, leaving a page that is not well formed.
        PageResizeOptions options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Fill;
        page.Resize(PageSize.A5, PageOrientation.Portrait, options);

        PdfRectangle media = page.MediaBox;
        PdfRectangle crop = page.CropBox;

        crop.X1.Should().BeGreaterThanOrEqualTo(media.X1 - Tolerance);
        crop.Y1.Should().BeGreaterThanOrEqualTo(media.Y1 - Tolerance);
        crop.X2.Should().BeLessThanOrEqualTo(media.X2 + Tolerance);
        crop.Y2.Should().BeLessThanOrEqualTo(media.Y2 + Tolerance);
    }

    // ------------------------------------------------------------------- argument checking

    [Fact]
    public void AnOrientationThatIsNotOneIsRefusedRatherThanTakenForPortrait()
    {
        PdfDocument document = DocumentWithAFilledPage();

        Action act = () => document.Pages[0].Resize(PageSize.A5, (PageOrientation)42);

        act.Should().Throw<System.ComponentModel.InvalidEnumArgumentException>();
    }

    [Fact]
    public void AnEncryptedDocumentIsOnlyRefusedOnceItIsActuallyEncrypted()
    {
        // A password set on a document that has not been saved yet is a setting for the save,
        // not a statement that the document is encrypted now. Refusing it would block a perfectly
        // ordinary "build it, resize it, save it encrypted" sequence.
        PdfDocument document = DocumentWithAFilledPage();
        document.SecuritySettings.UserPassword = "secret";

        Action act = () => document.Pages[0].Resize(PageSize.A5);

        act.Should().NotThrow();
    }

    [Fact]
    public void ADocumentReadBackFromAnEncryptedFileIsRefused()
    {
        PdfDocument document = DocumentWithAFilledPage();
        document.SecuritySettings.UserPassword = "secret";

        using MemoryStream stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;

        PdfDocument encrypted = PdfPinata.Pdf.IO.PdfReader.Open(
            stream, "secret", PdfDocumentOpenMode.Modify);

        Action act = () => encrypted.Pages[0].Resize(PageSize.A5);

        act.Should().Throw<InvalidOperationException>().WithMessage("*encrypted*");
    }
}
