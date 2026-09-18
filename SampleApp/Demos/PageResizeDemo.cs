using System.Collections.Generic;
using PdfPinata;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   Builds a two page A4 document whose first page links to a spot on the second, then shrinks the
///   whole thing to A5.
///   <para>
///     The point of the example is what does <b>not</b> have to be done. The content is scaled
///     rather than cropped, and the link goes on pointing at the same words - the destination is
///     found and moved even though it is held on a different page from the one being resized.
///   </para>
/// </summary>
internal sealed class PageResizeDemo : PdfDemo
{
    public PageResizeDemo() : base() { }

    public override string Name => "PageResize";

    public override string Summary => "Shrinking a finished document, links and destinations with it.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Two A4 pages resized to A5 in one call",
        "Content scaled rather than cropped",
        "A link and the destination it points at moving with the words",
    };

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        PdfDocument document = new PdfDocument();
        XFont font = new XFont("Liberation Sans", 14);

        PdfPage first = document.AddPage();
        first.Size = PageSize.A4;
        PdfPage second = document.AddPage();
        second.Size = PageSize.A4;

        using (XGraphics gfx = XGraphics.FromPdfPage(first))
            gfx.DrawString("Go to chapter two", font, XBrushes.Blue, new XPoint(60, 100));

        using (XGraphics gfx = XGraphics.FromPdfPage(second))
            gfx.DrawString("Chapter two", font, XBrushes.Black, new XPoint(60, 100));

        // A link on page one, pointing a third of the way down page two.
        first.AddDocumentLink(
            new PdfRectangle(new XPoint(60, first.Height - 115), new XPoint(220, first.Height - 95)),
            destinationPage: 2,
            destinationTop: second.Height - 90);

        // One call. The drawing on both pages is scaled to 70.6%, the link rectangle shrinks with
        // the words underneath it, and the destination it points at moves to where those words
        // ended up. Setting page.Size on a page that has been drawn on now throws and names
        // Resize, because writing a new media box would crop the page and leave the link behind.
        document.ResizePages(PageSize.A5);
        #endregion

        return document;
    }
}
