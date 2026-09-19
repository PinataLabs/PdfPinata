using System;
using System.Formats.Asn1;
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

        var result = provider.GetRevocationData(certificate, new X509Certificate2Collection());

        result.Should().BeSameAs(RevocationData.None);
    }

    [Fact]
    public void AMalformedAuthorityInfoAccessExtensionAnswersNoEvidenceRatherThanThrow()
    {
        var certificate = CertificateWithAuthorityInfoAccess(new byte[] { 0x01, 0x02, 0x03 });
        var provider = new OcspRevocationDataProvider();

        Func<RevocationData> gathering = () =>
            provider.GetRevocationData(certificate, new X509Certificate2Collection());

        gathering.Should().NotThrow();
        gathering().Should().BeSameAs(RevocationData.None);
    }

    [Fact]
    public void AResponderNamedByAnUnsupportedSchemeAnswersNoEvidence()
    {
        var certificate = CertificateWithAuthorityInfoAccess(
            AuthorityInfoAccess("ftp://example.invalid/ocsp"));
        var provider = new OcspRevocationDataProvider();

        var result = provider.GetRevocationData(certificate, new X509Certificate2Collection());

        result.Should().BeSameAs(RevocationData.None);
    }

    /// <summary>
    ///   A minimal <c>AuthorityInfoAccessSyntax</c> naming one OCSP responder, built the same way
    ///   <see cref="OcspRevocationDataProvider"/> itself builds ASN.1 — so a test asserting how it is
    ///   read is not also trusting a different encoder to agree with it.
    /// </summary>
    static byte[] AuthorityInfoAccess(string ocspUri)
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

    static X509Certificate2 CertificateWithAuthorityInfoAccess(byte[] rawExtensionData)
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
