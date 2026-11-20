using System;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Reads the operands of an operator as what it takes, and says so when they are something else.
/// </summary>
/// <remarks>
/// Operands are read from the end. Where an operator is given more than it takes, the extra ones
/// come first: they are what an operator before it left behind, and viewers draw the operator
/// from its own.
/// </remarks>
internal static class Operands
{
    /// <summary>
    /// Fills <paramref name="into"/> with the last operands, when there are enough of them and
    /// each is a finite number.
    /// </summary>
    internal static bool TryNumbers(COperator op, Span<double> into) => TryNumbers(op, into, 0);

    /// <summary>
    /// As <see cref="TryNumbers(COperator, Span{double})"/>, for the numbers that come before the
    /// last <paramref name="skip"/> operands.
    /// </summary>
    internal static bool TryNumbers(COperator op, Span<double> into, int skip)
    {
        var first = op.Operands.Count - skip - into.Length;
        if (first < 0)
            return false;

        for (var index = 0; index < into.Length; index++)
        {
            if (!op.TryNumberOperand(first + index, out into[index]) || !double.IsFinite(into[index]))
                return false;
        }

        return true;
    }

    /// <summary>The last operand as a finite number.</summary>
    internal static bool TryNumber(COperator op, out double value)
    {
        Span<double> number = stackalloc double[1];
        var read = TryNumbers(op, number);
        value = number[0];
        return read;
    }

    /// <summary>The operand <paramref name="fromEnd"/> places from the last, 0 being the last, or null.</summary>
    internal static CObject FromEnd(COperator op, int fromEnd)
    {
        var index = op.Operands.Count - 1 - fromEnd;
        return index >= 0 ? op.Operands[index] : null;
    }

    /// <summary>
    /// The name, with its slash, <paramref name="fromEnd"/> places from the last operand, or null
    /// when what is there is not a name.
    /// </summary>
    internal static string NameFromEnd(COperator op, int fromEnd) => (FromEnd(op, fromEnd) as CName)?.Name;
}
