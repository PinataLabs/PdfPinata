using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Reads the shadings that are not a ramp along one parameter into triangles with a colour at
/// each corner: the function grid of type 1, the triangle meshes of types 4 and 5, and the patch
/// meshes of types 6 and 7.
/// </summary>
/// <remarks>
/// <para>
/// A patch is cut into a grid of <see cref="PatchGrid"/> by <see cref="PatchGrid"/> and each cell
/// into two triangles, so a device blends colours across flat triangles and knows nothing of
/// Bézier patches. The colour is blended in RGB. Where a mesh has a <c>/Function</c>, a vertex
/// carries the function's input and not a colour. The input runs in a straight line across a
/// triangle, so the triangle is cut along the lines where the input is that of one of the
/// function's stops, as a gradient would have them, and between two cuts the colour is the
/// straight line between two stops. A hard edge of the function is then a hard edge across
/// the triangle.
/// </para>
/// <para>
/// Reading stops at <see cref="MaximumTriangles"/>, which is reported, and what was read is drawn.
/// </para>
/// </remarks>
internal static class MeshReader
{
    /// <summary>The most triangles one shading is handed over as.</summary>
    internal const int MaximumTriangles = 1 << 18;

    /// <summary>How many cells a patch is cut into along each side.</summary>
    internal const int PatchGrid = 8;

    /// <summary>How many cells a type 1 shading's domain is cut into along each side.</summary>
    internal const int FunctionGrid = 64;

    /// <returns>
    /// What makes the shader over a background or over none, or null where the shading cannot be
    /// read, which is reported.
    /// </returns>
    internal static Func<PagePaint?, PageShader> Read(PdfDictionary shading, int type, ShadingColors colors,
        bool isShared, DiagnosticSink sink, string subject)
    {
        var mesh = new Triangles(colors);
        var read = type switch
        {
            1 => ReadFunctionGrid(shading, colors, mesh),
            4 or 5 => ReadVertices(shading, type, colors, mesh),
            _ => ReadPatches(shading, type, colors, mesh)
        };

        // The caller reports it, as it does for a gradient.
        if (colors.Work.IsSpent)
            return null;

        if (!read && colors.Work.HasDecodedAll)
        {
            sink.Report(PageRenderProblem.LimitReached, subject,
                "The page's shadings decode more than one render reads. This one is not drawn.");
            return null;
        }

        if (!read)
        {
            Shadings.Invalid(sink, subject, "The shading's mesh is not laid out as its type is.");
            return null;
        }

        if (mesh.Overflowed)
        {
            sink.Report(PageRenderProblem.LimitReached, subject,
                "The shading has more triangles than are drawn. Those past the limit are left out.");
        }

        var points = mesh.Points.ToArray();
        var bytes = mesh.Colors.ToArray();
        var bounds = mesh.Bounds;
        return background => new PageMeshShading(points, bytes, bounds, background, isShared);
    }

    /// <summary>Type 1: the colour is a function of the two coordinates, over a rectangle of them.</summary>
    private static bool ReadFunctionGrid(PdfDictionary shading, ShadingColors colors, Triangles mesh)
    {
        var domain = PdfFunction.Numbers(shading.Elements["/Domain"]);
        if (domain == null || domain.Length != 4)
            domain = [0, 1, 0, 1];

        var matrix = XMatrix.Identity;
        if (PdfFunction.Numbers(shading.Elements["/Matrix"]) is { Length: 6 } m)
            matrix = new XMatrix(m[0], m[1], m[2], m[3], m[4], m[5]);

        var grid = new Vertex[FunctionGrid + 1, FunctionGrid + 1];
        Span<double> at = stackalloc double[2];
        for (var row = 0; row <= FunctionGrid; row++)
        {
            for (var column = 0; column <= FunctionGrid; column++)
            {
                at[0] = domain[0] + (domain[1] - domain[0]) * column / FunctionGrid;
                at[1] = domain[2] + (domain[3] - domain[2]) * row / FunctionGrid;
                grid[row, column] = new Vertex(matrix.Transform(new XPoint(at[0], at[1])), colors.At(at));
            }
        }

        mesh.AddGrid(grid);
        return true;
    }

