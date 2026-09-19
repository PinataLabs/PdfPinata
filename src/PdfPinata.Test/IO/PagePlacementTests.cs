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
        var stream = new MemoryStream();
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
        var document = OpenForModify();
        var page = document.AddPage();

        var ex = Assert.Throws<InvalidOperationException>(
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
        var document = OpenForModify();
        var before = document.PageCount;

        var page = new PdfPage(document);
        Assert.Equal(before, document.PageCount);
        Assert.Equal(-1, document.Pages.IndexOf(page));

        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);
    }

    /// <summary>
    ///   PlacePage returns the very page passed in, never a copy, and puts it where asked.
    /// </summary>
    [Fact]
    public void PlacePage_ReturnsTheSameObjectAndPlacesIt()
    {
        var document = OpenForModify();
        var before = document.PageCount;

        var page = new PdfPage(document);
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);

        var placed = document.PlacePage(0, page);

        Assert.Same(page, placed);
        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(0, document.Pages.IndexOf(page));
        Assert.True(Save(document).Length > 0);
    }

    [Fact]
    public void PlacePage_RejectsAForeignPage()
    {
        var target = OpenForModify();
        var foreign = OpenForImport();

        var ex = Assert.Throws<InvalidOperationException>(
            () => target.PlacePage(0, foreign.Pages[0]));

        Assert.Contains("belongs to another document", ex.Message);
        Assert.Contains("ImportPage", ex.Message);
    }

    [Fact]
    public void PlacePage_RejectsAnAlreadyPlacedPage()
    {
        var document = OpenForModify();
        var page = document.AddPage();

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
        var document = OpenForModify();
        var before = document.PageCount;
        var wasFirst = document.Pages[0];

        var inserted = document.InsertPage(0);

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
        var document = OpenForModify();
        var before = document.PageCount;
        var page = new PdfPage(document);

        var inserted = document.InsertPage(0, page);

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
        var document = OpenForModify();
        var foreign = OpenForImport();
        var before = document.PageCount;

        var source = foreign.Pages[0];
        var inserted = document.InsertPage(0, source);

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
        var target = OpenForModify();
        var foreign = OpenForImport();
        var before = target.PageCount;

        var source = foreign.Pages[0];
        var imported = target.ImportPage(0, source);

        Assert.NotSame(source, imported);
        Assert.Equal(before + 1, target.PageCount);
        Assert.Equal(0, target.Pages.IndexOf(imported));
        Assert.True(Save(target).Length > 0);
    }

    [Fact]
    public void ImportPage_RejectsAPageOfThisDocument()
    {
        var document = OpenForModify();

        var ex = Assert.Throws<InvalidOperationException>(
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
        var document = OpenForModify();
        var before = document.PageCount;
        var width = document.Pages[0].Width.Point;
        var height = document.Pages[0].Height.Point;

        var duplicate = document.DuplicatePage(0, 1);

        Assert.NotSame(document.Pages[0], duplicate);
        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(1, document.Pages.IndexOf(duplicate));

        var saved = Save(document);
        var reloaded = global::PdfPinata.Pdf.IO.PdfReader.Open(
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
        var plain = OpenForModify();
        var plainSize = Save(plain).Length;

        var doubled = OpenForModify();
        doubled.DuplicatePage(0, 1);
        var doubledSize = Save(doubled).Length;

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
        var document = OpenForModify();
        var duplicate = document.DuplicatePage(0, 1);
        var source = document.Pages[0];

        Assert.NotSame(source.Elements["/Resources"], duplicate.Elements["/Resources"]);
        Assert.Same(source.Elements["/Contents"], duplicate.Elements["/Contents"]);

        var resourcesBefore = source.Elements["/Resources"].ToString();

        var gfx = XGraphics.FromPdfPage(duplicate);
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
        var document = OpenForModify();
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
        var document = OpenForModify();
        var first = document.Pages[0];
        var appended = document.AddPage();

        document.MovePage(1, 0);

        Assert.Same(appended, document.Pages[0]);
        Assert.Same(first, document.Pages[1]);
        Assert.True(Save(document).Length > 0);
    }

    // ----- IndexOf ------------------------------------------------------------------------

    [Fact]
    public void IndexOf_TellsPlacedFromUnplaced()
    {
        var document = OpenForModify();

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
        var document = OpenForModify();
        var pageIndex = 0;
        var before = document.PageCount;

        var page = new PdfPage(document);
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);
        document.PlacePage(pageIndex + 1, page);

        Assert.Equal(before + 1, document.PageCount);
        Assert.Equal(pageIndex + 1, document.Pages.IndexOf(page));
        Assert.True(Save(document).Length > 0);
    }
}
