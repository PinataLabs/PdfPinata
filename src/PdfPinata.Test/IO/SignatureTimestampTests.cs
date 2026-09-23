using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.IO;

/// <summary>
///   PAdES B-T: a signature timestamp says a document was signed at a moment a party other than the
///   producer is answerable for. <see cref="LocalTimestampAuthority"/> mints a token the same shape a
///   real one over HTTP would, from a certificate the test controls, so these never touch the network.
/// </summary>
public class SignatureTimestampTests
{
    [Fact]
    public void ASignatureWithATimestampReportsWhatItSays()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var signed = Sign(Unsigned(), Timestamped());

        var after = DateTimeOffset.UtcNow.AddSeconds(5);
        var verification = PdfSignatureVerifier.Verify(signed).Single();

        verification.IsValid.Should().BeTrue();
        verification.HasTimestamp.Should().BeTrue();
        verification.Timestamp.Should().NotBeNull();
        verification.Timestamp!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void ASignatureWithoutATimestampReportsNone()
    {
        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned())).Single();

        verification.IsValid.Should().BeTrue();
        verification.HasTimestamp.Should().BeFalse();
        verification.Timestamp.Should().BeNull();
    }

    [Fact]
    public void ATimestampSourceThatFailsFailsTheSigningAndNothingIsWritten()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default, timestampProvider: new FailingTimestampProvider());

        Action signing = () => Sign(Unsigned(), signer);

        signing.Should().Throw<InvalidOperationException>().WithMessage("*timed out*");
    }

    private static Pkcs7Signer Timestamped() =>
        new(SigningCertificates.Default, timestampProvider: new LocalTimestampAuthority(
            SigningCertificates.CreateTimestampAuthority("CN=PdfPinata Test TSA")));

    private sealed class FailingTimestampProvider : ITimestampProvider
    {
        public byte[] GetTimestamp(byte[] messageImprint, System.Security.Cryptography.HashAlgorithmName hashAlgorithm) =>
            throw new InvalidOperationException("The time-stamping authority timed out.");
    }

    private static byte[] Unsigned()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawString("A document to timestamp", new XFont("Arial", 12), XBrushes.Black, 40, 100);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] Sign(byte[] document, IPdfSigner signer = null)
    {
        using var input = new MemoryStream(document);
        using var output = new MemoryStream();

        PdfSigner.Sign(input, output, signer ?? new Pkcs7Signer(SigningCertificates.Default));
        return output.ToArray();
    }
}
