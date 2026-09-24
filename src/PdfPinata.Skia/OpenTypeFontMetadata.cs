
using System;
using System.IO;
using System.Text;

using PdfPinata.Drawing;


namespace PdfPinata.Utils;

/// <summary>
/// Reads the family name and style straight out of an OpenType/TrueType file.
/// SkiaSharp cannot be used for this: SKTypeface.FamilyName reports the typographic
/// (WWS) family, so "Arial Narrow" comes back as "Arial" and distinct families collapse
/// into one. The legacy family name (name ID 1) is what PdfPinata resolves against.
/// </summary>
internal static class OpenTypeFontMetadata
{
    private const int OffsetTableLength = 12;
    private const int TableRecordLength = 16;

    private const uint TagName = 0x6E616D65; // 'name'
    private const uint TagOs2 = 0x4F532F32;  // 'OS/2'
    private const uint TagHead = 0x68656164; // 'head'

    private const int NameIdFamily = 1;

    private const int PlatformUnicode = 0;
    private const int PlatformMacintosh = 1;
    private const int PlatformWindows = 3;

    private const int LanguageWindowsEnUs = 0x0409;


    public static FontMetadata Read(string path)
    {
        return Read(path, -1);
    }


    /// <param name="path">The font file to read.</param>
    /// <param name="faceIndex">
    /// The face to read out of a collection, or -1 for the first font in the file whether it is
    /// a collection or not.
    /// </param>
    public static FontMetadata Read(string path, int faceIndex)
    {
        return Read(File.ReadAllBytes(path), faceIndex);
    }


    internal static FontMetadata Read(byte[] data)
    {
        return Read(data, -1);
    }


    /// <summary>
    /// Reads every face of a collection from bytes already in hand, so that a file holding a
    /// dozen faces is opened once rather than a dozen times.
    /// </summary>
    internal static FontMetadata[] ReadAll(byte[] data, int faceCount)
    {
        var metadata = new FontMetadata[faceCount];

        for (var face = 0; face < faceCount; face++)
            metadata[face] = Read(data, face);

        return metadata;
    }


    internal static FontMetadata Read(byte[] data, int faceIndex)
    {
        var baseOffset = FaceOffset(data, faceIndex);

        // A collection is free to point at a face outside the file, and the offset table holds
        // the table count, so both are checked before anything is read through them.
        if (baseOffset < 0 || baseOffset + OffsetTableLength > data.Length)
            throw new InvalidOperationException("Font collection points at a face outside the file.");

        var numTables = U16(data, baseOffset + 4);

        if (baseOffset + OffsetTableLength + numTables * TableRecordLength > data.Length)
            throw new InvalidOperationException("Font declares more tables than the file holds.");

        FindTables(data, baseOffset, numTables, out var nameOffset, out var os2Offset, out var headOffset);

        if (nameOffset < 0)
            throw new InvalidOperationException("Font contains no 'name' table.");

        return new FontMetadata(ReadFamilyName(data, nameOffset), ReadStyle(data, os2Offset, headOffset));
    }

    /// <summary>
    /// Where the offset table of the face asked for begins: zero for a single font, or the entry
    /// the collection's directory holds for it.
    /// </summary>
    private static int FaceOffset(byte[] data, int faceIndex)
    {
        // A TrueType collection starts with a directory of fonts. TrueTypeCollection owns both
        // the signature check and the validation of the declared face count against the room the
        // file has to point at that many, so neither is repeated here.
        if (!TrueTypeCollection.IsCollection(data))
        {
            if (faceIndex > 0)
                throw new InvalidOperationException(
                    "Font is not a collection and holds face 0 alone; face " + faceIndex + " was asked for.");

            return 0;
        }

        var faceCount = TrueTypeCollection.FaceCount(data);
        var face = faceIndex < 0 ? 0 : faceIndex;

        if (face >= faceCount)
            throw new InvalidOperationException(
                "Font collection holds " + faceCount + " faces; face " + face + " was asked for.");

        return (int)U32(data, OffsetTableLength + face * 4);
    }

