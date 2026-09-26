using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   A page removed from a document and inserted into it again - the case <c>PdfPages.Insert</c>
///   takes for a page this document already owns but no longer shows.
/// </summary>
/// <remarks>
///   Removed and put back before a save, it always worked: the page stays in the object table until
///   something drops it. A save is what drops it. Everything the trailer no longer reaches is taken
///   out and what is left is numbered from one again, so the removed page's number - and its
///   content stream's - soon belong to other objects. Inserting the page then found its number
///   taken, did nothing, and the next save threw adding a second object under that number.
/// </remarks>
public class PageReinsertionTests
{
    [Theory]
    [InlineData(1, "p1,p2,p3")]
    [InlineData(0, "p2,p1,p3")]
    [InlineData(2, "p1,p3,p2")]
    public void APageRemovedAndInsertedAgainIsWrittenWhereItWasPut(int index, string expected)
    {
        var document = ADocumentOf(3);
        var page = document.Pages[1];

        document.Pages.Remove(page);
        var inserted = document.Pages.Insert(index, page);

        inserted.Should().BeSameAs(page, "a page of this document is placed, not copied");
        Describe(document.Reopened()).Should().Be(expected);
    }

    [Fact]
    public void APageRemovedAcrossASaveAndInsertedAgainKeepsItsContent()
    {
        var document = ADocumentOf(3);
        var page = document.Pages[1];

        document.Pages.Remove(page);
        Saved.Bytes(document);
        _ = document.Pages.Insert(2, page);

        var reopened = document.Reopened();
        Describe(reopened).Should().Be("p1,p3,p2");
        ContentOf(reopened.Pages[2]).Should().Contain("0 1 0 rg",
            "the content stream was dropped with the page and has to come back with it");
        ContentOf(reopened.Pages[1]).Should().Contain("0 0 1 rg");
    }

    [Fact]
    public void APageOfADocumentReadFromAFileCanBeRemovedAcrossASaveAndPutBack()
    {
        var document = ADocumentOf(3).Reopened();
        var page = document.Pages[0];

        document.Pages.Remove(page);
        Saved.Bytes(document);
        _ = document.Pages.Insert(2, page);

        Describe(document.Reopened()).Should().Be("p2,p3,p1");
    }

    [Fact]
    public void ObjectsMadeAfterAPageIsPutBackAreNumberedPastIt()
    {
        // The last page's objects have the highest numbers. Removed and saved, the document is
        // numbered from one again below them, so the page comes back to a number above every
        // other - and the objects made after it were handed that number again, and silently not
        // added under it.
        var document = ADocumentOf(3);
        var page = document.Pages[2];
        document.Pages.Remove(page);
        Saved.Bytes(document);
        _ = document.Pages.Insert(2, page);

        for (var idx = 0; idx < 50; idx++)
        {
            var made = new PdfDictionary(document);
            document.Internals.AddObject(made);
            document.Internals.GetObject(PdfInternals.GetObjectID(made)).Should().BeSameAs(made,
                "each new object is found by its own number");
        }
        document.Internals.GetObject(PdfInternals.GetObjectID(page)).Should().BeSameAs(page);
    }

    // ── Arranging ───────────────────────────────────────────────────────────────────────────────

    private static readonly XColor[] Colours = [XColors.Red, XColors.Lime, XColors.Blue];

    /// <summary>
    ///   Each page carries a tag naming it and is painted in a colour of its own, so both the page
    ///   dictionary and its content stream can be told apart after a round trip.
    /// </summary>
    private static PdfDocument ADocumentOf(int pages)
    {
        var document = new PdfDocument();
        for (var idx = 0; idx < pages; idx++)
        {
            var page = document.AddPage();
            page.Elements.SetString("/PinataTag", $"p{idx + 1}");
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawRectangle(new XSolidBrush(Colours[idx % Colours.Length]), 10, 10, 20, 20);
        }
        document.Options.CompressContentStreams = false;
        return document;
    }

    /// <summary>
    ///   The pages' tags in page order, each marked when its <c>/Parent</c> is not the page tree.
    /// </summary>
    private static string Describe(PdfDocument document)
    {
        var tree = document.Internals.Catalog.Elements.GetDictionary("/Pages");
        return string.Join(",", Enumerable.Range(0, document.PageCount).Select(idx =>
        {
            var page = document.Pages[idx];
            var parent = page.Elements.GetDictionary("/Parent");
            return page.Elements.GetString("/PinataTag") + (ReferenceEquals(parent, tree) ? "" : "!parent");
        }));
    }

    private static string ContentOf(PdfPage page) =>
        System.Text.Encoding.Latin1.GetString(page.Contents.CreateSingleContent().Stream.UnfilteredValue);
}
