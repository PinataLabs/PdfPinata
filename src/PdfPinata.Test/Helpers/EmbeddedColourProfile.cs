using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Puts an ICC colour profile into an existing PNG or JPEG file, so that a test can decode the same
///   pixels with and without one.
/// </summary>
/// <remarks>
///   The profile is built here, byte by byte, rather than checked in. It is a small matrix/TRC
///   profile that is far from sRGB: the D50-adapted colorants of Adobe RGB (1998), a wider gamut,
///   with a straight line for every tone curve. A decoder that applied it would desaturate every
///   colour and brighten every mid-tone, so a test could not miss it. The colorants are what matter
///   to ImageSharp 4, which takes any profile with sRGB's colorants for sRGB whatever its tone
///   curves, and skips the conversion. The white point is the D50 one the core's
///   <c>sRGB-v2-micro.icc</c> carries.
/// </remarks>
internal static class EmbeddedColourProfile
{
    /// <summary>An ICC v2.1 display profile for linear-light, wide-gamut RGB.</summary>
    public static byte[] WideGamutLinearRgb()
    {
        var curve = Tag("curv", U32(1), U16(0x0100), U16(0)); // one entry: gamma 1.0
        var tags = new (string Signature, byte[] Data)[]
        {
            ("desc", Description("Wide-gamut linear RGB (PdfPinata test)")),
            ("cprt", Tag("text", Encoding.ASCII.GetBytes("CC0\0"))),
            ("wtpt", Xyz(0xF354, 0x10000, 0x116C9)),
            ("rXYZ", Xyz(0x9C15, 0x4FA4, 0x04FE)),
            ("gXYZ", Xyz(0x348F, 0xA02E, 0x0F97)),
            ("bXYZ", Xyz(0x2632, 0x102E, 0xBE9E)),
            ("rTRC", curve),
            ("gTRC", curve),
            ("bTRC", curve)
        };

        var tableLength = 4 + tags.Length * 12;
        var offsets = new int[tags.Length];
        var position = 128 + tableLength;
        for (var i = 0; i < tags.Length; i++)
        {
            offsets[i] = position;
            position += Align4(tags[i].Data.Length);
        }

        var profile = new byte[position];

        // The header: size, version 2.1, a display profile, RGB data, XYZ connection space, the
        // signature, and the D50 illuminant. Everything else in it may be zero.
        Put(profile, 0, U32((uint)position));
        Put(profile, 8, U32(0x02100000));
        Put(profile, 12, Encoding.ASCII.GetBytes("mntrRGB XYZ "));
        Put(profile, 24, U16(2026), U16(1), U16(1));
        Put(profile, 36, Encoding.ASCII.GetBytes("acsp"));
        Put(profile, 68, U32(0xF6D6), U32(0x10000), U32(0xD32D));

        Put(profile, 128, U32((uint)tags.Length));
        for (var i = 0; i < tags.Length; i++)
        {
            Put(profile, 132 + i * 12, Encoding.ASCII.GetBytes(tags[i].Signature),
                U32((uint)offsets[i]), U32((uint)tags[i].Data.Length));
            Put(profile, offsets[i], tags[i].Data);
        }

        return profile;
    }

    /// <summary>A PNG with an <c>iCCP</c> chunk holding <paramref name="profile"/>, just after its header.</summary>
    public static byte[] IntoPng(byte[] png, byte[] profile)
    {
        const int signatureLength = 8;
        const int headerChunkLength = 4 + 4 + 13 + 4;
        if (Encoding.ASCII.GetString(png, signatureLength + 4, 4) != "IHDR")
            throw new ArgumentException(@"Not a PNG.", nameof(png));

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(profile);

        // A profile name, its terminator, and compression method 0 (deflate).
        var data = Concat(Encoding.ASCII.GetBytes("Wide-gamut linear RGB"), [0, 0], compressed.ToArray());
        var typeAndData = Concat(Encoding.ASCII.GetBytes("iCCP"), data);
        var chunk = Concat(U32((uint)data.Length), typeAndData, U32(Crc32(typeAndData)));

        var split = signatureLength + headerChunkLength;
        return Concat(png[..split], chunk, png[split..]);
    }

    /// <summary>
    ///   A JPEG with an <c>APP2 ICC_PROFILE</c> segment holding <paramref name="profile"/>, after its
    ///   start-of-image marker and any <c>APP0</c> segment.
    /// </summary>
    public static byte[] IntoJpeg(byte[] jpeg, byte[] profile)
    {
        if (jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new ArgumentException(@"Not a JPEG.", nameof(jpeg));

        var split = 2;
        if (jpeg[2] == 0xFF && jpeg[3] == 0xE0)
            split += 2 + ((jpeg[4] << 8) | jpeg[5]);

        // The identifier, then this segment's sequence number and the segment count: one of one.
        var payload = Concat(Encoding.ASCII.GetBytes("ICC_PROFILE\0"), [1, 1], profile);
        var segment = Concat([0xFF, 0xE2], U16((ushort)(payload.Length + 2)), payload);

        return Concat(jpeg[..split], segment, jpeg[split..]);
    }

    private static byte[] Description(string text)
    {
        // textDescriptionType: the ASCII description, then empty Unicode and ScriptCode ones.
        var ascii = Encoding.ASCII.GetBytes(text + "\0");
        return Tag("desc", U32((uint)ascii.Length), ascii, U32(0), U32(0), U16(0), [0], new byte[67]);
    }

    private static byte[] Xyz(uint x, uint y, uint z) => Tag("XYZ ", U32(x), U32(y), U32(z));

    /// <summary>A tag's type signature, its four reserved bytes, and its data.</summary>
    private static byte[] Tag(string type, params byte[][] parts) =>
        Concat([Encoding.ASCII.GetBytes(type), new byte[4], .. parts]);

    private static int Align4(int length) => (length + 3) & ~3;

    private static void Put(byte[] target, int offset, params byte[][] parts)
    {
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, target, offset, part.Length);
            offset += part.Length;
        }
    }

    private static byte[] Concat(params byte[][] parts)
    {
        using var all = new MemoryStream();
        foreach (var part in parts)
            all.Write(part);
        return all.ToArray();
    }

    private static byte[] U16(ushort value) => [(byte)(value >> 8), (byte)value];

    private static byte[] U32(uint value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    /// <summary>The CRC every PNG chunk ends with: ISO 3309, over the chunk's type and data.</summary>
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return ~crc;
    }
}
