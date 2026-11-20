using System;
using System.Globalization;
using System.IO;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>
/// How far a page this package renders is from Ghostscript's rasterization of the same saved PDF
/// at the same resolution.
/// </summary>
/// <remarks>
/// <para>
/// The document is saved and read back, and the page that is rendered is the one read back: an
/// unsaved page can differ from what a reader meets. Ghostscript is handed the same bytes.
/// </para>
/// <para>
/// Two numbers, because anti-aliasing never matches byte for byte. The mean channel difference is
/// over every channel of every pixel, from 0 to 255, and says how far the pages are overall. The
/// share over the threshold is of pixels any channel of which differs by more than
/// <see cref="Threshold"/>, and says how much of the page is plainly a different colour: a shape
/// in the wrong place or the wrong colour moves it, where soft edges barely do.
/// </para>
/// </remarks>
internal sealed class Fidelity
{
    /// <summary>A channel difference past which a pixel is a different colour rather than a different edge.</summary>
    public const int Threshold = 64;

    private readonly string _name;
    private readonly Raster _actual;
    private readonly Raster _expected;

    private Fidelity(string name, Raster actual, Raster expected)
    {
        _name = name;
        _actual = actual;
        _expected = expected;

        long sum = 0;
        var over = 0;
        for (var pixel = 0; pixel < actual.Width * actual.Height; pixel++)
        {
            var largest = 0;
            for (var channel = 0; channel < 3; channel++)
            {
                var difference = Math.Abs(actual.Rgb[pixel * 3 + channel] - expected.Rgb[pixel * 3 + channel]);
                sum += difference;
                largest = Math.Max(largest, difference);
            }

            if (largest > Threshold)
                over++;
        }

        MeanChannelDifference = (double)sum / (actual.Width * actual.Height * 3);
        ShareOverThreshold = (double)over / (actual.Width * actual.Height);
    }

    public double MeanChannelDifference { get; }

    public double ShareOverThreshold { get; }

    /// <summary>
    /// Saves <paramref name="document"/>, reads it back, renders its first page with
    /// <paramref name="options"/>, and rasterizes the same bytes with Ghostscript at the same
    /// resolution, cut to the same region.
    /// </summary>
    public static Fidelity Of(PdfDocument document, string name, PageRenderOptions options = null)
    {
        options ??= new PageRenderOptions();
        var bytes = Saved.Bytes(document);

        Raster actual;
        using (var bitmap = Saved.Open(bytes).Pages[0].ToSKBitmap(options))
            actual = Raster.Of(bitmap);

        var expected = Ghostscript.Rasterize(bytes, 72 * options.Scale);
        var region = options.Region;
        var x = region == null ? 0 : (int)Math.Round(region.Value.X * options.Scale);
        var y = region == null ? 0 : (int)Math.Round(region.Value.Y * options.Scale);
        if (x + actual.Width > expected.Width || y + actual.Height > expected.Height)
        {
            throw new InvalidOperationException(
                $"{name}: rendered {actual.Width} by {actual.Height} at ({x}, {y}), and Ghostscript drew {expected.Width} by {expected.Height}.");
        }

        if (region == null && (actual.Width != expected.Width || actual.Height != expected.Height))
        {
            throw new InvalidOperationException(
                $"{name}: rendered {actual.Width} by {actual.Height}, and Ghostscript drew {expected.Width} by {expected.Height}.");
        }

        return new Fidelity(name, actual, expected.Crop(x, y, actual.Width, actual.Height));
    }

    /// <summary>
    /// Fails, writing the rendered page, Ghostscript's and their difference under
    /// <c>TestResults/rendering-fidelity</c>, unless both numbers are within their tolerance.
    /// </summary>
    public void ShouldBeWithin(double meanChannelDifference, double shareOverThreshold)
    {
        var measured = string.Format(CultureInfo.InvariantCulture,
            "{0}: mean channel difference {1:0.000} (at most {2}), share over {3} {4:0.0000} (at most {5})",
            _name, MeanChannelDifference, meanChannelDifference, Threshold, ShareOverThreshold, shareOverThreshold);
        Console.WriteLine(measured);

        if (MeanChannelDifference <= meanChannelDifference && ShareOverThreshold <= shareOverThreshold)
            return;

        var directory = Path.Combine(AppContext.BaseDirectory, "TestResults", "rendering-fidelity");
        Directory.CreateDirectory(directory);
        _actual.SavePng(Path.Combine(directory, _name + "-actual.png"));
        _expected.SavePng(Path.Combine(directory, _name + "-expected.png"));
        Difference().SavePng(Path.Combine(directory, _name + "-diff.png"));
        throw new InvalidOperationException($"{measured}. The pages are in {directory}.");
    }

    // White where the two agree, darker the more they differ.
    private Raster Difference()
    {
        var rgb = new byte[_actual.Rgb.Length];
        for (var i = 0; i < rgb.Length; i++)
            rgb[i] = (byte)(255 - Math.Min(255, 4 * Math.Abs(_actual.Rgb[i] - _expected.Rgb[i])));
        return new Raster(_actual.Width, _actual.Height, rgb);
    }
}
