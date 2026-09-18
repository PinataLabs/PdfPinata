using System;
using System.IO;
using System.Runtime.InteropServices;
using ImageMagick;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
/// Makes Ghostscript available to Magick.NET for PDF rasterization.
/// </summary>
/// <remarks>
/// The Ghostscript.NativeAssets package supplies the binaries, so no machine-wide install is
/// needed. On Windows, ImageMagick loads gsdll64.dll in process once the directory is set;
/// without it, it falls back to spawning gswin64c.exe from PATH and fails on a machine that
/// has no Ghostscript installed. On other platforms ImageMagick invokes the system "gs"
/// delegate instead, so the directory is deliberately left alone there rather than pointing a
/// working system installation at a different Ghostscript build.
/// </remarks>
internal static class GhostscriptSetup
{
    private static readonly Lazy<bool> AvailableLazy = new Lazy<bool>(Probe);

    /// <summary>
    /// True when a PDF can actually be rasterized, whatever the source of Ghostscript.
    /// Determined by rasterizing once rather than by looking for an installation, so the
    /// answer is accurate on every platform.
    /// </summary>
    public static bool IsAvailable => AvailableLazy.Value;

    public static void Configure()
    {
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return;

            var directory = FindNativeDirectory();
            if (directory != null)
                MagickNET.SetGhostscriptDirectory(directory);
        }
        catch
        {
            // Leave Magick.NET to find Ghostscript by itself; IsAvailable still reports the truth.
        }
    }

    private static string FindNativeDirectory()
    {
        var fileName = RuntimeInformation.OSArchitecture == Architecture.X86
            ? "gsdll32.dll"
            : "gsdll64.dll";

        var baseDirectory = AppContext.BaseDirectory;
        var runtimeIdentifier = RuntimeInformation.OSArchitecture == Architecture.X86 ? "win-x86" : "win-x64";

        // The package drops the binaries next to the test assembly and also keeps the
        // per-runtime tree, so check both.
        var candidates = new[]
        {
            baseDirectory,
            Path.Combine(baseDirectory, "runtimes", runtimeIdentifier, "native")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, fileName)))
                return candidate;
        }

        return null;
    }

    private static bool Probe()
    {
        try
        {
            var document = new PdfDocument();
            document.AddPage();

            // A blank page keeps the probe independent of font resolution.
            using (var rasterized = PdfHelper.Rasterize(document).ImageCollection)
                return rasterized.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
