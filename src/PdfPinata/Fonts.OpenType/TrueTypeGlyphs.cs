using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata.Fonts;

namespace PdfPinata.Fonts.OpenType;

/// <summary>
/// The glyph outlines of a TrueType font program, and the tables that find a glyph: <c>cmap</c>
/// and <c>post</c>. Read from the bytes alone, every read checked against their end.
/// </summary>
/// <remarks>
/// <para>
/// This is what page rendering draws an embedded <c>/FontFile2</c> with, and what it is to draw a
/// face the font resolver serves with, through <see cref="OpenTypeFontface.Glyphs"/>. It is not
/// built on <see cref="OpenTypeFontface"/>'s own reading, which reads every table it knows when it
/// is made and reads past the end of a truncated one: an embedded subset with a broken <c>name</c>
/// or <c>OS/2</c> table would lose every glyph, where here only what is broken is lost.
/// </para>
/// <para>
/// Nothing here hints. A glyph is its outline as designed, in font units, y upwards.
/// </para>
/// <para>
/// An instance may be read from several threads. Of the two things it reads late, the <c>post</c>
/// names are built whole before they are kept, so two threads asking first at once each build
/// them, and the <c>cmap</c> subtables are found under a lock.
/// </para>
/// <para>
/// <b>What an instance keeps is the font program and what <see cref="HeldBytes"/> counts</b>: the
/// table directory, the <c>post</c> names once they are asked for, and one small object for each
/// <c>cmap</c> subtable asked for. Whoever holds many of these bounds them by that number.
/// Nothing else is built from the font program and kept.
/// </para>
/// </remarks>
internal sealed class TrueTypeGlyphs
{
    /// <summary>How deep composite glyphs may be built of one another. Real fonts go two or three deep.</summary>
    internal const int MaximumCompositeDepth = 8;

    /// <summary>
    /// How many components one glyph may be built of in all, counting those of its components.
    /// Without it, a few composites that each use the one below twice make a glyph of millions.
    /// </summary>
    internal const int MaximumComponents = 1024;

    /// <summary>
    /// How many points one glyph may have in all, counting those of its components: as many as
    /// one simple glyph can have. Without it, a composite of a thousand references to a glyph of
    /// 65,535 points is a few hundred bytes of font and gigabytes of outline.
    /// </summary>
    internal const int MaximumPoints = 65536;

    private readonly byte[] _data;
    private readonly Dictionary<string, TableRange> _tables;
    private readonly TableRange _glyf;
    private readonly TableRange _loca;
    private readonly bool _longOffsets;
    private readonly Dictionary<int, TrueTypeCharacterMap> _characterMaps = new();
    private Dictionary<string, int> _postNames;

    /// <summary>What one entry of the table directory or of the <c>post</c> names is taken to cost, with its string.</summary>
    private const int EntryBytes = 96;

    /// <summary>
    /// How many names a <c>post</c> table can use past the standard 258: a glyph's name is a
    /// 16-bit index. A table may list any number more, which no glyph can be given.
    /// </summary>
    private const int MaximumExtraNames = 0x10000 - 258;

    private TrueTypeGlyphs(byte[] data, Dictionary<string, TableRange> tables, int unitsPerEm, bool longOffsets, int glyphCount)
    {
        _data = data;
        _tables = tables;
        _glyf = tables[TableTagNames.Glyf];
        _loca = tables[TableTagNames.Loca];
        _longOffsets = longOffsets;
        UnitsPerEm = unitsPerEm;
        GlyphCount = glyphCount;

        // No more names are kept than there are glyphs, or than the standard 258 and those post
        // has room to index, nor more characters than post has bytes.
        long postNames = 0;
        if (tables.TryGetValue(TableTagNames.Post, out var post))
        {
            postNames = (long)EntryBytes * Math.Min(glyphCount, Math.Max(258, post.Length / 2))
                        + 2L * Math.Min(post.Length, MaximumExtraNames * 255);
        }

        HeldBytes = data.Length + (long)EntryBytes * (tables.Count + 8) + postNames;
    }

    /// <summary>The font units in one em, from <c>head</c>: what an outline is divided by to be one em wide.</summary>
    public int UnitsPerEm { get; }

