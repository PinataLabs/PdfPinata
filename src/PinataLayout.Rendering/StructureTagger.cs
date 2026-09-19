using System;
using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Structure;

namespace PinataLayout.Rendering;

/// <summary>
/// Turns what PinataLayout already knows about a document into a PDF structure tree, so that a caller
/// gets an accessible file without tagging anything by hand.
/// </summary>
/// <remarks>
/// <para>
/// The renderer is rendering a <c>Paragraph</c> whose style is <c>Heading1</c>, and throws that away
/// on the way to the page: a heading and a caption both arrive as a <c>Tj</c>. This keeps it. Every
/// renderer that draws something asks here for the element it belongs to and wraps its drawing in
/// the scope that comes back; everything that is furniture rather than content — running heads,
/// folios, cell shading, borders, rules — goes inside <see cref="Artifact"/> instead.
/// </para>
/// <para>
/// <b>An element belongs to a document object, not to a page.</b> That is the whole reason this is a
/// class with a dictionary in it rather than a few calls scattered through the renderers. A paragraph
/// broken over a page boundary is one paragraph, and a table heading repeated at the top of every
/// page the table runs onto is one heading. Keyed by the DOM object and reused, they come out that
/// way; created per render pass they would come out as two paragraphs and five headings, and a reader
/// would announce them as such.
/// </para>
/// <para>
/// <b>Structural nesting is not drawing nesting.</b> A table is rows and cells, and it is drawn as
/// shading, then content, then borders, cell by cell through a flat list. So the tree is built by
/// naming a parent rather than by nesting <c>using</c> blocks, and the parent that a renderer drawing
/// something finds is whatever the renderer that called it made current.
/// </para>
/// </remarks>
internal sealed class StructureTagger
{
    /// <summary>
    /// A scope that closes nothing, handed back whenever tagging is off or the graphics being drawn
    /// into is not a page. Every call site is a <c>using</c> either way, so that the decision lives
    /// here and not at forty call sites.
    /// </summary>
    internal static readonly IDisposable Nothing = new NullScope();

    readonly Dictionary<ElementKey, PdfStructureElement> _elements =
        new Dictionary<ElementKey, PdfStructureElement>();

    readonly Stack<PdfStructureElement> _parents = new Stack<PdfStructureElement>();

    PdfDocument _document;
    PdfStructureElement _root;

    // The list, or run of nested lists, currently being gathered - one frame per level of nesting,
    // deepest on top. See ListItem for how a level compared against the frame on top decides whether
    // an item continues the current list, starts a sibling of it, opens one nested inside the last
    // item, or closes one or more before landing.
    readonly Stack<ListFrame> _listFrames = new Stack<ListFrame>();

    /// <summary>
    /// One level of a run of nested lists: the <c>/L</c> itself, what it hangs from, the type its
    /// items share, and the last item added to it - a list one level deeper attaches to that item's
    /// body, per the standard putting a nested list inside its enclosing item's body.
    /// </summary>
    sealed class ListFrame
    {
        internal PdfStructureElement List;
        internal PdfStructureElement Parent;
        internal ListType Type;
        internal int Level;
        internal PdfStructureElement LastItem;
        internal object LastItemKey;
    }

    /// <summary>
    /// Which of a list item's elements is which, beyond the <c>/LI</c> itself (slot 0). Shared with
    /// <c>ParagraphRenderer</c>, which builds the <c>/Lbl</c> and reuses the <c>/LBody</c> this
    /// creates - the same slot number is what makes the second ask return the first ask's element
    /// rather than a new one.
    /// </summary>
    internal const int ListBodySlot = 2;

    /// <summary>
    /// Whether to tag at all. Set from <see cref="DocumentRenderer.TagContent"/>.
    /// </summary>
    internal bool Enabled { get; set; } = true;

    /// <summary>
    /// The language the document is written in, as an RFC 3066 tag. A reader that does not know it
    /// cannot choose a voice, so PDF/UA requires it.
    /// </summary>
    internal string Language { get; set; }

    /// <summary>
    /// The element that anything tagged now becomes a child of.
    /// </summary>
    /// <remarks>
    /// A parent to build a new element under — not the element a scope you just opened returned,
    /// which a refused scope (one opened inside an artifact) never pushes here. Ask for that through
    /// the <c>out</c> parameter of <see cref="Block(XGraphics,object,PdfTag,out PdfStructureElement)"/>
    /// or <see cref="Container(XGraphics,object,PdfTag,out PdfStructureElement)"/> instead.
    /// </remarks>
    internal PdfStructureElement Parent => _parents.Count > 0 ? _parents.Peek() : _root;

