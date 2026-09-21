#region Copyright
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfPinata.com
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
using System.Collections.Generic;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Internal;

// ReSharper disable RedundantNameQualifier

namespace PdfPinata.Fonts;

/// <summary>
/// Provides functionality to map a fontface request to a physical font.
/// </summary>
internal static class FontFactory
{
    //// Suffix for internal face names to indicate that the font data comes from the platform
    //// and not from the users font resolver.
    //public const string PlatformTag = "platform:";

    /// <summary>
    /// Converts specified information about a required typeface into a specific font.
    /// </summary>
    /// <param name="familyName">Name of the font family.</param>
    /// <param name="fontResolvingOptions">The font resolving options.</param>
    /// <param name="typefaceKey">Typeface key if already known by caller, null otherwise.</param>
    /// <returns>
    /// Information about the typeface, or null if no typeface can be found.
    /// </returns>
    public static FontResolverInfo ResolveTypeface(string familyName, FontResolvingOptions fontResolvingOptions, string typefaceKey)
    {
        if (string.IsNullOrEmpty(typefaceKey))
            typefaceKey = XGlyphTypeface.ComputeKey(familyName, fontResolvingOptions);

        try
        {
            Lock.EnterFontFactory();
            // Was this typeface requested before?
            FontResolverInfo fontResolverInfo;
            if (FontResolverInfosByName.TryGetValue(typefaceKey, out fontResolverInfo))
                return fontResolverInfo;

            // Case: This typeface was not resolved before.

            // The resolver is the only way a typeface is found; the getter throws if none was
            // set, so there is nothing to fall back to and nothing to check for null.
            var customFontResolver = GlobalFontSettings.FontResolver;
            fontResolverInfo = customFontResolver.ResolveTypeface(familyName, fontResolvingOptions.IsBold, fontResolvingOptions.IsItalic);

            // If resolved by custom font resolver register info and font source.
            if (fontResolverInfo != null)
            {
                var resolverInfoKey = fontResolverInfo.Key;
                FontResolverInfo existingFontResolverInfo;
                if (FontResolverInfosByName.TryGetValue(resolverInfoKey, out existingFontResolverInfo))
                {
                    // Case: A new typeface was resolved with the same info as a previous one.
                    // Discard new object an reuse previous one.
                    fontResolverInfo = existingFontResolverInfo;
                    // Associate with typeface key.
                    FontResolverInfosByName.Add(typefaceKey, fontResolverInfo);
                    // The font source should exist.
                    Debug.Assert(FontSourcesByName.ContainsKey(fontResolverInfo.FaceName));
                }
                else
                {
                    // Case: No such font resolver info exists.
                    // Add to both dictionaries.
                    FontResolverInfosByName.Add(typefaceKey, fontResolverInfo);
                    Debug.Assert(resolverInfoKey == fontResolverInfo.Key);
                    FontResolverInfosByName.Add(resolverInfoKey, fontResolverInfo);

                    // Create font source if not yet exists.
                    if (FontSourcesByName.TryGetValue(fontResolverInfo.FaceName, out _))
                    {
                        // Case: The font source exists, because a previous font resolver info comes
                        // with the same face name, but was different in style simulation flags.
                        // Nothing to do.
                    }
                    else
                    {
                        // Case: Get font from custom font resolver and create font source.
                        var bytes = customFontResolver.GetFont(fontResolverInfo.FaceName);
                        var fontSource = XFontSource.GetOrCreateFrom(bytes);

                        // Add font source's font resolver name if it is different to the face name.
                        if (string.Compare(fontResolverInfo.FaceName, fontSource.FontName, StringComparison.OrdinalIgnoreCase) != 0)
                            FontSourcesByName.Add(fontResolverInfo.FaceName, fontSource);
                    }
                }
            }

            // Return value is null if the typeface could not be resolved.
            // In this case PDFsharp stops.
            return fontResolverInfo;
        }
        finally { Lock.ExitFontFactory(); }
    }

    /// <summary>
    /// Gets the bytes of a physical font with specified face name.
    /// </summary>
    public static XFontSource GetFontSourceByFontName(string fontName)
    {
        XFontSource fontSource;
        if (FontSourcesByName.TryGetValue(fontName, out fontSource))
            return fontSource;

        Debug.Assert(false, $"An XFontSource with the name '{fontName}' does not exists.");
        return null;
    }

    /// <summary>
    /// Gets the bytes of a physical font with specified face name.
    /// </summary>
    public static XFontSource GetFontSourceByTypefaceKey(string typefaceKey)
    {
        if (FontSourcesByName.TryGetValue(typefaceKey, out var fontSource))
            return fontSource;

        Debug.Assert(false, $"An XFontSource with the typeface key '{typefaceKey}' does not exists.");
        return null;
    }

    public static bool TryGetFontSourceByKey(ulong key, out XFontSource fontSource)
    {
        return FontSourcesByKey.TryGetValue(key, out fontSource);
    }

    /// <summary>
    /// Gets a value indicating whether at least one font source was created.
    /// </summary>
    public static bool HasFontSources => FontSourcesByName.Count > 0;

    public static bool TryGetFontResolverInfoByTypefaceKey(string typeFaceKey, out FontResolverInfo info)
    {
        return FontResolverInfosByName.TryGetValue(typeFaceKey, out info);
    }

