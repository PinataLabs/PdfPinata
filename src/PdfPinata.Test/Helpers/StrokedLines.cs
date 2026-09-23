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
        var reader = new LineReader();
        foreach (var item in ContentReader.ReadContent(ContentOf(page)))
        {
            if (item is COperator op)
                reader.Read(op);
        }

        return reader.Lines;
    }

    /// <summary>
    ///   What each path-painting operator does with the path: whether it closes the subpath
    ///   first, and whether it strokes it. Filling is not recorded, because a filled path leaves
    ///   no line; <c>n</c> paints nothing at all.
    /// </summary>
    private static readonly Dictionary<OpCodeName, (bool Closes, bool Strokes)> Painting = new()
    {
        // Close the path and then stroke it.
        [OpCodeName.s] = (true, true),
        [OpCodeName.b] = (true, true),
        [OpCodeName.bx] = (true, true),

        // Stroke the path, filling it first or not.
        [OpCodeName.S] = (false, true),
        [OpCodeName.B] = (false, true),
        [OpCodeName.Bx] = (false, true),

        // Fill the path, or paint nothing at all: either way nothing is stroked.
        [OpCodeName.f] = (false, false),
        [OpCodeName.F] = (false, false),
        [OpCodeName.fx] = (false, false),
        [OpCodeName.n] = (false, false)
    };

    /// <summary>Follows one page's content, one operator at a time.</summary>
    private sealed class LineReader
    {
        // The segments of the path being built. A path is not painted until its operator says
        // how, so they are held here and kept only if that operator strokes.
        private readonly List<Line> _path = [];

        // The current point and the point the subpath began at.
        private double _x, _y, _startX, _startY;
        private bool _inSubpath;

        // The graphics state a stroked segment is drawn under. A q puts a copy of it away and the
        // matching Q brings that copy back, so anything named inside the pair stops applying at
        // the end of it. Reading a page without following that reports the colour and the width of
        // an inner scope for every segment drawn after it.
        private double _width = 1;
        private string _colour = Black;
        private readonly Stack<(double Width, string Colour)> _saved = new();

        internal List<Line> Lines { get; } = [];

        internal void Read(COperator op)
        {
            var name = op.OpCode.OpCodeName;
            if (FollowGraphicsState(name, op.Operands) || SetColour(name, op.Operands) || BuildPath(name, op.Operands))
                return;

            PaintPath(name);
        }

        private bool FollowGraphicsState(OpCodeName name, CSequence operands)
        {
            switch (name)
            {
                case OpCodeName.w:
                    if (operands.Count >= 1)
                        _width = Number(operands[0]);
                    return true;

                case OpCodeName.q:
                    _saved.Push((_width, _colour));
                    return true;

                case OpCodeName.Q:
                    // A Q with nothing put away is malformed content; read on rather than throw.
                    if (_saved.Count > 0)
                        (_width, _colour) = _saved.Pop();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>The stroking colour, in whichever of the three device spaces it is named.</summary>
        private bool SetColour(OpCodeName name, CSequence operands)
        {
            switch (name)
            {
                case OpCodeName.RG:
                    if (operands.Count >= 3)
                        _colour = Rgb(Number(operands[0]), Number(operands[1]), Number(operands[2]));
                    return true;

                case OpCodeName.G:
                    if (operands.Count >= 1)
                    {
                        var grey = Number(operands[0]);
                        _colour = Rgb(grey, grey, grey);
                    }
                    return true;

                case OpCodeName.K:
                    if (operands.Count >= 4)
                        _colour = Cmyk(Number(operands[0]), Number(operands[1]),
                            Number(operands[2]), Number(operands[3]));
                    return true;

                default:
                    return false;
            }
        }

        private bool BuildPath(OpCodeName name, CSequence operands)
        {
            switch (name)
            {
                case OpCodeName.m:
                    MoveTo(operands);
                    return true;

                case OpCodeName.l:
                    LineTo(operands);
                    return true;

                case OpCodeName.h:
                    CloseSubpath();
                    return true;

                default:
                    return false;
            }
        }

        private void MoveTo(CSequence operands)
        {
            if (operands.Count < 2)
                return;

            _x = _startX = Number(operands[0]);
            _y = _startY = Number(operands[1]);
            _inSubpath = true;
        }

        private void LineTo(CSequence operands)
        {
            if (!_inSubpath || operands.Count < 2)
                return;

            var toX = Number(operands[0]);
            var toY = Number(operands[1]);
            _path.Add(new Line(_x, _y, toX, toY, _width, _colour));
            _x = toX;
            _y = toY;
        }

        // Closing a subpath draws the segment back to where it began.
        private void CloseSubpath()
        {
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (_inSubpath && (_x != _startX || _y != _startY))
                _path.Add(new Line(_x, _y, _startX, _startY, _width, _colour));
            // ReSharper restore CompareOfFloatsByEqualityOperator

            _x = _startX;
            _y = _startY;
        }

        private void PaintPath(OpCodeName name)
        {
            if (!Painting.TryGetValue(name, out var paint))
                return;

            if (paint.Closes)
                CloseSubpath();
            if (paint.Strokes)
                Lines.AddRange(_path);

            _path.Clear();
            _inSubpath = false;
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
        return [..joined];
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
