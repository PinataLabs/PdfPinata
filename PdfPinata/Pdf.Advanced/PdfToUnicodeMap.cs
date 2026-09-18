#region PDFsharp - A .NET library for processing PDF
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using PdfPinata.Fonts;
using PdfPinata.Pdf.Filters;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a ToUnicode map for composite font.
/// </summary>
internal sealed class PdfToUnicodeMap : PdfDictionary
{
    public PdfToUnicodeMap(PdfDocument document)
        : base(document)
    { }

    public PdfToUnicodeMap(PdfDocument document, CMapInfo cmapInfo)
        : base(document)
    {
        _cmapInfo = cmapInfo;
    }

    /// <summary>
    /// Gets or sets the CMap info.
    /// </summary>
    public CMapInfo CMapInfo
    {
        get => _cmapInfo;
        set => _cmapInfo = value;
    }
    CMapInfo _cmapInfo;

    /// <summary>
    /// Creates the ToUnicode map from the CMapInfo.
    /// </summary>
    internal override void PrepareForSave()
    {
        base.PrepareForSave();

        // This code comes literally from PDF Reference
        string prefix =
            "/CIDInit /ProcSet findresource begin\n" +
            "12 dict begin\n" +
            "begincmap\n" +
            "/CIDSystemInfo << /Registry (Adobe)/Ordering (UCS)/Supplement 0>> def\n" +
            "/CMapName /Adobe-Identity-UCS def /CMapType 2 def\n";
        string suffix = "endcmap CMapName currentdict /CMap defineresource pop end end";

        var meanings = _cmapInfo.GlyphMeanings();
        int lowIndex = 65536, hiIndex = -1;
        foreach (int index in meanings.Keys)
        {
            lowIndex = Math.Min(lowIndex, index);
            hiIndex = Math.Max(hiIndex, index);
        }

        // A glyph standing for exactly one character can go in a bfrange, which is how every
        // entry was written before there was anything else to write. One standing for several -
        // a ligature, a composed accent - cannot: a bfrange destination is a single code, so
        // those need a bfchar, whose destination is a string of any length.
        var single = meanings.Where(entry => entry.Value.Length == 1).ToList();
        var several = meanings.Where(entry => entry.Value.Length > 1).ToList();

        MemoryStream ms = new MemoryStream();
        StreamWriter wrt = new StreamWriter(ms, Encoding.UTF8);
        wrt.Write(prefix);

        if (hiIndex >= lowIndex)
        {
            wrt.WriteLine("1 begincodespacerange");
            wrt.WriteLine(String.Format("<{0:X4}><{1:X4}>", lowIndex, hiIndex));
            wrt.WriteLine("endcodespacerange");
        }

        // "shall not exceed 100" - ISO 32000-1 section 9.10.3, of both kinds of block. A document
        // of any size has more glyphs than that, so this is not a limit that only exotic files
        // reach; it was simply never applied.
        foreach (var block in Blocks(single))
        {
            wrt.WriteLine(String.Format("{0} beginbfrange", block.Count));
            foreach (var entry in block)
                wrt.WriteLine(String.Format("<{0:X4}><{0:X4}><{1:X4}>", entry.Key, (int)entry.Value[0]));
            wrt.WriteLine("endbfrange");
        }

        foreach (var block in Blocks(several))
        {
            wrt.WriteLine(String.Format("{0} beginbfchar", block.Count));
            foreach (var entry in block)
                wrt.WriteLine(String.Format("<{0:X4}><{1}>", entry.Key, Utf16BigEndian(entry.Value)));
            wrt.WriteLine("endbfchar");
        }

        wrt.Write(suffix);
        wrt.Dispose();

        // Compress like content streams
        byte[] bytes = ms.ToArray();
        ms.Dispose();
        if (Owner.Options.CompressContentStreams)
        {
            Elements.SetName("/Filter", "/FlateDecode");
            bytes = Filtering.FlateDecode.Encode(bytes, _document.Options.FlateEncodeMode);
        }
        //PdfStream stream = CreateStream(bytes);
        else
        {
            Elements.Remove("/Filter");
        }

        if (Stream == null)
            CreateStream(bytes);
        else
        {
            Stream.Value = bytes;
            Elements.SetInteger(PdfStream.Keys.Length, Stream.Length);
        }
    }

    /// <summary>
    /// The entries in blocks of at most a hundred, which is as many as a bfrange or a bfchar is
    /// allowed to hold.
    /// </summary>
    static IEnumerable<List<KeyValuePair<int, string>>> Blocks(List<KeyValuePair<int, string>> entries)
    {
        const int most = 100;
        for (int start = 0; start < entries.Count; start += most)
            yield return entries.GetRange(start, Math.Min(most, entries.Count - start));
    }

    /// <summary>
    /// A destination string as a bfchar wants it: UTF-16 big-endian, in hexadecimal.
    /// </summary>
    /// <remarks>
    /// The characters go in as they stand, surrogate pairs included, because UTF-16BE is what a
    /// <c>/ToUnicode</c> destination is defined to be and a .NET string is UTF-16 already.
    /// </remarks>
    static string Utf16BigEndian(string characters)
    {
        var hex = new StringBuilder(characters.Length * 4);
        foreach (char character in characters)
            hex.AppendFormat("{0:X4}", (int)character);

        return hex.ToString();
    }

    public sealed class Keys : PdfStream.Keys
    {
        // No new keys.
    }
}