    /// <summary>
    /// Whether the graphics being drawn into can carry marks — which it cannot when tagging is off,
    /// nor when it is a measuring context or a form rather than a page, nor before a page has been
    /// begun.
    /// </summary>
    /// <remarks>
    /// That last condition is the one that is easy to miss. <see cref="_document"/> is assigned by
    /// <see cref="BeginPage"/> and everything here is built against it, but nothing obliges a caller
    /// to begin a page: <see cref="DocumentRenderer.RenderObject"/> draws a single object onto a
    /// page-backed surface and never does. Tagging looked possible on that path — enabled, and a real
    /// page to mark — and a list paragraph reached <c>_document.Structure</c> with nothing there.
    /// Asked here rather than at each caller, because "no page has been begun" is one condition and
    /// not five.
    /// </remarks>
    bool CanTag(XGraphics gfx) => Enabled && gfx != null && gfx.PdfPage != null && _document != null;

    /// <summary>
    /// Whether content may be tagged here, which it may not once anything has declared itself
    /// furniture.
    /// </summary>
    /// <remarks>
    /// <b>An artifact is not a container that its contents can opt out of.</b> A running head is
    /// drawn by the same paragraph renderer as body text is, and that renderer tags what it draws —
    /// so without this the header's paragraphs appear in the tree as paragraphs, inside an
    /// <c>/Artifact</c> sequence saying they are not content. A reader following the tree reads the
    /// running head aloud on every page, which is the exact failure marking it as an artifact was
    /// meant to prevent.
    /// </remarks>
    bool CanTagContent(XGraphics gfx) => CanTag(gfx) && _artifactDepth == 0;

    int _artifactDepth;

    /// <summary>
    /// Brings a page into the tree whether or not anything on it is tagged, and settles the
    /// document-level entries the first time a page is seen.
    /// </summary>
    /// <remarks>
    /// A page that draws nothing still has to be in the tree, or it cannot be told apart from one
    /// imported out of an untagged document — and a validator is right to object to both.
    /// </remarks>
    internal void BeginPage(XGraphics gfx)
    {
        // Its own test rather than CanTag, which now asks whether a page has been begun — and this
        // is the method that begins one.
        if (!Enabled || gfx == null || gfx.PdfPage == null)
            return;

        var page = gfx.PdfPage;
        if (_document == null)
        {
            _document = page.Owner;

            if (!string.IsNullOrEmpty(Language))
                _document.Structure.Language = Language;

            _root = _document.Structure.CreateElement(PdfTag.Document);
        }
        else if (!ReferenceEquals(_document, page.Owner))
        {
            // One tagger belongs to one document. Two would share a cache of elements built against
            // the first document's object table, and hanging those off the second produces a tree
            // full of references to objects that are not in the file.
            throw new InvalidOperationException(
                "This renderer has already tagged pages of a different PdfDocument. A DocumentRenderer "
                + "renders one document; use a second one, or set TagContent to false.");
        }

        _document.Structure.RegisterPage(page);
    }

    /// <summary>
    /// Marks everything drawn in the scope as belonging to a new element of the given type, a child
    /// of whatever is current, and makes it current in turn.
    /// </summary>
    /// <param name="gfx">The surface being drawn on, which the marks are written into.</param>
    /// <param name="key">
    /// The DOM object the element stands for. Asked for twice — because the object was drawn on two
    /// pages, or in a table heading repeated on each — the same element comes back and gains a second
    /// run of marks, which is what makes it one thing in the tree rather than two.
    /// </param>
    /// <param name="tag">What kind of element it is.</param>
    internal IDisposable Block(XGraphics gfx, object key, PdfTag tag)
        => Block(gfx, key, tag, out _);

