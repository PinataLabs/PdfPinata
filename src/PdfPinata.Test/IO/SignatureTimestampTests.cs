using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using TUnit.Core;
using static PdfPinata.Test.Helpers.SigningCertificates;

namespace PdfPinata.Test.IO;

/// <summary>
///   PAdES B-T: a signature timestamp says a document was signed at a moment a party other than the
///   producer is answerable for. <see cref="LocalTimestampAuthority"/> mints a token the same shape a
///   real one over HTTP would, from a certificate the test controls, so these never touch the network.
/// </summary>
public class SignatureTimestampTests
{
    [Test]
    public void ASignatureWithATimestampReportsWhatItSays()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var signed = Sign(Unsigned(), signer: Timestamped());

        var after = DateTimeOffset.UtcNow.AddSeconds(5);
        var verification = PdfSignatureVerifier.Verify(signed).Single();

        verification.IsValid.Should().BeTrue();
        verification.HasTimestamp.Should().BeTrue();
        verification.IsTimestampIntact.Should().BeTrue();
        verification.Timestamp.Should().NotBeNull();
        verification.Timestamp!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Test]
    public void ASignatureWithoutATimestampReportsNone()
    {
        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned())).Single();

        verification.IsValid.Should().BeTrue();
        verification.HasTimestamp.Should().BeFalse();
        verification.IsTimestampIntact.Should().BeNull();
        verification.Timestamp.Should().BeNull();
    }

    [Test]
    public void ATimestampTokenWhoseOwnSignatureIsDamagedIsNotIntactAndItsTimeIsNotReported()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default,
            timestampProvider: new TamperingTimestampProvider(Authority(), FlipASignatureByte));

        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned(), signer: signer)).Single();

        verification.IsValid.Should().BeTrue("the token is an unsigned attribute, outside what the signature covers");
        verification.IsTimestampIntact.Should().BeFalse();
        verification.HasTimestamp.Should().BeFalse();
        verification.Timestamp.Should().BeNull();
    }

    [Test]
    public void ATimestampTokenTakenFromAnotherSignatureIsNotIntact()
    {
        var recorder = new RecordingTimestampProvider(Authority());
        var other = Sign(Unsigned(), signer: new Pkcs7Signer(SigningCertificates.Default, timestampProvider: recorder));
        var someoneElsesToken = recorder.Token;
        PdfSignatureVerifier.Verify(other).Single().IsTimestampIntact.Should().BeTrue(
            "the token is sound where it came from, so only the move can make it fail");

        var signer = new Pkcs7Signer(SigningCertificates.Default,
            timestampProvider: new TamperingTimestampProvider(Authority(), _ => someoneElsesToken));

        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned(), signer: signer)).Single();

        verification.IsValid.Should().BeTrue();
        verification.IsTimestampIntact.Should().BeFalse();
        verification.HasTimestamp.Should().BeFalse();
        verification.Timestamp.Should().BeNull();
    }

    [Test]
    public void ATokenWithoutItsCertificateIsCheckedAgainstTheCertificatesTheSignatureCarries()
    {
        var authorityCertificate = SigningCertificates.CreateTimestampAuthority("CN=PdfPinata Test TSA");
        var signer = new Pkcs7Signer(SigningCertificates.Default,
            chain: [authorityCertificate],
            timestampProvider: new TamperingTimestampProvider(
                new LocalTimestampAuthority(authorityCertificate), WithoutCertificates));

        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned(), signer: signer)).Single();

        verification.IsValid.Should().BeTrue();
        verification.IsTimestampIntact.Should().BeTrue();
        verification.HasTimestamp.Should().BeTrue();
    }

    [Test]
    public void ATokenWhoseCertificateIsNowhereToBeFoundIsNotIntact()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default,
            timestampProvider: new TamperingTimestampProvider(Authority(), WithoutCertificates));

        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned(), signer: signer)).Single();

        verification.IsValid.Should().BeTrue();
        verification.IsTimestampIntact.Should().BeFalse("a signature that cannot be checked cannot be called intact");
        verification.Timestamp.Should().BeNull();
    }

    [Test]
    public void ATimestampAttributeThatIsNotATokenIsNotIntact()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default,
            timestampProvider: new TamperingTimestampProvider(Authority(), _ => [0x04, 0x03, 0x01, 0x02, 0x03]));

        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned(), signer: signer)).Single();

        verification.IsValid.Should().BeTrue();
        verification.IsTimestampIntact.Should().BeFalse();
        verification.Timestamp.Should().BeNull();
    }

    [Test]
    public void ATimestampIsCheckedOnASignatureTheDocumentNoLongerMatches()
    {
        // The token's imprint is the hash of the signature value, not of the document, so a change
        // to the signed bytes breaks the signature and leaves the token as intact as it was. It is
        // still there, and reporting it as absent would say the signature never had one.
        var signed = Sign(Unsigned(document =>
            document.Info.Elements["/Keywords"] = new PdfString("TAMPERTARGET")), signer: Timestamped());
        var at = signed.AsSpan().IndexOf("TAMPERTARGET"u8);
        at.Should().BeGreaterThan(-1, "the marker has to be findable for this test to be testing anything");
        signed[at] = (byte)'X';

        var verification = PdfSignatureVerifier.Verify(signed).Single();

        verification.IsIntact.Should().BeFalse();
        verification.IsTimestampIntact.Should().BeTrue();
        verification.HasTimestamp.Should().BeTrue();
    }

    [Test]
    public void ATimestampSourceThatFailsFailsTheSigningAndNothingIsWritten()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default, timestampProvider: new FailingTimestampProvider());

        Action signing = () => Sign(Unsigned(), signer: signer);

        signing.Should().Throw<InvalidOperationException>().WithMessage("*timed out*");
    }

    private static Pkcs7Signer Timestamped() =>
        new(SigningCertificates.Default, timestampProvider: Authority());

    private static LocalTimestampAuthority Authority() =>
        new(SigningCertificates.CreateTimestampAuthority("CN=PdfPinata Test TSA"));

    /// <summary>
    /// Flips a byte of the token's own signature value, which leaves its DER structure, and so
    /// everything a structural read of it sees, exactly as it was.
    /// </summary>
    private static byte[] FlipASignatureByte(byte[] token)
    {
        var decoded = new SignedCms();
        decoded.Decode(token);
        var signature = decoded.SignerInfos[0].GetSignature();

        var at = token.AsSpan().IndexOf(signature);
        at.Should().BeGreaterThanOrEqualTo(0, "the token's signature value is written into it verbatim");

        var damaged = (byte[])token.Clone();
        damaged[at + signature.Length - 1] ^= 0x01;
        return damaged;
    }

    /// <summary>
    /// What an authority sends when the request does not ask for its certificate (RFC 3161 2.4.1).
    /// The certificates lie outside what the token's signature covers, so it still verifies.
    /// </summary>
    private static byte[] WithoutCertificates(byte[] token)
    {
        var decoded = new SignedCms();
        decoded.Decode(token);
        foreach (var certificate in decoded.Certificates)
            decoded.RemoveCertificate(certificate);

        return decoded.Encode();
    }

    /// <summary>Hands back whatever the authority mints, changed on the way.</summary>
    private sealed class TamperingTimestampProvider(ITimestampProvider inner, Func<byte[], byte[]> tamper)
        : ITimestampProvider
    {
        public byte[] GetTimestamp(byte[] messageImprint, HashAlgorithmName hashAlgorithm) =>
            tamper(inner.GetTimestamp(messageImprint, hashAlgorithm));
    }

    /// <summary>Remembers the last token the authority minted.</summary>
    private sealed class RecordingTimestampProvider(ITimestampProvider inner) : ITimestampProvider
    {
        public byte[] Token { get; private set; }

        public byte[] GetTimestamp(byte[] messageImprint, HashAlgorithmName hashAlgorithm) =>
            Token = inner.GetTimestamp(messageImprint, hashAlgorithm);
    }

    private sealed class FailingTimestampProvider : ITimestampProvider
    {
        public byte[] GetTimestamp(byte[] messageImprint, HashAlgorithmName hashAlgorithm) =>
            throw new InvalidOperationException("The time-stamping authority timed out.");
    }
}
