
using PdfPinata.Drawing;
using SixLabors.Fonts;


namespace PdfPinata.Utils;

/// <summary>
/// Resolves the fonts installed on the current platform, reading family name and style with SixLabors.Fonts.
/// Register it once before any font operation:
/// <c>GlobalFontSettings.FontResolver = new ImageSharpFontResolver();</c>
/// </summary>
public class ImageSharpFontResolver
    : FontResolverBase
{

    /// <summary>Reads the family name and style out of a single-font file.</summary>
    protected override FontMetadata ReadFontMetadata(string fontFilePath)
    {
        return ToMetadata(FontDescription.LoadDescription(fontFilePath));
    }


    /// <summary>Reads the family name and style of one face of a font file, by index within a collection.</summary>
    protected override FontMetadata ReadFontMetadata(string fontFilePath, int faceIndex)
    {
        if (faceIndex < 0)
            return ReadFontMetadata(fontFilePath);

        // LoadDescription reads one font and cannot say which; a collection has to go through
        // the collection reader even for its first face.
        var descriptions = FontDescription.LoadFontCollectionDescriptions(fontFilePath);
        if (faceIndex >= descriptions.Length)
            throw new System.InvalidOperationException(
                "Font collection holds " + descriptions.Length + " faces; face " + faceIndex + " was asked for.");

        return ToMetadata(descriptions[faceIndex]);
    }


    /// <summary>Reads the family name and style of every face of a collection file, opening it once.</summary>
    protected override FontMetadata[] ReadCollectionMetadata(string fontFilePath, int faceCount)
    {
        var descriptions = FontDescription.LoadFontCollectionDescriptions(fontFilePath);
        if (descriptions.Length < faceCount)
            throw new System.InvalidOperationException(
                "Font collection holds " + descriptions.Length + " faces; " + faceCount + " were expected.");

        var metadata = new FontMetadata[faceCount];
        for (var face = 0; face < faceCount; face++)
            metadata[face] = ToMetadata(descriptions[face]);

        return metadata;
    }


    private static FontMetadata ToMetadata(FontDescription fontDescription)
    {
        var style = fontDescription.Style switch
        {
            FontStyle.Bold => XFontStyle.Bold,
            FontStyle.Italic => XFontStyle.Italic,
            FontStyle.BoldItalic => XFontStyle.BoldItalic,
            _ => XFontStyle.Regular
        };

        return new FontMetadata(fontDescription.FontFamilyInvariantCulture, style);
    }
}
