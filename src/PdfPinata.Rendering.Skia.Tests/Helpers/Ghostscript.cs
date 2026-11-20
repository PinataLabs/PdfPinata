using System;
using System.IO;
using System.Runtime.InteropServices;
using ImageMagick;

namespace PdfPinata.Rendering.Skia.Tests.Helpers;

/// <summary>
/// Ghostscript, through Magick.NET: what a rendered page is compared with. Configured as
/// <c>PdfPinata.Test</c>'s <c>GhostscriptSetup</c> is, and rasterizing at a resolution the test
/// names rather than one chosen for it.
/// </summary>
/// <remarks>
/// On Windows the Ghostscript.NativeAssets package supplies <c>gsdll64.dll</c>, which ImageMagick
/// loads in process once it is told the directory. Elsewhere ImageMagick runs the system's
/// <c>gs</c>, so the directory is left alone there.
/// </remarks>
internal static class Ghostscript
{
    public static void Configure()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var fileName = RuntimeInformation.OSArchitecture == Architecture.X86 ? "gsdll32.dll" : "gsdll64.dll";
        var runtime = RuntimeInformation.OSArchitecture == Architecture.X86 ? "win-x86" : "win-x64";
        foreach (var candidate in new[]
                 {
                     AppContext.BaseDirectory,
                     Path.Combine(AppContext.BaseDirectory, "runtimes", runtime, "native")
                 })
        {
            if (File.Exists(Path.Combine(candidate, fileName)))
            {
                MagickNET.SetGhostscriptDirectory(candidate);
                return;
            }
        }
    }

    /// <summary>
    /// The first page of a PDF, rasterized at <paramref name="dpi"/> onto white, as RGB bytes.
    /// </summary>
    public static Raster Rasterize(byte[] pdf, double dpi)
    {
        var settings = new MagickReadSettings
        {
            Density = new Density(dpi, dpi),
            BackgroundColor = MagickColors.White,
            FrameIndex = 0,
            FrameCount = 1
        };

        using var image = new MagickImage(pdf, settings);
        image.BackgroundColor = MagickColors.White;
        image.Alpha(AlphaOption.Remove);
        using var pixels = image.GetPixels();
        return new Raster((int)image.Width, (int)image.Height, pixels.ToByteArray(PixelMapping.RGB));
    }
}
