using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Threading.Tasks;

namespace PdfPinata.Signing;

/// <summary>
/// Fetches a timestamp token from a real time-stamping authority over HTTP, per RFC 3161.
/// </summary>
/// <remarks>
/// The shipped implementation for real use: name the authority your organisation trusts, and every
/// signature made with it carries a token that authority is answerable for. Built entirely on
/// <see cref="Rfc3161TimestampRequest"/>, which already knows the request and response wire formats;
/// nothing here re-implements them.
/// </remarks>
public sealed class Rfc3161TimestampProvider : ITimestampProvider, IDisposable
{
    /// <summary>
    /// A generous cap on how large a timestamp response this will read into memory. A real one is a
    /// token and perhaps the authority's certificate chain, a few kilobytes; this is headroom rather
    /// than a tight bound, and exists so that a misbehaving authority cannot turn signing into
    /// unbounded memory use.
    /// </summary>
    private const int MaxTimestampResponseBytes = 1024 * 1024;

    private readonly Uri _timestampAuthorityUri;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Talks to the time-stamping authority at the given URI.
    /// </summary>
    /// <param name="timestampAuthorityUri">The TSA's endpoint, e.g. a counterparty's or a public one.</param>
    /// <param name="httpClient">
    /// Reused rather than created per call, if given. Left unset, this makes and owns one client for
    /// its own lifetime — construct one instance and reuse it rather than making one per signature.
    /// The owned client follows no redirect: the caller named the authority it trusts, and a 3xx
    /// response would put a different server in its place without the caller ever seeing the new
    /// name, so it is treated as failure instead. A caller supplying its own client decides that
    /// policy for itself. Either way no more than a megabyte of the response is read, and the whole
    /// exchange, body included, is bounded by the client's <see cref="HttpClient.Timeout"/>.
    /// </param>
    public Rfc3161TimestampProvider(Uri timestampAuthorityUri, HttpClient httpClient = null)
    {
        _timestampAuthorityUri = timestampAuthorityUri
            ?? throw new ArgumentNullException(nameof(timestampAuthorityUri));

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
    public byte[] GetTimestamp(byte[] messageImprint, HashAlgorithmName hashAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(messageImprint);

        var request = Rfc3161TimestampRequest.CreateFromHash(messageImprint, hashAlgorithm);

        try
        {
            return GetTimestampAsync(request).GetAwaiter().GetResult();
        }
        catch (Exception problem) when (problem is HttpRequestException or CryptographicException
                                             or OperationCanceledException or System.Formats.Asn1.AsnContentException)
        {
            throw new InvalidOperationException(
                $"Fetching a timestamp from {_timestampAuthorityUri} failed: {problem.Message}", problem);
        }
    }

    private async Task<byte[]> GetTimestampAsync(Rfc3161TimestampRequest request)
    {
        var responseBytes = await DerHttp
            .PostAsync(_httpClient, _timestampAuthorityUri, request.Encode(), "application/timestamp-query",
                MaxTimestampResponseBytes)
            .ConfigureAwait(false);
        var token = request.ProcessResponse(responseBytes, out _);
        return token.AsSignedCms().Encode();
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
