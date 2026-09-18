using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
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
        return PositionsOf(page).Select(position => position.Y).ToList();
    }

    /// <summary>
    ///   Where every run of text on the page starts, in the order it is drawn. Positions are in
    ///   points from the bottom left of the page, as PDF measures them.
    /// </summary>
    internal static IReadOnlyList<(double X, double Y)> PositionsOf(PdfPage page)
    {
        var baselines = new List<(double X, double Y)>();

        // The text line matrix, which Td moves and every run of text is drawn against. Only
        // the translation is tracked: nothing here draws text turned or scaled.
        double x = 0, y = 0;

        // The distance from one line to the next, which T* and the quote operators move by.
        double leading = 0;

        foreach (var item in ContentReader.ReadContent(ContentOf(page)))
        {
            if (item is not COperator op)
                continue;

            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.BT:
                    x = y = 0;
                    break;

                case OpCodeName.Td:
                case OpCodeName.TD:
                    if (op.Operands.Count >= 2)
                    {
                        x += Number(op.Operands[0]);
                        y += Number(op.Operands[1]);

                        // TD sets the leading to the distance it moved down by, as well.
                        if (op.OpCode.OpCodeName == OpCodeName.TD)
                            leading = -Number(op.Operands[1]);
                    }
                    break;

                case OpCodeName.TL:
                    if (op.Operands.Count >= 1)
                        leading = Number(op.Operands[0]);
                    break;

                case OpCodeName.Tx:
                    // T* is 0 -TL Td: down one line, and back to where this line began.
                    y -= leading;
                    break;

                case OpCodeName.Tm:
                    if (op.Operands.Count >= 6)
                    {
                        x = Number(op.Operands[4]);
                        y = Number(op.Operands[5]);
                    }
                    break;

                case OpCodeName.Tj:
                case OpCodeName.TJ:
                    baselines.Add((x, y));
                    break;

                case OpCodeName.QuoteSingle:
                case OpCodeName.QuoteDbl:
                    // Both move down a line before showing the text, as T* does.
                    y -= leading;
                    baselines.Add((x, y));
                    break;
            }
        }

        return baselines;
    }

    /// <summary>
    ///   The distinct lines the page draws text on, from the top of the page downwards.
    /// </summary>
    internal static IReadOnlyList<double> LinesOf(PdfPage page)
    {
        return Of(page)
            .Select(baseline => Math.Round(baseline, 3))
            .Distinct()
            .OrderByDescending(baseline => baseline)
            .ToList();
    }

    static byte[] ContentOf(PdfPage page) => PageContent.Of(page);

    static double Number(CObject operand)
    {
        return operand switch
        {
            CInteger integer => integer.Value,
            CReal real => real.Value,
            _ => 0.0
        };
    }
}
