#region Copyright

//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Diagnostics;
using System.Globalization;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;

namespace PdfPinata.Drawing;

/// <summary>
/// Specifies a physical font face that corresponds to a font file on the disk or in memory.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
internal sealed class XGlyphTypeface
{
    // Implementation Notes
    // XGlyphTypeface is the centerpiece for font management. There is a one-to-one relationship
    // between XFont an XGlyphTypeface.
    //
    // * Each XGlyphTypeface can belong to one or more XFont objects.
    // * An XGlyphTypeface hold an XFontFamily.
    // * XGlyphTypeface hold a reference to an OpenTypeFontface.
    // *
    //

    private const string _keyPrefix = "tk:"; // "typeface key"

    /// <summary>
    /// Initializes a typeface for a face already read from a font file.
    /// </summary>
    /// <param name="key">The cache key this typeface is held under.</param>
    /// <param name="fontSource">The bytes of the font file, and the face read out of them.</param>
    /// <param name="styleSimulations">
    /// What the renderer has to supply that the font file does not - a font resolver asked for
    /// a bold or italic face the family ships no file for, and named the nearest one instead.
    /// </param>
    public XGlyphTypeface(string key, XFontSource fontSource, XStyleSimulations styleSimulations)
    {
        var familyName = fontSource.Fontface.name.Name;
        FontFamily = new XFontFamily(familyName, false);
        _fontface = fontSource.Fontface;
        IsBold = _fontface.os2.IsBold;
        IsItalic = _fontface.os2.IsItalic;
        StyleSimulations = styleSimulations;

        Key = key;
        FontSource = fontSource;

        Initialize();
    }

    // ReSharper disable once UnusedMember.Global
    public XGlyphTypeface(string key, XFontFamily fontFamily, XFontSource fontSource,
        XStyleSimulations styleSimulations)
    {
        Key = key;
        FontFamily = fontFamily;
        FontSource = fontSource;
        StyleSimulations = styleSimulations;
        _fontface = OpenTypeFontface.CetOrCreateFrom(fontSource);

        Initialize();
    }

    public static XGlyphTypeface GetOrCreateFrom(string familyName, FontResolvingOptions fontResolvingOptions)
    {
        // Check cache for requested type face.
        var typefaceKey = ComputeKey(familyName, fontResolvingOptions);
        if (GlyphTypefaceCache.TryGetGlyphTypeface(typefaceKey, out var glyphTypeface))
        {
            // Just return existing one.
            return glyphTypeface;
        }

        // Resolve typeface by FontFactory.
        var fontResolverInfo = FontFactory.ResolveTypeface(familyName, fontResolvingOptions, typefaceKey);
        if (fontResolverInfo == null)
        {
            // No fallback - just stop.
            throw new InvalidOperationException("No appropriate font found.");
        }

        // Create new and exclusively used font family for custom font resolver retrieved font
        // source. The result is dropped on purpose: the typeface builds its own family below,
        // from the name inside the font file. This call is here for what it leaves behind in
        // FontFamilyCache, so that a resolver's face names cannot clash with family names.
        XFontFamily.CreateSolitary(fontResolverInfo.FaceName);

        // We have a valid font resolver info. That means we also have an XFontSource object loaded in the cache.
        var fontSource = FontFactory.GetFontSourceByFontName(fontResolverInfo.FaceName);
        Debug.Assert(fontSource != null);

        // Each font source already contains its OpenTypeFontface. The resolver's simulation
        // flags have to come along: they are the whole record of the difference between the
        // face that was asked for and the file that answered, and the renderer draws from them.
        glyphTypeface = new XGlyphTypeface(typefaceKey, fontSource, fontResolverInfo.StyleSimulations);
        GlyphTypefaceCache.AddGlyphTypeface(glyphTypeface);

        return glyphTypeface;
    }

    public XFontFamily FontFamily { get; }

    internal OpenTypeFontface Fontface => _fontface;

    private readonly OpenTypeFontface _fontface;

    public XFontSource FontSource { get; }


