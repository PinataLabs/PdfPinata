using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPinata.Pdf.Rendering;

/// <summary>Something read from a font's objects and kept, which says how much it holds.</summary>
internal interface IFontData
{
    /// <summary>
    /// About how many bytes this holds, and can come to hold: it is asked once, when the data is
    /// first kept.
    /// </summary>
    long HeldBytes { get; }
}

/// <summary>What a piece of font data is, which with the object it was read from is its key.</summary>
internal enum FontDataKind
{
    /// <summary>A decoded font program, read from a <c>/FontFile2</c> stream.</summary>
    Program,

    /// <summary>A decoded <c>/CIDToGIDMap</c> stream.</summary>
    CidToGidMap,

    /// <summary>The widths a <c>/W</c> array gives, by CID.</summary>
    CidWidths,

    /// <summary>The vertical metrics a <c>/W2</c> array gives, by CID.</summary>
    VerticalMetrics,

    /// <summary>The widths a simple font dictionary gives, by code.</summary>
    SimpleWidths,

    /// <summary>A simple font dictionary's encoding, and the glyph chosen for each code so far.</summary>
    SimpleGlyphs
}

/// <summary>
/// Everything page rendering keeps for the fonts of one document, and the bound on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule: whatever is read from a font and kept is kept here, under the PDF object it was
/// read from, and charged in bytes to a budget for the whole document.</b> A
/// <see cref="RenderFont"/> and a glyph source keep references to the document's own objects and a
/// few numbers, and nothing that grows. So font dictionaries that share a font program, a
/// descendant, a <c>/W</c> array or a <c>/CIDToGIDMap</c> share one copy of what is read from it,
/// and font dictionaries that share nothing are still held, together, to the budget.
/// </para>
/// <para>
/// A budget for each cache that was known of failed three times, each time on a structure that
/// grew with the number of font dictionaries and was charged to nothing: a font dictionary costs
/// about 150 bytes of file, and can name data that reads to megabytes.
/// </para>
/// <para>There are two budgets, because what is kept is of two kinds:</para>
/// <list type="bullet">
/// <item><b>Data that can be read again</b>: font programs, <c>/CIDToGIDMap</c> streams, widths,
/// vertical metrics and a simple font's glyph choices, <see cref="DataByteBudget"/> for all of
/// them. Past it the data least recently used is let go of, at any time, and read again if it is
/// asked for: that changes nothing a device has been handed.</item>
/// <item><b>Glyph outlines</b>, <see cref="OutlineByteBudget"/> for all of them, each charged for
/// its entry as well as its points, so a glyph with no points is not free. None is let go of
/// during a page, because a device keeps a path of its own for each shared one it is handed. A
/// glyph past the budget is decoded each time it is drawn and handed over unshared. Between
/// pages, where outlines are over half the budget, those of the font programs least recently
/// drawn from are forgotten.</item>
/// </list>
/// <para>
/// The two do not share one budget: outlines cannot be let go of during a page, so a page that
/// filled a shared budget with them would leave no room to hold a font program between one glyph
/// and the next.
/// </para>
/// <para>
/// <b>Time is bounded too.</b> Letting go of a font program and reading it again costs its whole
/// decoding, so a page may read <see cref="PageProgramReadAllowance"/> of font program and no
/// more. Past it a font program that would have to be read is not, and its glyphs are reported.
/// </para>
/// <para>
/// Nothing here is synchronised. One page of a document is rendered at a time, as
/// <see cref="PageRendering.Render"/> says.
/// </para>
/// </remarks>
internal sealed class RenderFontCache
{
    /// <summary>
    /// The most bytes of font data, other than outlines, a document keeps for all its fonts: room
    /// for two font programs of <see cref="ProgramByteLimit"/> with their names, and the widths
    /// and maps that go with them, so that a page setting lines in each in turn reads neither again.
    /// </summary>
    internal const long DataByteBudget = 144L << 20;

    /// <summary>
    /// The largest font program that is read. A TrueType font has at most 65,535 glyphs, and the
    /// largest there are, CJK fonts embedded whole, run to 20 or 30 MB and at the far end to
    /// about 50. This allows a kilobyte for every glyph a font can have. A stream that decodes
    /// to more is not a font any producer embedded, and is reported and not read.
    /// </summary>
    internal const long ProgramByteLimit = 64L << 20;

