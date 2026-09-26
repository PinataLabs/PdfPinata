using System.IO;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   A document written out and, for most tests, read straight back in - which is where a reader
///   meets it, and so where anything the writer got wrong first shows.
/// </summary>
/// <remarks>
///   The reader is named in full below because this assembly has two test classes called
///   <c>PdfReader</c> of its own, and inside <c>PdfPinata.Test</c> either one wins over the
///   library's. That clash is why so many test files carried a <c>using Reader = …</c> alias, and
///   why a helper here needs none.
///
///   <see cref="Open"/> hands the reader a stream it never disposes. The reader takes what it
///   needs in every mode - <see cref="PdfDocumentOpenMode.Append"/> copies the original bytes
///   rather than keeping the stream - and a <see cref="MemoryStream"/> over an array holds nothing
///   a finalizer needs to free, so leaving it to the collector costs nothing.
/// </remarks>
internal static class Saved
{
    /// <summary>The bytes <paramref name="document"/> saves to.</summary>
    internal static byte[] Bytes(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    /// <summary>A document read from <paramref name="bytes"/>, in <paramref name="mode"/>.</summary>
    internal static PdfDocument Open(byte[] bytes, PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify) =>
        global::PdfPinata.Pdf.IO.PdfReader.Open(new MemoryStream(bytes), mode);

    /// <summary><paramref name="document"/>, saved and read back in <paramref name="mode"/>.</summary>
    internal static PdfDocument Reopened(this PdfDocument document, PdfDocumentOpenMode mode = PdfDocumentOpenMode.Modify) =>
        Open(Bytes(document), mode);
}