    /// <summary>
    /// How many glyphs there are: <c>maxp</c>'s count, or fewer where <c>loca</c> has room for
    /// fewer. A glyph number at or past this has no glyph.
    /// </summary>
    public int GlyphCount { get; }

    /// <summary>How many bytes the font program is.</summary>
    public int Length => _data.Length;

    /// <summary>
    /// The most bytes this can come to hold: the font program, the table directory, and the
    /// <c>post</c> names, which are read only when a glyph is asked for by name. It is counted
    /// before they are read and does not change, so whoever charges for it charges once.
    /// </summary>
    public long HeldBytes { get; }

    /// <summary>Reads the table directory and the tables an outline needs.</summary>
    /// <param name="data">A TrueType font program. It is kept, not copied.</param>
    /// <exception cref="InvalidDataException">
    /// The bytes are not a TrueType font program, or lack <c>head</c>, <c>loca</c> or <c>glyf</c>.
    /// </exception>
    public static TrueTypeGlyphs Read(byte[] data)
    {
        var reader = new BigEndianReader(data, 0, data.Length);
        var version = reader.ReadUInt32();
        switch (version)
        {
            case 0x00010000:
            case 0x74727565: // 'true', as Apple writes it
                break;
            case 0x4F54544F: // 'OTTO'
                throw new InvalidDataException("The font program has PostScript outlines, not TrueType ones.");
            case 0x74746366: // 'ttcf'
                throw new InvalidDataException("The font program is a TrueType collection, which a PDF may not embed.");
            default:
                throw new InvalidDataException("The font program is not a TrueType font.");
        }

        var tableCount = reader.ReadUInt16();
        reader.Skip(6);
        var tables = new Dictionary<string, TableRange>(StringComparer.Ordinal);
        for (var i = 0; i < tableCount; i++)
        {
            var tag = reader.ReadTag();
            reader.Skip(4);
            var offset = reader.ReadUInt32();
            var length = reader.ReadUInt32();

            // A table that runs past the end of the data is kept as far as the data goes: a glyph
            // inside what is left can still be drawn. One that starts past the end is not there.
            if (offset >= (uint)data.Length)
                continue;
            var available = Math.Min(length, (uint)data.Length - offset);
            tables[tag] = new TableRange((int)offset, (int)available);
        }

        foreach (var required in new[] { TableTagNames.Head, TableTagNames.Loca, TableTagNames.Glyf })
        {
            if (!tables.ContainsKey(required))
                throw new InvalidDataException($"The font program has no '{required}' table.");
        }

        var head = tables[TableTagNames.Head].Reader(data);
        head.Skip(18);
        var unitsPerEm = head.ReadUInt16();
        head.Skip(30);
        var longOffsets = head.ReadInt16() != 0;

        // ISO 32000 and the OpenType specification both allow 16 to 16384. A value outside that
        // is a broken header, and 1000 and 2048 are what fonts are drawn on in practice.
        if (unitsPerEm is < 16 or > 16384)
            unitsPerEm = 2048;

        var loca = tables[TableTagNames.Loca];
        var locaEntries = loca.Length / (longOffsets ? 4 : 2);
        var glyphCount = Math.Max(0, locaEntries - 1);
        if (tables.TryGetValue(TableTagNames.MaxP, out var maxp) && maxp.Length >= 6)
        {
            var maxpReader = maxp.Reader(data);
            maxpReader.Skip(4);
            glyphCount = Math.Min(glyphCount, maxpReader.ReadUInt16());
        }

        return new TrueTypeGlyphs(data, tables, unitsPerEm, longOffsets, glyphCount);
    }

    /// <summary>
    /// The outline of a glyph, in font units, y upwards: <see cref="TrueTypeOutline.Empty"/> for
    /// a glyph with no contours, such as a space, and null for a glyph number the font has not.
    /// </summary>
    /// <exception cref="InvalidDataException">The glyph's data is malformed.</exception>
    public TrueTypeOutline GetOutline(int glyph)
    {
        if ((uint)glyph >= (uint)GlyphCount)
            return null;

        var builder = new OutlineBuilder();
        var budget = new Budget(MaximumComponents, MaximumPoints);
        AppendGlyph(builder, glyph, Transform.Identity, 0, ref budget);
        return builder.Count == 0 ? TrueTypeOutline.Empty : builder.ToOutline();
    }

