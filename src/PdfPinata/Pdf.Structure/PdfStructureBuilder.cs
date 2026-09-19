using System;
using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Structure;

/// <summary>
/// Builds a document's structure tree as it is drawn, and joins it up at save time.
/// </summary>
/// <remarks>
/// <para>
/// Three things have to line up for a tagged PDF to work, and this owns all three so that they
/// cannot disagree: the marked-content identifiers written into a page's content stream, the
/// structure elements that give them meaning, and the parent tree that takes a reader from one back
/// to the other.
/// </para>
/// <para>
/// Reached through <see cref="PdfDocument.Structure"/>. Asking for it is what makes a document
/// tagged; a document that never does is written exactly as it was before.
/// </para>
/// </remarks>
public sealed class PdfStructureBuilder
{
    readonly PdfDocument _document;
    readonly Dictionary<PdfPage, PageMarks> _pages = new();
    readonly List<KeyValuePair<int, PdfStructureElement>> _annotations = new();

    /// <summary>
    /// The next key to hand out in the parent tree.
    /// </summary>
    /// <remarks>
    /// One counter for pages and annotations both, because they share the tree and their keys have
    /// to be distinct: a page's key resolves to the array of elements its marks belong to, while an
    /// annotation's resolves straight to its own element. Handing an annotation the page's key —
    /// which is what deriving keys from the page count did — points a reader at an array where it
    /// expects an element, so the annotation's place in the structure cannot be found at all.
    /// </remarks>
    int _nextParentKey;

    internal PdfStructureBuilder(PdfDocument document)
    {
        _document = document;
        Root = new PdfStructureTreeRoot(document);
    }

    /// <summary>
    /// Gets the root of the structure tree.
    /// </summary>
    public PdfStructureTreeRoot Root { get; }

    /// <summary>
    /// Gets or sets the language of the document as a whole, as an RFC 3066 tag such as "en-GB".
    /// PDF/UA requires it, and a reader that does not know the language cannot choose a voice.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Creates an element of the given type under the given parent, or under the root when there is
    /// no parent.
    /// </summary>
    public PdfStructureElement CreateElement(PdfTag tag, PdfStructureElement parent = null)
    {
        var element = new PdfStructureElement(_document, tag);
        _document._irefTable.Add(element);

        if (parent == null)
            Root.Add(element);
        else
            parent.Add(element);

        return element;
    }

    /// <summary>
    /// Allocates the next marked-content identifier for a page and records which element it belongs
    /// to. The identifier is an index into that page's run of the parent tree, so it counts from
    /// zero on every page rather than across the document.
    /// </summary>
    internal int AddMarkedContent(PdfPage page, PdfStructureElement element)
    {
        var marks = MarksOf(page);
        var mcid = marks.Elements.Count;
        marks.Elements.Add(element);
        element.AddMarkedContent(page, mcid);
        return mcid;
    }

    /// <summary>
    /// Takes back the identifier handed out last for this page, because the sequence it named was
    /// removed from the content stream without ever holding anything.
    /// </summary>
    /// <remarks>
    /// Safe only for the identifier handed out last, which is all this is ever asked for: a sequence
    /// is taken back the moment it is found to be empty, and nothing can have been tagged in between
    /// because nothing was drawn. Removing one from the middle would renumber every mark after it,
    /// and the numbers are already in the content stream.
    /// </remarks>
    internal void RemoveLastMarkedContent(PdfPage page, PdfStructureElement element)
    {
        if (!_pages.TryGetValue(page, out var marks) || marks.Elements.Count == 0)
            return;

        var last = marks.Elements.Count - 1;
        if (!ReferenceEquals(marks.Elements[last], element))
            return;

        // Both or neither. The index into the page's marks is the identifier, so removing that entry
        // is what makes the next sequence reuse the number - but taking it out while the element
        // still names it would leave the element pointing at a mark that has become somebody else's.
        // The element is asked first, and told which number to look for rather than told to drop
        // whatever it has last.
        if (element.RemoveLastMarkedContent(last))
            marks.Elements.RemoveAt(last);
    }

    /// <summary>
    /// Brings a page into the structure tree without tagging anything on it, giving it the
    /// <c>/StructParents</c> and <c>/Tabs</c> that every page of a tagged document needs.
    /// </summary>
    /// <remarks>
    /// A page acquires those the moment something on it is tagged, so this is only needed for a page
    /// that draws nothing — a blank page between chapters, say. Without it such a page is
    /// indistinguishable from one imported out of an untagged document, and a validator is right to
    /// object to both. Calling it twice for the same page does nothing the second time.
    /// </remarks>
    public void RegisterPage(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        MarksOf(page);
    }

