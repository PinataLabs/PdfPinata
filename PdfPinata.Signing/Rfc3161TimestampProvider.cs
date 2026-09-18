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
    readonly Uri _timestampAuthorityUri;
    readonly HttpClient _httpClient;
    readonly bool _ownsHttpClient;

    /// <summary>
    /// Talks to the time-stamping authority at the given URI.
    /// </summary>
    /// <param name="timestampAuthorityUri">The TSA's endpoint, e.g. a counterparty's or a public one.</param>
    /// <param name="httpClient">
    /// Reused rather than created per call, if given. Left unset, this makes and owns one client for
    /// its own lifetime — construct one instance and reuse it rather than making one per signature.
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
            _httpClient = new HttpClient();
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
                                             or TaskCanceledException or System.Formats.Asn1.AsnContentException)
        {
            throw new InvalidOperationException(
                $"Fetching a timestamp from {_timestampAuthorityUri} failed: {problem.Message}", problem);
        }
    }

    async Task<byte[]> GetTimestampAsync(Rfc3161TimestampRequest request)
    {
        using var content = new ByteArrayContent(request.Encode());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/timestamp-query");

        using var response = await _httpClient.PostAsync(_timestampAuthorityUri, content).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
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
