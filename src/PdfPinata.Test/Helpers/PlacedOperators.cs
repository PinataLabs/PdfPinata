using System.Collections.Generic;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Every operator of a page's content stream, each paired with the transformation matrix in force
///   where it was written — the <c>cm</c> operators followed through every <c>q</c> and <c>Q</c> —
///   so a test can say where on the page something landed rather than which numbers were written.
/// </summary>
internal static class PlacedOperators
{
    internal sealed record Placed(OpCodeName Name, double[] Operands, XMatrix Ctm)
    {
        /// <summary>A point written as this operator's operands, carried into default user space.</summary>
        public XPoint At(int index) => Ctm.Transform(new XPoint(Operands[index], Operands[index + 1]));
    }

    internal static List<Placed> Of(PdfPage page)
    {
        var ctm = new XMatrix();
        var saved = new Stack<XMatrix>();
        var placed = new List<Placed>();

        foreach (var op in ContentReader.ReadContent(PageContent.Of(page)).OfType<COperator>())
        {
            var operands = op.Operands.Select(Number).ToArray();
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push(ctm);
                    break;

                case OpCodeName.Q:
                    ctm = saved.Pop();
                    break;

                case OpCodeName.cm:
                    ctm.Prepend(new XMatrix(operands[0], operands[1], operands[2], operands[3],
                        operands[4], operands[5]));
                    break;
            }
            placed.Add(new Placed(op.OpCode.OpCodeName, operands, ctm));
        }

        return placed;
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
