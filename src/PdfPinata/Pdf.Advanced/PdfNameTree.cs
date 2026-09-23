using System.Collections.Generic;
using System.Linq;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Reads a name tree — the sorted, possibly-nested mapping from string to object that ISO 32000-1
/// 7.9.6 defines, and that the catalog's <c>/Names</c> dictionary holds one of per kind of thing a
/// document can name.
/// </summary>
/// <remarks>
/// Two kinds are read here. <c>/Dests</c> holds the destinations a link can point at by name, and
/// <c>/EmbeddedFiles</c> holds the files a document carries. The tree is the same shape in both
/// cases and was walked twice before this existed; it is one walk now, so a tree that leads back
/// into itself is guarded against once rather than in each reader that forgets to.
/// </remarks>
internal static class PdfNameTree
{
    /// <summary>
    /// How far down a tree to go before giving up on it.
    /// </summary>
    /// <remarks>
    /// A depth cap alone is not enough to make a walk terminate cheaply, and it is worth saying why,
    /// because it reads as though it is. Depth bounds how far down a path goes; it does not bound how
    /// many paths there are. A node listing itself twice among its own kids doubles the work at every
    /// level, so a document of a few hundred bytes reaches 2^32 node visits before the cap stops it —
    /// which is a hang rather than a refusal. What bounds the work is entering each node once; the cap
    /// is kept for the honest tree that is merely absurdly deep.
    /// </remarks>
    internal const int MaxDepth = 32;

    /// <summary>
    /// The root of one of the catalog's name trees, or null if the document has none of that kind.
    /// </summary>
    internal static PdfDictionary Root(PdfDocument document, string kind)
    {
        var names = document?.Catalog?.Elements.GetDictionary("/Names");
        return names?.Elements.GetDictionary(kind);
    }

    /// <summary>
    /// Every name one of the catalog's name trees holds, with what it stands for, as it is written
    /// rather than resolved.
    /// </summary>
    internal static IEnumerable<KeyValuePair<string, PdfItem>> Enumerate(PdfDocument document, string kind)
    {
        var root = Root(document, kind);
        return root == null
            ? []
            : Walk(root, 0, []);
    }

    /// <summary>
    /// Every name a node holds and every name the nodes below it hold, in the order written.
    /// </summary>
    /// <param name="node">The node to read.</param>
    /// <param name="depth">How far down the tree this node is.</param>
    /// <param name="seen">
    /// The nodes already entered. A name tree is a tree, so a node reached twice is a malformed
    /// document rather than a shared subtree — and reading it a second time would at best repeat every
    /// name below it and at worst never finish. See <see cref="MaxDepth"/>.
    /// </param>
    internal static IEnumerable<KeyValuePair<string, PdfItem>> Walk(PdfDictionary node, int depth,
        HashSet<PdfDictionary> seen)
    {
        if (node == null || depth > MaxDepth || !seen.Add(node))
            yield break;

        var leaves = node.Elements.GetArray("/Names");
        if (leaves != null)
        {
            var count = leaves.Elements.Count;
            for (var idx = 0; idx + 1 < count; idx += 2)
            {
                var name = TextOf(leaves.Elements[idx]);
                if (name != null)
                    yield return new KeyValuePair<string, PdfItem>(name, leaves.Elements[idx + 1]);
            }
        }

        var kids = node.Elements.GetArray("/Kids");
        if (kids == null)
            yield break;

        for (var idx = 0; idx < kids.Elements.Count; idx++)
        {
            foreach (var entry in Walk(kids.Elements.GetDictionary(idx), depth + 1, seen))
                yield return entry;
        }
    }

    /// <summary>
    /// The text of a name written either as a string or as a name, without the slash a name
    /// carries, or null if the item is neither.
    /// </summary>
    internal static string TextOf(PdfItem item)
    {
        if (item is PdfString text)
            return text.Value;

        if (item is PdfStringObject textObject)
            return textObject.Value;

        var name = item as PdfName;
        if (name != null)
            return WithoutSlash(name.Value);

        var nameObject = item as PdfNameObject;
        if (nameObject != null)
            return WithoutSlash(nameObject.Value);

        return null;
    }

    private static string WithoutSlash(string name)
    {
        return name != null && name.Length > 0 && name[0] == '/' ? name[1..] : name;
    }
}
