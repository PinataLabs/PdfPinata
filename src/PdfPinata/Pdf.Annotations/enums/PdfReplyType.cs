namespace PdfPinata.Pdf.Annotations;

/// <summary>
/// How a markup annotation relates to the one it replies to - ISO 32000-1 Table 170, <c>/RT</c>.
/// </summary>
public enum PdfReplyType
{
    /// <summary>
    /// A reply to the other annotation, shown beneath it in a thread. The default.
    /// </summary>
    Reply,

    /// <summary>
    /// Grouped with the other annotation, which a reader shows and treats as one.
    /// </summary>
    Group,
}
