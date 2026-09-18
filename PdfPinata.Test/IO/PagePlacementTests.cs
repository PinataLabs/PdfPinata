using System;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The page placement API: creating a page without placing it, placing it, importing a
///   foreign page, duplicating a page, and moving one.
///   See https://github.com/ststeiger/PdfSharpCore/issues/455.
/// </summary>
public class PagePlacementTests
{
    private static string SourcePdf => Path.Combine("Assets", "FamilyTree.pdf");
    private static string ImagePath => Path.Combine("Assets", "lenna.png");

    private static PdfDocument OpenForModify() =>
        global::PdfPinata.Pdf.IO.PdfReader.Open(SourcePdf, PdfDocumentOpenMode.Modify);

    private static PdfDocument OpenForImport() =>
        global::PdfPinata.Pdf.IO.PdfReader.Open(SourcePdf, PdfDocumentOpenMode.Import);

    private static byte[] Save(PdfDocument document)
    {
        MemoryStream stream = new MemoryStream();
        document.Save(stream, false);
        return stream.ToArray();
    }

    // ----- the mistake from the issue, and what it now says -------------------------------

    /// <summary>
    ///   Placing the page AddPage() returned still throws - but the message now names the
    ///   index it sits at and the calls that do what the caller meant.
    /// </summary>
    [Fact]
    public void PlacingAnAlreadyPlacedPage_ExplainsTheRemedy()
    {
        PdfDocument document = OpenForModify();
        PdfPage page = document.AddPage();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => document.InsertPage(1, page));