    /// <summary>Types 4 and 5: vertices, joined into triangles by a flag on each or by rows of a lattice.</summary>
    private static bool ReadVertices(PdfDictionary shading, int type, ShadingColors colors, Triangles mesh)
    {
        if (!VertexStream.TryOpen(shading, colors, type == 4, out var stream))
            return false;

        // Each vertex of a triangle mesh, with its flag where it has one, takes a whole number of
        // bytes (ISO 32000-1 8.7.4.5.5, and 8.7.4.5.6, which gives type 5 the same layout less the
        // flags). Ghostscript and PDFium both pad type 5 so.
        stream.PadsVertices = true;
        if (type == 5)
            return ReadLattice(shading, ref stream, mesh);

        // The triangle before: a flag of 1 or 2 makes the next from two of its corners.
        Vertex a = default, b = default, c = default;
        var known = false;
        while (!mesh.Stopped && stream.TryReadFlag(out var flag))
        {
            if (flag == 0 || !known)
            {
                if (!stream.TryReadVertex(out a) || !stream.TryReadFlag(out _) || !stream.TryReadVertex(out b)
                    || !stream.TryReadFlag(out _) || !stream.TryReadVertex(out c))
                {
                    break;
                }

                known = true;
            }
            else
            {
                if (!stream.TryReadVertex(out var next))
                    break;

                (a, b, c) = flag == 1 ? (b, c, next) : (a, c, next);
            }

            mesh.Add(a, b, c);
        }

        return true;
    }

    private static bool ReadLattice(PdfDictionary shading, ref VertexStream stream, Triangles mesh)
    {
        if (!PdfItemValues.TryGetInteger(shading.Elements["/VerticesPerRow"], out var perRow) || perRow < 2
            || perRow > stream.VerticesLeft)
        {
            return false;
        }

        var above = new Vertex[perRow];
        var row = new Vertex[perRow];
        for (var rows = 0; !mesh.Stopped; rows++)
        {
            for (var column = 0; column < perRow; column++)
            {
                if (!stream.TryReadVertex(out row[column]))
                    return true;
            }

            for (var column = 0; rows > 0 && column + 1 < perRow; column++)
            {
                mesh.Add(above[column], above[column + 1], row[column]);
                mesh.Add(above[column + 1], row[column + 1], row[column]);
            }

            (above, row) = (row, above);
        }

        return true;
    }

    /// <summary>
    /// Types 6 and 7: patches bounded by four Bézier curves, each with a colour at its four
    /// corners. A type 7 patch states its four inner control points, and a type 6 patch's are
    /// worked out from its edges (ISO 32000-1 8.7.4.5.7 and 8.7.4.5.8).
    /// </summary>
    private static bool ReadPatches(PdfDictionary shading, int type, ShadingColors colors, Triangles mesh)
    {
        if (!VertexStream.TryOpen(shading, colors, true, out var stream))
            return false;

        // A patch is not padded to a whole byte. ISO 32000-1 asks that of each vertex of a
        // triangle mesh (8.7.4.5.5) and says nothing of a patch. pdf.js and PDFium read patches
        // unpadded and Ghostscript pads each one, so this follows the two. All three agree
        // wherever a patch's data ends on a byte.

        // The sixteen control points as p[column, row], and the colours at the corners in the
        // order the stream gives them: (0,0), (0,3), (3,3), (3,0).
        var p = new XPoint[4, 4];
        var corners = new Vertex[4];
        var known = false;
        while (!mesh.Stopped && stream.TryReadFlag(out var flag))
        {
            var given = 0;
            if (flag != 0 && known)
            {
                TakeEdge(p, corners, flag);
                given = 4;
            }

            // Round the edge from where the shared side ends, and then the inside.
            for (var index = given; index < (type == 7 ? 16 : 12); index++)
            {
                if (!stream.TryReadPoint(out var point))
                    return true;

                var (column, row) = Order[index];
                p[column, row] = point;
            }

            for (var index = given / 2; index < 4; index++)
            {
                if (!stream.TryReadColor(out corners[index]))
                    return true;
            }

            if (type == 6)
                FillInside(p);

            known = true;
            mesh.AddGrid(Tessellate(p, corners));
        }

        return true;
    }

