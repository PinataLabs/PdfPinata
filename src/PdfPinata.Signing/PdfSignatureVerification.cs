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
/// whether it had been revoked at the time, and whether the time-stamping authority can be believed
/// are separate matters this does not touch — see the note on <see cref="PdfSignatureVerifier"/>.
/// </para>
/// <para>
/// <b>A timestamp is answered apart from both.</b> <see cref="IsTimestampIntact"/> is about the
/// token, not about the signature: a damaged token leaves the signature exactly as valid as it was,
/// and only takes away the evidence of when it was made.
/// </para>
/// </remarks>
public sealed class PdfSignatureVerification
{
    internal PdfSignatureVerification(PdfSignatureInfo signature, bool isIntact, bool coversWholeDocument,
        X509Certificate2 signerCertificate, string problem, DateTimeOffset? timestamp = null,
        bool? isTimestampIntact = null)
    {
        Signature = signature;
        IsIntact = isIntact;
        CoversWholeDocument = coversWholeDocument;
        SignerCertificate = signerCertificate;
        Problem = problem;
        Timestamp = timestamp;
        IsTimestampIntact = isTimestampIntact;
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
    /// no intact one — an ordinary PAdES B-B signature, or one whose token is not
    /// <see cref="IsTimestampIntact">intact</see>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A time is reported only from a token that is intact, so an auditor can judge when the signature
    /// was made rather than trust the producer's own clock in <see cref="PdfSignatureInfo.SigningTime"/>
    /// — and cannot be handed a time from a token that was damaged or that covers some other signature.
    /// Whether the signature carried a token at all is <see cref="IsTimestampIntact"/>, which is not
    /// null whenever there was one.
    /// </para>
    /// <para>
    /// Intact is not trusted: whether the authority that signed the token deserves belief is exactly
    /// the kind of trust decision <see cref="PdfSignatureVerifier"/> does not make, for the same reason
    /// it makes none for the signature itself.
    /// </para>
    /// </remarks>
    public DateTimeOffset? Timestamp { get; }

    /// <summary>
    /// Whether this signature carries an intact timestamp, and so a <see cref="Timestamp"/>.
    /// </summary>
    public bool HasTimestamp => Timestamp.HasValue;

    /// <summary>
    /// Whether the signature's timestamp token is intact, or null if it carries none, or is too
    /// malformed to decode at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// True when the token's own signature verifies over its content, with the certificate its
    /// signing-certificate attribute names, and its message imprint is the hash of this signature's
    /// value — so the token was issued for this signature and has not changed since. False when
    /// either check fails, when that certificate is not to be found, or when the signature-timestamp
    /// attribute holds something that is not a timestamp token at all.
    /// </para>
    /// <para>
    /// This is reported apart from <see cref="IsIntact"/> and <see cref="IsValid"/> because the token
    /// is an unsigned attribute, outside what the signature covers: a broken one says nothing against
    /// the signature, only that it cannot say when the signature was made.
    /// </para>
    /// </remarks>
    public bool? IsTimestampIntact { get; }
}
