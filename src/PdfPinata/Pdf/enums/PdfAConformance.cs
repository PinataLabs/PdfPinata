namespace PdfPinata.Pdf;

/// <summary>
/// The archival profile a document claims to conform to.
/// </summary>
/// <remarks>
/// Setting this does more than label the file. The writer refuses to save a document that breaks a
/// rule of the profile it claims, because a conformance claim discovered to be false by a validator
/// — or by a customer — is worse than no claim at all.
/// <para>
/// The <c>A</c> ("accessible") levels are here as well as the <c>B</c> ("basic") ones. An <c>A</c>
/// level is the archival rules of its part plus the tagging rules
/// <see cref="Structure.PdfUaValidator"/> already holds a document to — checked at the claim as well
/// as at <c>Save</c>, because a document with no structure tree cannot become tagged by being saved.
/// </para>
/// </remarks>
public enum PdfAConformance
{
    /// <summary>
    /// No claim. The document is an ordinary PDF and nothing is enforced.
    /// </summary>
    None,

    /// <summary>
    /// PDF/A-1b (ISO 19005-1). The strictest of the three: no transparency, no JPXDecode, no
    /// embedded files, and PDF 1.4 constructs only.
    /// </summary>
    PdfA1B,

    /// <summary>
    /// PDF/A-2b (ISO 19005-2). Allows transparency and JPXDecode. Still no embedded files unless
    /// they are themselves PDF/A.
    /// </summary>
    PdfA2B,

    /// <summary>
    /// PDF/A-3b (ISO 19005-3). As PDF/A-2b, and the only profile that may carry an attachment of
    /// any kind — which is what hybrid e-invoices such as ZUGFeRD and Factur-X are built on.
    /// </summary>
    PdfA3B,

    /// <summary>
    /// PDF/A-1a (ISO 19005-1). PDF/A-1b plus a tagged structure tree — the oldest and strictest
    /// archival profile, made available to a document PinataLayout or the caller has tagged.
    /// </summary>
    /// <remarks>
    /// Appended rather than inserted next to <see cref="PdfA1B"/>, along with the two after it: the
    /// compiler inlines an enum constant at the call site, so renumbering an existing member would
    /// silently redirect a caller compiled against the old assembly into a different profile.
    /// </remarks>
    PdfA1A,

    /// <summary>
    /// PDF/A-2a (ISO 19005-2). PDF/A-2b plus a tagged structure tree — archival and accessible
    /// under one claim.
    /// </summary>
    PdfA2A,

    /// <summary>
    /// PDF/A-3a (ISO 19005-3). PDF/A-3b plus a tagged structure tree, so a hybrid e-invoice can be
    /// accessible as well as carry its attachment.
    /// </summary>
    PdfA3A
}
