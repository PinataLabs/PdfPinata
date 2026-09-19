using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.Annotations;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using PdfPinata.Test.Pdfs.AcroForms;
using Xunit;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   A certifying signature's <c>/DocMDP</c> level, enforced. The library used to write the level and
///   honour none of it: the same document reopened could be changed however the caller liked. This is
///   the second dimension of the matrix <see cref="OpenModeEnforcementTests"/> already pins, because
///   the guard both refusals go through is now the same one — <c>PdfDocument.EnsureCanModify</c>
///   widened to ask not just "may this document be changed" but "may it take <em>this kind</em> of
///   change". A mode refusal and a certification refusal are asserted on their message as well as
///   their type, for the same reason the mode matrix is: the point of the change is that the refusal
///   names which of the two applies.
/// </summary>
public class CertificationEnforcementTests
{
    [Theory]
    [InlineData(PdfCertificationLevel.NoChangesAllowed)]
    [InlineData(PdfCertificationLevel.FormFillingAllowed)]
    [InlineData(PdfCertificationLevel.FormFillingAndAnnotationsAllowed)]
    public void DocumentStructureIsRefusedAtEveryCertificationLevel(PdfCertificationLevel level)
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), level));

        Action act = () => document.AddPage();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*PdfCertificationLevel.{level}*")
            .And.Message.Should().Contain("adding a page");
    }

    [Fact]
    public void NoChangesAllowedRefusesFillingAFieldToo()
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), PdfCertificationLevel.NoChangesAllowed));

        Action act = () => Field(document).Value = new PdfString("filled");

        act.Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
    }

    /// <summary>
    ///   Every field type that keeps its own value has to go through the same guard as a text
    ///   field's <c>Value</c> - a choice, radio or check box field writes <c>/V</c> a different way
    ///   and each has its own entry point, so each has its own place the guard could have been
    ///   missing from.
    /// </summary>
    [Fact]
    public void NoChangesAllowedRefusesEveryKindOfFieldValueSetter()
    {
        var document = OpenedForAppend(
            Certified(UnsignedWithOneOfEachFieldKind(), PdfCertificationLevel.NoChangesAllowed));

        var fields = document.Internals.Catalog.AcroForm.Fields;
        var checkBox = (PdfCheckBoxField)fields["Check1"];
        var radio = (PdfRadioButtonField)fields["Radio1"];
        var combo = (PdfComboBoxField)fields["Combo1"];
        var list = (PdfListBoxField)fields["List1"];

        checkBox.Should().BeOfType<PdfCheckBoxField>();
        radio.Should().BeOfType<PdfRadioButtonField>();
        combo.Should().BeOfType<PdfComboBoxField>();
        list.Should().BeOfType<PdfListBoxField>();

        ((Action)(() => checkBox.Checked = true))
            .Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
        ((Action)(() => radio.SelectedIndex = 0))
            .Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
        ((Action)(() => combo.SelectedIndex = 0))
            .Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
        ((Action)(() => combo.Value = new PdfString("A")))
            .Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
        ((Action)(() => list.SelectedIndices = new[] { 0 }))
            .Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
    }

    [Fact]
    public void NoChangesAllowedRefusesAddingAnAnnotationToo()
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), PdfCertificationLevel.NoChangesAllowed));

        Action act = () => document.Pages[0].Annotations.Add(new PdfTextAnnotation());

        act.Should().Throw<InvalidOperationException>().WithMessage("*NoChangesAllowed*");
    }

    [Fact]
    public void FormFillingAllowedPermitsFillingAFieldAndRefusesAnnotations()
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), PdfCertificationLevel.FormFillingAllowed));

        Action filling = () => Field(document).Value = new PdfString("filled");
        filling.Should().NotThrow();

        Action annotating = () => document.Pages[0].Annotations.Add(new PdfTextAnnotation());
        annotating.Should().Throw<InvalidOperationException>().WithMessage("*FormFillingAllowed*");
    }

    [Fact]
    public void FormFillingAllowedPermitsSigningAgain()
    {
        var certified = Certified(UnsignedWithAField(), PdfCertificationLevel.FormFillingAllowed);

        Action signingAgain = () => Sign(certified, new PdfSignatureOptions { FieldName = "Signature2" });

        signingAgain.Should().NotThrow();
    }

    /// <summary>
    ///   FormFillingAllowed lets an ordinary signature through, but a document can carry only one
    ///   certifying signature and it must be the first one applied — so asking to certify again, even
    ///   at a level that would itself permit the change, is refused rather than silently replacing the
    ///   certification a reader has already relied on.
    /// </summary>
    [Fact]
    public void CertifyingAnAlreadyCertifiedDocumentIsRefused()
    {
        var certified = Certified(UnsignedWithAField(), PdfCertificationLevel.FormFillingAllowed);

        Action reCertifying = () => Sign(certified, new PdfSignatureOptions
        {
            FieldName = "Signature2",
            Certification = PdfCertificationLevel.NoChangesAllowed
        });

        reCertifying.Should().Throw<InvalidOperationException>().WithMessage("*already certified*");
    }

    /// <summary>
    ///   <c>/P</c> is optional and defaults to 2 (FormFillingAllowed) when a certifying signature omits
    ///   it — <see cref="PdfSigner"/> always writes it explicitly, so this builds the dictionary by
    ///   hand the way an incomplete but genuine certification from another producer would look.
    /// </summary>
    [Fact]
    public void ACertifyingSignatureWithNoExplicitPDefaultsToFormFillingAllowed()
    {
        var document = OpenedForAppend(CertifiedWithNoExplicitP(UnsignedWithAField()));

        Action filling = () => Field(document).Value = new PdfString("filled");
        filling.Should().NotThrow();

        Action addingAPage = () => document.AddPage();
        addingAPage.Should().Throw<InvalidOperationException>()
            .WithMessage("*FormFillingAllowed*");
    }

    [Fact]
    public void FormFillingAndAnnotationsAllowedPermitsBothAndStillRefusesThePageTree()
    {
        var document = OpenedForAppend(
            Certified(UnsignedWithAField(), PdfCertificationLevel.FormFillingAndAnnotationsAllowed));

        Action filling = () => Field(document).Value = new PdfString("filled");
        filling.Should().NotThrow();

        Action annotating = () => document.Pages[0].Annotations.Add(new PdfTextAnnotation());
        annotating.Should().NotThrow();

        Action addingAPage = () => document.AddPage();
        addingAPage.Should().Throw<InvalidOperationException>()
            .WithMessage("*FormFillingAndAnnotationsAllowed*");
    }

    [Fact]
    public void ARefusalByCertificationNamesCertificationRatherThanTheMode()
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), PdfCertificationLevel.NoChangesAllowed));

        Action act = () => document.AddPage();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*certified*")
            .And.Message.Should().NotContain("PdfDocumentOpenMode");
    }

    [Fact]
    public void ADocumentThatFailsBothIsRefusedOnceByTheModeItWasOpenedWith()
    {
        var certified = Certified(UnsignedWithAField(), PdfCertificationLevel.NoChangesAllowed);
        var document = Reader.Open(new MemoryStream(certified), PdfDocumentOpenMode.ReadOnly);

        Action act = () => document.AddPage();

        // Refused once, for the mode - the more fundamental of the two reasons, and the one to fix
        // first. The certification refusal, worded differently, is never reached.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PdfDocumentOpenMode.ReadOnly*")
            .And.Message.Should().NotContain("PdfCertificationLevel");
    }

    [Fact]
    public void AFullSaveOfACertifiedDocumentIsRefused()
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), PdfCertificationLevel.FormFillingAllowed));

        Action act = () => document.Save(new MemoryStream(), false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*FormFillingAllowed*");
    }

    [Fact]
    public void AnIncrementalSaveOfAPermittedChangeToACertifiedDocumentSucceeds()
    {
        var document = OpenedForAppend(Certified(UnsignedWithAField(), PdfCertificationLevel.FormFillingAllowed));
        Field(document).Value = new PdfString("filled");

        Action act = () => document.SaveIncremental(new MemoryStream());

        act.Should().NotThrow();
    }

    [Fact]
    public void AnUnsignedDocumentIsUnaffectedAcrossTheWholeMatrix()
    {
        var document = OpenedForAppend(UnsignedWithAField());

        Field(document).Value = new PdfString("filled");
        document.Pages[0].Annotations.Add(new PdfTextAnnotation());
        document.AddPage();

        Action act = () => document.Save(new MemoryStream(), false);
        act.Should().NotThrow();
    }

    static PdfAcroField Field(PdfDocument document) =>
        document.Internals.Catalog.AcroForm.Fields[0];

    static byte[] UnsignedWithAField()
    {
        var opened = new AcroFormBuilder().With("/Tx", "Field1").Build();

        using var output = new MemoryStream();
        opened.Save(output, false);
        return output.ToArray();
    }

    static byte[] UnsignedWithOneOfEachFieldKind()
    {
        var opened = new AcroFormBuilder()
            .With("/Btn", "Check1")
            .With("/Btn", "Radio1", field =>
            {
                AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Radio);
                AcroFormBuilder.WithOptions(field, "A", "B");
            })
            .With("/Ch", "Combo1", field =>
            {
                AcroFormBuilder.WithFlags(field, PdfAcroFieldFlags.Combo);
                AcroFormBuilder.WithOptions(field, "A", "B");
            })
            .With("/Ch", "List1", field => AcroFormBuilder.WithOptions(field, "A", "B"))
            .Build();

        using var output = new MemoryStream();
        opened.Save(output, false);
        return output.ToArray();
    }

    static byte[] Certified(byte[] document, PdfCertificationLevel level) =>
        Sign(document, new PdfSignatureOptions { Certification = level });

    static byte[] CertifiedWithNoExplicitP(byte[] document)
    {
        // Appended rather than fully saved: setting /Perms/DocMDP below makes the in-memory document
        // certified before a byte is written, and a full Save is itself a document-structure change
        // that certification refuses. SaveIncremental is not gated the same way - it is what
        // PdfSigner itself writes the certifying revision with, for exactly this reason.
        var opened = Reader.Open(new MemoryStream(document), PdfDocumentOpenMode.Append);

        var parameters = new PdfDictionary(opened);
        parameters.Elements.SetName("/Type", "/TransformParams");
        parameters.Elements.SetName("/V", "/1.2");
        // Deliberately no /P: the case this test exists to cover.

        var reference = new PdfDictionary(opened);
        reference.Elements.SetName("/Type", "/SigRef");
        reference.Elements.SetName("/TransformMethod", "/DocMDP");
        reference.Elements["/TransformParams"] = parameters;

        var references = new PdfArray(opened);
        references.Elements.Add(reference);

        var signature = new PdfDictionary(opened);
        signature.Elements.SetName("/Type", "/Sig");
        signature.Elements["/Reference"] = references;
        opened.Internals.AddObject(signature);

        var permissions = new PdfDictionary(opened);
        permissions.Elements["/DocMDP"] = signature.Reference;
        opened.Internals.Catalog.Elements["/Perms"] = permissions;

        using var output = new MemoryStream();
        opened.SaveIncremental(output);
        return output.ToArray();
    }

    static byte[] Sign(byte[] document, PdfSignatureOptions options)
    {
        using var input = new MemoryStream(document);
        using var output = new MemoryStream();

        PdfSigner.Sign(input, output, new Pkcs7Signer(SigningCertificates.Default), options);
        return output.ToArray();
    }

    static PdfDocument OpenedForAppend(byte[] document) =>
        Reader.Open(new MemoryStream(document), PdfDocumentOpenMode.Append);
}
