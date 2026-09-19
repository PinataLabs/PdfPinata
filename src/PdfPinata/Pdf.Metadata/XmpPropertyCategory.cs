namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// Where an XMP extension schema property's value came from — the choice ISO 19005 clause 6.6.2.3.1
/// asks a schema declaration to make for each property it describes.
/// </summary>
public enum XmpPropertyCategory
{
    /// <summary>The value is derived from the document's own content.</summary>
    Internal,

    /// <summary>The value came from outside the document.</summary>
    External
}
