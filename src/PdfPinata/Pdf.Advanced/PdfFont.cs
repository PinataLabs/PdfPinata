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
        if (CmapInfo != null)
            CmapInfo.AddChars(text);
    }

    internal void AddShapedRun(ShapedRun run, string text)
    {
        if (CmapInfo != null)
            CmapInfo.AddShapedRun(run, text);
    }

    internal void AddGlyphIndices(string glyphIndices)
    {
        if (CmapInfo != null)
            CmapInfo.AddGlyphIndices(glyphIndices);
    }

    /// <summary>
    /// Gets or sets the CMapInfo.
    /// </summary>
    internal CMapInfo CMapInfo
    {
        get => CmapInfo;
        set => CmapInfo = value;
    }
    internal CMapInfo CmapInfo;

    /// <summary>
    /// Gets or sets ToUnicodeMap.
    /// </summary>
    internal PdfToUnicodeMap ToUnicodeMap
    {
        get => ToUnicode;
        set => ToUnicode = value;
    }
    internal PdfToUnicodeMap ToUnicode;

    /// <summary>
    /// The base font name without and with the subset tag the constructor gave it, both null when
    /// it gave it none.
    /// </summary>
    /// <remarks>
    /// Both are kept so that <see cref="RestoreWholeFontName"/> can choose between them at every
    /// save: whether the program is a subset is settled then, by <see cref="EmbedsSubset"/>, and
    /// the option it reads may change between two saves of one document.
    /// </remarks>
    string _untaggedBaseFont, _taggedBaseFont;

    /// <summary>
    /// Gives <paramref name="name"/> a subset tag, remembering both spellings.
    /// </summary>
    internal string TagAsSubset(string name)
    {
        _untaggedBaseFont = name;
        _taggedBaseFont = CreateEmbeddedFontSubsetName(name);
        return _taggedBaseFont;
    }

    /// <summary>
    /// Whether the font program this font embeds is a subset of its face rather than the whole of it.
    /// </summary>
    /// <remarks>
    /// Two things embed a face whole. PostScript (CFF) outlines cannot be subsetted here at all, and
    /// a face whose <c>fsType</c> says No Subsetting may not be once the document has been asked,
    /// through <see cref="PdfDocumentOptions.RespectFontEmbeddingRestrictions"/>, to honour what its
    /// fonts say. Everything that depends on the answer — the program written, the subset tag on the
    /// name, PDF/A-1's <c>/CIDSet</c> — asks here, so none of them can disagree.
    /// </remarks>
    internal bool EmbedsSubset
    {
        get
        {
            var fontFace = FontDescriptor.Descriptor.FontFace;
            if (fontFace.IsPostscriptOutlines)
                return false;

            return !(Owner.Options.RespectFontEmbeddingRestrictions
                     && FontEmbeddingPermissions.Of(fontFace).ForbidsSubsetting);
        }
    }

    /// <summary>
    /// Refuses the font when the document honours embedding restrictions and the face's licence
    /// forbids embedding it.
    /// </summary>
    internal void EnsureEmbeddingPermitted()
    {
        if (Owner.Options.RespectFontEmbeddingRestrictions)
            FontEmbeddingPermissions.EnsureEmbeddable(FontDescriptor.Descriptor.FontFace);
    }

    /// <summary>
    /// Takes off the subset tag the constructor added when the program is embedded whole, and puts
    /// it back when it is a subset again. ISO 32000-1 9.6.4 reserves the tag for a program holding
    /// only some of the face's glyphs, so wearing it on a whole font says something untrue.
    /// </summary>
    /// <param name="setBaseFont">Writes the name wherever this font carries it.</param>
    internal void RestoreWholeFontName(Action<string> setBaseFont)
    {
        if (_untaggedBaseFont == null)
            return;

        var name = EmbedsSubset ? _taggedBaseFont : _untaggedBaseFont;
        setBaseFont(name);
        FontDescriptor.FontName = name;
    }

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
    /// as one. A TrueType face that may not be subsetted, as <see cref="EmbedsSubset"/> decides,
    /// goes into '/FontFile2' whole: its glyph indices are the ones the document already uses.
    /// </remarks>
    internal void EmbedFontProgram(bool cidFont)
    {
        var fontFace = FontDescriptor.Descriptor.FontFace;
        var postscriptOutlines = fontFace.IsPostscriptOutlines;

        var fontData = EmbedsSubset
            ? fontFace.CreateFontSubSet(CmapInfo.GlyphIndices, cidFont).FontSource.Bytes
            : fontFace.FontSource.Bytes;

        var fontStream = new PdfDictionary(Owner);
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
        var s = new StringBuilder(64);
        var bytes = Guid.NewGuid().ToByteArray();
        for (var idx = 0; idx < 6; idx++)
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
