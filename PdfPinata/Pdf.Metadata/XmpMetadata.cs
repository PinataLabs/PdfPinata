using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// The XMP metadata packet of a document: the same facts the document information dictionary holds,
/// said again in RDF/XML, plus the conformance identifier that is the only place a document can say
/// which standard it claims to meet.
/// </summary>
/// <remarks>
/// <para>
/// Every fact here also lives in <see cref="PdfDocumentInformation"/>, and a validator compares the
/// two and complains when they disagree. So the packet is built from the information dictionary at
/// save time rather than kept alongside it and hoped about — see <see cref="FromDocument"/>.
/// </para>
/// <para>
/// The packet is deliberately built to be extended. PDF/UA adds an identifier of its own, ZUGFeRD
/// adds a whole extension schema, and a caller may want a namespace nobody has thought of; all three
/// go in through <see cref="AdditionalDescriptions"/> rather than by editing this class.
/// </para>
/// </remarks>
public sealed class XmpMetadata
{
    /// <summary>
    /// The identifier every XMP packet carries. It is a fixed string rather than anything
    /// meaningful — a marker a byte scanner can find without parsing the PDF around it.
    /// </summary>
    private const string PacketId = "W5M0MpCehiHzreSzNTczkc9d";

    /// <summary>The document title. Required by every PDF/A profile, and by PDF/UA.</summary>
    public string Title { get; set; }

    /// <summary>The document author, going out as the single entry of a <c>dc:creator</c> sequence.</summary>
    public string Author { get; set; }

    /// <summary>The document subject, going out as <c>dc:description</c>.</summary>
    public string Subject { get; set; }

    /// <summary>The document keywords, going out as <c>pdf:Keywords</c>.</summary>
    public string Keywords { get; set; }

    /// <summary>The application that produced the content, going out as <c>xmp:CreatorTool</c>.</summary>
    public string CreatorTool { get; set; }

    /// <summary>The library that wrote the file, going out as <c>pdf:Producer</c>.</summary>
    public string Producer { get; set; }

    /// <summary>When the document was created.</summary>
    public DateTime? CreationDate { get; set; }

    /// <summary>When the document was last changed.</summary>
    public DateTime? ModificationDate { get; set; }

    /// <summary>
    /// The archival profile the document claims, which becomes the <c>pdfaid:part</c> and
    /// <c>pdfaid:conformance</c> pair. <see cref="PdfAConformance.None"/> writes neither.
    /// </summary>
    public PdfAConformance Conformance { get; set; }

    /// <summary>
    /// The accessibility profile the document claims, which becomes the <c>pdfuaid:part</c> entry.
    /// <see cref="PdfUAConformance.None"/> writes none.
    /// </summary>
    /// <remarks>
    /// XMP is the only place a PDF/UA claim can be made — unlike PDF/A there is no dictionary entry
    /// for it, so a document with a perfect structure tree and no identifier claims nothing at all.
    /// The two claims are independent and a document may carry both: PDF/A-3 says it will still open
    /// in fifty years, PDF/UA-1 says it can be read aloud, and neither implies the other.
    /// </remarks>
    public PdfUAConformance UAConformance { get; set; }

    /// <summary>
    /// Whole <c>rdf:Description</c> elements to place after the ones built here, for schemas this
    /// class knows nothing about. Each entry is written verbatim, so each is the caller's to get
    /// right — including its own namespace declarations.
    /// </summary>
    /// <remarks>
    /// <b>A document claiming PDF/A has to declare a namespace before it uses one.</b> ISO 19005
    /// clause 6.6.2.3.1 holds every property in the packet to a schema the file either predefines
    /// or describes, so a property in a namespace of the caller's own needs a
    /// <c>pdfaExtension:schemas</c> description naming it, placed here alongside the description
    /// that uses it. Without one the document opens perfectly in every reader and fails validation
    /// — for its metadata rather than for anything a reader would notice, which is a confusing way
    /// to be wrong. <c>PdfPinata.EInvoice</c> writes exactly that pair for the invoice
    /// namespace, and is worth reading as the worked example.
    /// </remarks>
    public IList<string> AdditionalDescriptions { get; } = new List<string>();

    private readonly List<XmpExtensionSchema> _extensionSchemas = new();

    /// <summary>
    /// The extension schemas declared so far, in declaration order. Populated by
    /// <see cref="DeclareSchema"/>; there is no other way to add to it, because a schema added any
    /// other way could not have had its prefix checked against the ones already here.
    /// </summary>
    public IReadOnlyList<XmpExtensionSchema> ExtensionSchemas => _extensionSchemas.AsReadOnly();

