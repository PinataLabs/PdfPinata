using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   A content stream added to a page of a document read from a file, rather than to one built in
///   memory.
/// </summary>
/// <remarks>
///   empira/PDFsharp#403: upstream's <c>PrependContent</c> registered the new stream with the
///   cross-reference table a second time, after the constructor had already done so, and a document
///   that was reopened, drawn on with <see cref="XGraphicsPdfPageOptions.Prepend"/> and saved
///   failed. This fork never had that bug - <c>PdfObject(PdfDocument)</c> does not register the
///   object, and <c>PdfCrossReferenceTable.Add</c> returns early for one it already holds - so these
///   are a guard against it coming back rather than a test of a fix. Each page is reopened in
///   <see cref="PdfDocumentOpenMode.Modify"/>, the mode that lets a page be drawn on, and read back
///   in <see cref="PdfDocumentOpenMode.Import"/> to count what was written.
/// </remarks>
public class ReopenedPageContentTests
{
    /// <summary>A one-page document with a line drawn on it, read back from its bytes.</summary>
    private static PdfDocument AOnePageDocumentWithALineReopened()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawLine(XPens.Black, 0, 0, 20, 20);

        return document.Reopened();
    }

    private static string TextOf(PdfContent content) =>
        Encoding.ASCII.GetString(content.Stream.UnfilteredValue);

    [Test]
    [Arguments(XGraphicsPdfPageOptions.Prepend, 2)]
    [Arguments(XGraphicsPdfPageOptions.Append, 2)]
    [Arguments(XGraphicsPdfPageOptions.Replace, 1)]
    public void DrawingOnAReopenedPageAddsOneContentStream(XGraphicsPdfPageOptions options, int streams)
    {
        var document = AOnePageDocumentWithALineReopened();
        using (var gfx = XGraphics.FromPdfPage(document.Pages[0], options))
            gfx.DrawRectangle(XBrushes.Yellow, 0, 0, 20, 20);

        var contents = document.Reopened(PdfDocumentOpenMode.Import).Pages[0].Contents;

        contents.Elements.Count.Should().Be(streams);
        var drawn = contents.Cast<PdfContent>().Select(TextOf).ToList();
        var rectangle = options == XGraphicsPdfPageOptions.Prepend ? drawn[0] : drawn[^1];
        rectangle.Should().Contain(" re", "the rectangle is drawn into the stream the option puts it in");
        if (options != XGraphicsPdfPageOptions.Replace)
            drawn.Count(text => text.Contains(" re")).Should().Be(1, "the line's own stream is left as it was");
    }

    [Test]
    public void ContentPrependedAndAppendedToAReopenedPageKeepsItsPlace()
    {
        var document = AOnePageDocumentWithALineReopened();
        var contents = document.Pages[0].Contents;
        var original = TextOf(contents.Cast<PdfContent>().Single());
        contents.PrependContent().CreateStream(Encoding.ASCII.GetBytes("% prepended\n"));
        contents.AppendContent().CreateStream(Encoding.ASCII.GetBytes("% appended\n"));

        var reopened = document.Reopened(PdfDocumentOpenMode.Import).Pages[0].Contents;

        reopened.Cast<PdfContent>().Select(TextOf).Should().Equal("% prepended\n", original, "% appended\n");
    }
}
