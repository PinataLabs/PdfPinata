using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Collapses indirect objects that say exactly the same thing into one, so that a document merged
/// out of many files carries one copy of each font, image and form rather than one per file.
/// <para>
/// Two objects are the same when their dictionaries hold the same entries, their streams the same
/// encoded bytes, and every reference in them leads to objects that are the same in turn. That is
/// decided for the whole set at once by partition refinement - every object starts in a class by
/// what it says with its references blanked out, and classes are split by where their references
/// lead until nothing more splits - so a font's program, descriptor, widths, <c>/ToUnicode</c> map
/// and the font dictionary itself all collapse together, and objects that refer to one another in a
/// cycle are handled without special cases.
/// </para>
/// <para>
/// Only content is considered: objects reached from a page's resources and content streams, and
/// from the resources of the appearance streams of its annotations, stopping at anything whose
/// identity matters rather than its value - pages, annotations, fields, structure elements,
/// optional content groups, signatures, anything with a <c>/Parent</c> or a <c>/P</c>. And only
/// objects of the plain <see cref="PdfDictionary"/> and <see cref="PdfArray"/> types: an object of
/// a derived type belongs to this document's own object model, which may write into it again at the
/// next save, and is neither merged nor looked into.
/// </para>
/// </summary>
internal static class PdfResourceDeduplicator
{
    /// <summary>
    /// Values of <c>/Type</c> naming an object that stands for itself rather than for its content.
    /// </summary>
    private static readonly HashSet<string> IdentityTypes = new(StringComparer.Ordinal)
    {
        "/Catalog", "/Pages", "/Page", "/Annot", "/Border", "/OCG", "/OCMD", "/StructTreeRoot",
        "/StructElem", "/MCR", "/OBJR", "/Sig", "/DocTimeStamp", "/SigRef", "/TransformParams",
        "/Outlines", "/Action", "/Filespec", "/EmbeddedFile", "/Encrypt", "/Collection", "/Thread",
        "/Bead", "/Template", "/NumberTree", "/Namespace"
    };

    /// <summary>
    /// Keys whose presence says that a dictionary is a node of some tree, a field, an annotation or
    /// a structure element: something that is referred to for where it is rather than what it holds.
    /// </summary>
    private static readonly string[] IdentityKeys = ["/Parent", "/P", "/Kids", "/FT", "/Rect", "/Dest", "/Names", "/Nums"];

    /// <summary>The categories of a resource dictionary, each a dictionary of names.</summary>
    private static readonly string[] ResourceCategories =
        ["/Font", "/XObject", "/ExtGState", "/ColorSpace", "/Pattern", "/Shading"];

    /// <summary>
    /// Points every reference to an object at one representative of the objects equal to it.
    /// Nothing is removed: the objects no longer referred to are dropped when the document is
    /// saved, as every unreachable object is.
    /// </summary>
    /// <returns>The number of objects that are no longer referred to.</returns>
    internal static int Deduplicate(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var graph = new CandidateGraph();
        foreach (var page in document.Pages)
            graph.AddPage(page);

        if (graph.Count < 2)
            return 0;

        var replacements = Replacements(graph, graph.Partition());
        if (replacements.Count == 0)
            return 0;

        foreach (var iref in document._irefTable.AllReferences)
        {
            if (iref.Value != null)
                Redirect(iref.Value, replacements, 0);
        }
        Redirect(document._trailer, replacements, 0);

        // A page imported later from a source still open would otherwise be given the copy that
        // was just dropped, which the save after that would write as a dangling reference.
        document.FormTable.RedirectImportedObjects(replacements);

        return replacements.Count;
    }

    /// <summary>
    /// The reference each merged object is to be replaced by, keyed by its own reference.
    /// </summary>
    private static Dictionary<PdfReference, PdfReference> Replacements(CandidateGraph graph, int[] classes)
    {
        // The first object found of each class stands for all of it, which keeps the one a
        // reader meets first and makes the outcome independent of how the classes are numbered.
        var representatives = new Dictionary<int, PdfReference>();
        var replacements = new Dictionary<PdfReference, PdfReference>();
        for (var i = 0; i < graph.Count; i++)
        {
            var reference = graph.Objects[i].Reference;
            if (representatives.TryGetValue(classes[i], out var representative))
                replacements[reference] = representative;
            else
                representatives[classes[i]] = reference;
        }

        return replacements;
    }

