using System.Collections.Generic;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Hands back the bytes of a page's content stream, however the page happens to store them.
/// </summary>
internal static class PageContent
{
    internal static byte[] Of(PdfPage page)
    {
        var item = page.Elements["/Contents"];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfArray streams)
        {
            var parts = new List<byte[]>();
            for (var idx = 0; idx < streams.Elements.Count; idx++)
                parts.Add(streams.Elements.GetDictionary(idx).Stream.UnfilteredValue);

            // The streams of a page are one stream broken up, and a token may span the break.
            var joined = new List<byte>();
            foreach (var part in parts)
            {
                joined.AddRange(part);
                joined.Add((byte)'\n');
            }
            return joined.ToArray();
        }

        return ((PdfDictionary)item).Stream.UnfilteredValue;
    }
}
