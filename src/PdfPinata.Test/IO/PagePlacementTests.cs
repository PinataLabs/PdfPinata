using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

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

    // ----- the mistake from the issue, and what it now says -------------------------------

    /// <summary>
    ///   Placing the page AddPage() returned still throws - but the message now names the
    ///   index it sits at and the calls that do what the caller meant.
    /// </summary>
    [Test]
    public void PlacingAnAlreadyPlacedPage_ExplainsTheRemedy()
    {
        var document = OpenForModify();
        var page = document.AddPage();

        var ex = FluentActions.Invoking(() => document.InsertPage(1, page))
            .Should().ThrowExactly<InvalidOperationException>().Which;

        ex.Message.Should().Contain("already at index 1");
        ex.Message.Should().Contain("document.MovePage(1, 1)");
        ex.Message.Should().Contain("document.DuplicatePage(1, 1)");
        ex.Message.Should().Contain("new PdfPage(document)");
    }

    // ----- create, draw, then place -------------------------------------------------------

    /// <summary>
    ///   A page built with new PdfPage(document) is drawable but not yet in the page tree.
    /// </summary>
    [Test]
    public void NewPageOwnedByDocument_IsDrawableButNotPlaced()
    {
        var document = OpenForModify();
        var before = document.PageCount;

        var page = new PdfPage(document);
        document.PageCount.Should().Be(before);
        document.Pages.IndexOf(page).Should().Be(-1);

        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);
    }

    /// <summary>
    ///   PlacePage returns the very page passed in, never a copy, and puts it where asked.
    /// </summary>
    [Test]
    public void PlacePage_ReturnsTheSameObjectAndPlacesIt()
    {
        var document = OpenForModify();
        var before = document.PageCount;

        var page = new PdfPage(document);
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);

        var placed = document.PlacePage(0, page);

        placed.Should().BeSameAs(page);
        document.PageCount.Should().Be(before + 1);
        document.Pages.IndexOf(page).Should().Be(0);
        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void PlacePage_RejectsAForeignPage()
    {
        var target = OpenForModify();
        var foreign = OpenForImport();

        var ex = FluentActions.Invoking(() => target.PlacePage(0, foreign.Pages[0]))
            .Should().ThrowExactly<InvalidOperationException>().Which;

        ex.Message.Should().Contain("belongs to another document");
        ex.Message.Should().Contain("ImportPage");
    }

    [Test]
    public void PlacePage_RejectsAnAlreadyPlacedPage()
    {
        var document = OpenForModify();
        var page = document.AddPage();

        FluentActions.Invoking(() => document.PlacePage(0, page)).Should().ThrowExactly<InvalidOperationException>();
    }

    // ----- insert -------------------------------------------------------------------------

    /// <summary>
    ///   InsertPage(int) creates the page where it is asked to and returns the page it created,
    ///   already placed. Only its rejection of an already-placed page was covered before.
    /// </summary>
    [Test]
    public void InsertPage_CreatesThePageAtTheIndexGiven()
    {
        var document = OpenForModify();
        var before = document.PageCount;
        var wasFirst = document.Pages[0];

        var inserted = document.InsertPage(0);

        document.PageCount.Should().Be(before + 1);
        document.Pages.IndexOf(inserted).Should().Be(0);
        document.Pages.IndexOf(wasFirst).Should().Be(1);
        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }

    /// <summary>
    ///   InsertPage(int, PdfPage) places a page of this document rather than copying it, which
    ///   is the branch AddPage(PdfPage) shares with it.
    /// </summary>
    [Test]
    public void InsertPage_PlacesAPageOfThisDocumentWithoutCopyingIt()
    {
        var document = OpenForModify();
        var before = document.PageCount;
        var page = new PdfPage(document);

        var inserted = document.InsertPage(0, page);

        inserted.Should().BeSameAs(page);
        document.PageCount.Should().Be(before + 1);
        document.Pages.IndexOf(page).Should().Be(0);
        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }

    /// <summary>
    ///   InsertPage(int, PdfPage) imports a page of another document, so what comes back is a
    ///   copy - the behaviour ImportPage says in its name and this overload does not.
    /// </summary>
    [Test]
    public void InsertPage_ImportsAForeignPageAndReturnsTheCopy()
    {
        var document = OpenForModify();
        var foreign = OpenForImport();
        var before = document.PageCount;

        var source = foreign.Pages[0];
        var inserted = document.InsertPage(0, source);

        inserted.Should().NotBeSameAs(source);
        document.PageCount.Should().Be(before + 1);
        document.Pages.IndexOf(inserted).Should().Be(0);
        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }

    // ----- import -------------------------------------------------------------------------

    /// <summary>
    ///   ImportPage always copies, so the value returned never aliases the argument.
    /// </summary>
    [Test]
    public void ImportPage_AlwaysReturnsACopy()
    {
        var target = OpenForModify();
        var foreign = OpenForImport();
        var before = target.PageCount;

        var source = foreign.Pages[0];
        var imported = target.ImportPage(0, source);

        imported.Should().NotBeSameAs(source);
        target.PageCount.Should().Be(before + 1);
        target.Pages.IndexOf(imported).Should().Be(0);
        Saved.Bytes(target).Length.Should().BeGreaterThan(0);
    }

    [Test]
    public void ImportPage_RejectsAPageOfThisDocument()
    {
        var document = OpenForModify();

        var ex = FluentActions.Invoking(() => document.ImportPage(0, document.Pages[0]))
            .Should().ThrowExactly<InvalidOperationException>().Which;

        ex.Message.Should().Contain("already belongs to this document");
        ex.Message.Should().Contain("DuplicatePage");
    }

    // ----- duplicate ----------------------------------------------------------------------

    /// <summary>
    ///   Duplicating gives a second, independent page object showing the same content, and the
    ///   result survives a save and reload.
    /// </summary>
    [Test]
    public void DuplicatePage_AddsASecondPageWithTheSameContent()
    {
        var document = OpenForModify();
        var before = document.PageCount;
        var width = document.Pages[0].Width.Point;
        var height = document.Pages[0].Height.Point;

        var duplicate = document.DuplicatePage(0, 1);

        duplicate.Should().NotBeSameAs(document.Pages[0]);
        document.PageCount.Should().Be(before + 1);
        document.Pages.IndexOf(duplicate).Should().Be(1);

        var saved = Saved.Bytes(document);
        var reloaded = global::PdfPinata.Pdf.IO.PdfReader.Open(
            new MemoryStream(saved), PdfDocumentOpenMode.Modify);

        reloaded.PageCount.Should().Be(before + 1);
        reloaded.Pages[1].Width.Point.Should().Be(width);
        reloaded.Pages[1].Height.Point.Should().Be(height);
    }

    /// <summary>
    ///   Sharing the content stream means the duplicate costs almost nothing in the file.
    /// </summary>
    [Test]
    public void DuplicatePage_SharesContentRatherThanCopyingIt()
    {
        var plain = OpenForModify();
        var plainSize = Saved.Bytes(plain).Length;

        var doubled = OpenForModify();
        _ = doubled.DuplicatePage(0, 1);
        var doubledSize = Saved.Bytes(doubled).Length;

        // A duplicated page adds a page object, not another copy of the content stream.
        ((double)doubledSize).Should().BeLessThan(plainSize * 1.05,
            $"duplicate grew the file from {plainSize} to {doubledSize}");
    }

    /// <summary>
    ///   Drawing on a duplicate must not reach the page it was made from. The content stream is
    ///   shared until one of the pages is drawn on, and the resource dictionary is never shared,
    ///   so the source keeps the resources it started with.
    /// </summary>
    [Test]
    public void DrawingOnADuplicate_LeavesTheSourceAlone()
    {
        var document = OpenForModify();
        var duplicate = document.DuplicatePage(0, 1);
        var source = document.Pages[0];

        duplicate.Elements["/Resources"].Should().NotBeSameAs(source.Elements["/Resources"]);
        duplicate.Elements["/Contents"].Should().BeSameAs(source.Elements["/Contents"]);

        var resourcesBefore = source.Elements["/Resources"].ToString();

        var gfx = XGraphics.FromPdfPage(duplicate);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, 200, 200);

        // The source is untouched: same resources, same single content stream.
        source.Elements["/Resources"].ToString().Should().Be(resourcesBefore);
        source.Elements["/Resources"].ToString().Should().NotContain("/XObject");
        duplicate.Elements["/Resources"].ToString().Should().Contain("/XObject");
        duplicate.Elements["/Contents"].Should().NotBeSameAs(source.Elements["/Contents"]);

        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }

    [Test]
    [Arguments(-1, 0)]
    [Arguments(99, 0)]
    [Arguments(0, -1)]
    [Arguments(0, 99)]
    public void DuplicatePage_RejectsIndicesOutOfRange(int sourceIndex, int index)
    {
        var document = OpenForModify();
        FluentActions.Invoking(() => document.DuplicatePage(sourceIndex, index))
            .Should().ThrowExactly<ArgumentOutOfRangeException>();
    }

    // ----- move ---------------------------------------------------------------------------

    /// <summary>
    ///   MovePage is reachable on the document, not only on document.Pages.
    /// </summary>
    [Test]
    public void MovePage_IsOnTheDocumentAndReorders()
    {
        var document = OpenForModify();
        var first = document.Pages[0];
        var appended = document.AddPage();

        document.MovePage(1, 0);

        document.Pages[0].Should().BeSameAs(appended);
        document.Pages[1].Should().BeSameAs(first);
        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }

    // ----- IndexOf ------------------------------------------------------------------------

    [Test]
    public void IndexOf_TellsPlacedFromUnplaced()
    {
        var document = OpenForModify();

        document.Pages.IndexOf(document.Pages[0]).Should().Be(0);
        (document.Pages.IndexOf(new PdfPage(document))).Should().Be(-1);
        FluentActions.Invoking(() => document.Pages.IndexOf(null)).Should().ThrowExactly<ArgumentNullException>();
    }

    // ----- the whole point ----------------------------------------------------------------

    /// <summary>
    ///   What the reporter of the issue was trying to do, spelled the way the API now supports.
    /// </summary>
    [Test]
    public void InsertAnImagePageAfterAGivenPage()
    {
        var document = OpenForModify();
        var before = document.PageCount;

        var page = new PdfPage(document);
        var gfx = XGraphics.FromPdfPage(page);
        gfx.DrawImage(XImage.FromFile(ImagePath), 0, 0, page.Width, page.Height);
        document.PlacePage(1, page);

        document.PageCount.Should().Be(before + 1);
        document.Pages.IndexOf(page).Should().Be(1);
        Saved.Bytes(document).Length.Should().BeGreaterThan(0);
    }
}
