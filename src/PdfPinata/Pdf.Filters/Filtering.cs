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
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Applies standard filters to streams.
/// </summary>
public static class Filtering
{
    /// <summary>
    /// Gets the filter specified by the case sensitive name.
    /// </summary>
    public static Filter GetFilter(string filterName)
    {
        ArgumentNullException.ThrowIfNull(filterName);

        if (filterName.StartsWith('/'))
            filterName = filterName[1..];

        // Some tools use abbreviations
        switch (filterName)
        {
            case "ASCIIHexDecode":
            case "AHx":
                return _asciiHexDecode ??= new AsciiHexDecode();

            case "ASCII85Decode":
            case "A85":
                return _ascii85Decode ??= new Ascii85Decode();

            case "LZWDecode":
            case "LZW":
                return _lzwDecode ??= new LzwDecode();

            case "FlateDecode":
            case "Fl":
                return _flateDecode ??= new FlateDecode();

            case "RunLengthDecode":
            case "RL":
                return _runLengthDecode ??= new RunLengthDecode();

            case "CCITTFaxDecode":
            case "JBIG2Decode":
            case "DCTDecode":
            case "JPXDecode":
            case "Crypt":
                Debug.WriteLine("Filter not implemented: " + filterName);
                return null;
        }
        throw new NotImplementedException("Unknown filter: " + filterName);
    }

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    // ReSharper disable InconsistentNaming
    public static AsciiHexDecode ASCIIHexDecode => _asciiHexDecode ??= new AsciiHexDecode(); // ReSharper restore InconsistentNaming

    private static AsciiHexDecode _asciiHexDecode;

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static Ascii85Decode ASCII85Decode => _ascii85Decode ??= new Ascii85Decode();

    private static Ascii85Decode _ascii85Decode;

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static LzwDecode LzwDecode => _lzwDecode ??= new LzwDecode();

    private static LzwDecode _lzwDecode;

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static FlateDecode FlateDecode => _flateDecode ??= new FlateDecode();

    private static FlateDecode _flateDecode;

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static RunLengthDecode RunLengthDecode => _runLengthDecode ??= new RunLengthDecode();

    private static RunLengthDecode _runLengthDecode;

    /// <summary>
    /// Encodes the data with the specified filter.
    /// </summary>
    public static byte[] Encode(byte[] data, string filterName)
    {
        var filter = GetFilter(filterName);
        return filter?.Encode(data);
    }

    /// <summary>
    /// Encodes a raw string with the specified filter.
    /// </summary>
    public static byte[] Encode(string rawString, string filterName)
    {
        var filter = GetFilter(filterName);
        return filter?.Encode(rawString);
    }

    /// <summary>
    /// Decodes the data with the specified filter.
    /// </summary>
    public static byte[] Decode(byte[] data, string filterName, FilterParms parms)
    {
        var filter = GetFilter(filterName);
        return filter?.Decode(data, parms);
    }

    /// <summary>
    /// Decodes the data with the specified filter.
    /// </summary>
    public static byte[] Decode(byte[] data, string filterName)
    {
        var filter = GetFilter(filterName);
        return filter?.Decode(data, (PdfDictionary)null);
    }

    /// <summary>
    /// Decodes the data with the specified filter.
    /// </summary>
    public static byte[] Decode(byte[] data, PdfItem filterItem, PdfItem decodeParms)
    {
        // Either entry, and any element of either, may be an indirect object, and a filter with
        // default parameters has null in its place in a parameter array (ISO 32000-1 Table 5).
        filterItem = Direct(filterItem);
        decodeParms = Direct(decodeParms);

        byte[] result = null;
        if (filterItem is PdfName && (decodeParms == null || decodeParms is PdfDictionary))
        {
            var filter = GetFilter(filterItem.ToString());
            if (filter != null)
                result = filter.Decode(data, decodeParms as PdfDictionary);
        }
        else if (filterItem is PdfArray itemArray && (decodeParms == null || decodeParms is PdfArray))
        {
            var decodeArray = decodeParms as PdfArray;
            // array length of filter and decode parms should match. if they dont, return data unmodified
            if (decodeArray != null && decodeArray.Elements.Count != itemArray.Elements.Count)
                return data;
            for (var i = 0; i < itemArray.Elements.Count; i++)
            {
                var item = itemArray.Elements[i];
                var parms = decodeArray != null ? decodeArray.Elements[i] : null;
                data = Decode(data, item, parms);
            }
            result = data;
        }
        return result;
    }

    /// <summary>
    /// The object a reference points at, or the item itself; and null for a PDF null.
    /// </summary>
    private static PdfItem Direct(PdfItem item)
    {
        if (item is PdfReference reference)
            item = reference.Value;
        return item is PdfNull or PdfNullObject ? null : item;
    }

    /// <summary>
    /// Decodes to a raw string with the specified filter.
    /// </summary>
    public static string DecodeToString(byte[] data, string filterName, FilterParms parms)
    {
        var filter = GetFilter(filterName);
        return filter?.DecodeToString(data, parms);
    }

    /// <summary>
    /// Decodes to a raw string with the specified filter.
    /// </summary>
    public static string DecodeToString(byte[] data, string filterName)
    {
        var filter = GetFilter(filterName);
        return filter?.DecodeToString(data, null);
    }
}
