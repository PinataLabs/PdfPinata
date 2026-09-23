using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PdfPinata.Signing;

/// <summary>
/// Mints an RFC 3161 timestamp token itself, from a certificate handed to it, without a network call.
/// </summary>
/// <remarks>
/// <para>
/// This is what makes the timestamp tests hermetic: a test builds a self-signed "authority"
/// certificate the same way it builds a signing one, hands it here, and gets back a token in the same
/// wire format <see cref="Rfc3161TimestampProvider"/> would have fetched over the network — a
/// <c>TimeStampToken</c>, which is a CMS <c>SignedData</c> wrapping a <c>TSTInfo</c>. Nothing here
/// checks that the certificate is a real time-stamping authority; it is a test double, not a client
/// for one.
/// </para>
/// </remarks>
public sealed class LocalTimestampAuthority : ITimestampProvider
{
    /// <summary>id-ct-TSTInfo, RFC 3161.</summary>
    private const string TstInfoOid = "1.2.840.113549.1.9.16.1.4";

    private readonly X509Certificate2 _certificate;

    /// <summary>
    /// Mints tokens signed with the given certificate, which must have a usable private key.
    /// </summary>
    public LocalTimestampAuthority(X509Certificate2 certificate)
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));

        if (!certificate.HasPrivateKey)
            throw new ArgumentException(
                @"The certificate has no private key, so it cannot sign a timestamp token.",
                nameof(certificate));
    }

    /// <inheritdoc/>
    public byte[] GetTimestamp(byte[] messageImprint, HashAlgorithmName hashAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(messageImprint);

        var tstInfo = BuildTstInfo(messageImprint, hashAlgorithm);

        var signed = new SignedCms(new ContentInfo(new Oid(TstInfoOid), tstInfo), detached: false);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
        {
            DigestAlgorithm = new Oid(HashAlgorithmOids.Of(hashAlgorithm)),
            IncludeOption = X509IncludeOption.WholeChain
        };

        // Rfc3161TimestampToken.TryDecode refuses a token whose SignerInfo carries no
        // signing-certificate(-v2) attribute — the same binding PAdES asks of the document
        // signature itself, and a real TSA's response always carries it.
        signer.SignedAttributes.Add(SigningCertificateAttribute.Build(_certificate, hashAlgorithm));

        signed.ComputeSignature(signer);
        return signed.Encode();
    }

    /// <summary>
    /// The <c>TSTInfo</c> a genuine time-stamping authority would answer with, minus everything
    /// optional: no accuracy, no nonce echo, no named authority. Built through
    /// <see cref="Rfc3161TimestampTokenInfo"/> rather than by hand, so its encoding is exactly what
    /// <see cref="Rfc3161TimestampToken.TryDecode"/> on the reading side already knows how to read.
    /// </summary>
    private static byte[] BuildTstInfo(byte[] messageImprint, HashAlgorithmName hashAlgorithm)
    {
        // An arbitrary private policy OID: nothing here claims conformance to a real one.
        var policyId = new Oid("1.2.3.4.5.6.7.8.9");
        var hashAlgorithmId = new Oid(HashAlgorithmOids.Of(hashAlgorithm));

        var serialNumber = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(serialNumber);

        var tokenInfo = new Rfc3161TimestampTokenInfo(
            policyId, hashAlgorithmId, messageImprint, serialNumber, DateTimeOffset.UtcNow);

        return tokenInfo.Encode();
    }
}
