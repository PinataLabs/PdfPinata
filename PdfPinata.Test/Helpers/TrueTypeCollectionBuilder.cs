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
            throw new ArgumentException("A collection needs at least one font.", nameof(fonts));

        var tableCounts = new int[fonts.Length];
        for (int i = 0; i < fonts.Length; i++)
            tableCounts[i] = U16(fonts[i], 4);

        // Header, then one directory per face, then the pooled table data.
        int position = OffsetTableLength + fonts.Length * 4;

        var directoryOffsets = new int[fonts.Length];
        for (int i = 0; i < fonts.Length; i++)
        {
            directoryOffsets[i] = position;
            position += OffsetTableLength + tableCounts[i] * TableRecordLength;
        }

        // Pool the table bytes, sharing anything that appears twice.
        var pooled = new Dictionary<string, int>(StringComparer.Ordinal);
        var data = new List<byte[]>();
        var placements = new int[fonts.Length][];

        for (int i = 0; i < fonts.Length; i++)
        {
            placements[i] = new int[tableCounts[i]];

            for (int t = 0; t < tableCounts[i]; t++)
            {
                int record = OffsetTableLength + t * TableRecordLength;
                int offset = (int)U32(fonts[i], record + 8);
                int length = (int)U32(fonts[i], record + 12);

                var bytes = new byte[length];
                Buffer.BlockCopy(fonts[i], offset, bytes, 0, length);

                string key = Convert.ToBase64String(bytes);
                if (!pooled.TryGetValue(key, out int placed))
                {
                    placed = position;
                    position += Align4(length);
                    pooled.Add(key, placed);
                    data.Add(bytes);
                }

                placements[i][t] = placed;
            }
        }

        var collection = new byte[position];

        W32(collection, 0, TagTtcf);
        W32(collection, 4, 0x00010000);
        W32(collection, 8, (uint)fonts.Length);

        for (int i = 0; i < fonts.Length; i++)
            W32(collection, OffsetTableLength + i * 4, (uint)directoryOffsets[i]);

        var written = new HashSet<int>();

        for (int i = 0; i < fonts.Length; i++)
        {
            // The offset table carries over as-is; only the table offsets need rewriting.
            Buffer.BlockCopy(fonts[i], 0, collection, directoryOffsets[i], OffsetTableLength);

            for (int t = 0; t < tableCounts[i]; t++)
            {
                int source = OffsetTableLength + t * TableRecordLength;
                int target = directoryOffsets[i] + OffsetTableLength + t * TableRecordLength;
                int length = (int)U32(fonts[i], source + 12);

                Buffer.BlockCopy(fonts[i], source, collection, target, 8);
                W32(collection, target + 8, (uint)placements[i][t]);
                W32(collection, target + 12, (uint)length);

                if (written.Add(placements[i][t]))
                    Buffer.BlockCopy(fonts[i], (int)U32(fonts[i], source + 8), collection, placements[i][t], length);
            }
        }

        return collection;
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
