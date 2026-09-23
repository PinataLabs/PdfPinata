using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using AwesomeAssertions;
using PdfPinata.EInvoice;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

// This namespace has a PdfReader of its own, so the one that opens documents needs saying in full.
using Reader = PdfPinata.Pdf.IO.PdfReader;

namespace PdfPinata.Test.Pdfs;

/// <summary>
///   Everything a hybrid e-invoice needs of the PDF side was here — PDF/A-3, attachments,
///   <c>/AFRelationship</c>, an extensible XMP packet — and nothing put them together, so a caller
///   wanting a Factur-X invoice had to know that the file must be called <c>factur-x.xml</c>, relate
///   as <c>/Data</c>, and be described by an XMP extension schema whose four property names have to
///   match the four the packet then writes. Every one of those is silent when got wrong: the
///   document opens perfectly and the system it was sent to rejects it.
///
///   <see cref="FacturXInvoice"/> is the whole of that, and <see cref="FacturXInvoice.FindIn"/> is
///   the receiving half — which is the half the German mandate has asked for since January 2025.
/// </summary>
public class EInvoiceTests
{
    private const string InvoiceXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Invoice><ID>2026-0042</ID></Invoice>";

    private static readonly XNamespace PdfaSchema = "http://www.aiim.org/pdfa/ns/schema#";
    private static readonly XNamespace PdfaProperty = "http://www.aiim.org/pdfa/ns/property#";
    private static readonly XNamespace FacturX = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";

    // ── What gets attached ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheInvoiceIsAttachedTheWayTheStandardAsksForIt()
    {
        var document = Prepared();

        var specification = new FacturXInvoice(Xml()).AttachTo(document);

        specification.FileName.Should().Be("factur-x.xml");
        specification.Relationship.Should().Be(PdfAFRelationship.Data);
        specification.EmbeddedFile.MimeType.Should().Be("/text/xml");
        specification.EmbeddedFile.Stream.UnfilteredValue.Should().Equal(Xml());
    }

    [Fact]
    public void AttachingAnInvoiceClaimsTheOneArchivalProfileThatMayCarryOne()
    {
        var document = Prepared();

        new FacturXInvoice(Xml()).AttachTo(document);

        document.Options.Conformance.Should().Be(PdfAConformance.PdfA3B,
            "a Factur-X document is a PDF/A-3 document, and no other PDF/A profile may carry a file");
    }

    [Fact]
    public void AClaimThatCouldNotCarryAnInvoiceIsRefusedRatherThanPromoted()
    {
        var document = Prepared();
        document.Options.Conformance = PdfAConformance.PdfA1B;

        var attaching = () => new FacturXInvoice(Xml()).AttachTo(document);

        attaching.Should().Throw<InvalidOperationException>()
            .WithMessage("*PdfA3B*", "the message has to say what to set instead");

        document.Attachments.Count.Should().Be(0,
            "a refusal leaves the document as it was rather than half converted");
        document.Options.Conformance.Should().Be(PdfAConformance.PdfA1B);
    }

    [Fact]
    public void APriorPdfA3AClaimSurvivesAttachingTheInvoice()
    {
        // PDF/A-3a is PDF/A-3b plus a tagged structure tree, and it may carry a file for exactly
        // the reason PDF/A-3b may: attaching must neither refuse it, the way it refuses every part
        // but 3, nor silently downgrade it to the B level nobody asked for.
        var document = Prepared();
        document.Options.Conformance = PdfAConformance.PdfA3A;

        new FacturXInvoice(Xml()).AttachTo(document);

        document.Options.Conformance.Should().Be(PdfAConformance.PdfA3A,
            "a caller who asked for an accessible hybrid invoice keeps that claim");
    }

    [Fact]
    public void AnInvoiceNeedsSomeXmlToBe()
    {
        var withNothing = () => new FacturXInvoice(null);
        var withEmptiness = () => new FacturXInvoice([]);

        withNothing.Should().Throw<ArgumentNullException>();
        withEmptiness.Should().Throw<ArgumentException>();
    }

    // ── What the metadata says about it ─────────────────────────────────────────────────────────

    [Fact]
    public void TheExtensionSchemaDeclaresExactlyThePropertiesThePacketThenUses()
    {
        // The one rule that makes this worth a package. PDF/A holds every property in the packet to
        // a schema the file predefines or describes, so a property written but not declared — or
        // declared but never written — fails validation for the metadata rather than for the
        // invoice, which is a confusing way to be wrong.
        var packet = Packet(Save(new FacturXInvoice(Xml())));

        var declared = packet.Descendants(PdfaProperty + "name").Select(name => name.Value).ToList();
        var used = packet.Descendants()
            .Where(element => element.Name.Namespace == FacturX)
            .Select(element => element.Name.LocalName)
            .ToList();

        declared.Should().BeEquivalentTo(
            "DocumentType", "DocumentFileName", "Version", "ConformanceLevel");
        used.Should().BeEquivalentTo(declared);

        packet.Descendants(PdfaSchema + "namespaceURI").Single().Value
            .Should().Be(FacturX.NamespaceName);
        packet.Descendants(PdfaSchema + "prefix").Single().Value.Should().Be("fx");
    }

