using System;

namespace PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;

/// <summary>
/// The decoded pixels of one image, handed across the <see cref="ImageSource.IImageSource"/> seam.
/// </summary>
/// <remarks>
/// <para>
/// There is exactly one layout: tightly packed, <b>top-down</b>, four bytes per pixel in
/// <b>B, G, R, A</b> order, one byte per channel, with straight (unpremultiplied) alpha. A row is
/// always exactly <c>Width * 4</c> bytes, so there is no stride to align and no format tag to read -
/// the buffer says how many pixels it holds, and that is the whole of its description.
/// </para>
/// <para>
/// Top-down means row 0 is the top of the image, which is also the row the PDF writer emits first.
/// A backend that decodes bottom-up flips on the way out; nothing downstream flips again.
/// </para>
/// <para>
/// The bytes are the backend's to produce and the writer's to read. A backend hands over a buffer it
/// no longer writes to, so the reader may keep the <see cref="ReadOnlyMemory{T}"/> for as long as it
/// needs it.
/// </para>
/// </remarks>
public readonly struct PixelBuffer
{
    /// <summary>
    /// Number of bytes one pixel occupies: B, G, R and A, one byte each.
    /// </summary>
    public const int BytesPerPixel = 4;

    /// <summary>
    /// Wraps decoded pixels, checking only that there are as many bytes as the size claims.
    /// </summary>
    /// <param name="width">Width of the image in pixels.</param>
    /// <param name="height">Height of the image in pixels.</param>
    /// <param name="pixels">
    /// The pixel bytes, tightly packed top-down BGRA. Must be exactly
    /// <paramref name="width"/> * <paramref name="height"/> * <see cref="BytesPerPixel"/> long.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The buffer is not the length the width and height call for. This is the one thing worth
    /// checking here, because it is the difference between a backend's mistake being named at the
    /// seam it was made at and being an index out of range in the middle of writing a PDF.
    /// </exception>
    public PixelBuffer(int width, int height, ReadOnlyMemory<byte> pixels)
    {
        if (width < 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width cannot be negative.");
        if (height < 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height cannot be negative.");

        long expected = (long)width * height * BytesPerPixel;
        if (pixels.Length != expected)
            throw new ArgumentException(
                "A " + width + "x" + height + " PixelBuffer needs exactly " + expected
                + " bytes of tightly packed BGRA, but " + pixels.Length + " were given.",
                nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>Width of the image in pixels.</summary>
    public int Width { get; }

    /// <summary>Height of the image in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// The pixel bytes, tightly packed top-down BGRA, <c>Width * 4</c> bytes per row.
    /// </summary>
    public ReadOnlyMemory<byte> Pixels { get; }

    /// <summary>
    /// Whether the buffer holds no pixels at all, which is what a default instance is and what an
    /// image of no extent decodes to.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
