using System;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// How much work one render may spend reading shadings: evaluating their functions, making
/// their triangles and decoding the streams of both, counted across every shading of the page,
/// however often each is read.
/// </summary>
/// <remarks>
/// A limit on one shading does not bound a page, which may hold a thousand shadings that each
/// stay inside it, or paint one shading a thousand times that is read each time. Once either
/// budget is spent, a shading being read is not drawn, and none is read after it.
/// </remarks>
internal sealed class ShadingWork
{
    /// <summary>
    /// The work one render spends: about a second of calculator programs, or two million
    /// triangles. Far past a real page, whose functions are a few steps each.
    /// </summary>
    internal const long Maximum = 1L << 27;

    /// <summary>What a triangle of a mesh costs, against a step of a function.</summary>
    internal const int TriangleCost = 64;

    /// <summary>
    /// The bytes one render decodes from the streams of functions and meshes, as much as it
    /// inflates of a page's content. Every sample table a render keeps was decoded against it,
    /// so it bounds what the render holds too: eight of the largest tables a function may have.
    /// </summary>
    internal const long MaximumBytes = 128L << 20;

    private long _left = Maximum;
    private long _bytesLeft = MaximumBytes;

    internal bool IsSpent => _left < 0 || _bytesLeft < 0;

    internal void Spend(long work) => _left -= work;

    /// <summary>
    /// A function's or a mesh's stream, with its filters undone as far as
    /// <paramref name="most"/> bytes, which are charged to the render.
    /// </summary>
    /// <returns>
    /// At most <paramref name="most"/> bytes, or null where the stream cannot be decoded or
    /// where the render has decoded all it may, which <see cref="IsSpent"/> then says.
    /// </returns>
    internal byte[] Decode(PdfDictionary stream, long most)
    {
        if (IsSpent)
            return null;

        var limit = Math.Min(most, _bytesLeft);
        var read = PdfContentStreams.TryGetContent(stream, limit, tolerateDamage: false, out var content, out var truncated, out _, out var charge);
        _bytesLeft -= charge;
        if (!read)
            return null;

        // A stream with no filter is the file's own bytes, which are not cut by the decode.
        if (content.Length > limit)
        {
            truncated = true;
            Array.Resize(ref content, (int)limit);
        }

        // Cut short by what the render had left, not by what the stream may be.
        if (truncated && limit < most)
        {
            _bytesLeft = -1;
            return null;
        }

        return content;
    }
}
