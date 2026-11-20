using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>What one element of a <see cref="PagePath"/> does, and how many points it takes.</summary>
[Experimental("PPEXP001")]
public enum PagePathVerb : byte
{
    /// <summary>Begins a figure at one point.</summary>
    MoveTo,

    /// <summary>Draws a straight line to one point.</summary>
    LineTo,

    /// <summary>Draws a cubic Bézier curve: two control points, then the point it ends at.</summary>
    CubicTo,

    /// <summary>Closes the figure back to where it began. Takes no point.</summary>
    Close
}

/// <summary>
/// An immutable path: verbs and the points they take, in a coordinate space that the call handing
/// the path over names with its transform.
/// </summary>
/// <remarks>
/// <para>
/// Curves are cubic only. PDF has no quadratic operator, and the quadratic of a TrueType outline
/// is exactly a cubic, which <see cref="PagePathBuilder.QuadraticTo"/> works out. A rectangle is
/// four lines and a close, as ISO 32000-1 Table 59 defines <c>re</c>.
/// </para>
/// <para>
/// This is not <c>XGlyphOutline</c>, which promises points already scaled to a type size and has
/// no identity a device could cache on.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public sealed class PagePath
{
    private readonly PagePathVerb[] _verbs;
    private readonly XPoint[] _points;

    internal PagePath(PagePathVerb[] verbs, XPoint[] points, bool isShared)
    {
        _verbs = verbs;
        _points = points;
        IsShared = isShared;
    }

    /// <summary>A path with nothing in it: the outline of a glyph that draws nothing, such as a space.</summary>
    public static PagePath Empty { get; } = new([], [], false);

    /// <summary>The verbs, in the order they are drawn.</summary>
    public ReadOnlySpan<PagePathVerb> Verbs => _verbs;

    /// <summary>
    /// The points the verbs take, in order: one for <see cref="PagePathVerb.MoveTo"/> and
    /// <see cref="PagePathVerb.LineTo"/>, three for <see cref="PagePathVerb.CubicTo"/> and none for
    /// <see cref="PagePathVerb.Close"/>.
    /// </summary>
    public ReadOnlySpan<XPoint> Points => _points;

    /// <summary>Whether the path has no verbs at all.</summary>
    public bool IsEmpty => _verbs.Length == 0;

    /// <summary>
    /// Whether this instance will be handed over again unchanged: true for a glyph outline, which
    /// is the same instance each time that glyph of that font is drawn, and false for a path
    /// built from the content's own operators, which is made once and painted once.
    /// </summary>
    /// <remarks>
    /// A device that builds something of its own from a path may keep it against a shared
    /// instance, weakly, because it does not own the path.
    /// </remarks>
    public bool IsShared { get; }

    /// <summary>The same verbs and points, as an instance that says it is handed over again.</summary>
    internal PagePath Shared() => IsShared || IsEmpty ? this : new PagePath(_verbs, _points, true);

    /// <summary>A copy with every point mapped through a matrix.</summary>
    internal PagePath Transformed(XMatrix matrix)
    {
        var points = (XPoint[])_points.Clone();
        matrix.TransformPoints(points);
        return new PagePath(_verbs, points, false);
    }
}

/// <summary>
/// Builds a <see cref="PagePath"/>. A <see cref="PageGlyphSource"/> builds its outlines with one.
/// </summary>
/// <remarks>
/// A line or a curve drawn where no figure is open begins one, at the point the last figure
/// began at if one was closed and otherwise at the point it is drawn to. So whatever is
/// built, every figure of the path begins with a <see cref="PagePathVerb.MoveTo"/>.
/// </remarks>
[Experimental("PPEXP001")]
public sealed class PagePathBuilder
{
    private readonly List<PagePathVerb> _verbs = [];
    private readonly List<XPoint> _points = [];
    private XPoint _figureStart;
    private XPoint _current;
    private bool _figureOpen;
    private bool _hasCurrentPoint;

    /// <summary>Whether anything has been drawn that leaves a point to draw on from.</summary>
    public bool HasCurrentPoint => _hasCurrentPoint;

    /// <summary>The point the next line or curve starts from. Meaningless without one.</summary>
    public XPoint CurrentPoint => _current;

    /// <summary>Begins a figure at a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public void MoveTo(double x, double y)
    {
        // A figure begun and at once begun again has drawn nothing; the second beginning stands.
        if (_verbs.Count > 0 && _verbs[^1] == PagePathVerb.MoveTo)
        {
            _points[^1] = new XPoint(x, y);
        }
        else
        {
            _verbs.Add(PagePathVerb.MoveTo);
            _points.Add(new XPoint(x, y));
        }

        _figureStart = _current = new XPoint(x, y);
        _figureOpen = _hasCurrentPoint = true;
    }

    /// <summary>Draws a straight line to a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    public void LineTo(double x, double y)
    {
        EnsureFigure(x, y);
        _verbs.Add(PagePathVerb.LineTo);
        _points.Add(_current = new XPoint(x, y));
    }

