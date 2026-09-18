using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AwesomeAssertions;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Metadata;
using PdfPinata.Pdf.Security;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.IO;

/// <summary>
///   A document could not say what standard it claimed to meet. <c>/Metadata</c> was a key-name
///   constant on the catalog, on the page and on the image, with no writer behind any of them, and
///   there was no output intent and no notion of conformance — so PDF/A was out of reach, and with
///   it the hybrid e-invoice formats that are built on PDF/A-3.
///
///   <see cref="XmpMetadata"/> is the packet; <see cref="PdfDocumentOptions.Conformance"/> is the
///   claim, and it is enforced rather than merely stamped on.
/// </summary>
public class XmpMetadataTests
{
    private const string Title = ConformingDocument.Title;
    private static readonly byte[] SomeProfile = ConformingDocument.SomeProfile;

    private static readonly XNamespace PdfaSchema = "http://www.aiim.org/pdfa/ns/schema#";
    private static readonly XNamespace PdfaProperty = "http://www.aiim.org/pdfa/ns/property#";

    [Fact]
    public void ADocumentGetsNoMetadataPacketUnlessItAsksForOne()
    {
        var bytes = Save(document => { });

        Latin1(bytes).Should().NotContain("xpacket",
            "the packet is several hundred bytes and most documents have no use for it");
    }

    [Fact]
    public void AskingForMetadataWritesAPacketThatSaysWhatTheDocumentSays()
    {
        var bytes = Save(document =>
        {
            document.Options.WriteXmpMetadata = true;
            document.Info.Author = "Ada Lovelace";
        });

        var text = Latin1(bytes);
        text.Should().Contain("<?xpacket begin=");
        text.Should().Contain("<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">" + Title);
        text.Should().Contain("<rdf:li>Ada Lovelace</rdf:li>");
    }

    [Fact]
    public void ThePacketIsLeftUncompressedSoThatAScannerCanFindIt()
    {
        // The xpacket markers exist so a tool can find the metadata by reading the bytes without
        // parsing the PDF around them, and a compressed packet is invisible to one.
        var bytes = Save(document =>
        {
            document.Options.WriteXmpMetadata = true;
            document.Options.CompressContentStreams = true;
        });

        Latin1(bytes).Should().Contain("<x:xmpmeta");
    }

    [Fact]
    public void AConformanceClaimSaysWhichPartAndWhichLevel()
    {
        var bytes = Save(Conforming(PdfAConformance.PdfA3B));

        var text = Latin1(bytes);
        text.Should().Contain("<pdfaid:part>3</pdfaid:part>");
        text.Should().Contain("<pdfaid:conformance>B</pdfaid:conformance>");
    }

    [Theory]
    [InlineData(PdfAConformance.PdfA1B, "1")]
    [InlineData(PdfAConformance.PdfA2B, "2")]
    [InlineData(PdfAConformance.PdfA3B, "3")]
    public void EachProfileNamesItsOwnPartOfTheStandard(PdfAConformance conformance, string part)
    {
        var bytes = Save(Conforming(conformance));

        Latin1(bytes).Should().Contain("<pdfaid:part>" + part + "</pdfaid:part>");
    }

    [Fact]
    public void AConformingDocumentEmbedsItsOutputIntentProfile()
    {
        var bytes = Save(Conforming(PdfAConformance.PdfA3B));

        var text = Latin1(bytes);
        text.Should().Contain("/OutputIntent");
        text.Should().Contain("/GTS_PDFA1", "the subtype names the family and not the part, for every part");
        text.Should().Contain("/DestOutputProfile");
        text.Should().Contain("NOT-AN-ICC-PROFILE", "the profile is embedded rather than referenced by name");
    }

    [Fact]
    public void AnEncryptedDocumentMayNotClaimConformance()
    {
        var saving = () => Save(document =>
        {
            Conforming(PdfAConformance.PdfA2B)(document);
            document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
            document.SecuritySettings.OwnerPassword = "12343";
        });

        saving.Should().Throw<InvalidOperationException>().WithMessage("*may not be encrypted*");
    }

    [Fact]
    public void ADocumentWithNoTitleMayNotClaimConformance()
    {
        var saving = () => Save(document =>
        {
            Conforming(PdfAConformance.PdfA2B)(document);
            document.Info.Title = "";
        });

        saving.Should().Throw<InvalidOperationException>().WithMessage("*has to have a title*");
    }

    [Fact]
    public void AnRgbDocumentThatNamesNoProfileIsGivenTheOneThatDescribesIt()
    {
        // Colours written as RGB by a library nobody told otherwise are sRGB, so supplying it is a
        // description rather than a guess — and every caller claiming PDF/A used to have to go and
        // find a profile before the writer would save anything at all.
        var bytes = Save(document => document.Options.Conformance = PdfAConformance.PdfA2B);

        var text = Latin1(bytes);
        text.Should().Contain("/OutputIntent");
        text.Should().Contain("(sRGB IEC61966-2.1)", "the condition is known, so it is named");
        text.Should().Contain("acsp", "the profile itself is embedded, not merely referred to");
    }

    [Fact]
    public void WhatTheCallerSuppliedIsWhatGoesIn()
    {
        var bytes = Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.OutputIntentIccProfile = SomeProfile;
        });

