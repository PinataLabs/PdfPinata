using System;
using System.Collections.Generic;
using System.Linq;
using System.Formats.Asn1;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   How <see cref="Rfc3161TimestampProvider"/> reads what a time-stamping authority sends back. The
///   authority is a server this library does not control, so what it answers is bounded — in size, in
///   time, and in where it may send the request next. Every test here stays off the network: a fake
///   <see cref="HttpMessageHandler"/> stands in for the authority, and the one test that needs the
///   provider's own client talks to a listener on the loopback interface.
/// </summary>
public class Rfc3161TimestampProviderTests
{
    private static readonly Uri Authority = new("http://tsa.example.invalid/");

    private static readonly Lazy<X509Certificate2> AuthorityCertificate =
        new(() => SigningCertificates.CreateTimestampAuthority("CN=PdfPinata Test TSA"));

    [Fact]
    public void AGenuineResponseYieldsItsToken()
    {
        using var handler = new FakeAuthority(request => new ByteArrayContent(GenuineResponseTo(request)));
        using var client = new HttpClient(handler);
        using var provider = new Rfc3161TimestampProvider(Authority, client);

        var token = provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256);

        Rfc3161TimestampToken.TryDecode(token, out _, out _).Should().BeTrue();
    }

    [Fact]
    public void TheRequestAsksTheAuthorityForItsCertificate()
    {
        byte[] query = null;
        using var handler = new FakeAuthority(request =>
        {
            query = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return new ByteArrayContent(GenuineResponseTo(request));
        });
        using var client = new HttpClient(handler);
        using var provider = new Rfc3161TimestampProvider(Authority, client);

        provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256);

        Rfc3161TimestampRequest.TryDecode(query, out var decoded, out _).Should().BeTrue();
        decoded!.RequestSignerCertificate.Should().BeTrue(
            "RFC 3161 2.4.1 lets an authority leave its certificate out of the token unless certReq asks for it");
    }

    [Fact]
    public void ASignatureTimestampedThroughTheProviderCarriesTheAuthoritysCertificate()
    {
        using var handler = new FakeAuthority(request => new ByteArrayContent(GenuineResponseTo(request)));
        using var client = new HttpClient(handler);
        using var provider = new Rfc3161TimestampProvider(Authority, client);
        var signer = new Pkcs7Signer(SigningCertificates.Default, timestampProvider: provider);

        var signature = new SignedCms();
        signature.Decode(signer.Sign(new System.IO.MemoryStream("a document"u8.ToArray())));

        var attribute = signature.SignerInfos[0].UnsignedAttributes
            .Cast<CryptographicAttributeObject>()
            .Single(a => a.Oid.Value == "1.2.840.113549.1.9.16.2.14");
        Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var token, out _).Should().BeTrue();

        token!.AsSignedCms().Certificates.Cast<X509Certificate2>()
            .Select(certificate => certificate.Thumbprint)
            .Should().Contain(AuthorityCertificate.Value.Thumbprint);
    }

    /// <summary>
    ///   Checked by <see cref="Rfc3161TimestampRequest.ProcessResponse"/>, and only because the
    ///   token now carries the certificate to check it with: without <c>certReq</c> the same damaged
    ///   token was accepted and folded into the signature.
    /// </summary>
    [Fact]
    public void ATokenWhoseSignatureDoesNotVerifyFailsTheFetch()
    {
        using var handler = new FakeAuthority(request =>
            new ByteArrayContent(GenuineResponseTo(request, corruptSignature: true)));
        using var client = new HttpClient(handler);
        using var provider = new Rfc3161TimestampProvider(Authority, client);

        var fetching = () => provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256);

        fetching.Should().Throw<InvalidOperationException>().WithMessage("*tsa.example.invalid*");
    }

    [Fact]
    public void AResponseLargerThanTheCapFailsWithoutBeingReadInFull()
    {
        var body = new CountingStream(16 * 1024 * 1024);
        using var handler = new FakeAuthority(_ => new StreamContent(body));
        using var client = new HttpClient(handler);
        using var provider = new Rfc3161TimestampProvider(Authority, client);

        var fetching = () => provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256);

        fetching.Should().Throw<InvalidOperationException>().WithMessage("*larger than*");
        body.BytesRead.Should().BeLessThan(2 * 1024 * 1024,
            "the body is read under the cap rather than buffered whole before anything looks at it");
    }

    [Fact]
    public void AResponseDeclaringALengthOverTheCapIsRefusedBeforeItsBodyIsRead()
    {
        var body = new CountingStream(16 * 1024 * 1024);
        using var handler = new FakeAuthority(_ =>
        {
            var content = new StreamContent(body);
            content.Headers.ContentLength = body.Length;
            return content;
        });
        using var client = new HttpClient(handler);
        using var provider = new Rfc3161TimestampProvider(Authority, client);

        var fetching = () => provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256);

        fetching.Should().Throw<InvalidOperationException>().WithMessage("*larger than*");
        body.BytesRead.Should().Be(0);
    }

    [Fact(Timeout = 30_000)]
    public async Task ABodyThatNeverFinishesArrivingIsBoundedByTheClientsTimeout()
    {
        using var handler = new FakeAuthority(_ => new StreamContent(new StallingStream()));
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(250) };
        using var provider = new Rfc3161TimestampProvider(Authority, client);

        var fetching = () => Task.Run(() =>
            provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256));

        await fetching.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(Timeout = 30_000)]
    public async Task TheProvidersOwnClientFollowsNoRedirect()
    {
        using var server = new RedirectingServer();
        using var provider = new Rfc3161TimestampProvider(server.Uri);

        var fetching = () => Task.Run(() =>
            provider.GetTimestamp(SHA256.HashData("signed"u8), HashAlgorithmName.SHA256));

        await fetching.Should().ThrowAsync<InvalidOperationException>();
        server.RequestTargets.Should().Equal(["/"],
            "a 307 from the authority the caller named is a failure, not somewhere else to send the query");
    }

    /// <summary>
    ///   The <c>TimeStampResp</c> an authority would send for <paramref name="request"/>: granted
    ///   status, and a token minted by <see cref="LocalTimestampAuthority"/> over the request's own
    ///   message imprint. Like a real authority, it puts its certificate in the token when the
    ///   query's <c>certReq</c> asks for it and leaves it out otherwise (RFC 3161 2.4.1).
    ///   <paramref name="corruptSignature"/> flips the last byte of the token's signature value,
    ///   leaving everything else about it — its structure, its imprint, its certificate — as it was.
    /// </summary>
    private static byte[] GenuineResponseTo(HttpRequestMessage request, bool corruptSignature = false)
    {
        var query = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        Rfc3161TimestampRequest.TryDecode(query, out var decoded, out _).Should().BeTrue();

        var authority = new LocalTimestampAuthority(AuthorityCertificate.Value);
        var token = authority.GetTimestamp(decoded!.GetMessageHash().ToArray(), HashAlgorithmName.SHA256);

        if (corruptSignature)
            token = WithCorruptSignature(token);

        // A real authority leaves its certificate out when the query's certReq says not to send it,
        // and the provider's own check of the response holds it to that.
        if (!decoded.RequestSignerCertificate)
        {
            var signed = new SignedCms();
            signed.Decode(token);
            foreach (var certificate in signed.Certificates)
                signed.RemoveCertificate(certificate);
            token = signed.Encode();
        }

        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())       // TimeStampResp
        {
            using (writer.PushSequence())   // PKIStatusInfo
                writer.WriteInteger(0);     // granted
            writer.WriteEncodedValue(token); // timeStampToken, a ContentInfo
        }

        return writer.Encode();
    }

    /// <summary>The token with the last byte of its one signature value changed.</summary>
    private static byte[] WithCorruptSignature(byte[] token)
    {
        var signed = new SignedCms();
        signed.Decode(token);
        var signature = signed.SignerInfos[0].GetSignature();

        var at = token.AsSpan().IndexOf(signature);
        at.Should().BeGreaterThanOrEqualTo(0);

        var corrupt = (byte[])token.Clone();
        corrupt[at + signature.Length - 1] ^= 0xFF;
        return corrupt;
    }

    /// <summary>Answers every request with whatever content the test hands it.</summary>
    internal sealed class FakeAuthority(Func<HttpRequestMessage, HttpContent> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = respond(request)
            });
    }

    /// <summary>A body of zeros of a set length, which counts how much of it anyone read.</summary>
    internal sealed class CountingStream(long length) : System.IO.Stream
    {
        public long BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - BytesRead);
            Array.Clear(buffer, offset, n);
            BytesRead += n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>A body whose headers have arrived and whose first byte never does.</summary>
    private sealed class StallingStream : System.IO.Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    ///   An HTTP server on the loopback interface that answers <c>/</c> with a 307 to <c>/moved</c>,
    ///   and records the target of every request it is sent. A 307 keeps the method and the body, so a
    ///   client that follows it would POST the query again, and be seen doing so.
    /// </summary>
    private sealed class RedirectingServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stopping = new();
        private readonly List<string> _targets = [];

        public RedirectingServer()
        {
            _listener.Start();
            Uri = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            _ = Task.Run(ServeAsync);
        }

        public Uri Uri { get; }

        public IReadOnlyList<string> RequestTargets
        {
            get { lock (_targets) return [.. _targets]; }
        }

        private async Task ServeAsync()
        {
            try
            {
                while (!_stopping.IsCancellationRequested)
                {
                    using var connection = await _listener.AcceptTcpClientAsync(_stopping.Token);
                    await using var stream = connection.GetStream();

                    var target = await ReadRequestAsync(stream);
                    lock (_targets) _targets.Add(target);

                    var reply = target == "/"
                        ? $"HTTP/1.1 307 Temporary Redirect\r\nLocation: {Uri}moved\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                        : "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(reply), _stopping.Token);
                }
            }
            catch (Exception stopped) when (stopped is OperationCanceledException or ObjectDisposedException
                                                or SocketException or System.IO.IOException)
            {
                // Disposed, or the client went away: either way there is nothing left to serve.
            }
        }

        /// <summary>Reads one request, headers and body, and answers its request target.</summary>
        private async Task<string> ReadRequestAsync(NetworkStream stream)
        {
            var received = new List<byte>();
            var chunk = new byte[4096];
            int headerEnd;
            while ((headerEnd = IndexOfHeaderEnd(received)) < 0)
            {
                var read = await stream.ReadAsync(chunk, _stopping.Token);
                if (read == 0)
                    return "";
                received.AddRange(chunk.AsSpan(0, read).ToArray());
            }

            var headers = Encoding.ASCII.GetString(received.ToArray(), 0, headerEnd);
            var contentLength = 0;
            foreach (var line in headers.Split("\r\n"))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    contentLength = int.Parse(line["Content-Length:".Length..].Trim());
            }

            var remaining = contentLength - (received.Count - headerEnd - 4);
            while (remaining > 0)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), _stopping.Token);
                if (read == 0)
                    break;
                remaining -= read;
            }

            return headers.Split(' ')[1];
        }

        private static int IndexOfHeaderEnd(List<byte> received)
        {
            for (var i = 0; i + 3 < received.Count; i++)
            {
                if (received[i] == '\r' && received[i + 1] == '\n' && received[i + 2] == '\r' && received[i + 3] == '\n')
                    return i;
            }

            return -1;
        }

        public void Dispose()
        {
            _stopping.Cancel();
            _listener.Stop();
            _stopping.Dispose();
        }
    }
}
