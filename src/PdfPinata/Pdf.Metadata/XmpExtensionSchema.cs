using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// A namespace of a caller's own, declared once so that <see cref="XmpMetadata"/> can write both the
/// <c>pdfaExtension:schemas</c> description that declares it and the values that use it from the same
/// description of what it means.
/// </summary>
/// <remarks>
/// <para>
/// ISO 19005 clause 6.6.2.3.1 holds every property in an XMP packet to a schema the file either
/// predefines or describes. A property in a namespace nobody has heard of — an invoice's document
/// type, a demo's note about which demo wrote the file — needs one of these before
/// <see cref="XmpMetadata.DeclareSchema"/> will accept it, and validation happens here, at the point
/// the schema is described, rather than waiting for the document to be saved: nothing about a schema
/// declaration depends on the rest of the document, so there is nothing to wait for.
/// </para>
/// <para>
/// <see cref="Prefix"/> is checked as an XML NCName rather than escaped, because there is no escaping
/// it — it becomes part of an element name and of a namespace declaration, and neither is a place a
/// character can be written as an entity.
/// </para>
/// </remarks>
public sealed class XmpExtensionSchema
{
    /// <param name="schemaName">
    /// The human-readable name of the schema, which is what a validator shows when it has something to
    /// say about it.
    /// </param>
    /// <param name="namespaceUri">The namespace URI the schema's properties are written in.</param>
    /// <param name="prefix">
    /// The prefix the namespace is bound to. Refused when it is not an XML NCName, since it becomes
    /// part of an element name and of a namespace declaration.
    /// </param>
    /// <param name="properties">
    /// The properties the schema declares. Refused when empty: a schema declaring nothing is not a
    /// schema a validator can hold anything to.
    /// </param>
    public XmpExtensionSchema(string schemaName, string namespaceUri, string prefix,
        IReadOnlyList<XmpSchemaProperty> properties)
    {
        SchemaName = Require(schemaName, nameof(schemaName));
        NamespaceUri = Require(namespaceUri, nameof(namespaceUri));
        Prefix = RequireName(Require(prefix, nameof(prefix)));

        if (properties == null || properties.Count == 0)
            throw new InvalidOperationException(
                "A schema with no properties declares nothing, so there is nothing for a validator to "
                + "hold it to.");

        if (properties.Any(property => property == null))
            throw new InvalidOperationException(
                "A schema's properties cannot contain a null entry: every one of them is written into "
                + "the extension description.");

        Properties = properties.ToList().AsReadOnly();
    }

    /// <summary>The human-readable name of the schema.</summary>
    public string SchemaName { get; }

    /// <summary>The namespace URI the schema's properties are written in.</summary>
    public string NamespaceUri { get; }

    /// <summary>The prefix the namespace is bound to.</summary>
    public string Prefix { get; }

    /// <summary>The properties the schema declares.</summary>
    public IReadOnlyList<XmpSchemaProperty> Properties { get; }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(
                parameterName + " has to say something: it goes into the metadata, and a schema "
                + "described by an empty string is described by nothing.");

        return value;
    }

    /// <summary>
    /// Refuses anything XML would not accept as a name, naming the value. There is only the one
    /// caller of this — the prefix — so the label in the message is fixed rather than derived from a
    /// parameter name.
    /// </summary>
    private static string RequireName(string value)
    {
        try
        {
            XmlConvert.VerifyNCName(value);
        }
        catch (XmlException malformed)
        {
            throw new InvalidOperationException(
                "Prefix becomes part of an XML element name and of a namespace declaration, so it has "
                + "to be a name XML accepts — no spaces, no quotation marks, no colon, and not "
                + "starting with a digit. '" + value + "' is not one: " + malformed.Message,
                malformed);
        }

        // An NCName alone is not enough: XML Namespaces reserves 'xml' and 'xmlns', so neither can
        // be bound to another URI, and 'rdf' would rebind the namespace this packet already uses for
        // rdf:Description and rdf:about on the very element the schema's values are written into.
        if (value.Equals("xml", StringComparison.OrdinalIgnoreCase)
            || value.Equals("xmlns", StringComparison.OrdinalIgnoreCase)
            || value.Equals("rdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Prefix '" + value + "' is reserved and cannot be declared: 'xml' and 'xmlns' cannot "
                + "be bound to another namespace, and 'rdf' is already bound to the one this packet "
                + "writes rdf:Description and rdf:about in.");

        return value;
    }
}