    [Theory]
    [InlineData(EInvoiceProfile.Minimum, "MINIMUM")]
    [InlineData(EInvoiceProfile.BasicWithoutLines, "BASIC WL")]
    [InlineData(EInvoiceProfile.Basic, "BASIC")]
    [InlineData(EInvoiceProfile.En16931, "EN 16931")]
    [InlineData(EInvoiceProfile.Extended, "EXTENDED")]
    [InlineData(EInvoiceProfile.XRechnung, "XRECHNUNG")]
    public void TheProfileIsSpelledTheWayAReceiverReadsIt(EInvoiceProfile profile, string expected)
    {
        // The spaces are the point. A document writing EN16931 or BASICWL passes every check that
        // looks at the PDF and is rejected by the system that reads the invoice.
        var packet = Packet(Save(new FacturXInvoice(Xml()) { Profile = profile }));

        packet.Descendants(FacturX + "ConformanceLevel").Single().Value.Should().Be(expected);
    }

    [Fact]
    public void TheDefaultProfileIsTheOneThePublicMandatesAreWrittenAgainst()
    {
        var packet = Packet(Save(new FacturXInvoice(Xml())));

        packet.Descendants(FacturX + "ConformanceLevel").Single().Value.Should().Be("EN 16931");
        packet.Descendants(FacturX + "DocumentType").Single().Value.Should().Be("INVOICE");
        packet.Descendants(FacturX + "Version").Single().Value.Should().Be("1.0");
    }

    [Fact]
    public void TheNameTheMetadataGivesIsTheNameTheFileWasAttachedUnder()
    {
        // A receiver takes the attachment by the name the metadata names, so the two disagreeing is
        // a document describing an invoice it does not carry.
        var invoice = new FacturXInvoice(Xml()) { FileName = "zugferd-invoice.xml" };
        var packet = Packet(Save(invoice));

        packet.Descendants(FacturX + "DocumentFileName").Single().Value
            .Should().Be("zugferd-invoice.xml");
    }

    [Fact]
    public void AHookTheCallerAlreadySetSurvivesAlongsideTheInvoice()
    {
        // AttachTo contributes through AddMetadataContributor, which cannot replace whatever
        // CustomizeMetadata already held — the two are independent slots, both always invoked.
        var document = Prepared();
        document.CustomizeMetadata = metadata => metadata.AdditionalDescriptions.Add(
            "<rdf:Description rdf:about=\"\" xmlns:mine=\"urn:example:mine#\">"
            + "<mine:Note>kept</mine:Note></rdf:Description>");

        new FacturXInvoice(Xml()).AttachTo(document);

        var text = Latin1(Written(document));
        text.Should().Contain("<mine:Note>kept</mine:Note>");
        text.Should().Contain("Factur-X PDFA Extension Schema");
    }

    [Fact]
    public void AHookSetAfterAttachingTheInvoiceSurvivesJustAsWell()
    {
        // The bug this guards against: a single assignable CustomizeMetadata meant a caller who set
        // it after AttachTo silently dropped the extension schema and the four fx: properties — the
        // document still claimed PDF/A-3, still opened perfectly, and failed validation for its
        // metadata. AddMetadataContributor has no ordering to get wrong.
        var document = Prepared();

        new FacturXInvoice(Xml()).AttachTo(document);
        document.CustomizeMetadata = metadata => metadata.AdditionalDescriptions.Add(
            "<rdf:Description rdf:about=\"\" xmlns:mine=\"urn:example:mine#\">"
            + "<mine:Note>kept</mine:Note></rdf:Description>");

        var text = Latin1(Written(document));
        text.Should().Contain("<mine:Note>kept</mine:Note>");
        text.Should().Contain("Factur-X PDFA Extension Schema");
    }

    [Fact]
    public void AnAmpersandInWhatTheCallerNamedTheSchemaDoesNotBreakThePacket()
    {
        // The descriptions go into the packet verbatim, so one unescaped character makes the whole
        // of it unparseable rather than only the part of it that is wrong.
        var invoice = new FacturXInvoice(Xml()) { SchemaName = "Bolts & Nuts <Ltd> schema" };

        var packet = Packet(Save(invoice));

        packet.Descendants(PdfaSchema + "schema").Single().Value.Should().Be("Bolts & Nuts <Ltd> schema");
    }

    [Fact]
    public void AQuotationMarkInTheNamespaceDoesNotEndTheAttributeEarly()
    {
        // The namespace goes into an attribute value rather than into element text, where a
        // quotation mark closes the attribute and everything after it becomes markup. Escaping
        // three characters is enough for a value and not for an attribute.
        var invoice = new FacturXInvoice(Xml())
        {
            NamespaceUri = "urn:example:\"quoted\"&odd#"
        };

        var packet = Packet(Save(invoice));
        XNamespace odd = "urn:example:\"quoted\"&odd#";

        packet.Descendants(PdfaSchema + "namespaceURI").Single().Value
            .Should().Be("urn:example:\"quoted\"&odd#");
        packet.Descendants(odd + "DocumentType").Single().Value.Should().Be("INVOICE");
    }

