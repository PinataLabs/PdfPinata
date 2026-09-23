#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using System.Globalization;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Security;

namespace PdfPinata.Pdf.Internal;

/// <summary>
/// Groups a set of static encoding helper functions.
/// </summary>
internal static class PdfEncoders
{
    /// <summary>
    /// Gets the raw encoding.
    /// </summary>
    public static Encoding RawEncoding => _rawEncoding ?? (_rawEncoding = new RawEncoding());

    private static Encoding _rawEncoding;

    /// <summary>
    /// Gets the raw Unicode encoding.
    /// </summary>
    public static Encoding RawUnicodeEncoding => _rawUnicodeEncoding ?? (_rawUnicodeEncoding = new RawUnicodeEncoding());

    private static Encoding _rawUnicodeEncoding;

    /// <summary>
    /// Gets the Windows 1252 (ANSI) encoding.
    /// </summary>
    public static Encoding WinAnsiEncoding
    {
        get
        {
            _winAnsiEncoding ??= new AnsiEncoding();
            return _winAnsiEncoding;
        }
    }
    private static Encoding _winAnsiEncoding;

    /// <summary>
    /// Gets the PDF DocEncoding encoding.
    /// </summary>
    public static Encoding DocEncoding => _docEncoding ??= new DocEncoding();

    private static Encoding _docEncoding;

    /// <summary>
    /// Gets the UNICODE little-endian encoding.
    /// </summary>
    public static Encoding UnicodeEncoding => _unicodeEncoding ?? (_unicodeEncoding = Encoding.Unicode);

    private static Encoding _unicodeEncoding;

    /// <summary>
    /// Converts a name, leading slash included, into the form it is written in: the slash, then
    /// every byte of the name, with white space, the ten delimiters, '#' and anything outside
    /// '!'..'~' written as #xx (ISO 32000-1 7.3.5).
    /// </summary>
    /// <remarks>
    /// A name is a byte string held one char per byte, and every name the lexers read has only
    /// chars below 256, which are written back as exactly those bytes. A char of 256 or more can
    /// only have come from a caller writing Unicode, and "#" and its hex digits would be read back
    /// as one byte followed by ordinary characters - so such a name is encoded as UTF-8, which is
    /// what 7.3.5 recommends, and those bytes are written instead.
    /// <para>
    /// An unpaired surrogate has no UTF-8 encoding, and the encoder would silently put U+FFFD in
    /// its place - so two names differing only there would be written as the same name, and a
    /// dictionary could end up with the same key twice. Such a name is refused instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">The name holds an unpaired surrogate.</exception>
    public static string ToNameLiteral(string name)
    {
        Debug.Assert(name.Length > 0 && name[0] == '/');
        var pdf = new StringBuilder("/", name.Length + 8);

        var isByteString = true;
        for (var idx = 1; idx < name.Length; idx++)
        {
            if (name[idx] > 0xFF)
            {
                isByteString = false;
                break;
            }
        }

        if (isByteString)
        {
            for (var idx = 1; idx < name.Length; idx++)
                AppendNameByte(pdf, (byte)name[idx]);
        }
        else
        {
            EnsureNoLoneSurrogate(name);
            foreach (var b in Encoding.UTF8.GetBytes(name.Substring(1)))
                AppendNameByte(pdf, b);
        }
        return pdf.ToString();
    }