    /// <summary>
    /// Joins an annotation to an element, so that a link is reachable by a reader walking the
    /// structure rather than only by one hit-testing rectangles.
    /// </summary>
    public void AddAnnotation(PdfStructureElement element, PdfPage page, PdfDictionary annotation)
    {
        element.AddObjectReference(page, annotation);

        // An annotation is indexed by the parent tree through its own /StructParent — a key of its
        // own, resolving to a single element rather than to the array a page's key resolves to.
        var key = _nextParentKey++;
        annotation.Elements.SetInteger("/StructParent", key);
        _annotations.Add(new KeyValuePair<int, PdfStructureElement>(key, element));
    }

    PageMarks MarksOf(PdfPage page)
    {
        if (_pages.TryGetValue(page, out var marks))
            return marks;

        marks = new PageMarks(_nextParentKey++);
        _pages[page] = marks;
        page.Elements.SetInteger(PdfPage.Keys.StructParents, marks.StructParents);

        // Written for every tagged page rather than only for one claiming PDF/UA, because it is a
        // mechanical consequence of there being a structure tree and no caller could want otherwise.
        // Unset, the tab key walks the annotations in the order they sit in the array — which is
        // drawing order, which is the order the tree exists to correct.
        page.Elements.SetName(PdfPage.Keys.Tabs, "/S");
        return marks;
    }

    /// <summary>
    /// Hangs the tree off the catalog and fills in the parent tree. Called while the document is
    /// preparing to be saved, because elements may be added until then.
    /// </summary>
    internal void PrepareForSave()
    {
        foreach (var pair in _pages)
        {
            var elements = new PdfArray(_document);
            foreach (var element in pair.Value.Elements)
                elements.Elements.Add(element.Reference);

            _document._irefTable.Add(elements);
            Root.ParentTree.SetValue(pair.Value.StructParents, elements.Reference);
        }

        foreach (var pair in _annotations)
            Root.ParentTree.SetValue(pair.Key, pair.Value.Reference);

        // Greater than every key handed out, pages and annotations alike, because it is what a later
        // revision adding to the tree starts counting from. Derived from the page count it could
        // name a key that is already in use.
        Root.Elements.SetInteger(PdfStructureTreeRoot.Keys.ParentTreeNextKey, _nextParentKey);
        Root.SetIdTree(NamedElements());
        Root.PrepareForSave();

        if (_document.Options.UAConformance == PdfUAConformance.PdfUA2)
            ApplyPdf20Namespace();

        var catalog = _document.Catalog;
        catalog.Elements[PdfCatalog.Keys.StructTreeRoot] = Root.Reference;

        // Saying the document is tagged is a claim about the whole of it, and it is what makes a
        // reader look for the tree at all.
        var markInfo = new PdfDictionary(_document);
        markInfo.Elements.SetBoolean("/Marked", true);
        catalog.Elements[PdfCatalog.Keys.MarkInfo] = markInfo;

        if (!string.IsNullOrEmpty(Language))
            catalog.Elements.SetString(PdfCatalog.Keys.Lang, Language);
    }

    /// <summary>
    /// Retags <c>/Note</c> as <c>/FENote</c>, and puts the root's <c>/Document</c> child and every
    /// retagged element explicitly in the PDF 2.0 structure namespace — ISO 14289-2 clauses 8.2.5.2
    /// and 8.2.5.14.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>/NS</c> only where it is needed, found by trial against veraPDF rather than assumed from
    /// the specification's inheritance rule for namespaces, which did not hold up here: a validator
    /// left unnamespaced held every element down the tree to the PDF 1.7 default regardless of what
    /// an ancestor declared, and held <em>every</em> element explicitly renamespaced to the PDF 2.0
    /// one instead — including <c>/Reference</c>, which that namespace does not recognise as a
    /// standard type but the PDF 1.7 default does.
    /// </para>
    /// <para>
    /// So this sets <c>/NS</c> on exactly the two kinds of element that need it: the root's single
    /// <c>/Document</c> child, which the rule names outright, and <c>/FENote</c>, PDF 2.0's
    /// replacement for <c>/Note</c> and a type the PDF 1.7 default does not recognise. Everything
    /// else — <c>/Sect</c>, <c>/P</c>, <c>/L</c>, <c>/Lbl</c>, <c>/LI</c>, <c>/Reference</c> among
    /// them — is left with no <c>/NS</c> of its own, exactly as it already validated correctly under
    /// PDF/UA-1.
    /// </para>
    /// </remarks>
    void ApplyPdf20Namespace()
    {
        var ns = new PdfDictionary(_document);
        ns.Elements.SetName("/Type", "/Namespace");
        // A namespace name, not an address: nothing is ever fetched from it, and ISO 32000-2 spells
        // it with "http", so that is the only string a reader will recognise.
        #pragma warning disable S5332 // A namespace identifier, never requested; the standard fixes its scheme.
        ns.Elements.SetString("/NS", "http://iso.org/pdf2/ssn");
        #pragma warning restore S5332
        _document._irefTable.Add(ns);

        // ISO 32000-2 14.7.4.1: the root lists every namespace the tree names. An element's /NS is
        // a pointer into this array rather than a declaration of its own, so a namespace that
        // appears only on the element is one a reader has nothing to resolve it against.
        var namespaces = new PdfArray(_document);
        namespaces.Elements.Add(ns.Reference);
        Root.Elements[PdfStructureTreeRoot.Keys.Namespaces] = namespaces;

        if (Root.Elements[PdfStructureTreeRoot.Keys.K] is PdfArray kids && kids.Elements.Count == 1
            && Resolve(kids.Elements[0]) is PdfStructureElement document && document.Tag.Name == "/Document")
        {
            document.Elements[PdfStructureElement.Keys.NS] = ns.Reference;
        }

        RetagNotes(Root.Elements[PdfStructureTreeRoot.Keys.K], ns, 0);
    }

