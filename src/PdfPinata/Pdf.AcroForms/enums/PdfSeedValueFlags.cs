using System;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// Which entries of a signature seed value dictionary are requirements rather than suggestions -
/// ISO 32000-1 Table 234, <c>/Ff</c>. A bit that is clear leaves the signer free to use another
/// value.
/// </summary>
[Flags]
public enum PdfSeedValueFlags
{
    /// <summary>Every entry is a suggestion.</summary>
    None = 0,

    /// <summary><c>/Filter</c> is required.</summary>
    Filter = 1 << 0,

    /// <summary>One of <c>/SubFilter</c> is required.</summary>
    SubFilter = 1 << 1,

    /// <summary><c>/V</c> is required.</summary>
    Version = 1 << 2,

    /// <summary>One of <c>/Reasons</c> is required.</summary>
    Reasons = 1 << 3,

    /// <summary>One of <c>/LegalAttestation</c> is required.</summary>
    LegalAttestation = 1 << 4,

    /// <summary><c>/AddRevInfo</c> is required.</summary>
    AddRevInfo = 1 << 5,

    /// <summary>One of <c>/DigestMethod</c> is required.</summary>
    DigestMethod = 1 << 6
}

/// <summary>
/// Which entries of a certificate seed value dictionary are requirements - ISO 32000-1 Table 235,
/// <c>/Ff</c>.
/// </summary>
[Flags]
public enum PdfCertificateSeedValueFlags
{
    /// <summary>Every entry is a suggestion.</summary>
    None = 0,

    /// <summary>The certificate must be one of <c>/Subject</c>.</summary>
    Subject = 1 << 0,

    /// <summary>The certificate must be issued by one of <c>/Issuer</c>.</summary>
    Issuer = 1 << 1,

    /// <summary>The certificate must carry one of the policies in <c>/OID</c>.</summary>
    Oid = 1 << 2,

    /// <summary>The certificate's subject must match one of <c>/SubjectDN</c>.</summary>
    SubjectDN = 1 << 3,

    /// <summary>The certificate's key usage must match <c>/KeyUsage</c>.</summary>
    KeyUsage = 1 << 5,

    /// <summary><c>/URL</c> is required.</summary>
    Url = 1 << 6
}
