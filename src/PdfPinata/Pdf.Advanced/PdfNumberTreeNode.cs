using System;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a number tree, which maps integer keys to values and is how a PDF holds a
/// mapping too large to want all of at once. The catalog holds the page labels of a document
/// in one.
/// <para>
/// A tree is one node where it is small and several where it is not. A node holds either the
/// entries themselves, in a Nums array of key and value one after the other, or the nodes
/// below it, in a Kids array; every node but the root also states the least and greatest key
/// beneath it. This class hides that: entries go in and come out by key, and the shape is
/// worked out when the tree is written.
/// </para>
/// </summary>
public sealed class PdfNumberTreeNode : PdfDictionary
{
    /// <summary>
    /// How many entries a leaf holds, and how many nodes a branch holds, before another level
    /// is put in. The figure is not laid down anywhere; it is chosen so that the trees this
    /// writes look like the ones it reads.
    /// </summary>
    private const int NodeCapacity = 64;

    /// <summary>
    /// How deep a tree is followed before reading gives up. Well past anything a real document
    /// holds, and there to stop a malformed one running away.
    /// </summary>
    private const int MaximumDepth = 32;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNumberTreeNode"/> class.
    /// </summary>
    public PdfNumberTreeNode()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfNumberTreeNode"/> class.
    /// </summary>
    public PdfNumberTreeNode(PdfDocument document)
        : base(document)
    { }

    internal PdfNumberTreeNode(PdfDictionary dict)
        : base(dict)
    { }

    /// <summary>
    /// The entries of the tree, by key. Read from the tree the first time it is asked for and
    /// held after that, so that reading a tree of many nodes is done once.
    /// </summary>
    private SortedDictionary<int, PdfItem> _entries;

    private SortedDictionary<int, PdfItem> Entries
    {
        get
        {
            if (_entries == null)
            {
                _entries = new SortedDictionary<int, PdfItem>();
                Read(this, new Dictionary<string, object>(), 0);
            }
            return _entries;
        }
    }

    /// <summary>
    /// The number of entries in the tree.
    /// </summary>
    public int Count => Entries.Count;

    /// <summary>
    /// The keys of the tree, least first.
    /// </summary>
    public int[] GetKeys()
    {
        var keys = new int[Entries.Count];
        Entries.Keys.CopyTo(keys, 0);
        return keys;
    }

    /// <summary>
    /// Whether the tree holds an entry for the key.
    /// </summary>
    public bool Contains(int key)
    {
        return Entries.ContainsKey(key);
    }

    /// <summary>
    /// The value the tree holds for the key, or null where it holds none. An indirect value is
    /// returned as the object it refers to.
    /// </summary>
    public PdfItem GetValue(int key)
    {
        if (!Entries.TryGetValue(key, out var value))
            return null;

        var reference = value as PdfReference;
        return reference != null ? reference.Value : value;
    }

    /// <summary>
    /// The value the tree holds for the key as a dictionary, or null where it holds none or
    /// holds something else. The page labels of a document are dictionaries.
    /// </summary>
    public PdfDictionary GetDictionary(int key)
    {
        return GetValue(key) as PdfDictionary;
    }

    /// <summary>
    /// Puts a value in the tree under the key, in place of whatever was there.
    /// </summary>
    public void SetValue(int key, PdfItem value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Entries[key] = Referenced(value);
        Write();
    }

    /// <summary>
    /// Takes the entry for the key out of the tree, and says whether there was one.
    /// </summary>
    public bool Remove(int key)
    {
        if (!Entries.Remove(key))
            return false;

        Write();
        return true;
    }

    /// <summary>
    /// An indirect object is held in the tree as the reference to it, which is what the tree
    /// is written with.
    /// </summary>
    private static PdfItem Referenced(PdfItem value)
    {
        var obj = value as PdfObject;
        if (obj != null && obj.Reference != null)
            return obj.Reference;

        return value;
    }

    #region Reading

    /// <summary>
    /// Reads a node and everything below it. Takes the tree as it finds it: a node holding
    /// both its entries and nodes below it, entries out of order, or a node reached twice are
    /// all things a document may hold, and none of them is worth refusing to read it over.
    /// </summary>
    private void Read(PdfDictionary node, Dictionary<string, object> seen, int depth)
    {
        if (node == null || depth > MaximumDepth)
            return;

        if (node.IsIndirect)
        {
            var id = node.ObjectID.ToString();
            if (seen.ContainsKey(id))
                return;

            seen[id] = null;
        }

        var nums = node.Elements.GetArray(Keys.Nums);
        if (nums != null)
        {
            // Key and value one after the other. An odd one at the end has no value and is
            // left where it is.
            for (var at = 0; at + 1 < nums.Elements.Count; at += 2)
            {
                if (TryGetInteger(nums.Elements[at], out var key))
                    _entries[key] = nums.Elements[at + 1];
            }
        }

        var kids = node.Elements.GetArray(Keys.Kids);
        if (kids != null)
        {
            for (var at = 0; at < kids.Elements.Count; at++)
                Read(kids.Elements.GetDictionary(at), seen, depth + 1);
        }
    }

