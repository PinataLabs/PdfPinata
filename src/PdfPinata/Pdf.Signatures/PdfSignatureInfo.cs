using System;

namespace PdfPinata.Pdf.Signatures;

/// <summary>
/// A signature found in a document: what it says about itself, and which bytes it covers.
/// </summary>
/// <remarks>
/// Everything here is read out of the file and none of it has been checked. The name, the reason and
/// the time are text the producer wrote and are not evidence of anything on their own; establishing
/// that the signature is intact, and that it covers what it claims to, needs cryptography and lives
/// in <c>PdfPinata.Signing</c>.
/// </remarks>
public sealed class PdfSignatureInfo
{
    internal PdfSignatureInfo(string fieldName, string subFilter, byte[] contents, int[] byteRange,
        string signerName, string reason, string location, string contactInfo, DateTime? signingTime,
        int certificationLevel)
    {
        FieldName = fieldName;
        SubFilter = subFilter;
        Contents = contents;
        ByteRange = byteRange;
        SignerName = signerName;
        Reason = reason;
        Location = location;
        ContactInfo = contactInfo;
        SigningTime = signingTime;
        CertificationLevel = certificationLevel;
    }

    /// <summary>
    /// The name of the form field holding the signature.
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// What kind of signature <see cref="Contents"/> holds — <c>/ETSI.CAdES.detached</c> and
    /// <c>/adbe.pkcs7.detached</c> being the two that matter.
    /// </summary>
    public string SubFilter { get; }

    /// <summary>
    /// The signature itself, as it was written into <c>/Contents</c>.
    /// </summary>
    /// <remarks>
    /// Including whatever zero padding follows it: the space is reserved before the length is known,
    /// so the encoded signature ends where its own length says it does and the rest is filler.
    /// </remarks>
    public byte[] Contents { get; }

    /// <summary>
    /// The offsets and lengths of the two spans the signature covers, as <c>/ByteRange</c> gives
    /// them: start, length, start, length.
    /// </summary>
    public int[] ByteRange { get; }

    /// <summary>The name the producer recorded for the signer, if any.</summary>
    public string SignerName { get; }

    /// <summary>Why the document was signed, if the producer said.</summary>
    public string Reason { get; }

    /// <summary>Where it was signed, if the producer said.</summary>
    public string Location { get; }

    /// <summary>How to reach the signer, if the producer said.</summary>
    public string ContactInfo { get; }

    /// <summary>
    /// When the producer's own clock said the document was signed. Not evidence: only a timestamp
    /// from a time-stamping authority is, and that is not implemented.
    /// </summary>
    public DateTime? SigningTime { get; }

    /// <summary>
    /// The <c>/DocMDP</c> permission level if this is a certifying signature, or zero if it is an
    /// ordinary approval one. See <see cref="PdfCertificationLevel"/>.
    /// </summary>
    public int CertificationLevel { get; }

    /// <summary>
    /// Whether the byte range leaves nothing out of the file but the signature itself.
    /// </summary>
    /// <param name="fileLength">The length of the whole file the signature was read from.</param>
    /// <remarks>
    /// A signature is entitled to cover only part of a file, and a signature covering only part of a
    /// file is how a document gets altered without the alteration showing. So this question has to
    /// be asked separately from whether the signature verifies — a signature over the first page of
    /// a five page document verifies perfectly.
    /// </remarks>
    public bool CoversWholeDocument(long fileLength) =>
        ByteRange is { Length: 4 }
        && ByteRange[0] == 0
        && ByteRange[2] >= ByteRange[0] + ByteRange[1]
        && ByteRange[2] + ByteRange[3] == fileLength;
}
