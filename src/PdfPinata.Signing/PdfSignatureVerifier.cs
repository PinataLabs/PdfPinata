using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Signing;

/// <summary>
/// Checks that the signatures on a document are intact and cover it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is integrity checking, not validation.</b> It answers whether each signature verifies
/// over the bytes it covers and whether those bytes are the whole file. It does not build a
/// certificate chain, consult a trust store or check revocation — all of which a reader showing a
/// green tick has done and none of which is implemented here. A signature this reports as valid may
/// still have been made with a certificate nobody should trust.
/// </para>
/// <para>
/// <b>A signature-timestamp is checked the same way, and no further.</b> When a signature carries
/// one, <see cref="PdfSignatureVerification.IsTimestampIntact"/> says whether the token's own
/// signature verifies over its content, with the certificate it names, and whether its
/// message imprint is the hash of this signature's value — so that a damaged token, or one lifted
/// from another signature, is not reported as a time this signature was made. Whether the
/// time-stamping authority deserves belief is the same trust question as above, and is not asked.
/// </para>
/// <para>
/// It is nevertheless the check that catches what actually goes wrong: a document edited after
/// signing, a signature covering only part of the file, or a producer that got its byte range wrong.
/// </para>
/// </remarks>
public static class PdfSignatureVerifier
{
    /// <summary>
    /// Checks every signature in a signed file.
    /// </summary>
    public static IReadOnlyList<PdfSignatureVerification> Verify(byte[] document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var stream = new MemoryStream(document, false);
        var opened = PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);

        var results = new List<PdfSignatureVerification>();
        foreach (var signature in PdfSignatures.InDocument(opened))
            results.Add(Check(signature, document));

        return results;
    }

    /// <summary>
    /// Checks every signature in a signed file.
    /// </summary>
    public static IReadOnlyList<PdfSignatureVerification> Verify(Stream document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var buffer = new MemoryStream();
        document.CopyTo(buffer);
        return Verify(buffer.ToArray());
    }

    private static PdfSignatureVerification Check(PdfSignatureInfo signature, byte[] document)
    {
        var covers = signature.CoversWholeDocument(document.Length);

        if (signature.ByteRange == null || signature.ByteRange.Length != 4)
            return new PdfSignatureVerification(signature, false, covers, null,
                "The signature's /ByteRange is not the four numbers it has to be.");

        byte[] covered;
        try
        {
            covered = BytesCovered(signature.ByteRange, document);
        }
        catch (ArgumentException problem)
        {
            return new PdfSignatureVerification(signature, false, covers, null,
                "The signature's /ByteRange does not lie inside the file: " + problem.Message);
        }

        SignedCms signed;
        try
        {
            signed = new SignedCms(new ContentInfo(covered), detached: true);
            signed.Decode(CmsEncoding.Trimmed(signature.Contents));
        }
        catch (Exception problem) when (IsMalformed(problem))
        {
            // Nothing can be read out of a signature that does not decode, a token included.
            return new PdfSignatureVerification(signature, false, covers, null, problem.Message);
        }

        // Checked before the signature and whatever becomes of it: the token's imprint is the hash
        // of the signature value, not of the document, so a change to the signed bytes breaks the
        // signature and leaves the token exactly as intact as it was.
        var (timestamp, timestampIntact) = signed.SignerInfos.Count > 0
            ? TimestampOf(signed.SignerInfos[0], signed.Certificates)
            : (null, null);

        try
        {
            signed.CheckSignature(verifySignatureOnly: true);

            var certificate = signed.SignerInfos.Count > 0 ? signed.SignerInfos[0].Certificate : null;
            return new PdfSignatureVerification(signature, true, covers, certificate, null,
                timestamp, timestampIntact);
        }
        catch (Exception problem) when (IsMalformed(problem))
        {
            return new PdfSignatureVerification(signature, false, covers, null, problem.Message,
                timestamp, timestampIntact);
        }
    }

    private static bool IsMalformed(Exception problem) =>
        problem is CryptographicException or AsnContentException or ArgumentException;

    /// <summary>
    /// The two spans of the file the byte range names, joined.
    /// </summary>
    private static byte[] BytesCovered(int[] byteRange, byte[] document)
    {
        var first = Span(byteRange[0], byteRange[1], document.LongLength);
        var second = Span(byteRange[2], byteRange[3], document.LongLength);

        var covered = new byte[(long)first + second];
        Array.Copy(document, byteRange[0], covered, 0, first);
        Array.Copy(document, byteRange[2], covered, first, second);
        return covered;
    }

    /// <remarks>
    /// The arithmetic is in <see cref="long"/> deliberately. These numbers come out of an untrusted
    /// file, and an offset and length that each pass their own check can still sum past
    /// <see cref="int.MaxValue"/> and wrap negative — which slipped through the bound, produced an
    /// allocation of the wrong size and threw <c>OverflowException</c> out of the whole of
    /// <c>Verify</c>, taking every other signature in the document with it. A malformed byte range
    /// has to be one signature's problem and no one else's.
    /// </remarks>
    private static int Span(long offset, long length, long total)
    {
        if (offset < 0 || length < 0 || offset > total || offset + length > total)
            throw new ArgumentException(
                $"the span at {offset} of length {length} runs past the end of a {total} byte file");

        return (int)length;
    }

    /// <summary>
    /// Checks the signature-timestamp attribute a signer carries, if any, and reads the moment it
    /// claims.
    /// </summary>
    /// <returns>
    /// No time and no answer when there is no such attribute. Otherwise whether the token is intact,
    /// and the time it states only when it is: a time the bytes do not support is not reported.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <see cref="Rfc3161TimestampToken.VerifySignatureForSignerInfo"/> makes both integrity checks
    /// in one: that the token's message imprint is the hash, under the token's own algorithm, of
    /// this signer's signature value, and that the token's signature verifies over its content with
    /// the certificate it names in its signing-certificate attribute. That certificate has to be
    /// fit for the job by its own account: valid at the moment the token states, not at the moment
    /// of verifying, and carrying the time-stamping key purpose in a critical extension. None of
    /// this builds a chain or consults a trust store, so a token stays intact after its authority's
    /// certificate expires.
    /// </para>
    /// <para>
    /// The certificate is looked for in the token first, which is why the timestamp request asks for
    /// it (#193), and then among the certificates the signature itself carries, where a producer
    /// whose authority left it out may have put it. Offering more candidates loosens nothing: a
    /// candidate is used only if it is the one the token's signing-certificate attribute names. A
    /// token whose certificate is in neither place cannot be checked, and so is not intact.
    /// </para>
    /// <para>
    /// A malformed token is that signature's problem and no one else's, exactly as a malformed byte
    /// range is: it is reported as not intact rather than failing the whole verification. Only the
    /// first value of the first such attribute is checked; a signer carrying several
    /// signature-timestamps is not something this library writes.
    /// </para>
    /// </remarks>
    private static (DateTimeOffset? Timestamp, bool? Intact) TimestampOf(SignerInfo signerInfo,
        X509Certificate2Collection signatureCertificates)
    {
        foreach (var attribute in signerInfo.UnsignedAttributes)
        {
            if (attribute.Oid.Value != CmsEncoding.SignatureTimeStampTokenOid || attribute.Values.Count == 0)
                continue;

            try
            {
                if (!Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var token, out _))
                    return (null, false);

                if (!token.VerifySignatureForSignerInfo(signerInfo, out _, signatureCertificates))
                    return (null, false);

                return (token.TokenInfo.Timestamp, true);
            }
            catch (CryptographicException)
            {
                return (null, false);
            }
        }

        return (null, null);
    }

}
