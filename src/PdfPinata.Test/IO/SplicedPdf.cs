using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PdfPinata.Test.IO;

/// <summary>
///   A document written by hand whose objects begin at a stated offset, behind a comment of one
///   repeated byte that <see cref="SplicedStream" /> serves without holding it. It knows where
///   every object it wrote begins and ends, so a test can say which of them a reader touched.
/// </summary>
internal sealed class SplicedPdf
{
    /// <summary>What precedes the comment: a header, then the "%" the comment opens with.</summary>
    internal static readonly byte[] Head = Encoding.Latin1.GetBytes("%PDF-1.7\n%");

    private readonly long _origin;

    // The newline ends the comment the head opened; the tail's own content begins after it.
    private readonly StringBuilder _pdf = new("\n");
    private readonly Dictionary<int, long> _positions = new();
    private readonly Dictionary<int, long> _ends = new();
    private readonly Dictionary<int, (int Stream, int Index)> _compressed = new();

    /// <param name="origin">
    ///   Where the first byte after the comment lies. Left out, there is no comment to speak of
    ///   and the document is an ordinary small one.
    /// </param>
    internal SplicedPdf(long origin = 0) => _origin = origin == 0 ? Head.Length : origin;

    internal long Here => _origin + _pdf.Length;

    internal long PositionOf(int id) => _positions[id];

    /// <summary>
    ///   The bytes of an object that only reading that object reaches. Its first two are left out:
    ///   a lexer holds two characters ahead, so reading the object written before this one reads
    ///   them too.
    /// </summary>
    internal (long Start, long End) BodyOf(int id) => (_positions[id] + 2, _ends[id]);

    internal void Append(string text) => _pdf.Append(text);

    internal void Object(int id, string body) => ObjectUnderTheHeader(id, id, body);

    /// <summary>
    ///   An object the cross-reference sections file under <paramref name="id" /> and whose own
    ///   header says <paramref name="headerId" />.
    /// </summary>
    internal void ObjectUnderTheHeader(int id, int headerId, string body)
    {
        _positions[id] = Here;
        _pdf.Append(headerId).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
        _ends[id] = Here;
    }

    /// <summary>
    ///   Has the cross-reference sections say object <paramref name="id" /> begins here, where
    ///   <paramref name="text" /> is written instead of one.
    /// </summary>
    internal void InPlaceOfObject(int id, string text)
    {
        _positions[id] = Here;
        _pdf.Append(text).Append('\n');
        _ends[id] = Here;
    }

    internal static string Stream(string content, string entries = "") =>
        "<< " + entries + " /Length " + content.Length + " >>stream\n" + content + "\nendstream";

    /// <summary>Objects 1 to 4: a catalog, a page tree, a page, and the page's content.</summary>
    internal void OnePage(string content, string resources = "")
    {
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R " + resources + " >>");
        Object(4, Stream(content));
    }

    /// <summary>The number of the dictionary of the page at <paramref name="index" />, counted from zero.</summary>
    internal static int PageId(int index) => 3 + 2 * index;

    /// <summary>The number of the content stream of the page at <paramref name="index" />.</summary>
    internal static int ContentId(int index) => 4 + 2 * index;

    /// <summary>What <see cref="Pages" /> draws on the page at <paramref name="index" />.</summary>
    internal static string ContentOfPage(int index) => index + " 0 0 rg 20 20 100 100 re f";

    /// <summary>
    ///   A catalog, a page tree and <paramref name="count" /> pages, each a dictionary and a content
    ///   stream numbered by <see cref="PageId" /> and <see cref="ContentId" />.
    /// </summary>
    internal void Pages(int count)
    {
        var kids = string.Join(" ", Enumerable.Range(0, count).Select(index => PageId(index) + " 0 R"));
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [" + kids + "] /Count " + count + " >>");
        for (var index = 0; index < count; index++)
        {
            Object(PageId(index), PageDictionary(index));
            Object(ContentId(index), Stream(ContentOfPage(index)));
        }
    }

    internal static string PageDictionary(int index, string entries = "") =>
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents " + ContentId(index) + " 0 R " +
        entries + " >>";

