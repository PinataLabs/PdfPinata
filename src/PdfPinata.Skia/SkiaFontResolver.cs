
namespace PdfPinata.Utils;

/// <summary>
/// Resolves the fonts installed on the current platform.
/// Register it once before any font operation:
/// <c>GlobalFontSettings.FontResolver = new SkiaFontResolver();</c>
/// </summary>
/// <remarks>
/// Family name and style are read from the font file itself rather than through SkiaSharp.
/// SKTypeface.FamilyName returns the typographic (WWS) family, which reports "Arial Narrow"
/// as "Arial" and collapses distinct families together - roughly a quarter of a typical
/// Windows font directory - breaking resolution of any family PdfPinata is asked for by
/// its legacy name.
/// </remarks>
public class SkiaFontResolver
    : FontResolverBase
{

    /// <summary>Reads the family name and style out of a single-font file.</summary>
    protected override FontMetadata ReadFontMetadata(string fontFilePath)
    {
        return OpenTypeFontMetadata.Read(fontFilePath);
    }


    /// <summary>Reads the family name and style of one face of a font file, by index within a collection.</summary>
    protected override FontMetadata ReadFontMetadata(string fontFilePath, int faceIndex)
    {
        return OpenTypeFontMetadata.Read(fontFilePath, faceIndex);
    }


    /// <summary>Reads the family name and style of every face of a collection file, opening it once.</summary>
    protected override FontMetadata[] ReadCollectionMetadata(string fontFilePath, int faceCount)
    {
        return OpenTypeFontMetadata.ReadAll(System.IO.File.ReadAllBytes(fontFilePath), faceCount);
    }
}
