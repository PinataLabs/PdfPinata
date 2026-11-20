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

        var item = PdfReference.Dereference(page.Elements[PdfPage.Keys.Contents]);

        if (item == null)
        {
            // A page that draws nothing draws nothing anywhere.
            content = [];
            return true;
        }

        if (item is not PdfArray streams)
            return TryGetContent(item as PdfDictionary, out content);

        var parts = new List<byte[]>();
        for (var idx = 0; idx < streams.Elements.Count; idx++)
        {
            if (!TryGetContent(streams.Elements.GetDictionary(idx), out var part))
                return false;

            parts.Add(part);
        }

        content = Join(parts);
        return true;
    }

    /// <summary>
    /// The content of a page for drawing it: where the page is written as several streams, those
    /// of them that can be read, with a count of those that cannot.
    /// </summary>
    /// <remarks>
    /// <see cref="TryGetPageContent(PdfPage, out byte[])"/> gives up the whole page for one
    /// stream, which is right for a reader that must not answer half of what a page says. A
    /// page being drawn is better drawn in part, with the part left out reported.
    /// </remarks>
    /// <param name="page">The page.</param>
    /// <param name="content">The streams that could be read, as one.</param>
    /// <param name="unreadable">How many of several streams could not be read.</param>
    /// <returns>False where the page is written as one stream and it cannot be read.</returns>
    internal static bool TryGetReadablePageContent(PdfPage page, out byte[] content, out int unreadable)
    {
        unreadable = 0;
        if (PdfReference.Dereference(page.Elements[PdfPage.Keys.Contents]) is not PdfArray streams)
            return TryGetPageContent(page, out content);

        var parts = new List<byte[]>();
        for (var idx = 0; idx < streams.Elements.Count; idx++)
        {
            if (TryGetContent(streams.Elements.GetDictionary(idx), out var part))
                parts.Add(part);
            else
                unreadable++;
        }

        content = Join(parts);
        return true;
    }

    /// <summary>
    /// The streams of a page are one stream broken up, and a token may span the break, so
    /// they are read as one with a separator where each break was.
    /// </summary>
    private static byte[] Join(List<byte[]> parts)
    {
        var length = 0;
        foreach (var part in parts)
            length += part.Length + 1;

        var content = new byte[length];
        var at = 0;
        foreach (var part in parts)
        {
            part.CopyTo(content, at);
            at += part.Length;
            content[at++] = (byte)'\n';
        }

        return content;
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
