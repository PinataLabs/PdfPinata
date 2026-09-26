using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Signing;

/// <summary>
/// Gathers validation data for every signature already in a document and writes it into the
/// document's security store.
/// </summary>
/// <remarks>
/// The core's <see cref="PdfValidationData"/> knows how to store bytes and nothing about what they
/// mean; this is what supplies them — decoding each signature's embedded certificates, which is
/// cryptography, and asking <see cref="IRevocationDataProvider"/> for evidence about each one. A
/// document signed by someone else works exactly the same way: nothing here needs the private key
/// that made the signature, only the certificates it already carries. A timestamped signature
/// carries two chains, the signer's and, inside its timestamp token, the time-stamping authority's;
/// both are gathered, because the timestamp is the part of a B-T signature meant to be checked
/// after the signing certificate has expired.
/// </remarks>
public static class PdfSignatureValidationData
{
    /// <summary>
    /// Adds validation data for every signature <see cref="PdfSignatures.InDocument"/> finds, and
    /// appends the revision to <paramref name="output"/>.
    /// </summary>
    public static void Add(PdfDocument document, Stream output, IRevocationDataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(provider);

        var certificates = new List<byte[]>();
        var ocspResponses = new List<byte[]>();
        var crls = new List<byte[]>();
        var seen = new HashSet<string>();

        foreach (var signature in PdfSignatures.InDocument(document))
        {
            var signed = Decoded(signature);

            Gather(signed.Certificates, signed.Certificates);

            // An authority often answers certReq with its own certificate and nothing above it,
            // so its issuer is looked for among the signer's certificates too: one CA commonly
            // issues both, and without its issuer no OCSP request for it can be built.
            foreach (var tokenCertificates in TimestampCertificatesOf(signed))
            {
                var candidates = new X509Certificate2Collection(tokenCertificates);
                candidates.AddRange(signed.Certificates);
                Gather(tokenCertificates, candidates);
            }
        }

        var entry = new PdfValidationDataEntry(certificates, ocspResponses, crls);
        PdfValidationData.Add(document, output, entry);

        void Gather(X509Certificate2Collection found, X509Certificate2Collection chain)
        {
            foreach (var certificate in found)
            {
                if (!seen.Add(certificate.Thumbprint))
                    continue;

                certificates.Add(certificate.RawData);

                var evidence = provider.GetRevocationData(certificate, chain);
                if (evidence == null)
                    continue;

                ocspResponses.AddRange(evidence.OcspResponses);
                crls.AddRange(evidence.Crls);
            }
        }
    }

    /// <summary>
    /// The certificates a signature embeds — the signer's own and, with
    /// <see cref="Pkcs7Signer"/>'s default <see cref="System.Security.Cryptography.X509Certificates.X509IncludeOption.WholeChain"/>,
    /// everything above it. Decoded without checking the signature itself: gathering evidence about a
    /// certificate needs to know which certificate it is, not whether it signed anything correctly.
    /// </summary>
    private static SignedCms Decoded(PdfSignatureInfo signature)
    {
        var encoded = CmsEncoding.Trimmed(signature.Contents);

        var signed = new SignedCms();
        signed.Decode(encoded);

        return signed;
    }

    /// <summary>
    /// The certificates each signature-timestamp token embeds — the authority's own, which
    /// <see cref="Rfc3161TimestampProvider"/> asks for, and whatever chain the authority adds.
    /// Each token's certificates are answered as a collection of their own.
    /// </summary>
    /// <remarks>
    /// A token that cannot be decoded contributes nothing rather than failing the whole call — the
    /// same view <see cref="PdfSignatureVerifier"/> takes of one: evidence for the signature itself
    /// is still worth storing when its timestamp is malformed.
    /// </remarks>
    private static IEnumerable<X509Certificate2Collection> TimestampCertificatesOf(SignedCms signed)
    {
        foreach (SignerInfo signerInfo in signed.SignerInfos)
        {
            foreach (var attribute in signerInfo.UnsignedAttributes)
            {
                if (attribute.Oid.Value != CmsEncoding.SignatureTimeStampTokenOid)
                    continue;

                foreach (var value in attribute.Values)
                {
                    var token = new SignedCms();
                    try
                    {
                        token.Decode(value.RawData);
                    }
                    catch (CryptographicException)
                    {
                        continue;
                    }

                    yield return token.Certificates;
                }
            }
        }
    }
}
