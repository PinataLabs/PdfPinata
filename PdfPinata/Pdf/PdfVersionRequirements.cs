namespace PdfPinata.Pdf;

/// <summary>
/// Where a feature that needs a newer PDF version than the document's default raises the floor —
/// so that fixing what one feature requires is not every module independently agreeing to raise
/// the same number.
/// </summary>
/// <remarks>
/// <see cref="Advanced.PdfAttachments"/> raises the floor for <c>/AF</c> and <c>/UF</c>,
/// <see cref="PdfDocument"/> raises it for a cross-reference stream,
/// <see cref="Metadata.PdfConformanceWriter"/> raises it for the PDF/A profile claimed, and
/// <see cref="Advanced.PdfFont"/> raises it for a PostScript-outline font embedded as
/// <c>/FontFile3 /OpenType</c>. All only ever raise: a document that has already asked for
/// something newer keeps it.
/// </remarks>
internal static class PdfVersionRequirements
{
    public static void Require(PdfDocument document, int floor)
    {
        if (document._version < floor)
            document._version = floor;
    }
}
