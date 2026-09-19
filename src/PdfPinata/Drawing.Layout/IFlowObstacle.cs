using System.Collections.Generic;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// Something standing in a block of text that the text has to be laid out around.
/// </summary>
/// <remarks>
/// <para>
/// One method, and it is the whole of what the layout loop ever asks: <i>for a line sitting here,
/// which parts of it do you take?</i> An obstacle is never asked what shape it is, where its middle
/// is, or which side text should go - the side a line takes is a consequence of which runs are left
/// free, not an instruction, which is how the same idea landed in PinataLayout and in iText7
/// independently.
/// </para>
/// <para>
/// That is what makes this worth being an interface rather than a rectangle. A circle, a polygon and
/// an <see cref="XGraphicsPath"/> are implementations of this - flatten, intersect the band with
/// the edges, sort the crossings, pair them - and none of them needs the layout loop to change or
/// the geometry types to grow. Only <see cref="RectangleObstacle"/> ships today; the rest are
/// deliberately not built, not deliberately impossible.
/// </para>
/// <para>
/// Answers may be given in any order, may overlap each other, and may lie outside the block
/// entirely. <see cref="IntervalSet"/> sorts and merges them, so an implementation is free to be
/// simple.
/// </para>
/// </remarks>
public interface IFlowObstacle
{
    /// <summary>
    /// The horizontal runs this obstacle takes out of a line occupying the given band.
    /// </summary>
    /// <returns>
    /// Nothing where the obstacle does not stand in that band at all, which is the ordinary answer
    /// for most lines of most blocks.
    /// </returns>
    IReadOnlyList<XInterval> GetExcludedIntervals(FlowBand band);

    /// <summary>
    /// How far down the obstacle reaches, past which it takes nothing from any line.
    /// </summary>
    /// <remarks>
    /// What a caller with no room left needs in order to do something about it. Without this the
    /// only move is down one line and ask again, which works but makes the number of tries a fact
    /// about the line height rather than about the obstacle.
    /// <para>
    /// It has to include whatever room the obstacle keeps clear around itself, or a line moved to
    /// exactly this depth would still be inside it and the caller would move again for nothing.
    /// </para>
    /// </remarks>
    double Bottom { get; }
}
