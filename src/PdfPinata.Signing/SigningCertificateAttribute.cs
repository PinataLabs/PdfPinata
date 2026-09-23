using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PdfPinata.Signing;

/// <summary>
/// Builds the <c>signing-certificate-v2</c> signed attribute (RFC 5035): a hash of a certificate,
/// binding it into what is signed.
/// </summary>
/// <remarks>
/// Two signers in this package need it. <see cref="Pkcs7Signer"/> adds it for the certificate that
/// signs the document, which is the whole difference between a plain PKCS#7 signature and a CAdES
/// one. <see cref="LocalTimestampAuthority"/> adds it for the certificate that signs the timestamp
/// token, because <see cref="System.Security.Cryptography.Pkcs.Rfc3161TimestampToken.TryDecode"/>
/// refuses to decode a token whose <c>SignerInfo</c> does not carry one — a real time-stamping
/// authority's response always does, and a token minted without it is not a token .NET's own reader
/// recognises as one.
/// </remarks>
internal static class SigningCertificateAttribute
{
    /// <summary>id-aa-signingCertificateV2, RFC 5035.</summary>
    private const string SigningCertificateV2Oid = "1.2.840.113549.1.9.16.2.47";

    public static AsnEncodedData Build(X509Certificate2 certificate, HashAlgorithmName hashAlgorithm)
    {
        var hash = certificate.GetCertHash(hashAlgorithm);

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())        // SigningCertificateV2
        using (writer.PushSequence())        // certs SEQUENCE OF ESSCertIDv2
        using (writer.PushSequence())        // ESSCertIDv2
        {
            // ESSCertIDv2 defaults its hash algorithm to SHA-256, so a DER encoder omits the field
            // when that is what was used; a verifier reading it back supplies SHA-256 itself.
            if (hashAlgorithm != HashAlgorithmName.SHA256)
            {
                using (writer.PushSequence())
                    writer.WriteObjectIdentifier(HashAlgorithmOids.Of(hashAlgorithm));
            }

            writer.WriteOctetString(hash);
        }

        return new AsnEncodedData(new Oid(SigningCertificateV2Oid), writer.Encode());
    }
}
