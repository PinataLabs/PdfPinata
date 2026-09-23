using System;
using System.Formats.Asn1;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PdfPinata.Signing;

/// <summary>
/// Fetches an OCSP response for a certificate over HTTP, per RFC 6960, using the responder named in
/// the certificate's own Authority Information Access extension.
/// </summary>
/// <remarks>
/// <para>
/// The shipped implementation for real use, the way <see cref="Rfc3161TimestampProvider"/> is for
/// timestamps. It supplies OCSP responses only — CRLs are left empty in what it answers — because OCSP
/// is what a certificate names its own responder for; fetching a CRL from a certificate's distribution
/// point is a caller's to add if it wants both channels covered.
/// </para>
/// <para>
/// A certificate with no OCSP responder named, or whose issuer is not among the certificates handed
/// to <see cref="GetRevocationData"/>, is not a failure: it answers <see cref="RevocationData.None"/>
/// rather than throw, because gathering validation data is inherently best-effort — not every
/// certificate carries evidence, and one that has none is not the request that should fail.
/// </para>
/// </remarks>
public sealed class OcspRevocationDataProvider : IRevocationDataProvider, IDisposable
{
    /// <summary>id-pkix-ocsp, RFC 6960.</summary>
    private const string OcspAccessMethodOid = "1.3.6.1.5.5.7.48.1";

    /// <summary>id-pe-authorityInfoAccess, RFC 5280.</summary>
    private const string AuthorityInfoAccessOid = "1.3.6.1.5.5.7.1.1";