    /// <summary>
    /// A <c>cmap</c> subtable, or null where the font has none for that platform and encoding or
    /// it is in a format this does not read.
    /// </summary>
    /// <param name="platform">The platform: 0 Unicode, 1 Macintosh, 3 Windows.</param>
    /// <param name="encoding">The platform's encoding: with Windows, 0 symbol, 1 Unicode BMP, 10 full Unicode.</param>
    public TrueTypeCharacterMap CharacterMap(int platform, int encoding)
    {
        // Kept, because a subtable finds out once whether it can be searched by halves, and a
        // caller asks for the same few subtables for each font dictionary over this program.
        var key = platform << 16 | encoding & 0xFFFF;
        lock (_characterMaps)
        {
            if (!_characterMaps.TryGetValue(key, out var map))
                _characterMaps[key] = map = FindCharacterMap(platform, encoding);
            return map;
        }
    }

    private TrueTypeCharacterMap FindCharacterMap(int platform, int encoding)
    {
        if (!_tables.TryGetValue(TableTagNames.CMap, out var cmap))
            return null;

        var reader = cmap.Reader(_data);
        if (!reader.TryReadUInt16(out _) || !reader.TryReadUInt16(out var count))
            return null;

        for (var i = 0; i < count; i++)
        {
            if (!reader.TryReadUInt16(out var platformId) || !reader.TryReadUInt16(out var encodingId)
                                                         || !reader.TryReadUInt32(out var offset))
            {
                return null;
            }

            if (platformId == platform && encodingId == encoding && offset < (uint)cmap.Length)
                return TrueTypeCharacterMap.At(_data, cmap.Offset + (int)offset, cmap.Offset + cmap.Length);
        }

        return null;
    }

    /// <summary>Whether the font program has a <c>cmap</c> table at all.</summary>
    public bool HasCharacterMaps => _tables.ContainsKey(TableTagNames.CMap);

    /// <summary>
    /// The glyph the <c>post</c> table gives a name to, or -1. Only formats 1 and 2 name glyphs,
    /// and where two glyphs have one name the first is the one meant.
    /// </summary>
    public int GlyphNamed(string name)
    {
        _postNames ??= ReadPostNames();
        return name != null && _postNames.TryGetValue(name, out var glyph) ? glyph : -1;
    }

    private Dictionary<string, int> ReadPostNames()
    {
        var names = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!_tables.TryGetValue(TableTagNames.Post, out var post))
            return names;

        var reader = post.Reader(_data);
        if (!reader.TryReadUInt32(out var format))
            return names;

        if (format == 0x00010000)
        {
            for (var glyph = 0; glyph < Math.Min(258, GlyphCount); glyph++)
                names.TryAdd(GlyphNames.MacStandardName(glyph), glyph);
            return names;
        }

        if (format != 0x00020000)
            return names;

        reader.Skip(28);
        if (!reader.TryReadUInt16(out var count))
            return names;

        var indices = new int[count];
        for (var glyph = 0; glyph < count; glyph++)
        {
            if (!reader.TryReadUInt16(out var index))
                return names;
            indices[glyph] = index;
        }

        // The names past the standard 258, each a length byte and that many bytes of ASCII.
        var extra = new List<string>();
        while (extra.Count < MaximumExtraNames && reader.TryReadByte(out var length) && reader.TryReadAscii(length, out var extraName))
            extra.Add(extraName);

        // A name given to a glyph the font has not names nothing.
        for (var glyph = 0; glyph < Math.Min(count, GlyphCount); glyph++)
        {
            var index = indices[glyph];
            var glyphName = index < 258 ? GlyphNames.MacStandardName(index)
                : index - 258 < extra.Count ? extra[index - 258] : null;
            if (glyphName != null)
                names.TryAdd(glyphName, glyph);
        }

