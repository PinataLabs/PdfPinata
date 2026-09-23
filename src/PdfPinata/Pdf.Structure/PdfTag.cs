using System;

namespace PdfPinata.Pdf.Structure;

/// <summary>
/// A structure type: what a piece of content <em>is</em>, as opposed to where it sits and what it
/// looks like. A heading and a caption are both a run of glyphs on the page, and this is the only
/// thing that tells them apart.
/// </summary>
/// <remarks>
/// A struct rather than an enum so that a caller may use a type this library has never heard of —
/// the standard set below is what readers understand without a role map, and a document is free to
/// name its own and map them through <see cref="PdfStructureTreeRoot.RoleMap"/>.
/// </remarks>
public readonly struct PdfTag : IEquatable<PdfTag>
{
    /// <summary>
    /// The name as it goes into the file, leading solidus and all.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Names a structure type directly, for one that is not among the standard set below.
    /// </summary>
    public PdfTag(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException(@"A structure type has to have a name.", nameof(name));

        Name = name[0] == '/' ? name : "/" + name;
    }

    /// <summary>The whole document. The usual root of the tree.</summary>
    public static PdfTag Document => new("/Document");

    /// <summary>A part of a document.</summary>
    public static PdfTag Part => new("/Part");

    /// <summary>A section.</summary>
    public static PdfTag Section => new("/Sect");

    /// <summary>A paragraph.</summary>
    public static PdfTag P => new("/P");

    /// <summary>A first-level heading.</summary>
    public static PdfTag H1 => new("/H1");

    /// <summary>A second-level heading.</summary>
    public static PdfTag H2 => new("/H2");

    /// <summary>A third-level heading.</summary>
    public static PdfTag H3 => new("/H3");

    /// <summary>A fourth-level heading.</summary>
    public static PdfTag H4 => new("/H4");

    /// <summary>A fifth-level heading.</summary>
    public static PdfTag H5 => new("/H5");

    /// <summary>A sixth-level heading.</summary>
    public static PdfTag H6 => new("/H6");

    /// <summary>A list.</summary>
    public static PdfTag L => new("/L");

    /// <summary>An item of a list.</summary>
    public static PdfTag LI => new("/LI");

    /// <summary>The label of a list item — its bullet or its number.</summary>
    public static PdfTag Lbl => new("/Lbl");

    /// <summary>The body of a list item.</summary>
    public static PdfTag LBody => new("/LBody");

    /// <summary>A table.</summary>
    public static PdfTag Table => new("/Table");

    /// <summary>A row of a table.</summary>
    public static PdfTag TR => new("/TR");

    /// <summary>A header cell of a table.</summary>
    public static PdfTag TH => new("/TH");

    /// <summary>A data cell of a table.</summary>
    public static PdfTag TD => new("/TD");

    /// <summary>A figure. Needs alternate text, or it says nothing to a reader who cannot see it.</summary>
    public static PdfTag Figure => new("/Figure");

    /// <summary>A caption.</summary>
    public static PdfTag Caption => new("/Caption");

    /// <summary>A link. The annotation belongs to the structure as well as to the page.</summary>
    public static PdfTag Link => new("/Link");

    /// <summary>
    /// A note, such as a footnote. ISO 14289-1 requires one to carry an
    /// <see cref="PdfStructureElement.Id"/>, so that the <see cref="Reference"/> citing it has
    /// something to point at.
    /// </summary>
    /// <remarks>
    /// PDF 1.7's type. PDF 2.0 removed it in favour of <see cref="FootnoteOrEndnote"/>, and ISO
    /// 14289-2 clause 8.2.5.14 refuses a PDF/UA-2 document that still uses this one.
    /// </remarks>
    public static PdfTag Note => new("/Note");

    /// <summary>
    /// A footnote or endnote — PDF 2.0's replacement for <see cref="Note"/>, and the one ISO
    /// 14289-2 (PDF/UA-2) requires in its place. Otherwise the same shape: a label and body, cited
    /// from the body text by a <see cref="Reference"/>.
    /// </summary>
    public static PdfTag FootnoteOrEndnote => new("/FENote");

    /// <summary>
    /// A citation of something elsewhere in the document — the raised mark in the body text that
    /// names a footnote, rather than the note itself.
    /// </summary>
    /// <remarks>
    /// Worth telling apart from <see cref="Lbl"/>, which is the label of a list item. Both are a
    /// short mark standing in front of something, and a reader announces them differently: a label
    /// is read as part of the list it numbers, where a reference is read as a pointer somewhere else
    /// and can be offered as a jump.
    /// </remarks>
    public static PdfTag Reference => new("/Reference");

    /// <summary>A span of text within a paragraph.</summary>
    public static PdfTag Span => new("/Span");

    /// <summary>
    /// Content that is on the page but is not part of what the page says — a running head, a
    /// folio, a rule. Written directly by <c>XGraphics.BeginArtifact</c> rather than through this
    /// type, so this is here for a reader to compare a run's tag against, not a writer to pass one.
    /// </summary>
    public static PdfTag Artifact => new("/Artifact");

    /// <inheritdoc/>
    public bool Equals(PdfTag other) => Name == other.Name;

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is PdfTag other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Name?.GetHashCode() ?? 0;

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>Answers whether two structure types are the same type.</summary>
    public static bool operator ==(PdfTag left, PdfTag right) => left.Equals(right);

    /// <summary>Answers whether two structure types are different types.</summary>
    public static bool operator !=(PdfTag left, PdfTag right) => !left.Equals(right);
}