    static void RetagNotes(PdfItem item, PdfDictionary pdf20Namespace, int depth)
    {
        if (item == null || depth > MaxTreeDepth)
            return;

        item = Resolve(item);

        if (item is PdfArray array)
        {
            for (int idx = 0; idx < array.Elements.Count; idx++)
                RetagNotes(array.Elements[idx], pdf20Namespace, depth + 1);
            return;
        }

        if (item is not PdfStructureElement element)
            return;

        if (element.Tag.Name == PdfTag.Note.Name)
        {
            element.Elements.SetName(PdfStructureElement.Keys.S, PdfTag.FootnoteOrEndnote.Name);
            element.Elements[PdfStructureElement.Keys.NS] = pdf20Namespace.Reference;
        }

        RetagNotes(element.Elements[PdfStructureElement.Keys.K], pdf20Namespace, depth + 1);
    }

    static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    /// <summary>
    /// Every element that carries an <see cref="PdfStructureElement.Id"/>, sorted by it, ready to be
    /// written as the <c>/IDTree</c>.
    /// </summary>
    /// <remarks>
    /// Found by walking the tree rather than recorded as identifiers are handed out, so that an
    /// identifier a caller set themselves is indexed exactly like one this library generated. The
    /// alternative — a registration call beside the property — is a call somebody will forget, and
    /// forgetting it writes an element nothing can look up.
    /// </remarks>
    List<KeyValuePair<string, PdfStructureElement>> NamedElements()
    {
        var named = new List<KeyValuePair<string, PdfStructureElement>>();
        Collect(Root.Elements[PdfStructureTreeRoot.Keys.K], named, 0);

        // Ordinal, because a reader is entitled to binary-search the tree and compares the keys as
        // bytes.
        named.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));

        for (int idx = 1; idx < named.Count; idx++)
        {
            // Two elements under one name make the index ambiguous, and the loser is silently
            // unreachable. Refused rather than resolved, because which of the two a reader should
            // land on is a question about the document.
            if (string.CompareOrdinal(named[idx - 1].Key, named[idx].Key) == 0)
                throw new InvalidOperationException(
                    "Two structure elements share the identifier '" + named[idx].Key + "'. An "
                    + "identifier is what something else points at, so it has to name one element — "
                    + "give one of them an identifier of its own.");
        }

        return named;
    }

    /// <summary>
    /// Walks the kids of an element, collecting the named ones. Kids hold three different things, and
    /// only a structure element can carry an identifier or have kids of its own.
    /// </summary>
    static void Collect(PdfItem item, List<KeyValuePair<string, PdfStructureElement>> named, int depth)
    {
        // The same guard the readers of a name tree carry, for the same reason: an element made its
        // own ancestor would otherwise be walked forever.
        if (item == null || depth > MaxTreeDepth)
            return;

        if (item is PdfReference reference)
            item = reference.Value;

        if (item is PdfArray array)
        {
            for (int idx = 0; idx < array.Elements.Count; idx++)
                Collect(array.Elements[idx], named, depth + 1);
            return;
        }

        if (item is not PdfStructureElement element)
            return;

        var id = element.Id;
        if (!string.IsNullOrEmpty(id))
            named.Add(new KeyValuePair<string, PdfStructureElement>(id, element));

        Collect(element.Elements[PdfStructureElement.Keys.K], named, depth + 1);
    }

    /// <summary>
    /// How deep into the structure tree to go before giving up on it.
    /// </summary>
    const int MaxTreeDepth = 256;

    /// <summary>
    /// What one page contributes to the parent tree: the key it is filed under, and the elements
    /// its marks belong to, in identifier order.
    /// </summary>
    sealed class PageMarks
    {
        public PageMarks(int structParents) => StructParents = structParents;

        public int StructParents { get; }

        public List<PdfStructureElement> Elements { get; } = new();
    }
}