        return names;
    }

    private void AppendGlyph(OutlineBuilder builder, int glyph, Transform transform, int depth, ref Budget budget)
    {
        if ((uint)glyph >= (uint)GlyphCount)
            throw new InvalidDataException($"A composite glyph is built of glyph {glyph}, which the font has not.");

        var locaReader = _loca.Reader(_data);
        var entrySize = _longOffsets ? 4 : 2;
        locaReader.Skip(glyph * entrySize);
        var start = _longOffsets ? locaReader.ReadUInt32() : locaReader.ReadUInt16() * 2u;
        var end = _longOffsets ? locaReader.ReadUInt32() : locaReader.ReadUInt16() * 2u;
        if (end == start)
            return;
        if (end < start || end > (uint)_glyf.Length)
            throw new InvalidDataException($"Glyph {glyph} lies outside the 'glyf' table.");

        var reader = new BigEndianReader(_data, _glyf.Offset + (int)start, _glyf.Offset + (int)end);
        var contours = reader.ReadInt16();
        reader.Skip(8);
        if (contours >= 0)
            AppendSimpleGlyph(builder, ref reader, glyph, contours, transform, ref budget);
        else
            AppendCompositeGlyph(builder, ref reader, glyph, transform, depth, ref budget);
    }

    private static void AppendSimpleGlyph(OutlineBuilder builder, ref BigEndianReader reader, int glyph, int contours,
        Transform transform, ref Budget budget)
    {
        if (contours == 0)
            return;

        var ends = new int[contours];
        var previous = -1;
        for (var i = 0; i < contours; i++)
        {
            ends[i] = reader.ReadUInt16();
            if (ends[i] <= previous)
                throw new InvalidDataException("A glyph's contours do not end in order.");
            previous = ends[i];
        }

        var pointCount = ends[contours - 1] + 1;
        budget.Points -= pointCount;
        if (budget.Points < 0)
            throw new InvalidDataException($"Glyph {glyph} has more than {MaximumPoints} points, counting its components'.");
        reader.Skip(reader.ReadUInt16());

        var flags = new byte[pointCount];
        for (var i = 0; i < pointCount;)
        {
            var flag = reader.ReadByte();
            flags[i++] = flag;
            if ((flag & RepeatFlag) != 0)
            {
                for (int repeat = reader.ReadByte(); repeat > 0 && i < pointCount; repeat--)
                    flags[i++] = flag;
            }
        }

        var xs = ReadCoordinates(ref reader, flags, XShortVector, XIsSameOrPositive);
        var ys = ReadCoordinates(ref reader, flags, YShortVector, YIsSameOrPositive);

        var first = builder.Count;
        for (var i = 0; i < pointCount; i++)
            builder.Add(transform.Apply(xs[i], ys[i]), (flags[i] & OnCurvePoint) != 0);
        foreach (var end in ends)
            builder.EndContour(first + end);
    }

    /// <summary>
    /// One coordinate of every point: each is a delta from the point before, one byte with its
    /// sign in the flags, or two bytes, or the same as before.
    /// </summary>
    private static int[] ReadCoordinates(ref BigEndianReader reader, byte[] flags, byte shortFlag, byte sameOrPositiveFlag)
    {
        var coordinates = new int[flags.Length];
        var value = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            var flag = flags[i];
            if ((flag & shortFlag) != 0)
            {
                var delta = reader.ReadByte();
                value += (flag & sameOrPositiveFlag) != 0 ? delta : -delta;
            }
            else if ((flag & sameOrPositiveFlag) == 0)
            {
                value += reader.ReadInt16();
            }

            coordinates[i] = value;
        }

        return coordinates;
    }

    private void AppendCompositeGlyph(OutlineBuilder builder, ref BigEndianReader reader, int glyph, Transform transform,
        int depth, ref Budget budget)
    {
        if (depth >= MaximumCompositeDepth)
            throw new InvalidDataException($"Glyph {glyph} is built of composite glyphs more than {MaximumCompositeDepth} deep.");

        var compositeStart = builder.Count;
        int flags;
        do
        {
            if (--budget.Components < 0)
                throw new InvalidDataException($"Glyph {glyph} is built of more than {MaximumComponents} components.");

            flags = reader.ReadUInt16();
            var component = reader.ReadUInt16();

            int argument1, argument2;
            var xyValues = (flags & ArgumentsAreXYValues) != 0;
            if ((flags & ArgumentsAreWords) != 0)
            {
                argument1 = xyValues ? reader.ReadInt16() : reader.ReadUInt16();
                argument2 = xyValues ? reader.ReadInt16() : reader.ReadUInt16();
            }
            else
            {
                argument1 = xyValues ? (sbyte)reader.ReadByte() : reader.ReadByte();
                argument2 = xyValues ? (sbyte)reader.ReadByte() : reader.ReadByte();
            }

            // The component's own 2 by 2 transform: x' = a·x + c·y, y' = b·x + d·y.
            double a = 1, b = 0, c = 0, d = 1;
            if ((flags & WeHaveAScale) != 0)
            {
                a = d = reader.ReadF2Dot14();
            }
            else if ((flags & WeHaveAnXAndYScale) != 0)
            {
                a = reader.ReadF2Dot14();
                d = reader.ReadF2Dot14();
            }
            else if ((flags & WeHaveATwoByTwo) != 0)
            {
                a = reader.ReadF2Dot14();
                b = reader.ReadF2Dot14();
                c = reader.ReadF2Dot14();
                d = reader.ReadF2Dot14();
            }

            var local = new Transform(a, b, c, d, 0, 0);
            var first = builder.Count;

            if (xyValues)
            {
                // The offset is scaled with the component only where the font asks for that. Not
                // scaling it is the OpenType default; Apple's TrueType makes scaling the default.
                double dx = argument1, dy = argument2;
                if ((flags & ScaledComponentOffset) != 0 && (flags & UnscaledComponentOffset) == 0)
                    (dx, dy) = local.Apply(dx, dy);
                AppendGlyph(builder, component, transform.After(local with { Dx = dx, Dy = dy }), depth + 1, ref budget);
            }
            else
            {
                // Point matching: the component is moved so that its point argument2 lies on
                // point argument1 of the components before it. Both are found once placed, so the
                // move is the difference of the two in the outer glyph's space.
                AppendGlyph(builder, component, transform.After(local), depth + 1, ref budget);
                var parent = compositeStart + argument1;
                var child = first + argument2;
                if (parent >= first || child >= builder.Count)
                    throw new InvalidDataException($"Glyph {glyph} matches a point its components do not have.");
                var (px, py) = builder.PointAt(parent);
                var (cx, cy) = builder.PointAt(child);
                builder.Shift(first, px - cx, py - cy);
            }
        }
        while ((flags & MoreComponents) != 0);
    }

    // Simple glyph flags.
    private const byte OnCurvePoint = 0x01;
    private const byte XShortVector = 0x02;
    private const byte YShortVector = 0x04;
    private const byte RepeatFlag = 0x08;
    private const byte XIsSameOrPositive = 0x10;
    private const byte YIsSameOrPositive = 0x20;

    // Composite glyph flags.
    private const int ArgumentsAreWords = 0x0001;
    private const int ArgumentsAreXYValues = 0x0002;
    private const int WeHaveAScale = 0x0008;
    private const int MoreComponents = 0x0020;
    private const int WeHaveAnXAndYScale = 0x0040;
    private const int WeHaveATwoByTwo = 0x0080;
    private const int ScaledComponentOffset = 0x0800;
    private const int UnscaledComponentOffset = 0x1000;

    /// <summary>What is left of the components and points one glyph may be built of.</summary>
    private struct Budget(int components, int points)
    {
        public int Components = components;
        public int Points = points;
    }

    private readonly record struct TableRange(int Offset, int Length)
    {
        public BigEndianReader Reader(byte[] data) => new(data, Offset, Offset + Length);
    }

    /// <summary>An affine transform of font units: x' = A·x + C·y + Dx, y' = B·x + D·y + Dy.</summary>
    private readonly record struct Transform(double A, double B, double C, double D, double Dx, double Dy)
    {
        public static readonly Transform Identity = new(1, 0, 0, 1, 0, 0);

        public (double X, double Y) Apply(double x, double y) => (A * x + C * y + Dx, B * x + D * y + Dy);

        /// <summary>This transform applied after <paramref name="inner"/>.</summary>
        public Transform After(Transform inner)
        {
            var (dx, dy) = Apply(inner.Dx, inner.Dy);
            return new Transform(
                A * inner.A + C * inner.B, B * inner.A + D * inner.B,
                A * inner.C + C * inner.D, B * inner.C + D * inner.D,
                dx, dy);
        }
    }

    /// <summary>The points of a glyph as they are gathered, components and all.</summary>
    private sealed class OutlineBuilder
    {
        private readonly List<TrueTypePoint> _points = [];
        private readonly List<int> _ends = [];

        public int Count => _points.Count;

        public void Add((double X, double Y) point, bool onCurve) => _points.Add(new TrueTypePoint(point.X, point.Y, onCurve));

        public void EndContour(int last) => _ends.Add(last);

        public (double X, double Y) PointAt(int index) => (_points[index].X, _points[index].Y);

        public void Shift(int from, double dx, double dy)
        {
            for (var i = from; i < _points.Count; i++)
                _points[i] = _points[i] with { X = _points[i].X + dx, Y = _points[i].Y + dy };
        }

        public TrueTypeOutline ToOutline() => new(_points.ToArray(), _ends.ToArray());
    }
}

