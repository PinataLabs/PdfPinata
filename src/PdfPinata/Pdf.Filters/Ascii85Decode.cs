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

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Implements the ASCII85Decode filter.
/// </summary>
public class Ascii85Decode : Filter
{
    // Reference: 3.3.2  ASCII85Decode Filter / Page 69

    /// <summary>
    /// Encodes the specified data.
    /// </summary>
    public override byte[] Encode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var length = data.Length;  // length == 0 is must not be treated as a special case.
        var words = length / 4;
        var rest = length - words * 4;
        var result = new byte[words * 5 + (rest == 0 ? 0 : rest + 1) + 2];

        int idxIn = 0, idxOut = 0;
        var wCount = 0;
        while (wCount < words)
        {
            var val = ((uint)data[idxIn++] << 24) + ((uint)data[idxIn++] << 16) + ((uint)data[idxIn++] << 8) + data[idxIn++];
            if (val == 0)
            {
                result[idxOut++] = (byte)'z';
            }
            else
            {
                var c5 = (byte)(val % 85 + '!');
                val /= 85;
                var c4 = (byte)(val % 85 + '!');
                val /= 85;
                var c3 = (byte)(val % 85 + '!');
                val /= 85;
                var c2 = (byte)(val % 85 + '!');
                val /= 85;
                var c1 = (byte)(val + '!');

                result[idxOut++] = c1;
                result[idxOut++] = c2;
                result[idxOut++] = c3;
                result[idxOut++] = c4;
                result[idxOut++] = c5;
            }
            wCount++;
        }
        if (rest == 1)
        {
            var val = (uint)data[idxIn] << 24;
            val /= 85 * 85 * 85;
            var c2 = (byte)(val % 85 + '!');
            val /= 85;
            var c1 = (byte)(val + '!');

            result[idxOut++] = c1;
            result[idxOut++] = c2;
        }
        else if (rest == 2)
        {
            var val = ((uint)data[idxIn++] << 24) + ((uint)data[idxIn] << 16);
            val /= 85 * 85;
            var c3 = (byte)(val % 85 + '!');
            val /= 85;
            var c2 = (byte)(val % 85 + '!');
            val /= 85;
            var c1 = (byte)(val + '!');

            result[idxOut++] = c1;
            result[idxOut++] = c2;
            result[idxOut++] = c3;
        }
        else if (rest == 3)
        {
            var val = ((uint)data[idxIn++] << 24) + ((uint)data[idxIn++] << 16) + ((uint)data[idxIn] << 8);
            val /= 85;
            var c4 = (byte)(val % 85 + '!');
            val /= 85;
            var c3 = (byte)(val % 85 + '!');
            val /= 85;
            var c2 = (byte)(val % 85 + '!');
            val /= 85;
            var c1 = (byte)(val + '!');

            result[idxOut++] = c1;
            result[idxOut++] = c2;
            result[idxOut++] = c3;
            result[idxOut++] = c4;
        }
        result[idxOut++] = (byte)'~';
        result[idxOut++] = (byte)'>';

        if (idxOut < result.Length)
            Array.Resize(ref result, idxOut);

