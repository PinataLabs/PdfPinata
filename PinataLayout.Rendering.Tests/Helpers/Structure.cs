using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;

namespace PinataLayout.Rendering.Tests.Helpers;

/// <summary>
///   Reads the structure tree back out of a rendered document.
/// </summary>
/// <remarks>
///   The tree is what a screen reader walks, so a test asserting about accessibility has to assert
///   about the tree and not about the content stream. Read from the saved file rather than off the
///   renderer, for the reason <see cref="Rendered"/> gives: what matters is what was written.
/// </remarks>
internal static class Structure
{
    /// <summary>The root of the tree of the given document, laid out and read back.</summary>
    internal static StructureNode Of(Document document) => RootOf(Rendered.Of(document));

    /// <summary>The root of the tree of a document already rendered.</summary>
    internal static StructureNode RootOf(PdfDocument rendered)
    {
        var root = rendered.Internals.Catalog.Elements.GetDictionary("/StructTreeRoot");
        if (root == null)
            return null;

        // The root is not itself an element — it is the holder of the tree — so what comes back is
        // the single element beneath it, which for anything MigraDoc renders is the /Document.
        var children = ChildrenOf(root);
        return children.Count == 1 ? children[0] : new StructureNode("StructTreeRoot", children);
    }

    static List<StructureNode> ChildrenOf(PdfDictionary element)
    {
        var children = new List<StructureNode>();
        Collect(element.Elements["/K"], children);
        return children;
    }

    /// <summary>
    ///   Reads the three different things a <c>/K</c> can hold, keeping only the elements.
    /// </summary>
    /// <remarks>
    ///   The other two are what a leaf points at: a bare integer, which is a mark on the element's
    ///   own page, and a dictionary that is a reference rather than an element — <c>/MCR</c> for a
    ///   mark on some other page, <c>/OBJR</c> for an annotation. Counted rather than described,
    ///   because what a test wants to know is that an element has content, not which identifier.
    /// </remarks>
    static void Collect(PdfItem kids, List<StructureNode> into)
    {
        switch (Resolve(kids))
        {
            case PdfArray array:
                foreach (var item in array.Elements)
                    Collect(item, into);
                break;

            case PdfDictionary dictionary when dictionary.Elements.GetName("/Type") == "/StructElem":
                into.Add(NodeOf(dictionary));
                break;
        }
    }

    static StructureNode NodeOf(PdfDictionary element)
    {
        var attributes = element.Elements.GetDictionary("/A");

        return new StructureNode(Bare(element.Elements.GetName("/S")), ChildrenOf(element))
        {
            AlternateText = element.Elements.GetString("/Alt"),
            ActualText = element.Elements.GetString("/ActualText"),
            Id = element.Elements.GetString("/ID"),
            Summary = element.Elements.GetString("/Summary"),
            Scope = Bare(attributes?.Elements.GetName("/Scope")),
            ColumnSpan = attributes?.Elements.GetInteger("/ColSpan") ?? 1,
            RowSpan = attributes?.Elements.GetInteger("/RowSpan") ?? 1,
            MarkCount = MarksOf(element),
            AnnotationCount = ReferencesOf(element, "/OBJR"),
        };
    }

    static int MarksOf(PdfDictionary element)
    {
        var marks = 0;
        foreach (var item in Items(element))
        {
            if (Resolve(item) is PdfInteger)
                marks++;
            else if (Resolve(item) is PdfDictionary dictionary
                     && dictionary.Elements.GetName("/Type") == "/MCR")
                marks++;
        }

        return marks;
    }

    static int ReferencesOf(PdfDictionary element, string type)
    {
        var found = 0;
        foreach (var item in Items(element))
        {
            if (Resolve(item) is PdfDictionary dictionary && dictionary.Elements.GetName("/Type") == type)
                found++;
        }

        return found;
    }

    static IEnumerable<PdfItem> Items(PdfDictionary element)
    {
        var kids = Resolve(element.Elements["/K"]);
        if (kids is PdfArray array)
            return array.Elements;

        return kids == null ? Enumerable.Empty<PdfItem>() : new[] { kids };
    }

    static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    static string Bare(string name) =>
        string.IsNullOrEmpty(name) ? name : name.TrimStart('/');
}

/// <summary>
///   One element of the structure tree, as a test wants to see it.
/// </summary>
internal sealed class StructureNode
{
    internal StructureNode(string tag, List<StructureNode> children)
    {
        Tag = tag;
        Children = children;
    }

    internal string Tag { get; }
    internal IReadOnlyList<StructureNode> Children { get; }
    internal string AlternateText { get; init; }

    /// <summary>What this element's marks really spell, when the glyphs disagree with the text.</summary>
    internal string ActualText { get; init; }

    /// <summary>
    ///   The name this element can be pointed at by. PDF/UA requires one of every <c>Note</c>.
    /// </summary>
    internal string Id { get; init; }

    internal string Summary { get; init; }
    internal string Scope { get; init; }
    internal int ColumnSpan { get; init; } = 1;
    internal int RowSpan { get; init; } = 1;

    /// <summary>How many runs of marks on a page this element covers.</summary>
    internal int MarkCount { get; init; }

    /// <summary>How many annotations hang off this element.</summary>
    internal int AnnotationCount { get; init; }

    /// <summary>Every element of the subtree, this one first, depth first.</summary>
    internal IEnumerable<StructureNode> Descendants()
    {
        yield return this;
        foreach (var child in Children)
        foreach (var node in child.Descendants())
            yield return node;
    }

    /// <summary>Every element of the subtree with the given structure type.</summary>
    internal IEnumerable<StructureNode> OfTag(string tag) =>
        Descendants().Where(node => node.Tag == tag);

    /// <summary>The one element of the subtree with the given structure type.</summary>
    internal StructureNode Single(string tag) => OfTag(tag).Single();

    /// <summary>The tags of this element's children, in order.</summary>
    internal string[] ChildTags() => Children.Select(child => child.Tag).ToArray();

    /// <summary>
    ///   The subtree as an indented outline, which is what a failure should print.
    /// </summary>
    public override string ToString()
    {
        var text = new StringBuilder();
        Write(text, 0);
        return text.ToString();
    }

    void Write(StringBuilder text, int depth)
    {
        text.Append(new string(' ', depth * 2)).Append(Tag);

        if (MarkCount > 0)
            text.Append(" (").Append(MarkCount).Append(MarkCount == 1 ? " mark)" : " marks)");
        if (!string.IsNullOrEmpty(AlternateText))
            text.Append(" alt=\"").Append(AlternateText).Append('"');
        if (!string.IsNullOrEmpty(ActualText))
            text.Append(" actual=\"").Append(ActualText).Append('"');
        if (!string.IsNullOrEmpty(Scope))
            text.Append(" scope=").Append(Scope);

        text.Append('\n');

        foreach (var child in Children)
            child.Write(text, depth + 1);
    }
}
