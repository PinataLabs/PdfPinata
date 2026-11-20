using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The line state a path is stroked with. Every length is in the coordinates of the path it
/// strokes, and is transformed with it.
/// </summary>
/// <remarks>
/// Immutable, and a class rather than a struct because it carries the dash pattern: the graphics
/// state shares one instance until a line operator replaces it.
/// </remarks>
[Experimental("PPEXP001")]
public sealed class PageStroke
{
    private static readonly double[] s_solid = [];

    internal PageStroke(double width, XLineCap lineCap, XLineJoin lineJoin, double miterLimit, double[] dashPattern,
        double dashPhase)
    {
        Width = width;
        LineCap = lineCap;
        LineJoin = lineJoin;
        MiterLimit = miterLimit;
        _dashPattern = dashPattern;
        DashPhase = dashPhase;
    }

    /// <summary>
    /// The line state content starts with: one unit wide, flat caps, mitred joins with a limit of
    /// ten, and solid.
    /// </summary>
    internal static PageStroke Initial { get; } = new(1, XLineCap.Flat, XLineJoin.Miter, 10, s_solid, 0);

    /// <summary>
    /// The line width, never negative. Zero is the thinnest line the device can draw, and not no
    /// line at all (ISO 32000-1 8.4.3.2).
    /// </summary>
    public double Width { get; }

    /// <summary>The shape of the ends of an open path, and of each dash.</summary>
    public XLineCap LineCap { get; }

    /// <summary>The shape of the corners.</summary>
    public XLineJoin LineJoin { get; }

    /// <summary>
    /// How long a mitred corner may grow, as a multiple of the line width, before it is bevelled.
    /// At least 1.
    /// </summary>
    public double MiterLimit { get; }

    /// <summary>
    /// The lengths of the dashes and the gaps between them, alternately and starting with a dash.
    /// Empty for a solid line. Otherwise an even number of lengths, none negative and not all
    /// zero: a pattern a PDF writes with an odd number of lengths is given here twice over, which
    /// is what repeating it means.
    /// </summary>
    public IReadOnlyList<double> DashPattern => _dashPattern;

    private readonly double[] _dashPattern;

    /// <summary>How far into <see cref="DashPattern"/> the stroke starts.</summary>
    public double DashPhase { get; }

    internal PageStroke WithWidth(double width) => new(width, LineCap, LineJoin, MiterLimit, _dashPattern, DashPhase);

    internal PageStroke WithLineCap(XLineCap lineCap) => new(Width, lineCap, LineJoin, MiterLimit, _dashPattern, DashPhase);

    internal PageStroke WithLineJoin(XLineJoin lineJoin) => new(Width, LineCap, lineJoin, MiterLimit, _dashPattern, DashPhase);

    internal PageStroke WithMiterLimit(double miterLimit) => new(Width, LineCap, LineJoin, miterLimit, _dashPattern, DashPhase);

    /// <summary>
    /// The same line measured in a unit that is <paramref name="unit"/> of this one's long: the
    /// width, the dashes and the phase each divided by it. Null where a length is then too
    /// large to be a number.
    /// </summary>
    internal PageStroke InUnitsOf(double unit)
    {
        var width = Width / unit;
        var phase = DashPhase / unit;
        var pattern = _dashPattern.Length == 0 ? _dashPattern : Array.ConvertAll(_dashPattern, length => length / unit);
        return double.IsFinite(width) && double.IsFinite(phase) && Array.TrueForAll(pattern, double.IsFinite)
            ? new PageStroke(width, LineCap, LineJoin, MiterLimit, pattern, phase)
            : null;
    }

    /// <summary>
    /// The same line with another dash. A pattern with an odd number of lengths is doubled, and
    /// one that is empty or all zeros is a solid line.
    /// </summary>
    internal PageStroke WithDash(double[] pattern, double phase)
    {
        if (pattern.Length == 0 || Array.TrueForAll(pattern, length => length == 0))
            return new PageStroke(Width, LineCap, LineJoin, MiterLimit, s_solid, 0);

        if (pattern.Length % 2 == 1)
        {
            var doubled = new double[pattern.Length * 2];
            pattern.CopyTo(doubled, 0);
            pattern.CopyTo(doubled, pattern.Length);
            pattern = doubled;
        }

        return new PageStroke(Width, LineCap, LineJoin, MiterLimit, pattern, phase);
    }
}