    // Where the points of a patch come in its data: along the first side, round the other three,
    // and then the four inside.
    private static readonly (int Column, int Row)[] Order =
    [
        (0, 0), (0, 1), (0, 2), (0, 3), (1, 3), (2, 3), (3, 3), (3, 2), (3, 1), (3, 0), (2, 0), (1, 0),
        (1, 1), (1, 2), (2, 2), (2, 1)
    ];

    // The side a patch shares with the one before becomes its first side, and the two colours
    // on that side its first two.
    private static void TakeEdge(XPoint[,] p, Vertex[] corners, int flag)
    {
        var (from, colors) = flag switch
        {
            1 => (new[] { (0, 3), (1, 3), (2, 3), (3, 3) }, (1, 2)),
            2 => (new[] { (3, 3), (3, 2), (3, 1), (3, 0) }, (2, 3)),
            _ => (new[] { (3, 0), (2, 0), (1, 0), (0, 0) }, (3, 0))
        };

        Span<XPoint> side = stackalloc XPoint[4];
        for (var index = 0; index < 4; index++)
            side[index] = p[from[index].Item1, from[index].Item2];
        for (var index = 0; index < 4; index++)
            p[0, index] = side[index];

        (corners[0], corners[1]) = (corners[colors.Item1], corners[colors.Item2]);
    }

    // A Coons patch has no inner control points of its own: they are these.
    private static void FillInside(XPoint[,] p)
    {
        p[1, 1] = Inner(p[0, 0], p[0, 1], p[1, 0], p[0, 3], p[3, 0], p[3, 1], p[1, 3], p[3, 3]);
        p[1, 2] = Inner(p[0, 3], p[0, 2], p[1, 3], p[0, 0], p[3, 3], p[3, 2], p[1, 0], p[3, 0]);
        p[2, 1] = Inner(p[3, 0], p[3, 1], p[2, 0], p[3, 3], p[0, 0], p[0, 1], p[2, 3], p[0, 3]);
        p[2, 2] = Inner(p[3, 3], p[3, 2], p[2, 3], p[3, 0], p[0, 3], p[0, 2], p[2, 0], p[0, 0]);
    }

    private static XPoint Inner(XPoint corner, XPoint near1, XPoint near2, XPoint side1, XPoint side2, XPoint far1,
        XPoint far2, XPoint opposite) =>
        new((-4 * corner.X + 6 * (near1.X + near2.X) - 2 * (side1.X + side2.X) + 3 * (far1.X + far2.X) - opposite.X) / 9,
            (-4 * corner.Y + 6 * (near1.Y + near2.Y) - 2 * (side1.Y + side2.Y) + 3 * (far1.Y + far2.Y) - opposite.Y) / 9);

    private static Vertex[,] Tessellate(XPoint[,] p, Vertex[] corners)
    {
        var grid = new Vertex[PatchGrid + 1, PatchGrid + 1];
        Span<double> across = stackalloc double[4];
        Span<double> up = stackalloc double[4];
        for (var row = 0; row <= PatchGrid; row++)
        {
            var v = (double)row / PatchGrid;
            Bernstein(v, up);
            for (var column = 0; column <= PatchGrid; column++)
            {
                var u = (double)column / PatchGrid;
                Bernstein(u, across);

                double x = 0, y = 0;
                for (var i = 0; i < 4; i++)
                {
                    for (var j = 0; j < 4; j++)
                    {
                        x += p[i, j].X * across[i] * up[j];
                        y += p[i, j].Y * across[i] * up[j];
                    }
                }

                grid[row, column] = Vertex.Blend(new XPoint(x, y), corners[0], (1 - u) * (1 - v), corners[1],
                    (1 - u) * v, corners[2], u * v, corners[3], u * (1 - v));
            }
        }

        return grid;
    }

    private static void Bernstein(double t, Span<double> into)
    {
        var s = 1 - t;
        into[0] = s * s * s;
        into[1] = 3 * s * s * t;
        into[2] = 3 * s * t * t;
        into[3] = t * t * t;
    }

