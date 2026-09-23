using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Metadata;

namespace PdfPinata.EInvoice;

/// <summary>
/// An invoice as XML, ready to be attached to the PDF that shows the same invoice to a person.
/// </summary>
/// <remarks>
/// <para>
/// Factur-X — which is ZUGFeRD 2.1 and later, the same file format under two names — is a PDF/A-3
/// document carrying its own invoice as an attached UN/CEFACT CII XML file. A person opens the PDF
/// and reads it; a system opens the same file, takes the attachment, and books it without anybody
/// retyping anything. The two are one document, which is the whole idea and is what
/// <see cref="PdfAFRelationship.Data"/> says out loud.
/// </para>
/// <para>
/// What the PDF side of that standard asks for is small and exact, and getting any of it slightly
/// wrong produces a file that opens perfectly and is rejected by the system it was sent to: the
/// attachment has to be named <c>factur-x.xml</c>, relate to the document as <c>/Data</c>, say it is
/// <c>text/xml</c>, be associated with the document rather than merely carried inside it, and be
/// described in the XMP metadata by an extension schema that declares the properties the packet then
/// uses. <see cref="AttachTo"/> does all of it.
/// </para>
/// <para>
/// <b>Generating the XML is out of scope and always will be.</b> EN 16931 semantics, profile
/// validation and the country-specific business rules are a library of their own and a permanent
/// maintenance liability; hand this the bytes something else produced. Nothing here reads the XML —
/// <see cref="Profile"/> is a claim about it that only a schema validator can settle.
/// </para>
/// <para>
/// The defaults describe a Factur-X invoice. The four properties that name the schema —
/// <see cref="FileName"/>, <see cref="NamespaceUri"/>, <see cref="Prefix"/> and
/// <see cref="SchemaName"/> — are settable so that the older ZUGFeRD 1.0 layout and the Order-X
/// sibling are reachable by a caller who needs one. They are not what this package was written
/// against and nothing here has been validated in those shapes.
/// </para>
/// </remarks>
public sealed class FacturXInvoice
{
    /// <summary>
    /// The media type of the attachment. Fixed rather than settable: the standard names this one,
    /// and an invoice that says it is something else is not one.
    /// </summary>
    private const string InvoiceMimeType = "text/xml";

    /// <summary>
    /// The names an incoming hybrid invoice may be carried under, which is what <see cref="FindIn"/>
    /// looks for. Factur-X and ZUGFeRD 2.1 write the first; ZUGFeRD 1.0 and 2.0 wrote the second and
    /// the German profile the third; the fourth is the order sibling of the format.
    /// </summary>
    private static readonly string[] KnownFileNames =
    [
        "factur-x.xml",
        "zugferd-invoice.xml",
        "xrechnung.xml",
        "order-x.xml"
    ];

    /// <summary>
    /// Describes an invoice to be attached, from the bytes of its XML.
    /// </summary>
    /// <param name="xml">
    /// The invoice as UN/CEFACT CII XML. Taken as bytes rather than as a string because bytes are
    /// what is embedded: an XML document declares its own encoding, and re-encoding a string here
    /// would be this class deciding to disagree with that declaration.
    /// </param>
    public FacturXInvoice(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        if (xml.Length == 0)
            throw new ArgumentException(
                "An e-invoice is the XML. A document attaching an empty file claims to be a Factur-X "
                + "invoice and carries nothing to read, which is worse than a plain PDF.", nameof(xml));

        Xml = xml;
    }

    /// <summary>Gets the invoice XML, as it will be embedded.</summary>
    public byte[] Xml { get; }

    /// <summary>
    /// Gets or sets how much of an invoice the XML says, written as <c>fx:ConformanceLevel</c>. The
    /// default is <see cref="EInvoiceProfile.En16931"/>, which is what the public mandates are
    /// written against.
    /// </summary>
    public EInvoiceProfile Profile { get; set; } = EInvoiceProfile.En16931;

    /// <summary>
    /// Gets or sets the name the XML is attached under. The default is what Factur-X and ZUGFeRD 2.1
    /// require, and a receiver looks for it by name.
    /// </summary>
    public string FileName { get; set; } = "factur-x.xml";

    /// <summary>
    /// Gets or sets what a viewer shows beside the attachment in its attachments pane.
    /// </summary>
    public string Description { get; set; } = "Factur-X invoice";

    /// <summary>
    /// Gets or sets what kind of document the XML is, written as <c>fx:DocumentType</c>.
    /// <c>INVOICE</c> is the only value Factur-X defines; the Order-X sibling uses <c>ORDER</c>,
    /// <c>ORDER_RESPONSE</c> and <c>ORDER_CHANGE</c>.
    /// </summary>
    public string DocumentType { get; set; } = "INVOICE";