    /// <inheritdoc cref="Block(XGraphics,object,PdfTag)"/>
    /// <param name="gfx">The surface being drawn on, which the marks are written into.</param>
    /// <param name="key">The DOM object the element stands for.</param>
    /// <param name="tag">What kind of element it is.</param>
    /// <param name="element">
    /// The element that was opened, or null if none was — which is the answer whenever tagging is
    /// off, no page has been begun, or this is inside an artifact.
    /// </param>
    /// <remarks>
    /// For a caller with something to write onto the element it just opened. It must not read
    /// <see cref="Parent"/> for that: a scope that was refused pushed nothing, so Parent still
    /// names whatever was current before — the enclosing paragraph, the section, the document — and
    /// the caller's alternate text or summary lands on that instead. A figure in a running head
    /// reaches exactly that case, because a header is drawn inside an artifact and an artifact does
    /// not change what is current.
    /// </remarks>
    internal IDisposable Block(XGraphics gfx, object key, PdfTag tag, out PdfStructureElement element)
    {
        element = null;
        if (!CanTagContent(gfx))
            return Nothing;

        element = Element(key, tag, ParentFor(key));
        return Marks(gfx, element);
    }

    /// <summary>
    /// Makes a new element of the given type current for the scope, without giving it any marks of
    /// its own.
    /// </summary>
    /// <remarks>
    /// For an element that holds other elements and draws nothing itself — a table, a row, a list.
    /// Giving one an identifier of its own would claim that something on the page belongs directly to
    /// the table rather than to one of its cells, which is a claim about reading order and a false
    /// one.
    /// </remarks>
    internal IDisposable Container(XGraphics gfx, object key, PdfTag tag)
        => Container(gfx, key, tag, out _);

    /// <inheritdoc cref="Container(XGraphics,object,PdfTag)"/>
    /// <param name="gfx">The surface being drawn on.</param>
    /// <param name="key">The DOM object the element stands for.</param>
    /// <param name="tag">What kind of element it is.</param>
    /// <param name="element">
    /// The element that was opened, or null if none was. See
    /// <see cref="Block(XGraphics,object,PdfTag,out PdfStructureElement)"/> for why a caller with
    /// something to write onto it must ask this way rather than read <see cref="Parent"/>.
    /// </param>
    internal IDisposable Container(XGraphics gfx, object key, PdfTag tag, out PdfStructureElement element)
    {
        element = null;
        if (!CanTagContent(gfx))
            return Nothing;

        element = Element(key, tag, ParentFor(key));
        return Enter(element);
    }

    /// <summary>
    /// Marks everything drawn in the scope as belonging to an element built earlier, and makes it
    /// current.
    /// </summary>
    internal IDisposable Marks(XGraphics gfx, PdfStructureElement element)
    {
        if (!CanTagContent(gfx) || element == null)
            return Nothing;

        var scope = gfx.BeginMarkedContent(element);
        _parents.Push(element);
        return new Scope(this, scope, popsParent: true);
    }

    /// <summary>
    /// Makes an element current for the scope without opening a marked-content sequence.
    /// </summary>
    internal IDisposable Enter(PdfStructureElement element)
    {
        if (element == null)
            return Nothing;

        _parents.Push(element);
        return new Scope(this, null, popsParent: true);
    }

    /// <summary>
    /// Marks everything drawn in the scope as furniture: on the page, and not part of what the page
    /// says.
    /// </summary>
    /// <remarks>
    /// Half the rule rather than a tidiness measure. Everything on a page is either content or an
    /// artifact, and something that is neither is the first thing a validator objects to — so the
    /// running head, the folio, the shading behind a table and every border go through here.
    /// </remarks>
    internal IDisposable Artifact(XGraphics gfx)
    {
        if (!CanTag(gfx))
            return Nothing;

        // A second sequence inside the first would say nothing the first has not said already, and
        // the depth is what the content scopes read to know they are inside furniture. So the inner
        // one counts and writes nothing.
        var marks = _artifactDepth == 0 ? gfx.BeginArtifact() : null;
        _artifactDepth++;
        return new Scope(this, marks, popsParent: false, leavesArtifact: true);
    }

    /// <summary>
    /// The element for a document object, built the first time it is asked for and handed back
    /// unchanged afterwards.
    /// </summary>
    /// <param name="key">The DOM object the element stands for.</param>
    /// <param name="tag">What kind of element it is.</param>
    /// <param name="parent">The element it hangs beneath, the first time it is built.</param>
    /// <param name="slot">
    /// Which of the object's elements is wanted, for the few objects that need more than one. A list
    /// paragraph is one object holding a label and a body, and the two have to be told apart by
    /// something that outlives the renderer drawing them — a renderer is built afresh for each page a
    /// split paragraph appears on, so anything belonging to the renderer would file the second half
    /// of the paragraph under a new element and read it out as a second paragraph.
    /// </param>
    internal PdfStructureElement Element(object key, PdfTag tag, PdfStructureElement parent, int slot = 0)
    {
        // Nothing to build against until a page has been begun, which is also to say: whenever
        // tagging is off — and nothing to build at all inside furniture. Every caller treats a null
        // element as "do not tag this", so both checks live here rather than at each of them.
        if (_document == null || _artifactDepth > 0)
            return null;

        if (key == null)
            return _document.Structure.CreateElement(tag, parent ?? _root);

        var identity = new ElementKey(key, slot);
        if (_elements.TryGetValue(identity, out var existing))
            return existing;

        var element = _document.Structure.CreateElement(tag, parent ?? _root);
        _elements[identity] = element;
        return element;
    }