    /// <summary>
    /// Replaces the references of a dictionary or array, and of the direct ones nested in it.
    /// </summary>
    private static void Redirect(PdfItem item, Dictionary<PdfReference, PdfReference> replacements, int depth)
    {
        if (depth > MaxDepth)
            return;

        switch (item)
        {
            case PdfDictionary dictionary:
                RedirectEntries(dictionary, replacements, depth);
                break;

            case PdfArray array:
                RedirectElements(array, replacements, depth);
                break;
        }
    }

    private static void RedirectEntries(PdfDictionary dictionary, Dictionary<PdfReference, PdfReference> replacements, int depth)
    {
        foreach (var key in dictionary.Elements.Keys.ToList())
        {
            if (Redirected(dictionary.Elements[key], replacements, depth) is { } replacement)
                dictionary.Elements[key] = replacement;
        }
    }

    private static void RedirectElements(PdfArray array, Dictionary<PdfReference, PdfReference> replacements, int depth)
    {
        for (var i = 0; i < array.Elements.Count; i++)
        {
            if (Redirected(array.Elements[i], replacements, depth) is { } replacement)
                array.Elements[i] = replacement;
        }
    }

    /// <summary>
    /// The reference to put in place of <paramref name="value"/>, or null to leave it where it is.
    /// A direct dictionary or array is redirected in place, one level deeper.
    /// </summary>
    private static PdfReference Redirected(PdfItem value, Dictionary<PdfReference, PdfReference> replacements, int depth)
    {
        if (value is PdfReference reference)
            return replacements.TryGetValue(reference, out var replacement) ? replacement : null;

        if (value is PdfDictionary or PdfArray)
            Redirect(value, replacements, depth + 1);
        return null;
    }

    /// <summary>
    /// How deep direct dictionaries and arrays are followed inside one object. Real documents nest
    /// a handful of levels; the cap only stops a pathological one from exhausting the stack.
    /// </summary>
    private const int MaxDepth = 64;

    /// <summary>
    /// The indirect objects that may be merged, the references between them, and what each says
    /// apart from those references.
    /// </summary>
    private sealed class CandidateGraph
    {
        public readonly List<PdfObject> Objects = [];
        private readonly Dictionary<PdfObject, int> _index = new();
        private readonly HashSet<PdfObject> _seen = [];
        private readonly Queue<PdfObject> _pending = new();

        public int Count => Objects.Count;

        public void AddPage(PdfPage page)
        {
            AddResources(page.Elements["/Resources"]);
            AddItem(page.Elements["/Contents"], 0);

            if (PdfReference.Dereference(page.Elements["/Annots"]) is PdfArray annotations)
            {
                foreach (var annotation in annotations.Elements)
                {
                    if (PdfReference.Dereference(annotation) is PdfDictionary dictionary
                        && PdfReference.Dereference(dictionary.Elements["/AP"]) is PdfDictionary appearances)
                        AddAppearances(appearances);
                }
            }

            Drain();
        }

        /// <summary>
        /// The resources an annotation's appearance streams draw with. The streams themselves are
        /// left alone: a field redraws its appearance when its value changes, and two widgets that
        /// had come to share one stream would then change together.
        /// </summary>
        private void AddAppearances(PdfDictionary appearances)
        {
            foreach (var key in new[] { "/N", "/R", "/D" })
            {
                switch (PdfReference.Dereference(appearances.Elements[key]))
                {
                    case PdfDictionary { Stream: not null } stream:
                        AddResources(stream.Elements["/Resources"]);
                        break;
                    case PdfDictionary states:
                        AddStateResources(states);
                        break;
                }
            }
        }

        /// <summary>
        /// The resources of each appearance stream of a dictionary of appearance states.
        /// </summary>
        private void AddStateResources(PdfDictionary states)
        {
            foreach (var state in states.Elements.Values)
            {
                if (PdfReference.Dereference(state) is PdfDictionary { Stream: not null } stateStream)
                    AddResources(stateStream.Elements["/Resources"]);
            }
        }

        /// <summary>
        /// The entries of a resource dictionary. The dictionary itself, and the dictionary of each
        /// category, belong to the page or form holding them and are only looked through.
        /// </summary>
        private void AddResources(PdfItem item)
        {
            if (PdfReference.Dereference(item) is not PdfDictionary resources)
                return;

            foreach (var category in ResourceCategories)
            {
                if (PdfReference.Dereference(resources.Elements[category]) is not PdfDictionary entries)
                    continue;

                foreach (var entry in entries.Elements.Values)
                    AddItem(entry, 0);
            }
        }

