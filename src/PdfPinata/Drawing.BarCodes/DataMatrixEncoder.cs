using System;
using System.Collections.Generic;

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// Turns text into the data codewords of an ecc200 DataMatrix symbol.
/// <para>
/// Only the ASCII encodation is produced. It carries any byte of data - a pair of digits in
/// one codeword, a character below 128 in one, and anything above it in two - so every symbol
/// this writes is one a reader can read. The denser encodations for runs of one kind of
/// character are not written; asking for one says so rather than quietly writing ASCII, so
/// that a caller who needs the density knows it did not get it.
/// </para>
/// </summary>
internal static class DataMatrixEncoder
{
    /// <summary>The codeword that pads a symbol out to its capacity.</summary>
    internal const byte Pad = 129;

    /// <summary>The codeword that says the next one stands for a character 128 higher.</summary>
    private const byte UpperShift = 235;

    /// <summary>
    /// The data codewords for the text, padded out to the capacity given.
    /// </summary>
    internal static byte[] Encode(string text, string encoding, int capacity)
    {
        var codewords = EncodeAscii(text, encoding);

        if (codewords.Count > capacity)
            throw new InvalidOperationException(BcgSR.DataMatrixTooBig);

        Pad_(codewords, capacity);
        return [..codewords];
    }

    /// <summary>
    /// The number of codewords the text needs, so that a symbol to hold it can be chosen.
    /// </summary>
    internal static int CountCodewords(string text, string encoding)
    {
        return EncodeAscii(text, encoding).Count;
    }

    private static List<byte> EncodeAscii(string text, string encoding)
    {
        RejectUnwrittenEncodations(encoding);

        var codewords = new List<byte>();
        var at = 0;
        while (at < text.Length)
        {
            var ch = text[at];

            // Two digits go into one codeword, which is what makes ASCII the right encodation
            // for the numbers most of these codes carry.
            if (IsDigit(ch) && at + 1 < text.Length && IsDigit(text[at + 1]))
            {
                var value = (ch - '0') * 10 + (text[at + 1] - '0');
                codewords.Add((byte)(value + 130));
                at += 2;
                continue;
            }

            if (ch < 128)
            {
                codewords.Add((byte)(ch + 1));
                at++;
                continue;
            }

            if (ch < 256)
            {
                codewords.Add(UpperShift);
                codewords.Add((byte)(ch - 128 + 1));
                at++;
                continue;
            }

            throw new InvalidOperationException(BcgSR.DataMatrixCharacterTooBig(ch));
        }

        return codewords;
    }

    /// <summary>
    /// Pads the codewords out to the capacity of the symbol. The first pad says the data has
    /// ended; the rest are scrambled by position, so that a symbol mostly of padding does not
    /// come out as a block of one pattern.
    /// </summary>
    private static void Pad_(List<byte> codewords, int capacity)
    {
        if (codewords.Count >= capacity)
            return;

        codewords.Add(Pad);
        while (codewords.Count < capacity)
            codewords.Add(Randomize253(Pad, codewords.Count + 1));
    }

    /// <summary>
    /// The 253-state randomizing that pad codewords after the first are scrambled by.
    /// </summary>
    private static byte Randomize253(byte codeword, int position)
    {
        var pseudoRandom = ((149 * position) % 253) + 1;
        var value = codeword + pseudoRandom;
        return (byte)(value <= 254 ? value : value - 254);
    }

    private static bool IsDigit(char ch)
    {
        return ch >= '0' && ch <= '9';
    }

    /// <summary>
    /// The encoding string names an encodation per character. Anything but ASCII is turned
    /// down rather than written as ASCII behind the caller's back.
    /// </summary>
    private static void RejectUnwrittenEncodations(string encoding)
    {
        if (string.IsNullOrEmpty(encoding))
            return;

        foreach (var scheme in encoding)
        {
            if (scheme != 'a' && scheme != '\0')
                throw new NotImplementedException(BcgSR.DataMatrixEncodationNotImplemented(scheme));
        }
    }
}
