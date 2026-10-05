using System;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Reads an operator's operands as the values they spell. Every content-stream reader in the
///   tests needs this, and each used to carry its own private copy; three of them did not even agree
///   on what a non-number was.
/// </summary>
/// <remarks>
///   The library has the same thing on <c>COperator</c>, but it is internal and this repository
///   carries no <c>InternalsVisibleTo</c>, so the tests keep this one copy of their own. Linked into
///   <c>PinataLayout.Rendering.Tests</c> and <c>PdfPinata.Charting.Tests</c> the way the readers that
///   call it are, so a change here reaches all three test projects.
/// </remarks>
internal static class ContentOperands
{
    /// <summary>
    ///   The value of an operand the reader knows is a number, an integer or a real. Anything else
    ///   throws, because a reader that asked for a number where the stream has none has misread
    ///   the stream, and a zero in its place would be a plausible coordinate.
    /// </summary>
    internal static double Number(CObject operand) => operand switch
    {
        CInteger integer => integer.Value,
        CReal real => real.Value,
        _ => throw new InvalidOperationException("Operand is not a number: " + operand)
    };

    /// <summary>
    ///   The value of an operand that is a number, and zero for one that is not. For a reader that
    ///   converts the operands of every operator before it knows which operator it has, so that a
    ///   name or a string among them is passed over rather than thrown on.
    /// </summary>
    internal static double NumberOrZero(CObject operand) => operand switch
    {
        CInteger integer => integer.Value,
        CReal real => real.Value,
        _ => 0
    };
}
