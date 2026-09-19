using System;
using System.Collections.Generic;

namespace PdfPinata.Signing;

/// <summary>
/// The raw revocation evidence found for one certificate: DER-encoded OCSP responses and CRLs.
/// </summary>
/// <remarks>
/// Either list may be empty — a certificate can have neither, one, or both kinds of evidence
/// available — but never null; <see cref="PdfSignatureValidationData"/> is what turns this, once
/// gathered for every certificate a document's signatures embed, into the document's security store.
/// </remarks>
public sealed class RevocationData
{
    /// <summary>No evidence at all, for a certificate none could be found for.</summary>
    public static readonly RevocationData None = new(null, null);

    /// <summary>Bundles what was found for one certificate.</summary>
    public RevocationData(IReadOnlyList<byte[]> ocspResponses, IReadOnlyList<byte[]> crls)
    {
        OcspResponses = ocspResponses ?? Array.Empty<byte[]>();
        Crls = crls ?? Array.Empty<byte[]>();
    }

    /// <summary>DER-encoded <c>OCSPResponse</c> structures (RFC 6960) vouching for this certificate.</summary>
    public IReadOnlyList<byte[]> OcspResponses { get; }

    /// <summary>DER-encoded <c>CertificateList</c> structures (RFC 5280) covering this certificate.</summary>
    public IReadOnlyList<byte[]> Crls { get; }
}
