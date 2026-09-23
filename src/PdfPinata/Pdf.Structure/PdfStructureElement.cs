namespace PdfPinata.Pdf.Structure;

/// <summary>
/// One node of the structure tree: a heading, a paragraph, a table cell. It says what a piece of
/// content is, and points at the marks on the page that draw it.
/// </summary>
public sealed class PdfStructureElement : PdfDictionary
{
    internal PdfStructureElement(PdfDocument document, PdfTag tag)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/StructElem");
        Elements.SetName(Keys.S, tag.Name);
        Tag = tag;
    }

    /// <summary>
    /// Gets the structure type of this element.
    /// </summary>
    public PdfTag Tag { get; }

    /// <summary>
    /// Gets or sets the text that stands in for this element for a reader who cannot see it. A
    /// figure without it says nothing at all, which is why tagging an image without alternate text
    /// is worse than leaving it an artifact.
    /// </summary>
    public string AlternateText
    {
        get => Elements.GetString(Keys.Alt);
        set => Elements.SetString(Keys.Alt, value ?? "");
    }

    /// <summary>
    /// Gets or sets the text this element's marks really spell, for when the glyphs and the text
    /// disagree — a ligature, or a word broken across a hyphenation point.
    /// </summary>
    public string ActualText
    {
        get => Elements.GetString(Keys.ActualText);
        set => Elements.SetString(Keys.ActualText, value ?? "");
    }

    /// <summary>
    /// Gets or sets the language of this element, when it differs from the document's.
    /// </summary>
    public string Language
    {
        get => Elements.GetString(Keys.Lang);
        set => Elements.SetString(Keys.Lang, value ?? "");
    }

    /// <summary>
    /// Gets or sets the name this element can be referred to by from elsewhere in the document.
    /// Setting it to null or empty removes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most elements need none — the tree already says where they are. What needs one is an element
    /// something else has to point at, and PDF/UA-1 requires it of every <c>/Note</c>, so that a
    /// reader can offer to jump from a footnote's reference mark to the note and back.
    /// </para>
    /// <para>
    /// A byte string rather than a text string, and written raw for that reason: this is a key in the
    /// structure tree root's <c>/IDTree</c>, which a reader compares byte by byte. Re-encoding it as
    /// UTF-16 would change the bytes and stop the key matching the element that owns it.
    /// </para>
    /// </remarks>
    public string Id
    {
        get => Elements.GetString(Keys.ID);
        set
        {
            if (string.IsNullOrEmpty(value))
                Elements.Remove(Keys.ID);
            else
                Elements.SetString(Keys.ID, value, PdfStringEncoding.RawEncoding);
        }
    }

    /// <summary>
    /// Sets the numbering scheme of an <c>/L</c> element — how a reader should announce its items,
    /// bulleted or numbered and in what style. ISO 32000-2 Table 349's "List" attribute owner class;
    /// ISO 14289-2 clause 8.2.5.25 requires it, at any value but <see cref="PdfListNumbering.None"/>,
    /// on every list carrying a <c>/Lbl</c>.
    /// </summary>
    public void SetListNumbering(PdfListNumbering listNumbering)
    {
        var attributes = new PdfDictionary(Owner);
        attributes.Elements.SetName("/O", "/List");
        attributes.Elements.SetName("/ListNumbering", "/" + listNumbering);
        Elements[Keys.A] = attributes;
    }

    /// <summary>
    /// Adds a child element and records this element as its parent. The tree is doubly linked
    /// because a reader walks it in both directions — down to read, up to work out context.
    /// </summary>
    internal void Add(PdfStructureElement child)
    {
        Kids().Elements.Add(child.Reference);
        child.Elements[Keys.P] = Reference;
    }

    /// <summary>
    /// Points this element at a run of marks on a page. The integer is the marked-content
    /// identifier that the <c>BDC</c> in the content stream carries.
    /// </summary>
    /// <remarks>
    /// A bare integer in <c>/K</c> is read against the element's own <c>/Pg</c>, so it can only ever
    /// mean a mark on that one page. An element whose marks are on more than one page is not an edge
    /// case once anything tags automatically — a paragraph broken over a page boundary is one, and so
    /// is a table heading repeated at the top of each page the table continues onto. For those the
    /// standard has a marked-content reference, which carries its own <c>/Pg</c>, and this switches
    /// to one the moment a second page turns up. Writing an integer instead would file the mark under
    /// whichever page happened to be named first, and a reader following it would land on the wrong
    /// page or on nothing at all.
    /// </remarks>
    internal void AddMarkedContent(PdfPage page, int mcid)
    {
        var pg = Elements[Keys.Pg];

        if (pg == null)
        {
            Elements[Keys.Pg] = page.Reference;
            Kids().Elements.Add(new PdfInteger(mcid));
            return;
        }

        if (ReferenceEquals(pg, page.Reference))
        {
            Kids().Elements.Add(new PdfInteger(mcid));
            return;
        }

        // Direct rather than indirect: it is two entries long, it has exactly one referent, and
        // tagging already multiplies the object count enough without a fresh object per mark.
        var reference = new PdfDictionary(Owner);
        reference.Elements.SetName(Keys.Type, "/MCR");
        reference.Elements[MarkedContentKeys.Pg] = page.Reference;
        reference.Elements.SetInteger(MarkedContentKeys.MCID, mcid);
        Kids().Elements.Add(reference);
    }

    /// <summary>
    /// Takes back the content item naming <paramref name="mcid"/>, because the marks it named were
    /// never written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A sequence that is opened and closed with nothing drawn inside it is removed from the content
    /// stream rather than written empty, and its identifier has to go with it: a content item naming
    /// marks that are not in the stream is a hole in the tree, and a reader following it finds
    /// nothing.
    /// </para>
    /// <para>
    /// The identifier is checked rather than the last kid simply dropped, because kids hold three
    /// different things and only one of them is a content item. An element that has since acquired a
    /// child element - a note acquires a label and a paragraph - has that child last, and dropping
    /// it takes the child and everything under it out of the tree.
    /// </para>
    /// </remarks>
    /// <returns>
    /// True when the content item was found last and removed. False leaves the element untouched,
    /// which is what the caller needs to know: the identifier is not this element's to give back, so
    /// nothing else may be given back on its behalf either.
    /// </returns>
    internal bool RemoveLastMarkedContent(int mcid)
    {
        if (Elements[Keys.K] is not PdfArray kids || kids.Elements.Count == 0)
            return false;

        var last = kids.Elements[^1];

        if (last is PdfInteger integer && integer.Value == mcid)
        {
            kids.Elements.RemoveAt(kids.Elements.Count - 1);
            return true;
        }

        if (last is PdfDictionary reference
            && reference.Elements.GetName(Keys.Type) == "/MCR"
            && reference.Elements.GetInteger(MarkedContentKeys.MCID) == mcid)
        {
            kids.Elements.RemoveAt(kids.Elements.Count - 1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Points this element at an annotation, which is content as much as anything drawn is — a link
    /// is unreachable to a screen reader that only walks the marks.
    /// </summary>
    internal void AddObjectReference(PdfPage page, PdfDictionary annotation)
    {
        var objr = new PdfDictionary(Owner);
        objr.Elements.SetName(Keys.Type, "/OBJR");
        objr.Elements[ObjectReferenceKeys.Obj] = annotation.Reference;
        objr.Elements[ObjectReferenceKeys.Pg] = page.Reference;
        Owner._irefTable.Add(objr);

        Kids().Elements.Add(objr.Reference);
    }

    private PdfArray Kids()
    {
        if (Elements[Keys.K] is PdfArray kids)
            return kids;

        var array = new PdfArray(Owner);
        Elements[Keys.K] = array;
        return array;
    }

    /// <summary>
    /// The entries of a structure element dictionary.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        /// <summary>(Required) Must be StructElem.</summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "StructElem")]
        public const string Type = "/Type";

        /// <summary>(Required) The structure type — what this element is.</summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string S = "/S";

        /// <summary>(Required) The parent of this element in the structure tree.</summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string P = "/P";

        /// <summary>(Optional) The children: elements, marked-content identifiers, object references.</summary>
        [KeyInfo(KeyType.Various | KeyType.Optional)]
        public const string K = "/K";

        /// <summary>(Optional) The page the marked content of this element is on.</summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string Pg = "/Pg";

        /// <summary>(Optional) Alternate text, for a reader who cannot see the content.</summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Alt = "/Alt";

        /// <summary>(Optional) What the marks really spell, when the glyphs disagree with the text.</summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string ActualText = "/ActualText";

        /// <summary>(Optional) The language of this element.</summary>
        [KeyInfo(KeyType.TextString | KeyType.Optional)]
        public const string Lang = "/Lang";

        /// <summary>
        /// (Optional) Attributes for this element, owned by a class such as List, Table or Layout —
        /// a dictionary, or an array of them when more than one class contributes.
        /// </summary>
        [KeyInfo(KeyType.Various | KeyType.Optional)]
        public const string A = "/A";

        /// <summary>
        /// (Optional; PDF 2.0) The namespace this element's structure type is defined in. An element
        /// without one inherits its nearest ancestor's, all the way up to the tree root's single
        /// child — which is what lets <see cref="PdfStructureBuilder"/> put a whole tree in the
        /// PDF 2.0 namespace by setting this once, on that one element, for ISO 14289-2's sake.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string NS = "/NS";

        /// <summary>
        /// (Optional; required by ISO 14289-1 of every Note) The name this element may be referred to
        /// by, unique within the document and a key of the structure tree root's IDTree. A byte
        /// string, not a text string.
        /// </summary>
        [KeyInfo(KeyType.ByteString | KeyType.Optional)]
        public const string ID = "/ID";

        /// <summary>Gets the KeysMeta for these keys.</summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// The entries of an object reference, which is how an annotation joins the structure tree.
    /// </summary>
    private static class ObjectReferenceKeys
    {
        public const string Obj = "/Obj";
        public const string Pg = "/Pg";
    }

    /// <summary>
    /// The entries of a marked-content reference, which is how a mark on a page other than the
    /// element's own says which page it is on.
    /// </summary>
    private static class MarkedContentKeys
    {
        public const string Pg = "/Pg";
        public const string MCID = "/MCID";
    }

    internal override DictionaryMeta Meta => Keys.Meta;
}