    private void Initialize()
    {
        FamilyName = _fontface.name.Name;
        if (string.IsNullOrEmpty(FaceName) || FaceName.StartsWith('?'))
            FaceName = FamilyName;
        StyleName = _fontface.name.Style;
        DisplayName = _fontface.name.FullFontName;
        if (string.IsNullOrEmpty(DisplayName))
        {
            DisplayName = FamilyName;
            if (string.IsNullOrEmpty(StyleName))
                DisplayName += " (" + StyleName + ")";
        }

        // Bold, as defined in OS/2 table.
        IsBold = _fontface.os2.IsBold;

        // Italic, as defined in OS/2 table.
        IsItalic = _fontface.os2.IsItalic;
    }

    /// <summary>
    /// Gets the name of the font face. This can be a file name, a uri, or a GUID.
    /// </summary>
    internal string FaceName { get; private set; }

    /// <summary>
    /// Gets the English family name of the font, for example "Arial".
    /// </summary>
    public string FamilyName { get; private set; }

    /// <summary>
    /// Gets the English subfamily name of the font,
    /// for example "Bold".
    /// </summary>
    public string StyleName { get; private set; }

    /// <summary>
    /// Gets the English display name of the font,
    /// for example "Arial italic".
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the font weight is bold.
    /// </summary>
    public bool IsBold { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the font style is italic.
    /// </summary>
    public bool IsItalic { get; private set; }

    public XStyleSimulations StyleSimulations { get; }

    /// <summary>
    /// Gets the suffix of the face name in a PDF font and font descriptor.
    /// The name based on the effective value of bold and italic from the OS/2 table.
    /// </summary>
    private string GetFaceNameSuffix()
    {
        // Use naming of Microsoft Word.
        if (IsBold)
            return IsItalic ? ",BoldItalic" : ",Bold";
        return IsItalic ? ",Italic" : "";
    }

    internal string GetBaseName()
    {
        var name = DisplayName;
        var ich = name.IndexOf("bold", StringComparison.OrdinalIgnoreCase);
        if (ich > 0)
            name = name.Remove(ich, 4);
        ich = name.IndexOf("italic", StringComparison.OrdinalIgnoreCase);
        if (ich > 0)
            name = name.Remove(ich, 6);
        name = name.Trim();
        name += GetFaceNameSuffix();
        return name;
    }

    /// <summary>
    /// Computes the bijective key for a typeface.
    /// </summary>
    internal static string ComputeKey(string familyName, FontResolvingOptions fontResolvingOptions)
    {
        // Compute a human-readable key.
        var simulationSuffix = "";
        if (fontResolvingOptions.OverrideStyleSimulations)
        {
            switch (fontResolvingOptions.StyleSimulations)
            {
                case XStyleSimulations.BoldSimulation: simulationSuffix = "|b+/i-"; break;
                case XStyleSimulations.ItalicSimulation: simulationSuffix = "|b-/i+"; break;
                case XStyleSimulations.BoldItalicSimulation: simulationSuffix = "|b+/i+"; break;
                case XStyleSimulations.None: break;
                default: throw new ArgumentOutOfRangeException(nameof(fontResolvingOptions));
            }
        }

        var key = _keyPrefix + familyName.ToLowerInvariant()
                             + (fontResolvingOptions.IsItalic ? "/i" : "/n") // normal / oblique / italic
                             + (fontResolvingOptions.IsBold ? "/700" : "/400") + "/5" // Stretch.Normal
                             + simulationSuffix;
        return key;
    }

    /// <summary>
    /// Computes the bijective key for a typeface.
    /// </summary>
    internal static string ComputeKey(string familyName, bool isBold, bool isItalic)
    {
        return ComputeKey(familyName, new FontResolvingOptions(FontHelper.CreateStyle(isBold, isItalic)));
    }

    public string Key { get; }

    /// <summary>
    /// Gets the DebuggerDisplayAttribute text.
    /// </summary>
    // ReSharper disable UnusedMember.Local
    internal string DebuggerDisplay =>
        string.Format(CultureInfo.InvariantCulture, "{0} - {1} ({2})", FamilyName, StyleName,
            FaceName); // ReSharper restore UnusedMember.Local
}
