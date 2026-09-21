using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.AcroForms;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Signatures;
using PdfPinata.Signing;
using PdfPinata.Test.Helpers;
using Xunit;
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Forms;

/// <summary>
///   A signature field's <c>/Lock</c> and <c>/SV</c> - ISO 32000-1 Tables 233 to 235 - and the lock
///   <see cref="PdfSigner"/> honours when it is asked for one.
/// </summary>
public class SignatureFieldLockTests
{
    // ----- the lock dictionary --------------------------------------------------------------------------

    [Fact]
    public void ALockSurvivesTheFileAsAnIndirectObject()
    {
        var document = FormWithSignatureField(out var field);
        field.Lock = new PdfSignatureFieldLock(document, PdfFieldLockAction.Include, "name", "address.city");

        field.Elements.GetReference("/Lock").Should().NotBeNull("ISO 32000-1 requires it indirect");

        var read = SignatureField(ReadBack(document));
        read.Lock.Should().NotBeNull();
        read.Lock.Action.Should().Be(PdfFieldLockAction.Include);
        read.Lock.Fields.Should().Equal("name", "address.city");
        read.Lock.Elements.GetName("/Type").Should().Be("/SigFieldLock");
    }

    [Theory]
    [InlineData(PdfFieldLockAction.All, "anything", true)]
    [InlineData(PdfFieldLockAction.Include, "name", true)]
    [InlineData(PdfFieldLockAction.Include, "other", false)]
    [InlineData(PdfFieldLockAction.Exclude, "name", false)]
    [InlineData(PdfFieldLockAction.Exclude, "other", true)]
    public void ALockCoversTheFieldsItsActionSays(PdfFieldLockAction action, string field, bool covered)
    {
        var document = new PdfDocument();

        new PdfSignatureFieldLock(document, action, "name").Covers(field).Should().Be(covered);
    }

    [Theory]
    [InlineData(PdfFieldLockAction.Include, "address.street", true)]
    [InlineData(PdfFieldLockAction.Include, "address", true)]
    [InlineData(PdfFieldLockAction.Include, "addressee", false)]
    [InlineData(PdfFieldLockAction.Exclude, "address.city", false)]
    [InlineData(PdfFieldLockAction.Exclude, "addressee", true)]
    public void NamingAParentFieldCoversItsChildren(PdfFieldLockAction action, string field, bool covered)
    {
        new PdfSignatureFieldLock(new PdfDocument(), action, "address").Covers(field).Should().Be(covered);
    }

    [Fact]
    public void AnEmptyNameInASeedValueListIsRefused()
    {
        var seed = new PdfSignatureSeedValue(new PdfDocument());

        Action act = () => seed.SubFilters = new[] { "/adbe.pkcs7.detached", "" };

        act.Should().Throw<ArgumentException>();
        seed.SubFilters.Should().BeEmpty("nothing is written when a value is refused");
    }

    [Fact]
    public void AnAllLockWritesNoFieldList()
    {
        var @lock = new PdfSignatureFieldLock(new PdfDocument());

        @lock.Action.Should().Be(PdfFieldLockAction.All);
        @lock.Elements.ContainsKey("/Fields").Should().BeFalse();
    }

