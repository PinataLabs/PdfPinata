using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Writes a TrueType font program by hand, with only the tables page rendering reads: <c>head</c>,
/// <c>maxp</c>, <c>loca</c>, <c>glyf</c>, and the <c>cmap</c> subtables and <c>post</c> names a
/// test asks for.
/// </summary>
/// <remarks>
/// Glyph 0 is always .notdef, a rectangle. Every glyph added with <see cref="AddBar"/> is a
/// rectangle 700 units high whose width says which glyph it is, so a test reads which glyph was
/// drawn off the outline it was handed.
/// </remarks>
internal sealed class TrueTypeFontBuilder
{
    private readonly List<byte[]> _glyphs = [];
    private readonly List<(int Platform, int Encoding, Dictionary<int, int> Map)> _cmaps = [];
    private string[] _postNames;

    public TrueTypeFontBuilder()
    {
        AddRectangle(0, 0, 500, 700);
    }

    /// <summary>The font units in one em.</summary>
    public int UnitsPerEm { get; init; } = 1000;

    /// <summary>The number of glyphs added, .notdef included.</summary>
    public int GlyphCount => _glyphs.Count;

    /// <summary>
    /// A glyph that is a bar from (0, 0) to (<paramref name="width"/>, 700): what a test tells it
    /// by.
    /// </summary>
    public int AddBar(int width) => AddRectangle(0, 0, width, 700);

    /// <summary>A glyph that is one closed rectangle, wound counterclockwise.</summary>
    public int AddRectangle(int left, int bottom, int right, int top)
    {
        var glyph = new BigEndian();
        glyph.Int16(1).Int16(left).Int16(bottom).Int16(right).Int16(top);
        glyph.UInt16(3).UInt16(0);
        glyph.Bytes(1, 1, 1, 1);
        foreach (var delta in new[] { left, right - left, 0, left - right })
            glyph.Int16(delta);
        foreach (var delta in new[] { bottom, 0, top - bottom, 0 })
            glyph.Int16(delta);
        return Add(glyph.ToArray());
    }

    /// <summary>
    /// A glyph of one contour through these points, each on the curve or a quadratic control point.
    /// </summary>
    public int AddContour(params (int X, int Y, bool OnCurve)[] points)
    {
        var glyph = new BigEndian();
        glyph.Int16(1).Int16(0).Int16(0).Int16(0).Int16(0);
        glyph.UInt16(points.Length - 1).UInt16(0);
        foreach (var point in points)
            glyph.Bytes((byte)(point.OnCurve ? 1 : 0));
        var previous = 0;
        foreach (var point in points)
        {
            glyph.Int16(point.X - previous);
            previous = point.X;
        }

        previous = 0;
        foreach (var point in points)
        {
            glyph.Int16(point.Y - previous);
            previous = point.Y;
        }

        return Add(glyph.ToArray());
    }

    /// <summary>A glyph with no contours: a space.</summary>
    public int AddEmpty() => Add([]);

    /// <summary>A glyph whose data is exactly these bytes.</summary>
    public int Add(byte[] data)
    {
        _glyphs.Add(data);
        return _glyphs.Count - 1;
    }

    /// <summary>Replaces a glyph's data, so that a glyph can be built of one added after it.</summary>
    public void Replace(int glyph, byte[] data) => _glyphs[glyph] = data;

    /// <summary>The data of a composite glyph built of <paramref name="components"/>.</summary>
    public static byte[] Composite(params Component[] components)
    {
        var glyph = new BigEndian();
        glyph.Int16(-1).Int16(0).Int16(0).Int16(0).Int16(0);
        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            var flags = ArgumentsAreWords | component.Flags;
            if (component.PointMatched is null)
                flags |= ArgumentsAreXYValues;
            if (component.Scale != null)
                flags |= WeHaveAScale;
            else if (component.XYScale != null)
                flags |= WeHaveAnXAndYScale;
            else if (component.TwoByTwo != null)
                flags |= WeHaveATwoByTwo;
            if (i < components.Length - 1)
                flags |= MoreComponents;

            glyph.UInt16(flags).UInt16(component.Glyph);
            if (component.PointMatched is { } points)
                glyph.UInt16(points.Parent).UInt16(points.Child);
            else
                glyph.Int16(component.Dx).Int16(component.Dy);
            var transform = component.Scale is { } scale ? [scale]
                : component.XYScale is { } xy ? [xy.X, xy.Y]
                : component.TwoByTwo is { } m ? new[] { m.A, m.B, m.C, m.D }
                : Array.Empty<double>();
            foreach (var value in transform)
                glyph.Int16((int)Math.Round(value * 16384));
        }

