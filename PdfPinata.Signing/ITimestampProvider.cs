using System.Security.Cryptography;

namespace PdfPinata.Signing;

/// <summary>
/// Fetches a trusted timestamp token for a signature, so the moment it was made is evidence rather
/// than the producer's own clock.
/// </summary>
/// <remarks>
/// <para>
/// A named seam rather than a bare delegate, because this capability has a sibling —
/// <see cref="IRevocationDataProvider"/> — and both are learned once by learning the shape. The token
/// this returns travels inside the signed message the existing signing seam already hands back: it is
/// added as an unsigned attribute of the CMS <c>SignerInfo</c>, so <see cref="Pdf.Signatures.IPdfSigner"/>
/// itself needs no change and no third-party implementation of it has to learn a new concept.
/// </para>
/// <para>
/// A failure here has to fail the signing rather than be swallowed: a signature that silently falls
/// back to B-B while the caller believes it is B-T is worse than no signature, because it is
/// discovered by a verifier and not by its author.
/// </para>
/// </remarks>
public interface ITimestampProvider
{
    /// <summary>
    /// Answers a timestamp token — a complete RFC 3161 <c>TimeStampToken</c>, DER-encoded — covering
    /// the given message imprint.
    /// </summary>
    /// <param name="messageImprint">
    /// The hash of the value being timestamped — the signature itself, for a signature timestamp.
    /// </param>
    /// <param name="hashAlgorithm">Which algorithm <paramref name="messageImprint"/> was hashed with.</param>
    byte[] GetTimestamp(byte[] messageImprint, HashAlgorithmName hashAlgorithm);
}