    /// <summary>
    /// A corner of a triangle: where it is, and its colour, or else the input of the shading's
    /// function that its colour is found by.
    /// </summary>
    private readonly struct Vertex
    {
        internal Vertex(XPoint point, PagePaint color)
        {
            Point = point;
            Red = color.Red;
            Green = color.Green;
            Blue = color.Blue;
        }

        private Vertex(XPoint point, double red, double green, double blue, double input)
        {
            Point = point;
            Red = red;
            Green = green;
            Blue = blue;
            Input = input;
        }

        internal Vertex(XPoint point, double input) : this(point, 0, 0, 0, input)
        {
        }

        internal XPoint Point { get; }
        internal double Red { get; }
        internal double Green { get; }
        internal double Blue { get; }
        internal double Input { get; }

        internal Vertex At(XPoint point) => new(point, Red, Green, Blue, Input);

        /// <summary>A vertex at a point whose colour and input are four others', each by its weight.</summary>
        internal static Vertex Blend(XPoint point, Vertex a, double wa, Vertex b, double wb, Vertex c, double wc,
            Vertex d, double wd) =>
            new(point,
                a.Red * wa + b.Red * wb + c.Red * wc + d.Red * wd,
                a.Green * wa + b.Green * wb + c.Green * wc + d.Green * wd,
                a.Blue * wa + b.Blue * wb + c.Blue * wc + d.Blue * wd,
                a.Input * wa + b.Input * wb + c.Input * wc + d.Input * wd);
    }

    /// <summary>The triangles read so far, as the points and colours a <see cref="PageMeshShading"/> holds.</summary>
    private sealed class Triangles
    {
        // The function as a gradient's stops, where the vertices carry its input, and its domain.
        private readonly PageGradientStop[] _ramp;
        private readonly double _low, _high;
        private readonly ShadingWork _work;

        internal Triangles(ShadingColors colors)
        {
            _work = colors.Work;
            if (!colors.BlendsInput)
                return;

            (_low, _high) = colors.DomainOf(0);
            _ramp = colors.Ramp(_low, _high);
        }

        private double _left = double.PositiveInfinity, _top = double.PositiveInfinity;
        private double _right = double.NegativeInfinity, _bottom = double.NegativeInfinity;

        internal List<XPoint> Points { get; } = [];

        internal List<byte> Colors { get; } = [];

        /// <summary>Whether triangles were left out at <see cref="MaximumTriangles"/>.</summary>
        internal bool Overflowed { get; private set; }

        /// <summary>Whether no more triangles are read: there are as many as are kept, or the render's work is spent.</summary>
        internal bool Stopped => Overflowed || _work.IsSpent;

        internal XRect Bounds => Points.Count == 0 ? default : new XRect(_left, _top, _right - _left, _bottom - _top);

        /// <summary>The cells of a grid of vertices, two triangles each.</summary>
        internal void AddGrid(Vertex[,] grid)
        {
            for (var row = 0; row + 1 < grid.GetLength(0); row++)
            {
                for (var column = 0; column + 1 < grid.GetLength(1); column++)
                {
                    Add(grid[row, column], grid[row, column + 1], grid[row + 1, column]);
                    Add(grid[row, column + 1], grid[row + 1, column + 1], grid[row + 1, column]);
                }
            }
        }

        internal void Add(Vertex a, Vertex b, Vertex c)
        {
            if (_ramp == null)
            {
                Emit(a, b, c);
                return;
            }

            // A triangle between two cuts has at most five corners.
            Span<XPoint> points = stackalloc XPoint[8];
            Span<double> offsets = stackalloc double[8];
            Span<XPoint> above = stackalloc XPoint[8];
            Span<double> aboveOffsets = stackalloc double[8];
            Span<XPoint> between = stackalloc XPoint[8];
            Span<double> betweenOffsets = stackalloc double[8];

            (points[0], points[1], points[2]) = (a.Point, b.Point, c.Point);
            (offsets[0], offsets[1], offsets[2]) = (OffsetOf(a.Input), OffsetOf(b.Input), OffsetOf(c.Input));
            var lowest = Math.Min(offsets[0], Math.Min(offsets[1], offsets[2]));
            var highest = Math.Max(offsets[0], Math.Max(offsets[1], offsets[2]));

            for (var stop = 1; stop < _ramp.Length && !Stopped; stop++)
            {
                PageGradientStop from = _ramp[stop - 1], to = _ramp[stop];
                if (!(to.Offset > from.Offset) || to.Offset < lowest || from.Offset > highest)
                    continue;

                var count = Cut(points, offsets, 3, from.Offset, true, above, aboveOffsets);
                count = Cut(above, aboveOffsets, count, to.Offset, false, between, betweenOffsets);
                for (var corner = 2; corner < count; corner++)
                {
                    Emit(Colored(between[0], betweenOffsets[0], from, to),
                        Colored(between[corner - 1], betweenOffsets[corner - 1], from, to),
                        Colored(between[corner], betweenOffsets[corner], from, to));
                }
            }
        }

