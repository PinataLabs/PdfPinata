using System.Collections.Generic;
using System.Linq;
using PinataLayout.DocumentObjectModel;

namespace PinataLayout.Rendering;

/// <summary>
/// Every footnote in a document, in reading order, with the page each one's mark landed on.
/// </summary>
/// <remarks>
/// <para>
/// A note is identified by the <see cref="Footnote"/> object itself rather than by a counter, so
/// that formatting the same paragraph more than once - which the formatter does whenever an element
/// has to be tried on a page and then moved to the next - neither renumbers it nor counts it twice.
/// Registering a note that is already known simply moves it.
/// </para>
/// <para>
/// The mark is worked out on demand rather than stored, because two of the three numbering rules
/// depend on where the note ended up. During formatting the answer is provisional - the page it is
/// asked about is the page currently being filled - and that is exactly right: if the element
/// carrying the mark is moved to the next page it is formatted again there, and the answer moves
/// with it.
/// </para>
/// </remarks>
internal class FootnoteRegistry
{
    internal FootnoteRegistry(Document document)
    {
        _document = document;
    }

    /// <summary>Forgets everything. Called when a document is about to be formatted.</summary>
    internal void Reset()
    {
        _entries.Clear();
        _order.Clear();
    }

    /// <summary>
    /// Records where a note's mark fell, and what the note came out as when laid out. Called again
    /// for the same note whenever the element carrying it is formatted again.
    /// </summary>
    internal void Place(Footnote footnote, int section, int page, FormattedFootnote formatted)
    {
        if (!_entries.TryGetValue(footnote, out var entry))
        {
            entry = new Entry();
            _entries.Add(footnote, entry);
            _order.Add(footnote);
        }

        entry.Section = section;
        entry.Page = page;
        entry.Formatted = formatted;
    }

    /// <summary>Whether this note has been placed at all.</summary>
    internal bool IsPlaced(Footnote footnote) => _entries.ContainsKey(footnote);

    /// <summary>
    /// The text of the note's reference mark: the caller's own <see cref="Footnote.Reference"/>
    /// where they set one, and the generated number where they did not.
    /// </summary>
    internal string MarkFor(Footnote footnote)
    {
        if (footnote.Reference.Length > 0)
            return footnote.Reference;

        return !_entries.TryGetValue(footnote, out var entry)
            ? ""
            : FootnoteNumbering.Mark(OrdinalOf(footnote, entry), _document.FootnoteNumberStyle);
    }

    /// <summary>The notes whose marks landed on this page, in reading order.</summary>
    internal IReadOnlyList<Footnote> On(int page) =>
        [.._order.Where(note => _entries[note].Page == page)];

    /// <summary>The note as it was laid out, or null if it never was.</summary>
    internal FormattedFootnote FormattedOf(Footnote footnote) =>
        _entries.TryGetValue(footnote, out var entry) ? entry.Formatted : null;

    /// <summary>
    /// Where this note comes in the sequence its numbering rule counts, from the document's
    /// starting number.
    /// </summary>
    /// <remarks>
    /// A starting number of zero is the property's unset default rather than a request to begin at
    /// zero, and a first footnote marked "0" would be a strange thing to ship. Anything below one
    /// therefore starts at one.
    /// </remarks>
    private int OrdinalOf(Footnote footnote, Entry entry)
    {
        var start = _document.FootnoteStartingNumber;
        if (start < 1)
            start = 1;

        var before = 0;
        foreach (var earlier in _order)
        {
            if (earlier == footnote)
                break;

            // A note the caller marked themselves is not part of the counting. It shows a symbol of
            // their choosing, and letting it advance the sequence would make the numbers around it
            // skip for no reason a reader could see.
            if (earlier.Reference.Length > 0)
                continue;

            if (CountsWith(_entries[earlier], entry))
                ++before;
        }

        return start + before;
    }

    private bool CountsWith(Entry earlier, Entry entry)
    {
        switch (_document.FootnoteNumberingRule)
        {
            case FootnoteNumberingRule.RestartPage:
                return earlier.Page == entry.Page;

            case FootnoteNumberingRule.RestartSection:
                return earlier.Section == entry.Section;

            case FootnoteNumberingRule.RestartContinuous:
            default:
                return true;
        }
    }

    private class Entry
    {
        internal int Section;
        internal int Page;
        internal FormattedFootnote Formatted;
    }

    private readonly Document _document;

    // Reference equality, deliberately: two notes with identical content are two notes.
    private readonly Dictionary<Footnote, Entry> _entries =
        new Dictionary<Footnote, Entry>(ReferenceEqualityComparer.Instance);

    private readonly List<Footnote> _order = [];
}

/// <summary>
/// Reference equality for a dictionary key, for the netstandard2.1 leg where
/// <c>System.Collections.Generic.ReferenceEqualityComparer</c> does not exist.
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<Footnote>
{
    internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

    public bool Equals(Footnote x, Footnote y) => ReferenceEquals(x, y);

    public int GetHashCode(Footnote obj) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
