using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.Extraction;

namespace PdfPinata.Pdf.Structure;

/// <summary>
/// Re-tags what flattening a form draws: gives each drawn widget appearance a marked-content
/// identifier, puts a marked-content reference to it where the widget's <c>/OBJR</c> was, and keeps
/// the parent tree in step. The counterpart of <see cref="PdfStructureAnnotationRemover"/> for a
/// widget whose appearance is staying on the page as content.
/// </summary>
/// <remarks>
/// <para>
/// A widget is content of the element that holds its <c>/OBJR</c>: a <c>/Form</c> element, or a
/// type the role map maps to one. Once the appearance is drawn into the page and the annotation is
/// gone, that content is a mark on the page and the element has to say so, or the drawn value is
/// neither tagged nor an artifact and the <c>/OBJR</c> names something that is not there. Three
/// things change, and they change together because a reader follows each of them: the marked-content
/// sequence in the page's content stream carries an <c>/MCID</c> (the caller writes it, having been
/// handed the number by <see cref="TryBegin"/>), the element's kid becomes a marked-content
/// reference to it, and the parent tree maps the identifier back to the element.
/// </para>
/// <para>
/// <strong>The element stays and becomes a <c>/Span</c>.</strong> A <c>/Form</c> is a form control,
/// and what is on the page is no longer one; it is text, or a mark, in the running content. It is
/// retyped rather than dropped because the value drawn is content that has to be in the tree, and
/// the element is where it belongs - in the reading order, where the control was. A type that is not
/// a form control (an element that held the widget as one thing among others) keeps its type and
/// only has its kid replaced. The <c>/TU</c> of the field becomes the element's <c>/Alt</c> where it
/// has none, so the control's name is not lost with the control.
/// </para>
/// <para>
/// <strong>This runs before <see cref="PdfStructureAnnotationRemover"/>, and that is what keeps the
/// remover from pruning an element this has re-pointed.</strong> The remover drops an <c>/OBJR</c> to
/// a removed annotation, and a <c>/Form</c> element it leaves empty. By the time it runs, every drawn
/// widget's <c>/OBJR</c> is already a marked-content reference, so it finds nothing of theirs to drop
/// and nothing to prune; and the element is no longer a <c>/Form</c> either. A widget that was not
/// drawn - hidden, or with no appearance - still has its <c>/OBJR</c>, which the remover drops, and
/// its <c>/Form</c> element if that was all it held, which is right: it draws nothing and stands for
/// nothing. The parent tree entries for the drawn widgets are removed here, in the same write that
/// adds the new ones.
/// </para>
/// <para>
/// It works on whichever tree the document has. One built in this session takes its identifiers from
/// the builder, which also writes the parent tree at save; one read from a file is the plain
/// dictionaries under the catalog's <c>/StructTreeRoot</c>, and its identifiers are numbered after
/// the page's existing parent tree array and after anything the page's content stream already says.
/// </para>
/// </remarks>
internal sealed class PdfStructureFlattenTagger
{
    private readonly PdfDocument _document;
    private readonly PdfDictionary _root;
    private readonly PdfStructureBuilder _builder;
    private readonly Dictionary<string, string> _roles;
    private readonly Func<PdfDictionary, string> _alternateTextOf;
    private readonly ICollection<PdfDictionary> _widgets;
    private readonly Dictionary<PdfDictionary, Hit> _hits = new();
    private readonly List<Pending> _pending = [];
    private readonly Dictionary<PdfPage, PageNumbering> _numbering = new();
    private readonly Dictionary<PdfPage, HashSet<int>> _usedOnPage = new();
    private int _nextKey = -1;

    private PdfStructureFlattenTagger(PdfDocument document, PdfDictionary root, ICollection<PdfDictionary> widgets,
        Func<PdfDictionary, string> alternateTextOf)
    {
        _widgets = widgets;
        _document = document;
        _root = root;
        _builder = document.TaggedStructure;
        _roles = PdfStructureAnnotationRemover.RoleMapOf(root);
        _alternateTextOf = alternateTextOf;
    }