    private static void EnsureNoLoneSurrogate(string name)
    {
        for (var idx = 1; idx < name.Length; idx++)
        {
            var ch = name[idx];
            if (!char.IsSurrogate(ch))
                continue;
            if (char.IsHighSurrogate(ch) && idx + 1 < name.Length && char.IsLowSurrogate(name[idx + 1]))
            {
                idx++;
                continue;
            }
            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                "The PDF name '{0}' holds an unpaired surrogate U+{1:X4} at index {2}, which cannot be written as UTF-8.",
                name, (int)ch, idx), nameof(name));
        }
    }

    private static void AppendNameByte(StringBuilder pdf, byte b)
    {
        switch ((char)b)
        {
            case < '!' or > '~':
            case '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%' or '#':
                pdf.Append('#').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                break;

            default:
                pdf.Append((char)b);
                break;
        }
    }

    /// <summary>
    /// Converts a raw string into a raw string literal, possibly encrypted.
    /// </summary>
    public static string ToStringLiteral(string text, PdfStringEncoding encoding, PdfStandardSecurityHandler securityHandler)
    {
        if (String.IsNullOrEmpty(text))
            return "()";

        byte[] bytes;
        switch (encoding)
        {
            case PdfStringEncoding.RawEncoding:
                bytes = RawEncoding.GetBytes(text);
                break;

            case PdfStringEncoding.WinAnsiEncoding:
                bytes = WinAnsiEncoding.GetBytes(text);
                break;

            case PdfStringEncoding.PDFDocEncoding:
                bytes = DocEncoding.GetBytes(text);
                break;

            case PdfStringEncoding.Unicode:
                bytes = RawUnicodeEncoding.GetBytes(text);
                break;

            default:
                throw new NotImplementedException(encoding.ToString());
        }
        var temp = FormatStringLiteral(bytes, encoding == PdfStringEncoding.Unicode, true, false, securityHandler);
        return RawEncoding.GetString(temp, 0, temp.Length);
    }

    /// <summary>
    /// Converts a raw string into a raw string literal, possibly encrypted.
    /// </summary>
    public static string ToStringLiteral(byte[] bytes, bool unicode, PdfStandardSecurityHandler securityHandler)
    {
        if (bytes == null || bytes.Length == 0)
            return "()";

        var temp = FormatStringLiteral(bytes, unicode, true, false, securityHandler);
        return RawEncoding.GetString(temp, 0, temp.Length);
    }

    /// <summary>
    /// Converts a raw string into a raw hexadecimal string literal, possibly encrypted.
    /// </summary>
    public static string ToHexStringLiteral(string text, PdfStringEncoding encoding, PdfStandardSecurityHandler securityHandler)
    {
        if (String.IsNullOrEmpty(text))
            return "<>";

        byte[] bytes;
        switch (encoding)
        {
            case PdfStringEncoding.RawEncoding:
                bytes = RawEncoding.GetBytes(text);
                break;

            case PdfStringEncoding.WinAnsiEncoding:
                bytes = WinAnsiEncoding.GetBytes(text);
                break;

            case PdfStringEncoding.PDFDocEncoding:
                bytes = DocEncoding.GetBytes(text);
                break;

            case PdfStringEncoding.Unicode:
                bytes = RawUnicodeEncoding.GetBytes(text);
                break;

            default:
                throw new NotImplementedException(encoding.ToString());
        }

        var agTemp = FormatStringLiteral(bytes, encoding == PdfStringEncoding.Unicode, true, true, securityHandler);
        return RawEncoding.GetString(agTemp, 0, agTemp.Length);
    }

    /// <summary>
    /// Converts a raw string into a raw hexadecimal string literal, possibly encrypted.
    /// </summary>
    public static string ToHexStringLiteral(byte[] bytes, bool unicode, PdfStandardSecurityHandler securityHandler)
    {
        if (bytes == null || bytes.Length == 0)
            return "<>";

        var agTemp = FormatStringLiteral(bytes, unicode, true, true, securityHandler);
        return RawEncoding.GetString(agTemp, 0, agTemp.Length);
    }

    /// <summary>
    /// Converts the specified byte array into a byte array representing a string literal.
    /// </summary>
    /// <param name="bytes">The bytes of the string.</param>
    /// <param name="unicode">Indicates whether one or two bytes are one character.</param>
    /// <param name="prefix">Indicates whether to use Unicode prefix.</param>
    /// <param name="hex">Indicates whether to create a hexadecimal string literal.</param>
    /// <param name="securityHandler">Encrypts the bytes if specified.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] FormatStringLiteral(byte[] bytes, bool unicode, bool prefix, bool hex, PdfStandardSecurityHandler securityHandler)
    {
        if (bytes == null || bytes.Length == 0)
            return hex ? "<>"u8.ToArray() : "()"u8.ToArray();

        Debug.Assert(!unicode || bytes.Length % 2 == 0, "Odd number of bytes in Unicode string.");

        // The byte order mark belongs to the value of the string, not to its syntax, so it has to
        // be part of what gets encrypted. Written outside the ciphertext it would be decrypted as
        // if it were text, which shifts everything after it and leaves the whole string unreadable
        // to every reader but this one. Putting it into the bytes here writes the same characters
        // as before when nothing is encrypted.
        var byteOrderMarkLength = 0;
        if (unicode && prefix)
        {
            var withByteOrderMark = new byte[bytes.Length + 2];
            withByteOrderMark[0] = 0xFE;
            withByteOrderMark[1] = 0xFF;
            Array.Copy(bytes, 0, withByteOrderMark, 2, bytes.Length);
            bytes = withByteOrderMark;
            byteOrderMarkLength = 2;
        }

        if (securityHandler != null)
        {
            bytes = (byte[])bytes.Clone();
            bytes = securityHandler.EncryptBytes(bytes);
        }

        var count = bytes.Length;
        var pdf = new StringBuilder();
        if (!unicode)
        {
            if (!hex)
            {
                pdf.Append('(');
                for (var idx = 0; idx < count; idx++)
                {
                    var ch = (char)bytes[idx];
                    if (ch < 32)
                    {
                        switch (ch)
                        {
                            case '\n':
                                pdf.Append("\\n");
                                break;

                            case '\r':
                                pdf.Append("\\r");
                                break;

                            case '\t':
                                pdf.Append("\\t");
                                break;

                            case '\b':
                                pdf.Append("\\b");
                                break;

                            // A form feed is deliberately not escaped as \f: escaping it corrupted encrypted text.

                            default:
                                // Any other byte below 32 is written as it is, encrypted or not:
                                // a literal string may hold any byte but the ones escaped here.
                                pdf.Append(ch);
                                break;
                        }
                    }
                    else
                    {
                        switch (ch)
                        {
                            case '(':
                                pdf.Append("\\(");
                                break;

                            case ')':
                                pdf.Append("\\)");
                                break;

                            case '\\':
                                pdf.Append("\\\\");
                                break;

                            default:
                                pdf.Append(ch);
                                break;
                        }
                    }
                }
                pdf.Append(')');
            }
            else
            {
                pdf.Append('<');
                for (var idx = 0; idx < count; idx++)
                    pdf.AppendFormat("{0:X2}", bytes[idx]);
                pdf.Append('>');
            }
        }
        else
        {
            // Unicode is always written in hex, whatever was asked for. A byte order mark that was
            // asked for is already the first two bytes, put there above so that it is encrypted
            // with the rest.
            pdf.Append('<');
            for (var idx = 0; idx < count; idx += 2)
            {
                pdf.AppendFormat("{0:X2}{1:X2}", bytes[idx], bytes[idx + 1]);
                // The mark is part of the bytes now, so count from the text that follows it
                // and the lines break where they always did.
                var positionInText = idx - byteOrderMarkLength;
                if (positionInText != 0 && (positionInText % 48) == 0)
                    pdf.Append('\n');
            }
            pdf.Append('>');
        }
        return RawEncoding.GetBytes(pdf.ToString());
    }

    /// <summary>
    /// ...because I always forget CultureInfo.InvariantCulture and wonder why Acrobat
    /// cannot understand my German decimal separator...
    /// </summary>
    public static string Format(string format, params object[] args)
    {
        return String.Format(CultureInfo.InvariantCulture, format, args);
    }

    /// <summary>
    /// Converts a float into a string with up to 3 decimal digits and a decimal point.
    /// </summary>
    public static string ToString(double val)
    {
        return val.ToString(Config.SignificantFigures3, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converts an XColor into a string with up to 3 decimal digits and a decimal point.
    /// </summary>
    public static string ToString(XColor color, PdfColorMode colorMode, bool withAlpha = false)
    {
        const string format = Config.SignificantFigures3;

        // If not defined let color decide
        if (colorMode == PdfColorMode.Undefined)
            colorMode = color.ColorSpace == XColorSpace.Cmyk ? PdfColorMode.Cmyk : PdfColorMode.Rgb;

        switch (colorMode)
        {
            case PdfColorMode.Cmyk:
                return String.Format(CultureInfo.InvariantCulture, "{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "}",
                    color.C, color.M, color.Y, color.K);

            default:
            {
                if (withAlpha)
                    return String.Format(CultureInfo.InvariantCulture, "{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "}", color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A);
                else
                    return String.Format(CultureInfo.InvariantCulture, "{0:" + format + "} {1:" + format + "} {2:" + format + "}", color.R / 255.0, color.G / 255.0, color.B / 255.0);

            }
        }
    }

    /// <summary>
    /// Converts an XMatrix into a string with up to 4 decimal digits and a decimal point.
    /// </summary>
    public static string ToString(XMatrix matrix)
    {
        const string format = Config.SignificantFigures4;
        return String.Format(CultureInfo.InvariantCulture,
            "{0:" + format + "} {1:" + format + "} {2:" + format + "} {3:" + format + "} {4:" + format + "} {5:" + format + "}",
            matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.OffsetX, matrix.OffsetY);
    }
}