        // Where an input lies along the function's domain, from 0 to 1, which is how the stops are placed.
        private double OffsetOf(double input) =>
            _high > _low ? Math.Min(Math.Max((input - _low) / (_high - _low), 0), 1) : 0;

        private static Vertex Colored(XPoint point, double offset, PageGradientStop from, PageGradientStop to)
        {
            var along = (offset - from.Offset) / (to.Offset - from.Offset);
            return new Vertex(point, new PagePaint(
                from.Color.Red + along * (to.Color.Red - from.Color.Red),
                from.Color.Green + along * (to.Color.Green - from.Color.Green),
                from.Color.Blue + along * (to.Color.Blue - from.Color.Blue)));
        }

        // The part of a convex polygon whose offsets are at or above a bound, or at or below it:
        // each corner that is, and a new one wherever a side crosses the bound.
        private static int Cut(ReadOnlySpan<XPoint> points, ReadOnlySpan<double> offsets, int count, double bound,
            bool keepAbove, Span<XPoint> into, Span<double> intoOffsets)
        {
            var kept = 0;
            for (var index = 0; index < count; index++)
            {
                var next = (index + 1) % count;
                double here = offsets[index], there = offsets[next];
                bool hereIn = keepAbove ? here >= bound : here <= bound, thereIn = keepAbove ? there >= bound : there <= bound;
                if (hereIn)
                {
                    into[kept] = points[index];
                    intoOffsets[kept++] = here;
                }

                if (hereIn != thereIn)
                {
                    var along = (bound - here) / (there - here);
                    into[kept] = new XPoint(points[index].X + along * (points[next].X - points[index].X),
                        points[index].Y + along * (points[next].Y - points[index].Y));
                    intoOffsets[kept++] = bound;
                }
            }

            return kept;
        }

        private void Emit(Vertex a, Vertex b, Vertex c)
        {
            if (Points.Count / 3 >= MaximumTriangles)
            {
                Overflowed = true;
                return;
            }

            if (_work.IsSpent)
                return;

            _work.Spend(ShadingWork.TriangleCost);

            Emit(a);
            Emit(b);
            Emit(c);
        }

        private void Emit(Vertex vertex)
        {
            var point = vertex.Point;
            if (!double.IsFinite(point.X + point.Y))
                point = new XPoint(0, 0);

            Points.Add(point);
            _left = Math.Min(_left, point.X);
            _right = Math.Max(_right, point.X);
            _top = Math.Min(_top, point.Y);
            _bottom = Math.Max(_bottom, point.Y);

            var color = new PagePaint(vertex.Red, vertex.Green, vertex.Blue);
            Colors.Add((byte)Math.Round(color.Red * 255));
            Colors.Add((byte)Math.Round(color.Green * 255));
            Colors.Add((byte)Math.Round(color.Blue * 255));
        }
    }