        return glyph.ToArray();
    }

    /// <summary>Maps characters or codes to glyphs in a <c>cmap</c> subtable of format 4, or format 0 for (1,0).</summary>
    public TrueTypeFontBuilder Map(int platform, int encoding, Dictionary<int, int> map)
    {
        _cmaps.Add((platform, encoding, map));
        return this;
    }

    /// <summary>Gives the glyphs names in a <c>post</c> table of format 2, null for a standard name's index 0.</summary>
    public TrueTypeFontBuilder Name(params string[] names)
    {
        _postNames = names;
        return this;
    }

    /// <summary>The font program.</summary>
    /// <param name="locaEnd">Overrides where <c>loca</c> says the glyphs end, for a font that is broken.</param>
    public byte[] Build(Func<int, uint?> locaEnd = null)
    {
        var glyf = new BigEndian();
        var loca = new BigEndian();
        for (var i = 0; i < _glyphs.Count; i++)
        {
            loca.UInt32((uint)glyf.Length);
            glyf.Bytes(_glyphs[i]);
            while (glyf.Length % 4 != 0)
                glyf.Bytes(0);
        }

        loca.UInt32((uint)glyf.Length);
        var locaBytes = loca.ToArray();
        if (locaEnd != null)
        {
            for (var i = 0; i < _glyphs.Count; i++)
            {
                if (locaEnd(i) is { } end)
                    BitConverterBigEndian(locaBytes, 4 * (i + 1), end);
            }
        }

        var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["head"] = Head(),
            ["maxp"] = new BigEndian().UInt32(0x00005000).UInt16(_glyphs.Count).ToArray(),
            ["loca"] = locaBytes,
            ["glyf"] = glyf.ToArray()
        };
        if (_cmaps.Count > 0)
            tables["cmap"] = Cmap();
        if (_postNames != null)
            tables["post"] = Post();

        var font = new BigEndian();
        font.UInt32(0x00010000).UInt16(tables.Count).UInt16(0).UInt16(0).UInt16(0);
        // The directory is in tag order, and glyf is written last, so that cutting the file short
        // cuts into the glyphs.
        var order = tables.Keys.Where(tag => tag != "glyf").Append("glyf").ToList();
        var offsets = new Dictionary<string, int>();
        var offset = 12 + 16 * tables.Count;
        foreach (var tag in order)
        {
            offsets[tag] = offset;
            offset += (tables[tag].Length + 3) & ~3;
        }

        foreach (var (tag, data) in tables)
            font.Bytes(Encoding.ASCII.GetBytes(tag)).UInt32(0).UInt32((uint)offsets[tag]).UInt32((uint)data.Length);

        foreach (var tag in order)
        {
            font.Bytes(tables[tag]);
            while (font.Length % 4 != 0)
                font.Bytes(0);
        }

        return font.ToArray();
    }

    private byte[] Head()
    {
        var head = new BigEndian();
        head.UInt32(0x00010000).UInt32(0x00010000).UInt32(0).UInt32(0x5F0F3CF5).UInt16(0).UInt16(UnitsPerEm);
        head.UInt32(0).UInt32(0).UInt32(0).UInt32(0);
        head.Int16(0).Int16(0).Int16(1000).Int16(1000);
        head.UInt16(0).UInt16(8).Int16(2).Int16(1).Int16(0);
        return head.ToArray();
    }

    private byte[] Cmap()
    {
        var subtables = _cmaps.Select(cmap => cmap.Platform == 1 ? Format0(cmap.Map) : Format4(cmap.Map)).ToList();
        var cmap = new BigEndian();
        cmap.UInt16(0).UInt16(_cmaps.Count);
        var offset = 4 + 8 * _cmaps.Count;
        for (var i = 0; i < _cmaps.Count; i++)
        {
            cmap.UInt16(_cmaps[i].Platform).UInt16(_cmaps[i].Encoding).UInt32((uint)offset);
            offset += subtables[i].Length;
        }

        foreach (var subtable in subtables)
            cmap.Bytes(subtable);
        return cmap.ToArray();
    }

    private static byte[] Format0(Dictionary<int, int> map)
    {
        var table = new BigEndian().UInt16(0).UInt16(262).UInt16(0);
        for (var code = 0; code < 256; code++)
            table.Bytes((byte)(map.TryGetValue(code, out var glyph) ? glyph : 0));
        return table.ToArray();
    }

    /// <summary>One segment for each character, then the closing segment at 0xFFFF.</summary>
    private static byte[] Format4(Dictionary<int, int> map)
    {
        var characters = map.Keys.OrderBy(c => c).ToList();
        var segments = characters.Count + 1;
        var table = new BigEndian();
        table.UInt16(4).UInt16(16 + 8 * segments).UInt16(0).UInt16(2 * segments).UInt16(0).UInt16(0).UInt16(0);
        foreach (var character in characters)
            table.UInt16(character);
        table.UInt16(0xFFFF).UInt16(0);
        foreach (var character in characters)
            table.UInt16(character);
        table.UInt16(0xFFFF);
        foreach (var character in characters)
            table.UInt16((map[character] - character) & 0xFFFF);
        table.UInt16(1);
        for (var i = 0; i < segments; i++)
            table.UInt16(0);
        return table.ToArray();
    }

    private byte[] Post()
    {
        var post = new BigEndian();
        post.UInt32(0x00020000).UInt32(0).Int16(0).Int16(0).UInt32(0).UInt32(0).UInt32(0).UInt32(0).UInt32(0);
        post.UInt16(_glyphs.Count);
        var extra = new List<string>();
        for (var glyph = 0; glyph < _glyphs.Count; glyph++)
        {
            var name = glyph < _postNames.Length ? _postNames[glyph] : null;
            if (name == null)
            {
                post.UInt16(0);
                continue;
            }

            post.UInt16(258 + extra.Count);
            extra.Add(name);
        }

        foreach (var name in extra)
            post.Bytes((byte)name.Length).Bytes(Encoding.ASCII.GetBytes(name));
        return post.ToArray();
    }

    private static void BitConverterBigEndian(byte[] bytes, int at, uint value)
    {
        bytes[at] = (byte)(value >> 24);
        bytes[at + 1] = (byte)(value >> 16);
        bytes[at + 2] = (byte)(value >> 8);
        bytes[at + 3] = (byte)value;
    }

    private const int ArgumentsAreWords = 0x0001;
    private const int ArgumentsAreXYValues = 0x0002;
    private const int WeHaveAScale = 0x0008;
    private const int MoreComponents = 0x0020;
    private const int WeHaveAnXAndYScale = 0x0040;
    private const int WeHaveATwoByTwo = 0x0080;

    /// <summary>Asks for a component's offset to be scaled with it.</summary>
    public const int ScaledComponentOffset = 0x0800;

    /// <summary>
    /// One component of a composite glyph: placed by an offset, or by matching a point of the
    /// components before it to one of its own, and transformed by one scale, a scale for x and one
    /// for y, or a 2 by 2 matrix written in the font's order: x' = A·x + C·y, y' = B·x + D·y.
    /// </summary>
    internal sealed record Component(int Glyph, int Dx = 0, int Dy = 0, double? Scale = null,
        (int Parent, int Child)? PointMatched = null, int Flags = 0, (double X, double Y)? XYScale = null,
        (double A, double B, double C, double D)? TwoByTwo = null);

    private sealed class BigEndian
    {
        private readonly MemoryStream _stream = new();

        public int Length => (int)_stream.Length;

        public BigEndian UInt16(int value)
        {
            _stream.WriteByte((byte)(value >> 8));
            _stream.WriteByte((byte)value);
            return this;
        }

        public BigEndian Int16(int value) => UInt16(value & 0xFFFF);

        public BigEndian UInt32(uint value)
        {
            UInt16((int)(value >> 16));
            return UInt16((int)(value & 0xFFFF));
        }

        public BigEndian Bytes(params byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
            return this;
        }

        public byte[] ToArray() => _stream.ToArray();
    }
}
