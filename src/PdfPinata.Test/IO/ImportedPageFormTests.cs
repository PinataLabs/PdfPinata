using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.

namespace PdfPinata.Test.IO;

/// <summary>
///   <see cref="XPdfForm"/> is how a page of somebody else's document is drawn onto a page of this
///   one. It is an <see cref="XImage"/> that happens to be a page, so it is placed with
///   <c>DrawImage</c> like any other - and everything interesting about it is what it does before
///   that: find the file, read it once, and answer the page's size so the caller can place it.
///   <para>
///   Reading it once is the part with a mechanism behind it. An imported document is cached per
///   thread against its full path, so a document drawn on ten pages is opened once rather than ten
///   times; a second form over the same file finds the first's document rather than re-reading it,
///   and a form that is disposed detaches it. That cache is the whole of
///   <c>PdfPinata.Pdf.Internal.ThreadLocalStorage</c>, which nothing else in the library reaches.
///   </para>
/// </summary>
public sealed class ImportedPageFormTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "PdfPinataImported" + Guid.NewGuid().ToString("N"));

    public ImportedPageFormTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
            // A file the reader still has open is not this test's business to insist on.
        }
    }

    /// <summary>
    ///   A document of the given page sizes, written to a file of its own so that it can be
    ///   imported by path - which is the route the whole per-thread cache exists for.
    /// </summary>
    private string AFileOf(params XSize[] pages)
    {
        var document = new PdfDocument();
        foreach (var size in pages)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(size.Width);
            page.Height = XUnit.FromPoint(size.Height);
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(XBrushes.Black, 10, 10, 20, 20);
        }

        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".pdf");
        document.Save(path);
        return path;
    }

    // ----- reading a page out of a file -------------------------------------------------------

    [Fact]
    public void AFormReadsThePageSizeOutOfTheDocumentItPointsAt()
    {
        var path = AFileOf(new XSize(300, 400), new XSize(500, 600));

        using var form = XPdfForm.FromFile(path);

        form.PageCount.Should().Be(2);
        form.PageNumber.Should().Be(1);
        form.PageIndex.Should().Be(0);
        form.PointWidth.Should().BeApproximately(300, 1e-6);
        form.PointHeight.Should().BeApproximately(400, 1e-6);
        form.Size.Width.Should().BeApproximately(300, 1e-6);
        form.PixelWidth.Should().Be(300);
        form.PixelHeight.Should().Be(400);
        form.Page.Should().NotBeNull();
    }

    [Fact]
    public void AFormCanBeTurnedToAnyPageOfTheDocumentItPointsAt()
    {
        var path = AFileOf(new XSize(300, 400), new XSize(500, 600));

        using var form = XPdfForm.FromFile(path);
        form.PageNumber = 2;

        form.PageIndex.Should().Be(1);
        form.PointWidth.Should().BeApproximately(500, 1e-6);

        form.PageIndex = 0;
        form.PageNumber.Should().Be(1);
    }

    /// <summary>
    ///   A page number written on the end of the path is how one is named without a second call -
    ///   <c>report.pdf#2</c> is the second page. The same notation the DOM's image helper reads,
    ///   and the two parse it with the same code for the same reason.
    /// </summary>
    [Fact]
    public void APageNumberOnTheEndOfThePathIsThePageTheFormOpensAt()
    {
        var path = AFileOf(new XSize(300, 400), new XSize(500, 600));

        using var form = XPdfForm.FromFile(path + "#2");

        form.PageNumber.Should().Be(2);
        form.PointWidth.Should().BeApproximately(500, 1e-6);
    }

    [Fact]
    public void AFormOverAFileThatIsNotThereSaysSoRatherThanDrawingNothing()
    {
        var missing = Path.Combine(_directory, "no-such-file.pdf");

        var opening = () => XPdfForm.FromFile(missing);

        opening.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void AFormOverAFileThatIsNotAPdfSaysSoBeforeItIsDrawn()
    {
        var notAPdf = Path.Combine(_directory, "not-a-pdf.pdf");
        File.WriteAllText(notAPdf, "this is not a PDF at all");

        var opening = () => XPdfForm.FromFile(notAPdf);

        opening.Should().Throw<ArgumentException>().WithMessage("*no valid PDF file header*");
    }

    [Fact]
    public void AFormOverAStreamThatIsNotAPdfSaysSoTheSameWay()
    {
        using var stream = new MemoryStream([1, 2, 3, 4, 5]);

        var opening = () => XPdfForm.FromStream(stream, null);

        opening.Should().Throw<ArgumentException>().WithMessage("*no valid PDF file header*");
    }

    // ----- the document behind it is read once ------------------------------------------------

    /// <summary>
    ///   Two forms over the same file share one imported document, because reading a document is
    ///   what importing costs and a page drawn on twenty pages would otherwise pay it twenty times.
    ///   The cache is keyed on the full path and lives on the thread.
    /// </summary>
    [Fact]
    public void TwoFormsOverOneFileShareTheDocumentBehindThem()
    {
        var path = AFileOf(new XSize(300, 400));

        using var first = XPdfForm.FromFile(path);
        using var second = XPdfForm.FromFile(path);

        first.Page.Owner.Should().BeSameAs(second.Page.Owner);
    }

    /// <summary>
    ///   Disposing detaches the document from the cache, and a form made afterwards reads it again
    ///   rather than finding a document nobody is holding any more.
    /// </summary>
    [Fact]
    public void AFormReadsTheDocumentAgainAfterTheLastOneOverItWasDisposed()
    {
        var path = AFileOf(new XSize(300, 400));

        PdfDocument first;
        using (var form = XPdfForm.FromFile(path))
            first = form.Page.Owner;

        using var again = XPdfForm.FromFile(path);

        again.Page.Should().NotBeNull();
        again.Page.Owner.Should().NotBeSameAs(first);
    }

    // ----- drawn onto a page ------------------------------------------------------------------

    [Fact]
    public void APageDrawnOntoAnotherArrivesAsAFormXObject()
    {
        var path = AFileOf(new XSize(300, 400), new XSize(500, 600));

        var document = new PdfDocument();
        var page = document.AddPage();

        using (var form = XPdfForm.FromFile(path))
        {
            form.PageNumber = 2;
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawImage(form, new XRect(0, 0, 250, 300));
        }

        var written = document.Reopened().Pages[0];
        var xObjects = written.Resources.Elements.GetDictionary("/XObject");

        xObjects.Should().NotBeNull();
        xObjects!.Elements.Count.Should().Be(1);
    }

    // ----- a template rather than an imported page --------------------------------------------

    /// <summary>
    ///   An <see cref="XForm"/> made against a document is a template drawn on here and reused; an
    ///   <see cref="XPdfForm"/> over a file is a page drawn elsewhere. They share a base class and
    ///   almost nothing else, so each refuses what only the other can do.
    /// </summary>
    [Fact]
    public void ATemplateIsOnePageAndHasNoDocumentToImportFrom()
    {
        var document = new PdfDocument();
        using var template = new XForm(document, new XRect(0, 0, 100, 100));

        template.ViewBox.Width.Should().Be(100);
        template.PixelWidth.Should().Be(100);
        template.PixelHeight.Should().Be(100);
        template.HorizontalResolution.Should().Be(72);
        template.VerticalResolution.Should().Be(72);
        template.BoundingBox = new XRect(0, 0, 50, 50);
        template.BoundingBox.Width.Should().Be(50);
    }

    [Fact]
    public void ATemplateMustBeGivenADocumentAndASizeToDrawOn()
    {
        var withoutSize = () => new XForm(new PdfDocument(), new XRect(0, 0, 0, 0));
        var withoutDocument = () => new XForm(null, new XRect(0, 0, 100, 100));

        withoutSize.Should().Throw<ArgumentNullException>();
        withoutDocument.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    ///   Asking a template for a graphics object twice hands back the one it already has rather
    ///   than a second surface over the same content stream. A form holds one set of operators, so
    ///   two writers would interleave into it and the second would not know.
    /// </summary>
    [Fact]
    public void ATemplateHasOneGraphicsObjectHoweverOftenItIsAskedForOne()
    {
        var document = new PdfDocument();
        using var template = new XForm(document, new XRect(0, 0, 100, 100));

        using var gfx = XGraphics.FromForm(template);

        XGraphics.FromForm(template).Should().BeSameAs(gfx);
    }

    /// <summary>
    ///   An imported page is somebody else's content and this library does not rewrite it, so
    ///   asking to draw on one is refused rather than quietly producing a page with the drawing and
    ///   not the import.
    /// </summary>
    [Fact]
    public void AnImportedPageCannotBeDrawnOn()
    {
        var path = AFileOf(new XSize(300, 400));
        using var form = XPdfForm.FromFile(path);

        var drawing = () => XGraphics.FromForm(form);

        drawing.Should().Throw<Exception>();
    }

    /// <summary>
    ///   A template that has been drawn is written into the document it belongs to, so changing the
    ///   transform afterwards would move something already on the page.
    /// </summary>
    [Fact]
    public void ATemplateRefusesToBeMovedOnceItHasBeenDrawn()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var template = new XForm(document, new XRect(0, 0, 100, 100));

        using (var formGfx = XGraphics.FromForm(template))
            formGfx.DrawRectangle(XBrushes.Black, 0, 0, 10, 10);

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawImage(template, 0, 0);

        var moving = () => template.Transform = new XMatrix();

        moving.Should().Throw<InvalidOperationException>()
            .WithMessage("*must not be modified*");
    }

    [Fact]
    public void AFormCanBeGivenAPlaceHolderForSurfacesThatCannotDrawIt()
    {
        var path = AFileOf(new XSize(300, 400));
        using var form = XPdfForm.FromFile(path);

        form.PlaceHolder.Should().BeNull();

        var placeHolder = XImage.FromFile(PathHelper.GetInstance().GetAssetPath("frog-and-toad.jpg"));
        form.PlaceHolder = placeHolder;

        form.PlaceHolder.Should().BeSameAs(placeHolder);
    }
}