    /// <summary>
    /// Finds where each widget is held in the document's structure tree.
    /// </summary>
    /// <param name="document">The document the widgets belong to.</param>
    /// <param name="widgets">
    /// The widget dictionaries, each as its reference answers it - the field, for a widget that is
    /// merged with one.
    /// </param>
    /// <param name="alternateTextOf">
    /// What to say in place of a widget's control, for the element that held it: the field's
    /// <c>/TU</c>. Answers null or empty for none.
    /// </param>
    /// <returns>The tagger, or null when the document has no structure tree.</returns>
    /// <exception cref="InvalidOperationException">
    /// A tree read from a file, and a page that lists one of the widgets has content that cannot be
    /// read, so the identifiers it already uses cannot be known. Nothing has been changed.
    /// </exception>
    internal static PdfStructureFlattenTagger Open(PdfDocument document, ICollection<PdfDictionary> widgets,
        Func<PdfDictionary, string> alternateTextOf)
    {
        var root = PdfStructureAnnotationRemover.RootOf(document);
        if (root == null)
            return null;

        var tagger = new PdfStructureFlattenTagger(document, root, widgets, alternateTextOf);
        tagger.Find(root, widgets, new HashSet<PdfDictionary>(), 0);
        tagger.ReadPagesThatListAWidget(widgets);
        return tagger;
    }

    /// <summary>
    /// Asks for an identifier to draw a widget's appearance with.
    /// </summary>
    /// <param name="page">The page the appearance is drawn on.</param>
    /// <param name="widget">The widget, as its reference answers it.</param>
    /// <param name="mcid">The identifier, unique on the page, to put in the sequence's <c>/MCID</c>.</param>
    /// <param name="tag">
    /// The structure type the element will have once this is committed, to open the sequence with:
    /// <c>/Span</c> for a form control, which is retyped, else the element's own type, so that the
    /// sequence and its element agree.
    /// </param>
    /// <returns>
    /// False when no element of the tree holds the widget, so there is nothing to attach the drawing
    /// to; the caller marks it as an artifact instead, which is what a drawing that is not content of
    /// anything is.
    /// </returns>
    internal bool TryBegin(PdfPage page, PdfDictionary widget, out int mcid, out string tag)
    {
        mcid = -1;
        tag = null;
        if (!_hits.TryGetValue(widget, out var hit))
            return false;

        if (_builder != null)
        {
            // A tree built this session is made of these, and only these.
            if (hit.Element is not PdfStructureElement element)
                return false;

            mcid = _builder.AllocateMarkedContent(page, element);
        }
        else
        {
            var numbering = NumberingOf(page);
            mcid = numbering.Take();
            numbering.Elements[mcid] = hit.Element;
        }

        tag = BecomesSpan(hit.Element) ? PdfTag.Span.Name : WritableTagOf(hit.Element);
        _pending.Add(new Pending(page, widget, hit, mcid));
        return true;
    }

    /// <summary>
    /// Writes what the drawing decided into the tree: the marked-content references, the retyped
    /// elements, and one write of the parent tree for the lot.
    /// </summary>
    internal void Commit()
    {
        if (_pending.Count == 0)
            return;

        var elements = new Dictionary<PdfDictionary, List<Pending>>();
        foreach (var pending in _pending)
        {
            Place(pending.Hit, PdfStructureElement.MarkedContentKid(pending.Hit.Element, pending.Page, pending.Mcid));

            if (!elements.TryGetValue(pending.Hit.Element, out var list))
                elements[pending.Hit.Element] = list = [];
            list.Add(pending);
        }

        foreach (var pair in elements)
            Retype(pair.Key, pair.Value);

        // The annotations' own keys: the widgets are about to go, and the entries under their
        // /StructParent with them. The remover that runs afterwards finds the keys already gone from
        // the widgets, and so has nothing to write.
        var keys = new List<int>();
        foreach (var pending in _pending)
        {
            if (!pending.Widget.Elements.ContainsKey(PdfStructureAnnotationRemover.StructParent))
                continue;

            keys.Add(pending.Widget.Elements.GetInteger(PdfStructureAnnotationRemover.StructParent));
            pending.Widget.Elements.Remove(PdfStructureAnnotationRemover.StructParent);
        }

        _builder?.ForgetAnnotationKeys(keys);

        var additions = new List<KeyValuePair<int, PdfItem>>();
        foreach (var numbering in _numbering.Values)
            numbering.WriteInto(_document, additions);

        if (keys.Count == 0 && additions.Count == 0)
            return;

        var parentTree = PdfStructureAnnotationRemover.ParentTreeOf(_root);
        if (parentTree == null)
        {
            parentTree = new PdfNumberTreeNode(_document);
            _document._irefTable.Add(parentTree);
            _root.Elements[PdfStructureTreeRoot.Keys.ParentTree] = parentTree.Reference;
        }

        // One write: the tree is written again whole each time, and one of several nodes makes new
        // ones each time.
        parentTree.Update(keys, additions);
    }

