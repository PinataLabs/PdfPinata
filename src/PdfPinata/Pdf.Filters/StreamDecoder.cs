using PdfPinata.Pdf.IO;

namespace PdfPinata.Pdf.Filters;

internal static class StreamDecoder
{
    // PdfReference, chapter 7.4.4.3

    /// <summary>
    /// Further decodes a stream of bytes that were processed by the Flate- or LZW-decoder. 
    /// </summary>
    /// <param name="data">The data to decode</param>
    /// <param name="decodeParms">Parameters for the decoder. If this is null, <paramref name="data"/> is returned unchanged</param>
    /// <returns>The decoded data as a byte-array</returns>
    /// <exception cref="PdfReaderException">The predictor or the number of bits per component is
    /// not one the reference defines, or a PNG-predicted row names a filter type other than 0 to 4.</exception>
    public static byte[] Decode(byte[] data, PdfDictionary decodeParms)
    {
        if (decodeParms == null)
            return data;

        var predictor = decodeParms.Elements.GetInteger("/Predictor");
        var colors = decodeParms.Elements.GetInteger("/Colors");
        var bpc = decodeParms.Elements.GetInteger("/BitsPerComponent");
        var columns = decodeParms.Elements.GetInteger("/Columns");

        // set up defaults according to the spec
        if (predictor < 1)
            predictor = 1;
        if (colors < 1)
            colors = 1;
        if (bpc < 1)
            bpc = 8;
        if (columns < 1)
            columns = 1;

        if (predictor == 1)     // no prediction, return data as is
            return data;

        // TIFF predictor
        if (predictor == 2)
        {
            if (bpc != 1 && bpc != 2 && bpc != 4 && bpc != 8 && bpc != 16)
                throw new PdfReaderException("Invalid number of bits per component");
            return UndoTiffPredictor(data, colors, bpc, columns);
        }

        // PNG predictors
        if (predictor < 10 || predictor > 15)
            throw new PdfReaderException("Invalid predictor " + predictor);

        if (bpc != 1 && bpc != 2 && bpc != 4 && bpc != 8 && bpc != 16)
            throw new PdfReaderException("Invalid number of bits per component");
        var stride = (bpc * colors * columns + 7) / 8;
        var rows = data.Length / (stride + 1);
        var unfilteredData = new byte[rows * stride];
        PngFilter.Unfilter(stride, (bpc * colors + 7) / 8, data, unfilteredData);
        return unfilteredData;
    }

    /// <summary>
    /// Undoes TIFF Predictor 2, horizontal differencing (TIFF 6.0 section 14, ISO 32000-1 7.4.4.4):
    /// every sample but those of a row's first pixel was written as its difference from the same
    /// component of the pixel to its left, modulo 2 to the power of the component size. Rows are
    /// predicted independently and each begins on a byte boundary; sixteen-bit samples are big-endian.
    /// A trailing part row is left as it was, since there is no telling where its pixels fall.
    /// </summary>
    private static byte[] UndoTiffPredictor(byte[] data, int colors, int bpc, int columns)
    {
        var result = (byte[])data.Clone();
        var stride = (bpc * colors * columns + 7) / 8;
        var samplesPerRow = colors * columns;
        var mask = (1 << bpc) - 1;

        for (var row = 0; row + stride <= result.Length; row += stride)
        {
            for (var sample = colors; sample < samplesPerRow; sample++)
            {
                var left = ReadSample(result, row, sample - colors, bpc);
                var difference = ReadSample(result, row, sample, bpc);
                WriteSample(result, row, sample, bpc, (left + difference) & mask);
            }
        }
        return result;
    }

    private static int ReadSample(byte[] data, int row, int sample, int bpc)
    {
        switch (bpc)
        {
            case 16:
                return data[row + 2 * sample] << 8 | data[row + 2 * sample + 1];
            case 8:
                return data[row + sample];
            default:
                var bit = sample * bpc;
                var shift = 8 - bpc - bit % 8;
                return data[row + bit / 8] >> shift & ((1 << bpc) - 1);
        }
    }

    private static void WriteSample(byte[] data, int row, int sample, int bpc, int value)
    {
        switch (bpc)
        {
            case 16:
                data[row + 2 * sample] = (byte)(value >> 8);
                data[row + 2 * sample + 1] = (byte)value;
                break;
            case 8:
                data[row + sample] = (byte)value;
                break;
            default:
                var bit = sample * bpc;
                var shift = 8 - bpc - bit % 8;
                var mask = ((1 << bpc) - 1) << shift;
                var index = row + bit / 8;
                data[index] = (byte)(data[index] & ~mask | value << shift);
                break;
        }
    }
}
