#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Diagnostics;
using System.Text;
using PdfPinata.Pdf.IO;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf;

/// <summary>
/// Determines the encoding of a PdfString or PdfStringObject.
/// </summary>
[Flags]
public enum PdfStringEncoding
{
    /// <summary>
    /// The characters of the string are actually bytes with an unknown or context specific meaning or encoding.
    /// With this encoding the 8 high bits of each character is zero.
    /// </summary>
    RawEncoding = PdfStringFlags.RawEncoding,

    /// <summary>
    /// Not yet used by PdfPinata.
    /// </summary>
    StandardEncoding = PdfStringFlags.StandardEncoding,

    /// <summary>
    /// The characters of the string are actually bytes with PDF document encoding.
    /// With this encoding the 8 high bits of each character is zero.
    /// </summary>
    // ReSharper disable InconsistentNaming because the name is spelled as in the Adobe reference.
    PDFDocEncoding = PdfStringFlags.PDFDocEncoding,
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// The characters of the string are actually bytes with Windows ANSI encoding.
    /// With this encoding the 8 high bits of each character is zero.
    /// </summary>
    WinAnsiEncoding = PdfStringFlags.WinAnsiEncoding,

    /// <summary>
    /// Not yet used by PdfPinata.
    /// </summary>
    /// <remarks>
    /// 4. It was 5, MacExpert's value, from PDFsharp 1.5 until PdfPinata 0.1; an assembly
    /// compiled against those has 5 baked in, and now asks for MacExpert - which PdfPinata
    /// treats exactly as it treats MacRoman, so nothing it writes changes.
    /// </remarks>
    MacRomanEncoding = PdfStringFlags.MacRomanEncoding,

    /// <summary>
    /// Not yet used by PdfPinata.
    /// </summary>
    MacExpertEncoding = PdfStringFlags.MacExpertEncoding,

    /// <summary>
    /// The characters of the string are Unicode characters.
    /// </summary>
    Unicode = PdfStringFlags.Unicode
}

/// <summary>
/// Internal wrapper for PdfStringEncoding.
/// </summary>
[Flags]
enum PdfStringFlags
{
    // ReSharper disable InconsistentNaming
    RawEncoding = 0x00,
    StandardEncoding = 0x01,
    PDFDocEncoding = 0x02,
    WinAnsiEncoding = 0x03,
    MacRomanEncoding = 0x04,
    MacExpertEncoding = 0x05,
    Unicode = 0x06,
    EncodingMask = 0x0F,

    HexLiteral = 0x80
    // ReSharper restore InconsistentNaming
}

