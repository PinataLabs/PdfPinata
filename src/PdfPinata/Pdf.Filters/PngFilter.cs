using PdfPinata.Pdf.IO;
using System;

namespace PdfPinata.Pdf.Filters;

internal static class PngFilter
{
    /// <summary>
    /// Implements PNG-Filtering according to the PNG-specification<br></br>
    /// see: https://datatracker.ietf.org/doc/html/rfc2083#section-6
    /// </summary>
    /// <param name="stride">The width of a scanline in bytes</param>
    /// <param name="bpp">Bytes per pixel</param>
    /// <param name="inData">The input data</param>
    /// <param name="outData">The target array where the unfiltered data is stored</param>
    internal static void Unfilter(int stride, int bpp, byte[] inData, byte[] outData)
    {
        var prevRow = new byte[stride];
        var row = new byte[stride];
        var pos = 0;
        var outIndex = 0;
        while (pos < inData.Length)
        {
            Array.Copy(inData, pos + 1, row, 0, stride);
            var filterType = inData[pos];
            if (filterType > 4)
                throw new PdfReaderException($"Unexpected Png-Predictor {filterType} in Xref Stream. Expected 0 to 4.");
            UnfilterRow(filterType, row, prevRow, bpp, outData, outIndex);
            outIndex += row.Length;
            // remember current scanline
            Array.Copy(outData, outIndex - stride, prevRow, 0, stride);
            pos += stride + 1;    // each scanline is preceded by a predictor-byte
        }
    }

    /// <summary>
    /// Undoes one row's filter, writing the row's bytes to <paramref name="outData"/> from
    /// <paramref name="outIndex"/> on. The bytes to the left of a pixel are read back out of
    /// <paramref name="outData"/>, which is where the row being undone already is.
    /// </summary>
    private static void UnfilterRow(byte filterType, byte[] row, byte[] prevRow, int bpp, byte[] outData, int outIndex)
    {
        switch (filterType)
        {
            case 0:         // None
                UnfilterNone(row, outData, outIndex);
                break;
            case 1:         // Sub
                UnfilterSub(row, bpp, outData, outIndex);
                break;
            case 2:         // Up
                UnfilterUp(row, prevRow, outData, outIndex);
                break;
            case 3:         // Average
                UnfilterAverage(row, prevRow, bpp, outData, outIndex);
                break;
            case 4:         // Paeth
                UnfilterPaeth(row, prevRow, bpp, outData, outIndex);
                break;
        }
    }

    private static void UnfilterNone(byte[] row, byte[] outData, int outIndex)
    {
        foreach (var value in row)
            outData[outIndex++] = value;
    }

    private static void UnfilterSub(byte[] row, int bpp, byte[] outData, int outIndex)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i < bpp ? 0 : outData[outIndex - bpp];
            outData[outIndex++] = (byte)(row[i] + left);
        }
    }

    private static void UnfilterUp(byte[] row, byte[] prevRow, byte[] outData, int outIndex)
    {
        for (var i = 0; i < row.Length; i++)
            outData[outIndex++] = (byte)(row[i] + prevRow[i]);
    }

    private static void UnfilterAverage(byte[] row, byte[] prevRow, int bpp, byte[] outData, int outIndex)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i < bpp ? 0 : outData[outIndex - bpp];
            outData[outIndex++] = (byte)(row[i] + (byte)((left + prevRow[i]) / 2));
        }
    }

    private static void UnfilterPaeth(byte[] row, byte[] prevRow, int bpp, byte[] outData, int outIndex)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i < bpp ? (byte)0 : outData[outIndex - bpp];
            var above = prevRow[i];
            var aboveLeft = i < bpp ? (byte)0 : prevRow[i - bpp];
            outData[outIndex++] = (byte)(row[i] + PaethPredictor(left, above, aboveLeft));
        }
    }

    // https://datatracker.ietf.org/doc/html/rfc2083#page-36
    private static byte PaethPredictor(byte a, byte b, byte c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
            return a;
        if (pb <= pc)
            return b;
        return c;
    }
}
