using System;
using System.Collections.Generic;
using System.IO;
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
/// that made the signature, only the certificates it already carries.
/// </remarks>
public static class PdfSignatureValidationData
{
    /// <summary>
    /// Adds validation data for every signature <see cref="PdfSignatures.InDocument"/> finds, and
    /// appends the revision to <paramref name="output"/>.
    /// </summary>
    public static void Add(PdfDocument document, Stream output, IRevocationDataProvider provider)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        var certificates = new List<byte[]>();
        var ocspResponses = new List<byte[]>();
        var crls = new List<byte[]>();
        var seen = new HashSet<string>();

        foreach (var signature in PdfSignatures.InDocument(document))
        {
            var chain = CertificatesOf(signature);

            foreach (var certificate in chain)
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

        var entry = new PdfValidationDataEntry(certificates, ocspResponses, crls);
        PdfValidationData.Add(document, output, entry);
    }

    /// <summary>
    /// The certificates a signature embeds — the signer's own and, with
    /// <see cref="Pkcs7Signer"/>'s default <see cref="System.Security.Cryptography.X509Certificates.X509IncludeOption.WholeChain"/>,
    /// everything above it. Decoded without checking the signature itself: gathering evidence about a
    /// certificate needs to know which certificate it is, not whether it signed anything correctly.
    /// </summary>
    static X509Certificate2Collection CertificatesOf(PdfSignatureInfo signature)
    {
        var encoded = CmsEncoding.Trimmed(signature.Contents);

        var signed = new SignedCms();
        signed.Decode(encoded);

        return signed.Certificates;
    }
}