        Assert.Contains("already at index 1", ex.Message);
        Assert.Contains("document.MovePage(1, 1)", ex.Message);
        Assert.Contains("document.DuplicatePage(1, 1)", ex.Message);
        Assert.Contains("new PdfPage(document)", ex.Message);
    }

    // ----- create, draw, then place -------------------------------------------------------

    /// <summary>
    ///   A page built with new PdfPage(document) is drawable but not yet in the page tree.
    /// </summary>
    [Fact]
    public void NewPageOwnedByDocument_IsDrawableButNotPlaced()
    {
        PdfDocument document = OpenForModify();
        int before = document.PageCount;

        PdfPage page = new PdfPage(document);
        Assert.Equal(before, document.PageCount);
        Assert.Equal(-1, document.Pages.IndexOf(page));

        XGraphics gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);
    }

    /// <summary>
    ///   PlacePage returns the very page passed in, never a copy, and puts it where asked.
    /// </summary>
    [Fact]
    public void PlacePage_ReturnsTheSameObjectAndPlacesIt()
    {
        PdfDocument document = OpenForModify();
        int before = document.PageCount;

        PdfPage page = new PdfPage(document);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);

        PdfPage placed = document.PlacePage(0, page);

        Assert.Same(page, placed);
        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(0, document.Pages.IndexOf(page));
        Assert.True(Save(document).Length > 0);
    }

    [Fact]
    public void PlacePage_RejectsAForeignPage()
    {
        PdfDocument target = OpenForModify();
        PdfDocument foreign = OpenForImport();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => target.PlacePage(0, foreign.Pages[0]));

        Assert.Contains("belongs to another document", ex.Message);
        Assert.Contains("ImportPage", ex.Message);
    }

    [Fact]
    public void PlacePage_RejectsAnAlreadyPlacedPage()
    {
        PdfDocument document = OpenForModify();
        PdfPage page = document.AddPage();

        Assert.Throws<InvalidOperationException>(() => document.PlacePage(0, page));
    }

    // ----- insert -------------------------------------------------------------------------

    /// <summary>
    ///   InsertPage(int) creates the page where it is asked to and returns the page it created,
    ///   already placed. Only its rejection of an already-placed page was covered before.
    /// </summary>
    [Fact]
    public void InsertPage_CreatesThePageAtTheIndexGiven()
    {
        PdfDocument document = OpenForModify();
        int before = document.PageCount;
        PdfPage wasFirst = document.Pages[0];

        PdfPage inserted = document.InsertPage(0);

        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(0, document.Pages.IndexOf(inserted));
        Assert.Equal(1, document.Pages.IndexOf(wasFirst));
        Assert.True(Save(document).Length > 0);
    }

    /// <summary>
    ///   InsertPage(int, PdfPage) places a page of this document rather than copying it, which
    ///   is the branch AddPage(PdfPage) shares with it.
    /// </summary>
    [Fact]
    public void InsertPage_PlacesAPageOfThisDocumentWithoutCopyingIt()
    {
        PdfDocument document = OpenForModify();
        int before = document.PageCount;
        PdfPage page = new PdfPage(document);

        PdfPage inserted = document.InsertPage(0, page);

        Assert.Same(page, inserted);
        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(0, document.Pages.IndexOf(page));
        Assert.True(Save(document).Length > 0);
    }

    /// <summary>
    ///   InsertPage(int, PdfPage) imports a page of another document, so what comes back is a
    ///   copy - the behaviour ImportPage says in its name and this overload does not.
    /// </summary>
    [Fact]
    public void InsertPage_ImportsAForeignPageAndReturnsTheCopy()
    {
        PdfDocument document = OpenForModify();
        PdfDocument foreign = OpenForImport();
        int before = document.PageCount;

        PdfPage source = foreign.Pages[0];
        PdfPage inserted = document.InsertPage(0, source);

        Assert.NotSame(source, inserted);
        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(0, document.Pages.IndexOf(inserted));
        Assert.True(Save(document).Length > 0);
    }

    // ----- import -------------------------------------------------------------------------

    /// <summary>
    ///   ImportPage always copies, so the value returned never aliases the argument.
    /// </summary>
    [Fact]
    public void ImportPage_AlwaysReturnsACopy()
    {
        PdfDocument target = OpenForModify();
        PdfDocument foreign = OpenForImport();
        int before = target.PageCount;

        PdfPage source = foreign.Pages[0];
        PdfPage imported = target.ImportPage(0, source);

        Assert.NotSame(source, imported);
        Assert.Equal(before + 1, target.PageCount);
        Assert.Equal(0, target.Pages.IndexOf(imported));
        Assert.True(Save(target).Length > 0);
    }

    [Fact]
    public void ImportPage_RejectsAPageOfThisDocument()
    {
        PdfDocument document = OpenForModify();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => document.ImportPage(0, document.Pages[0]));

        Assert.Contains("already belongs to this document", ex.Message);
        Assert.Contains("DuplicatePage", ex.Message);
    }

    // ----- duplicate ----------------------------------------------------------------------

    /// <summary>
    ///   Duplicating gives a second, independent page object showing the same content, and the
    ///   result survives a save and reload.
    /// </summary>
    [Fact]
    public void DuplicatePage_AddsASecondPageWithTheSameContent()
    {
        PdfDocument document = OpenForModify();
        int before = document.PageCount;
        double width = document.Pages[0].Width.Point;
        double height = document.Pages[0].Height.Point;

        PdfPage duplicate = document.DuplicatePage(0, 1);

        Assert.NotSame(document.Pages[0], duplicate);
        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(1, document.Pages.IndexOf(duplicate));

        byte[] saved = Save(document);
        PdfDocument reloaded = global::PdfPinata.Pdf.IO.PdfReader.Open(
            new MemoryStream(saved), PdfDocumentOpenMode.Modify);

        Assert.Equal(before + 1, reloaded.PageCount);
        Assert.Equal(width, reloaded.Pages[1].Width.Point);
        Assert.Equal(height, reloaded.Pages[1].Height.Point);
    }

    /// <summary>
    ///   Sharing the content stream means the duplicate costs almost nothing in the file.
    /// </summary>
    [Fact]
    public void DuplicatePage_SharesContentRatherThanCopyingIt()
    {
        PdfDocument plain = OpenForModify();
        int plainSize = Save(plain).Length;

        PdfDocument doubled = OpenForModify();
        doubled.DuplicatePage(0, 1);
        int doubledSize = Save(doubled).Length;

        // A duplicated page adds a page object, not another copy of the content stream.
        Assert.True(doubledSize < plainSize * 1.05,
            $"duplicate grew the file from {plainSize} to {doubledSize}");
    }

    /// <summary>
    ///   Drawing on a duplicate must not reach the page it was made from. The content stream is
    ///   shared until one of the pages is drawn on, and the resource dictionary is never shared,
    ///   so the source keeps the resources it started with.
    /// </summary>
    [Fact]
    public void DrawingOnADuplicate_LeavesTheSourceAlone()
    {
        PdfDocument document = OpenForModify();
        PdfPage duplicate = document.DuplicatePage(0, 1);
        PdfPage source = document.Pages[0];

        Assert.NotSame(source.Elements["/Resources"], duplicate.Elements["/Resources"]);
        Assert.Same(source.Elements["/Contents"], duplicate.Elements["/Contents"]);

        string resourcesBefore = source.Elements["/Resources"].ToString();

        XGraphics gfx = XGraphics.FromPdfPage(duplicate);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, 200, 200);

        // The source is untouched: same resources, same single content stream.
        Assert.Equal(resourcesBefore, source.Elements["/Resources"].ToString());
        Assert.DoesNotContain("/XObject", source.Elements["/Resources"].ToString());
        Assert.Contains("/XObject", duplicate.Elements["/Resources"].ToString());
        Assert.NotSame(source.Elements["/Contents"], duplicate.Elements["/Contents"]);

        Assert.True(Save(document).Length > 0);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(99, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 99)]
    public void DuplicatePage_RejectsIndicesOutOfRange(int sourceIndex, int index)
    {
        PdfDocument document = OpenForModify();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => document.DuplicatePage(sourceIndex, index));
    }

    // ----- move ---------------------------------------------------------------------------

    /// <summary>
    ///   MovePage is reachable on the document, not only on document.Pages.
    /// </summary>
    [Fact]
    public void MovePage_IsOnTheDocumentAndReorders()
    {
        PdfDocument document = OpenForModify();
        PdfPage first = document.Pages[0];
        PdfPage appended = document.AddPage();

        document.MovePage(1, 0);

        Assert.Same(appended, document.Pages[0]);
        Assert.Same(first, document.Pages[1]);
        Assert.True(Save(document).Length > 0);
    }

    // ----- IndexOf ------------------------------------------------------------------------

    [Fact]
    public void IndexOf_TellsPlacedFromUnplaced()
    {
        PdfDocument document = OpenForModify();

        Assert.Equal(0, document.Pages.IndexOf(document.Pages[0]));
        Assert.Equal(-1, document.Pages.IndexOf(new PdfPage(document)));
        Assert.Throws<ArgumentNullException>(() => document.Pages.IndexOf(null));
    }

    // ----- the whole point ----------------------------------------------------------------

    /// <summary>
    ///   What the reporter of the issue was trying to do, spelled the way the API now supports.
    /// </summary>
    [Fact]
    public void InsertAnImagePageAfterAGivenPage()
    {
        PdfDocument document = OpenForModify();
        int pageIndex = 0;
        int before = document.PageCount;

        PdfPage page = new PdfPage(document);
        XGraphics gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);
        document.PlacePage(pageIndex + 1, page);

        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(pageIndex + 1, document.Pages.IndexOf(page));
        Assert.True(Save(document).Length > 0);
    }
}
