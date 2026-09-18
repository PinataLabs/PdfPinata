using System.Collections.Generic;
using PdfPinata.Drawing;

namespace PdfPinata.Internal;

/// <summary>One font family, and the face that serves each style it ships.</summary>
public class FontFamilyModel
{
    /// <summary>Gets or sets the family name.</summary>
    public string Name { get; set; }

    /// <summary>
    /// Maps each style this family ships a file for to the face name that file is known by.
    /// Face names, not paths: the resolver hands these to callers and looks the path back up
    /// from one, so holding a path here would let the two drift apart.
    /// </summary>
    public Dictionary<XFontStyle, string> FontFiles = new();

    /// <summary>Determines whether this family ships a file for the given style.</summary>
    public bool IsStyleAvailable(XFontStyle fontStyle)
    {
        return FontFiles.ContainsKey(fontStyle);
    }
}
