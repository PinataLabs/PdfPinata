using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using PdfPinata.Pdf.Signatures;

namespace PdfPinata.Signing;

/// <summary>
/// Signs with an X.509 certificate, producing the detached CMS signature a PDF signature dictionary
/// holds.
/// </summary>
/// <remarks>
/// <para>
/// Built on <c>System.Security.Cryptography.Pkcs</c>, which is part of the framework on every target
/// this package has — so signing adds no third-party dependency, and the private key can live
/// wherever the platform lets a certificate's key live, including a smart card or an HSM, without
/// this class knowing anything about it.
/// </para>
/// <para>
/// <b>What this produces is a signature, not a trusted one.</b> Whether a reader shows a green tick
/// depends on the certificate chaining to a root it trusts, which is a matter of what certificate is
/// passed in, not of anything here.
/// </para>
/// </remarks>
public sealed class Pkcs7Signer : IPdfSigner
{
    /// <summary>id-aa-signatureTimeStampToken, RFC 3161 / RFC 5035.</summary>
    private const string SignatureTimeStampTokenOid = "1.2.840.113549.1.9.16.2.14";

    private readonly X509Certificate2 _certificate;
    private readonly X509Certificate2Collection _chain;
    private readonly HashAlgorithmName _hashAlgorithm;
    private readonly ITimestampProvider _timestampProvider;

    /// <summary>
    /// Signs with the given certificate, which must have a usable private key.
    /// </summary>
    /// <param name="certificate">The signing certificate, with its private key.</param>
    /// <param name="format">Which flavour of signature to produce. PAdES by default.</param>
    /// <param name="hashAlgorithm">
    /// The digest to sign. SHA-256 by default, which is what everything accepts; SHA-1 is refused.
    /// </param>
    /// <param name="chain">
    /// Intermediate certificates to embed alongside the signing one, so a verifier that does not
    /// already hold them can still build the chain. The signing certificate is always embedded.
    /// </param>
    /// <param name="timestampProvider">
    /// Left unset, this produces a PAdES B-B signature exactly as before. Given one, the signature
    /// carries a trusted timestamp — PAdES B-T — folded in as an unsigned attribute of the CMS
    /// <c>SignerInfo</c>, after the signature itself has been computed. A failure fetching the token
    /// fails the whole signing rather than falling back silently.
    /// </param>
    public Pkcs7Signer(X509Certificate2 certificate, PdfSignatureFormat format = PdfSignatureFormat.Pades,
        HashAlgorithmName? hashAlgorithm = null, X509Certificate2Collection chain = null,
        ITimestampProvider timestampProvider = null)
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));

        if (!certificate.HasPrivateKey)
            throw new ArgumentException(
                "The certificate has no private key, so it can identify a signer but cannot be one.",
                nameof(certificate));

        _hashAlgorithm = hashAlgorithm ?? HashAlgorithmName.SHA256;
        if (_hashAlgorithm == HashAlgorithmName.MD5 || _hashAlgorithm == HashAlgorithmName.SHA1)
            throw new ArgumentException(
                "SHA-1 and MD5 are broken for signatures and readers reject them. Use SHA-256 or better.",
                nameof(hashAlgorithm));

        Format = format;
        _chain = chain;
        _timestampProvider = timestampProvider;
        IncludeSigningTime = format == PdfSignatureFormat.Pkcs7;
    }

    /// <summary>
    /// Which flavour of signature this produces.
    /// </summary>
    public PdfSignatureFormat Format { get; }

    /// <inheritdoc/>
    public string SubFilter =>
        Format == PdfSignatureFormat.Pades ? "/ETSI.CAdES.detached" : "/adbe.pkcs7.detached";

    /// <summary>
    /// How much room to reserve in the file for the signature.
    /// </summary>
    /// <remarks>
    /// The default is generous on purpose. A CMS signature with one RSA-2048 certificate is around
    /// 1.5 kB, but the embedded chain, the key size and the algorithm all move it, and the cost of
    /// guessing high is a few kilobytes of file while the cost of guessing low is an exception after
    /// the document has already been written.
    /// </remarks>
    public int EstimatedSignatureSize { get; set; } = 16384;

    /// <summary>
    /// Whether to record the signing time as a signed CMS attribute as well as in the signature
    /// dictionary's <c>/M</c>.
    /// </summary>
    /// <remarks>
    /// <b>Off for PAdES and on for PKCS#7</b>, which is what the two formats ask for. PAdES carries
    /// the claimed signing time in <c>/M</c> and the ETSI profiles have said since TS 102 778 that
    /// the CMS <c>signing-time</c> attribute should not be there as well — two claimed times that
    /// can disagree help nobody. Either way the time is the signer's own and proves nothing; only a
    /// timestamp from a time-stamping authority does, and that is PAdES B-T.
    /// </remarks>
    public bool IncludeSigningTime { get; set; }

    /// <inheritdoc/>
    public byte[] Sign(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var signed = new SignedCms(new ContentInfo(ReadAll(content)), detached: true);

        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, _certificate)
        {
            DigestAlgorithm = new Oid(HashAlgorithmOids.Of(_hashAlgorithm)),
            IncludeOption = X509IncludeOption.WholeChain
        };

        if (_chain != null)
        {
            foreach (var certificate in _chain)
                signer.Certificates.Add(certificate);
        }

        if (IncludeSigningTime)
            signer.SignedAttributes.Add(new Pkcs9SigningTime(GlobalTimeSettings.Now));

        if (Format == PdfSignatureFormat.Pades)
            signer.SignedAttributes.Add(SigningCertificateAttribute.Build(_certificate, _hashAlgorithm));

        signed.ComputeSignature(signer);

        if (_timestampProvider != null)
            Timestamp(signed);

        return signed.Encode();
    }

    /// <summary>
    /// Folds a timestamp token for the signature just computed into the message as an unsigned
    /// attribute, so it travels inside the same blob <see cref="Sign"/> always returned.
    /// </summary>
    /// <remarks>
    /// The token covers the signature value itself — the CAdES signature-timestamp, not a timestamp
    /// over the document — which is what lets a verifier believe the signature existed at the time
    /// the token was issued regardless of what happens to the certificate afterwards.
    /// </remarks>
    private void Timestamp(SignedCms signed)
    {
        var signerInfo = signed.SignerInfos[0];
        var messageImprint = HashOf(signerInfo.GetSignature(), _hashAlgorithm);
        var token = _timestampProvider.GetTimestamp(messageImprint, _hashAlgorithm);

        if (token == null || token.Length == 0)
            throw new InvalidOperationException("The timestamp provider returned no timestamp token.");

        signerInfo.AddUnsignedAttribute(new AsnEncodedData(new Oid(SignatureTimeStampTokenOid), token));
    }

    private static byte[] HashOf(byte[] data, HashAlgorithmName algorithm)
    {
        using var hasher = IncrementalHash.CreateHash(algorithm);
        hasher.AppendData(data);
        return hasher.GetHashAndReset();
    }

    private static byte[] ReadAll(Stream stream)
    {
        if (stream is MemoryStream already)
            return already.ToArray();

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