    /// <summary>
    /// The offsets of the 'name', 'OS/2' and 'head' tables, each -1 when the face has none.
    /// </summary>
    private static void FindTables(byte[] data, int baseOffset, int numTables,
        out int nameOffset, out int os2Offset, out int headOffset)
    {
        nameOffset = -1;
        os2Offset = -1;
        headOffset = -1;

        for (var i = 0; i < numTables; i++)
        {
            var record = baseOffset + OffsetTableLength + i * TableRecordLength;
            if (record + TableRecordLength > data.Length)
                break;

            var tag = U32(data, record);
            var offset = (int)U32(data, record + 8);

            if (tag == TagName) nameOffset = offset;
            else if (tag == TagOs2) os2Offset = offset;
            else if (tag == TagHead) headOffset = offset;
        }
    }


    private static string ReadFamilyName(byte[] data, int nameOffset)
    {
        var count = U16(data, nameOffset + 2);
        var stringBase = nameOffset + U16(data, nameOffset + 4);

        string best = null;
        var bestScore = int.MinValue;

        for (var i = 0; i < count; i++)
        {
            var record = nameOffset + 6 + i * 12;
            if (record + 12 > data.Length)
                break;

            var value = BetterFamilyName(data, record, stringBase, bestScore, out var score);
            if (value == null)
                continue;

            best = value;
            bestScore = score;
        }

        return best ?? throw new InvalidOperationException("Font contains no family name (name ID 1).");
    }

    /// <summary>
    /// The family name the name record at <paramref name="record"/> holds, when it is one, lies
    /// inside the file, is not empty and scores above <paramref name="bestScore"/>; null otherwise.
    /// </summary>
    private static string BetterFamilyName(byte[] data, int record, int stringBase, int bestScore, out int score)
    {
        score = int.MinValue;
        if (U16(data, record + 6) != NameIdFamily)
            return null;

        var platformId = U16(data, record);
        var languageId = U16(data, record + 4);
        var length = U16(data, record + 8);
        var offset = stringBase + U16(data, record + 10);

        if (offset < 0 || offset + length > data.Length)
            return null;

        score = ScoreName(platformId, languageId);
        if (score <= bestScore)
            return null;

        var value = Decode(data, offset, length, platformId);
        return string.IsNullOrEmpty(value) ? null : value;
    }


    /// <summary>
    /// Prefers the US-English entries, which is what the invariant-culture family name means.
    /// </summary>
    private static int ScoreName(int platformId, int languageId)
    {
        if (platformId == PlatformWindows && languageId == LanguageWindowsEnUs)
            return 4;
        if (platformId == PlatformMacintosh && languageId == 0)
            return 3;
        if (platformId == PlatformWindows)
            return 2;
        return platformId == PlatformUnicode ? 1 : 0;
    }


    private static string Decode(byte[] data, int offset, int length, int platformId)
    {
        // Windows and Unicode platform strings are UTF-16BE; Macintosh ones are single byte.
        var encoding = platformId == PlatformMacintosh
            ? Encoding.ASCII
            : Encoding.BigEndianUnicode;

        return encoding.GetString(data, offset, length).Trim('\0').Trim();
    }


    private static XFontStyle ReadStyle(byte[] data, int os2Offset, int headOffset)
    {
        var bold = false;
        var italic = false;

        if (os2Offset >= 0 && os2Offset + 64 <= data.Length)
        {
            // fsSelection: bit 0 ITALIC, bit 5 BOLD
            var fsSelection = U16(data, os2Offset + 62);
            italic = (fsSelection & 0x0001) != 0;
            bold = (fsSelection & 0x0020) != 0;
        }
        else if (headOffset >= 0 && headOffset + 46 <= data.Length)
        {
            // macStyle: bit 0 Bold, bit 1 Italic
            var macStyle = U16(data, headOffset + 44);
            bold = (macStyle & 0x0001) != 0;
            italic = (macStyle & 0x0002) != 0;
        }

        if (bold && italic)
            return XFontStyle.BoldItalic;
        if (bold)
            return XFontStyle.Bold;
        return italic ? XFontStyle.Italic : XFontStyle.Regular;
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
}