    /// <summary>
    /// The most bytes of font program one page may read, counting each time a program is read
    /// again after it was let go of. It is sixteen of the largest, which a page that cycles
    /// through more large fonts than the budget holds can come to; past it a file is making the
    /// renderer decode for the sake of decoding, at some hundreds of megabytes a second.
    /// </summary>
    internal const long PageProgramReadAllowance = 1L << 30;

    /// <summary>
    /// The most bytes of glyph outline a document keeps for all its fonts: what 1,048,576 points
    /// cost, at sixteen bytes and a verb each. A <c>/CIDToGIDMap</c> or a <c>loca</c> table can
    /// name one glyph of 65,536 points tens of thousands of times in a few hundred bytes.
    /// </summary>
    internal const long OutlineByteBudget = 18L << 20;

    /// <summary>
    /// How many fonts are kept from one page to the next. A <see cref="RenderFont"/> is about
    /// 300 bytes and is made again from its font dictionary for the asking, so past this they
    /// are all forgotten as a page begins, and a document of a million font dictionaries keeps
    /// no more of them than this between pages.
    /// </summary>
    internal const int MaximumFontsKept = 4096;

    /// <summary>What keeping a piece of data is taken to cost beside what it holds: its key, its place in the order, its slot.</summary>
    private const int DataEntryBytes = 192;

    /// <summary>
    /// What keeping an outline is taken to cost beside its points, so that a glyph with none is
    /// not kept for nothing: a font of 65,534 empty glyphs under 256 font dictionaries held 727 MiB.
    /// </summary>
    private const int OutlineEntryBytes = 96;

    private readonly Dictionary<object, RenderFont> _fonts = new(ReferenceIdentity.Instance);
    private readonly Dictionary<FontDataKey, LinkedListNode<FontDataEntry>> _data = new(FontDataKey.ByIdentity);

    /// <summary>The data kept, the most recently used first.</summary>
    private readonly LinkedList<FontDataEntry> _recent = new();

    private readonly Dictionary<object, OutlineSet> _outlines = new(ReferenceIdentity.Instance);
    private long _dataBytes;
    private long _outlineBytes;
    private long _programBytesRead;
    private long _clock;

    /// <summary>The font for a font dictionary, read the first time it is asked for. Never null.</summary>
    /// <remarks>
    /// The key is the font dictionary's reference, which a document holds one instance of for an
    /// object. A font dictionary written in place has no reference, and is its own key. A
    /// <see cref="RenderFont"/> is of a fixed size and holds nothing read from the font, so
    /// within a page these are as many as the font dictionaries the page selects, each smaller
    /// than the dictionary it stands for, and between pages no more than <see cref="MaximumFontsKept"/>.
    /// </remarks>
    internal RenderFont For(PdfDictionary font, Func<PdfDictionary, PageGlyphSource> custom)
    {
        var key = ReferenceIdentity.KeyOf(font);
        if (!_fonts.TryGetValue(key, out var renderFont))
            _fonts[key] = renderFont = RenderFont.From(font, custom, this);
        return renderFont;
    }

    /// <summary>
    /// Called as a page begins: gives the page its allowance of font program to read, and where
    /// outlines are over half their budget forgets those of the font programs least recently
    /// drawn from, so that a long render keeps room for each new page's glyphs.
    /// </summary>
    internal void BeginPage()
    {
        _programBytesRead = 0;
        if (_fonts.Count > MaximumFontsKept)
            _fonts.Clear();
        if (_outlineBytes <= OutlineByteBudget / 2)
            return;

        var oldestFirst = new List<KeyValuePair<object, OutlineSet>>(_outlines);
        oldestFirst.Sort((a, b) => a.Value.LastUsed.CompareTo(b.Value.LastUsed));
        foreach (var (owner, set) in oldestFirst)
        {
            if (_outlineBytes <= OutlineByteBudget / 2)
                break;
            _outlines.Remove(owner);
            _outlineBytes -= set.Bytes;
        }
    }

