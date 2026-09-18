using System;
using System.Xml;

namespace PdfPinata.Pdf.Metadata;

/// <summary>
/// One property of an <see cref="XmpExtensionSchema"/>: what it is called, what it means, where its
/// value came from, and what this document says for it.
/// </summary>
/// <remarks>
/// The name and the value travel together deliberately. A schema is declared and written from the
/// same list of these, so a property that is declared but never written, or written but never
/// declared, cannot happen — there is only the one list to get wrong.
/// </remarks>
public sealed class XmpSchemaProperty
{
    /// <param name="name">
    /// The property's name. Becomes part of an XML element name in the packet — <c>fx:DocumentType</c>
    /// for a property named <c>DocumentType</c> in a schema prefixed <c>fx</c> — so it is refused when
    /// it is not an XML NCName, the same check <see cref="XmpExtensionSchema"/> applies to a prefix
    /// and for the same reason: there is no escaping it once it becomes part of an element name.
    /// </param>
    /// <param name="description">The human-readable description a validator shows for the property.</param>
    /// <param name="category">
    /// Whether the value is derived from the document's own content or came from outside it.
    /// </param>
    /// <param name="value">
    /// The value this document says for the property, written as XMP's <c>Text</c> value type — the
    /// only one this shape delivers today, though it does not preclude another arriving later.
    /// </param>
    public XmpSchemaProperty(string name, string description, XmpPropertyCategory category, string value)
    {
        Name = RequireName(Require(name, nameof(name)));
        Description = Require(description, nameof(description));
        Category = category;
        Value = Require(value, nameof(value));
    }

    /// <summary>The property's name.</summary>
    public string Name { get; }

    /// <summary>The human-readable description a validator shows for the property.</summary>
    public string Description { get; }

    /// <summary>Whether the value came from the document's own content or from outside it.</summary>
    public XmpPropertyCategory Category { get; }

    /// <summary>The value this document says for the property.</summary>
    public string Value { get; }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException(
                parameterName + " has to say something: it goes into the metadata, and a property "
                + "described by an empty string is described by nothing.");

        return value;
    }

    /// <summary>
    /// Refuses anything XML would not accept as a name, naming the value. There is only the one
    /// caller of this — the name — so the label in the message is fixed rather than derived from a
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
                "Name becomes part of an XML element name, so it has to be a name XML accepts — no "
                + "spaces, no quotation marks, no colon, and not starting with a digit. '" + value
                + "' is not one: " + malformed.Message,
                malformed);
        }

        return value;
    }
}
