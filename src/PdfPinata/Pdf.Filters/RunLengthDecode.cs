using System;
using System.IO;

namespace PdfPinata.Pdf.Filters;

/// <summary>
/// Implements the RunLengthDecode filter.
/// <para>
/// ISO 32000-1 7.4.5: the data is a sequence of runs, each introduced by a length byte. A length
/// from 0 to 127 is followed by that many plus one bytes, copied literally; a length from 129 to 255
/// is followed by one byte, repeated 257 minus the length times; and 128 is the end of the data.
/// </para>
/// </summary>
public class RunLengthDecode : Filter
{
    const int EndOfData = 128;

    // The most either kind of run can carry: 127 + 1 literal bytes, or 257 - 129 repeats.
    const int LongestRun = 128;

    /// <summary>
    /// Encodes the specified data, ending it with the end-of-data marker.
    /// <para>
    /// Two or more equal bytes are written as a repeat; anything else is gathered into a literal
    /// run. Any encoding that follows the rules decodes to the same bytes, so this one aims to be
    /// easy to check rather than as short as it could be.
    /// </para>
    /// </summary>
    public override byte[] Encode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var output = new MemoryStream(data.Length + data.Length / LongestRun + 2);
        var idx = 0;
        while (idx < data.Length)
        {
            var repeats = RepeatsAt(data, idx);
            if (repeats >= 2)
            {
                output.WriteByte((byte)(257 - repeats));
                output.WriteByte(data[idx]);
                idx += repeats;
                continue;
            }

            // A literal run goes on until the data ends, it is as long as one can be, or a repeat
            // begins.
            var start = idx;
            do
                idx++;
            while (idx < data.Length && idx - start < LongestRun && RepeatsAt(data, idx) < 2);

            output.WriteByte((byte)(idx - start - 1));
            output.Write(data, start, idx - start);
        }
        output.WriteByte(EndOfData);
        return output.ToArray();
    }

    // How many times the byte at the index repeats from there, counting itself, up to one run's worth.
    static int RepeatsAt(byte[] data, int index)
    {
        var count = 1;
        while (index + count < data.Length && count < LongestRun && data[index + count] == data[index])
            count++;
        return count;
    }

    /// <summary>
    /// Decodes the specified data.
    /// <para>
    /// Decoding stops at the end-of-data marker, and nothing after it is read. Data that ends
    /// without one, or ends part way through a run, gives back what it held up to that point
    /// rather than throwing: a literal run cut short keeps the bytes that are there, and a repeat
    /// missing its byte adds nothing.
    /// </para>
    /// </summary>
    public override byte[] Decode(byte[] data, FilterParms parms)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var output = new MemoryStream(data.Length * 2);
        var idx = 0;
        while (idx < data.Length)
        {
            int length = data[idx++];
            if (length == EndOfData)
                break;

            if (length < EndOfData)
            {
                var count = Math.Min(length + 1, data.Length - idx);
                output.Write(data, idx, count);
                idx += count;
            }
            else
            {
                if (idx >= data.Length)
                    break;
                var value = data[idx++];
                for (var repeat = 257 - length; repeat > 0; repeat--)
                    output.WriteByte(value);
            }
        }
        return output.ToArray();
    }
}