/// <summary>A point of a TrueType outline, in font units: on the curve, or the control point of a quadratic curve.</summary>
internal readonly record struct TrueTypePoint(double X, double Y, bool OnCurve);

/// <summary>
/// The outline of one glyph as the font program has it: points in font units, y upwards, and where
/// each contour ends. A composite glyph's components are already placed.
/// </summary>
internal sealed class TrueTypeOutline
{
    internal TrueTypeOutline(TrueTypePoint[] points, int[] contourEnds)
    {
        Points = points;
        ContourEnds = contourEnds;
    }

    /// <summary>A glyph with no contours.</summary>
    public static TrueTypeOutline Empty { get; } = new([], []);

    /// <summary>Every point, contour after contour.</summary>
    public IReadOnlyList<TrueTypePoint> Points { get; }

    /// <summary>The index in <see cref="Points"/> of the last point of each contour.</summary>
    public IReadOnlyList<int> ContourEnds { get; }

    public bool IsEmpty => ContourEnds.Count == 0;
}

/// <summary>Reads big-endian numbers from a range of bytes, and throws where the range ends.</summary>
internal struct BigEndianReader(byte[] data, int start, int end)
{
    private int _position = start;

    public void Skip(int count) => _position = (int)Math.Min((long)_position + Math.Max(count, 0), end + 1L);