    public static bool TryGetFontSourceByTypefaceKey(string typefaceKey, out XFontSource source)
    {
        return FontSourcesByName.TryGetValue(typefaceKey, out source);
    }

    internal static void CacheFontResolverInfo(string typefaceKey, FontResolverInfo fontResolverInfo)
    {
        // Check whether identical font is already registered.
        if (FontResolverInfosByName.TryGetValue(typefaceKey, out _))
        {
            // Should never come here.
            throw new InvalidOperationException(
                $"A font file with different content already exists with the specified face name '{typefaceKey}'.");
        }
        if (FontResolverInfosByName.TryGetValue(fontResolverInfo.Key, out _))
        {
            // Should never come here.
            throw new InvalidOperationException(
                $"A font resolver already exists with the specified key '{fontResolverInfo.Key}'.");
        }
        // Add to both dictionaries.
        FontResolverInfosByName.Add(typefaceKey, fontResolverInfo);
        FontResolverInfosByName.Add(fontResolverInfo.Key, fontResolverInfo);
    }

    /// <summary>
    /// Caches a font source under its face name and its key.
    /// </summary>
    public static XFontSource CacheFontSource(XFontSource fontSource)
    {
        try
        {
            Lock.EnterFontFactory();
            // Check whether an identical font source with a different face name already exists.
            if (FontSourcesByKey.TryGetValue(fontSource.Key, out var existingFontSource))
            {
                Debug.Assert(existingFontSource.Fontface != null);
                return existingFontSource;
            }

            var fontface = fontSource.Fontface;
            if (fontface == null)
            {
                // Create OpenType fontface for this font source.
                fontSource.Fontface = new OpenTypeFontface(fontSource);
            }
            FontSourcesByKey.Add(fontSource.Key, fontSource);
            FontSourcesByName.Add(fontSource.FontName, fontSource);
            return fontSource;
        }
        finally { Lock.ExitFontFactory(); }
    }

    /// <summary>
    /// Caches a font source under its face name and its key.
    /// </summary>
    public static XFontSource CacheNewFontSource(string typefaceKey, XFontSource fontSource)
    {
        // Check whether an identical font source with a different face name already exists.
        if (FontSourcesByKey.TryGetValue(fontSource.Key, out var existingFontSource))
        {
            return existingFontSource;
        }

        var fontface = fontSource.Fontface;
        if (fontface == null)
        {
            fontface = new OpenTypeFontface(fontSource);
            fontSource.Fontface = fontface;  // Also sets the font name in fontSource
        }

        FontSourcesByName.Add(typefaceKey, fontSource);
        FontSourcesByName.Add(fontSource.FontName, fontSource);
        FontSourcesByKey.Add(fontSource.Key, fontSource);

        return fontSource;
    }

    public static void CacheExistingFontSourceWithNewTypefaceKey(string typefaceKey, XFontSource fontSource)
    {
        try
        {
            Lock.EnterFontFactory();
            FontSourcesByName.Add(typefaceKey, fontSource);
        }
        finally { Lock.ExitFontFactory(); }
    }

    internal static string GetFontCachesState()
    {
        var state = new StringBuilder();

        // FontResolverInfo by name.
        state.Append("====================\n");
        state.Append("Font resolver info by name\n");
        var keyCollection = FontResolverInfosByName.Keys;
        var count = keyCollection.Count;
        var keys = new string[count];
        keyCollection.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
            state.AppendFormat("  {0}: {1}\n", key, FontResolverInfosByName[key].DebuggerDisplay);
        state.Append('\n');

        // FontSource by key.
        state.Append("Font source by key and name\n");
        var fontSourceKeys = FontSourcesByKey.Keys;
        count = fontSourceKeys.Count;
        var ulKeys = new ulong[count];
        fontSourceKeys.CopyTo(ulKeys, 0);
        Array.Sort(ulKeys, (x, y) => x == y ? 0 : (x > y ? 1 : -1));
        foreach (var ul in ulKeys)
            state.AppendFormat("  {0}: {1}\n", ul, FontSourcesByKey[ul].DebuggerDisplay);
        var fontSourceNames = FontSourcesByName.Keys;
        count = fontSourceNames.Count;
        keys = new string[count];
        fontSourceNames.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
            state.AppendFormat("  {0}: {1}\n", key, FontSourcesByName[key].DebuggerDisplay);
        state.Append("--------------------\n\n");

        // FontFamilyInternal by name.
        state.Append(FontFamilyCache.GetCacheState());
        // XGlyphTypeface by name.
        state.Append(GlyphTypefaceCache.GetCacheState());
        // OpenTypeFontface by name.
        state.Append(OpenTypeFontfaceCache.GetCacheState());
        return state.ToString();
    }

    /// <summary>
    /// Maps font typeface key to font resolver info.
    /// </summary>
    static readonly Dictionary<string, FontResolverInfo> FontResolverInfosByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps typeface key or font name to font source.
    /// </summary>
    static readonly Dictionary<string, XFontSource> FontSourcesByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps font source key to font source.
    /// </summary>
    static readonly Dictionary<ulong, XFontSource> FontSourcesByKey = new();
}
