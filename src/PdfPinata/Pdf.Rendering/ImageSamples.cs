namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The unfiltered samples of an image, read one component at a time: 1, 2, 4, 8 or 16 bits each,
/// the high bits first, every row starting on a byte.
/// </summary>
/// <remarks>
/// A sample past the end of the data reads as 0, so data that stops short is drawn as far as it
/// goes and costs no copy to pad.
/// </remarks>
internal sealed class ImageSamples
{
    private readonly byte[] _data;
    private readonly long _rowBits;

    internal ImageSamples(byte[] data, int width, int height, int components, int bitsPerComponent)
    {
        _data = data;
        Width = width;
        Height = height;
        Components = components;
        BitsPerComponent = bitsPerComponent;
        _rowBits = ((long)width * components * bitsPerComponent + 7) / 8 * 8;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal int Components { get; }

    internal int BitsPerComponent { get; }

    /// <summary>The largest value a sample can have: 1 for one bit, 65535 for sixteen.</summary>
    internal int MaxValue => (1 << BitsPerComponent) - 1;

    /// <summary>How many bytes the data holds.</summary>
    internal long Length => _data.Length;

    /// <summary>How many bytes the rows need.</summary>
    internal long LengthNeeded => LengthOf(Width, Height, Components, BitsPerComponent);

    /// <summary>How many bytes the rows of an image of this size and depth need.</summary>
    internal static long LengthOf(int width, int height, int components, int bitsPerComponent) =>
        ((long)width * components * bitsPerComponent + 7) / 8 * height;

    /// <summary>The sample of a component of the sample at column x of row y, as the integer it is written as.</summary>
    internal int Read(int x, int y, int component)
    {
        var bit = y * _rowBits + ((long)x * Components + component) * BitsPerComponent;
        var index = bit >> 3;
        return BitsPerComponent switch
        {
            8 => ByteAt(index),
            16 => ByteAt(index) << 8 | ByteAt(index + 1),
            _ => ByteAt(index) >> (8 - BitsPerComponent - (int)(bit & 7)) & MaxValue
        };
    }

    private int ByteAt(long index) => index < _data.Length ? _data[index] : 0;
}
