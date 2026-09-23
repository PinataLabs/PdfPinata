using System;
using System.Collections.Generic;
using System.Globalization;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Charting.Tests.Helpers;

/// <summary>
///   The rectangles a page paints, for the tests that ask where the columns and bars went.
/// </summary>
/// <remarks>
///   A column is the one thing on a chart with no other trace in the content stream. It is drawn
///   with <c>XGraphics.DrawRectangle</c>, which writes a single <c>re</c> and then the operator
///   saying how to paint it - so it is neither a stroked segment, which
///   <see cref="StrokedLines"/> would find, nor a string, which <see cref="ShownText"/> would.
///   Reading the <c>re</c> operators back is the only way to see it short of rasterizing the page,
///   and rasterizing needs Ghostscript, which this project deliberately does not.
///
///   Positions are as the content stream states them: x and y of the corner nearest the origin,
///   with y increasing up the page, in the space the chart was drawn in. Every renderer here draws
///   under the one translate the frame applies, so distances and orderings between two rectangles
///   on the same page are exact and comparable, which is what the assertions ask of them.
///
///   The graphics state is followed through q/Q for the same reason <see cref="StrokedLines"/>
///   follows it: the plot area renderers name a fill colour inside a save/restore pair, and
///   reading the page without unwinding it reports that colour for everything drawn afterwards.
/// </remarks>
internal static class PaintedRectangles
{
    internal readonly struct Rectangle
    {
        internal Rectangle(double x, double y, double width, double height, string colour, bool filled, bool stroked)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Colour = colour;
            Filled = filled;
            Stroked = stroked;
        }

        internal double X { get; }

        /// <summary>The foot of the rectangle - y increases up the page.</summary>
        internal double Y { get; }

        internal double Width { get; }
        internal double Height { get; }

        /// <summary>
        ///   The colour it was painted in, as "r,g,b" with each component between 0 and 1: the
        ///   fill colour for a rectangle that was filled, the stroking colour for one that was
        ///   only outlined. A page that never names one paints in black.
        /// </summary>
        internal string Colour { get; }

        /// <summary>Whether the paint operator filled it. A column body is filled.</summary>
        internal bool Filled { get; }

        /// <summary>Whether the paint operator stroked it. A column border is stroked.</summary>
        internal bool Stroked { get; }

        internal double Right => X + Width;
        internal double Top => Y + Height;
        internal double CentreX => X + Width / 2;
        internal double CentreY => Y + Height / 2;