    /// <summary>
    /// What was read from an object of the file, read now if it is not kept. Keeping it may let
    /// go of other data, the least recently used first, but never of what is answered here.
    /// </summary>
    /// <param name="kind">What the data is.</param>
    /// <param name="source">The object it is read from, which with <paramref name="kind"/> is its key.</param>
    /// <param name="read">Reads it. What this throws is thrown on, and nothing is kept.</param>
    internal T Data<TSource, T>(FontDataKind kind, TSource source, Func<TSource, RenderFontCache, T> read)
        where TSource : PdfObject
        where T : class, IFontData
    {
        var key = new FontDataKey(kind, ReferenceIdentity.KeyOf(source));
        if (_data.TryGetValue(key, out var node))
        {
            if (node != _recent.First)
            {
                _recent.Remove(node);
                _recent.AddFirst(node);
            }

            return (T)node.Value.Data;
        }

        var data = read(source, this);
        var entry = new FontDataEntry(key, data, DataEntryBytes + data.HeldBytes);
        _data[key] = node = _recent.AddFirst(entry);
        _dataBytes += entry.Bytes;
        while (_dataBytes > DataByteBudget && _recent.Last != node)
        {
            var oldest = _recent.Last.Value;
            _recent.RemoveLast();
            _data.Remove(oldest.Key);
            _dataBytes -= oldest.Bytes;
        }

        return data;
    }

    /// <summary>
    /// Says a font program is about to be read, and throws where the page has read all it may.
    /// </summary>
    /// <exception cref="FontProgramNotReadException">The page has read its allowance of font program.</exception>
    internal void BeforeProgramRead()
    {
        if (_programBytesRead >= PageProgramReadAllowance)
        {
            throw new FontProgramNotReadException(
                $"The page has read {PageProgramReadAllowance >> 20} MiB of font program, and this font's is not read again for it.");
        }
    }

    /// <summary>Counts what reading a font program decoded against the page's allowance.</summary>
    internal void AfterProgramRead(long decoded) => _programBytesRead += decoded;

    /// <summary>The outline kept for a glyph, where one is.</summary>
    /// <param name="owner">What the glyph is a glyph of: see <see cref="PageGlyphSource.OutlineOwner"/>.</param>
    /// <param name="glyph">The glyph's number.</param>
    /// <param name="outline">The outline, or null for a glyph that has none.</param>
    /// <param name="why">Why it has none.</param>
    internal bool TryGetOutline(object owner, int glyph, out PagePath outline, out string why)
    {
        if (_outlines.TryGetValue(owner, out var set) && set.Glyphs.TryGetValue(glyph, out var kept))
        {
            set.LastUsed = ++_clock;
            (outline, why) = kept;
            return true;
        }

        (outline, why) = (null, null);
        return false;
    }

    /// <summary>
    /// Keeps a glyph's outline where there is room, and answers the outline to hand over: the
    /// shared instance that is kept, or the one given where there is no room for it.
    /// </summary>
    internal PagePath KeepOutline(object owner, int glyph, PagePath outline, string why)
    {
        var known = _outlines.TryGetValue(owner, out var set);
        var bytes = OutlineEntryBytes + (outline?.HeldBytes ?? 0) + 2L * (why?.Length ?? 0) + (known ? 0 : DataEntryBytes);
        if (bytes > OutlineByteBudget - _outlineBytes)
            return outline;

        if (!known)
            _outlines[owner] = set = new OutlineSet();
        outline = outline?.Shared();
        set.Glyphs[glyph] = (outline, why);
        set.Bytes += bytes;
        set.LastUsed = ++_clock;
        _outlineBytes += bytes;
        return outline;
    }

    /// <summary>The outlines kept of one font program's glyphs, or of one source's.</summary>
    private sealed class OutlineSet
    {
        public readonly Dictionary<int, (PagePath Outline, string Why)> Glyphs = new();
        public long Bytes;
        public long LastUsed;
    }

    private sealed class FontDataEntry(FontDataKey key, IFontData data, long bytes)
    {
        public FontDataKey Key { get; } = key;
        public IFontData Data { get; } = data;
        public long Bytes { get; } = bytes;
    }

    /// <summary>A kind of data and the object it was read from, compared by which object that is.</summary>
    private readonly struct FontDataKey(FontDataKind kind, object source)
    {
        public static readonly IEqualityComparer<FontDataKey> ByIdentity = new IdentityComparer();

        private readonly FontDataKind _kind = kind;
        private readonly object _source = source;

        private sealed class IdentityComparer : IEqualityComparer<FontDataKey>
        {
            public bool Equals(FontDataKey x, FontDataKey y) => x._kind == y._kind && ReferenceEquals(x._source, y._source);

            public int GetHashCode(FontDataKey key) => RuntimeHelpers.GetHashCode(key._source) * 31 + (int)key._kind;
        }
    }
}

/// <summary>
/// A font program was not read because the page has read all it may. Unlike a font program that
/// cannot be read, this is true for the page and not for the font, so nothing is kept of it.
/// </summary>
internal sealed class FontProgramNotReadException(string message) : Exception(message);
