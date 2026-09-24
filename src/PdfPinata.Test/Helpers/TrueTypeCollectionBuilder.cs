using System;
using System.Collections.Generic;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Packs single-font files into a TrueType collection, so that collection handling can be
///   tested against the fonts already shipped with the tests rather than against a machine that
///   may or may not have one installed.
/// </summary>
/// <remarks>
///   Tables with identical bytes are stored once and pointed at by every face that uses them.
///   That is what a real collection does, and it is the case worth testing: extracting one face
///   has to copy a shared table rather than reference it.
/// </remarks>
internal static class TrueTypeCollectionBuilder
{
    private const uint TagTtcf = 0x74746366;

    private const int OffsetTableLength = 12;

    private const int TableRecordLength = 16;

    public static byte[] Build(params byte[][] fonts)
    {
        if (fonts == null || fonts.Length == 0)
            throw new ArgumentException(@"A collection needs at least one font.", nameof(fonts));

        var tableCounts = new int[fonts.Length];
        for (var i = 0; i < fonts.Length; i++)
            tableCounts[i] = U16(fonts[i], 4);

        // Header, then one directory per face, then the pooled table data.
        var position = OffsetTableLength + fonts.Length * 4;

        var directoryOffsets = new int[fonts.Length];
        for (var i = 0; i < fonts.Length; i++)
        {
            directoryOffsets[i] = position;
            position += OffsetTableLength + tableCounts[i] * TableRecordLength;
        }

        // Pool the table bytes, sharing anything that appears twice.
        var pooled = new Dictionary<string, int>(StringComparer.Ordinal);
        var placements = new int[fonts.Length][];

        for (var i = 0; i < fonts.Length; i++)
            placements[i] = PlaceTables(fonts[i], tableCounts[i], pooled, ref position);

        var collection = new byte[position];

        W32(collection, 0, TagTtcf);
        W32(collection, 4, 0x00010000);
        W32(collection, 8, (uint)fonts.Length);

        for (var i = 0; i < fonts.Length; i++)
            W32(collection, OffsetTableLength + i * 4, (uint)directoryOffsets[i]);

        var written = new HashSet<int>();

        for (var i = 0; i < fonts.Length; i++)
            WriteFace(collection, fonts[i], directoryOffsets[i], tableCounts[i], placements[i], written);

        return collection;
    }

    /// <summary>
    ///   Where each of one font's tables goes in the pooled data, placing a table whose bytes are
    ///   not pooled yet at <paramref name="position"/> and moving it past.
    /// </summary>
    private static int[] PlaceTables(byte[] font, int tableCount, Dictionary<string, int> pooled, ref int position)
    {
        var placements = new int[tableCount];

        for (var t = 0; t < tableCount; t++)
        {
            var record = OffsetTableLength + t * TableRecordLength;
            var offset = (int)U32(font, record + 8);
            var length = (int)U32(font, record + 12);

            var bytes = new byte[length];
            Buffer.BlockCopy(font, offset, bytes, 0, length);

            var key = Convert.ToBase64String(bytes);
            if (!pooled.TryGetValue(key, out var placed))
            {
                placed = position;
                position += Align4(length);
                pooled.Add(key, placed);
            }

            placements[t] = placed;
        }

        return placements;
    }

    /// <summary>
    ///   Writes one face's directory, and whichever of its tables no earlier face has written.
    /// </summary>
    private static void WriteFace(byte[] collection, byte[] font, int directoryOffset, int tableCount,
        int[] placements, HashSet<int> written)
    {
        // The offset table carries over as-is; only the table offsets need rewriting.
        Buffer.BlockCopy(font, 0, collection, directoryOffset, OffsetTableLength);

        for (var t = 0; t < tableCount; t++)
        {
            var source = OffsetTableLength + t * TableRecordLength;
            var target = directoryOffset + OffsetTableLength + t * TableRecordLength;
            var length = (int)U32(font, source + 12);

            Buffer.BlockCopy(font, source, collection, target, 8);
            W32(collection, target + 8, (uint)placements[t]);
            W32(collection, target + 12, (uint)length);

            if (written.Add(placements[t]))
                Buffer.BlockCopy(font, (int)U32(font, source + 8), collection, placements[t], length);
        }
    }

    private static int Align4(int length) => (length + 3) & ~3;

    private static int U16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

    private static uint U32(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                                          | ((uint)data[offset + 2] << 8) | data[offset + 3];
    }

    private static void W32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