    [Fact]
    public void ALockFromAnotherDocumentIsRefused()
    {
        FormWithSignatureField(out var field);

        Action act = () => field.Lock = new PdfSignatureFieldLock(new PdfDocument());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemovingTheLockRemovesTheEntry()
    {
        var document = FormWithSignatureField(out var field);
        field.Lock = new PdfSignatureFieldLock(document);

        field.Lock = null;

        field.Elements.ContainsKey("/Lock").Should().BeFalse();
    }

    // ----- the seed value dictionary --------------------------------------------------------------------

    [Fact]
    public void ASeedValueSurvivesTheFile()
    {
        var document = FormWithSignatureField(out var field);
        byte[] certificate = { 0x30, 0x82, 0x00, 0xFF, 0x0A };
        field.SeedValue = new PdfSignatureSeedValue(document)
        {
            Flags = PdfSeedValueFlags.SubFilter | PdfSeedValueFlags.DigestMethod,
            Filter = "/Adobe.PPKLite",
            SubFilters = new[] { "/ETSI.CAdES.detached", "adbe.pkcs7.detached" },
            DigestMethods = new[] { "/SHA256" },
            Reasons = new[] { "I approve", "I have reviewed" },
            LegalAttestations = new[] { "No JavaScript" },
            Version = 2,
            AddRevocationInfo = true,
            CertificationLevel = PdfCertificationLevel.FormFillingAllowed,
            TimeStampUrl = "https://tsa.example.invalid/",
            Certificate = new PdfCertificateSeedValue
            {
                Flags = PdfCertificateSeedValueFlags.Issuer | PdfCertificateSeedValueFlags.Oid,
                Issuers = new[] { certificate },
                PolicyOids = new[] { "2.16.840.1.101.3.2.1.3.7" },
                KeyUsages = new[] { "1X" },
                Url = "https://ca.example.invalid/",
            },
        };

        field.Elements.GetReference("/SV").Should().NotBeNull("ISO 32000-1 requires it indirect");

        var read = SignatureField(ReadBack(document)).SeedValue;

        read.Flags.Should().Be(PdfSeedValueFlags.SubFilter | PdfSeedValueFlags.DigestMethod);
        read.Filter.Should().Be("/Adobe.PPKLite");
        read.SubFilters.Should().Equal("/ETSI.CAdES.detached", "/adbe.pkcs7.detached");
        read.DigestMethods.Should().Equal("/SHA256");
        read.Reasons.Should().Equal("I approve", "I have reviewed");
        read.LegalAttestations.Should().Equal("No JavaScript");
        read.Version.Should().Be(2);
        read.AddRevocationInfo.Should().BeTrue();
        read.CertificationLevel.Should().Be(PdfCertificationLevel.FormFillingAllowed);
        read.TimeStampUrl.Should().Be("https://tsa.example.invalid/");

        var cert = read.Certificate;
        cert.Flags.Should().Be(PdfCertificateSeedValueFlags.Issuer | PdfCertificateSeedValueFlags.Oid);
        cert.Issuers.Should().ContainSingle().Which.Should().Equal(certificate);
        cert.PolicyOids.Should().Equal("2.16.840.1.101.3.2.1.3.7");
        cert.KeyUsages.Should().Equal("1X");
        cert.Url.Should().Be("https://ca.example.invalid/");
    }

    [Fact]
    public void AnEmptySeedValueSaysNothing()
    {
        var document = FormWithSignatureField(out var field);
        field.SeedValue = new PdfSignatureSeedValue(document);

        var read = SignatureField(ReadBack(document)).SeedValue;

        read.Flags.Should().Be(PdfSeedValueFlags.None);
        read.Filter.Should().BeNull();
        read.SubFilters.Should().BeEmpty();
        read.Version.Should().BeNull();
        read.CertificationLevel.Should().BeNull();
        read.Certificate.Should().BeNull();
    }

    // ----- honoured by the signer -----------------------------------------------------------------------

    [Fact]
    public void SigningWithALockMakesTheFieldsItCoversReadOnly()
    {
        var signed = Sign(TwoFieldForm(), new PdfSignatureOptions
        {
            LockAction = PdfFieldLockAction.Include,
            LockFields = new[] { "name" },
        });

        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.Modify);
        var form = document.AcroForm;

        form.Fields["name"].ReadOnly.Should().BeTrue();
        form.Fields["comment"].ReadOnly.Should().BeFalse();

        var field = (PdfSignatureField)form.Fields["Signature1"];
        field.ReadOnly.Should().BeFalse("a signature never locks its own field");
        field.Lock.Action.Should().Be(PdfFieldLockAction.Include);
        field.Lock.Fields.Should().Equal("name");

        Action fill = () => form.Fields["name"].Value = new PdfString("changed");
        fill.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AnAllLockCoversEveryOtherField()
    {
        var signed = Sign(TwoFieldForm(), new PdfSignatureOptions { LockAction = PdfFieldLockAction.All });

        var form = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.Modify).AcroForm;

        form.Fields["name"].ReadOnly.Should().BeTrue();
        form.Fields["comment"].ReadOnly.Should().BeTrue();
    }

