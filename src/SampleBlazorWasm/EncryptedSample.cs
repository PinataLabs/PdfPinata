using System;
using System.IO;
using PdfPinata.Pdf.IO;

namespace SampleBlazorWasm;

/// <summary>
/// Opens the AES-256 document the app ships, and says what happened.
/// </summary>
/// <remarks>
/// <para>
/// .NET on WebAssembly provides no AES: <c>Aes.Create()</c> throws there. PdfPinata catches that and
/// throws a <see cref="NotSupportedException"/> naming the document's encryption instead, from
/// <see cref="PdfReader.Open(Stream, string, PdfDocumentOpenMode)"/>, which is where every object is
/// decrypted. In the browser this therefore answers that message. Anywhere the runtime has AES the
/// document opens, which is what the tests check beside the refusal.
/// </para>
/// <para>
/// PdfPinata reads AES but writes only RC4, so the document was made with qpdf 12.4.2 from a one-page
/// PDF set in Helvetica:
/// <c>qpdf --encrypt --user-password=pinata --owner-password=pinata-owner --bits=256 -- plain.pdf aes-256.pdf</c>
/// (revision 6, AESV3 for strings, streams and embedded files).
/// </para>
/// </remarks>
public static class EncryptedSample
{
    /// <summary>Where the app serves the document from, relative to its base address.</summary>
    public const string Url = "samples/aes-256.pdf";

    /// <summary>The document's user password.</summary>
    public const string Password = "pinata";

    /// <summary>
    /// Opens <paramref name="pdf"/> with <see cref="Password"/> and answers what a reader of the page
    /// should be told: how many pages it has, or why it could not be opened.
    /// </summary>
    public static string TryOpen(byte[] pdf)
    {
        try
        {
            var document = PdfReader.Open(new MemoryStream(pdf), Password, PdfDocumentOpenMode.Import);
            var pages = document.PageCount;
            return $"Opened the AES-256 document: {pages} {(pages == 1 ? "page" : "pages")}.";
        }
        catch (NotSupportedException ex)
        {
            return ex.Message;
        }
    }
}
