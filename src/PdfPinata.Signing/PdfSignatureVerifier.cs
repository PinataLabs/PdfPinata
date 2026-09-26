using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
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
/// certificate chain, consult a trust store, check revocation, or evaluate a timestamp — all of
/// which a reader showing a green tick has done and none of which is implemented here. A signature
/// this reports as valid may still have been made with a certificate nobody should trust.
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

        try
        {
            var encoded = CmsEncoding.Trimmed(signature.Contents);

            var signed = new SignedCms(new ContentInfo(covered), detached: true);
            signed.Decode(encoded);
            signed.CheckSignature(verifySignatureOnly: true);

            var certificate = signed.SignerInfos.Count > 0 ? signed.SignerInfos[0].Certificate : null;
            var timestamp = signed.SignerInfos.Count > 0 ? TimestampOf(signed.SignerInfos[0]) : null;
            return new PdfSignatureVerification(signature, true, covers, certificate, null, timestamp);
        }
        catch (Exception problem) when (problem is CryptographicException
                                            or AsnContentException
                                            or ArgumentException)
        {
            return new PdfSignatureVerification(signature, false, covers, null, problem.Message);
        }
    }

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
    /// Reads the moment a signature-timestamp attribute claims, if the signer carries one.
    /// </summary>
    /// <remarks>
    /// This decodes the token's own structure to answer what it says; it does not check that the
    /// token's signature verifies or that its issuer is trusted, for the same reason the signature
    /// itself is checked without either. A token this cannot decode is treated as absent rather than
    /// as a failure of the whole verification — one signature's malformed attribute is that
    /// signature's problem, exactly as a malformed byte range is.
    /// </remarks>
    private static DateTimeOffset? TimestampOf(SignerInfo signerInfo)
    {
        foreach (var attribute in signerInfo.UnsignedAttributes)
        {
            if (attribute.Oid.Value != CmsEncoding.SignatureTimeStampTokenOid || attribute.Values.Count == 0)
                continue;

            try
            {
                if (Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var token, out _))
                    return token.TokenInfo.Timestamp;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        return null;
    }

}
