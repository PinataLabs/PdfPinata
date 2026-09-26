using System;
using System.Formats.Asn1;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using PdfPinata.Signing;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   The parts of <see cref="OcspRevocationDataProvider"/> reachable without a network call. A
///   certificate's own Authority Information Access extension can be malformed, absent, or name a
///   responder this will not talk to — every one of those has to answer
///   <see cref="RevocationData.None"/> rather than throw, because gathering validation data is
///   best-effort: one certificate with bad data should not fail evidence-gathering for the rest of a
///   chain. All three reach that answer before <see cref="OcspRevocationDataProvider.GetRevocationData"/>
///   would ever make a request, which is what keeps these tests off the network.
/// </summary>
public class OcspRevocationDataProviderTests
{
    [Fact]
    public void ACertificateWithNoAuthorityInfoAccessExtensionAnswersNoEvidence()
    {
        var certificate = CertificateWithAuthorityInfoAccess(null);
        var provider = new OcspRevocationDataProvider();

        var result = provider.GetRevocationData(certificate, []);

        result.Should().BeSameAs(RevocationData.None);
    }

    [Fact]
    public void AMalformedAuthorityInfoAccessExtensionAnswersNoEvidenceRatherThanThrow()
    {
        var certificate = CertificateWithAuthorityInfoAccess([0x01, 0x02, 0x03]);
        var provider = new OcspRevocationDataProvider();

        var gathering = () =>
            provider.GetRevocationData(certificate, []);

        gathering.Should().NotThrow();
        gathering().Should().BeSameAs(RevocationData.None);
    }

    [Fact]
    public void AResponderNamedByAnUnsupportedSchemeAnswersNoEvidence()
    {
        var certificate = CertificateWithAuthorityInfoAccess(
            AuthorityInfoAccess("ftp://example.invalid/ocsp"));
        var provider = new OcspRevocationDataProvider();

        var result = provider.GetRevocationData(certificate, []);

        result.Should().BeSameAs(RevocationData.None);
    }

    [Fact]
    public void AResponseFromTheResponderIsAnsweredAsEvidence()
    {
        var (certificate, chain) = CertificateIssuedWithResponder("http://ocsp.example.invalid/");
        var answer = new byte[] { 0x30, 0x03, 0x0A, 0x01, 0x00 };
        using var handler = new Rfc3161TimestampProviderTests.FakeAuthority(_ => new ByteArrayContent(answer));
        using var client = new HttpClient(handler);
        using var provider = new OcspRevocationDataProvider(client);

        var result = provider.GetRevocationData(certificate, chain);

        result.OcspResponses.Should().ContainSingle().Which.Should().Equal(answer);
    }

    [Fact]
    public void AResponseLargerThanTheCapAnswersNoEvidenceWithoutBeingReadInFull()
    {
        var (certificate, chain) = CertificateIssuedWithResponder("http://ocsp.example.invalid/");
        var body = new Rfc3161TimestampProviderTests.CountingStream(16 * 1024 * 1024);
        using var handler = new Rfc3161TimestampProviderTests.FakeAuthority(_ => new StreamContent(body));
        using var client = new HttpClient(handler);
        using var provider = new OcspRevocationDataProvider(client);

        var result = provider.GetRevocationData(certificate, chain);

        result.Should().BeSameAs(RevocationData.None);
        body.BytesRead.Should().BeLessThan(2 * 1024 * 1024);
    }

    /// <summary>
    ///   A leaf naming <paramref name="ocspUri"/> as its responder, and a chain holding the issuer it
    ///   names: the two things <see cref="OcspRevocationDataProvider.GetRevocationData"/> needs before
    ///   it sends a request at all.
    /// </summary>
    private static (X509Certificate2 Certificate, X509Certificate2Collection Chain) CertificateIssuedWithResponder(
        string ocspUri)
    {
        using var issuerKey = RSA.Create(2048);
        var issuerRequest = new CertificateRequest("CN=PdfPinata Test Issuer", issuerKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        var issuer = issuerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        using var leafKey = RSA.Create(2048);
        var leafRequest = new CertificateRequest("CN=PdfPinata Test Subject", leafKey, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        leafRequest.CertificateExtensions.Add(
            new X509Extension("1.3.6.1.5.5.7.1.1", AuthorityInfoAccess(ocspUri), critical: false));
        var leaf = leafRequest.Create(issuer, DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMonths(1),
            [1, 2, 3, 4]);

        return (leaf, [issuer]);
    }

    /// <summary>
    ///   A minimal <c>AuthorityInfoAccessSyntax</c> naming one OCSP responder, built the same way
    ///   <see cref="OcspRevocationDataProvider"/> itself builds ASN.1 — so a test asserting how it is
    ///   read is not also trusting a different encoder to agree with it.
    /// </summary>
    private static byte[] AuthorityInfoAccess(string ocspUri)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())      // AuthorityInfoAccessSyntax
        using (writer.PushSequence())      // AccessDescription
        {
            writer.WriteObjectIdentifier("1.3.6.1.5.5.7.48.1"); // id-pkix-ocsp
            writer.WriteCharacterString(UniversalTagNumber.IA5String, ocspUri,
                new Asn1Tag(TagClass.ContextSpecific, 6));
        }

        return writer.Encode();
    }

    private static X509Certificate2 CertificateWithAuthorityInfoAccess(byte[] rawExtensionData)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=PdfPinata Test Subject", key, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        if (rawExtensionData != null)
            request.CertificateExtensions.Add(
                new X509Extension("1.3.6.1.5.5.7.1.1", rawExtensionData, critical: false));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
