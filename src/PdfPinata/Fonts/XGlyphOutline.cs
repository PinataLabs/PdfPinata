using System;
using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Fonts;

/// <summary>
/// What one segment of a glyph's outline does.
/// </summary>
public enum XGlyphSegmentKind
{
    /// <summary>Begins a figure at <see cref="XGlyphSegment.End"/>.</summary>
    Start,

    /// <summary>Draws a straight line to <see cref="XGlyphSegment.End"/>.</summary>
    Line,

    /// <summary>
    /// Draws a cubic Bézier curve to <see cref="XGlyphSegment.End"/> through
    /// <see cref="XGlyphSegment.Control1"/> and <see cref="XGlyphSegment.Control2"/>.
    /// </summary>
    Curve,

    /// <summary>Closes the figure back to where it began. Carries no point.</summary>
    Close
}

/// <summary>
/// One segment of a glyph's outline.
/// </summary>
/// <remarks>
/// Cubic curves only. A TrueType outline is drawn with quadratics, and a quadratic converts to a
/// cubic exactly - controls at <c>p0 + 2/3(q - p0)</c> and <c>p2 + 2/3(q - p2)</c> - so a backend
/// converts rather than subdividing and losing precision. PDF has no quadratic curve operator, so
/// the conversion has to happen somewhere regardless.
/// </remarks>
public readonly struct XGlyphSegment
{
    private XGlyphSegment(XGlyphSegmentKind kind, XPoint control1, XPoint control2, XPoint end)
    {
        Kind = kind;
        Control1 = control1;
        Control2 = control2;
        End = end;
    }

    /// <summary>What this segment does.</summary>
    public XGlyphSegmentKind Kind { get; }

    /// <summary>The first control point of a curve. Meaningless for any other kind.</summary>
    public XPoint Control1 { get; }

    /// <summary>The second control point of a curve. Meaningless for any other kind.</summary>
    public XPoint Control2 { get; }

    /// <summary>Where the segment ends. Meaningless for <see cref="XGlyphSegmentKind.Close"/>.</summary>
    public XPoint End { get; }

    /// <summary>Begins a figure at a point.</summary>
    public static XGlyphSegment StartAt(XPoint point) =>
        new(XGlyphSegmentKind.Start, point, point, point);

    /// <summary>Draws a straight line to a point.</summary>
    public static XGlyphSegment LineTo(XPoint point) =>
        new(XGlyphSegmentKind.Line, point, point, point);

    /// <summary>Draws a cubic Bézier curve to a point through two control points.</summary>
    public static XGlyphSegment CurveTo(XPoint control1, XPoint control2, XPoint end) =>
        new(XGlyphSegmentKind.Curve, control1, control2, end);

    /// <summary>Closes the figure back to where it began.</summary>
    public static XGlyphSegment Close() =>
        new(XGlyphSegmentKind.Close, new XPoint(), new XPoint(), new XPoint());
}

/// <summary>
/// The outline of one glyph, as the figures it is drawn from.
/// </summary>
/// <remarks>
/// The points are in <b>points</b>, already scaled to the em size that was asked for, and in the
/// font's own convention: the origin sits on the baseline at the glyph's own starting pen
/// position, and <b>y increases upwards</b>. A run of text hands back one of these per glyph, each
/// already advanced along the run, so the caller has one origin to place rather than one per
/// glyph.
/// <para>
/// The flip onto a page whose y increases downwards is the caller's, deliberately: it is done once
/// in <see cref="PdfPinata.Drawing.XGraphicsPath.AddString(string,XFontFamily,XFontStyle,double,XPoint,XStringFormat)"/>
/// rather than once per backend, so two backends cannot disagree about which way up a glyph goes.
/// </para>
/// </remarks>
public sealed class XGlyphOutline
{
    /// <summary>
    /// Initializes a glyph outline from its segments.
    /// </summary>
    public XGlyphOutline(IEnumerable<XGlyphSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        Segments = new List<XGlyphSegment>(segments);
    }

    /// <summary>
    /// The segments of the outline, in the order they are drawn. Empty for a glyph that has no
    /// outline at all - a space, or a character the font draws with a bitmap.
    /// </summary>
    public IReadOnlyList<XGlyphSegment> Segments { get; }
}