        /// <summary>
        /// Queues the objects a reference, or a direct dictionary or array, leads to.
        /// </summary>
        private void AddItem(PdfItem item, int depth)
        {
            if (depth > MaxDepth)
                return;

            switch (item)
            {
                case PdfReference { Value: { } value }:
                    if (_seen.Add(value))
                        _pending.Enqueue(value);
                    break;
                case PdfDictionary dictionary:
                    AddItems(dictionary.Elements.Values, depth + 1);
                    break;
                case PdfArray array:
                    AddItems(array.Elements, depth + 1);
                    break;
            }
        }

        private void AddItems(IEnumerable<PdfItem> items, int depth)
        {
            foreach (var item in items)
                AddItem(item, depth);
        }

        private void Drain()
        {
            while (_pending.Count > 0)
            {
                var obj = _pending.Dequeue();
                if (!IsMergeable(obj))
                    continue;

                _index[obj] = Objects.Count;
                Objects.Add(obj);
                AddItem(obj, 0);
            }
        }

        private static bool IsMergeable(PdfObject obj)
        {
            if (obj.Reference == null)
                return false;

            var type = obj.GetType();
            if (type == typeof(PdfArray))
                return true;
            if (type != typeof(PdfDictionary))
                return false;

            var dictionary = (PdfDictionary)obj;
            if (dictionary.Stream is { Value: null })
                return false;
            if (dictionary.Elements["/Type"] is PdfName name && IdentityTypes.Contains(name.Value))
                return false;
            foreach (var key in IdentityKeys)
            {
                if (dictionary.Elements.ContainsKey(key))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Numbers the objects so that two share a number exactly when they are equal.
        /// </summary>
        public int[] Partition()
        {
            var children = new List<int>[Count];
            var classes = ClassesByContent(children, out var classCount);

            // Split every class by the classes its references lead to, until nothing splits. Each
            // round can only split, never join, so the count of classes rising is the only change
            // to look for, and it cannot rise past the number of objects.
            var signature = new StringBuilder();
            while (true)
            {
                var next = SplitByChildren(classes, children, signature, out var nextCount);
                var changed = nextCount != classCount;
                classes = next;
                classCount = nextCount;
                if (!changed)
                    return classes;
            }
        }

        /// <summary>
        /// What each object says with the references among the candidates blanked out, and where
        /// those references lead. Stream bytes are keyed by length and hash, and compared outright
        /// within a key, so that a collision cannot merge two different streams.
        /// </summary>
        private int[] ClassesByContent(List<int>[] children, out int classCount)
        {
            var classes = new int[Count];
            var groups = new Dictionary<string, List<(byte[] Bytes, int Class)>>(StringComparer.Ordinal);
            classCount = 0;
            for (var i = 0; i < Count; i++)
            {
                var obj = Objects[i];
                var targets = new List<int>();
                var key = Describe(obj, targets);
                children[i] = targets;

                if (key == null)
                {
                    // Something in it this does not know how to compare: equal to nothing.
                    classes[i] = classCount++;
                    continue;
                }

                if (!groups.TryGetValue(key, out var candidates))
                    groups[key] = candidates = [];

                classes[i] = ClassAmong(candidates, (obj as PdfDictionary)?.Stream?.Value, ref classCount);
            }

            return classes;
        }

        /// <summary>
        /// The class of the candidate whose stream bytes match, or a new one added to the candidates.
        /// </summary>
        private static int ClassAmong(List<(byte[] Bytes, int Class)> candidates, byte[] bytes, ref int classCount)
        {
            foreach (var candidate in candidates)
            {
                if (bytes == null || candidate.Bytes.AsSpan().SequenceEqual(bytes))
                    return candidate.Class;
            }

            var found = classCount++;
            candidates.Add((bytes, found));
            return found;
        }

        private int[] SplitByChildren(int[] classes, List<int>[] children, StringBuilder signature, out int classCount)
        {
            var next = new int[Count];
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < Count; i++)
            {
                signature.Clear();
                signature.Append(classes[i]);
                foreach (var child in children[i])
                    signature.Append(',').Append(classes[child]);

                var text = signature.ToString();
                if (!ids.TryGetValue(text, out var id))
                    ids[text] = id = ids.Count;
                next[i] = id;
            }

            classCount = ids.Count;
            return next;
        }

        /// <summary>
        /// Writes out what an object says, with each reference to a candidate written as a
        /// placeholder and its target added to <paramref name="targets"/> in order. Null when the
        /// object holds something this cannot compare.
        /// </summary>
        private string Describe(PdfObject obj, List<int> targets)
        {
            var text = new StringBuilder();
            if (!Describe(obj, text, targets, 0, true))
                return null;

            if (obj is PdfDictionary { Stream.Value: { } bytes })
                text.Append("stream").Append(bytes.Length).Append(':').Append(Fnv1a(bytes));

            return text.ToString();
        }

        private bool Describe(PdfItem item, StringBuilder text, List<int> targets, int depth, bool top)
        {
            if (depth > MaxDepth)
                return false;

            switch (item)
            {
                case PdfReference reference:
                    DescribeReference(reference, text, targets);
                    return true;
                case PdfDictionary dictionary:
                    return DescribeDictionary(dictionary, text, targets, depth, top);
                case PdfArray array:
                    return DescribeArray(array, text, targets, depth);
                default:
                    return DescribeScalar(item, text);
            }
        }

        private void DescribeReference(PdfReference reference, StringBuilder text, List<int> targets)
        {
            if (reference.Value != null && _index.TryGetValue(reference.Value, out var target))
            {
                text.Append("@;");
                targets.Add(target);
            }
            else
            {
                // Something that is not merged is equal only to itself.
                text.Append('R').Append(reference.ObjectNumber).Append(',')
                    .Append(reference.GenerationNumber).Append(';');
            }
        }

        private bool DescribeDictionary(PdfDictionary dictionary, StringBuilder text, List<int> targets, int depth, bool top)
        {
            var isStream = top && dictionary.Stream != null;
            text.Append("<<");
            foreach (var key in dictionary.Elements.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                // The writer states a stream's length from its bytes, which are compared
                // outright; a /Length held as a reference would otherwise keep two equal
                // streams apart.
                if (isStream && key == "/Length")
                    continue;
                AppendString(text, 'k', key);
                if (!Describe(dictionary.Elements[key], text, targets, depth + 1, false))
                    return false;
            }
            text.Append(">>");
            return true;
        }

        private bool DescribeArray(PdfArray array, StringBuilder text, List<int> targets, int depth)
        {
            text.Append('[');
            foreach (var element in array.Elements)
            {
                if (!Describe(element, text, targets, depth + 1, false))
                    return false;
            }
            text.Append(']');
            return true;
        }

        /// <summary>
        /// Writes out an item that holds no other items. False for one this does not know how to
        /// compare.
        /// </summary>
        private static bool DescribeScalar(PdfItem item, StringBuilder text)
        {
            switch (item)
            {
                case PdfName name:
                    AppendString(text, 'n', name.Value);
                    return true;
                case PdfString str:
                    text.Append('s').Append((int)str.Flags);
                    AppendString(text, ':', str.Value);
                    return true;
                case PdfBoolean boolean:
                    text.Append(boolean.Value ? "b1;" : "b0;");
                    return true;
                case PdfNull:
                    text.Append("N;");
                    return true;
                case PdfLiteral literal:
                    AppendString(text, 'L', literal.Value);
                    return true;
                case PdfRectangle rectangle:
                    text.Append("rect")
                        .Append(rectangle.X1.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rectangle.Y1.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rectangle.X2.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                        .Append(rectangle.Y2.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                    return true;
                default:
                    return DescribeNumber(item, text);
            }
        }

        /// <summary>
        /// Writes out a number, tagged by its kind so that an integer and a real of the same value
        /// stay apart. False for anything that is not a number this knows.
        /// </summary>
        private static bool DescribeNumber(PdfItem item, StringBuilder text)
        {
            var (tag, value) = item switch
            {
                PdfInteger integer => ('i', integer.Value.ToString(CultureInfo.InvariantCulture)),
                PdfLong integer => ('l', integer.Value.ToString(CultureInfo.InvariantCulture)),
                PdfUInteger integer => ('u', integer.Value.ToString(CultureInfo.InvariantCulture)),
                PdfReal real => ('r', real.Value.ToString("R", CultureInfo.InvariantCulture)),
                _ => ('\0', null)
            };

            if (value == null)
                return false;

            text.Append(tag).Append(value).Append(';');
            return true;
        }

        /// <summary>
        /// A string with its length in front, so that no two sequences of strings run together
        /// into the same text.
        /// </summary>
        private static void AppendString(StringBuilder text, char tag, string value)
        {
            value ??= "";
            text.Append(tag).Append(value.Length).Append(':').Append(value);
        }

        private static ulong Fnv1a(byte[] bytes)
        {
            var hash = 14695981039346656037UL;
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
