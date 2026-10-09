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

        filterName = PdfName.WithoutSolidus(filterName);

        // Some tools use abbreviations
        return filterName switch
        {
            "ASCIIHexDecode" or "AHx" => ASCIIHexDecode,
            "ASCII85Decode" or "A85" => ASCII85Decode,
            "LZWDecode" or "LZW" => LzwDecode,
            "FlateDecode" or "Fl" => FlateDecode,
            "RunLengthDecode" or "RL" => RunLengthDecode,
            _ when IsRecognisedButNotImplemented(filterName) => NotImplementedFilter(filterName),
            _ => throw new NotImplementedException("Unknown filter: " + filterName)
        };
    }

    /// <summary>
    /// Whether the name is one of the standard filters this library knows of but cannot apply.
    /// </summary>
    private static bool IsRecognisedButNotImplemented(string filterName) =>
        filterName is "CCITTFaxDecode" or "JBIG2Decode" or "DCTDecode" or "JPXDecode" or "Crypt";

    /// <summary>
    /// What a recognised but unimplemented filter is looked up as: nothing, with a note in the
    /// debug output.
    /// </summary>
    private static Filter NotImplementedFilter(string filterName)
    {
        Debug.WriteLine("Filter not implemented: " + filterName);
        return null;
    }

    // The singletons are made by the type initializer, which the runtime runs once, rather than on
    // first use with ??=, which two threads asking at once could each run - handing out two
    // instances of what is promised to be one.

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    // ReSharper disable InconsistentNaming
    public static AsciiHexDecode ASCIIHexDecode { get; } = new(); // ReSharper restore InconsistentNaming

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static Ascii85Decode ASCII85Decode { get; } = new();

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static LzwDecode LzwDecode { get; } = new();

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static FlateDecode FlateDecode { get; } = new();

    /// <summary>
    /// Gets the filter singleton.
    /// </summary>
    public static RunLengthDecode RunLengthDecode { get; } = new();

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
    /// Decodes the data with the filter or chain of filters a stream's <c>/Filter</c> entry names,
    /// given its <c>/DecodeParms</c> entry; or null when a filter is not one this library applies,
    /// or the parameters cannot be told apart by filter.
    /// </summary>
    public static byte[] Decode(byte[] data, PdfItem filterItem, PdfItem decodeParms)
    {
        // Either entry, and any element of either, may be an indirect object, and a filter with
        // default parameters has null in its place in a parameter array (ISO 32000-1 Table 5).
        filterItem = Direct(filterItem);
        decodeParms = Direct(decodeParms);

        if (filterItem is PdfName)
        {
            return ParametersForEach(1, decodeParms) is [var parms and (null or PdfDictionary)]
                ? GetFilter(filterItem.ToString())?.Decode(data, parms as PdfDictionary)
                : null;
        }

        if (filterItem is PdfArray filters)
        {
            return ParametersForEach(filters.Elements.Count, decodeParms) is { } each
                ? DecodeWithEach(data, filters, each)
                : null;
        }

        return null;
    }

    /// <summary>
    /// The parameters of each of <paramref name="count"/> filters in turn, null for one taking its
    /// defaults; or null when which parameters belong to which filter cannot be told.
    /// </summary>
    /// <remarks>
    /// ISO 32000-1 Table 5 asks for the shape of <c>/Filter</c> - a dictionary for a single filter,
    /// an array as long as the chain for a chain. A file in another shape is read only where there
    /// is still one way to read it: a lone filter's parameters wrapped in an array of one, a chain
    /// of one given its dictionary bare, and parameters of any count that are all null. Anything
    /// else would be a guess about which filter a dictionary was meant for, and a wrong guess
    /// decodes to the wrong bytes with nothing to say so.
    /// </remarks>
    private static PdfItem[] ParametersForEach(int count, PdfItem decodeParms)
    {
        if (decodeParms is null)
            return new PdfItem[count];

        if (decodeParms is not PdfArray array)
            return count == 1 ? [decodeParms] : null;

        var each = new PdfItem[array.Elements.Count];
        for (var idx = 0; idx < each.Length; idx++)
            each[idx] = Direct(array.Elements[idx]);

        if (each.Length == count)
            return each;

        return Array.TrueForAll(each, parms => parms is null) ? new PdfItem[count] : null;
    }

    /// <summary>
    /// Decodes the data with each filter of an array in turn, each with its own parameters.
    /// </summary>
    private static byte[] DecodeWithEach(byte[] data, PdfArray filters, PdfItem[] parameters)
    {
        for (var idx = 0; idx < filters.Elements.Count; idx++)
            data = Decode(data, filters.Elements[idx], parameters[idx]);
        return data;
    }

    /// <summary>
    /// The object a reference points at, or the item itself; and null for a PDF null.
    /// </summary>
    private static PdfItem Direct(PdfItem item)
    {
        item = PdfReference.Dereference(item);
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
