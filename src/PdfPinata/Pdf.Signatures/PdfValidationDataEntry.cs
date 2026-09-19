using System;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// Raw bytes to embed in a document's security store: certificates and revocation responses supplying
/// the evidence a signature's chain can still be checked after the certificate it was made with has
/// expired.
/// </summary>
/// <remarks>
/// What these bytes mean is cryptography, and gathering them is <c>PdfPinata.Signing</c>'s job,
/// through <c>IRevocationDataProvider</c> — this only knows how to store them, exactly as
/// <see cref="PdfSignatures"/> only knows how to read a signature dictionary back, never what the
/// signature inside it proves.
/// </remarks>
public sealed class PdfValidationDataEntry
{
    /// <summary>Bundles the raw bytes <see cref="PdfValidationData.Add"/> writes into the store.</summary>
    public PdfValidationDataEntry(IReadOnlyList<byte[]> certificates, IReadOnlyList<byte[]> ocspResponses,
        IReadOnlyList<byte[]> crls)
    {
        Certificates = certificates ?? Array.Empty<byte[]>();
        OcspResponses = ocspResponses ?? Array.Empty<byte[]>();
        Crls = crls ?? Array.Empty<byte[]>();
    }

    /// <summary>DER-encoded certificates: the chain a signature's own embedded certificates name.</summary>
    public IReadOnlyList<byte[]> Certificates { get; }

    /// <summary>DER-encoded OCSP responses (RFC 6960).</summary>
    public IReadOnlyList<byte[]> OcspResponses { get; }

    /// <summary>DER-encoded certificate revocation lists (RFC 5280).</summary>
    public IReadOnlyList<byte[]> Crls { get; }
}