    private static bool TryGetInteger(PdfItem item, out int value)
    {
        var reference = item as PdfReference;
        if (reference != null)
            item = reference.Value;

        var integer = item as PdfInteger;
        if (integer != null)
        {
            value = integer.Value;
            return true;
        }

        // A key written as a real is not what the standard asks for, but it is unambiguous.
        var real = item as PdfReal;
        #pragma warning disable S1244 // Exact on purpose: a real is an integer only when it is exactly one.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (real != null && real.Value == Math.Floor(real.Value))
        #pragma warning restore S1244
        {
            value = (int)real.Value;
            return true;
        }

        value = 0;
        return false;
    }

    #endregion

    #region Writing

    /// <summary>
    /// Writes the entries back as a tree. One node while the entries are few, and a node of
    /// nodes once they are not.
    /// </summary>
    private void Write()
    {
        Elements.Remove(Keys.Kids);
        Elements.Remove(Keys.Nums);

        // The root of a tree states no limits, whatever the nodes below it do.
        Elements.Remove(Keys.Limits);

        var keys = new List<int>(_entries.Keys);

        // A node below the root has to be referred to indirectly, which takes a document to
        // hold it. Without one the entries stay in the root, which is a tree all the same.
        if (keys.Count <= NodeCapacity || Owner == null)
        {
            Elements[Keys.Nums] = Nums(keys, 0, keys.Count);
            return;
        }

        var level = new List<PdfDictionary>();
        for (var at = 0; at < keys.Count; at += NodeCapacity)
        {
            var length = Math.Min(NodeCapacity, keys.Count - at);
            var leaf = new PdfDictionary(Owner);
            leaf.Elements[Keys.Nums] = Nums(keys, at, length);
            leaf.Elements[Keys.Limits] = Limits(keys[at], keys[at + length - 1]);
            Owner._irefTable.Add(leaf);
            level.Add(leaf);
        }

        // Another level of nodes for as long as one level will not hold them.
        while (level.Count > NodeCapacity)
        {
            var above = new List<PdfDictionary>();
            for (var at = 0; at < level.Count; at += NodeCapacity)
            {
                var length = Math.Min(NodeCapacity, level.Count - at);
                var branch = new PdfDictionary(Owner);
                branch.Elements[Keys.Kids] = Kids(level, at, length);
                branch.Elements[Keys.Limits] = Limits(LeastOf(level[at]), GreatestOf(level[at + length - 1]));
                Owner._irefTable.Add(branch);
                above.Add(branch);
            }
            level = above;
        }

        Elements[Keys.Kids] = Kids(level, 0, level.Count);
    }

    private PdfArray Nums(List<int> keys, int from, int length)
    {
        var nums = new PdfArray(Owner);
        for (var at = from; at < from + length; at++)
        {
            nums.Elements.Add(new PdfInteger(keys[at]));
            nums.Elements.Add(_entries[keys[at]]);
        }
        return nums;
    }

    private PdfArray Kids(List<PdfDictionary> nodes, int from, int length)
    {
        var kids = new PdfArray(Owner);
        for (var at = from; at < from + length; at++)
            kids.Elements.Add(nodes[at].Reference);

        return kids;
    }

    private PdfArray Limits(int least, int greatest)
    {
        var limits = new PdfArray(Owner);
        limits.Elements.Add(new PdfInteger(least));
        limits.Elements.Add(new PdfInteger(greatest));
        return limits;
    }

    private static int LeastOf(PdfDictionary node)
    {
        return node.Elements.GetArray(Keys.Limits).Elements.GetInteger(0);
    }

    private static int GreatestOf(PdfDictionary node)
    {
        return node.Elements.GetArray(Keys.Limits).Elements.GetInteger(1);
    }

    #endregion

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    internal sealed class Keys : KeysBase
    {
        /// <summary>
        /// (Root and intermediate nodes only; required in intermediate nodes; present in the
        /// root node if and only if Nums is not present) An array of indirect references to
        /// the immediate children of this node.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Kids = "/Kids";

        /// <summary>
        /// (Root and leaf nodes only; required in leaf nodes; present in the root node if and
        /// only if Kids is not present) An array of the form [key1 value1 key2 value2 ...]
        /// where each key is an integer and the keys are in ascending numerical order.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Nums = "/Nums";

        /// <summary>
        /// (Intermediate and leaf nodes only; required) An array of two integers, giving the
        /// least and greatest keys included in the Nums array of a leaf node, or in the Nums
        /// arrays of any leaf nodes that are descendants of an intermediate node.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Optional)]
        public const string Limits = "/Limits";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        public static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