    // ----- finding -----------------------------------------------------------------------------------------

    private void Find(PdfDictionary parent, ICollection<PdfDictionary> widgets, HashSet<PdfDictionary> seen, int depth)
    {
        if (depth > PdfStructureAnnotationRemover.MaxTreeDepth || !seen.Add(parent))
            return;

        foreach (var kid in KidsOf(parent))
        {
            var type = kid.Elements.GetName(PdfStructureElement.Keys.Type);
            if (type == "/OBJR")
            {
                if (kid.Elements.GetDictionary("/Obj") is { } target && widgets.Contains(target) && !_hits.ContainsKey(target))
                    _hits[target] = new Hit(parent, kid);
            }
            else if (type != "/MCR" && kid.Elements.ContainsKey(PdfStructureElement.Keys.S))
            {
                Find(kid, widgets, seen, depth + 1);
            }
        }
    }

    private static List<PdfDictionary> KidsOf(PdfDictionary element)
    {
        var kids = new List<PdfDictionary>();
        switch (PdfReference.Dereference(element.Elements[PdfStructureElement.Keys.K]))
        {
            case PdfArray array:
                for (var idx = 0; idx < array.Elements.Count; idx++)
                {
                    if (array.Elements.GetDictionary(idx) is { } kid)
                        kids.Add(kid);
                }
                break;
            case PdfDictionary only:
                kids.Add(only);
                break;
        }
        return kids;
    }

    // ----- the tree ----------------------------------------------------------------------------------------

    /// <summary>
    /// Puts a content item where the widget's <c>/OBJR</c> is, or at the end when it is gone
    /// already - a widget drawn on a second page has two marks and one <c>/OBJR</c>.
    /// </summary>
    private static void Place(Hit hit, PdfItem kid)
    {
        var element = hit.Element;
        var raw = element.Elements[PdfStructureElement.Keys.K];

        switch (PdfReference.Dereference(raw))
        {
            case PdfArray kids:
                for (var idx = 0; idx < kids.Elements.Count; idx++)
                {
                    if (ReferenceEquals(kids.Elements.GetDictionary(idx), hit.ObjectReference))
                    {
                        kids.Elements[idx] = kid;
                        return;
                    }
                }
                kids.Elements.Add(kid);
                break;

            case PdfDictionary only when ReferenceEquals(only, hit.ObjectReference):
                element.Elements[PdfStructureElement.Keys.K] = kid;
                break;

            case null:
                element.Elements[PdfStructureElement.Keys.K] = kid;
                break;

            default:
                var array = new PdfArray(element.Owner);
                array.Elements.Add(raw);
                array.Elements.Add(kid);
                element.Elements[PdfStructureElement.Keys.K] = array;
                break;
        }
    }

    /// <summary>
    /// Makes a form control element what it is now: a span of the running text, still carrying the
    /// control's name for a reader that announces it.
    /// </summary>
    private void Retype(PdfDictionary element, List<Pending> drawn)
    {
        if (!BecomesSpan(element))
            return;

        if (element is PdfStructureElement built)
            built.Retag(PdfTag.Span);
        else
            element.Elements.SetName(PdfStructureElement.Keys.S, PdfTag.Span.Name);

        if (element.Elements.GetString(PdfStructureElement.Keys.Alt).Length > 0)
            return;

        foreach (var pending in drawn)
        {
            if (_alternateTextOf(pending.Widget) is { Length: > 0 } text)
            {
                element.Elements.SetString(PdfStructureElement.Keys.Alt, text);
                return;
            }
        }
    }

