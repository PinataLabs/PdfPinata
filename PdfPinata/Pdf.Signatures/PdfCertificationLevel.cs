namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// What a certifying signature permits a later revision to change.
/// </summary>
/// <remarks>
/// <para>
/// An ordinary — "approval" — signature says only that the signer saw the document as it then stood.
/// A <em>certifying</em> signature additionally declares which further changes are legitimate, and a
/// reader that sees a later revision doing anything else reports the document as altered.
/// </para>
/// <para>
/// A document may carry <b>one</b> certifying signature and it must be the first one applied. That
/// rule belongs to the reader rather than to this library: certifying an already-signed document
/// produces a file that opens and is reported as invalid.
/// </para>
/// </remarks>
public enum PdfCertificationLevel
{
    /// <summary>
    /// An ordinary approval signature, claiming nothing about what may follow it.
    /// </summary>
    NotCertified = 0,

    /// <summary>
    /// No change at all is permitted after this signature.
    /// </summary>
    NoChangesAllowed = 1,

    /// <summary>
    /// Form fields may still be filled in and signatures added.
    /// </summary>
    FormFillingAllowed = 2,

    /// <summary>
    /// Form filling, signing, and adding or changing annotations are all still permitted.
    /// </summary>
    FormFillingAndAnnotationsAllowed = 3
}
