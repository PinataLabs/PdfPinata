using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   The signature widget's <c>/F</c> used to be a fixed <c>4</c>, and because the field is created
///   at the moment of signing, a caller had no point at which to change it that did not break the
///   signature (empira/PDFsharp#157). <see cref="PdfSignatureOptions.AnnotationFlags"/> is written
///   into the signed revision, so what it says is covered by the signature rather than laid over it.
/// </summary>
public class SignatureAnnotationFlagsTests
{
    [Fact]
    public void TheDefaultIsPrintAsItAlwaysWas()
    {
        new PdfSignatureOptions().AnnotationFlags.Should().Be(PdfAnnotationFlags.Print);

        var widget = Widget(Sign(Unsigned()));

        widget.Elements.GetInteger("/F").Should().Be(4);
    }

    [Theory]
    [InlineData(PdfAnnotationFlags.Print | PdfAnnotationFlags.Locked)]
    [InlineData(PdfAnnotationFlags.Print | PdfAnnotationFlags.ReadOnly | PdfAnnotationFlags.NoRotate)]
    [InlineData(PdfAnnotationFlags.Hidden)]
    [InlineData(PdfAnnotationFlags.NoView | PdfAnnotationFlags.Print)]
    public void TheFlagsAskedForAreWrittenAndTheSignatureStillVerifies(PdfAnnotationFlags flags)
    {
        var signed = Sign(Unsigned(), new PdfSignatureOptions { AnnotationFlags = flags });

        Widget(signed).Elements.GetInteger("/F").Should().Be((int)flags);

        var verification = PdfSignatureVerifier.Verify(signed).Single();
        verification.IsValid.Should().BeTrue();
        verification.CoversWholeDocument.Should().BeTrue();
    }

    [Fact]
    public void AVisibleSignatureCarriesItsFlagsToo()
    {
        const PdfAnnotationFlags flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.Locked;
        var signed = Sign(Unsigned(), new PdfSignatureOptions
        {
            Rectangle = new XRect(40, 200, 160, 50),
            DrawAppearance = (gfx, rect) => gfx.DrawRectangle(XPens.Black, rect),
            AnnotationFlags = flags
        });

        Widget(signed).Elements.GetInteger("/F").Should().Be((int)flags);
        PdfSignatureVerifier.Verify(signed).Single().IsValid.Should().BeTrue();
    }

    [Fact]
    public void NoFlagsLeavesTheEntryOutRatherThanWritingZero()
    {
        // Zero is /F's default (ISO 32000-1 Table 164), so writing it says nothing a reader would
        // not assume anyway.
        var signed = Sign(Unsigned(), new PdfSignatureOptions { AnnotationFlags = 0 });

        Widget(signed).Elements.ContainsKey("/F").Should().BeFalse();
        PdfSignatureVerifier.Verify(signed).Single().IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(PdfAnnotationFlags.Hidden | PdfAnnotationFlags.Print)]
    [InlineData(PdfAnnotationFlags.Invisible | PdfAnnotationFlags.Print)]
    [InlineData(PdfAnnotationFlags.NoView | PdfAnnotationFlags.Print)]
    [InlineData(PdfAnnotationFlags.ToggleNoView | PdfAnnotationFlags.Print)]
    [InlineData(PdfAnnotationFlags.Locked)]
    [InlineData((PdfAnnotationFlags)0)]
    public void ADocumentClaimingPdfAIsRefusedFlagsTheProfileForbids(PdfAnnotationFlags flags)
    {
        using var input = new MemoryStream(Unsigned());
        var document = Reader.Open(input, PdfDocumentOpenMode.Append);
        document.Info.Title = "A document to sign";
        document.Options.Conformance = PdfAConformance.PdfA2B;

        var signing = () => PdfSigner.Sign(document, new MemoryStream(),
            new Pkcs7Signer(SigningCertificates.Default),
            new PdfSignatureOptions { AnnotationFlags = flags });

        signing.Should().Throw<InvalidOperationException>().WithMessage("PdfA2B*signature widget*");
    }

    [Fact]
    public void ADocumentClaimingPdfAAcceptsFlagsThatKeepPrint()
    {
        using var input = new MemoryStream(Unsigned());
        var document = Reader.Open(input, PdfDocumentOpenMode.Append);
        document.Info.Title = "A document to sign";
        document.Options.Conformance = PdfAConformance.PdfA2B;

        const PdfAnnotationFlags flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.Locked;
        using var output = new MemoryStream();
        PdfSigner.Sign(document, output, new Pkcs7Signer(SigningCertificates.Default),
            new PdfSignatureOptions { AnnotationFlags = flags });

        var signed = output.ToArray();
        Widget(signed).Elements.GetInteger("/F").Should().Be((int)flags);
        PdfSignatureVerifier.Verify(signed).Single().IsValid.Should().BeTrue();
    }

    [Fact]
    public void PdfA1DoesNotNameToggleNoView()
    {
        // ToggleNoView is PDF 1.5, later than the PDF 1.4 that PDF/A-1 is built on, and ISO 19005-1
        // 6.5.3 forbids only Invisible, Hidden and NoView. Parts 2 and 3 add it.
        using var input = new MemoryStream(Unsigned());
        var document = Reader.Open(input, PdfDocumentOpenMode.Append);
        document.Info.Title = "A document to sign";
        document.Options.Conformance = PdfAConformance.PdfA1B;

        const PdfAnnotationFlags flags = PdfAnnotationFlags.Print | PdfAnnotationFlags.ToggleNoView;
        var signing = () => PdfSigner.Sign(document, new MemoryStream(),
            new Pkcs7Signer(SigningCertificates.Default),
            new PdfSignatureOptions { AnnotationFlags = flags });

        signing.Should().NotThrow();
    }

    private static byte[] Unsigned()
    {
        var document = new PdfDocument();
        using (var gfx = XGraphics.FromPdfPage(document.AddPage()))
            gfx.DrawString("A document to sign", new XFont("Arial", 12), XBrushes.Black, 40, 100);

        return Saved.Bytes(document);
    }

    private static byte[] Sign(byte[] document, PdfSignatureOptions options = null)
    {
        using var input = new MemoryStream(document);
        using var output = new MemoryStream();

        PdfSigner.Sign(input, output, new Pkcs7Signer(SigningCertificates.Default), options);
        return output.ToArray();
    }

    private static PdfDictionary Widget(byte[] signed)
    {
        var page = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly).Pages[0];
        var annotations = page.Elements.GetArray("/Annots");
        return Enumerable.Range(0, annotations.Elements.Count)
            .Select(index => annotations.Elements.GetDictionary(index))
            .Single(annotation => annotation != null && annotation.Elements.GetName("/FT") == "/Sig");
    }
}
