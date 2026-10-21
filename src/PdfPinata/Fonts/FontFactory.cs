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
using System.Collections.Concurrent;
using System.Diagnostics;
using PdfPinata.Drawing;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Internal;

// ReSharper disable RedundantNameQualifier

namespace PdfPinata.Fonts;

/// <summary>
/// Provides functionality to map a fontface request to a physical font.
/// </summary>
/// <remarks>
/// Every cache here is a <see cref="ConcurrentDictionary{TKey,TValue}"/>, because some of its
/// readers take no lock: a plain dictionary read while another thread writes it can throw, answer
/// the wrong entry or never return. Writes are still made under the font factory lock, so that
/// what is resolved and what is cached for it are one step to every other thread.
/// </remarks>
internal static class FontFactory
{
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
            if (FontResolverInfosByName.TryGetValue(typefaceKey, out var fontResolverInfo))
                return fontResolverInfo;

            // Case: This typeface was not resolved before.

            // The resolver is the only way a typeface is found; the getter throws if none was
            // set, so there is nothing to fall back to and nothing to check for null.
            var customFontResolver = GlobalFontSettings.FontResolver;
            fontResolverInfo = customFontResolver.ResolveTypeface(familyName, fontResolvingOptions.IsBold, fontResolvingOptions.IsItalic);

            // If resolved by custom font resolver register info and font source.
            // Return value is null if the typeface could not be resolved.
            // In this case PDFsharp stops.
            return fontResolverInfo == null
                ? null
                : Register(typefaceKey, fontResolverInfo, customFontResolver);
        }
        finally { Lock.ExitFontFactory(); }
    }

    /// <summary>
    /// Registers a typeface the custom font resolver resolved under <paramref name="typefaceKey"/>,
    /// and answers the info to use for it — an existing one where the resolver's answer duplicates it.
    /// </summary>
    /// <remarks>
    /// The font source is cached before either key is, so that a resolver whose <c>GetFont</c>
    /// throws leaves nothing behind. A typeface key registered ahead of its font source used to send
    /// every later request for that typeface to a font source that was never cached.
    /// </remarks>
    private static FontResolverInfo Register(string typefaceKey, FontResolverInfo fontResolverInfo,
        IFontResolver customFontResolver)
    {
        var resolverInfoKey = fontResolverInfo.Key;
        if (FontResolverInfosByName.TryGetValue(resolverInfoKey, out var existingFontResolverInfo))
        {
            // Case: A new typeface was resolved with the same info as a previous one.
            // Discard new object an reuse previous one.
            // Associate with typeface key.
            FontResolverInfosByName.TryAdd(typefaceKey, existingFontResolverInfo);
            // The font source should exist.
            Debug.Assert(FontSourcesByFaceName.ContainsKey(existingFontResolverInfo.FaceName));
            return existingFontResolverInfo;
        }

        // Create font source if not yet exists.
        // Otherwise the font source exists, because a previous font resolver info comes
        // with the same face name, but was different in style simulation flags.
        if (!FontSourcesByFaceName.ContainsKey(fontResolverInfo.FaceName))
            AddFontSource(fontResolverInfo, customFontResolver);

        // Case: No such font resolver info exists.
        FontResolverInfosByName.TryAdd(typefaceKey, fontResolverInfo);
        FontResolverInfosByName.TryAdd(resolverInfoKey, fontResolverInfo);
        return fontResolverInfo;
    }

    /// <summary>
    /// Caches the bytes the resolver serves for a face under that face's name, which is the one
    /// name a font source is looked up by.
    /// </summary>
    /// <remarks>
    /// A font source is not also held under the full name in its own name table. Two files can
    /// share one — two revisions of a font, or a font with a byte of it changed — and a resolver may
    /// serve both, under two face names. Holding them by full name refused the second file with an
    /// <see cref="ArgumentException"/>, and would have handed one face the other's bytes had it
    /// not (#329).
    /// </remarks>
    private static void AddFontSource(FontResolverInfo fontResolverInfo, IFontResolver customFontResolver)
    {
        var bytes = customFontResolver.GetFont(fontResolverInfo.FaceName);
        var fontSource = XFontSource.GetOrCreateFrom(bytes);
        FontSourcesByFaceName.TryAdd(fontResolverInfo.FaceName, fontSource);
    }

    /// <summary>
    /// Gets the font source the resolver served for the face with the specified name.
    /// </summary>
    public static XFontSource GetFontSourceByFontName(string fontName)
    {
        if (FontSourcesByFaceName.TryGetValue(fontName, out var fontSource))
            return fontSource;

        Debug.Assert(false, $"An XFontSource with the name '{fontName}' does not exists.");
        return null;
    }

    public static bool TryGetFontSourceByKey(ulong key, out XFontSource fontSource)
    {
        return FontSourcesByKey.TryGetValue(key, out fontSource);
    }

    /// <summary>
    /// Gets a value indicating whether at least one font source was created.
    /// </summary>
    public static bool HasFontSources => !FontSourcesByKey.IsEmpty;

    public static bool TryGetFontResolverInfoByTypefaceKey(string typeFaceKey, out FontResolverInfo info)
    {
        return FontResolverInfosByName.TryGetValue(typeFaceKey, out info);
    }

    /// <summary>
    /// Caches a font source under its key, or answers the one already cached for the same bytes.
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
            FontSourcesByKey.TryAdd(fontSource.Key, fontSource);
            return fontSource;
        }
        finally { Lock.ExitFontFactory(); }
    }

    /// <summary>
    /// Maps font typeface key to font resolver info.
    /// </summary>
    private static readonly ConcurrentDictionary<string, FontResolverInfo> FontResolverInfosByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a font resolver's face name to the font source it served for that face.
    /// </summary>
    private static readonly ConcurrentDictionary<string, XFontSource> FontSourcesByFaceName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps font source key to font source.
    /// </summary>
    private static readonly ConcurrentDictionary<ulong, XFontSource> FontSourcesByKey = new();
}
