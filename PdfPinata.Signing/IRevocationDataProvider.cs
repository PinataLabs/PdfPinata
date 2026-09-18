using System.Security.Cryptography.X509Certificates;

namespace PdfPinata.Signing;

/// <summary>
/// Supplies the revocation evidence — OCSP responses and CRLs — that lets a certificate's chain still
/// be checked after the certificate has expired.
/// </summary>
/// <remarks>
/// <para>
/// Shaped like <see cref="ITimestampProvider"/> deliberately: both are network-facing capabilities a
/// signer either has or does not, and learning the shape once covers both. The bytes this answers are
/// opaque to the core — <c>PdfPinata.Pdf.Signatures.PdfValidationData</c> only knows how to store
/// them in the document's security store, never what they mean, exactly as it never decodes the
/// signature itself.
/// </para>
/// <para>
/// Building an OCSP request and posting it, or fetching a CRL, is left to an implementation of this
/// rather than shipped as a batteries-included HTTP client here — the same division
/// <see cref="Pdf.Signatures.IPdfSigner"/> draws between the PDF machinery this package's core cares
/// about and the certificate handling a caller's environment already has opinions about.
/// </para>
/// </remarks>
public interface IRevocationDataProvider
{
    /// <summary>
    /// Answers whatever revocation evidence is available for <paramref name="certificate"/>.
    /// </summary>
    /// <param name="certificate">The certificate to find evidence for.</param>
    /// <param name="chain">
    /// The certificates embedded alongside it in the signature — typically its issuer and the roots
    /// above it — so an implementation can find the issuer's key to build an OCSP request with.
    /// </param>
    RevocationData GetRevocationData(X509Certificate2 certificate, X509Certificate2Collection chain);
}