    /// <summary>
    /// Declares a namespace of the caller's own, so that the properties it describes may be written
    /// into the packet. Declaration and value are the same list by construction — see
    /// <see cref="XmpExtensionSchema"/> — so a property cannot be declared without being written, or
    /// written without being declared.
    /// </summary>
    /// <remarks>
    /// Refused immediately, not at save time, when a schema already declared here uses the same
    /// prefix: two schemas sharing a prefix would leave a property's namespace ambiguous, and nothing
    /// about that depends on the rest of the document.
    /// </remarks>
    public void DeclareSchema(XmpExtensionSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        foreach (var already in _extensionSchemas)
        {
            if (already.Prefix == schema.Prefix)
                throw new InvalidOperationException(
                    "A schema with prefix '" + schema.Prefix + "' is already declared in this packet. "
                    + "Two schemas cannot share a prefix, or a property's namespace would be "
                    + "ambiguous — give the new one a prefix of its own.");
        }

        _extensionSchemas.Add(schema);
    }

    /// <summary>
    /// Takes the facts from the document's information dictionary, so that the two cannot drift
    /// apart. Anything set on the result afterwards wins.
    /// </summary>
    public static XmpMetadata FromDocument(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var info = document.Info;
        return new XmpMetadata
        {
            Title = NullIfEmpty(info.Title),
            Author = NullIfEmpty(info.Author),
            Subject = NullIfEmpty(info.Subject),
            Keywords = NullIfEmpty(info.Keywords),
            CreatorTool = NullIfEmpty(info.Creator),
            Producer = NullIfEmpty(info.Producer),
            CreationDate = NullIfDefault(info.CreationDate),
            ModificationDate = NullIfDefault(info.ModificationDate),
        };
    }

    /// <summary>
    /// Builds the packet, ready to become the value of a metadata stream.
    /// </summary>
    /// <remarks>
    /// UTF-8 without a byte order mark on the stream itself: the mark belongs inside the
    /// <c>begin</c> attribute of the processing instruction, where the specification puts it, and a
    /// second one in front of that is a parse error in some readers.
    /// </remarks>
    public byte[] Build()
    {
        var xmp = new StringBuilder();

        // U+FEFF inside the attribute is how a scanner works out the encoding of what follows.
        xmp.Append("<?xpacket begin=\"﻿\" id=\"").Append(PacketId).Append("\"?>\n");
        xmp.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n");
        xmp.Append(" <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");

        AppendDublinCore(xmp);
        AppendPdf(xmp);
        AppendBasic(xmp);
        AppendConformance(xmp);
        AppendAccessibility(xmp);
        AppendExtensionSchemas(xmp);

        foreach (var description in AdditionalDescriptions)
            xmp.Append("  ").Append(description).Append('\n');

        xmp.Append(" </rdf:RDF>\n");
        xmp.Append("</x:xmpmeta>\n");

        // The run of spaces is padding a writer may overwrite in place to change the metadata
        // without moving anything after it. Nothing here does that yet, and leaving the room costs
        // a few hundred bytes against the day something does.
        for (var line = 0; line < 4; line++)
            xmp.Append(new string(' ', 99)).Append('\n');

        // "w" says the packet may be written over in place; "r" would say it may not.
        xmp.Append("<?xpacket end=\"w\"?>\n");

        return new UTF8Encoding(false).GetBytes(xmp.ToString());
    }

    private void AppendDublinCore(StringBuilder xmp)
    {
        if (Title == null && Author == null && Subject == null)
            return;

        xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
        AppendLanguageAlternative(xmp, "dc:title", Title);
        AppendSequence(xmp, "dc:creator", Author);
        AppendLanguageAlternative(xmp, "dc:description", Subject);
        xmp.Append("  </rdf:Description>\n");
    }

