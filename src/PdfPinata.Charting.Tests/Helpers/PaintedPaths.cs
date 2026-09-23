using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Charting.Tests.Helpers;

/// <summary>
///   Every path a page paints, whatever it is made of, for the tests that ask what shape was drawn.
/// </summary>
/// <remarks>
///   A line chart's marker is a closed path of straight segments - a square, a diamond, a star -
///   or, for a circle, of Bézier curves, and it is painted twice: filled in the marker's background
///   colour and then outlined in its foreground colour. <see cref="StrokedLines"/> sees only the
///   straight segments of the outline and <see cref="PaintedRectangles"/> only an <c>re</c>, so
///   neither can say which shape a marker was. This keeps each painted path whole: the points it
///   names, how many of them are distinct, whether it curves, and the colours and width it was
///   painted with.
///
///   Positions are as the content stream states them, y increasing up the page, in the space the
///   chart was drawn in - the same convention as the two readers above, so a position read here
///   compares directly with one read there.
///
///   The graphics state is followed through q/Q for the reason the other readers follow it: the
///   plot area draws its markers inside a save/restore pair that also clips, and reading on without
///   unwinding it would report the plot area's colours for everything drawn afterwards. A clipping
///   path is painted with <c>n</c> and is reported as nothing.
/// </remarks>
internal static class PaintedPaths
{
    internal sealed class Path
    {
        internal Path(IReadOnlyList<(double X, double Y)> points, int curves, bool filled, bool stroked,
            string fillColour, string strokeColour, double lineWidth)
        {
            Points = points;
            Curves = curves;
            Filled = filled;
            Stroked = stroked;
            FillColour = fillColour;
            StrokeColour = strokeColour;
            LineWidth = lineWidth;
        }

        /// <summary>
        ///   Every point the path names, in order: the start of each subpath, the end of each
        ///   segment, and for a curve its two control points as well. A closed figure that returns
        ///   to its start names that point twice.
        /// </summary>
        internal IReadOnlyList<(double X, double Y)> Points { get; }

        /// <summary>How many of the segments are Bézier curves rather than straight lines.</summary>
        internal int Curves { get; }

        internal bool Filled { get; }
        internal bool Stroked { get; }

        /// <summary>The fill colour in force when it was painted, as "r,g,b" between 0 and 1.</summary>
        internal string FillColour { get; }

        /// <summary>The stroking colour in force when it was painted, as "r,g,b" between 0 and 1.</summary>
        internal string StrokeColour { get; }

        internal double LineWidth { get; }

        /// <summary>
        ///   How many different points the path names - the corners of a polygon, which is what
        ///   tells a triangle from a diamond from a star. Compared to a hundredth of a point, well
        ///   below any two corners of a marker and well above the rounding of the content stream.
        /// </summary>
        internal int DistinctPoints =>
            Points.Select(p => (Math.Round(p.X, 2), Math.Round(p.Y, 2))).Distinct().Count();

        internal double Left => Points.Min(p => p.X);
        internal double Right => Points.Max(p => p.X);
        internal double Bottom => Points.Min(p => p.Y);
        internal double Top => Points.Max(p => p.Y);
        internal double Width => Right - Left;
        internal double Height => Top - Bottom;
        internal double CentreX => (Left + Right) / 2;
        internal double CentreY => (Bottom + Top) / 2;

        public override string ToString()
        {
            var paint = Filled ? Stroked ? "FS" : "F" : "S";
            return $"{paint} {DistinctPoints} points, {Curves} curves, ({Left:F2},{Bottom:F2}) {Width:F2}x{Height:F2}" +
                $" fill={FillColour} stroke={StrokeColour} w={LineWidth:F2}";
        }
    }