    [Fact]
    public void APrefixThatIsNotAnXmlNameIsRefusedRatherThanWritten()
    {
        // There is no escaping this one: the prefix becomes part of an element name and of a
        // namespace declaration, and neither is a place a character can be written as an entity.
        // Writing it anyway produces a packet no parser will read.
        var document = Prepared();
        var invoice = new FacturXInvoice(Xml()) { Prefix = "not a name" };

        var attaching = () => invoice.AttachTo(document);

        attaching.Should().Throw<InvalidOperationException>().WithMessage("*Prefix*");
        document.Attachments.Count.Should().Be(0, "a refusal leaves the document as it was");
    }

    [Fact]
    public void APrefixThatIsAnXmlNameIsAccepted()
    {
        // The escape hatch has to keep working: ZUGFeRD 1.0 used zf, and a caller reaching for it
        // should not be stopped by the check that stops "not a name".
        var invoice = new FacturXInvoice(Xml())
        {
            Prefix = "zf",
            NamespaceUri = "urn:ferd:pdfa:CrossIndustryDocument:invoice:1p0#"
        };

        var packet = Packet(Save(invoice));
        XNamespace ferd = "urn:ferd:pdfa:CrossIndustryDocument:invoice:1p0#";

        packet.Descendants(PdfaSchema + "prefix").Single().Value.Should().Be("zf");
        packet.Descendants(ferd + "DocumentFileName").Single().Value.Should().Be("factur-x.xml");
    }

    // ── Reading one back ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AnInvoiceIsStillThereAfterBeingSavedAndOpenedAgain()
    {
        var bytes = Save(new FacturXInvoice(Xml()));

        using var saved = new MemoryStream(bytes);
        var reread = Reader.Open(saved, PdfDocumentOpenMode.Modify);

        FacturXInvoice.ReadFrom(reread).Should().Equal(Xml());
        FacturXInvoice.FindIn(reread).Relationship.Should().Be(PdfAFRelationship.Data);
    }

    [Fact]
    public void AnInvoiceIsFoundWhateverVersionOfTheStandardNamedIt()
    {
        // ZUGFeRD 1.0 capitalised its own file name, which is why the comparison ignores case.
        var document = Prepared();
        document.Attachments.Add("ZUGFeRD-invoice.xml", Xml(), PdfAFRelationship.Alternative,
            "The invoice as data", "text/xml");

        FacturXInvoice.FindIn(document).Should().NotBeNull();
        FacturXInvoice.ReadFrom(document).Should().Equal(Xml());
    }

    [Fact]
    public void ADocumentCarryingSomeOtherXmlIsNotCarryingAnInvoice()
    {
        // Found by name rather than by relationship and media type: every /Data attachment that is
        // text/xml would match that, and answering "here is your invoice" about a file that is not
        // one is worse than answering nothing.
        var document = Prepared();
        document.Attachments.Add("measurements.xml", Xml(), PdfAFRelationship.Data,
            "Data behind the chart", "text/xml");

        FacturXInvoice.FindIn(document).Should().BeNull();
        FacturXInvoice.ReadFrom(document).Should().BeNull();
    }

    [Fact]
    public void ADocumentCarryingNothingIsAskedWithoutBeingChanged()
    {
        var document = Prepared();

        FacturXInvoice.FindIn(document).Should().BeNull();

        Latin1(Written(document)).Should().NotContain("/AF").And.NotContain("/EmbeddedFiles");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///   A document with the two things a PDF/A claim will be held to at save time, so that a test
    ///   about invoicing fails for something about invoicing.
    /// </summary>
    private static PdfDocument Prepared() => ConformingDocument.Prepared();

    private static byte[] Xml() => Encoding.UTF8.GetBytes(InvoiceXml);

    private static byte[] Save(FacturXInvoice invoice)
    {
        var document = Prepared();
        invoice.AttachTo(document);
        return Written(document);
    }

    private static byte[] Written(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    /// <summary>
    ///   The XMP packet of a saved document, parsed. Located by scanning the bytes as Latin-1 —
    ///   which is one character per byte, so the positions are byte positions — and then decoded as
    ///   the UTF-8 the packet actually is.
    /// </summary>
    private static XDocument Packet(byte[] bytes)
    {
        const string closing = "</x:xmpmeta>";

        var text = Latin1(bytes);
        var start = text.IndexOf("<x:xmpmeta", StringComparison.Ordinal);
        var end = text.IndexOf(closing, StringComparison.Ordinal);

        start.Should().BeGreaterThan(-1, "the document should carry a metadata packet");
        end.Should().BeGreaterThan(start);

        return XDocument.Parse(Encoding.UTF8.GetString(bytes, start, end + closing.Length - start));
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);
}
