using System;
using System.IO;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   The page tree on its own terms. Every one of these operations was reached only by going
///   through a <c>PdfDocument</c> forwarder first - AddPage, InsertPage, PlacePage and the rest -
///   so <c>document.Pages</c>, which is public and which callers do use directly, had no tests of
///   its own. It is also where the decision about whether the document may be modified is now
///   made, which is the other reason for testing it here rather than one level up.
///   <para>
///   <c>Insert</c> gets one test per branch, because it has three: a page this document already
///   owns, a page with no owner at all, and a page belonging to somebody else.
///   </para>
/// </summary>
public class PdfPagesTests
{
    /// <summary>
    ///   A document that can be imported from, which means one written out and read back: the
    ///   import path refuses a document that was not opened with <see cref="PdfDocumentOpenMode.Import"/>.
    /// </summary>
    static PdfDocument AnImportableDocument(int pageCount = 3)
    {
        var source = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
            _ = source.AddPage();

        var bytes = new MemoryStream();
        source.Save(bytes, false);
        bytes.Position = 0;
        return Pdf.IO.PdfReader.Open(bytes, PdfDocumentOpenMode.Import);
    }

    // ----- Insert: a page with no owner ----------------------------------------------------------

    [Fact]
    public void InsertingAPageNobodyOwnsPlacesThatVeryPage()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var page = new PdfPage();

        var inserted = document.Pages.Insert(0, page);

        inserted.Should().BeSameAs(page, "a page with no owner is placed rather than copied");
        document.Pages.IndexOf(page).Should().Be(0);
        document.PageCount.Should().Be(2);
    }

    [Fact]
    public void AddingAPageNobodyOwnsPutsItLast()
    {
        var document = new PdfDocument();
        var first = document.Pages.Add();

        var second = document.Pages.Add(new PdfPage());

        document.PageCount.Should().Be(2);
        document.Pages.IndexOf(first).Should().Be(0);
        document.Pages.IndexOf(second).Should().Be(1);
    }

    [Fact]
    public void InsertingAtAnIndexCreatesThePageThere()
    {
        var document = new PdfDocument();
        var last = document.Pages.Add();

        var inserted = document.Pages.Insert(0);

        document.Pages.IndexOf(inserted).Should().Be(0);
        document.Pages.IndexOf(last).Should().Be(1);
    }

    // ----- Insert: a page this document already owns ---------------------------------------------

    [Fact]
    public void InsertingAPageThisDocumentOwnsButHasNotPlacedPlacesThatVeryPage()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var page = new PdfPage(document);
        document.Pages.IndexOf(page).Should().Be(-1, "a page built this way is not yet in the tree");

        var inserted = document.Pages.Insert(0, page);

        inserted.Should().BeSameAs(page);
        document.Pages.IndexOf(page).Should().Be(0);
        document.PageCount.Should().Be(2);
    }

    [Fact]
    public void InsertingAPageThisDocumentHasAlreadyPlacedIsRefused()
    {
        var document = new PdfDocument();
        var page = document.Pages.Add();

        var insert = () => document.Pages.Insert(1, page);

        insert.Should().Throw<InvalidOperationException>()
            .WithMessage("*already at index 0*");
        document.PageCount.Should().Be(1);
    }

    // ----- Insert: a page from another document --------------------------------------------------

    [Fact]
    public void InsertingAPageFromAnotherDocumentCopiesIt()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        var foreign = AnImportableDocument(1);
        var foreignPage = foreign.Pages[0];

        var inserted = document.Pages.Insert(0, foreignPage);

        inserted.Should().NotBeSameAs(foreignPage, "a foreign page is imported, not adopted");
        inserted.Owner.Should().BeSameAs(document);
        document.Pages.IndexOf(inserted).Should().Be(0);
        document.PageCount.Should().Be(2);
    }

    [Fact]
    public void InsertingAPageFromADocumentNotOpenedForImportIsRefused()
    {
        var document = new PdfDocument();
        var other = new PdfDocument();
        var otherPage = other.AddPage();

        var insert = () => document.Pages.Insert(0, otherPage);

        insert.Should().Throw<InvalidOperationException>()
            .WithMessage("*PdfDocumentOpenMode.Import*");
    }

    [Fact]
    public void InsertingNothingIsRefused()
    {
        var document = new PdfDocument();

        var insert = () => document.Pages.Insert(0, null);

        insert.Should().Throw<ArgumentNullException>();
    }

    // ----- InsertRange, which has no PdfDocument forwarder at all --------------------------------

    [Fact]
    public void InsertingARangeBringsEveryPageOfIt()
    {
        var document = new PdfDocument();
        var kept = document.AddPage();

        document.Pages.InsertRange(0, AnImportableDocument());

        document.PageCount.Should().Be(4);
        document.Pages.IndexOf(kept).Should().Be(3, "the range went in ahead of it");
    }

    [Fact]
    public void InsertingAPartOfARangeBringsThatPartAlone()
    {
        var document = new PdfDocument();

        document.Pages.InsertRange(0, AnImportableDocument(), startIndex: 1, pageCount: 2);

        document.PageCount.Should().Be(2);
    }

    [Fact]
    public void InsertingARangeAtAnIndexOutsideTheDocumentIsRefused()
    {
        var document = new PdfDocument();

        var insert = () => document.Pages.InsertRange(1, AnImportableDocument(1));

        insert.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }

    [Fact]
    public void InsertingARangeOfNoDocumentIsRefused()
    {
        var document = new PdfDocument();

        var insert = () => document.Pages.InsertRange(0, null);

        insert.Should().Throw<ArgumentNullException>();
    }
}
