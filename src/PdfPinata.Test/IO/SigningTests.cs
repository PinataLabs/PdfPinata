using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   Signing was declared and not implemented: <c>PdfSignatureField</c> named every key a signature
///   dictionary has and nothing ever wrote one. These tests drive the whole way round — build a
///   document, sign it, and check the signature back out of the bytes that were written.
///
///   Two properties matter more than the rest, and both are easy to get wrong in a way that still
///   produces a file that opens. The signature has to <em>verify over the bytes it covers</em>, which
///   catches a byte range computed against the wrong offsets. And it has to <em>cover the whole
///   document</em>, which catches the subtler failure: a signature over part of a file verifies
///   perfectly and proves nothing about the rest of it.
/// </summary>
public class SigningTests
{
    [Fact]
    public void ASignedDocumentStillOpens()
    {
        var signed = Sign(Unsigned());

        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);

        document.PageCount.Should().Be(1);
    }

    [Fact]
    public void TheSignedFileBeginsWithTheDocumentThatWasSigned()
    {
        // Signing appends a revision. Rewriting the file instead would invalidate any signature
        // already on it, so the original bytes surviving byte for byte is the property everything
        // else here rests on.
        var unsigned = Unsigned();

        var signed = Sign(unsigned);

        signed.Length.Should().BeGreaterThan(unsigned.Length);
        signed.Take(unsigned.Length).Should().Equal(unsigned);
    }

    [Fact]
    public void TheDocumentSaysItIsSigned()
    {
        var document = Reader.Open(new MemoryStream(Sign(Unsigned())), PdfDocumentOpenMode.ReadOnly);

        var signatures = PdfSignatures.InDocument(document);

        signatures.Should().ContainSingle();
        signatures[0].FieldName.Should().Be("Signature1");
        signatures[0].SubFilter.Should().Be("/ETSI.CAdES.detached");
    }

    [Fact]
    public void TheSignatureVerifies()
    {
        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned())).Single();

        verification.Problem.Should().BeNull();
        verification.IsIntact.Should().BeTrue();
        verification.CoversWholeDocument.Should().BeTrue();
        verification.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TheSignatureNamesTheCertificateItWasMadeWith()
    {
        var verification = PdfSignatureVerifier.Verify(Sign(Unsigned())).Single();

        verification.SignerCertificate.Should().NotBeNull();
        verification.SignerCertificate!.Subject.Should().Contain("PdfPinata Test Signer");
    }

    [Fact]
    public void ChangingTheDocumentAfterSigningBreaksTheSignature()
    {
        // A marker put into the info dictionary as a plain string, so it can be found in the file
        // and altered without disturbing anything structural — which is exactly the edit a
        // signature exists to catch. Set through Elements rather than through Info.Title, because
        // Title forces UTF-16 and the marker would go into the file as two bytes per character.
        var signed = Sign(Unsigned(document =>
            document.Info.Elements["/Keywords"] = new PdfString("TAMPERTARGET")));

        var at = IndexOf(signed, "TAMPERTARGET");
        at.Should().BeGreaterThan(-1, "the title has to be findable for this test to be testing anything");
        signed[at] = (byte)'X';

        var verification = PdfSignatureVerifier.Verify(signed).Single();

        verification.IsIntact.Should().BeFalse();
        verification.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AppendingToASignedDocumentShowsUpAsNotCoveringIt()
    {
        // The other half of the check, and the one a naive verifier misses. The signature itself is
        // untouched and still verifies; what has changed is that it no longer accounts for the whole
        // file, which is how a document gets material added to it after signing.
        var signed = Sign(Unsigned());
        var extended = signed.Concat(new byte[64]).ToArray();

        var verification = PdfSignatureVerifier.Verify(extended).Single();

        verification.IsIntact.Should().BeTrue();
        verification.CoversWholeDocument.Should().BeFalse();
        verification.IsValid.Should().BeFalse();
    }

    [Fact]
    public void TheByteRangeSkipsTheSignatureAndNothingElse()
    {
        var signed = Sign(Unsigned());
        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);

        var range = PdfSignatures.InDocument(document).Single().ByteRange;

        range.Should().HaveCount(4);
        range[0].Should().Be(0);

        // The hole starts at the opening angle bracket of the hexadecimal string and ends after its
        // closing one, and the two spans meet at those brackets with nothing in between them.
        (range[0] + range[1]).Should().Be(range[0] + range[1]);
        signed[range[1]].Should().Be((byte)'<');
        signed[range[2] - 1].Should().Be((byte)'>');
        (range[2] + range[3]).Should().Be(signed.Length);
    }

    [Fact]
    public void WhatTheSignerSaidAboutItselfComesBackOut()
    {
        var signed = Sign(Unsigned(), new PdfSignatureOptions
        {
            FieldName = "Approval",
            SignerName = "A. Signer",
            Reason = "I approve this document",
            Location = "Cologne",
            ContactInfo = "signer@example.com",
            SigningTime = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Local)
        });

        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);
        var signature = PdfSignatures.InDocument(document).Single();

        signature.FieldName.Should().Be("Approval");
        signature.SignerName.Should().Be("A. Signer");
        signature.Reason.Should().Be("I approve this document");
        signature.Location.Should().Be("Cologne");
        signature.ContactInfo.Should().Be("signer@example.com");

        // A PDF date carries its offset from UTC and the reader normalises to UTC, so the instant
        // survives and the way it is written down does not. Comparing instants is what is being
        // claimed here.
        signature.SigningTime!.Value.ToUniversalTime()
            .Should().Be(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Local).ToUniversalTime());
    }

    [Fact]
    public void AnInvisibleSignatureIsAFieldWithNothingToShow()
    {
        var signed = Sign(Unsigned());
        var page = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly).Pages[0];

        var widget = Widgets(page).Single();

        widget.Elements.GetRectangle("/Rect").Width.Should().Be(0);
        widget.Elements.ContainsKey("/AP").Should().BeFalse();
    }

    [Fact]
    public void AVisibleSignatureIsDrawnIntoTheFieldItOccupies()
    {
        var drawn = false;
        var signed = Sign(Unsigned(), new PdfSignatureOptions
        {
            Rectangle = new XRect(40, 60, 200, 50),
            DrawAppearance = (gfx, box) =>
            {
                drawn = true;
                gfx.DrawRectangle(XBrushes.LightGray, box);
                gfx.DrawString("Signed", new XFont("Arial", 10), XBrushes.Black, 4, 20);
            }
        });

        drawn.Should().BeTrue();

        var page = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly).Pages[0];
        var widget = Widgets(page).Single();

        var rect = widget.Elements.GetRectangle("/Rect");
        rect.Width.Should().BeApproximately(200, 0.01);
        rect.Height.Should().BeApproximately(50, 0.01);

        // The caller measures downwards from the top of the page and a PDF rectangle measures
        // upwards from the bottom, so a box 60 down on an A4 page has its top edge at 842 - 60.
        rect.Y2.Should().BeApproximately(842 - 60, 0.5);

        widget.Elements.GetDictionary("/AP")?.Elements.GetDictionary("/N").Should().NotBeNull();
    }

    [Fact]
    public void AVisibleSignatureStillVerifies()
    {
        var signed = Sign(Unsigned(), new PdfSignatureOptions
        {
            Rectangle = new XRect(40, 60, 200, 50),
            DrawAppearance = (gfx, box) => gfx.DrawRectangle(XPens.Black, box)
        });

        PdfSignatureVerifier.Verify(signed).Single().IsValid.Should().BeTrue();
    }

    [Fact]
    public void TheFieldGoesOnThePageItWasAskedFor()
    {
        var signed = Sign(TwoPages(), new PdfSignatureOptions { PageIndex = 1 });
        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);

        Widgets(document.Pages[0]).Should().BeEmpty();
        Widgets(document.Pages[1]).Should().ContainSingle();
    }

    [Fact]
    public void TheFormSaysTheDocumentMustOnlyBeAppendedTo()
    {
        var signed = Sign(Unsigned());
        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);

        var form = document.Internals.Catalog.Elements.GetDictionary("/AcroForm");

        // 1 is SignaturesExist and 2 is AppendOnly. The second is the promise a signature depends
        // on: this file may be added to and must never be rewritten.
        form.Elements.GetInteger("/SigFlags").Should().Be(3);
    }

    [Fact]
    public void ACertifyingSignatureSaysWhatMayStillBeChanged()
    {
        var signed = Sign(Unsigned(), new PdfSignatureOptions
        {
            Certification = PdfCertificationLevel.FormFillingAllowed
        });

        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);

        PdfSignatures.InDocument(document).Single().CertificationLevel.Should().Be(2);

        // And the catalog has to point at it, or a reader never finds out the document is certified.
        document.Internals.Catalog.Elements.GetDictionary("/Perms").Elements.ContainsKey("/DocMDP")
            .Should().BeTrue();
    }

    [Fact]
    public void AnApprovalSignatureCertifiesNothing()
    {
        var document = Reader.Open(new MemoryStream(Sign(Unsigned())), PdfDocumentOpenMode.ReadOnly);

        PdfSignatures.InDocument(document).Single().CertificationLevel.Should().Be(0);
        document.Internals.Catalog.Elements.ContainsKey("/Perms").Should().BeFalse();
    }

    [Fact]
    public void SigningTwiceLeavesTheFirstSignatureIntact()
    {
        // The reason signing appends rather than rewrites. Both signatures survive; the first no
        // longer covers the whole file, because the second revision came after it, and that is what
        // a reader expects to see rather than a failure.
        var once = Sign(Unsigned());
        var twice = Sign(once, new PdfSignatureOptions { FieldName = "Signature2" });

        var verifications = PdfSignatureVerifier.Verify(twice);

        verifications.Should().HaveCount(2);
        verifications.Should().OnlyContain(verification => verification.IsIntact);
        verifications.Count(verification => verification.CoversWholeDocument).Should().Be(1);
    }

    [Fact]
    public void TheOlderSignatureIsTheOneThatNoLongerCoversTheFile()
    {
        var twice = Sign(Sign(Unsigned()), new PdfSignatureOptions { FieldName = "Signature2" });

        var verifications = PdfSignatureVerifier.Verify(twice);

        var latest = verifications.Single(verification => verification.CoversWholeDocument);
        latest.Signature.FieldName.Should().Be("Signature2");
    }

    [Fact]
    public void TheOlderFormatIsAvailableForReadersThatNeedIt()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default, PdfSignatureFormat.Pkcs7);

        var signed = Sign(Unsigned(), signer: signer);
        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);

        PdfSignatures.InDocument(document).Single().SubFilter.Should().Be("/adbe.pkcs7.detached");
        PdfSignatureVerifier.Verify(signed).Single().IsValid.Should().BeTrue();
    }

    [Fact]
    public void AStrongerDigestCanBeAskedFor()
    {
        var signer = new Pkcs7Signer(SigningCertificates.Default,
            hashAlgorithm: System.Security.Cryptography.HashAlgorithmName.SHA512);

        PdfSignatureVerifier.Verify(Sign(Unsigned(), signer: signer)).Single().IsValid.Should().BeTrue();
    }

    [Fact]
    public void ABrokenDigestIsRefused()
    {
        var making = () => new Pkcs7Signer(SigningCertificates.Default,
            hashAlgorithm: System.Security.Cryptography.HashAlgorithmName.SHA1);

        making.Should().Throw<ArgumentException>().WithMessage("*SHA-256*");
    }

    [Fact]
    public void ASignerThatReservesTooLittleRoomIsToldSo()
    {
        // The room has to be committed to before the signature exists, so getting it wrong can only
        // be found out afterwards. What matters is that it is an exception naming the cause rather
        // than a truncated signature in a file that opens.
        var signing = () => Sign(Unsigned(), signer: new CrampedSigner(SigningCertificates.Default));

        signing.Should().Throw<InvalidOperationException>()
            .WithMessage("*EstimatedSignatureSize*");
    }

    [Fact]
    public void ADocumentThatWasNotOpenedForAppendingCannotBeSigned()
    {
        var document = new PdfDocument();
        _ = document.AddPage();

        var signing = () => PdfSigner.Sign(document, new MemoryStream(),
            new Pkcs7Signer(SigningCertificates.Default));

        signing.Should().Throw<InvalidOperationException>().WithMessage("*Append*");
    }

    [Fact]
    public void SigningAPageThatIsNotThereIsRefused()
    {
        var signing = () => Sign(Unsigned(), new PdfSignatureOptions { PageIndex = 7 });

        signing.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AByteRangeThatOverflowsIsReportedRatherThanThrown()
    {
        // The numbers come out of a file nobody here wrote. An offset and a length that each pass
        // their own bound can still sum past int.MaxValue and wrap negative, which slipped through
        // the check, sized an allocation wrongly and threw out of the whole of Verify — one
        // malformed signature taking every other signature in the document with it.
        var signed = Sign(Unsigned());
        var corrupted = WithByteRange(signed, "[0 1000 1000 2147483647]");

        var verifying = () => PdfSignatureVerifier.Verify(corrupted);

        var verifications = verifying.Should().NotThrow().Which;
        verifications.Should().ContainSingle();
        verifications[0].IsValid.Should().BeFalse();
        verifications[0].Problem.Should().NotBeNull();
    }

    [Fact]
    public void APadesSignatureLeavesTheClaimedTimeToTheSignatureDictionary()
    {
        // PAdES carries the claimed signing time in /M, and the ETSI profiles have said since
        // TS 102 778 that the CMS signing-time attribute should not be there as well. Two claimed
        // times that can disagree help nobody.
        new Pkcs7Signer(SigningCertificates.Default).IncludeSigningTime.Should().BeFalse();
        new Pkcs7Signer(SigningCertificates.Default, PdfSignatureFormat.Pkcs7)
            .IncludeSigningTime.Should().BeTrue();
    }

    [Fact]
    public void ADocumentWithNoSignatureReportsNone()
    {
        var document = Reader.Open(new MemoryStream(Unsigned()), PdfDocumentOpenMode.ReadOnly);

        PdfSignatures.InDocument(document).Should().BeEmpty();
        PdfSignatureVerifier.Verify(Unsigned()).Should().BeEmpty();
    }

    /// <summary>
    ///   Reserves far less room than a CMS signature needs, so that the failure can be seen.
    /// </summary>
    private sealed class CrampedSigner : IPdfSigner
    {
        private readonly Pkcs7Signer _inner;

        public CrampedSigner(System.Security.Cryptography.X509Certificates.X509Certificate2 certificate) =>
            _inner = new Pkcs7Signer(certificate);

        public string SubFilter => _inner.SubFilter;

        public int EstimatedSignatureSize => 64;

        public byte[] Sign(Stream content) => _inner.Sign(content);
    }

    private static byte[] Unsigned(Action<PdfDocument> customise = null)
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawString("A document to sign", new XFont("Arial", 12), XBrushes.Black, 40, 100);

        customise?.Invoke(document);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] TwoPages()
    {
        var document = new PdfDocument();
        _ = document.AddPage();
        _ = document.AddPage();

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] Sign(byte[] document, PdfSignatureOptions options = null, IPdfSigner signer = null)
    {
        using var input = new MemoryStream(document);
        using var output = new MemoryStream();

        PdfSigner.Sign(input, output, signer ?? new Pkcs7Signer(SigningCertificates.Default), options);
        return output.ToArray();
    }

    /// <summary>
    ///   The signature widgets on a page, which is where a signature field shows itself.
    /// </summary>
    private static PdfDictionary[] Widgets(PdfPage page)
    {
        var annotations = page.Elements.GetArray("/Annots");
        if (annotations == null)
            return [];

        return [..Enumerable.Range(0, annotations.Elements.Count)
            .Select(index => annotations.Elements.GetDictionary(index))
            .Where(annotation => annotation != null && annotation.Elements.GetName("/FT") == "/Sig")];
    }

    /// <summary>
    ///   Writes a different byte range over the one in a signed file, keeping the file the same
    ///   length by padding — which is what makes it a malformed document rather than a shifted one.
    /// </summary>
    private static byte[] WithByteRange(byte[] signed, string replacement)
    {
        // Found through /ByteRange rather than by looking for an array that starts with zero — a
        // document has plenty of those, and the first one is not this one.
        var key = IndexOf(signed, "/ByteRange");
        key.Should().BeGreaterThan(-1);

        var at = Array.IndexOf(signed, (byte)'[', key);
        var end = Array.IndexOf(signed, (byte)']', at);
        var room = end - at + 1;

        replacement.Length.Should().BeLessThanOrEqualTo(room,
            "the replacement has to fit the space the original occupied");

        var corrupted = (byte[])signed.Clone();
        for (var index = 0; index < room; index++)
            corrupted[at + index] = (byte)(index < replacement.Length ? replacement[index] : ' ');

        return corrupted;
    }

    private static int IndexOf(byte[] haystack, string needle)
    {
        for (var at = 0; at <= haystack.Length - needle.Length; at++)
        {
            var index = 0;
            while (index < needle.Length && haystack[at + index] == (byte)needle[index])
                index++;

            if (index == needle.Length)
                return at;
        }

        return -1;
    }
}
