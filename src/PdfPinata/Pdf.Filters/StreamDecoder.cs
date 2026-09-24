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

        // set up defaults according to the spec
        var predictor = PositiveOrDefault(decodeParms, "/Predictor", 1);
        var colors = PositiveOrDefault(decodeParms, "/Colors", 1);
        var bpc = PositiveOrDefault(decodeParms, "/BitsPerComponent", 8);
        var columns = PositiveOrDefault(decodeParms, "/Columns", 1);

        if (predictor == 1)     // no prediction, return data as is
            return data;

        // TIFF predictor
        if (predictor == 2)
        {
            EnsureValidBitsPerComponent(bpc);
            return UndoTiffPredictor(data, colors, bpc, columns);
        }

        // PNG predictors
        if (predictor is < 10 or > 15)
            throw new PdfReaderException("Invalid predictor " + predictor);

        EnsureValidBitsPerComponent(bpc);
        return UndoPngPredictor(data, colors, bpc, columns);
    }

    /// <summary>
    /// The integer under the key, or the default the specification gives where it is absent or
    /// not positive.
    /// </summary>
    private static int PositiveOrDefault(PdfDictionary decodeParms, string key, int defaultValue)
    {
        var value = decodeParms.Elements.GetInteger(key);
        return value < 1 ? defaultValue : value;
    }

    private static void EnsureValidBitsPerComponent(int bpc)
    {
        if (bpc is not (1 or 2 or 4 or 8 or 16))
            throw new PdfReaderException("Invalid number of bits per component");
    }

    /// <summary>
    /// Undoes a PNG predictor, which puts a byte naming the filter in front of every row.
    /// </summary>
    private static byte[] UndoPngPredictor(byte[] data, int colors, int bpc, int columns)
    {
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