    /// <summary>
    /// Whether the element is a form control that flattening turns into a span: a <c>/Form</c>, or a
    /// type the role map reads as one, holding no annotation but widgets of the form.
    /// </summary>
    /// <remarks>
    /// An element that still holds an annotation after this - a link, say - is not only the control,
    /// and what it is called is not this step's to change. A widget of the form that was not drawn
    /// does not count: it is taken out of the tree next, and the element is what the widgets that
    /// were drawn leave behind.
    /// </remarks>
    private bool BecomesSpan(PdfDictionary element)
    {
        var tag = element.Elements.GetName(PdfStructureElement.Keys.S);
        if (PdfStructureAnnotationRemover.ResolveTag(tag, _roles) != "/Form")
            return false;

        foreach (var kid in KidsOf(element))
        {
            if (kid.Elements.GetName(PdfStructureElement.Keys.Type) == "/OBJR"
                && !(kid.Elements.GetDictionary("/Obj") is { } target && _widgets.Contains(target)))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// The element's own type as a name that can be written into a content stream as it stands, or
    /// <c>/Span</c> for one that cannot.
    /// </summary>
    private static string WritableTagOf(PdfDictionary element)
    {
        var tag = element.Elements.GetName(PdfStructureElement.Keys.S);
        return Regex.IsMatch(tag, @"^/[A-Za-z0-9_.\-]+$") ? tag : PdfTag.Span.Name;
    }

    // ----- numbering, for a tree read from a file ---------------------------------------------------------------

    private PageNumbering NumberingOf(PdfPage page)
    {
        if (_numbering.TryGetValue(page, out var numbering))
            return numbering;

        var key = page.Elements.ContainsKey(PdfPage.Keys.StructParents)
            ? page.Elements.GetInteger(PdfPage.Keys.StructParents)
            : AllocateKey(page);

        var parentTree = PdfStructureAnnotationRemover.ParentTreeOf(_root);
        var existing = parentTree?.GetValue(key) as PdfArray;

        // What the page already uses, from both places a file may say so: the parent tree array, where
        // a null is a free number, and the page's content, which is what the marks really are. An
        // identifier in the content and missing from the array would otherwise be handed out twice.
        var used = new HashSet<int>(_usedOnPage.TryGetValue(page, out var inContent) ? inContent : ReadMcidsOf(page));
        for (var idx = 0; existing != null && idx < existing.Elements.Count; idx++)
        {
            if (PdfReference.Dereference(existing.Elements[idx]) is not (null or PdfNull))
                used.Add(idx);
        }

        numbering = new PageNumbering(key, existing, used);
        _numbering[page] = numbering;
        return numbering;
    }

    /// <summary>
    /// Reads the content of every page that lists one of the widgets, before anything is drawn, so
    /// that content that cannot be read refuses the flatten instead of stopping it half way.
    /// </summary>
    private void ReadPagesThatListAWidget(ICollection<PdfDictionary> widgets)
    {
        // A tree built this session numbers its marks itself, and never reads the page.
        if (_builder != null)
            return;

        foreach (var page in _document.Pages)
        {
            var annotations = page.Elements.GetArray(PdfPage.Keys.Annots);
            for (var idx = 0; annotations != null && idx < annotations.Elements.Count; idx++)
            {
                if (annotations.Elements.GetDictionary(idx) is { } annotation && widgets.Contains(annotation))
                {
                    _usedOnPage[page] = ReadMcidsOf(page);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// A new key for a page that had none, past every key the parent tree and the pages use.
    /// </summary>
    private int AllocateKey(PdfPage page)
    {
        if (_nextKey < 0)
        {
            var next = _root.Elements.ContainsKey(PdfStructureTreeRoot.Keys.ParentTreeNextKey)
                ? _root.Elements.GetInteger(PdfStructureTreeRoot.Keys.ParentTreeNextKey)
                : 0;

            if (PdfStructureAnnotationRemover.ParentTreeOf(_root) is { } parentTree)
            {
                foreach (var key in parentTree.GetKeys())
                    next = Math.Max(next, key + 1);
            }

            foreach (var other in _document.Pages)
            {
                if (other.Elements.ContainsKey(PdfPage.Keys.StructParents))
                    next = Math.Max(next, other.Elements.GetInteger(PdfPage.Keys.StructParents) + 1);
            }

            _nextKey = next;
        }

        var allocated = _nextKey++;
        page.Elements.SetInteger(PdfPage.Keys.StructParents, allocated);
        _root.Elements.SetInteger(PdfStructureTreeRoot.Keys.ParentTreeNextKey, _nextKey);
        return allocated;
    }

    /// <summary>
    /// The <c>/MCID</c> of every marked-content sequence the page's own content opens, read from the
    /// <c>BDC</c> and <c>DP</c> operators by the content parser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By the operators and not by looking for the text <c>/MCID</c>: the parser skips strings,
    /// comments and inline image data, which a search of the bytes does not, so a string that says
    /// "/MCID 100000000" is not an identifier. A property list is read the two ways a producer may
    /// write one, as the text extractor reads them: written out in the operator, or a name looked up
    /// in the page's <c>/Resources /Properties</c>. Forms the page draws number their own marks, so
    /// they are not read.
    /// </para>
    /// <para>
    /// The numbers are used as a set and never as a length. The next identifier is the lowest one
    /// that is free, so a real sequence carrying a huge number takes nothing but its own place, and
    /// the page's array grows by no more than the identifiers there are.
    /// </para>
    /// </remarks>
    private static HashSet<int> ReadMcidsOf(PdfPage page)
    {
        CSequence content;
        try
        {
            content = ContentReader.ReadContent(page);
        }
        catch (ContentReaderException error)
        {
            throw new InvalidOperationException(
                "A page's content cannot be read, so the marked-content identifiers it already uses are "
                + "not known and the flattened form cannot be given ones that are free: " + error.Message,
                error);
        }

        var used = new HashSet<int>();
        for (var index = 0; index < content.Count; index++)
        {
            if (content[index] is not COperator op || op.OpCode.OpCodeName is not (OpCodeName.BDC or OpCodeName.DP))
                continue;

            int? mcid = null;
            if (op.Operands.Count >= 2 && op.Operands[0] is CName)
            {
                var properties = PropertiesNamed(page, op.NameOperand(1));
                if (properties != null && properties.Elements.ContainsKey("/MCID"))
                    mcid = properties.Elements.GetInteger("/MCID");
            }
            else if (index > 0 && content[index - 1] is COperator { OpCode.OpCodeName: OpCodeName.Dictionary } prior
                && prior.Operands.Count >= 2 && prior.Operands[1] is CString inline)
            {
                mcid = InlineMarkedContentProperties.Read(inline.Value).Mcid;
            }

            if (mcid is >= 0)
                used.Add(mcid.Value);
        }
        return used;
    }

    private static PdfDictionary PropertiesNamed(PdfPage page, string name)
    {
        if (name == null)
            return null;

        var category = page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary("/Properties")
            ?? page.Resources?.Elements.GetDictionary("/Properties");
        return category?.Elements.GetDictionary(name);
    }

    private sealed record Hit(PdfDictionary Element, PdfDictionary ObjectReference);

    private sealed record Pending(PdfPage Page, PdfDictionary Widget, Hit Hit, int Mcid);

    /// <summary>
    /// What one page's run of the parent tree is going to be: the array it had, and the elements the
    /// new identifiers belong to.
    /// </summary>
    private sealed class PageNumbering
    {
        private readonly int _key;
        private readonly PdfArray _existing;

        private readonly HashSet<int> _used;
        private int _cursor;

        public PageNumbering(int key, PdfArray existing, HashSet<int> used)
        {
            _key = key;
            _existing = existing;
            _used = used;
        }

        /// <summary>The lowest identifier nothing on the page uses yet.</summary>
        public int Take()
        {
            while (_used.Contains(_cursor))
                _cursor++;

            _used.Add(_cursor);
            return _cursor;
        }

        public SortedDictionary<int, PdfDictionary> Elements { get; } = new();

        /// <summary>
        /// Puts the new identifiers in the page's array, null where an identifier below them is
        /// not anyone's, and adds the entry to <paramref name="additions"/> when the array is new.
        /// </summary>
        public void WriteInto(PdfDocument document, List<KeyValuePair<int, PdfItem>> additions)
        {
            if (Elements.Count == 0)
                return;

            var array = _existing;
            if (array == null)
            {
                array = new PdfArray(document);
                document._irefTable.Add(array);
                additions.Add(new KeyValuePair<int, PdfItem>(_key, array));
            }

            foreach (var pair in Elements)
            {
                while (array.Elements.Count < pair.Key)
                    array.Elements.Add(PdfNull.Value);

                PdfItem value = (PdfItem)pair.Value.Reference ?? pair.Value;
                if (array.Elements.Count == pair.Key)
                    array.Elements.Add(value);
                else
                    array.Elements[pair.Key] = value;
            }
        }
    }
}