    /// <summary>
    /// The packed data of a mesh: flags, coordinates and colour components, each a whole number
    /// of bits, the high bit first, taken to its range by <c>/Decode</c>.
    /// </summary>
    private ref struct VertexStream
    {
        private readonly byte[] _data;
        private readonly ShadingColors _colors;
        private readonly double[] _decode;
        private readonly int _bitsPerCoordinate;
        private readonly int _bitsPerComponent;
        private readonly int _bitsPerFlag;
        private long _bit;

        private VertexStream(byte[] data, ShadingColors colors, double[] decode, int bitsPerCoordinate,
            int bitsPerComponent, int bitsPerFlag)
        {
            _data = data;
            _colors = colors;
            _decode = decode;
            _bitsPerCoordinate = bitsPerCoordinate;
            _bitsPerComponent = bitsPerComponent;
            _bitsPerFlag = bitsPerFlag;
            _bit = 0;
            PadsVertices = false;
        }

        internal static bool TryOpen(PdfDictionary shading, ShadingColors colors, bool hasFlags, out VertexStream stream)
        {
            stream = default;
            var elements = shading.Elements;
            var flagBits = 0;
            var decode = PdfFunction.Numbers(elements["/Decode"]);
            if (!PdfItemValues.TryGetInteger(elements["/BitsPerCoordinate"], out var coordinate)
                || coordinate is not (1 or 2 or 4 or 8 or 12 or 16 or 24 or 32)
                || !PdfItemValues.TryGetInteger(elements["/BitsPerComponent"], out var component)
                || component is not (1 or 2 or 4 or 8 or 12 or 16)
                || hasFlags && (!PdfItemValues.TryGetInteger(elements["/BitsPerFlag"], out flagBits) || flagBits is not (2 or 4 or 8))
                || decode == null || decode.Length != 4 + 2 * colors.ComponentCount)
            {
                return false;
            }

            // Decoded no further than the most triangles a shading is drawn as can reach, at three
            // whole vertices each. A patch takes far fewer bytes a triangle than that.
            var vertexBytes = (flagBits + 2 * coordinate + colors.ComponentCount * component + 7) / 8;
            var data = colors.Work.Decode(shading, 3L * (MaximumTriangles + 1) * vertexBytes);
            if (data == null)
                return false;

            stream = new VertexStream(data, colors, decode, coordinate, component, flagBits);
            return true;
        }

        /// <summary>How many more vertices the data is long enough to hold, where they have no flags.</summary>
        internal readonly long VerticesLeft
        {
            get
            {
                long bits = 2 * _bitsPerCoordinate + _colors.ComponentCount * _bitsPerComponent;
                if (PadsVertices)
                    bits = (bits + 7) & ~7L;
                return (_data.Length * 8L - _bit) / bits;
            }
        }

        internal bool TryReadFlag(out int flag)
        {
            var read = TryReadBits(_bitsPerFlag, out var value);
            flag = (int)(value & 3);
            return read;
        }

        internal bool TryReadPoint(out XPoint point)
        {
            point = default;
            if (!TryReadBits(_bitsPerCoordinate, out var x) || !TryReadBits(_bitsPerCoordinate, out var y))
                return false;

            var top = Math.Pow(2, _bitsPerCoordinate) - 1;
            point = new XPoint(_decode[0] + x * (_decode[1] - _decode[0]) / top, _decode[2] + y * (_decode[3] - _decode[2]) / top);
            return true;
        }

        /// <summary>A colour alone, as a vertex with no place yet: the corner of a patch.</summary>
        internal bool TryReadColor(out Vertex vertex)
        {
            vertex = default;
            Span<double> components = stackalloc double[_colors.ComponentCount];
            var top = Math.Pow(2, _bitsPerComponent) - 1;
            for (var index = 0; index < components.Length; index++)
            {
                if (!TryReadBits(_bitsPerComponent, out var value))
                    return false;

                components[index] = _decode[4 + 2 * index] + value * (_decode[5 + 2 * index] - _decode[4 + 2 * index]) / top;
            }

            vertex = _colors.BlendsInput ? new Vertex(default, components[0]) : new Vertex(default, _colors.At(components));
            return true;
        }

        /// <summary>Whether the bits after a vertex, up to a whole byte, are passed over.</summary>
        internal bool PadsVertices { get; set; }

        /// <summary>A whole vertex of a triangle mesh.</summary>
        internal bool TryReadVertex(out Vertex vertex)
        {
            vertex = default;
            if (!TryReadPoint(out var point) || !TryReadColor(out var color))
                return false;

            vertex = color.At(point);
            if (PadsVertices)
                _bit = (_bit + 7) & ~7L;
            return true;
        }

        private bool TryReadBits(int count, out uint value)
        {
            value = 0;
            if (_bit + count > _data.Length * 8L)
                return false;

            for (var index = 0; index < count; index++, _bit++)
                value = value << 1 | (uint)(_data[_bit >> 3] >> (int)(7 - (_bit & 7)) & 1);
            return true;
        }
    }
}
