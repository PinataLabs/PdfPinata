namespace PdfPinata.Pdf.Content;

/// <summary>
/// One place a content stream could not be read, as <see cref="CParser.ReadContentPastFaults"/>
/// met it.
/// </summary>
internal readonly struct ContentFault
{
    internal ContentFault(int operatorIndex, string message, string unknownOperator)
    {
        OperatorIndex = operatorIndex;
        Message = message;
        UnknownOperator = unknownOperator;
    }

    /// <summary>
    /// How many operators had been read when the fault was met: the index, in the sequence read,
    /// of the first operator after it.
    /// </summary>
    internal int OperatorIndex { get; }

    /// <summary>What went wrong.</summary>
    internal string Message { get; }

    /// <summary>
    /// The name of the operator, when the fault is a name that is no operator at all, and null
    /// for any other fault. Kept apart because such a name is not an error between <c>BX</c> and
    /// <c>EX</c>, and only whoever follows those two knows whether it lies between them.
    /// </summary>
    internal string UnknownOperator { get; }
}
