using System.IO;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

namespace PinataLayout.Rendering.Tests.Helpers;

/// <summary>
///   Lays a document out and hands back the result as a reader sees it.
/// </summary>
/// <remarks>
///   Saved and reopened rather than read off the renderer, so that what the assertions look at is
///   the content stream as it was written rather than as it stood while it was being built.
///
///   A document may be laid out once and once only - rendering rewrites its internal structure, and
///   binding a second renderer to it throws. So a test comparing two layouts builds two documents
///   rather than rendering one twice, which is why the arrangements below are written as a function
///   of the thing being varied instead of as a document set up once in a field.
/// </remarks>
internal static class Rendered
{
    /// <summary>The document, laid out and read back.</summary>
    internal static PdfDocument Of(Document document)
    {
        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        return PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
    }

    /// <summary>The first page of the document, for the assertions that only need one.</summary>
    internal static PdfPage FirstPageOf(Document document) => Of(document).Pages[0];
}
