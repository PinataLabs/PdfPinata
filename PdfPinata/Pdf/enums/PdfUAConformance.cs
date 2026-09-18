namespace PdfPinata.Pdf;

/// <summary>
/// The accessibility profile a document claims to conform to.
/// </summary>
/// <remarks>
/// As with <see cref="PdfAConformance"/>, setting this does more than label the file: the writer
/// refuses to save a document that breaks a rule it can check, naming the rule. An accessibility
/// claim is read by a procurement officer rather than by a reader, and one that turns out to be
/// false is worse than none.
/// <para>
/// What can be checked before the bytes are written is not the whole standard, and
/// <see cref="Structure.PdfUaValidator"/> says which rules it holds a document to. A successful save
/// is not a validator's verdict; veraPDF is.
/// </para>
/// </remarks>
public enum PdfUAConformance
{
    /// <summary>
    /// No claim. A document may still be tagged — tagging is what makes it useful, and the claim is
    /// a separate promise about the whole file.
    /// </summary>
    None,

    /// <summary>
    /// PDF/UA-1 (ISO 14289-1). What every tool validates against today.
    /// </summary>
    PdfUA1,

    /// <summary>
    /// PDF/UA-2 (ISO 14289-2:2024). The current accessibility standard. Held to the same rules
    /// <see cref="Structure.PdfUaValidator"/> already checks, plus four this claim adds of its own —
    /// the <c>pdfuaid:rev</c> identifier, the PDF 2.0 structure namespace, retagging <c>/Note</c> as
    /// <c>/FENote</c>, and a <c>/ListNumbering</c> attribute on every list — appended after
    /// <see cref="PdfUA1"/> rather than inserted, for the same reason <see cref="PdfAConformance"/>
    /// appends its own new members.
    /// </summary>
    /// <remarks>
    /// <b>veraPDF does not yet validate a document claiming this.</b> ISO 14289-2 clause 8.8
    /// requires every destination internal to the document — outline items, links, an OpenAction —
    /// to be a "structure destination" through the <c>/SD</c> entry ISO 32000-2:2020 introduced, and
    /// this library still writes the page-relative kind PDF 1.7 always has. <c>/SD</c> is itself
    /// unresolved in the published standard — pdf-association/pdf-issues#162 is an open, unfixed
    /// errata report that ISO 32000-2:2020 never defines what it contains or how a reader uses it —
    /// so guessing at the mechanism risks a destination that neither validates nor navigates
    /// correctly, which is worse than the explicit one this keeps writing. Until that is resolved,
    /// this is a claim the library cannot yet stand behind end to end, and no document claiming it
    /// is in the gated conformance corpus — see <c>ConformanceCorpus.Corpus.Documents</c>.
    /// </remarks>
    PdfUA2,
}
