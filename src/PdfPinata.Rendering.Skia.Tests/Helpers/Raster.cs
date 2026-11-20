using System;
using System.IO;
using SkiaSharp;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>An opaque image as three bytes a pixel, red, green and blue, rows from the top.</summary>
internal sealed class Raster(int width, int height, byte[] rgb)
{
    public int Width { get; } = width;

    public int Height { get; } = height;

    public byte[] Rgb { get; } = rgb;

    /// <summary>A bitmap's pixels, composited onto white.</summary>
    public static Raster Of(SKBitmap bitmap)
    {
        var rgb = new byte[bitmap.Width * bitmap.Height * 3];
        var i = 0;
        foreach (var pixel in bitmap.Pixels)
        {
            var alpha = pixel.Alpha / 255.0;
            rgb[i++] = OnWhite(pixel.Red, alpha);
            rgb[i++] = OnWhite(pixel.Green, alpha);
            rgb[i++] = OnWhite(pixel.Blue, alpha);
        }

        return new Raster(bitmap.Width, bitmap.Height, rgb);
    }

    /// <summary>The rectangle of this raster at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public Raster Crop(int x, int y, int width, int height)
    {
        if (x < 0 || y < 0 || x + width > Width || y + height > Height)
            throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}, {width}, {height}) is not inside {Width} by {Height}.");

        var rgb = new byte[width * height * 3];
        for (var row = 0; row < height; row++)
            Array.Copy(Rgb, ((y + row) * Width + x) * 3, rgb, row * width * 3, width * 3);
        return new Raster(width, height, rgb);
    }

    public void SavePng(string path)
    {
        using var bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        var pixels = new SKColor[Width * Height];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = new SKColor(Rgb[i * 3], Rgb[i * 3 + 1], Rgb[i * 3 + 2]);
        bitmap.Pixels = pixels;
        using var file = File.Create(path);
        bitmap.Encode(file, SKEncodedImageFormat.Png, 100);
    }

    // An SKColor is not premultiplied, whatever the bitmap holds.
    private static byte OnWhite(byte colour, double alpha) => (byte)Math.Round(colour * alpha + 255 * (1 - alpha));
}
