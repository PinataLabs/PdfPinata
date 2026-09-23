using System.Collections.Generic;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Structure;

/// <summary>
/// The root of the structure tree, hanging off the catalog and holding the two indexes a reader
/// needs: the role map, which explains any structure type it does not already know, and the parent
/// tree, which takes it from a mark on a page back to what that mark means.
/// </summary>
public sealed class PdfStructureTreeRoot : PdfDictionary
{
    internal PdfStructureTreeRoot(PdfDocument document)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/StructTreeRoot");
        document._irefTable.Add(this);

        ParentTree = new PdfNumberTreeNode(document);
        document._irefTable.Add(ParentTree);
        Elements[Keys.ParentTree] = ParentTree.Reference;
    }

    /// <summary>
    /// Maps a structure type of the document's own invention onto one a reader understands. A type
    /// that is not standard and not in here means nothing to anybody.
    /// </summary>
    public IDictionary<string, string> RoleMap { get; } = new Dictionary<string, string>();

    /// <summary>
    /// The index from a mark on a page back to the element that gives it meaning. Keyed by the
    /// page's <c>/StructParents</c>, valued by an array holding one element per marked-content
    /// identifier on that page.
    /// </summary>
    internal PdfNumberTreeNode ParentTree { get; }

    internal void Add(PdfStructureElement child)
    {
        Kids().Elements.Add(child.Reference);
        child.Elements[PdfStructureElement.Keys.P] = Reference;
    }

    /// <summary>
    /// Writes the index from an element's <see cref="PdfStructureElement.Id"/> back to the element,
    /// given the named elements in the order their names sort.
    /// </summary>
    /// <remarks>
    /// ISO 32000-1 requires this the moment any element carries an <c>/ID</c> — an identifier
    /// nothing can be looked up by is a name with no directory, and a reader offered a jump to a
    /// footnote has no way to find it. One flat leaf, however many entries: nesting exists so a
    /// reader can binary-search thousands without loading them all, and the elements that need a
    /// name are the few something points at rather than every node of the tree.
    /// <para>
    /// Called once per save, and a document may be saved more than once — to a stream and then to a
    /// file is an ordinary thing to do. So the tree written last time is filled in again rather than
    /// replaced: a fresh dictionary each time would leave the earlier ones in the cross-reference
    /// table with nothing pointing at them, written into the file as orphans.
    /// </para>
    /// </remarks>
    internal void SetIdTree(IReadOnlyList<KeyValuePair<string, PdfStructureElement>> named)
    {
        if (named.Count == 0)
        {
            Elements.Remove(Keys.IDTree);
            return;
        }

        var leaves = new PdfArray(Owner);
        foreach (var pair in named)
        {
            // Raw for the same reason the /ID itself is: the key and the identifier it stands for are
            // compared byte by byte, so both have to be the same bytes.
            leaves.Elements.Add(new PdfString(pair.Key, PdfStringEncoding.RawEncoding));
            leaves.Elements.Add(pair.Value.Reference);
        }

        var root = Elements.GetDictionary(Keys.IDTree);
        if (root == null)
        {
            root = new PdfDictionary(Owner);
            Owner._irefTable.Add(root);
            Elements[Keys.IDTree] = root.Reference;
        }

        root.Elements.SetObject("/Names", leaves);
    }

    /// <summary>
    /// Writes out the role map, if there is one. Left to save time because entries may be added at
    /// any point before then.
    /// </summary>
    internal override void PrepareForSave()
    {
        if (RoleMap.Count == 0)
            return;

        var map = new PdfDictionary(Owner);
        foreach (var pair in RoleMap)
            map.Elements.SetName(Name(pair.Key), Name(pair.Value));

        Elements[Keys.RoleMap] = map;
    }

    private static string Name(string value) => value.Length > 0 && value[0] == '/' ? value : "/" + value;

    private PdfArray Kids()
    {
        if (Elements[Keys.K] is PdfArray kids)
            return kids;

        var array = new PdfArray(Owner);
        Elements[Keys.K] = array;
        return array;
    }

    /// <summary>
    /// The entries of a structure tree root dictionary.
    /// </summary>
    public sealed class Keys : KeysBase
    {
        /// <summary>(Required) Must be StructTreeRoot.</summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "StructTreeRoot")]
        public const string Type = "/Type";

        /// <summary>(Optional) The top-level structure elements.</summary>
        [KeyInfo(KeyType.Various | KeyType.Optional)]
        public const string K = "/K";

        /// <summary>(Required if any content is marked) Mark back to structure element.</summary>
        [KeyInfo(KeyType.NumberTree | KeyType.Optional)]
        public const string ParentTree = "/ParentTree";

        /// <summary>(Optional) The next free key of the parent tree.</summary>
        [KeyInfo(KeyType.Integer | KeyType.Optional)]
        public const string ParentTreeNextKey = "/ParentTreeNextKey";

        /// <summary>
        /// (Required if any structure element has an ID) A name tree from an element's ID back to the
        /// element, which is how anything that names an element is resolved.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string IDTree = "/IDTree";

        /// <summary>(Optional) Non-standard structure types explained in terms of standard ones.</summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
        public const string RoleMap = "/RoleMap";

        /// <summary>
        /// (Required if any structure element names a namespace; PDF 2.0) Every namespace used
        /// anywhere in the tree. An element's <c>/NS</c> points into this array, and a namespace
        /// that appears on an element but not here is one a reader has no dictionary for.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Namespaces = "/Namespaces";

        /// <summary>Gets the KeysMeta for these keys.</summary>
        internal static DictionaryMeta Meta => _meta ??= CreateMeta(typeof(Keys));

        private static DictionaryMeta _meta;
    }

    internal override DictionaryMeta Meta => Keys.Meta;
}
