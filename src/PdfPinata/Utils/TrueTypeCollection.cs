using System;
using System.IO;


namespace PdfPinata.Utils;

/// <summary>
/// Reads TrueType/OpenType collection files (.ttc, .otc) and lifts one face out of a collection
/// as a standalone font.
/// </summary>
/// <remarks>
/// Nothing below the font resolver understands collections: <c>OpenTypeFontface.Read</c> throws
/// on the 'ttcf' signature. Teaching every layer between the resolver and the subsetter to carry
/// a face index is not possible without changing <c>IFontResolver.GetFont(string)</c>, which
/// takes a name and nothing else and is implemented by every custom resolver in the wild. So the
/// index travels inside the face name instead - "msgothic.ttc#1" - and the resolver answers with
/// an extracted single-font sfnt. Nothing downstream ever sees a collection.
/// </remarks>
public static class TrueTypeCollection
{
    /// <summary>
    /// Signature of a collection file, big-endian 'ttcf'.
    /// </summary>
    private const uint TagTtcf = 0x74746366;

    private const int OffsetTableLength = 12;

    private const int TableRecordLength = 16;

    /// <summary>
    /// Separates the file name from the face index in a face name.
    /// </summary>
    public const char FaceIndexSeparator = '#';


    /// <summary>
    /// Builds the face name for one member of a collection.
    /// </summary>
    public static string FaceName(string fileName, int faceIndex)
    {
        return fileName + FaceIndexSeparator + faceIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }


    /// <summary>
    /// Gets a value indicating whether the given bytes start a font collection.
    /// </summary>
    public static bool IsCollection(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data.Length >= OffsetTableLength && U32(data, 0) == TagTtcf;
    }


    /// <summary>
    /// Gets the number of faces the given bytes hold. A font that is not a collection holds one.
    /// </summary>
    public static int FaceCount(byte[] data)
    {
        return !IsCollection(data) ? 1 : ValidateFaceCount(U32(data, 8), data.Length);
    }


    /// <summary>
    /// Reads from the file's header alone whether it is a collection, and how many faces it
    /// holds. Sets <paramref name="faceCount"/> to 1 and returns false for a single font.
    /// </summary>
    /// <remarks>
    /// The signature is read rather than the extension, so that a collection saved under a
    /// .ttf name is still taken apart instead of reaching the parser whole.
    /// </remarks>
    public static bool TryGetFaceCount(string path, out int faceCount)
    {
        faceCount = 1;

        using (var stream = File.OpenRead(path))
        {
            var header = new byte[OffsetTableLength];

            var read = 0;
            while (read < header.Length)
            {
                var count = stream.Read(header, read, header.Length - read);
                if (count == 0)
                    return false; // Too short to be a collection, and too short to be a font.
                read += count;
            }

            if (U32(header, 0) != TagTtcf)
                return false;

            faceCount = ValidateFaceCount(U32(header, 8), stream.Length);
            return true;
        }
    }


    /// <summary>
    /// Each face costs a four-byte offset in the collection header, so a file cannot declare
    /// more faces than it has room to point at. Catches truncation and garbage here, rather
    /// than as a very long loop over faces that do not exist.
    /// </summary>
    private static int ValidateFaceCount(uint count, long fileLength)
    {
        if (count == 0 || count > (fileLength - OffsetTableLength) / 4)
            throw new InvalidOperationException(
                "Font collection declares " + count + " faces, which a file of " + fileLength + " bytes cannot hold.");

        return (int)count;
    }


