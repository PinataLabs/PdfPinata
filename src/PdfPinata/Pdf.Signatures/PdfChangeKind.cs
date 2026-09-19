namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// What kind of change an operation that guards on <c>PdfDocument.EnsureCanModify</c> is making, so a
/// certifying signature's <see cref="PdfCertificationLevel"/> can permit some kinds and refuse others.
/// </summary>
/// <remarks>
/// The three kinds the standard itself distinguishes, and no more — that is what the three
/// certification levels are defined in terms of, and inventing finer categories would mean deciding
/// things the standard does not. An open mode that forbids modification refuses every kind alike;
/// this only matters once that question has already answered yes.
/// </remarks>
internal enum PdfChangeKind
{
    /// <summary>
    /// A change to the document's structure or content: pages, drawing, the catalog's own settings.
    /// Never permitted once a document is certified, at any level.
    /// </summary>
    DocumentStructure,

    /// <summary>
    /// Adding or changing an annotation. Permitted only by
    /// <see cref="PdfCertificationLevel.FormFillingAndAnnotationsAllowed"/>.
    /// </summary>
    Annotations,

    /// <summary>
    /// Filling in a form field's value, or adding a signature. Permitted by
    /// <see cref="PdfCertificationLevel.FormFillingAllowed"/> and
    /// <see cref="PdfCertificationLevel.FormFillingAndAnnotationsAllowed"/>.
    /// </summary>
    FormFieldValues
}
