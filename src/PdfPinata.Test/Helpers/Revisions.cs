using System;
using System.IO;
using System.Text;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   A revision appended to a document, and the bytes it appended - for the tests about
///   incremental updates, which all ask what <see cref="PdfDocument.SaveIncremental(Stream)"/>
///   wrote after the bytes that were already there.
/// </summary>
internal static class Revisions
{
    /// <summary>
    ///   <paramref name="original"/>, opened in <see cref="PdfDocumentOpenMode.Append"/>, changed by
    ///   <paramref name="change"/> and saved incrementally: the original bytes followed by one new
    ///   revision.
    /// </summary>
    internal static byte[] AppendChange(byte[] original, Action<PdfDocument> change)
    {
        using var source = new MemoryStream(original);
        var document = global::PdfPinata.Pdf.IO.PdfReader.Open(source, PdfDocumentOpenMode.Append);

        change(document);

        using var output = new MemoryStream();
        document.SaveIncremental(output);
        return output.ToArray();
    }

    /// <summary>What was appended, and nothing that was there before.</summary>
    internal static string Appended(byte[] updated, int originalLength) =>
        Encoding.Latin1.GetString(updated, originalLength, updated.Length - originalLength);
}
