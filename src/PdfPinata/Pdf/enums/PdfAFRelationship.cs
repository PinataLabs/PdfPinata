namespace PdfPinata.Pdf;

/// <summary>
/// What an attached file has to do with the document it is attached to, written as
/// <c>/AFRelationship</c> on the file specification.
/// </summary>
/// <remarks>
/// PDF/A-3 is the only archival profile that may carry an attachment at all, and it requires every
/// one to say which of these it is. That is the point of the entry rather than a formality: an
/// archive read fifty years from now has to be able to tell the authoritative source of a document
/// from a rendering of it without opening either, and a reader deciding what to do with an
/// attachment has nothing else to go on.
/// <para>
/// These are the values ISO 19005-3 defines. ISO 32000-2 adds <c>/EncryptedPayload</c>,
/// <c>/FormData</c> and <c>/Schema</c>, and they are deliberately absent: a PDF/A-3 validator
/// rejects them, so offering them here would be offering a way to write a file that fails the one
/// profile this enumeration exists for. A document that wants one and claims no conformance can
/// write the name itself with <c>Elements.SetName("/AFRelationship", …)</c>.
/// </para>
/// </remarks>
public enum PdfAFRelationship
{
    /// <summary>
    /// Nothing more precise is known. Written as <c>/Unspecified</c> rather than left out, because
    /// PDF/A-3 asks for the entry to be there and this is the value it gives for saying nothing —
    /// an absent entry is a broken file, whereas this is an honest one.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The attached file is the source this document was produced from — a spreadsheet a report was
    /// generated out of, say. The pages are derived; the attachment is the original.
    /// </summary>
    Source,

    /// <summary>
    /// The attached file holds the same information as the document, in machine-readable form. This
    /// is what a hybrid e-invoice uses: the pages are what a person reads, the attached UN/CEFACT
    /// CII XML is what a system reads, and the two are one invoice. ZUGFeRD and Factur-X both want
    /// this value.
    /// </summary>
    Data,

    /// <summary>
    /// The attached file is an alternative representation of the whole document, such as an audio
    /// rendering of it.
    /// </summary>
    Alternative,

    /// <summary>
    /// The attached file supplements the document — the full dataset behind a summary, or the
    /// working that a figure was drawn from.
    /// </summary>
    Supplement
}
