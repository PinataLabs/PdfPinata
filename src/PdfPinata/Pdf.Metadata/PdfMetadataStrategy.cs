namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// What a save does with the document's XMP metadata packet - the <c>/Metadata</c> stream on the
/// catalog. Set through <see cref="PdfDocumentOptions.MetadataStrategy"/>.
/// </summary>
/// <remarks>
/// <para>
/// The information dictionary (<see cref="PdfDocument.Info"/>) and the XMP packet say the same
/// things under different names. A document built here has no packet unless one is asked for; a
/// document read from a file keeps the packet it came with, and that packet goes stale the moment
/// the title or the author is changed through <see cref="PdfDocument.Info"/>, because nothing
/// rewrites it. This is how a caller says which of those they want.
/// </para>
/// <para>
/// <b>A conformance claim always writes a fresh packet</b>, whatever this says short of
/// <see cref="NoMetadata"/>: PDF/A and PDF/UA are claimed <em>in</em> the packet, and it has to
/// agree with the information dictionary for a validator to accept either. A claim together with
/// <see cref="NoMetadata"/> is refused, since it asks for a file that cannot carry its own claim.
/// </para>
/// <para>
/// There is no "user generated" member of the kind upstream PDFsharp has. What it is for - user
/// code shaping the packet - is <see cref="PdfDocument.CustomizeMetadata"/> and
/// <see cref="PdfDocument.AddMetadataContributor"/>, which see every packet
/// <see cref="AutoGenerate"/> writes and can add anything to it.
/// </para>
/// </remarks>
public enum PdfMetadataStrategy
{
    /// <summary>
    /// The default, and what a save has always done: write no packet unless a conformance claim
    /// needs one, and leave a packet a read document already has exactly as it was.
    /// </summary>
    KeepExisting = 0,

    /// <summary>
    /// Write a packet built from the information dictionary on every save, replacing any the
    /// document already had - so a read document's packet cannot go stale. The same as
    /// <see cref="PdfDocumentOptions.WriteXmpMetadata"/> set to true.
    /// </summary>
    AutoGenerate = 1,

    /// <summary>
    /// Write no packet, and remove one a read document already has. Refused together with a
    /// conformance claim.
    /// </summary>
    NoMetadata = 2
}
