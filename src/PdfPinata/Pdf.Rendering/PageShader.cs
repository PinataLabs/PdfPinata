using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// A colour for every point of a plane, and none where it is transparent: a shading or a pattern
/// as a device paints with it.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. The classes derived from it here are all there are: <see cref="PageAxialShading"/>,
/// <see cref="PageRadialShading"/>, <see cref="PageMeshShading"/> and <see cref="PageTiling"/>.
/// </para>
/// <para>
/// A shader says nothing of where it lies on the device. The <see cref="PageShaderPaint"/> it is
/// handed over in carries the transform, so one shader serves every place it is painted.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public abstract class PageShader
{
    private protected PageShader(bool isShared, PagePaint? fallback)
    {
        IsShared = isShared;
        Fallback = fallback;
    }

    /// <summary>
    /// Whether this instance is handed over again each time the content paints with the same
    /// shading or pattern. A device keeps what it builds from a shared shader, as it does for a
    /// glyph's <see cref="PagePath"/>, and builds anything else for the one call.
    /// </summary>
    public bool IsShared { get; }

    /// <summary>
    /// The one colour painted in place of the shader on a device that cannot draw it, or null
    /// where nothing is.
    /// </summary>
    internal PagePaint? Fallback { get; }
}

/// <summary>A colour at a place along a gradient.</summary>
[Experimental("PPEXP001")]
public readonly struct PageGradientStop
{
    internal PageGradientStop(double offset, PagePaint color)
    {
        Offset = offset;
        Color = color;
    }

    /// <summary>Where along the gradient, from 0 at its start to 1 at its end.</summary>
    public double Offset { get; }

    /// <summary>The colour there, as RGB. Its alpha is 1.</summary>
    public PagePaint Color { get; }
}

/// <summary>
/// A shading whose colour runs along one parameter: the stops, and what lies beyond each end.
/// </summary>
[Experimental("PPEXP001")]
public abstract class PageGradient : PageShader
{
    private readonly PageGradientStop[] _stops;

    private protected PageGradient(PageGradientStop[] stops, bool extendStart, bool extendEnd,
        PagePaint? background, bool isShared) : base(isShared, MeanOf(stops))
    {
        _stops = stops;
        ExtendStart = extendStart;
        ExtendEnd = extendEnd;
        Background = background;
    }

    /// <summary>
    /// The colours along the gradient, at least two, the first at 0 and the last at 1. The
    /// shading's function, its <c>/Domain</c> and its colour space are already in them. Offsets
    /// never decrease, and two stops at one offset are a hard edge. Between two stops the colour
    /// runs in a straight line.
    /// </summary>
    public IReadOnlyList<PageGradientStop> Stops => _stops;

    /// <summary>
    /// Whether the first stop's colour carries on before the start. Where it does not, the shader
    /// is transparent there.
    /// </summary>
    public bool ExtendStart { get; }

    /// <summary>Whether the last stop's colour carries on past the end.</summary>
    public bool ExtendEnd { get; }

    /// <summary>
    /// The colour under the gradient wherever the gradient is transparent, or null for none. It
    /// and the gradient are one paint: the alpha of a <see cref="PageShaderPaint"/> applies once
    /// to the two together, and not to each in turn.
    /// </summary>
    public PagePaint? Background { get; }

    // The colour of the whole ramp, each stretch counted by its length.
    private static PagePaint MeanOf(PageGradientStop[] stops)
    {
        double red = 0, green = 0, blue = 0;
        for (var index = 1; index < stops.Length; index++)
        {
            var length = (stops[index].Offset - stops[index - 1].Offset) / 2;
            red += length * (stops[index].Color.Red + stops[index - 1].Color.Red);
            green += length * (stops[index].Color.Green + stops[index - 1].Color.Green);
            blue += length * (stops[index].Color.Blue + stops[index - 1].Color.Blue);
        }

        return new PagePaint(red, green, blue);
    }
}

/// <summary>
/// An axial shading (shading type 2): the colour runs along a line and is the same all the way
/// across it.
/// </summary>
[Experimental("PPEXP001")]
public sealed class PageAxialShading : PageGradient
{
    internal PageAxialShading(XPoint start, XPoint end, PageGradientStop[] stops, bool extendStart, bool extendEnd,
        PagePaint? background, bool isShared) : base(stops, extendStart, extendEnd, background, isShared)
    {
        Start = start;
        End = end;
    }

    /// <summary>Where the gradient starts, in the shader's coordinates.</summary>
    public XPoint Start { get; }

    /// <summary>Where the gradient ends. Never <see cref="Start"/>.</summary>
    public XPoint End { get; }
}