        return result;
    }

    /// <summary>
    /// Decodes the specified data.
    /// </summary>
    public override byte[] Decode(byte[] data, FilterParms parms)
    {
        ArgumentNullException.ThrowIfNull(data);

        int idx;
        var length = data.Length;
        var zCount = 0;
        var idxOut = 0;
        // How far into a five-character group the characters kept so far reach.
        var groupLength = 0;
        for (idx = 0; idx < length; idx++)
        {
            var ch = (char)data[idx];
            if (ch is >= '!' and <= 'u')
            {
                data[idxOut++] = (byte)ch;
                groupLength = (groupLength + 1) % 5;
            }
            else if (ch == 'z')
            {
                // A z stands for a whole group of zeros, so it can only come where a group would
                // begin. Inside one it would be read as a digit worth 89, which no digit is, and
                // every group after it would be read out of step.
                if (groupLength != 0)
                    throw new ArgumentException(@"Illegal character 'z' inside a group.", nameof(data));
                data[idxOut++] = (byte)ch;
                zCount++;
            }
            else if (ch == '~')
            {
                // The end marker is two characters, and data that stops between them is as
                // malformed as data that spells them wrongly. Reading the second one without
                // checking there is one turns a truncated stream into an index out of range.
                if (idx + 1 >= length || (char)data[idx + 1] != '>')
                    throw new ArgumentException(@"Illegal character.", nameof(data));
                break;
            }
            // ignore unknown character
        }
        // Loop not ended with break?
        if (idx == length)
            throw new ArgumentException(@"Illegal character.", nameof(data));

        length = idxOut;
        var nonZero = length - zCount;
        var byteCount = 4 * (zCount + nonZero / 5); // full 4 byte blocks

        var remainder = nonZero % 5;
        if (remainder == 1)
            throw new InvalidOperationException("Illegal character.");

        if (remainder != 0)
            byteCount += remainder - 1;

        var output = new byte[byteCount];

        idxOut = 0;
        idx = 0;
        while (idx < length)
        {
            if ((char)data[idx] == 'z')
            {
                // A z stands for four zero bytes and is one character wide rather than five.
                // Taking it before asking whether a whole group is left is what keeps idx and
                // idxOut pointing at the trailing partial group once the loop ends.
                idx++;
                idxOut += 4;
                continue;
            }

            // Fewer than five characters left, so what remains is the trailing partial group,
            // which the code below the loop reads.
            if (length - idx < 5)
                break;

            // Every character here is '!' to 'u', a digit from 0 to 84, so only the first term can
            // take the sum past 32 bits - hence the long - and the check below refuses one that does.
            var value =
                (long)(data[idx++] - '!') * (85 * 85 * 85 * 85) +
                (uint)(data[idx++] - '!') * (85 * 85 * 85) +
                (uint)(data[idx++] - '!') * (85 * 85) +
                (uint)(data[idx++] - '!') * 85 +
                (uint)(data[idx++] - '!');

            if (value > uint.MaxValue)
                throw new InvalidOperationException("Value of group greater than 2 power 32 - 1.");

            output[idxOut++] = (byte)(value >> 24);
            output[idxOut++] = (byte)(value >> 16);
            output[idxOut++] = (byte)(value >> 8);
            output[idxOut++] = (byte)value;
        }

        // I have found no appropriate algorithm, so I write my own. In some rare cases the value must not
        // be increased by one, but I cannot found a general formula or a proof.
        // All possible cases are tested programmatically.
        if (remainder == 2) // one byte
        {
            var value =
                (uint)(data[idx++] - '!') * (85 * 85 * 85 * 85) +
                (uint)(data[idx] - '!') * (85 * 85 * 85);

            // Always increase if not zero (tried out).
            if (value != 0)
                value += 0x01000000;

            output[idxOut] = (byte)(value >> 24);
        }
        else if (remainder == 3) // two bytes
        {
            var idxIn = idx;
            var value =
                (uint)(data[idx++] - '!') * (85 * 85 * 85 * 85) +
                (uint)(data[idx++] - '!') * (85 * 85 * 85) +
                (uint)(data[idx] - '!') * (85 * 85);

            if (value != 0)
            {
                value &= 0xFFFF0000;
                var val = value / (85 * 85);
                var c3 = (byte)(val % 85 + '!');
                val /= 85;
                var c2 = (byte)(val % 85 + '!');
                val /= 85;
                var c1 = (byte)(val + '!');
                if (c1 != data[idxIn] || c2 != data[idxIn + 1] || c3 != data[idxIn + 2])
                {
                    value += 0x00010000;
                }
            }
            output[idxOut++] = (byte)(value >> 24);
            output[idxOut] = (byte)(value >> 16);
        }
        else if (remainder == 4) // three bytes
        {
            var idxIn = idx;
            var value =
                (uint)(data[idx++] - '!') * (85 * 85 * 85 * 85) +
                (uint)(data[idx++] - '!') * (85 * 85 * 85) +
                (uint)(data[idx++] - '!') * (85 * 85) +
                (uint)(data[idx] - '!') * 85;

            if (value != 0)
            {
                value &= 0xFFFFFF00;
                var val = value / 85;
                var c4 = (byte)(val % 85 + '!');
                val /= 85;
                var c3 = (byte)(val % 85 + '!');
                val /= 85;
                var c2 = (byte)(val % 85 + '!');
                val /= 85;
                var c1 = (byte)(val + '!');
                if (c1 != data[idxIn] || c2 != data[idxIn + 1] || c3 != data[idxIn + 2] || c4 != data[idxIn + 3])
                {
                    value += 0x00000100;
                }
            }
            output[idxOut++] = (byte)(value >> 24);
            output[idxOut++] = (byte)(value >> 16);
            output[idxOut] = (byte)(value >> 8);
        }
        return output;
    }
}