        var text = Latin1(bytes);
        text.Should().Contain("NOT-AN-ICC-PROFILE");
        text.Should().Contain("(Custom)", "a caller who named no condition is not given one");
        text.Should().NotContain("sRGB IEC61966-2.1");
    }

    [Fact]
    public void ACallerWhoNamedAConditionKeepsTheirName()
    {
        // Said something specific, so this is not the place to argue with it — only the placeholder
        // nobody chose gives way to the condition the built-in profile describes.
        var bytes = Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.OutputIntentIdentifier = "Coated FOGRA39";
        });

        Latin1(bytes).Should().Contain("(Coated FOGRA39)").And.NotContain("(sRGB IEC61966-2.1)");
    }

    [Fact]
    public void ACmykDocumentWithNoProfileMayNotClaimConformance()
    {
        // The same four numbers are a different colour on every press, so there is nothing true to
        // supply and the message says what to do instead.
        var saving = () => Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.ColorMode = PdfColorMode.Cmyk;
        });

        saving.Should().Throw<InvalidOperationException>().WithMessage("*OutputIntentIccProfile*");
    }

    [Fact]
    public void AnUndefinedColourModeWithNoProfileMayNotClaimConformanceEither()
    {
        // Undefined writes every colour as the XColor gave it, so the document may hold RGB and
        // CMYK together and no one profile describes it.
        var saving = () => Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.ColorMode = PdfColorMode.Undefined;
        });

        saving.Should().Throw<InvalidOperationException>().WithMessage("*Undefined*");
    }

    [Fact]
    public void TheComponentCountComesFromTheProfileRatherThanFromTheColourMode()
    {
        // /N has to agree with the profile's own colour space, and the colour mode does not decide
        // that. A caller in Undefined mode — which writes each colour as the XColor gave it — who
        // supplies a CMYK profile was getting /N 3 over four-component data, which is a broken
        // output intent in a document whose whole point is that it validates.
        var bytes = Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.ColorMode = PdfColorMode.Undefined;
            document.Options.OutputIntentIccProfile = ProfileFor("CMYK");
        });

        Latin1(bytes).Should().Contain("/N 4");
    }

    [Fact]
    public void AGreyProfileSaysOneComponent()
    {
        var bytes = Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.OutputIntentIccProfile = ProfileFor("GRAY");
        });

        Latin1(bytes).Should().Contain("/N 1");
    }

    [Theory]
    [InlineData("CMY ", 3)]
    [InlineData("Luv ", 3)]
    // ICC.1:2010 Table 19's nCLR family for multi-channel devices: the leading character spells
    // the component count in hex, from '2CLR' (2) up through '9CLR' (9) and 'ACLR' (10) up through
    // 'FCLR' (15).
    [InlineData("2CLR", 2)]
    [InlineData("9CLR", 9)]
    [InlineData("ACLR", 10)]
    [InlineData("FCLR", 15)]
    public void AMultiChannelOrFixedThreeComponentProfileSaysItsOwnComponentCount(
        string space, int components)
    {
        var bytes = Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.OutputIntentIccProfile = ProfileFor(space);
        });

        Latin1(bytes).Should().Contain("/N " + components);
    }

    [Fact]
    public void AProfileTooShortToReadFallsBackToWhatTheColourModeImplies()
    {
        // Nothing in this library parses a profile, and these tests hand it legible stand-ins
        // rather than real ones — so an unreadable header must not throw.
        var bytes = Save(document =>
        {
            document.Options.Conformance = PdfAConformance.PdfA2B;
            document.Options.ColorMode = PdfColorMode.Cmyk;
            document.Options.OutputIntentIccProfile = SomeProfile;
        });

        Latin1(bytes).Should().Contain("/N 4");
    }

    [Fact]
    public void TheBuiltInProfileSaysThreeComponents()
    {
        var bytes = Save(document => document.Options.Conformance = PdfAConformance.PdfA2B);

        Latin1(bytes).Should().Contain("/N 3");
    }

    /// <summary>
    ///   A profile header real enough to be read: the <c>acsp</c> file signature where one belongs
    ///   and the given data colour space where one belongs. Not a profile a validator would accept,
    ///   which is the point — this is about what the writer reads out of the header.
    /// </summary>
    private static byte[] ProfileFor(string space)
    {
        var profile = new byte[128];
        Encoding.ASCII.GetBytes(space).CopyTo(profile, 16);
        Encoding.ASCII.GetBytes("acsp").CopyTo(profile, 36);
        return profile;
    }

    [Fact]
    public void TheProfileHandedOutIsACopy()
    {
        // An array is mutable, and a caller who edited a shared one would change what every later
        // document said about its colours.
        var first = PdfOutputIntents.SrgbProfile;
        first[0] = 0xFF;

        PdfOutputIntents.SrgbProfile.Should().NotEqual(first);
        PdfOutputIntents.SrgbProfile[36..40].Should().Equal(
            (byte)'a', (byte)'c', (byte)'s', (byte)'p');
    }

    [Fact]
    public void TheVersionIsRaisedToWhatTheClaimedProfileIsDefinedAgainst()
    {
        Latin1(Save(Conforming(PdfAConformance.PdfA1B))).Should().StartWith("%PDF-1.4");
        Latin1(Save(Conforming(PdfAConformance.PdfA2B))).Should().StartWith("%PDF-1.7");
    }

    [Fact]
    public void ADocumentAlreadyPastPdfOnePointFourMayNotClaimPdfA1()
    {
        // Raising a low version is not enough on its own: a document that has already asked for
        // something newer keeps the newer version, and would then carry a PDF/A-1 claim over a
        // header PDF/A-1 does not allow. Asking for a cross-reference stream is one way to get
        // there without meaning to.
        var saving = () => Save(document =>
        {
            Conforming(PdfAConformance.PdfA1B)(document);
            document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;
        });

        saving.Should().Throw<InvalidOperationException>().WithMessage("*PDF/A-1*");
    }

    // ── ClaimConformance: refused at the claim, where the checks can be settled that early ───────

    [Fact]
    public void ClaimingConformanceChecksTheTitleImmediately()
    {
        var document = new PdfDocument();

        var claiming = () => document.ClaimConformance(PdfAConformance.PdfA2B);

        claiming.Should().Throw<InvalidOperationException>().WithMessage("*has to have a title*");
        document.Options.Conformance.Should().Be(PdfAConformance.None,
            "a refused claim must not half-set what it refused to make");
    }

    [Fact]
    public void ClaimingConformanceChecksEncryptionImmediately()
    {
        var document = new PdfDocument();
        document.Info.Title = Title;
        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        document.SecuritySettings.OwnerPassword = "12343";

        var claiming = () => document.ClaimConformance(PdfAConformance.PdfA2B);

        claiming.Should().Throw<InvalidOperationException>().WithMessage("*may not be encrypted*");
    }

    [Fact]
    public void ClaimingConformanceChecksACmykColourModeImmediately()
    {
        var document = new PdfDocument();
        document.Info.Title = Title;
        document.Options.ColorMode = PdfColorMode.Cmyk;

        var claiming = () => document.ClaimConformance(PdfAConformance.PdfA2B);

        claiming.Should().Throw<InvalidOperationException>().WithMessage("*OutputIntentIccProfile*");
    }

    [Fact]
    public void ClaimingConformanceChecksAnUndefinedColourModeImmediately()
    {
        var document = new PdfDocument();
        document.Info.Title = Title;
        document.Options.ColorMode = PdfColorMode.Undefined;

        var claiming = () => document.ClaimConformance(PdfAConformance.PdfA2B);

        claiming.Should().Throw<InvalidOperationException>().WithMessage("*Undefined*");
    }

    [Fact]
    public void ClaimingPdfA1OverACrossReferenceStreamIsRefusedImmediately()
    {
        // The version a cross-reference stream implies is not raised until the document is
        // written, which used to be the reason this rule could only be asked at Save. Checked here
        // directly against the setting rather than against the version it would eventually raise.
        var document = new PdfDocument();
        document.Info.Title = Title;
        document.Options.CrossReferenceFormat = PdfCrossReferenceFormat.Stream;

        var claiming = () => document.ClaimConformance(PdfAConformance.PdfA1B);

        claiming.Should().Throw<InvalidOperationException>().WithMessage("*PDF/A-1*");
    }

    [Fact]
    public void ClaimingConformanceSucceedsWhenEverythingItCanCheckIsAlreadyTrue()
    {
        var document = new PdfDocument();
        document.Info.Title = Title;
        document.Options.OutputIntentIccProfile = SomeProfile;

        document.ClaimConformance(PdfAConformance.PdfA2B);

        document.Options.Conformance.Should().Be(PdfAConformance.PdfA2B);
    }

    [Fact]
    public void ARefusedClaimLeavesWhateverWasClaimedBeforeInPlace()
    {
        var document = new PdfDocument();
        document.Info.Title = Title;
        document.ClaimConformance(PdfAConformance.PdfA3B);

        document.SecuritySettings.DocumentSecurityLevel = PdfDocumentSecurityLevel.Encrypted128Bit;
        document.SecuritySettings.OwnerPassword = "12343";

        var claiming = () => document.ClaimConformance(PdfAConformance.PdfA2B);

        claiming.Should().Throw<InvalidOperationException>();
        document.Options.Conformance.Should().Be(PdfAConformance.PdfA3B,
            "a refused reclaim should not half-change what the document already claims");
    }

    [Fact]
    public void ClaimingConformanceDoesNotCheckForAnAttachmentAddedAfterwards()
    {
        // Attachments cannot be checked at the claim: a caller may legitimately claim PDF/A-3 and
        // attach a file afterwards, which is exactly what FacturXInvoice does. That rule is not
        // moved early — it stays at Save, checked against the document as it stands there.
        var document = new PdfDocument();
        document.AddPage();
        document.Info.Title = Title;

        document.ClaimConformance(PdfAConformance.PdfA1B);
        document.Attachments.Add("data.xml", Encoding.UTF8.GetBytes("<x/>"),
            PdfAFRelationship.Data, "Data", "text/xml");

        var saving = () =>
        {
            using var output = new MemoryStream();
            document.Save(output, false);
        };

        saving.Should().Throw<InvalidOperationException>().WithMessage("*embedded file*");
    }

    [Fact]
    public void TheHookCannotWithdrawTheConformanceClaim()
    {
        // The claim is what a validator reads to decide which rules to hold the file to, and
        // Options.Conformance is the one place that decides it. A hook able to clear it, or to set
        // one the document was never checked against, would be a way of writing a claim that
        // nothing stands behind.
        var bytes = Save(document =>
        {
            Conforming(PdfAConformance.PdfA2B)(document);
            document.CustomizeMetadata = metadata => metadata.Conformance = PdfAConformance.None;
        });

        Latin1(bytes).Should().Contain("pdfaid:part");
    }

    [Fact]
    public void TheHookCanAddASchemaTheLibraryKnowsNothingAbout()
    {
        // What a PDF/UA identifier or a ZUGFeRD extension schema would go in through.
        var bytes = Save(document =>
        {
            document.Options.WriteXmpMetadata = true;
            document.CustomizeMetadata = metadata => metadata.AdditionalDescriptions.Add(
                "<rdf:Description rdf:about=\"\" xmlns:zf=\"urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#\">"
                + "<zf:DocumentType>INVOICE</zf:DocumentType></rdf:Description>");
        });

        Latin1(bytes).Should().Contain("<zf:DocumentType>INVOICE</zf:DocumentType>");
    }

    // ── Declaring an extension schema ──────────────────────────────────────────────────────────

    [Fact]
    public void EveryDeclaredPropertyIsWrittenAndEveryWrittenPropertyIsDeclared()
    {
        XNamespace sample = "http://example.invalid/sample/1.0/";
        var metadata = new XmpMetadata();
        metadata.DeclareSchema(new XmpExtensionSchema(
            "Sample schema", sample.NamespaceName, "sample",
            new[]
            {
                new XmpSchemaProperty("One", "The first property", XmpPropertyCategory.Internal, "1"),
                new XmpSchemaProperty("Two", "The second property", XmpPropertyCategory.External, "2"),
            }));

        var packet = ParsePacket(metadata.Build());

        var declared = packet.Descendants(PdfaProperty + "name").Select(name => name.Value).ToList();
        var used = packet.Descendants()
            .Where(element => element.Name.Namespace == sample)
            .Select(element => element.Name.LocalName)
            .ToList();

        declared.Should().BeEquivalentTo(new[] { "One", "Two" });
        used.Should().BeEquivalentTo(declared);
    }

    [Fact]
    public void AQuotationMarkAndAnAmpersandInTheNamespaceDoNotBreakThePacket()
    {
        // The namespace URI lands in an attribute value, where a quotation mark ends the attribute
        // early and everything after it becomes markup — three escaped characters is enough for
        // element text and not for an attribute.
        const string odd = "urn:example:\"quoted\"&odd#";
        var metadata = new XmpMetadata();
        metadata.DeclareSchema(new XmpExtensionSchema(
            "Odd schema", odd, "odd",
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.Internal, "value") }));

        var packet = ParsePacket(metadata.Build());
        XNamespace oddNamespace = odd;

        packet.Descendants(PdfaSchema + "namespaceURI").Single().Value.Should().Be(odd);
        packet.Descendants(oddNamespace + "Note").Single().Value.Should().Be("value");
    }

    [Fact]
    public void APrefixThatIsNotAnXmlNameIsRefusedNamingTheValue()
    {
        // There is no escaping this one: the prefix becomes part of an element name and of a
        // namespace declaration, and neither is a place a character can be written as an entity.
        Action declaring = () => new XmpExtensionSchema(
            "Sample schema", "http://example.invalid/sample/1.0/", "not a name",
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.Internal, "value") });

        declaring.Should().Throw<InvalidOperationException>()
            .WithMessage("*Prefix*").WithMessage("*not a name*");
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("xmlns")]
    [InlineData("rdf")]
    [InlineData("RDF")]
    public void AReservedPrefixIsRefusedEvenThoughItIsAnXmlName(string prefix)
    {
        // Each of these is a valid NCName on its own, but XML Namespaces reserves 'xml' and 'xmlns',
        // and 'rdf' is already bound to the namespace rdf:Description and rdf:about are written in.
        Action declaring = () => new XmpExtensionSchema(
            "Sample schema", "http://example.invalid/sample/1.0/", prefix,
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.Internal, "value") });

        declaring.Should().Throw<InvalidOperationException>()
            .WithMessage("*Prefix*").WithMessage("*reserved*");
    }

    [Fact]
    public void APropertyNameThatIsNotAnXmlNameIsRefused()
    {
        // The name becomes part of an element name too, the same as the prefix — see
        // APrefixThatIsNotAnXmlNameIsRefusedNamingTheValue — and is checked the same way.
        Action declaring = () => new XmpSchemaProperty(
            "not a name", "A note", XmpPropertyCategory.Internal, "value");

        declaring.Should().Throw<InvalidOperationException>()
            .WithMessage("*Name*").WithMessage("*not a name*");
    }

    [Fact]
    public void TwoSchemasCanBeDeclaredInOnePacketAndBothAppear()
    {
        XNamespace first = "http://example.invalid/first/1.0/";
        XNamespace second = "http://example.invalid/second/1.0/";
        var metadata = new XmpMetadata();
        metadata.DeclareSchema(new XmpExtensionSchema(
            "First schema", first.NamespaceName, "first",
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.Internal, "one") }));
        metadata.DeclareSchema(new XmpExtensionSchema(
            "Second schema", second.NamespaceName, "second",
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.External, "two") }));

        var packet = ParsePacket(metadata.Build());

        packet.Descendants(first + "Note").Single().Value.Should().Be("one");
        packet.Descendants(second + "Note").Single().Value.Should().Be("two");
        packet.Descendants(PdfaSchema + "prefix").Select(prefix => prefix.Value)
            .Should().BeEquivalentTo(new[] { "first", "second" });
    }

    [Fact]
    public void ASchemaWithNoPropertiesIsRefused()
    {
        Action declaring = () => new XmpExtensionSchema(
            "Empty schema", "http://example.invalid/empty/1.0/", "empty",
            Array.Empty<XmpSchemaProperty>());

        declaring.Should().Throw<InvalidOperationException>().WithMessage("*no properties*");
    }

    [Fact]
    public void TwoSchemasSharingAPrefixAreRefused()
    {
        var metadata = new XmpMetadata();
        metadata.DeclareSchema(new XmpExtensionSchema(
            "First schema", "http://example.invalid/first/1.0/", "dup",
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.Internal, "one") }));

        Action declaringAgain = () => metadata.DeclareSchema(new XmpExtensionSchema(
            "Second schema", "http://example.invalid/second/1.0/", "dup",
            new[] { new XmpSchemaProperty("Note", "A note", XmpPropertyCategory.External, "two") }));

        declaringAgain.Should().Throw<InvalidOperationException>().WithMessage("*dup*");
    }

    [Fact]
    public void ADocumentUsingOnlyAdditionalDescriptionsWritesNoExtensionSchemaBlock()
    {
        // No schema was declared, so AppendExtensionSchemas has nothing to write — the same path
        // this packet always took, which is what keeps a document using only the hatch unchanged.
        var bytes = Save(document =>
        {
            document.Options.WriteXmpMetadata = true;
            document.CustomizeMetadata = metadata => metadata.AdditionalDescriptions.Add(
                "<rdf:Description rdf:about=\"\" xmlns:zf=\"urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#\">"
                + "<zf:DocumentType>INVOICE</zf:DocumentType></rdf:Description>");
        });

        Latin1(bytes).Should().NotContain("pdfaExtension");
    }

    /// <summary>
    ///   The packet <see cref="XmpMetadata.Build"/> returns, parsed. The leading processing
    ///   instruction and the trailing padding and closing instruction are not part of the
    ///   <c>x:xmpmeta</c> element, so they are cut away before parsing rather than fed to it.
    /// </summary>
    private static XDocument ParsePacket(byte[] bytes)
    {
        const string closing = "</x:xmpmeta>";

        var text = Encoding.UTF8.GetString(bytes);
        var start = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal);
        var end = text.IndexOf(closing, StringComparison.Ordinal);

        start.Should().BeGreaterThan(-1, "the packet should carry the xmpmeta root element");
        end.Should().BeGreaterThan(start);

        return XDocument.Parse(text.Substring(start, end + closing.Length - start));
    }

    [Fact]
    public void EveryRegisteredContributorSurvivesAlongsideTheOthers()
    {
        // CustomizeMetadata is a single assignable delegate — the wrong shape for something several
        // independent packages each want to add to. AddMetadataContributor has no such slot to lose:
        // every one registered is called, whatever order they were registered in and whatever else
        // the document already carries.
        var bytes = Save(document =>
        {
            document.Options.WriteXmpMetadata = true;
            document.AddMetadataContributor(metadata => metadata.AdditionalDescriptions.Add(
                "<rdf:Description rdf:about=\"\" xmlns:a=\"urn:example:a#\">"
                + "<a:One>first</a:One></rdf:Description>"));
            document.CustomizeMetadata = metadata => metadata.AdditionalDescriptions.Add(
                "<rdf:Description rdf:about=\"\" xmlns:b=\"urn:example:b#\">"
                + "<b:Two>second</b:Two></rdf:Description>");
            document.AddMetadataContributor(metadata => metadata.AdditionalDescriptions.Add(
                "<rdf:Description rdf:about=\"\" xmlns:c=\"urn:example:c#\">"
                + "<c:Three>third</c:Three></rdf:Description>"));
        });

        var text = Latin1(bytes);
        text.Should().Contain("<a:One>first</a:One>");
        text.Should().Contain("<b:Two>second</b:Two>");
        text.Should().Contain("<c:Three>third</c:Three>");
    }

    [Fact]
    public void ThePacketAndTheInformationDictionaryAgreeAboutTheTitle()
    {
        // A validator compares the two and complains when they differ, which is why the packet is
        // built from the dictionary at save time rather than kept beside it.
        var bytes = Save(document => document.Options.WriteXmpMetadata = true);

        using var saved = new MemoryStream(bytes);
        var reread = Reader.Open(saved, PdfDocumentOpenMode.Modify);

        reread.Info.Title.Should().Be(Title);
        Latin1(bytes).Should().Contain(">" + Title + "</rdf:li>");
    }

    [Fact]
    public void MarkupInAValueIsEscapedRatherThanWritten()
    {
        var bytes = Save(document =>
        {
            document.Options.WriteXmpMetadata = true;
            document.Info.Title = "Bolts & Nuts <Ltd>";
        });

        var text = Latin1(bytes);
        text.Should().Contain("Bolts &amp; Nuts &lt;Ltd&gt;");
        text.Should().NotContain("Bolts & Nuts <Ltd>", "unescaped markup would make the packet unparseable");
    }

    [Fact]
    public void ADocumentThatClaimsNothingIsUnchanged()
    {
        var bytes = Save(document => { });

        Latin1(bytes).Should().NotContain("/OutputIntent").And.NotContain("pdfaid");
    }

    [Fact]
    public void APacketCanBeBuiltWithoutADocumentToBuildItFrom()
    {
        var metadata = new XmpMetadata
        {
            Title = "Standalone",
            Conformance = PdfAConformance.PdfA1B,
        };

        var text = Encoding.UTF8.GetString(metadata.Build());

        text.Should().Contain("<pdfaid:part>1</pdfaid:part>");
        text.Should().Contain("Standalone");
        text.Should().EndWith("<?xpacket end=\"w\"?>\n");
    }

    /// <summary>Everything a document needs before it may claim a profile.</summary>
    private static Action<PdfDocument> Conforming(PdfAConformance conformance) =>
        ConformingDocument.Conforming(conformance);

    private static byte[] Save(Action<PdfDocument> arrange)
    {
        var document = new PdfDocument();
        document.AddPage();
        document.Info.Title = Title;

        arrange(document);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
