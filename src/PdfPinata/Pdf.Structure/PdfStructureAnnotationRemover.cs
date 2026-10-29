using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Structure;

/// <summary>
/// Takes annotations out of a document's structure tree, for when the annotations themselves are
/// being taken out of the document.
/// </summary>
/// <remarks>
/// <para>
/// An annotation joins the tree in two places: an <c>/OBJR</c> object reference among the kids of
/// the element that stands for it, and an entry in the parent tree under the annotation's own
/// <c>/StructParent</c>. Removing the annotation from its page and leaving either behind is a
/// tagged document that names something that is not there - the failure PDF/UA exists to rule
/// out, and one that renders perfectly, so nothing but a validator ever says so.
/// </para>
/// <para>
/// It works on whichever tree the document has. One built in this session by
/// <see cref="PdfDocument.Structure"/> is walked through the builder, which also holds the
/// annotation's entry until the parent tree is written at save; one read from a file is walked
/// from the catalog's <c>/StructTreeRoot</c> as the plain dictionaries it is. A document with
/// neither has nothing to do, and asking creates no tree.
/// </para>
/// </remarks>
internal static class PdfStructureAnnotationRemover
{
    /// <summary>
    /// How deep into the tree to go before giving up on it, against a file that makes an element
    /// its own ancestor.
    /// </summary>
    private const int MaxTreeDepth = 256;

    private const string StructParent = "/StructParent";

    /// <summary>
    /// Removes every reference to <paramref name="annotations"/> from the document's structure
    /// tree and parent tree.
    /// </summary>
    /// <param name="document">The document the annotations belong to.</param>
    /// <param name="annotations">
    /// The annotation dictionaries, each as its reference answers it - the field, for a widget that
    /// is merged with one.
    /// </param>
    /// <remarks>
    /// An element left with no kids because its only kid was the annotation is removed too, but
    /// only when it is a <c>/Form</c>: that is the element a widget is tagged as, and one with
    /// nothing in it stands for nothing. Any other type is left, since the annotation was not what
    /// made it.
    /// </remarks>
    internal static void Remove(PdfDocument document, ICollection<PdfDictionary> annotations)
    {
        if (annotations.Count == 0)
            return;

        var root = RootOf(document);
        if (root == null)
            return;

        // The parent tree first, while the annotations still say which key they are filed under.
        var keys = new List<int>();
        foreach (var annotation in annotations)
        {
            if (annotation.Elements.ContainsKey(StructParent))
                keys.Add(annotation.Elements.GetInteger(StructParent));
        }

        document.TaggedStructure?.ForgetAnnotationKeys(keys);

        if (keys.Count > 0 && ParentTreeOf(root) is { } parentTree)
        {
            foreach (var key in keys)
            {
                if (parentTree.Contains(key))
                    parentTree.Remove(key);
            }
        }

        foreach (var annotation in annotations)
            annotation.Elements.Remove(StructParent);

        RemoveKids(root, annotations, new HashSet<PdfDictionary>(), 0);
    }

    /// <summary>
    /// The structure tree root: the builder's when anything has been tagged in this session, else
    /// the catalog's, else null.
    /// </summary>
    private static PdfDictionary RootOf(PdfDocument document)
    {
        if (document.TaggedStructure is { } structure)
            return structure.Root;

        return document.Internals.Catalog.Elements.GetDictionary(PdfCatalog.Keys.StructTreeRoot);
    }

    /// <summary>
    /// The root's parent tree, wrapped as a number tree when it was read as a plain dictionary.
    /// </summary>
    private static PdfNumberTreeNode ParentTreeOf(PdfDictionary root)
    {
        if (root is PdfStructureTreeRoot built)
            return built.ParentTree;

        var tree = root.Elements.GetDictionary(PdfStructureTreeRoot.Keys.ParentTree);
        if (tree == null)
            return null;

        if (tree is PdfNumberTreeNode node)
            return node;

        node = new PdfNumberTreeNode(tree);
        if (!tree.IsIndirect)
            root.Elements[PdfStructureTreeRoot.Keys.ParentTree] = node;
        return node;
    }

    /// <summary>
    /// Removes the object references to the annotations from the kids of an element, and recurses
    /// into the elements among them.
    /// </summary>
    /// <returns>Whether anything under this element changed.</returns>
    private static bool RemoveKids(PdfDictionary element, ICollection<PdfDictionary> annotations,
        HashSet<PdfDictionary> seen, int depth)
    {
        if (depth > MaxTreeDepth || !seen.Add(element))
            return false;

        var changed = false;
        switch (PdfReference.Dereference(element.Elements[PdfStructureElement.Keys.K]))
        {
            case PdfArray kids:
                for (var idx = kids.Elements.Count - 1; idx >= 0; idx--)
                {
                    if (kids.Elements.GetDictionary(idx) is not { } kid)
                        continue;

                    if (Decide(kid, annotations, seen, depth, out var drop))
                        changed = true;
                    if (drop)
                        kids.Elements.RemoveAt(idx);
                }
                break;

            case PdfDictionary only:
                if (Decide(only, annotations, seen, depth, out var dropOnly))
                    changed = true;
                if (dropOnly)
                    element.Elements.Remove(PdfStructureElement.Keys.K);
                break;
        }
        return changed;
    }

    /// <summary>
    /// What to do with one kid: drop an object reference to a removed annotation, and recurse into
    /// an element, dropping it as well when it is a <c>/Form</c> that the recursion emptied.
    /// </summary>
    /// <returns>Whether the kid, or anything under it, changed.</returns>
    private static bool Decide(PdfDictionary kid, ICollection<PdfDictionary> annotations,
        HashSet<PdfDictionary> seen, int depth, out bool drop)
    {
        drop = false;

        var type = kid.Elements.GetName(PdfStructureElement.Keys.Type);
        if (type == "/OBJR")
        {
            drop = kid.Elements.GetDictionary("/Obj") is { } target && annotations.Contains(target);
            return drop;
        }

        if (type == "/MCR" || !kid.Elements.ContainsKey(PdfStructureElement.Keys.S))
            return false;

        var changed = RemoveKids(kid, annotations, seen, depth + 1);
        drop = changed && kid.Elements.GetName(PdfStructureElement.Keys.S) == "/Form" && !HasKids(kid);
        return changed;
    }

    private static bool HasKids(PdfDictionary element) =>
        PdfReference.Dereference(element.Elements[PdfStructureElement.Keys.K]) switch
        {
            PdfArray array => array.Elements.Count > 0,
            null => false,
            _ => true
        };
}