    /// <summary>Every path the page paints, in the order it paints them.</summary>
    internal static IReadOnlyList<Path> On(PdfPage page)
    {
        var painted = new List<Path>();

        var points = new List<(double X, double Y)>();
        var curves = 0;
        (double X, double Y) current;

        var fill = PaintedRectangles.Black;
        var stroke = PaintedRectangles.Black;
        double width = 1;
        var saved = new Stack<(string Fill, string Stroke, double Width)>();

        void Paint(bool filled, bool stroked)
        {
            if (points.Count > 0)
                painted.Add(new Path([..points], curves, filled, stroked, fill, stroke, width));
            Discard();
        }

        void Discard()
        {
            points.Clear();
            curves = 0;
        }

        void Add(double x, double y)
        {
            current = (x, y);
            points.Add(current);
        }

        foreach (var item in ContentReader.ReadContent(PageContent.Of(page)))
        {
            if (item is not COperator op)
                continue;

            var operands = op.Operands;
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push((fill, stroke, width));
                    break;

                case OpCodeName.Q:
                    // A Q with nothing put away is malformed content; read on rather than throw.
                    if (saved.Count > 0)
                        (fill, stroke, width) = saved.Pop();
                    break;

                case OpCodeName.w:
                    if (operands.Count >= 1)
                        width = Number(operands[0]);
                    break;

                case OpCodeName.rg:
                    if (operands.Count >= 3)
                        fill = Rgb(Number(operands[0]), Number(operands[1]), Number(operands[2]));
                    break;

                case OpCodeName.g:
                    if (operands.Count >= 1)
                        fill = Rgb(Number(operands[0]), Number(operands[0]), Number(operands[0]));
                    break;

                case OpCodeName.RG:
                    if (operands.Count >= 3)
                        stroke = Rgb(Number(operands[0]), Number(operands[1]), Number(operands[2]));
                    break;

                case OpCodeName.G:
                    if (operands.Count >= 1)
                        stroke = Rgb(Number(operands[0]), Number(operands[0]), Number(operands[0]));
                    break;

                case OpCodeName.m:
                case OpCodeName.l:
                    if (operands.Count >= 2)
                        Add(Number(operands[0]), Number(operands[1]));
                    break;

                case OpCodeName.c:
                    if (operands.Count >= 6)
                    {
                        Add(Number(operands[0]), Number(operands[1]));
                        Add(Number(operands[2]), Number(operands[3]));
                        Add(Number(operands[4]), Number(operands[5]));
                        curves++;
                    }
                    break;

                // The two curve forms that borrow a control point from an end point.
                case OpCodeName.v:
                case OpCodeName.y:
                    if (operands.Count >= 4)
                    {
                        Add(Number(operands[0]), Number(operands[1]));
                        Add(Number(operands[2]), Number(operands[3]));
                        curves++;
                    }
                    break;

                case OpCodeName.re:
                    if (operands.Count >= 4)
                    {
                        var x = Number(operands[0]);
                        var y = Number(operands[1]);
                        var w = Number(operands[2]);
                        var h = Number(operands[3]);
                        Add(x, y);
                        Add(x + w, y);
                        Add(x + w, y + h);
                        Add(x, y + h);
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
                    Discard();
                    break;
            }
        }

        return painted;
    }

    /// <summary>The paths the page fills in the given colour, in the order it fills them.</summary>
    internal static IReadOnlyList<Path> FilledIn(PdfPage page, string colour) =>
        [..On(page).Where(path => path.Filled && path.FillColour == colour)];

    /// <summary>The paths the page strokes in the given colour, in the order it strokes them.</summary>
    internal static IReadOnlyList<Path> StrokedIn(PdfPage page, string colour) =>
        [..On(page).Where(path => path.Stroked && path.StrokeColour == colour)];

    // Written exactly as PaintedRectangles writes a colour, so that its ColourOf names one here too.
    private static string Rgb(double r, double g, double b) =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", r, g, b);

    private static double Number(CObject operand) => operand switch
    {
        CInteger integer => integer.Value,
        CReal real => real.Value,
        _ => throw new InvalidOperationException("Operand is not a number: " + operand)
    };
}
