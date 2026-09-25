using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPinata.Signing;

/// <summary>
/// The one HTTP exchange both network providers make: POST a DER-encoded request, read a DER-encoded
/// response, and read no more of it than the caller allows.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OcspRevocationDataProvider"/> and <see cref="Rfc3161TimestampProvider"/> send the same
/// shape of request to a server this library does not control, and it is the reading of the answer
/// that has to be guarded — a misbehaving or hostile server can send a body of any length. Keeping
/// the exchange here is what stops one provider having a guard the other lacks.
/// </para>
/// <para>
/// Redirect policy is deliberately not here: it belongs to the <see cref="HttpClient"/>, which a
/// caller may supply. Each provider's own client follows none.
/// </para>
/// </remarks>
internal static class DerHttp
{
    /// <summary>
    /// POSTs <paramref name="der"/> to <paramref name="uri"/> as <paramref name="mediaType"/> and
    /// answers the response body.
    /// </summary>
    /// <exception cref="HttpRequestException">
    /// The response was not a success, or its body ran past <paramref name="maxBytes"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The exchange, body included, outlasted <see cref="HttpClient.Timeout"/>.
    /// </exception>
    public static async Task<byte[]> PostAsync(HttpClient client, Uri uri, byte[] der, string mediaType,
        int maxBytes)
    {
        using var content = new ByteArrayContent(der);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        // ReSharper disable once UsingStatementResourceInitialization
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };

        // ResponseHeadersRead hands the body over unread, so it is read below under the cap rather
        // than buffered in full by the client first. It also takes the body out of what
        // HttpClient.Timeout covers, so the same limit is put back over the whole exchange: a server
        // that sends its headers and then drips the body would otherwise hold the caller forever.
        using var timeout = client.Timeout == Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource()
            : new CancellationTokenSource(client.Timeout);

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await ReadBoundedAsync(response.Content, maxBytes, uri, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads <paramref name="content"/> into memory, or throws once it has read more than
    /// <paramref name="maxBytes"/> without ever buffering the excess.
    /// </summary>
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maxBytes, Uri uri,
        CancellationToken cancellationToken)
    {
        // A declared length over the cap is refused before a byte of it is read.
        if (content.Headers.ContentLength > maxBytes)
            throw TooLarge(uri, maxBytes);

        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > maxBytes)
                throw TooLarge(uri, maxBytes);
        }

        return buffer.ToArray();
    }

    private static HttpRequestException TooLarge(Uri uri, int maxBytes) =>
        new($"The response from {uri} is larger than the {maxBytes} bytes this will read.");
}