    public byte ReadByte() => TryReadByte(out var value) ? value : throw Truncated();

    public ushort ReadUInt16() => TryReadUInt16(out var value) ? value : throw Truncated();

    public short ReadInt16() => (short)ReadUInt16();

    public uint ReadUInt32() => TryReadUInt32(out var value) ? value : throw Truncated();

    public double ReadF2Dot14() => ReadInt16() / 16384.0;

    public string ReadTag() => TryReadAscii(4, out var value) ? value : throw Truncated();

    public bool TryReadByte(out byte value)
    {
        value = 0;
        if (_position + 1 > end)
            return false;
        value = data[_position++];
        return true;
    }

    public bool TryReadUInt16(out ushort value)
    {
        value = 0;
        if (_position + 2 > end)
            return false;
        value = (ushort)(data[_position] << 8 | data[_position + 1]);
        _position += 2;
        return true;
    }

    public bool TryReadUInt32(out uint value)
    {
        value = 0;
        if (_position + 4 > end)
            return false;
        value = (uint)(data[_position] << 24 | data[_position + 1] << 16 | data[_position + 2] << 8 | data[_position + 3]);
        _position += 4;
        return true;
    }

    public bool TryReadAscii(int length, out string value)
    {
        value = null;
        if (_position + length > end)
            return false;
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = (char)data[_position + i];
        _position += length;
        value = new string(chars);
        return true;
    }

    private static InvalidDataException Truncated() => new("The font program ends inside a table.");
}