    /// <summary>
    /// A generous cap on how large an OCSP response this will read into memory. A real response is a
    /// few kilobytes; this is headroom rather than a tight bound, and exists only so a malicious or
    /// misbehaving responder — named by the certificate being checked, not chosen by the caller — cannot
    /// turn evidence-gathering into unbounded memory use.
    /// </summary>
    private const int MaxOcspResponseBytes = 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <param name="httpClient">
    /// Reused rather than created per call, if given. Left unset, this makes and owns one for its own
    /// lifetime — construct one instance and reuse it rather than making one per certificate. The
    /// owned client follows no redirect: the request goes to a URI a certificate named, not one the
    /// caller chose, so a 3xx response is treated as failure rather than as somewhere else to go. A
    /// caller supplying its own client decides that policy for itself.
    /// </param>
    public OcspRevocationDataProvider(HttpClient httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            _ownsHttpClient = true;
        }
    }

    /// <inheritdoc/>
    public RevocationData GetRevocationData(X509Certificate2 certificate, X509Certificate2Collection chain)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var responderUri = OcspResponderOf(certificate);
        if (responderUri == null)
            return RevocationData.None;

        var issuer = IssuerOf(certificate, chain);
        if (issuer == null)
            return RevocationData.None;

        var request = BuildRequest(certificate, issuer);

        try
        {
            using var content = new ByteArrayContent(request);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/ocsp-request");

            // ReSharper disable once UsingStatementResourceInitialization
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, responderUri) { Content = content };

            // ResponseHeadersRead so the body is read by ReadBounded, under its own cap, rather than
            // buffered in full by the client before this ever sees it.
            using var response = _httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead)
                .GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var responseBytes = ReadBounded(response.Content, MaxOcspResponseBytes);
            if (responseBytes == null)
                return RevocationData.None;

            return new RevocationData(new[] { responseBytes }, null);
        }
        catch (Exception problem) when (problem is HttpRequestException or TaskCanceledException)
        {
            return RevocationData.None;
        }
    }

    /// <summary>
    /// Reads <paramref name="content"/> into memory, or answers null once it has read more than
    /// <paramref name="maxBytes"/> without ever buffering the excess.
    /// </summary>
    private static byte[] ReadBounded(HttpContent content, int maxBytes)
    {
        using var stream = content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
                return null;
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// The certificate in <paramref name="chain"/> that issued <paramref name="certificate"/>, found
    /// by subject/issuer name rather than by verifying a signature — this is evidence-gathering, not
    /// trust, and the same certificate the OCSP request names its issuer by is what a responder needs.
    /// </summary>
    private static X509Certificate2 IssuerOf(X509Certificate2 certificate, X509Certificate2Collection chain)
    {
        if (chain == null)
            return null;

        foreach (var candidate in chain)
        {
            if (candidate.Thumbprint != certificate.Thumbprint
                && string.Equals(candidate.Subject, certificate.Issuer, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    private static Uri OcspResponderOf(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions[AuthorityInfoAccessOid];
        if (extension == null)
            return null;

        // The extension's bytes come from a certificate this code did not create, so a malformed
        // one is an absence of evidence rather than a reason to fail the whole request — the same
        // stance GetRevocationData takes on an issuer it cannot find or a responder that will not
        // answer.
        try
        {
            var reader = new AsnReader(extension.RawData, AsnEncodingRules.BER);
            var accessDescriptions = reader.ReadSequence();

            while (accessDescriptions.HasData)
            {
                var accessDescription = accessDescriptions.ReadSequence();
                var accessMethod = accessDescription.ReadObjectIdentifier();

                var uriTag = new Asn1Tag(TagClass.ContextSpecific, 6);
                if (accessMethod == OcspAccessMethodOid && accessDescription.PeekTag() == uriTag)
                {
                    var uri = accessDescription.ReadCharacterString(UniversalTagNumber.IA5String, uriTag);

                    // http(s) only. The URI names where an HTTP POST goes, chosen by whoever issued
                    // the certificate being checked rather than by this library's caller — accepting
                    // any scheme Uri.TryCreate parses would hand that issuer more than "which server",
                    // for no benefit, since RFC 6960 traffic is HTTP either way.
                    if (Uri.TryCreate(uri, UriKind.Absolute, out var responderUri)
                        && (responderUri.Scheme == Uri.UriSchemeHttp || responderUri.Scheme == Uri.UriSchemeHttps))
                        return responderUri;
                }
            }
        }
        catch (AsnContentException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// A minimal, unsigned <c>OCSPRequest</c> asking about one certificate: no requestor name, no
    /// extensions, nothing a responder would need a nonce or a signature to answer.
    /// </summary>
    private static byte[] BuildRequest(X509Certificate2 certificate, X509Certificate2 issuer)
    {
        // SHA-1 identifies the certificate here; it protects nothing. RFC 6960's CertID names the
        // issuer by hash, and SHA-1 is the one algorithm every responder is required to accept
        // (RFC 5019), so a stronger one would get "unknown" back from responders that do not match
        // on it.
        #pragma warning disable S4790 // SHA-1 is what OCSP responders are required to understand; see above.
        var issuerNameHash = SHA1.HashData(issuer.SubjectName.RawData);
        var issuerKeyHash = SHA1.HashData(issuer.PublicKey.EncodedKeyValue.RawData);
        #pragma warning restore S4790

        var serialNumberBytes = certificate.GetSerialNumber(); // little-endian
        var serialNumber = new System.Numerics.BigInteger(serialNumberBytes, isUnsigned: true, isBigEndian: false);

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())      // OCSPRequest
        using (writer.PushSequence())      // TBSRequest
        using (writer.PushSequence())      // requestList
        using (writer.PushSequence())      // Request
        using (writer.PushSequence())      // CertID
        {
            using (writer.PushSequence()) // hashAlgorithm: SHA-1, per RFC 6960's own examples
            {
                writer.WriteObjectIdentifier("1.3.14.3.2.26");
                writer.WriteNull();
            }

            writer.WriteOctetString(issuerNameHash);
            writer.WriteOctetString(issuerKeyHash);
            writer.WriteInteger(serialNumber);
        }

        return writer.Encode();
    }

    /// <summary>
    /// Releases the <see cref="HttpClient"/> this created, if it made one for itself rather than
    /// being handed one to reuse.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