        public override string ToString()
        {
            var paint = Filled ? Stroked ? "FS" : "F" : "S";
            return $"({X:F2},{Y:F2}) {Width:F2}x{Height:F2} {paint} rgb={Colour}";
        }
    }

    /// <summary>The colour a page paints in until it names another one.</summary>
    internal const string Black = "0,0,0";

    /// <summary>Every rectangle the page paints, in the order it draws them.</summary>
    internal static IReadOnlyList<Rectangle> On(PdfPage page)
    {
        var reader = new RectangleReader();
        foreach (var item in ContentReader.ReadContent(PageContent.Of(page)))
        {
            if (item is COperator op)
                reader.Read(op);
        }

        return reader.Painted;
    }

    /// <summary>
    ///   What each path-painting operator does with the path: whether it fills it, strokes it, or
    ///   both. <c>n</c>, which paints nothing and is how a clipping path ends, is not here.
    /// </summary>
    internal static readonly IReadOnlyDictionary<OpCodeName, (bool Filled, bool Stroked)> Painting =
        new Dictionary<OpCodeName, (bool Filled, bool Stroked)>
        {
            [OpCodeName.f] = (true, false),
            [OpCodeName.F] = (true, false),
            [OpCodeName.fx] = (true, false),
            [OpCodeName.S] = (false, true),
            [OpCodeName.s] = (false, true),
            [OpCodeName.B] = (true, true),
            [OpCodeName.Bx] = (true, true),
            [OpCodeName.b] = (true, true),
            [OpCodeName.bx] = (true, true)
        };

    /// <summary>Follows one page's content, one operator at a time.</summary>
    private sealed class RectangleReader
    {
        // Rectangles named but not yet painted. A path is not painted until its operator says
        // how, and DrawRectangle writes one re per call, so in practice this holds one.
        private readonly List<(double X, double Y, double Width, double Height)> _pending = [];

        private readonly Stack<(string Fill, string Stroke)> _saved = new();
        private string _fill = Black;
        private string _stroke = Black;

        internal List<Rectangle> Painted { get; } = [];

        internal void Read(COperator op)
        {
            var name = op.OpCode.OpCodeName;
            if (FollowGraphicsState(name) || SetColour(name, op.Operands))
                return;

            if (name == OpCodeName.re)
                NameRectangle(op.Operands);
            else
                PaintPath(name);
        }

        private bool FollowGraphicsState(OpCodeName name)
        {
            switch (name)
            {
                case OpCodeName.q:
                    _saved.Push((_fill, _stroke));
                    return true;

                case OpCodeName.Q:
                    // A Q with nothing put away is malformed content; read on rather than throw.
                    if (_saved.Count > 0)
                        (_fill, _stroke) = _saved.Pop();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>The fill and stroking colours, in whichever of the three device spaces they are named.</summary>
        private bool SetColour(OpCodeName name, CSequence operands)
        {
            switch (name)
            {
                case OpCodeName.rg: SetFill(RgbIn(operands)); return true;
                case OpCodeName.g: SetFill(GreyIn(operands)); return true;
                case OpCodeName.k: SetFill(CmykIn(operands)); return true;
                case OpCodeName.RG: SetStroke(RgbIn(operands)); return true;
                case OpCodeName.G: SetStroke(GreyIn(operands)); return true;
                case OpCodeName.K: SetStroke(CmykIn(operands)); return true;
                default: return false;
            }
        }

        // A colour operator with too few operands is malformed and changes nothing.
        private void SetFill(string colour) => _fill = colour ?? _fill;
        private void SetStroke(string colour) => _stroke = colour ?? _stroke;

        private void NameRectangle(CSequence operands)
        {
            if (operands.Count < 4)
                return;

            var width = Number(operands[2]);
            var height = Number(operands[3]);
            var x = Number(operands[0]);
            var y = Number(operands[1]);

            // A negative extent names the same rectangle from the far corner.
            (x, width) = FromNearCorner(x, width);
            (y, height) = FromNearCorner(y, height);

            _pending.Add((x, y, width, height));
        }

        private void PaintPath(OpCodeName name)
        {
            // Painted with nothing, which a clipping path is.
            if (name == OpCodeName.n)
            {
                _pending.Clear();
                return;
            }

            if (!Painting.TryGetValue(name, out var paint))
                return;

            foreach (var (x, y, width, height) in _pending)
            {
                var colour = paint.Filled ? _fill : _stroke;
                Painted.Add(new Rectangle(x, y, width, height, colour, paint.Filled, paint.Stroked));
            }
            _pending.Clear();
        }
    }

    /// <summary>
    ///   The rectangles the page fills, which for a column or bar chart are the columns and bars
    ///   themselves. The borders drawn around them afterwards are stroked rather than filled, so
    ///   this reports each column once.
    /// </summary>
    internal static IReadOnlyList<Rectangle> FilledOn(PdfPage page)
    {
        var filled = new List<Rectangle>();
        foreach (var rectangle in On(page))
        {
            if (rectangle.Filled)
                filled.Add(rectangle);
        }
        return filled;
    }

    /// <summary>
    ///   A colour written the way this reports one, so a test may name the colour it expects
    ///   rather than the three numbers the content stream happens to round it to.
    /// </summary>
    internal static string ColourOf(XColor colour) =>
        Rgb(colour.R / 255.0, colour.G / 255.0, colour.B / 255.0);

    internal static string Rgb(double r, double g, double b) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", r, g, b);

    internal static string Grey(double level) => Rgb(level, level, level);

    internal static string Cmyk(double c, double m, double y, double k) =>
        Rgb((1 - c) * (1 - k), (1 - m) * (1 - k), (1 - y) * (1 - k));

    private static string RgbIn(CSequence operands) =>
        operands.Count >= 3 ? Rgb(Number(operands[0]), Number(operands[1]), Number(operands[2])) : null;

    private static string GreyIn(CSequence operands) =>
        operands.Count >= 1 ? Grey(Number(operands[0])) : null;

    private static string CmykIn(CSequence operands) =>
        operands.Count >= 4
            ? Cmyk(Number(operands[0]), Number(operands[1]), Number(operands[2]), Number(operands[3]))
            : null;

    private static (double Start, double Extent) FromNearCorner(double start, double extent) =>
        extent < 0 ? (start + extent, -extent) : (start, extent);

    private static double Number(CObject operand) => operand switch
    {
        CInteger integer => integer.Value,
        CReal real => real.Value,
        _ => throw new InvalidOperationException("Operand is not a number: " + operand)
    };
}