    /// <summary>Draws a cubic Bézier curve.</summary>
    /// <param name="x1">The x coordinate of the first control point.</param>
    /// <param name="y1">The y coordinate of the first control point.</param>
    /// <param name="x2">The x coordinate of the second control point.</param>
    /// <param name="y2">The y coordinate of the second control point.</param>
    /// <param name="x3">The x coordinate of the point the curve ends at.</param>
    /// <param name="y3">The y coordinate of the point the curve ends at.</param>
    public void CubicTo(double x1, double y1, double x2, double y2, double x3, double y3)
    {
        EnsureFigure(x1, y1);
        _verbs.Add(PagePathVerb.CubicTo);
        _points.Add(new XPoint(x1, y1));
        _points.Add(new XPoint(x2, y2));
        _points.Add(_current = new XPoint(x3, y3));
    }

    /// <summary>
    /// Draws a quadratic Bézier curve, as the cubic curve that traces exactly the same line.
    /// </summary>
    /// <param name="x1">The x coordinate of the control point.</param>
    /// <param name="y1">The y coordinate of the control point.</param>
    /// <param name="x2">The x coordinate of the point the curve ends at.</param>
    /// <param name="y2">The y coordinate of the point the curve ends at.</param>
    public void QuadraticTo(double x1, double y1, double x2, double y2)
    {
        EnsureFigure(x1, y1);
        const double twoThirds = 2.0 / 3.0;
        CubicTo(
            _current.X + twoThirds * (x1 - _current.X), _current.Y + twoThirds * (y1 - _current.Y),
            x2 + twoThirds * (x1 - x2), y2 + twoThirds * (y1 - y2),
            x2, y2);
    }

    /// <summary>
    /// Draws a rectangle as a figure of its own, closed, leaving the current point at the corner
    /// it began at.
    /// </summary>
    /// <param name="x">The x coordinate of one corner.</param>
    /// <param name="y">The y coordinate of that corner.</param>
    /// <param name="width">The width, which may be negative.</param>
    /// <param name="height">The height, which may be negative.</param>
    public void AddRectangle(double x, double y, double width, double height)
    {
        MoveTo(x, y);
        LineTo(x + width, y);
        LineTo(x + width, y + height);
        LineTo(x, y + height);
        Close();
    }

    /// <summary>
    /// Closes the open figure with a straight line back to where it began. Does nothing where no
    /// figure is open.
    /// </summary>
    public void Close()
    {
        if (!_figureOpen)
            return;

        _verbs.Add(PagePathVerb.Close);
        _current = _figureStart;
        _figureOpen = false;
    }

    /// <summary>Adds every figure of another path, each point mapped through a matrix.</summary>
    /// <param name="path">The path to add.</param>
    /// <param name="transform">Maps the path's coordinates to this builder's.</param>
    public void Append(PagePath path, XMatrix transform)
    {
        ArgumentNullException.ThrowIfNull(path);

        var points = path.Points;
        var at = 0;
        foreach (var verb in path.Verbs)
        {
            switch (verb)
            {
                case PagePathVerb.MoveTo:
                    var start = transform.Transform(points[at++]);
                    MoveTo(start.X, start.Y);
                    break;

                case PagePathVerb.LineTo:
                    var end = transform.Transform(points[at++]);
                    LineTo(end.X, end.Y);
                    break;

                case PagePathVerb.CubicTo:
                    var control1 = transform.Transform(points[at++]);
                    var control2 = transform.Transform(points[at++]);
                    var last = transform.Transform(points[at++]);
                    CubicTo(control1.X, control1.Y, control2.X, control2.Y, last.X, last.Y);
                    break;

                default:
                    Close();
                    break;
            }
        }
    }

    /// <summary>
    /// Maps every point drawn so far through a matrix, the point the next line starts from
    /// among them.
    /// </summary>
    internal void Transform(XMatrix matrix)
    {
        for (var index = 0; index < _points.Count; index++)
            _points[index] = matrix.Transform(_points[index]);

        _figureStart = matrix.Transform(_figureStart);
        _current = matrix.Transform(_current);
    }

    /// <summary>
    /// The path built so far. The builder can go on being added to, and what is added later is
    /// not in the path this answered.
    /// </summary>
    public PagePath ToPath()
    {
        var verbs = _verbs.Count;

        // A figure begun and never drawn in paints nothing, and is left out.
        if (verbs > 0 && _verbs[verbs - 1] == PagePathVerb.MoveTo)
            verbs--;

        if (verbs == 0)
            return PagePath.Empty;

        return new PagePath(_verbs.GetRange(0, verbs).ToArray(),
            _points.GetRange(0, verbs == _verbs.Count ? _points.Count : _points.Count - 1).ToArray(), false);
    }

    /// <summary>
    /// Opens a figure for a line or curve drawn where none is open: at the point the last one
    /// began at when one was closed, and otherwise at the point given.
    /// </summary>
    private void EnsureFigure(double x, double y)
    {
        if (_figureOpen)
            return;

        if (_hasCurrentPoint)
            MoveTo(_figureStart.X, _figureStart.Y);
        else
            MoveTo(x, y);
    }
}
