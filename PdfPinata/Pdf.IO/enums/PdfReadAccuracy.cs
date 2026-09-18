namespace PdfPinata.Pdf.IO.enums;

/// <summary>How strictly a document is read, deciding whether a recoverable fault stops the read or is worked around.</summary>
public enum PdfReadAccuracy
{
    /// <summary>A fault in the file stops the read and is reported.</summary>
    Strict,
    /// <summary>A fault the reader can work around is worked around, so that a damaged document still opens.</summary>
    Moderate
}