    private void AppendPdf(StringBuilder xmp)
    {
        if (Keywords == null && Producer == null)
            return;

        xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">\n");
        AppendSimple(xmp, "pdf:Keywords", Keywords);
        AppendSimple(xmp, "pdf:Producer", Producer);
        xmp.Append("  </rdf:Description>\n");
    }

    private void AppendBasic(StringBuilder xmp)
    {
        if (CreatorTool == null && CreationDate == null && ModificationDate == null)
            return;

        xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">\n");
        AppendSimple(xmp, "xmp:CreatorTool", CreatorTool);
        AppendSimple(xmp, "xmp:CreateDate", Iso8601(CreationDate));
        AppendSimple(xmp, "xmp:ModifyDate", Iso8601(ModificationDate));
        xmp.Append("  </rdf:Description>\n");
    }

    private void AppendConformance(StringBuilder xmp)
    {
        if (Conformance == PdfAConformance.None)
            return;

        xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">\n");
        AppendSimple(xmp, "pdfaid:part", PartOf(Conformance));
        AppendSimple(xmp, "pdfaid:conformance", LevelOf(Conformance));
        xmp.Append("  </rdf:Description>\n");
    }

    private void AppendAccessibility(StringBuilder xmp)
    {
        if (UAConformance == PdfUAConformance.None)
            return;

        xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\">\n");

        // No conformance letter to go with it. PDF/UA has parts and no levels, where PDF/A has
        // both — writing a pdfuaid:conformance to match the pdfaid one above is a common mistake and
        // a validator objects to it.
        AppendSimple(xmp, "pdfuaid:part", UAConformance == PdfUAConformance.PdfUA2 ? "2" : "1");

        // ISO 14289-2 clause 5 requires the revision year alongside the part, because part 2 of
        // the standard has already had one revision — 2024 is the only value that exists so far,
        // and part 1 carries no such property at all.
        if (UAConformance == PdfUAConformance.PdfUA2)
            AppendSimple(xmp, "pdfuaid:rev", "2024");

        xmp.Append("  </rdf:Description>\n");
    }

    /// <summary>
    /// One <c>pdfaExtension:schemas</c> description declaring every schema in <see cref="ExtensionSchemas"/>,
    /// followed by one <c>rdf:Description</c> per schema carrying the values it declared. Both are
    /// written from <see cref="XmpExtensionSchema.Properties"/> — the same list, walked twice — so a
    /// property declared here is always one written below, and a property written below is always one
    /// declared here.
    /// </summary>
    private void AppendExtensionSchemas(StringBuilder xmp)
    {
        if (_extensionSchemas.Count == 0)
            return;

        xmp.Append("  <rdf:Description rdf:about=\"\"")
           .Append(" xmlns:pdfaExtension=\"http://www.aiim.org/pdfa/ns/extension/\"")
           .Append(" xmlns:pdfaSchema=\"http://www.aiim.org/pdfa/ns/schema#\"")
           .Append(" xmlns:pdfaProperty=\"http://www.aiim.org/pdfa/ns/property#\">\n");
        xmp.Append("   <pdfaExtension:schemas>\n");
        xmp.Append("    <rdf:Bag>\n");

        foreach (var schema in _extensionSchemas)
            AppendSchemaDeclaration(xmp, schema);

        xmp.Append("    </rdf:Bag>\n");
        xmp.Append("   </pdfaExtension:schemas>\n");
        xmp.Append("  </rdf:Description>\n");

        foreach (var schema in _extensionSchemas)
            AppendSchemaValues(xmp, schema);
    }

    private static void AppendSchemaDeclaration(StringBuilder xmp, XmpExtensionSchema schema)
    {
        xmp.Append("     <rdf:li rdf:parseType=\"Resource\">\n");
        xmp.Append("      <pdfaSchema:schema>").Append(EscapeAttribute(schema.SchemaName))
           .Append("</pdfaSchema:schema>\n");
        xmp.Append("      <pdfaSchema:namespaceURI>").Append(EscapeAttribute(schema.NamespaceUri))
           .Append("</pdfaSchema:namespaceURI>\n");
        xmp.Append("      <pdfaSchema:prefix>").Append(schema.Prefix).Append("</pdfaSchema:prefix>\n");
        xmp.Append("      <pdfaSchema:property>\n");
        xmp.Append("       <rdf:Seq>\n");

        foreach (var property in schema.Properties)
        {
            xmp.Append("        <rdf:li rdf:parseType=\"Resource\">\n");
            xmp.Append("         <pdfaProperty:name>").Append(property.Name)
               .Append("</pdfaProperty:name>\n");
            xmp.Append("         <pdfaProperty:valueType>Text</pdfaProperty:valueType>\n");
            xmp.Append("         <pdfaProperty:category>").Append(CategoryOf(property.Category))
               .Append("</pdfaProperty:category>\n");
            xmp.Append("         <pdfaProperty:description>").Append(EscapeAttribute(property.Description))
               .Append("</pdfaProperty:description>\n");
            xmp.Append("        </rdf:li>\n");
        }

        xmp.Append("       </rdf:Seq>\n");
        xmp.Append("      </pdfaSchema:property>\n");
        xmp.Append("     </rdf:li>\n");
    }

    private static void AppendSchemaValues(StringBuilder xmp, XmpExtensionSchema schema)
    {
        xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:").Append(schema.Prefix).Append("=\"")
           .Append(EscapeAttribute(schema.NamespaceUri)).Append("\">\n");

        foreach (var property in schema.Properties)
        {
            xmp.Append("   <").Append(schema.Prefix).Append(':').Append(property.Name).Append('>')
               .Append(EscapeAttribute(property.Value))
               .Append("</").Append(schema.Prefix).Append(':').Append(property.Name).Append(">\n");
        }

        xmp.Append("  </rdf:Description>\n");
    }

    /// <summary>
    /// The category string PDF/A expects — the enum exists so a caller cannot misspell either of the
    /// two, and this is where the string it misspells nothing of comes from.
    /// </summary>
    private static string CategoryOf(XmpPropertyCategory category) => category switch
    {
        XmpPropertyCategory.Internal => "internal",
        XmpPropertyCategory.External => "external",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category,
            "There is no PDF/A category string for this value."),
    };

    /// <summary>
    /// The part number of ISO 19005 a profile belongs to, which is what <c>pdfaid:part</c> holds.
    /// </summary>
    internal static string PartOf(PdfAConformance conformance) => conformance switch
    {
        PdfAConformance.PdfA1B or PdfAConformance.PdfA1A => "1",
        PdfAConformance.PdfA2B or PdfAConformance.PdfA2A => "2",
        PdfAConformance.PdfA3B or PdfAConformance.PdfA3A => "3",
        _ => null,
    };

    /// <summary>
    /// The conformance letter a profile writes as <c>pdfaid:conformance</c> — <c>A</c> for a level
    /// that additionally requires a tagged structure tree, <c>B</c> for the basic level, and
    /// <c>null</c> for no claim at all, which is what stops <see cref="AppendConformance"/> writing
    /// the element.
    /// </summary>
    internal static string LevelOf(PdfAConformance conformance) => conformance switch
    {
        PdfAConformance.None => null,
        PdfAConformance.PdfA1A or PdfAConformance.PdfA2A or PdfAConformance.PdfA3A => "A",
        _ => "B",
    };

    private static void AppendSimple(StringBuilder xmp, string element, string value)
    {
        if (value == null)
            return;

        xmp.Append("   <").Append(element).Append('>')
           .Append(Escape(value))
           .Append("</").Append(element).Append(">\n");
    }

    /// <summary>
    /// A title or a description is a set of alternatives in different languages, even when there is
    /// only one of them, and the default one is marked <c>x-default</c> rather than by position.
    /// </summary>
    private static void AppendLanguageAlternative(StringBuilder xmp, string element, string value)
    {
        if (value == null)
            return;

        xmp.Append("   <").Append(element).Append("><rdf:Alt><rdf:li xml:lang=\"x-default\">")
           .Append(Escape(value))
           .Append("</rdf:li></rdf:Alt></").Append(element).Append(">\n");
    }

    /// <summary>
    /// An author is an ordered sequence, because a document may have several and the order is part
    /// of what it says.
    /// </summary>
    private static void AppendSequence(StringBuilder xmp, string element, string value)
    {
        if (value == null)
            return;

        xmp.Append("   <").Append(element).Append("><rdf:Seq><rdf:li>")
           .Append(Escape(value))
           .Append("</rdf:li></rdf:Seq></").Append(element).Append(">\n");
    }

    /// <summary>
    /// The date form XMP wants, which is ISO 8601 with the offset spelled out. A local time written
    /// without one is read as UTC by some tools and as local by others.
    /// </summary>
    private static string Iso8601(DateTime? value) =>
        value?.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);

    private static string Escape(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    /// <summary>
    /// The same three characters <see cref="Escape"/> handles, and the quotation mark besides. An
    /// extension schema's namespace URI and prefix are written into <em>attribute</em> values, where a
    /// quotation mark ends the attribute early and everything after it becomes markup — a mistake
    /// <see cref="Escape"/> alone cannot catch because none of the built-in descriptions it serves ever
    /// land in an attribute.
    /// </summary>
    private static string EscapeAttribute(string value) => Escape(value).Replace("\"", "&quot;");

    private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static DateTime? NullIfDefault(DateTime value) => value == default ? null : value;
}
