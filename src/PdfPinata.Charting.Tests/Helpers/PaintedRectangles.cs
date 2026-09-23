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
        var painted = new List<Rectangle>();

        // Rectangles named but not yet painted. A path is not painted until its operator says
        // how, and DrawRectangle writes one re per call, so in practice this holds one.
        var pending = new List<(double X, double Y, double Width, double Height)>();

        var fill = Black;
        var stroke = Black;
        var saved = new Stack<(string Fill, string Stroke)>();

        foreach (var item in ContentReader.ReadContent(PageContent.Of(page)))
        {
            if (item is not COperator op)
                continue;

            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push((fill, stroke));
                    break;

                case OpCodeName.Q:
                    // A Q with nothing put away is malformed content; read on rather than throw.
                    if (saved.Count > 0)
                        (fill, stroke) = saved.Pop();
                    break;

                case OpCodeName.rg:
                    if (op.Operands.Count >= 3)
                        fill = Rgb(Number(op.Operands[0]), Number(op.Operands[1]), Number(op.Operands[2]));
                    break;

                case OpCodeName.g:
                    if (op.Operands.Count >= 1)
                        fill = Grey(Number(op.Operands[0]));
                    break;

                case OpCodeName.k:
                    if (op.Operands.Count >= 4)
                        fill = Cmyk(Number(op.Operands[0]), Number(op.Operands[1]),
                            Number(op.Operands[2]), Number(op.Operands[3]));
                    break;

                case OpCodeName.RG:
                    if (op.Operands.Count >= 3)
                        stroke = Rgb(Number(op.Operands[0]), Number(op.Operands[1]), Number(op.Operands[2]));
                    break;

                case OpCodeName.G:
                    if (op.Operands.Count >= 1)
                        stroke = Grey(Number(op.Operands[0]));
                    break;

                case OpCodeName.K:
                    if (op.Operands.Count >= 4)
                        stroke = Cmyk(Number(op.Operands[0]), Number(op.Operands[1]),
                            Number(op.Operands[2]), Number(op.Operands[3]));
                    break;

                case OpCodeName.re:
                    if (op.Operands.Count >= 4)
                    {
                        var width = Number(op.Operands[2]);
                        var height = Number(op.Operands[3]);
                        var x = Number(op.Operands[0]);
                        var y = Number(op.Operands[1]);

                        // A negative extent names the same rectangle from the far corner.
                        if (width < 0)
                        {
                            x += width;
                            width = -width;
                        }
                        if (height < 0)
                        {
                            y += height;
                            height = -height;
                        }

                        pending.Add((x, y, width, height));
                    }
                    break;

                case OpCodeName.f:
                case OpCodeName.F:
                case OpCodeName.fx:
                    Paint(filled: true, stroked: false);
                    break;

                case OpCodeName.S:
                case OpCodeName.s:
                    Paint(filled: false, stroked: true);
                    break;

                case OpCodeName.B:
                case OpCodeName.Bx:
                case OpCodeName.b:
                case OpCodeName.bx:
                    Paint(filled: true, stroked: true);
                    break;

                // Painted with nothing, which a clipping path is.
                case OpCodeName.n:
                    pending.Clear();
                    break;
            }
        }

        return painted;

        void Paint(bool filled, bool stroked)
        {
            foreach (var (x, y, width, height) in pending)
                painted.Add(new Rectangle(x, y, width, height, filled ? fill : stroke, filled, stroked));
            pending.Clear();
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

    private static double Number(CObject operand) => operand switch
    {
        CInteger integer => integer.Value,
        CReal real => real.Value,
        _ => throw new InvalidOperationException("Operand is not a number: " + operand)
    };
}
