using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   A self-signed certificate to sign test documents with.
/// </summary>
/// <remarks>
///   Generated rather than checked in, because a checked-in certificate expires and then the build
///   fails one morning for a reason nobody changed. Nothing here is trusted by anything — the point
///   of the signing tests is that a signature is intact and covers what it claims to, which is a
///   question about bytes, not about who is believed.
/// </remarks>
public static class SigningCertificates
{
    private static readonly Lazy<X509Certificate2> Generated = new(() => Create("CN=PdfPinata Test Signer"));

    /// <summary>
    ///   The certificate the signing tests share. Generating an RSA key costs enough to be worth
    ///   doing once.
    /// </summary>
    public static X509Certificate2 Default => Generated.Value;

    private static X509Certificate2 Create(string subject) => Create(subject, timestampAuthority: false);

    /// <summary>
    ///   A certificate fit to sign an RFC 3161 timestamp token: it carries the critical
    ///   <c>id-kp-timeStamping</c> extended key usage a genuine time-stamping authority's certificate
    ///   has to, and <c>System.Security.Cryptography.Pkcs.Rfc3161TimestampToken.TryDecode</c> checks
    ///   for exactly this before it will read a token back — a certificate without it decodes the
    ///   token's structure but is then refused, which is indistinguishable from having no timestamp
    ///   at all unless you already know to look for the extension.
    /// </summary>
    public static X509Certificate2 CreateTimestampAuthority(string subject) => Create(subject, timestampAuthority: true);

    private static X509Certificate2 Create(string subject, bool timestampAuthority)
    {
        using var key = RSA.Create(2048);

        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));

        if (timestampAuthority)
        {
            // id-kp-timeStamping. Must be the only key purpose and the extension must be critical,
            // or a reader is required to treat the certificate as unfit for time-stamping.
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection { new Oid("1.3.6.1.5.5.7.3.8") }, critical: true));
        }

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        // Round-tripped through PKCS#12 on purpose. A certificate straight out of CreateSelfSigned
        // carries an ephemeral key on Windows, and CMS signing goes looking for that key in a store
        // it was never put in — "Keyset does not exist", from a certificate that plainly has one.
        const string password = "pdfsharpcore";
        var exported = certificate.Export(X509ContentType.Pfx, password);

#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(exported, password, X509KeyStorageFlags.Exportable);
#else
        return new X509Certificate2(exported, password, X509KeyStorageFlags.Exportable);
#endif
    }
}
