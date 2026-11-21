namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// How much work one render may spend reading shadings: evaluating their functions and making
/// their triangles, counted across every shading of the page, however often each is read.
/// </summary>
/// <remarks>
/// A limit on one shading does not bound a page, which may hold a thousand shadings that each
/// stay inside it, or paint one shading a thousand times that is read each time. Once this is
/// spent, a shading being read is not drawn, and none is read after it.
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

    private long _left = Maximum;

    internal bool IsSpent => _left < 0;

    internal void Spend(long work) => _left -= work;
}