    /// <summary>
    /// Extracts one face of a collection as a standalone font. Bytes that are not a collection
    /// are returned unchanged, provided face 0 is the one asked for.
    /// </summary>
    /// <remarks>
    /// Tables shared between the faces of the collection are copied, which is the point of
    /// extracting: the result has to stand on its own.
    /// <para>
    /// The table checksums in the directory stay valid, because no table's bytes are touched.
    /// The whole-file checksum in 'head' (checkSumAdjustment) does not, and is left stale: no
    /// viewer verifies it, and the subsetter rewrites the file before it reaches one anyway.
    /// </para>
    /// </remarks>
    public static byte[] ExtractFace(byte[] data, int faceIndex)
    {
        if (!IsCollection(data))
        {
            if (faceIndex != 0)
                throw new ArgumentOutOfRangeException(nameof(faceIndex),
                    "Font is not a collection and holds face 0 alone.");

            return data;
        }

        var faceCount = ValidateFaceCount(U32(data, 8), data.Length);
        if (faceIndex < 0 || faceIndex >= faceCount)
            throw new ArgumentOutOfRangeException(nameof(faceIndex),
                "Font collection holds " + faceCount + " faces; face " + faceIndex + " was asked for.");

        var directory = (int)U32(data, OffsetTableLength + faceIndex * 4);
        if (directory < 0 || directory + OffsetTableLength > data.Length)
            throw new InvalidOperationException("Font collection points at a face outside the file.");

        var sfntVersion = U32(data, directory);
        var tableCount = U16(data, directory + 4);
        if (tableCount == 0)
            throw new InvalidOperationException("Font collection face declares no tables.");

        var records = directory + OffsetTableLength;
        if (records + tableCount * TableRecordLength > data.Length)
            throw new InvalidOperationException("Font collection face declares more tables than the file holds.");

        // Lay the new file out: offset table, then the directory, then each table 4-byte aligned.
        var lengths = new int[tableCount];
        var sources = new int[tableCount];
        var position = OffsetTableLength + tableCount * TableRecordLength;
        var targets = new int[tableCount];

        for (var idx = 0; idx < tableCount; idx++)
        {
            var record = records + idx * TableRecordLength;
            var offset = (int)U32(data, record + 8);
            var length = (int)U32(data, record + 12);

            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new InvalidOperationException("Font collection face points at table data outside the file.");

            sources[idx] = offset;
            lengths[idx] = length;
            targets[idx] = position;
            position += Align4(length);
        }

        var font = new byte[position];

        W32(font, 0, sfntVersion);
        W16(font, 4, tableCount);

        // The binary-search hints of the offset table describe this directory, not the one the
        // face had inside the collection, so they are recomputed rather than copied.
        var entrySelector = EntrySelector(tableCount);
        var searchRange = (1 << entrySelector) * TableRecordLength;
        W16(font, 6, searchRange);
        W16(font, 8, entrySelector);
        W16(font, 10, tableCount * TableRecordLength - searchRange);

        for (var idx = 0; idx < tableCount; idx++)
        {
            var source = records + idx * TableRecordLength;
            var target = OffsetTableLength + idx * TableRecordLength;

            // Tag and checksum carry over untouched; only the offset is rewritten.
            Buffer.BlockCopy(data, source, font, target, 8);
            W32(font, target + 8, (uint)targets[idx]);
            W32(font, target + 12, (uint)lengths[idx]);

            Buffer.BlockCopy(data, sources[idx], font, targets[idx], lengths[idx]);
        }

        return font;
    }


    /// <summary>
    /// floor(log2(count)), which is what the offset table's entrySelector holds.
    /// </summary>
    private static int EntrySelector(int count)
    {
        var selector = 0;
        while (1 << (selector + 1) <= count)
            selector++;

        return selector;
    }


    private static int Align4(int length)
    {
        return (length + 3) & ~3;
    }


    private static int U16(byte[] data, int offset)
    {
        return (data[offset] << 8) | data[offset + 1];
    }


    private static uint U32(byte[] data, int offset)
    {
        return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                                          | ((uint)data[offset + 2] << 8) | data[offset + 3];
    }


    private static void W16(byte[] data, int offset, int value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }


    private static void W32(byte[] data, int offset, uint value)
    {
        data[offset] = (byte)(value >> 24);
        data[offset + 1] = (byte)(value >> 16);
        data[offset + 2] = (byte)(value >> 8);
        data[offset + 3] = (byte)value;
    }
}