    /// <summary>
    /// Gets or sets the version of the standard the XML follows, written as <c>fx:Version</c>. This
    /// is the version of Factur-X, not of the invoice and not of the document.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Gets or sets the XMP namespace the invoice properties are written in. The default is the
    /// Factur-X one; ZUGFeRD 1.0 used <c>urn:ferd:pdfa:CrossIndustryDocument:invoice:1p0#</c> and
    /// Order-X ends <c>:order:1p0#</c>.
    /// </summary>
    public string NamespaceUri { get; set; } = "urn:factur-x:pdfa:CrossIndustryDocument:invoice:1p0#";

    /// <summary>
    /// Gets or sets the prefix the namespace is bound to, which the extension schema declares and
    /// the properties then use. ZUGFeRD 1.0 used <c>zf</c>.
    /// </summary>
    public string Prefix { get; set; } = "fx";

    /// <summary>
    /// Gets or sets the human-readable name of the extension schema, which is what a validator shows
    /// when it has something to say about it.
    /// </summary>
    public string SchemaName { get; set; } = "Factur-X PDFA Extension Schema";

    /// <summary>
    /// Gets or sets what the attachment has to do with the document.
    /// <see cref="PdfAFRelationship.Data"/> is what Factur-X and ZUGFeRD 2.x require — it says the
    /// XML and the page are the same invoice. ZUGFeRD 1.0 asked for
    /// <see cref="PdfAFRelationship.Alternative"/>.
    /// </summary>
    public PdfAFRelationship Relationship { get; set; } = PdfAFRelationship.Data;

    /// <summary>
    /// Attaches the invoice to the document, claims PDF/A-3 for it, and arranges for the metadata
    /// packet to describe what was attached. Answers the file specification, so that a caller can
    /// set anything this does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The document is left claiming PDF/A-3</b>, because that is the only archival profile that
    /// may carry an attachment at all and a Factur-X document is a PDF/A-3 document by definition.
    /// The claim is enforced at save time rather than stamped on, so a document that reaches this
    /// with no title or no output-intent profile will refuse to save until it has both — which is
    /// the point of the claim rather than a fault of it. An existing PDF/A-1 or PDF/A-2 claim is
    /// refused here rather than overwritten: those profiles may carry nothing, and quietly promoting
    /// a caller's claim would be deciding for them which standard their document meets.
    /// </para>
    /// <para>
    /// The metadata is written through <see cref="PdfDocument.AddMetadataContributor"/>, which adds
    /// alongside whatever else the document is already carrying — or carries afterwards — rather
    /// than replacing it. The packet is built at save time from the information dictionary, so there
    /// is nowhere else to reach it, and neither this call nor a caller's own
    /// <see cref="PdfDocument.CustomizeMetadata"/> can drop the other's contribution, whichever runs
    /// first and whichever is set up first.
    /// </para>
    /// </remarks>
    public PdfFileSpecification AttachTo(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Required(FileName, nameof(FileName));
        Required(NamespaceUri, nameof(NamespaceUri));
        Required(Prefix, nameof(Prefix));
        Required(SchemaName, nameof(SchemaName));
        Required(DocumentType, nameof(DocumentType));
        Required(Version, nameof(Version));

        // Built now rather than inside the hook, so that a later edit to this object does not change
        // what a document already told to carry the invoice says about it. Built before anything about
        // the document changes, too: an XmpExtensionSchema refuses a prefix XML would not accept as a
        // name, and the point of building it here is that the refusal happens before the document is
        // half converted.
        var schema = Schema();

        var claimed = document.Options.Conformance;
        if (claimed != PdfAConformance.None
            && claimed != PdfAConformance.PdfA3B && claimed != PdfAConformance.PdfA3A)
            throw new InvalidOperationException(
                "This document claims " + claimed + ", and no PDF/A profile but PDF/A-3 may carry an "
                + "embedded file — which is exactly why the hybrid e-invoice formats are built on "
                + "PDF/A-3. Either set Options.Conformance to " + nameof(PdfAConformance.PdfA3B)
                + " (or " + nameof(PdfAConformance.PdfA3A) + " for an accessible hybrid invoice) or "
                + "leave it alone and let attaching the invoice set it.");

        // Attached before anything is changed about the document, so that the one failure a caller
        // is likely to hit here — a second invoice under a name the first already took — leaves the
        // document exactly as it was rather than half converted.
        var specification =
            document.Attachments.Add(FileName, Xml, Relationship, Description, InvoiceMimeType);

        // A caller who already claimed PdfA3A wants an accessible hybrid invoice and keeps that
        // claim; one who claimed nothing gets the plain PdfA3B a Factur-X document defaults to.
        // Either way the claim already carries an attachment happily, so there is nothing to change
        // for it — only the caller who asked for nothing gets a claim set on their behalf.
        if (claimed == PdfAConformance.None)
            document.Options.Conformance = PdfAConformance.PdfA3B;

        document.AddMetadataContributor(metadata => metadata.DeclareSchema(schema));

        return specification;
    }

