#region PDFsharp - A .NET library for processing PDF
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
using System.Text;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a PDF font.
/// </summary>
public class PdfFont : PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFont"/> class.
    /// </summary>
    public PdfFont(PdfDocument document)
        : base(document)
    { }

    internal PdfFontDescriptor FontDescriptor
    {
        get
        {
            Debug.Assert(_fontDescriptor != null);
            return _fontDescriptor;
        }
        set => _fontDescriptor = value;
    }
    PdfFontDescriptor _fontDescriptor;

    internal PdfFontEncoding FontEncoding;

    /// <summary>
    /// Gets a value indicating whether this instance is symbol font.
    /// </summary>
    public bool IsSymbolFont => _fontDescriptor.IsSymbolFont;

    internal void AddChars(string text)
    {
        if (_cmapInfo != null)
            _cmapInfo.AddChars(text);
    }

    internal void AddShapedRun(Fonts.ShapedRun run, string text)
    {
        if (_cmapInfo != null)
            _cmapInfo.AddShapedRun(run, text);
    }

    internal void AddGlyphIndices(string glyphIndices)
    {
        if (_cmapInfo != null)
            _cmapInfo.AddGlyphIndices(glyphIndices);
    }

    /// <summary>
    /// Gets or sets the CMapInfo.
    /// </summary>
    internal CMapInfo CMapInfo
    {
        get => _cmapInfo;
        set => _cmapInfo = value;
    }
    internal CMapInfo _cmapInfo;

    /// <summary>
    /// Gets or sets ToUnicodeMap.
    /// </summary>
    internal PdfToUnicodeMap ToUnicodeMap
    {
        get => _toUnicode;
        set => _toUnicode = value;
    }
    internal PdfToUnicodeMap _toUnicode;


    /// <summary>
    /// Writes the font program into the document and points the font descriptor at it.
    /// Fonts are always embedded, so every derived font calls this when it is saved.
    /// </summary>
    /// <param name="cidFont">Whether the program is being embedded for a CID font.</param>
    /// <remarks>
    /// A TrueType font is subsetted down to the glyphs the document draws and embedded as
    /// '/FontFile2'. A font with PostScript (CFF) outlines cannot be subsetted - that would
    /// mean rebuilding its charstrings and subroutines - so it is embedded whole as
    /// '/FontFile3' with a subtype of '/OpenType'. '/FontFile2' would be a misdescription:
    /// the key is defined as a TrueType font program, and a viewer is entitled to read it
    /// as one.
    /// </remarks>
    internal void EmbedFontProgram(bool cidFont)
    {
        OpenTypeFontface fontFace = FontDescriptor._descriptor.FontFace;
        bool postscriptOutlines = fontFace.IsPostscriptOutlines;

        byte[] fontData = postscriptOutlines
            ? fontFace.FontSource.Bytes
            : fontFace.CreateFontSubSet(_cmapInfo.GlyphIndices, cidFont).FontSource.Bytes;

        PdfDictionary fontStream = new PdfDictionary(Owner);
        Owner.Internals.AddObject(fontStream);

        if (postscriptOutlines)
        {
            FontDescriptor.Elements[PdfFontDescriptor.Keys.FontFile3] = fontStream.Reference;

            // '/Subtype' is what tells the viewer which program this is, and it takes the place
            // of the '/Length1' that a '/FontFile2' carries.
            fontStream.Elements["/Subtype"] = new PdfName("/OpenType");

            // '/Subtype /OpenType' arrives in PDF 1.6. Raising the version is the honest thing
            // to do; lowering it is not this method's business.
            PdfVersionRequirements.Require(Owner, 16);
        }
        else
        {
            FontDescriptor.Elements[PdfFontDescriptor.Keys.FontFile2] = fontStream.Reference;
            fontStream.Elements["/Length1"] = new PdfInteger(fontData.Length);
        }

        if (!Owner.Options.NoCompression)
        {
            fontData = Filters.Filtering.FlateDecode.Encode(fontData, Owner.Options.FlateEncodeMode);
            fontStream.Elements["/Filter"] = new PdfName("/FlateDecode");
        }

        fontStream.Elements["/Length"] = new PdfInteger(fontData.Length);
        fontStream.CreateStream(fontData);
    }


    /// <summary>
    /// Adds a tag of exactly six uppercase letters to the font name
    /// according to PDF Reference Section 5.5.3 'Font Subsets'
    /// </summary>
    internal static string CreateEmbeddedFontSubsetName(string name)
    {
        StringBuilder s = new StringBuilder(64);
        byte[] bytes = Guid.NewGuid().ToByteArray();
        for (int idx = 0; idx < 6; idx++)
            s.Append((char)('A' + bytes[idx] % 26));
        s.Append('+');
        if (name.StartsWith('/'))
            s.Append(name, 1, name.Length - 1);
        else
            s.Append(name);
        return s.ToString();
    }

    /// <summary>
    /// Predefined keys common to all font dictionaries.
    /// </summary>
    public class Keys : KeysBase
    {
        /// <summary>
        /// (Required) The type of PDF object that this dictionary describes;
        /// must be Font for a font dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "Font")]
        public const string Type = "/Type";

        /// <summary>
        /// (Required) The type of font.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string Subtype = "/Subtype";

        /// <summary>
        /// (Required) The PostScript name of the font.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public const string BaseFont = "/BaseFont";

        /// <summary>
        /// (Required except for the standard 14 fonts; must be an indirect reference)
        /// A font descriptor describing the font�s metrics other than its glyph widths.
        /// Note: For the standard 14 fonts, the entries FirstChar, LastChar, Widths, and 
        /// FontDescriptor must either all be present or all be absent. Ordinarily, they are
        /// absent; specifying them enables a standard font to be overridden.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.MustBeIndirect, typeof(PdfFontDescriptor))]
        public const string FontDescriptor = "/FontDescriptor";
    }
}