/// <summary>
/// Represents a direct text string value.
/// </summary>
[DebuggerDisplay("({Value})")]
public sealed class PdfString : PdfItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfString"/> class.
    /// </summary>
    public PdfString()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfString"/> class with the encoding
    /// the value needs, which is Unicode for anything that ASCII cannot spell.
    /// </summary>
    /// <param name="value">The value.</param>
    public PdfString(string value)
    {
        _value = value;
        _flags = EncodingFor(value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfString"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="encoding">The encoding.</param>
    public PdfString(string value, PdfStringEncoding encoding)
    {
        switch (encoding)
        {
            case PdfStringEncoding.RawEncoding:
                CheckRawEncoding(value);
                break;

            case PdfStringEncoding.StandardEncoding:
                break;

            case PdfStringEncoding.PDFDocEncoding:
                break;

            case PdfStringEncoding.WinAnsiEncoding:
                CheckRawEncoding(value);
                break;

            case PdfStringEncoding.MacRomanEncoding:
                break;

            case PdfStringEncoding.MacExpertEncoding:
                break;

            case PdfStringEncoding.Unicode:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(encoding));
        }
        _value = value;
        _flags = (PdfStringFlags)encoding;
    }

    internal PdfString(string value, PdfStringFlags flags)
    {
        _value = value;
        _flags = flags;
    }

    /// <summary>
    /// Gets the number of characters in this string.
    /// </summary>
    public int Length => _value == null ? 0 : _value.Length;

    /// <summary>
    /// Gets the encoding.
    /// </summary>
    public PdfStringEncoding Encoding => (PdfStringEncoding)(_flags & PdfStringFlags.EncodingMask);

    /// <summary>
    /// Gets a value indicating whether the string is a hexadecimal literal.
    /// </summary>
    public bool HexLiteral => (_flags & PdfStringFlags.HexLiteral) != 0;

    internal PdfStringFlags Flags => _flags;

    readonly PdfStringFlags _flags;

    /// <summary>
    /// Gets the string value.
    /// </summary>
    public string Value =>
        // This class must behave like a value type. Therefore it cannot be changed (like System.String).
        _value ?? "";

    readonly string _value;

    /// <summary>
    /// Gets the string value as the bytes encryption works on.
    /// </summary>
    internal byte[] EncryptionValue => _value == null ? [] : GetBytesFromEncoding();

    /// <summary>
    /// Builds the string those bytes spell, once encryption has run over them.
    /// </summary>
    /// <remarks>
    /// A text string keeps its byte order mark inside the encrypted bytes, so whether it is
    /// UTF-16BE only becomes apparent once it has been decrypted. The flags were decided by
    /// the lexer, which saw the ciphertext and had nothing to go by, so the mark decides
    /// here and the flags of the string returned say what the value now holds. This is a
    /// factory rather than a setter because a PdfString is a simple type and must not change
    /// after it is constructed; the caller replaces the entry that held the old one.
    /// </remarks>
    internal static PdfString FromEncryptionValue(byte[] value, PdfStringFlags flags)
    {
        if (value.Length >= 2 && value[0] == 0xFE && value[1] == 0xFF)
        {
            return new PdfString(
                PdfEncoders.RawUnicodeEncoding.GetString(value, 2, value.Length - 2),
                (flags & ~PdfStringFlags.EncodingMask) | PdfStringFlags.Unicode);
        }

        var encoding = (PdfStringEncoding)(flags & PdfStringFlags.EncodingMask);
        switch (encoding)
        {
            case PdfStringEncoding.Unicode:
                return new PdfString(PdfEncoders.RawUnicodeEncoding.GetString(value), flags);
            default:
                return new PdfString(PdfEncoders.RawEncoding.GetString(value), flags);
        }
    }

    private byte[] GetBytesFromEncoding()
    {
        var encoding = (PdfStringEncoding)(_flags & PdfStringFlags.EncodingMask);
        switch (encoding)
        {
            case PdfStringEncoding.Unicode:
                return PdfEncoders.RawUnicodeEncoding.GetBytes(_value);
            default:
                return PdfEncoders.RawEncoding.GetBytes(_value);
        }
    }

    /// <summary>
    /// Returns the string.
    /// </summary>
    public override string ToString()
    {
        var encoding = (PdfStringEncoding)(_flags & PdfStringFlags.EncodingMask);
        var pdf = (_flags & PdfStringFlags.HexLiteral) == 0 ?
            PdfEncoders.ToStringLiteral(_value, encoding, null) :
            PdfEncoders.ToHexStringLiteral(_value, encoding, null);
        return pdf;
    }

    /// <summary>
    /// Hack for document encoded bookmarks.
    /// </summary>
    public string ToStringFromPdfDocEncoded()
    {
        var length = _value.Length;
        var bytes = new char[length];
        for (var idx = 0; idx < length; idx++)
        {
            var ch = _value[idx];
            if (ch <= 255)
            {
                bytes[idx] = Encode[ch];
            }
            else
            {
                throw new InvalidOperationException("DocEncoded string contains char greater 255.");
            }
        }
        var sb = new StringBuilder(length);
        for (var idx = 0; idx < length; idx++)
            sb.Append(bytes[idx]);
        return sb.ToString();
    }
    static readonly char[] Encode =
    [
        '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07', '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F',
        '\x10', '\x11', '\x12', '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D', '\x1E', '\x1F',
        '\x20', '\x21', '\x22', '\x23', '\x24', '\x25', '\x26', '\x27', '\x28', '\x29', '\x2A', '\x2B', '\x2C', '\x2D', '\x2E', '\x2F',
        '\x30', '\x31', '\x32', '\x33', '\x34', '\x35', '\x36', '\x37', '\x38', '\x39', '\x3A', '\x3B', '\x3C', '\x3D', '\x3E', '\x3F',
        '\x40', '\x41', '\x42', '\x43', '\x44', '\x45', '\x46', '\x47', '\x48', '\x49', '\x4A', '\x4B', '\x4C', '\x4D', '\x4E', '\x4F',
        '\x50', '\x51', '\x52', '\x53', '\x54', '\x55', '\x56', '\x57', '\x58', '\x59', '\x5A', '\x5B', '\x5C', '\x5D', '\x5E', '\x5F',
        '\x60', '\x61', '\x62', '\x63', '\x64', '\x65', '\x66', '\x67', '\x68', '\x69', '\x6A', '\x6B', '\x6C', '\x6D', '\x6E', '\x6F',
        '\x70', '\x71', '\x72', '\x73', '\x74', '\x75', '\x76', '\x77', '\x78', '\x79', '\x7A', '\x7B', '\x7C', '\x7D', '\x7E', '\x7F',
        '\x2022', '\x2020', '\x2021', '\x2026', '\x2014', '\x2013', '\x0192', '\x2044', '\x2039', '\x203A', '\x2212', '\x2030', '\x201E', '\x201C', '\x201D', '\x2018',
        '\x2019', '\x201A', '\x2122', '\xFB01', '\xFB02', '\x0141', '\x0152', '\x0160', '\x0178', '\x017D', '\x0131', '\x0142', '\x0153', '\x0161', '\x017E', '\xFFFD',
        '\x20AC', '\xA1', '\xA2', '\xA3', '\xA4', '\xA5', '\xA6', '\xA7', '\xA8', '\xA9', '\xAA', '\xAB', '\xAC', '\xAD', '\xAE', '\xAF',
        '\xB0', '\xB1', '\xB2', '\xB3', '\xB4', '\xB5', '\xB6', '\xB7', '\xB8', '\xB9', '\xBA', '\xBB', '\xBC', '\xBD', '\xBE', '\xBF',
        '\xC0', '\xC1', '\xC2', '\xC3', '\xC4', '\xC5', '\xC6', '\xC7', '\xC8', '\xC9', '\xCA', '\xCB', '\xCC', '\xCD', '\xCE', '\xCF',
        '\xD0', '\xD1', '\xD2', '\xD3', '\xD4', '\xD5', '\xD6', '\xD7', '\xD8', '\xD9', '\xDA', '\xDB', '\xDC', '\xDD', '\xDE', '\xDF',
        '\xE0', '\xE1', '\xE2', '\xE3', '\xE4', '\xE5', '\xE6', '\xE7', '\xE8', '\xE9', '\xEA', '\xEB', '\xEC', '\xED', '\xEE', '\xEF',
        '\xF0', '\xF1', '\xF2', '\xF3', '\xF4', '\xF5', '\xF6', '\xF7', '\xF8', '\xF9', '\xFA', '\xFB', '\xFC', '\xFD', '\xFE', '\xFF'
    ];

    /// <summary>
    /// Chooses the encoding for a string whose author did not name one.
    /// </summary>
    /// <remarks>
    /// Plain ASCII means the same characters to every reader whether the bytes are taken as
    /// raw, PDFDocEncoded or WinAnsi, so such a string still goes out one byte per character
    /// and a document that was only ever ASCII is written exactly as it was before. Above
    /// ASCII those encodings disagree, and the raw encoding would keep nothing but the low
    /// byte of each character, so the string goes out as UTF-16BE instead and says what it
    /// was given. Callers with bytes rather than text ask for
    /// <see cref="PdfStringEncoding.RawEncoding"/> and are left alone.
    /// </remarks>
    internal static PdfStringFlags EncodingFor(string value)
    {
        if (value == null)
            return PdfStringFlags.RawEncoding;

        for (var idx = 0; idx < value.Length; idx++)
        {
            if (value[idx] > 0x7F)
                return PdfStringFlags.Unicode;
        }
        return PdfStringFlags.RawEncoding;
    }

    static void CheckRawEncoding(string s)
    {
        if (String.IsNullOrEmpty(s))
            return;

        var length = s.Length;
        for (var idx = 0; idx < length; idx++)
        {
            Debug.Assert(s[idx] < 256, "RawString contains invalid character.");
        }
    }

    /// <summary>
    /// Writes the string DocEncoded.
    /// </summary>
    internal override void WriteObject(PdfWriter writer)
    {
        writer.Write(this);
    }
}
