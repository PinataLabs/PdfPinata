#region Copyright
//
// Authors:
//   David Stephensen
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
using System.IO;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Implements the LzwDecode filter.
/// </summary>
public class LzwDecode : Filter
{
    // Reference: 3.3.3  LZWDecode and FlateDecode Filters / Page 71

    /// <summary>
    /// Throws a NotImplementedException because the obsolete LZW encoding is not supported by PdfPinata.
    /// </summary>
    public override byte[] Encode(byte[] data)
    {
        throw new NotImplementedException("PdfPinata does not support LZW encoding.");
    }

    /// <summary>
    /// Decodes the specified data.
    /// </summary>
    public override byte[] Decode(byte[] data, FilterParms parms)
    {
        if (data[0] == 0x00 && data[1] == 0x01)
            throw new Exception("LZW flavour not supported.");

        var outputStream = new MemoryStream();

        InitializeDictionary();

        _data = data;
        _bytePointer = 0;
        _nextData = 0;
        _nextBits = 0;
        DecodeCodes(outputStream);

        if (outputStream.Length < 0)
            return null;

        // No parameters at all is not an error: it is what DecodeToString passes, and it says
        // the same thing as parameters with no predictor in them.
        return parms?.DecodeParms != null
            ? StreamDecoder.Decode(outputStream.ToArray(), parms.DecodeParms)
            : outputStream.ToArray();
    }

    /// <summary>
    /// Reads codes up to the end-of-data code, writing the string each one stands for.
    /// </summary>
    private void DecodeCodes(MemoryStream outputStream)
    {
        int code, oldCode = 0;
        while ((code = NextCode) != 257)
        {
            if (code == 256)
            {
                InitializeDictionary();
                code = NextCode;
                if (code == 257)
                    break;

                outputStream.Write(_stringTable[code], 0, _stringTable[code].Length);
            }
            else
            {
                WriteString(outputStream, code, oldCode);
            }
            oldCode = code;
        }
    }

    /// <summary>
    /// Writes the string a code stands for, and adds the table entry that code completes.
    /// </summary>
    private void WriteString(Stream outputStream, int code, int oldCode)
    {
        byte[] str;
        if (code < _tableIndex)
        {
            str = _stringTable[code];
            outputStream.Write(str, 0, str.Length);
            AddEntry(_stringTable[oldCode], str[0]);
            return;
        }

        // The encoder is allowed to emit the code for the entry it is in the middle of
        // defining, which it does whenever the input repeats a run. That entry is the
        // previous string followed by that string's own first byte, so writing the
        // previous string alone drops the repeated byte.
        var previous = _stringTable[oldCode];
        str = AddEntry(previous, previous[0]);
        outputStream.Write(str, 0, str.Length);
    }

    /// <summary>
    /// Initialize the dictionary.
    /// </summary>
    private void InitializeDictionary()
    {
        _stringTable = new byte[8192][];

        for (var i = 0; i < 256; i++)
        {
            _stringTable[i] = new byte[1];
            _stringTable[i][0] = (byte)i;
        }

        _tableIndex = 258;
        _bitsToGet = 9;
    }

    /// <summary>
    /// Add a new entry to the Dictionary and return it.
    /// </summary>
    private byte[] AddEntry(byte[] oldstring, byte newstring)
    {
        var length = oldstring.Length;
        var str = new byte[length + 1];
        Array.Copy(oldstring, 0, str, 0, length);
        str[length] = newstring;

        _stringTable[_tableIndex++] = str;

        if (_tableIndex == 511)
            _bitsToGet = 10;
        else if (_tableIndex == 1023)
            _bitsToGet = 11;
        else if (_tableIndex == 2047)
            _bitsToGet = 12;

        return str;
    }

    /// <summary>
    /// Returns the next set of bits.
    /// </summary>
    private int NextCode
    {
        get
        {
            try
            {
                _nextData = (_nextData << 8) | (_data[_bytePointer++] & 0xff);
                _nextBits += 8;

                if (_nextBits < _bitsToGet)
                {
                    _nextData = (_nextData << 8) | (_data[_bytePointer++] & 0xff);
                    _nextBits += 8;
                }

                var code = (_nextData >> (_nextBits - _bitsToGet)) & _andTable[_bitsToGet - 9];
                _nextBits -= _bitsToGet;

                return code;
            }
            catch (IndexOutOfRangeException)
            {
                // Reading past the end of the data means the stream stopped without its end-of-data
                // marker, which truncated and malformed PDFs both do. 257 is that marker, so
                // answering it here ends the decode tidily rather than throwing. Narrow on purpose:
                // an index past the end is the only failure this arithmetic can produce, and
                // anything else coming out of it would be a bug worth seeing.
                return 257;
            }
        }
    }

    private readonly int[] _andTable = [511, 1023, 2047, 4095];
    private byte[][] _stringTable;
    private byte[] _data;
    private int _tableIndex, _bitsToGet = 9;
    private int _bytePointer;
    private int _nextData;
    private int _nextBits;
}
