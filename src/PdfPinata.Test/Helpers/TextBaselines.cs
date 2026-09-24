using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Works out where on a page each run of text is drawn, for the tests that care about what
///   is laid out where rather than about what it says. Reading the positions out of the
///   content is exact, where rasterizing and looking is neither exact nor available on every
///   machine.
/// </summary>
internal static class TextBaselines
{
    /// <summary>
    ///   The vertical position of every run of text on the page, in the order it is drawn.
    ///   Positions are in points from the bottom of the page, as PDF measures them.
    /// </summary>
    internal static IReadOnlyList<double> Of(PdfPage page)
    {
        return [.. PositionsOf(page).Select(position => position.Y)];
    }

    /// <summary>
    ///   Where every run of text on the page starts, in the order it is drawn. Positions are in
    ///   points from the bottom left of the page, as PDF measures them.
    /// </summary>
    internal static IReadOnlyList<(double X, double Y)> PositionsOf(PdfPage page)
    {
        var reader = new PositionReader();
        foreach (var item in ContentReader.ReadContent(ContentOf(page)))
        {
            if (item is COperator op)
                reader.Read(op);
        }

        return reader.Baselines;
    }

    /// <summary>Follows one page's text, one operator at a time.</summary>
    private sealed class PositionReader
    {
        // The text line matrix, which Td moves and every run of text is drawn against. Only
        // the translation is tracked: nothing here draws text turned or scaled.
        private double _x, _y;

        // The distance from one line to the next, which T* and the quote operators move by.
        private double _leading;

        internal List<(double X, double Y)> Baselines { get; } = [];

        internal void Read(COperator op)
        {
            if (!FollowTextPosition(op))
                ShowText(op.OpCode.OpCodeName);
        }

        private bool FollowTextPosition(COperator op)
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.BT:
                    _x = _y = 0;
                    return true;

                case OpCodeName.Td:
                case OpCodeName.TD:
                    MoveLine(op);
                    return true;

                case OpCodeName.TL:
                    if (op.Operands.Count >= 1)
                        _leading = Number(op.Operands[0]);
                    return true;

                case OpCodeName.Tx:
                    // T* is 0 -TL Td: down one line, and back to where this line began.
                    _y -= _leading;
                    return true;

                case OpCodeName.Tm:
                    SetLine(op.Operands);
                    return true;

                default:
                    return false;
            }
        }

        private void MoveLine(COperator op)
        {
            if (op.Operands.Count < 2)
                return;

            _x += Number(op.Operands[0]);
            _y += Number(op.Operands[1]);

            // TD sets the leading to the distance it moved down by, as well.
            if (op.OpCode.OpCodeName == OpCodeName.TD)
                _leading = -Number(op.Operands[1]);
        }

        private void SetLine(CSequence operands)
        {
            if (operands.Count < 6)
                return;

            _x = Number(operands[4]);
            _y = Number(operands[5]);
        }

        private void ShowText(OpCodeName name)
        {
            switch (name)
            {
                case OpCodeName.Tj:
                case OpCodeName.TJ:
                    Baselines.Add((_x, _y));
                    break;

                case OpCodeName.QuoteSingle:
                case OpCodeName.QuoteDbl:
                    // Both move down a line before showing the text, as T* does.
                    _y -= _leading;
                    Baselines.Add((_x, _y));
                    break;
            }
        }
    }

    /// <summary>
    ///   The distinct lines the page draws text on, from the top of the page downwards.
    /// </summary>
    internal static IReadOnlyList<double> LinesOf(PdfPage page)
    {
        return
        [
            .. Of(page)
                .Select(baseline => Math.Round(baseline, 3))
                .Distinct()
                .OrderByDescending(baseline => baseline)
        ];
    }

    private static byte[] ContentOf(PdfPage page) => PageContent.Of(page);

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
