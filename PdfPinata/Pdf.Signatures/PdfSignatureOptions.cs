using System;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// What to record about a signature beyond the signature itself, and where to show it.
/// </summary>
public sealed class PdfSignatureOptions
{
    /// <summary>
    /// The name of the form field the signature is held in. Must be unique within the document — a
    /// second signature added under a name already in use replaces the first in the eyes of a
    /// reader, so a document signed twice needs two names.
    /// </summary>
    public string FieldName { get; set; } = "Signature1";

    /// <summary>
    /// The name of the person or authority signing, as it should be displayed.
    /// </summary>
    /// <remarks>
    /// Left unset, readers show the common name out of the signing certificate, which is the more
    /// trustworthy of the two: this one is text the producer chose and nothing verifies it.
    /// </remarks>
    public string SignerName { get; set; }

    /// <summary>
    /// Why the document was signed, such as "I am the author of this document".
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Where the signing took place.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// How to reach the signer to verify the signature.
    /// </summary>
    public string ContactInfo { get; set; }

    /// <summary>
    /// The time written to the signature dictionary's <c>/M</c>. Defaults to now.
    /// </summary>
    /// <remarks>
    /// This is the producer's own clock and is not evidence of anything — a reader will say so.
    /// Making the time of signing provable needs a timestamp token from a time-stamping authority,
    /// which is PAdES B-T and is not implemented; see <c>docs/specs/digital-signatures.md</c>.
    /// </remarks>
    public DateTime? SigningTime { get; set; }

    /// <summary>
    /// The page the signature field belongs to, counted from zero.
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// Where on the page the signature is shown, in the same coordinates
    /// <see cref="XGraphics.FromPdfPage(PdfPage)"/> draws in — the origin at the top left of the
    /// page and Y increasing downwards.
    /// </summary>
    /// <remarks>
    /// Left unset the signature is invisible: it is still a field on the page and still covers the
    /// whole document, it simply has nothing to show. Most machine-applied signatures are invisible,
    /// and an invisible one is never mistaken for a picture of a signature.
    /// </remarks>
    public XRect? Rectangle { get; set; }

    /// <summary>
    /// Draws what the signature looks like. Called with a surface the size of
    /// <see cref="Rectangle"/> whose own origin is its top left corner.
    /// </summary>
    /// <remarks>
    /// Only consulted when <see cref="Rectangle"/> is set. What is drawn here is decoration: it is
    /// not what a reader validates, and drawing the word "signed" does not sign anything.
    /// </remarks>
    public Action<XGraphics, XRect> DrawAppearance { get; set; }

    /// <summary>
    /// Whether this signature certifies the document, and what it then still permits.
    /// </summary>
    public PdfCertificationLevel Certification { get; set; } = PdfCertificationLevel.NotCertified;
}
