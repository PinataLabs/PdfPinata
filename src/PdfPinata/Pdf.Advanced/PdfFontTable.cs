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
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;

namespace PdfPinata.Pdf.Advanced;

internal enum FontType
{
    /// <summary>
    /// TrueType with WinAnsi encoding.
    /// </summary>
    TrueType = 1,

    /// <summary>
    /// TrueType with Identity-H or Identity-V encoding (unicode).
    /// </summary>
    Type0 = 2
}

/// <summary>
/// Contains all used fonts of a document.
/// </summary>
internal sealed class PdfFontTable : PdfResourceTable
{
    /// <summary>
    /// Initializes a new instance of this class, which is a singleton for each document.
    /// </summary>
    public PdfFontTable(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Gets a PdfFont from an XFont. If no PdfFont already exists, a new one is created.
    /// </summary>
    public PdfFont GetFont(XFont font)
    {
        var selector = font.Selector;
        if (selector == null)
        {
            selector = ComputeKey(font); //new FontSelector(font);
            font.Selector = selector;
        }
        if (!_fonts.TryGetValue(selector, out var pdfFont))
        {
            // Refused before anything is added to the document, so that a font the document will
            // not carry leaves nothing of itself behind, and the exception comes from the call that
            // drew with it.
            if (Owner.Options.RespectFontEmbeddingRestrictions)
                FontEmbeddingPermissions.EnsureEmbeddable(font.GlyphTypeface.Fontface);

            if (font.Unicode)
                pdfFont = new PdfType0Font(Owner, font, font.IsVertical);
            else
                pdfFont = new PdfTrueTypeFont(Owner, font);
            Debug.Assert(pdfFont.Owner == Owner);
            _fonts[selector] = pdfFont;
        }
        return pdfFont;
    }

    /// <summary>
    /// Gets a PdfFont from a font program. If no PdfFont already exists, a new one is created.
    /// </summary>
    public PdfFont GetFont(string idName, byte[] fontData)
    {
        if (idName == null)
            throw new ArgumentNullException(nameof(idName));
        if (fontData == null)
            throw new ArgumentNullException(nameof(fontData));

        var selector = ComputeKey(idName);
        if (!_fonts.TryGetValue(selector, out var pdfFont))
        {
            if (Owner.Options.RespectFontEmbeddingRestrictions)
            {
                var descriptor = (OpenTypeDescriptor)FontDescriptorCache.GetOrCreateDescriptor(idName, fontData);
                FontEmbeddingPermissions.EnsureEmbeddable(descriptor.FontFace);
            }

            pdfFont = new PdfType0Font(Owner, idName, fontData, false);
            Debug.Assert(pdfFont.Owner == Owner);
            _fonts[selector] = pdfFont;
        }
        return pdfFont;
    }

    /// <summary>
    /// Tries to get a PdfFont from the font dictionary.
    /// Returns null if no such PdfFont exists.
    /// </summary>
    public PdfFont TryGetFont(string idName)
    {
        if (idName == null)
            throw new ArgumentNullException(nameof(idName));

        _fonts.TryGetValue(ComputeKey(idName), out var pdfFont);
        return pdfFont;
    }

    internal static string ComputeKey(XFont font)
    {
        var glyphTypeface = font.GlyphTypeface;
        var key = glyphTypeface.Fontface.FullFaceName.ToLowerInvariant() +
                  (glyphTypeface.IsBold ? "/b" : "") + (glyphTypeface.IsItalic ? "/i" : "") + font.Unicode;
        return key;
    }

    /// <summary>
    /// The key a font embedded from a font program is held under. It is the caller's own name for the
    /// program and nothing else, so that <see cref="TryGetFont"/> finds again what
    /// <see cref="GetFont(string, byte[])"/> put there: both are handed the name alone, and a key
    /// drawn from the bytes as well could not be recomputed from it. The prefix keeps it clear of the
    /// keys <see cref="ComputeKey(XFont)"/> makes, so a program named after an installed face cannot
    /// be answered in place of the face.
    /// </summary>
    private static string ComputeKey(string idName) => "program:" + idName;

    /// <summary>
    /// Map from PdfFontSelector to PdfFont.
    /// </summary>
    private readonly Dictionary<string, PdfFont> _fonts = new();

    public void PrepareForSave()
    {
        // Every font is asked before any is prepared, so that a refusal leaves the fonts as they
        // were. The option may have been set after the fonts were drawn with, which is why the
        // question is asked here as well as in GetFont.
        foreach (var font in _fonts.Values)
            font.EnsureEmbeddingPermitted();

        foreach (var font in _fonts.Values)
            font.PrepareForSave();
    }
}
