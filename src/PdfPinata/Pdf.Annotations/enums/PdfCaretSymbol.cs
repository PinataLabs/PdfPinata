namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// What a caret annotation stands for - ISO 32000-1 Table 183, <c>/Sy</c>.
/// </summary>
public enum PdfCaretSymbol
{
    /// <summary>
    /// A plain insertion point. The default, written by leaving the entry out.
    /// </summary>
    None,

    /// <summary>
    /// A new paragraph - <c>/P</c>, which a reader may show as a pilcrow.
    /// </summary>
    Paragraph
}
