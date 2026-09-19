
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
        var baseOffset = 0;

        // A TrueType collection starts with a directory of fonts. TrueTypeCollection owns both
        // the signature check and the validation of the declared face count against the room the
        // file has to point at that many, so neither is repeated here.
        if (TrueTypeCollection.IsCollection(data))
        {
            var faceCount = TrueTypeCollection.FaceCount(data);
            var face = faceIndex < 0 ? 0 : faceIndex;

            if (face >= faceCount)
                throw new InvalidOperationException(
                    "Font collection holds " + faceCount + " faces; face " + face + " was asked for.");

            baseOffset = (int)U32(data, OffsetTableLength + face * 4);
        }
        else if (faceIndex > 0)
        {
            throw new InvalidOperationException(
                "Font is not a collection and holds face 0 alone; face " + faceIndex + " was asked for.");
        }

        // A collection is free to point at a face outside the file, and the offset table holds
        // the table count, so both are checked before anything is read through them.
        if (baseOffset < 0 || baseOffset + OffsetTableLength > data.Length)
            throw new InvalidOperationException("Font collection points at a face outside the file.");

        var numTables = U16(data, baseOffset + 4);

        if (baseOffset + OffsetTableLength + numTables * TableRecordLength > data.Length)
            throw new InvalidOperationException("Font declares more tables than the file holds.");

        var nameOffset = -1;
        var os2Offset = -1;
        var headOffset = -1;

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

        if (nameOffset < 0)
            throw new InvalidOperationException("Font contains no 'name' table.");

        return new FontMetadata(ReadFamilyName(data, nameOffset), ReadStyle(data, os2Offset, headOffset));
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

            if (U16(data, record + 6) != NameIdFamily)
                continue;

            var platformId = U16(data, record);
            var languageId = U16(data, record + 4);
            var length = U16(data, record + 8);
            var offset = stringBase + U16(data, record + 10);

            if (offset < 0 || offset + length > data.Length)
                continue;

            var score = ScoreName(platformId, languageId);
            if (score <= bestScore)
                continue;

            var value = Decode(data, offset, length, platformId);
            if (string.IsNullOrEmpty(value))
                continue;

            best = value;
            bestScore = score;
        }

        if (best == null)
            throw new InvalidOperationException("Font contains no family name (name ID 1).");

        return best;
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
        if (platformId == PlatformUnicode)
            return 1;

        return 0;
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
        if (italic)
            return XFontStyle.Italic;

        return XFontStyle.Regular;
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
