using System;
using System.Diagnostics.CodeAnalysis;

namespace PdfPinata.Pdf.Rendering;

/// <summary>How the samples of a <see cref="PageImage"/> are laid out.</summary>
[Experimental("PPEXP001")]
public enum PageImageFormat
{
    /// <summary>
    /// Four bytes a sample: red, green, blue and alpha, the alpha not multiplied into the colour.
    /// </summary>
    Rgba32,

    /// <summary>
    /// One byte a sample: how much of it is covered, from 0 to 255. A stencil mask, painted in
    /// the paint's colour where it is covered.
    /// </summary>
    Alpha8
}

/// <summary>An encoding a <see cref="PageDevice"/> may be asked to decode.</summary>
[Experimental("PPEXP001")]
public enum PageImageCodec
{
    /// <summary>A JPEG file: what a PDF's <c>/DCTDecode</c> filter holds.</summary>
    Jpeg
}

/// <summary>
/// An image as page rendering draws it: decoded samples, rows from the top, no padding between
/// rows, with every mask already applied to them.
/// </summary>
[Experimental("PPEXP001")]
public sealed class PageImage
{
    private PageImage(int width, int height, PageImageFormat format, ReadOnlyMemory<byte> data)
    {
        Width = width;
        Height = height;
        Format = format;
        Data = data;
    }

    /// <summary>The width in samples. At least 1.</summary>
    public int Width { get; }

    /// <summary>The height in samples. At least 1.</summary>
    public int Height { get; }

    /// <summary>How <see cref="Data"/> is laid out.</summary>
    public PageImageFormat Format { get; }

    /// <summary>
    /// The samples: <see cref="Width"/> times <see cref="Height"/> of them, four bytes each for
    /// <see cref="PageImageFormat.Rgba32"/> and one for <see cref="PageImageFormat.Alpha8"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>An image of colour samples.</summary>
    /// <param name="width">The width in samples.</param>
    /// <param name="height">The height in samples.</param>
    /// <param name="data">Four bytes a sample: red, green, blue, alpha, rows from the top.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is less than 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> is not four bytes for every sample.</exception>
    public static PageImage FromRgba32(int width, int height, ReadOnlyMemory<byte> data) =>
        Create(width, height, PageImageFormat.Rgba32, 4, data);

    /// <summary>A stencil mask.</summary>
    /// <param name="width">The width in samples.</param>
    /// <param name="height">The height in samples.</param>
    /// <param name="data">One byte a sample, 0 for not painted to 255 for painted, rows from the top.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is less than 1.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> is not one byte for every sample.</exception>
    public static PageImage FromAlpha8(int width, int height, ReadOnlyMemory<byte> data) =>
        Create(width, height, PageImageFormat.Alpha8, 1, data);

    private static PageImage Create(int width, int height, PageImageFormat format, int bytesPerSample,
        ReadOnlyMemory<byte> data)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), width, "An image is at least one sample wide.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), height, "An image is at least one sample high.");
        if (data.Length != (long)width * height * bytesPerSample)
        {
            throw new ArgumentException(
                $"An image of {width} by {height} samples takes {(long)width * height * bytesPerSample} bytes, and {data.Length} were given.",
                nameof(data));
        }

        return new PageImage(width, height, format, data);
    }
}