    [Fact]
    public void TheSignatureCarriesAFieldMdpReferenceBesideItsCertification()
    {
        var signed = Sign(TwoFieldForm(), new PdfSignatureOptions
        {
            Certification = PdfCertificationLevel.FormFillingAllowed,
            LockAction = PdfFieldLockAction.Exclude,
            LockFields = new[] { "comment" },
        });

        var document = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.ReadOnly);
        var signature = document.AcroForm.Fields["Signature1"].Elements.GetDictionary("/V");
        var references = signature.Elements.GetArray("/Reference");

        references.Elements.Count.Should().Be(2);
        references.Elements.GetDictionary(0).Elements.GetName("/TransformMethod").Should().Be("/DocMDP");

        var fieldMdp = references.Elements.GetDictionary(1);
        fieldMdp.Elements.GetName("/TransformMethod").Should().Be("/FieldMDP");
        var parameters = fieldMdp.Elements.GetDictionary("/TransformParams");
        parameters.Elements.GetName("/Action").Should().Be("/Exclude");
        parameters.Elements.GetArray("/Fields").Elements.GetString(0).Should().Be("comment");

        PdfSignatures.InDocument(document).Single().CertificationLevel
            .Should().Be((int)PdfCertificationLevel.FormFillingAllowed, "the DocMDP reference is still found");
    }

    [Fact]
    public void ALockedSignatureStillVerifies()
    {
        var signed = Sign(TwoFieldForm(), new PdfSignatureOptions { LockAction = PdfFieldLockAction.All });

        var verification = PdfSignatureVerifier.Verify(signed).Single();

        verification.IsValid.Should().BeTrue();
        verification.CoversWholeDocument.Should().BeTrue();
    }

    [Fact]
    public void SigningWithoutALockLocksNothing()
    {
        var signed = Sign(TwoFieldForm(), new PdfSignatureOptions());

        var form = Reader.Open(new MemoryStream(signed), PdfDocumentOpenMode.Modify).AcroForm;

        form.Fields["name"].ReadOnly.Should().BeFalse();
        ((PdfSignatureField)form.Fields["Signature1"]).Lock.Should().BeNull();
    }

    // ----- helpers ------------------------------------------------------------------------------------

    static PdfDocument FormWithSignatureField(out PdfSignatureField field)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        field = new PdfSignatureField(document) { Name = "approval" };
        form.Fields.Add(field);
        field.AddWidget(page, new PdfRectangle(new XPoint(50, 50), new XPoint(250, 100)));
        return document;
    }

    static PdfSignatureField SignatureField(PdfDocument document) =>
        (PdfSignatureField)document.AcroForm.Fields["approval"];

    static byte[] TwoFieldForm()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var form = document.GetOrCreateAcroForm();
        foreach (var name in new[] { "name", "comment" })
        {
            var field = new PdfTextField(document) { Name = name };
            form.Fields.Add(field);
            field.AddWidget(page, new PdfRectangle(new XPoint(50, 700), new XPoint(250, 720)));
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    static PdfDocument ReadBack(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return Reader.Open(new MemoryStream(output.ToArray()), PdfDocumentOpenMode.Modify);
    }

    static byte[] Sign(byte[] document, PdfSignatureOptions options)
    {
        using var input = new MemoryStream(document);
        using var output = new MemoryStream();
        PdfSigner.Sign(input, output, new Pkcs7Signer(SigningCertificates.Default), options);
        return output.ToArray();
    }
}
