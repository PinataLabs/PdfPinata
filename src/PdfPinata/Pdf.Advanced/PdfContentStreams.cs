using System;
using System.Collections.Generic;
using PdfPinata.Internal;
using PdfPinata.Pdf.Filters;

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
    /// <param name="maximumLength">
    /// The most bytes of content to inflate, across all of the page's streams. A page whose
    /// filters would produce more is drawn from the start of it.
    /// </param>
    /// <param name="content">The streams that could be read, as one.</param>
    /// <param name="unreadable">How many of several streams could not be read.</param>
    /// <param name="truncated">Whether the cap cut the content short, or left a stream out.</param>
    /// <param name="charged">How much of the cap the page's streams used, counting what was inflated and thrown away.</param>
    /// <returns>False where the page is written as one stream and it cannot be read.</returns>
    internal static bool TryGetReadablePageContent(PdfPage page, long maximumLength, out byte[] content,
        out int unreadable, out bool truncated, out long charged)
    {
        unreadable = 0;
        truncated = false;
        charged = 0;
        var item = PdfReference.Dereference(page.Elements[PdfPage.Keys.Contents]);
        if (item == null)
        {
            content = [];
            return true;
        }

        if (item is not PdfArray streams)
            return TryGetContent(item as PdfDictionary, maximumLength, out content, out truncated, out charged);

        var parts = new List<byte[]>();
        var remaining = maximumLength;
        for (var idx = 0; idx < streams.Elements.Count; idx++)
        {
            if (remaining <= 0)
            {
                truncated = true;
                break;
            }

            var charge = 0L;
            if (TryGetStream(streams, idx, out var stream)
                && TryGetContent(stream, remaining, out var part, out var cut, out charge))
            {
                parts.Add(part);
                truncated |= cut;
                remaining -= charge + 1;
            }
            else
            {
                unreadable++;
                remaining -= charge;
            }
        }

        charged = maximumLength - remaining;
        content = Join(parts);
        return true;
    }

    /// <summary>
    /// One of the streams a page is written in. A document opened to read its objects as they
    /// are asked for reads the stream here, and a stream that cannot be read is one that cannot
    /// be drawn, not a page that cannot.
    /// </summary>
    private static bool TryGetStream(PdfArray streams, int index, out PdfDictionary stream)
    {
        try
        {
            stream = streams.Elements.GetDictionary(index);
            return stream != null;
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            stream = null;
            return false;
        }
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
    internal static bool TryGetContent(PdfDictionary stream, out byte[] content) =>
        TryGetContent(stream, long.MaxValue, out content, out _, out _);

    /// <summary>
    /// The content of a single stream, with its filters undone, at most
    /// <paramref name="maximumLength"/> bytes of it; <paramref name="truncated"/> says whether
    /// it was cut there, and <paramref name="charge"/> how much of a shared budget it used: what
    /// the filters inflated, which a predictor or a failure can leave larger than the content.
    /// </summary>
    internal static bool TryGetContent(PdfDictionary stream, long maximumLength, out byte[] content,
        out bool truncated, out long charge)
    {
        content = null;
        truncated = false;
        charge = 0;
        if (stream == null || stream.Stream == null)
            return false;

        var limit = maximumLength == long.MaxValue ? null : new DecodeLimit(maximumLength);
        try
        {
            content = stream.Stream.GetUnfilteredValue(limit);
            truncated = limit is { Truncated: true };
            charge = Math.Max(limit?.Produced ?? 0, content.Length);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // A filter that cannot be undone leaves the content unreadable.
            charge = limit?.Produced ?? 0;
            return false;
        }

        return content != null;
    }
}
