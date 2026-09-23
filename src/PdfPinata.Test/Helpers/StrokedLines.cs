using System;
using System.Collections.Generic;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Works out which straight lines a page strokes, for the tests that care about where rules
///   and borders are drawn. Reading the segments out of the content is exact, where rasterizing
///   and looking is neither exact nor available on every machine.
/// </summary>
internal static class StrokedLines
{
    /// <summary>The colour a page strokes in until it names another one.</summary>
    internal const string Black = "0,0,0";

    private static readonly System.Globalization.CultureInfo Invariant =
        System.Globalization.CultureInfo.InvariantCulture;

    internal readonly struct Line
    {
        internal Line(double x1, double y1, double x2, double y2, double width, string colour)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Width = width;
            Colour = colour;
        }

        internal double X1 { get; }
        internal double Y1 { get; }
        internal double X2 { get; }
        internal double Y2 { get; }
        internal double Width { get; }

        /// <summary>
        ///   The stroking colour in force when the segment was drawn, as "r,g,b" with each
        ///   component between 0 and 1. A page that never names one strokes in black. A colour
        ///   named in grey or in CMYK is reported converted, so that one form of assertion reads
        ///   every page whichever space it was written in.
        /// </summary>
        internal string Colour { get; }

        internal bool IsVertical => Math.Abs(X1 - X2) < 0.001;
        internal bool IsHorizontal => Math.Abs(Y1 - Y2) < 0.001;

        /// <summary>The lower of the two ends, which for a vertical line is its foot.</summary>
        internal double Bottom => Math.Min(Y1, Y2);

        /// <summary>The higher of the two ends, which for a vertical line is its head.</summary>
        internal double Top => Math.Max(Y1, Y2);

        public override string ToString()
        {
            var kind = IsVertical ? "V" : IsHorizontal ? "H" : "?";
            return $"{kind} ({X1:F2},{Y1:F2})-({X2:F2},{Y2:F2}) w={Width:F2} rgb={Colour}";
        }
    }

    /// <summary>
    ///   Every straight segment the page strokes, in the order it is drawn. Positions are in
    ///   points from the bottom left of the page, as PDF measures them.
    /// </summary>
    internal static IReadOnlyList<Line> Of(PdfPage page)
    {
        var lines = new List<Line>();

        // The segments of the path being built. A path is not painted until its operator says
        // how, so they are held here and kept only if that operator strokes.
        var path = new List<Line>();

        // The current point and the point the subpath began at.
        double x = 0, y = 0, startX = 0, startY = 0;
        var inSubpath = false;

        // The graphics state a stroked segment is drawn under. A q puts a copy of it away and the
        // matching Q brings that copy back, so anything named inside the pair stops applying at
        // the end of it. Reading a page without following that reports the colour and the width of
        // an inner scope for every segment drawn after it.
        double width = 1;
        var colour = Black;
        var saved = new Stack<(double Width, string Colour)>();

        foreach (var item in ContentReader.ReadContent(ContentOf(page)))
        {
            if (item is not COperator op)
                continue;

            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.w:
                    if (op.Operands.Count >= 1)
                        width = Number(op.Operands[0]);
                    break;

                case OpCodeName.q:
                    saved.Push((width, colour));
                    break;

                case OpCodeName.Q:
                    // A Q with nothing put away is malformed content; read on rather than throw.
                    if (saved.Count > 0)
                        (width, colour) = saved.Pop();
                    break;

                // The stroking colour, in whichever of the three device spaces it is named.
                case OpCodeName.RG:
                    if (op.Operands.Count >= 3)
                        colour = Rgb(Number(op.Operands[0]), Number(op.Operands[1]), Number(op.Operands[2]));
                    break;

                case OpCodeName.G:
                    if (op.Operands.Count >= 1)
                    {
                        var grey = Number(op.Operands[0]);
                        colour = Rgb(grey, grey, grey);
                    }
                    break;

                case OpCodeName.K:
                    if (op.Operands.Count >= 4)
                        colour = Cmyk(Number(op.Operands[0]), Number(op.Operands[1]),
                            Number(op.Operands[2]), Number(op.Operands[3]));
                    break;

                case OpCodeName.m:
                    if (op.Operands.Count >= 2)
                    {
                        x = startX = Number(op.Operands[0]);
                        y = startY = Number(op.Operands[1]);
                        inSubpath = true;
                    }
                    break;

                case OpCodeName.l:
                    if (inSubpath && op.Operands.Count >= 2)
                    {
                        var toX = Number(op.Operands[0]);
                        var toY = Number(op.Operands[1]);
                        path.Add(new Line(x, y, toX, toY, width, colour));
                        x = toX;
                        y = toY;
                    }
                    break;

                case OpCodeName.h:
                    CloseSubpath();
                    break;

                // Close the path and then stroke it.
                case OpCodeName.s:
                case OpCodeName.b:
                case OpCodeName.bx:
                    CloseSubpath();
                    goto case OpCodeName.S;

                // Stroke the path, filling it first or not.
                case OpCodeName.S:
                case OpCodeName.B:
                case OpCodeName.Bx:
                    lines.AddRange(path);
                    path.Clear();
                    inSubpath = false;
                    break;

                // Fill the path, or paint nothing at all: either way nothing is stroked.
                case OpCodeName.f:
                case OpCodeName.F:
                case OpCodeName.fx:
                case OpCodeName.n:
                    path.Clear();
                    inSubpath = false;
                    break;
            }
        }

        return lines;

        // Closing a subpath draws the segment back to where it began.
        void CloseSubpath()
        {
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (inSubpath && (x != startX || y != startY))
                path.Add(new Line(x, y, startX, startY, width, colour));
            // ReSharper restore CompareOfFloatsByEqualityOperator

            x = startX;
            y = startY;
        }
    }

    private static byte[] ContentOf(PdfPage page)
    {
        var item = page.Elements["/Contents"];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is not PdfArray streams)
            return ((PdfDictionary)item).Stream.UnfilteredValue;

        // The streams of a page are one stream broken up, and a token may span the break.
        var joined = new List<byte>();
        for (var idx = 0; idx < streams.Elements.Count; idx++)
        {
            joined.AddRange(streams.Elements.GetDictionary(idx).Stream.UnfilteredValue);
            joined.Add((byte)'\n');
        }
        return joined.ToArray();
    }

    /// <summary>A colour as this reports one: the three components, comma separated.</summary>
    private static string Rgb(double red, double green, double blue)
    {
        // Four places is finer than anything a colour is written to and coarse enough that the
        // arithmetic below does not leave a component reading 0.30000000000000004.
        return string.Join(",",
            Math.Round(red, 4).ToString(Invariant),
            Math.Round(green, 4).ToString(Invariant),
            Math.Round(blue, 4).ToString(Invariant));
    }

    /// <summary>
    ///   A CMYK colour as the plain conversion gives it, which is what a reader with no colour
    ///   profile to go by does. It is exact for the primaries a test asks a border to be drawn in.
    /// </summary>
    private static string Cmyk(double cyan, double magenta, double yellow, double black)
    {
        return Rgb((1 - cyan) * (1 - black), (1 - magenta) * (1 - black), (1 - yellow) * (1 - black));
    }

    private static double Number(CObject operand)
    {
        return operand switch
        {
            CInteger integer => integer.Value,
            CReal real => real.Value,
            _ => 0.0
        };
    }
}
