namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Which fields a signature field's lock covers - ISO 32000-1 Table 233, <c>/Action</c>.
/// </summary>
public enum PdfFieldLockAction
{
    /// <summary>
    /// Every field in the document.
    /// </summary>
    All,

    /// <summary>
    /// Only the fields named in <see cref="PdfSignatureFieldLock.Fields"/>.
    /// </summary>
    Include,

    /// <summary>
    /// Every field except those named in <see cref="PdfSignatureFieldLock.Fields"/>.
    /// </summary>
    Exclude,
}
