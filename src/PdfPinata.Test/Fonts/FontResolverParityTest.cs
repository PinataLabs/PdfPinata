using System;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Utils;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
/// The Skia backend reads font family and style from the OpenType tables directly, because
/// SKTypeface.FamilyName reports the typographic family and would collapse "Arial Narrow"
/// into "Arial". This pins it against the ImageSharp backend, whose behaviour PdfPinata
/// shipped with: a divergence silently changes which font file a document renders with.
/// </summary>
public class FontResolverParityTest
{
    private sealed class SkiaProbe : SkiaFontResolver
    {
        public FontMetadata Read(string path) => ReadFontMetadata(path);
    }

    private sealed class ImageSharpProbe : ImageSharpFontResolver
    {
        public FontMetadata Read(string path) => ReadFontMetadata(path);
    }

    [Fact]
    public void BothBackendsReportTheSameFamilyAndStyle()
    {
        var fontFiles = PlatformFontFiles();
        fontFiles.Should().NotBeEmpty("the parity check is meaningless without fonts to compare");

        var skia = new SkiaProbe();
        var imageSharp = new ImageSharpProbe();

        var mismatches = new List<string>();
        var compared = 0;

        foreach (var file in fontFiles)
        {
            FontMetadata fromImageSharp;
            try
            {
                fromImageSharp = imageSharp.Read(file);
            }
            catch
            {
                // A font the reference backend cannot parse is out of scope for this comparison.
                continue;
            }

            FontMetadata fromSkia;
            try
            {
                fromSkia = skia.Read(file);
            }
            catch (Exception ex)
            {
                mismatches.Add($"{file}: Skia backend threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            compared++;

            if (fromSkia.FamilyName != fromImageSharp.FamilyName || fromSkia.Style != fromImageSharp.Style)
                mismatches.Add(
                    $"{file}: skia=({fromSkia.FamilyName}|{fromSkia.Style}) imagesharp=({fromImageSharp.FamilyName}|{fromImageSharp.Style})");
        }

        compared.Should().BeGreaterThan(0);
        mismatches.Should().BeEmpty();
    }

    private static string[] PlatformFontFiles()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            var dir = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Fonts");
            return System.IO.Directory.GetFiles(dir, "*.ttf", System.IO.SearchOption.AllDirectories);
        }

        return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
            ? System.IO.Directory.GetFiles("/Library/Fonts/", "*.ttf", System.IO.SearchOption.AllDirectories)
            : LinuxSystemFontResolver.Resolve();
    }
}