    /// <summary>
    /// Which element of which document object. Compared by identity rather than by value, because
    /// several DOM types override <c>Equals</c> to compare by value and two empty paragraphs of the
    /// same style are equal without being the same paragraph.
    /// </summary>
    readonly struct ElementKey : IEquatable<ElementKey>
    {
        readonly object _owner;
        readonly int _slot;

        internal ElementKey(object owner, int slot)
        {
            _owner = owner;
            _slot = slot;
        }

        public bool Equals(ElementKey other) =>
            ReferenceEquals(_owner, other._owner) && _slot == other._slot;

        public override bool Equals(object obj) => obj is ElementKey other && Equals(other);

        public override int GetHashCode() =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_owner) * 397 ^ _slot;
    }

    /// <summary>
    /// The list item a list paragraph belongs to, gathering consecutive paragraphs of the same kind
    /// into one list and, per <paramref name="level"/>, one list nested inside another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PinataLayout has no list object — a list is however many paragraphs in a row happen to carry a
    /// <c>ListInfo</c>, and the run is only visible from the order they are rendered in. So the run
    /// is tracked here and broken by <see cref="EndList"/> the moment anything else is tagged at the
    /// same level. That reads a change of list type as a new list, which matches what the page looks
    /// like.
    /// </para>
    /// <para>
    /// <paramref name="level"/> is compared against the run's open frames, deepest on top: deeper
    /// opens a list nested inside the previous item's body; shallower closes frames until one is
    /// found whose level is no greater than this item's; equal continues that frame unless the type
    /// changed, which starts a sibling of it in the same place. A skipped level — three straight
    /// after one, with nothing in between — lands exactly like a plain step, because it is compared
    /// only as deeper or shallower and never against the numbers themselves: refusing it would fail a
    /// document over a cosmetic inconsistency, and inventing the missing level would tag something
    /// the page has nothing corresponding to.
    /// </para>
    /// </remarks>
    internal PdfStructureElement ListItem(XGraphics gfx, Paragraph paragraph, ListType type, int level)
    {
        if (!CanTagContent(gfx))
            return null;

        var ambientParent = ParentFor(paragraph);

        if (_listFrames.Count == 0)
        {
            OpenList(type, level, ambientParent);
        }
        else
        {
            while (_listFrames.Count > 1 && level < _listFrames.Peek().Level)
                _listFrames.Pop();

            var top = _listFrames.Peek();

            if (level < top.Level)
            {
                // Shallower than even the outermost open frame: nothing here still applies.
                _listFrames.Clear();
                OpenList(type, level, ambientParent);
            }
            else if (level == top.Level)
            {
                // The outermost frame hangs off wherever the ambient context says it should; a
                // nested one always hangs off the same fixed parent it was opened with, so this
                // comparison only ever finds a mismatch at the outermost level - exactly where a
                // change of surrounding context matters.
                var desiredParent = _listFrames.Count == 1 ? ambientParent : top.Parent;
                if (type != top.Type || !ReferenceEquals(top.Parent, desiredParent))
                {
                    var parent = top.Parent;
                    _listFrames.Pop();
                    OpenList(type, level, parent);
                }
            }
            else
            {
                // Deeper: nested inside the body of the item the enclosing frame last added, which
                // is where the standard puts a nested list. top.LastItem is never null here - a frame
                // only ever becomes "top" after a ListItem call set it - so the body already exists by
                // now, made when that item finished rendering, Lbl and LBody both. Asking for it
                // rather than building it here matters: building it here would give an item's body a
                // child before its own label, and every list would come out with LBody ahead of Lbl
                // instead of behind it.
                var enclosingBody = Element(top.LastItemKey, PdfTag.LBody, top.LastItem, ListBodySlot);
                OpenList(type, level, enclosingBody);
            }
        }

        var frame = _listFrames.Peek();
        var item = Element(paragraph, PdfTag.LI, frame.List);
        frame.LastItem = item;
        frame.LastItemKey = paragraph;
        return item;
    }

    /// <summary>
    /// Opens a new nested-list frame as the run's current, deepest level.
    /// </summary>
    void OpenList(ListType type, int level, PdfStructureElement parent)
    {
        var list = _document.Structure.CreateElement(PdfTag.L, parent);

        // Every level says how it is numbered, not just the outermost one: PDF/UA-2 asks it of each
        // /L element, and a nested list is an /L of its own rather than a continuation of its parent.
        list.SetListNumbering(ListNumberingOf(type));

        _listFrames.Push(new ListFrame
        {
            List = list,
            Parent = parent,
            Type = type,
            Level = level
        });
    }

    /// <summary>
    /// The list-numbering scheme a <c>ListType</c> announces itself as. PinataLayout does not vary the
    /// bullet glyph by level, so every bulleted level reads the same; every numbered level reads as
    /// plain decimal, which is what PinataLayout actually renders.
    /// </summary>
    static PdfListNumbering ListNumberingOf(ListType type) => type switch
    {
        ListType.BulletList1 or ListType.BulletList2 or ListType.BulletList3 => PdfListNumbering.Disc,
        ListType.NumberList1 or ListType.NumberList2 or ListType.NumberList3 => PdfListNumbering.Decimal,
        _ => PdfListNumbering.None
    };

    /// <summary>
    /// Ends the run of list paragraphs, so that the next one starts a new list. Called by every
    /// renderer that tags something which is not a list item.
    /// </summary>
    internal void EndList()
    {
        _listFrames.Clear();
    }

    /// <summary>
    /// Marks the raised mark in the body text as the citation of a footnote, and builds the note's own
    /// element beside it so that the note reads where it is cited rather than where it is drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A footnote is drawn in two places by two different renderers: the mark by
    /// <c>ParagraphRenderer</c>, in the middle of a line, and the note itself by
    /// <c>FootnoteRenderer</c>, in a band at the foot of the page. Where the <c>/Note</c> is created
    /// decides where a screen reader hears it, and drawing order would put every note after all the
    /// body text, severed from the sentence that cited it. So it is created here, at the citation,
    /// as a sibling of the <c>/Reference</c> and a child of the paragraph — both are inline-level
    /// structure types, which is exactly where ISO 32000-1 puts them — and
    /// <see cref="FootnoteFor"/> hands the same element back to the other renderer later.
    /// </para>
    /// <para>
    /// Three elements keyed by one <see cref="Footnote"/>, which is what the slots are for. The key
    /// has to outlive the renderer: a paragraph split across a page break is drawn by a renderer
    /// built afresh for each page, and anything belonging to the renderer would file the second half
    /// under a new element.
    /// </para>
    /// </remarks>
    internal IDisposable FootnoteReference(XGraphics gfx, Footnote footnote)
    {
        if (!CanTagContent(gfx))
            return Nothing;

        var parent = ParentFor(footnote);
        var reference = Element(footnote, PdfTag.Reference, parent, ReferenceSlot);

        // Built now rather than when the note is drawn, and built before the scope is entered so that
        // it hangs off the paragraph beside the reference rather than inside it.
        NoteFor(footnote, parent);

        return Marks(gfx, reference);
    }

    /// <summary>
    /// The element for a footnote's own content, as created at the point the note was cited. Null when
    /// nothing is being tagged.
    /// </summary>
    /// <remarks>
    /// A note whose mark was suppressed — a caller can set <see cref="Footnote.Reference"/> to an empty
    /// string — was never cited anywhere, so there is no citation to hang it from and it is built here
    /// against whatever is current instead. That is the honest answer rather than a good one: a note
    /// nothing refers to has no place in the reading order, because nothing reads to it.
    /// </remarks>
    internal PdfStructureElement FootnoteFor(XGraphics gfx, Footnote footnote)
        => !CanTagContent(gfx) ? null : NoteFor(footnote, ParentFor(footnote));

    /// <summary>
    /// The footnote's element, made on first ask and given the identifier ISO 14289-1 requires of a
    /// note. Handed back unchanged afterwards, however many times either renderer asks.
    /// </summary>
    PdfStructureElement NoteFor(Footnote footnote, PdfStructureElement parent)
    {
        var identity = new ElementKey(footnote, NoteSlot);
        if (_elements.TryGetValue(identity, out var existing))
            return existing;

        var note = Element(footnote, PdfTag.Note, parent, NoteSlot);
        if (note != null)
        {
            // The caller's own identifier where they set one, because a note whose name has to mean
            // something outside this document cannot be given a generated one. Otherwise numbered
            // from one in the order the notes are cited, which is the order they are built in. The
            // prefix keeps the generated names clear of a caller's; two elements under one name is
            // refused when the identifier tree is written, whichever of them chose it.
            //
            // The counter advances either way, so that setting one note's identifier does not
            // renumber the notes around it.
            var generated = ++_notes;
            note.Id = footnote.Identifier.Length > 0 ? footnote.Identifier : "note" + generated;
        }

        return note;
    }

    /// <summary>
    /// Marks the note's own mark, drawn in the gutter at the foot of the page, as the label of the
    /// note it belongs to.
    /// </summary>
    /// <remarks>
    /// A label rather than a second <see cref="PdfTag.Reference"/>: this mark points nowhere, it names
    /// the note it sits beside. The one in the body text is the pointer.
    /// </remarks>
    internal IDisposable FootnoteLabel(XGraphics gfx, Footnote footnote, PdfStructureElement note)
    {
        if (!CanTagContent(gfx) || note == null)
            return Nothing;

        return Marks(gfx, Element(footnote, PdfTag.Lbl, note, LabelSlot));
    }

    /// <summary>Which of a footnote's elements is which. See <see cref="Element"/> on slots.</summary>
    const int ReferenceSlot = 0;

    const int NoteSlot = 1;

    const int LabelSlot = 2;

    /// <summary>
    /// How many notes have been given an identifier, which is what makes the next one unique.
    /// </summary>
    int _notes;

    /// <summary>
    /// Joins a link annotation to its element, so that the link is reachable by a reader walking the
    /// structure and not only by one hit-testing rectangles.
    /// </summary>
    internal void AddAnnotation(XGraphics gfx, PdfStructureElement element, PdfDictionary annotation)
    {
        if (!CanTagContent(gfx) || element == null || annotation == null)
            return;

        _document.Structure.AddAnnotation(element, gfx.PdfPage, annotation);
    }

    /// <summary>
    /// What a new element for this object hangs off: whatever renderer is drawing it, or — for
    /// anything at the top of the flow — the section it came from.
    /// </summary>
    PdfStructureElement ParentFor(object key)
    {
        if (_parents.Count > 0)
            return _parents.Peek();

        var section = SectionOf(key as DocumentObject);
        return section != null ? Element(section, PdfTag.Section, _root) : _root;
    }

    /// <summary>
    /// The section a document object came out of, found by walking up the DOM.
    /// </summary>
    /// <remarks>
    /// Only meaningful for something at the top of the flow. A paragraph inside a table cell would
    /// answer with the section the table is in, which is true and useless — but that paragraph never
    /// reaches here, because the cell it is in is already current.
    /// </remarks>
    static Section SectionOf(DocumentObject documentObject)
    {
        var current = documentObject;
        while (current != null)
        {
            if (current is Section section)
                return section;

            current = DocumentRelations.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// Closes a marked-content sequence and gives back the parent it displaced.
    /// </summary>
    sealed class Scope : IDisposable
    {
        readonly StructureTagger _tagger;
        readonly IDisposable _marks;
        readonly bool _popsParent;
        readonly bool _leavesArtifact;
        bool _closed;

        internal Scope(StructureTagger tagger, IDisposable marks, bool popsParent,
            bool leavesArtifact = false)
        {
            _tagger = tagger;
            _marks = marks;
            _popsParent = popsParent;
            _leavesArtifact = leavesArtifact;
        }

        public void Dispose()
        {
            if (_closed)
                return;

            _closed = true;

            // The marks first and the rest second, mirroring the order they were taken in. An
            // unbalanced BDC corrupts every mark after it on the page, so this runs from a using
            // rather than from a matched pair of calls that an early return could step over.
            _marks?.Dispose();

            if (_popsParent)
                _tagger._parents.Pop();

            if (_leavesArtifact)
                _tagger._artifactDepth--;
        }
    }

    sealed class NullScope : IDisposable
    {
        public void Dispose() { }
    }
}
