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
using System.Globalization;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;

namespace PdfPinata.Drawing;

/// <summary>
/// The bytes of a font file.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
internal class XFontSource
{
    // Implementation Notes
    //
    // * XFontSource represents a single font (file) in memory.
    // * An XFontSource hold a reference to it OpenTypeFontface.
    // * To prevent large heap fragmentation this class must exists only once.
    // * A font collection never reaches here: PdfPinata.Utils.TrueTypeCollection extracts
    //   the requested face in the font resolver, so what arrives is always a single font.

    private XFontSource(byte[] bytes, ulong key)
    {
        _fontName = null;
        _bytes = bytes;
        _key = key;
    }

    /// <summary>
    /// Gets an existing font source or creates a new one.
    /// A new font source is cached in font factory.
    /// </summary>
    public static XFontSource GetOrCreateFrom(byte[] bytes)
    {
        var key = FontHelper.CalcChecksum(bytes);
        if (!FontFactory.TryGetFontSourceByKey(key, out var fontSource))
        {
            fontSource = new XFontSource(bytes, key);
            // Theoretically the font source could be created by a differend thread in the meantime.
            fontSource = FontFactory.CacheFontSource(fontSource);
        }
        return fontSource;
    }
    public static XFontSource GetOrCreateFrom(string typefaceKey, byte[] fontBytes)
    {
        var key = FontHelper.CalcChecksum(fontBytes);
        if (FontFactory.TryGetFontSourceByKey(key, out var fontSource))
        {
            // The font source already exists, but is not yet cached under the specified typeface key.
            FontFactory.CacheExistingFontSourceWithNewTypefaceKey(typefaceKey, fontSource);
        }
        else
        {
            // No font source exists. Create new one and cache it.
            fontSource = new XFontSource(fontBytes, key);
            FontFactory.CacheNewFontSource(typefaceKey, fontSource);
        }
        return fontSource;
    }

    public static XFontSource CreateCompiledFont(byte[] bytes)
    {
        var fontSource = new XFontSource(bytes, 0);
        return fontSource;
    }

    /// <summary>
    /// Gets or sets the fontface.
    /// </summary>
    internal OpenTypeFontface Fontface
    {
        get => _fontface;
        set
        {
            _fontface = value;
            _fontName = value.name.FullFontName;
        }
    }

    private OpenTypeFontface _fontface;

    /// <summary>
    /// Gets the key that uniquely identifies this font source.
    /// </summary>
    internal ulong Key
    {
        get
        {
            if (_key == 0)
                _key = FontHelper.CalcChecksum(Bytes);
            return _key;
        }
    }

    private ulong _key;

    public void IncrementKey()
    {
        // HACK: Depends on implementation of CalcChecksum.
        // Increment check sum and keep length untouched.
        _key += 1ul << 32;
    }

    /// <summary>
    /// Gets the name of the font's name table.
    /// </summary>
    public string FontName => _fontName;

    private string _fontName;

    /// <summary>
    /// Gets the bytes of the font.
    /// </summary>
    public byte[] Bytes => _bytes;

    private readonly byte[] _bytes;

    public override int GetHashCode()
    {
        return (int)((Key >> 32) ^ Key);
    }

    public override bool Equals(object obj)
    {
        var fontSource = obj as XFontSource;
        if (fontSource == null)
            return false;
        return Key == fontSource.Key;
    }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSha rper disable UnusedMember.Local
    // The key is converted to a value a human can remember during debugging.
    internal string DebuggerDisplay => String.Format(CultureInfo.InvariantCulture, "XFontSource: '{0}', keyhash={1}", FontName, Key % 99991 /* largest prime number less than 100000 */); // ReShar per restore UnusedMember.Local
}
