namespace PdfPinata.Text;

/// <summary>
/// What the line breaking algorithm, UAX #14, says of one position in a text.
/// </summary>
public enum LineBreakOpportunity : byte
{
    /// <summary>A line may not be broken here (UAX #14's "×").</summary>
    None = 0,

    /// <summary>A line may be broken here (UAX #14's "÷").</summary>
    Allowed = 1,

    /// <summary>
    /// A line must be broken here (UAX #14's "!"): after a hard line break, and at the end of the
    /// text.
    /// </summary>
    Mandatory = 2,
}
