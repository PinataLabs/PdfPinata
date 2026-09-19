using System;
using System.Collections.Generic;
using PdfPinata.Internal;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Gets at the bytes of the content streams a page and the things it draws are written in.
/// </summary>
internal static class PdfContentStreams
{
    /// <summary>
    /// The content of a page, whether it is written as one stream or as several.
    /// </summary>
    internal static bool TryGetPageContent(PdfPage page, out byte[] content)
    {
        content = null;

        PdfItem item = page.Elements[PdfPage.Keys.Contents];
        if (item is PdfReference)
            item = ((PdfReference)item).Value;

        if (item == null)
        {
            // A page that draws nothing draws nothing anywhere.
            content = Array.Empty<byte>();
            return true;
        }

        PdfArray streams = item as PdfArray;
        if (streams == null)
            return TryGetContent(item as PdfDictionary, out content);

        // The streams of a page are one stream broken up, and a token may span the break, so
        // they are read as one with a separator where each break was.
        List<byte[]> parts = new List<byte[]>();
        int length = 0;
        for (int idx = 0; idx < streams.Elements.Count; idx++)
        {
            byte[] part;
            if (!TryGetContent(streams.Elements.GetDictionary(idx), out part))
                return false;

            parts.Add(part);
            length += part.Length + 1;
        }

        content = new byte[length];
        int at = 0;
        foreach (byte[] part in parts)
        {
            part.CopyTo(content, at);
            at += part.Length;
            content[at++] = (byte)'\n';
        }

        return true;
    }

    /// <summary>
    /// The content of a single stream, with its filters undone.
    /// </summary>
    internal static bool TryGetContent(PdfDictionary stream, out byte[] content)
    {
        content = null;
        if (stream == null || stream.Stream == null)
            return false;

        try
        {
            content = stream.Stream.UnfilteredValue;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // A filter that cannot be undone leaves the content unreadable.
            return false;
        }

        return content != null;
    }
}
