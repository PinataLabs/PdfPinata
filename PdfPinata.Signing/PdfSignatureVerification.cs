using System;
using System.Security.Cryptography.X509Certificates;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Signing;

/// <summary>
/// What checking one signature found.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two questions, and both have to be yes.</b> <see cref="IsIntact"/> says the signature verifies
/// over the bytes it covers; <see cref="CoversWholeDocument"/> says those bytes are the whole file
/// but for the signature itself. A signature over the first page of a five page document is
/// perfectly intact, and reporting only that would be reporting the document as sound when a reader
/// would not.
/// </para>
/// <para>
/// <b>Neither question is about trust.</b> Whether the certificate chains to a root worth believing,
/// whether it had been revoked at the time, and whether the time itself can be believed are separate
/// matters this does not touch — see the note on <see cref="PdfSignatureVerifier"/>.
/// </para>
/// </remarks>
public sealed class PdfSignatureVerification
{
    internal PdfSignatureVerification(PdfSignatureInfo signature, bool isIntact, bool coversWholeDocument,
        X509Certificate2 signerCertificate, string problem, DateTimeOffset? timestamp = null)
    {
        Signature = signature;
        IsIntact = isIntact;
        CoversWholeDocument = coversWholeDocument;
        SignerCertificate = signerCertificate;
        Problem = problem;
        Timestamp = timestamp;
    }

    /// <summary>
    /// What the document says about the signature, unchecked.
    /// </summary>
    public PdfSignatureInfo Signature { get; }

    /// <summary>
    /// Whether the signature verifies over the bytes it covers.
    /// </summary>
    public bool IsIntact { get; }

    /// <summary>
    /// Whether the bytes it covers are everything in the file but the signature itself.
    /// </summary>
    public bool CoversWholeDocument { get; }

    /// <summary>
    /// The certificate the signature was made with, as embedded in the signature.
    /// </summary>
    public X509Certificate2 SignerCertificate { get; }

    /// <summary>
    /// What went wrong, or null if nothing did.
    /// </summary>
    public string Problem { get; }

    /// <summary>
    /// Whether the signature is intact and covers the whole document.
    /// </summary>
    public bool IsValid => IsIntact && CoversWholeDocument;

    /// <summary>
    /// When a time-stamping authority's token says this signature was made, or null if it carries
    /// none — an ordinary PAdES B-B signature, or one whose token this could not read.
    /// </summary>
    /// <remarks>
    /// This is a structural read of the token, not a validation of it: whether the token's own
    /// signature is trustworthy is exactly the kind of trust decision <see cref="PdfSignatureVerifier"/>
    /// does not make, for the same reason it makes none for the signature itself. What this answers is
    /// what the token <em>says</em>, so an auditor can judge when the signature was made rather than
    /// trust the producer's own clock in <see cref="PdfSignatureInfo.SigningTime"/>.
    /// </remarks>
    public DateTimeOffset? Timestamp { get; }

    /// <summary>
    /// Whether this signature carries a timestamp at all.
    /// </summary>
    public bool HasTimestamp => Timestamp.HasValue;
}