    /// <summary>
    ///   A comment of <paramref name="length" /> bytes. The scan for "startxref" reads the end of
    ///   the file a buffer at a time, so objects closer to the end than one buffer are read by it
    ///   whoever asks; this is what keeps them further away.
    /// </summary>
    internal void Padding(int length) => _pdf.Append('%').Append('A', length).Append('\n');

    /// <summary>
    ///   An object stream holding <paramref name="objects" />, each of which a later
    ///   <see cref="CrossReferenceStream" /> lists as compressed in it.
    /// </summary>
    internal void ObjectStream(int id, params (int Number, string Body)[] objects)
    {
        var header = new StringBuilder();
        var bodies = new StringBuilder();
        for (var index = 0; index < objects.Length; index++)
        {
            header.Append(objects[index].Number).Append(' ').Append(bodies.Length).Append(' ');
            bodies.Append(objects[index].Body).Append('\n');
            _compressed[objects[index].Number] = (id, index);
            _positions.Remove(objects[index].Number);
        }

        Object(id, Stream(header.ToString() + bodies,
            "/Type /ObjStm /N " + objects.Length + " /First " + header.Length));
    }

    /// <summary>
    ///   A classic section of one subsection, its trailer and the startxref naming it, and
    ///   answers where it begins. Object 0 is the free list's head; <paramref name="free" />
    ///   are marked free in subsections of their own.
    /// </summary>
    internal long ClassicTable(int firstId, int count, string trailer, int[] free = null)
    {
        var start = Here;
        _pdf.Append("xref\n").Append(firstId).Append(' ').Append(count).Append('\n');
        for (var id = firstId; id < firstId + count; id++)
            _pdf.Append(id == 0 ? "0000000000 65535 f \n" : _positions[id].ToString("D10") + " 00000 n \n");
        foreach (var id in free ?? [])
            _pdf.Append(id).Append(" 1\n0000000000 65535 f \n");

        _pdf.Append("trailer\n<< ").Append(trailer).Append(" >>\n");
        _pdf.Append("startxref\n").Append(start).Append("\n%%EOF\n");
        return start;
    }

    /// <summary>
    ///   A cross-reference stream numbered <paramref name="id" /> and the startxref naming it, and
    ///   answers where it begins. It indexes every object written so far, or
    ///   <paramref name="only" /> those given, and itself.
    /// </summary>
    internal long CrossReferenceStream(int id, string entries = "", int[] only = null)
    {
        var start = Here;
        _positions[id] = start;

        var numbers = only == null ? Enumerable.Range(0, id + 1).ToArray() : [.. only, id];
        var rows = new StringBuilder();
        foreach (var number in numbers)
        {
            if (_positions.TryGetValue(number, out var position))
                rows.Append(Row(1, position, 0));
            else if (_compressed.TryGetValue(number, out var place))
                rows.Append(Row(2, place.Stream, place.Index));
            else
                rows.Append(Row(0, 0, 0));
        }

        var index = only == null ? "" : "/Index [" + string.Join(" ", numbers.Select(number => number + " 1")) + "] ";
        Object(id, Stream(rows.ToString(),
            "/Type /XRef /Size " + (id + 1) + " /W [1 5 1] /Root 1 0 R " + index + entries));
        _pdf.Append("startxref\n").Append(start).Append("\n%%EOF\n");
        return start;
    }

    /// <summary>One cross-reference stream row of the widths [1 5 1], as one character per byte.</summary>
    internal static string Row(int type, long field2, int field3)
    {
        var row = new StringBuilder().Append((char)type);
        for (var shift = 4; shift >= 0; shift--)
            row.Append((char)((field2 >> (shift * 8)) & 0xFF));
        return row.Append((char)field3).ToString();
    }

    internal byte[] TailBytes() => Encoding.Latin1.GetBytes(_pdf.ToString());

    internal SplicedStream ToStream() => new(Head, _origin - Head.Length, (byte)'A', TailBytes());

    /// <summary>The whole document as bytes, for one whose comment is short enough to hold.</summary>
    internal byte[] ToBytes()
    {
        using var stream = ToStream();
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
