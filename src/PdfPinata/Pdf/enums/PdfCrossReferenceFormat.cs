namespace PdfPinata.Pdf;

/// <summary>
/// Selects how the objects of a document are indexed when it is saved.
/// </summary>
public enum PdfCrossReferenceFormat
{
    /// <summary>
    /// A cross-reference table and a trailer dictionary, as PDF 1.4 and earlier had them. Every
    /// object is written out on its own, uncompressed. Readable by anything that reads PDF at all.
    /// </summary>
    Classic,

    /// <summary>
    /// A cross-reference stream, with the objects that may go in one gathered into object streams
    /// and compressed together. Smaller, and needs a reader that understands PDF 1.5.
    /// </summary>
    Stream
}