    /// <summary>
    /// Finds the e-invoice a document carries, or null when it carries none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Receiving is half of what the mandates ask for, and it is the half that starts with this
    /// question. The answer is found by name: the standards name the file precisely so that a
    /// receiver need not guess, and the names every version of them uses are few enough to list —
    /// <c>factur-x.xml</c>, <c>zugferd-invoice.xml</c>, <c>xrechnung.xml</c>, <c>order-x.xml</c>,
    /// compared without regard to case because ZUGFeRD 1.0 capitalised its own.
    /// </para>
    /// <para>
    /// Deliberately not found by relationship and media type. Every <c>/Data</c> attachment that is
    /// <c>text/xml</c> would match, and a document may well carry an XML that is data about it
    /// without being an invoice; answering "here is your invoice" about such a file is worse than
    /// answering nothing.
    /// </para>
    /// </remarks>
    public static PdfFileSpecification FindIn(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var attachment in document.Attachments)
        {
            var name = attachment.FileName;
            if (name == null)
                continue;

            foreach (var known in KnownFileNames)
            {
                if (string.Equals(name, known, StringComparison.OrdinalIgnoreCase))
                    return attachment;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads the bytes of the e-invoice a document carries, or null when it carries none. The XML is
    /// answered as it was embedded, so that whatever parses it reads its own encoding declaration
    /// rather than one this decided on its behalf.
    /// </summary>
    public static byte[] ReadFrom(PdfDocument document)
    {
        var specification = FindIn(document);
        var embedded = specification?.EmbeddedFile;
        return embedded?.Stream?.UnfilteredValue;
    }

    /// <summary>
    /// The exact string a validator expects for a profile. The space in two of them is load-bearing.
    /// </summary>
    internal static string ConformanceLevelOf(EInvoiceProfile profile)
    {
        switch (profile)
        {
            case EInvoiceProfile.Minimum: return "MINIMUM";
            case EInvoiceProfile.BasicWithoutLines: return "BASIC WL";
            case EInvoiceProfile.Basic: return "BASIC";
            case EInvoiceProfile.En16931: return "EN 16931";
            case EInvoiceProfile.Extended: return "EXTENDED";
            case EInvoiceProfile.XRechnung: return "XRECHNUNG";
            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile,
                    "There is no conformance level for this profile, so there is nothing true to "
                    + "write for it.");
        }
    }

    /// <summary>
    /// The extension schema declaring the four properties the packet then uses, built from
    /// <see cref="Properties"/> so that the declared set and the written set are the same set by
    /// construction.
    /// </summary>
    /// <remarks>
    /// Not optional and not decoration. PDF/A requires every property in the metadata to belong to a
    /// schema the file either predefines or describes, and the invoice namespace is nobody's
    /// predefined schema — so a packet carrying <c>fx:DocumentType</c> without this fails validation
    /// for its metadata rather than for its invoice, which is a confusing way to be wrong.
    /// </remarks>
    private XmpExtensionSchema Schema()
    {
        var properties = Properties()
            .Select(property => new XmpSchemaProperty(
                property.Name, property.Description,
                // "external" says the value describes something that came from outside rather than
                // something derived from the document's own content, which an invoice attached to it is.
                XmpPropertyCategory.External, property.Value))
            .ToList();

        return new XmpExtensionSchema(SchemaName, NamespaceUri, Prefix, properties);
    }

    /// <summary>
    /// The four properties the format defines: what each is called, what the extension schema says
    /// about it, and what this invoice says for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One list, walked twice — once to declare the four and once to write them. Declaring a
    /// property that is never written and writing one that was never declared are both validation
    /// failures, and the way to make neither is to have one place saying which four there are. The
    /// name and the value travel together for the same reason: a lookup keyed by name is a second
    /// place to get the set wrong.
    /// </para>
    /// <para>
    /// <c>DocumentFileName</c> is <see cref="FileName"/> rather than a second copy of it, because a
    /// receiver takes the attachment by the name the metadata gives it and the two disagreeing is a
    /// document describing an invoice it does not carry.
    /// </para>
    /// </remarks>
    private IEnumerable<(string Name, string Description, string Value)> Properties()
    {
        yield return ("DocumentType",
            "The type of the embedded XML document", DocumentType);
        yield return ("DocumentFileName",
            "The name of the embedded XML document", FileName);
        yield return ("Version",
            "The version of the standard the embedded XML document follows", Version);
        yield return ("ConformanceLevel",
            "The profile of the embedded XML document", ConformanceLevelOf(Profile));
    }

    private static void Required(string value, string property)
    {
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(
                property + " has to say something: it goes into the metadata, and an invoice "
                + "described by an empty string is described by nothing.");
    }
}