/// <summary>
/// A radial shading (shading type 3): the circles between a starting circle and an ending one,
/// each in the colour of its place along the gradient, later circles over earlier ones
/// (ISO 32000-1 8.7.4.5.4).
/// </summary>
[Experimental("PPEXP001")]
public sealed class PageRadialShading : PageGradient
{
    internal PageRadialShading(XPoint startCenter, double startRadius, XPoint endCenter, double endRadius,
        PageGradientStop[] stops, bool extendStart, bool extendEnd, PagePaint? background, bool isShared)
        : base(stops, extendStart, extendEnd, background, isShared)
    {
        StartCenter = startCenter;
        StartRadius = startRadius;
        EndCenter = endCenter;
        EndRadius = endRadius;
    }

    /// <summary>The centre of the starting circle, in the shader's coordinates.</summary>
    public XPoint StartCenter { get; }

    /// <summary>The radius of the starting circle. Never negative.</summary>
    public double StartRadius { get; }

    /// <summary>The centre of the ending circle.</summary>
    public XPoint EndCenter { get; }

    /// <summary>The radius of the ending circle. Never negative.</summary>
    public double EndRadius { get; }
}

/// <summary>
/// Triangles with a colour at each corner, blended across each. Shading types 1 and 4 to 7 all
/// arrive as this: a patch is already cut into triangles and a function already applied.
/// </summary>
[Experimental("PPEXP001")]
public sealed class PageMeshShading : PageShader
{
    private readonly XPoint[] _points;
    private readonly byte[] _colors;

    internal PageMeshShading(XPoint[] points, byte[] colors, XRect bounds, PagePaint? background, bool isShared)
        : base(isShared, MeanOf(colors))
    {
        _points = points;
        _colors = colors;
        Bounds = bounds;
        Background = background;
    }

    /// <summary>Three points for each triangle, in the order the triangles are painted.</summary>
    public ReadOnlySpan<XPoint> Points => _points;

    /// <summary>Three bytes for each of <see cref="Points"/>: red, green and blue.</summary>
    public ReadOnlySpan<byte> Colors => _colors;

    /// <summary>The box that holds every point. Outside the triangles the shader is transparent.</summary>
    public XRect Bounds { get; }

    /// <summary>The colour under the mesh wherever it is transparent, or null for none, as <see cref="PageGradient.Background"/>.</summary>
    public PagePaint? Background { get; }

    private static PagePaint? MeanOf(byte[] colors)
    {
        if (colors.Length == 0)
            return null;

        Span<long> sums = stackalloc long[3];
        for (var index = 0; index < colors.Length; index++)
            sums[index % 3] += colors[index];

        var scale = 255.0 * (colors.Length / 3);
        return new PagePaint(sums[0] / scale, sums[1] / scale, sums[2] / scale);
    }
}

/// <summary>A tiling pattern: one picture, repeated without end across and up.</summary>
[Experimental("PPEXP001")]
public sealed class PageTiling : PageShader
{
    internal PageTiling(PagePicture cell, XRect tile, PagePaint? fallback, bool isShared) : base(isShared, fallback)
    {
        Cell = cell;
        Tile = tile;
    }

    /// <summary>
    /// What one tile draws, in the shader's coordinates, colours and all: the tint of an
    /// uncoloured pattern is already in it.
    /// </summary>
    public PagePicture Cell { get; }

    /// <summary>
    /// The rectangle that repeats. Its width and height are the steps, both above zero.
    /// <see cref="Cell"/> draws nothing outside it and everything inside it, the parts of
    /// neighbouring cells that reach into it included. So a device repeats this rectangle and
    /// needs to know nothing of <c>/BBox</c>, <c>/XStep</c> or <c>/YStep</c>.
    /// </summary>
    public XRect Tile { get; }
}

/// <summary>What a path or a stencil is painted with when it is not one colour.</summary>
/// <remarks>
/// <b>The shader has a transform of its own</b>, straight to device space like every other
/// transform at this seam, and it is not the transform of the geometry painted. A pattern is laid
/// on the page and a path is a window onto it: the same shader under the same transform shows
/// through every path painted with it, wherever each path's own transform puts it.
/// </remarks>
[Experimental("PPEXP001")]
public readonly struct PageShaderPaint
{
    /// <summary>Initializes a paint.</summary>
    /// <param name="shader">The shader.</param>
    /// <param name="transform">Maps the shader's coordinates to device space.</param>
    /// <param name="alpha">The constant alpha, from 0 to 1.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shader"/> is null.</exception>
    public PageShaderPaint(PageShader shader, XMatrix transform, double alpha = 1)
    {
        ArgumentNullException.ThrowIfNull(shader);
        Shader = shader;
        Transform = transform;
        Alpha = new PagePaint(0, 0, 0, alpha).Alpha;
    }

    /// <summary>The shader. Never null in a paint page rendering hands over.</summary>
    public PageShader Shader { get; }

    /// <summary>
    /// Maps the shader's coordinates to device space. Finite and invertible in a paint page
    /// rendering hands over.
    /// </summary>
    public XMatrix Transform { get; }

    /// <summary>
    /// The constant alpha, from 0 to 1: <c>/ca</c> for a fill and a stencil, <c>/CA</c> for a
    /// stroke. It multiplies whatever the shader answers.
    /// </summary>
    public double Alpha { get; }
}
