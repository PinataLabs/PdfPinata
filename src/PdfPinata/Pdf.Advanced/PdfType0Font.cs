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

using System.Diagnostics;
using System.Globalization;
using System.Text;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a composite font. Used for Unicode encoding.
/// </summary>
internal sealed class PdfType0Font : PdfFont
{
    public PdfType0Font(PdfDocument document)
        : base(document)
    { }

    /// <summary>
    /// Whether the font program about to be embedded will be a subset of the face it came from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The six-letter <c>ABCDEF+</c> tag on a base font name is a statement of fact, not decoration:
    /// ISO 32000-1 9.6.4 says it marks a font program holding only some of the face's glyphs, and the
    /// tag exists so that two subsets of one face, with the same name and different glyphs, cannot be
    /// mistaken for each other. A font embedded whole has nothing to distinguish it from, and wearing
    /// the tag claims something untrue of it — a tool merging documents is entitled to believe two
    /// differently-tagged programs differ, and to keep both.
    /// </para>
    /// <para>
    /// Only TrueType outlines are subsetted here. CFF outlines are embedded entire because this
    /// library cannot cut them down, which is what <see cref="PdfFont.EmbedFontProgram"/> decides on
    /// the same question — so the two have to agree, and both ask the face.
    /// </para>
    /// <para>
    /// A TrueType face can still be embedded whole, when
    /// <see cref="PdfDocumentOptions.RespectFontEmbeddingRestrictions"/> honours its No Subsetting
    /// bit. The option may be set after the font is created, so that is not asked here:
    /// <see cref="PdfFont.EmbedsSubset"/> settles it at save time, and
    /// <see cref="PdfFont.RestoreWholeFontName"/> takes back the tag this gave.
    /// </para>
    /// </remarks>
    private static bool IsSubsetted(OpenTypeDescriptor descriptor)
        => !descriptor.FontFace.IsPostscriptOutlines;

    /// <summary>
    /// Whether a base font name already carries a subset tag: six upper-case letters and a plus,
    /// at the front, after any leading solidus.
    /// </summary>
    /// <remarks>
    /// The shape is what makes it a tag. ISO 32000-1 9.6.4 fixes it at exactly six upper-case ASCII
    /// letters followed by <c>+</c>, and a name is entitled to hold a <c>+</c> anywhere else for
    /// reasons of its own — so looking for the character rather than the shape mistakes such a name
    /// for an already-subsetted font and leaves a real subset untagged. That is the same defect as
    /// tagging a whole font, pointing the other way.
    /// </remarks>
    private static bool HasSubsetPrefix(string baseFont)
    {
        // The getter answers a PDF name, which carries its solidus; a name being assembled here may
        // not have one yet.
        var start = baseFont.Length > 0 && baseFont[0] == '/' ? 1 : 0;

        if (baseFont.Length < start + 7 || baseFont[start + 6] != '+')
            return false;

        for (var index = start; index < start + 6; index++)
        {
            if (baseFont[index] < 'A' || baseFont[index] > 'Z')
                return false;
        }

        return true;
    }

    public PdfType0Font(PdfDocument document, XFont font, bool vertical)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Font");
        Elements.SetName(Keys.Subtype, "/Type0");
        Elements.SetName(Keys.Encoding, vertical ? "/Identity-V" : "/Identity-H");

        var ttDescriptor = (OpenTypeDescriptor)FontDescriptorCache.GetOrCreateDescriptorFor(font);
        FontDescriptor = new PdfFontDescriptor(document, ttDescriptor);
        Debug.Assert(font.PdfOptions != null);

        CmapInfo = new CMapInfo(ttDescriptor);
        _descendantFont = new PdfCIDFont(document, FontDescriptor, font) { CMapInfo = CmapInfo };

        // Create ToUnicode map
        ToUnicode = new PdfToUnicodeMap(document, CmapInfo);
        document.Internals.AddObject(ToUnicode);
        Elements.Add(Keys.ToUnicode, ToUnicode);

        BaseFont = font.GlyphTypeface.GetBaseName();

        // CID fonts are always embedded, but not always subsetted, and the tag says which. Only
        // TrueType outlines are cut down; CFF ones go in whole because they cannot be subsetted.
        // A TrueType face may still go in whole, when the document honours its No Subsetting bit;
        // that is settled at save time, where RestoreWholeFontName takes the tag off again.
        if (IsSubsetted(ttDescriptor))
            BaseFont = TagAsSubset(BaseFont);

        FontDescriptor.FontName = BaseFont;
        _descendantFont.BaseFont = BaseFont;

        var descendantFonts = new PdfArray(document);
        Owner._irefTable.Add(_descendantFont);
        descendantFonts.Elements.Add(_descendantFont.Reference);
        Elements[Keys.DescendantFonts] = descendantFonts;
    }

    public PdfType0Font(PdfDocument document, string idName, byte[] fontData, bool vertical)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Font");
        Elements.SetName(Keys.Subtype, "/Type0");
        Elements.SetName(Keys.Encoding, vertical ? "/Identity-V" : "/Identity-H");

        var ttDescriptor = (OpenTypeDescriptor)FontDescriptorCache.GetOrCreateDescriptor(idName, fontData);
        FontDescriptor = new PdfFontDescriptor(document, ttDescriptor);

        CmapInfo = new CMapInfo(ttDescriptor);
        _descendantFont = new PdfCIDFont(document, FontDescriptor) { CMapInfo = CmapInfo };

        // Create ToUnicode map
        ToUnicode = new PdfToUnicodeMap(document, CmapInfo);
        document.Internals.AddObject(ToUnicode);
        Elements.Add(Keys.ToUnicode, ToUnicode);

        BaseFont = ttDescriptor.FontName;

        // As above, and this constructor asks a second question: it is handed both the bytes and the
        // name by a caller, and a name that already carries a tag came from a font that was already
        // a subset when it arrived. Tagging it twice would say it had been cut down twice.
        if (IsSubsetted(ttDescriptor) && !HasSubsetPrefix(BaseFont))
            BaseFont = TagAsSubset(BaseFont);

        FontDescriptor.FontName = BaseFont;
        _descendantFont.BaseFont = BaseFont;

        var descendantFonts = new PdfArray(document);
        Owner._irefTable.Add(_descendantFont);
        descendantFonts.Elements.Add(_descendantFont.Reference);
        Elements[Keys.DescendantFonts] = descendantFonts;
    }

    public string BaseFont
    {
        get => Elements.GetName(Keys.BaseFont);
        set => Elements.SetName(Keys.BaseFont, value);
    }

    internal PdfCIDFont DescendantFont => _descendantFont;

    private readonly PdfCIDFont _descendantFont;

    internal override void PrepareForSave()
    {
        base.PrepareForSave();

        RestoreWholeFontName(name =>
        {
            BaseFont = name;
            _descendantFont.BaseFont = name;
        });

        // Use GetGlyphIndices to create the widths array.
        var descriptor = FontDescriptor.Descriptor;
        var w = new StringBuilder("[");
        if (CmapInfo != null)
        {
            // The indices come back sorted, so each run of consecutive CIDs is adjacent and can be
            // written as one "c [w1 w2 ...]" entry (ISO 32000-1 9.7.4.3) rather than one per glyph.
            var glyphIndices = CmapInfo.GetGlyphIndices();
            for (var idx = 0; idx < glyphIndices.Length; idx++)
            {
                var cid = glyphIndices[idx];
                if (idx > 0 && cid == glyphIndices[idx - 1] + 1)
                {
                    w.Append(' ');
                }
                else
                {
                    if (idx > 0)
                        w.Append(']');
                    w.Append(cid.ToString(CultureInfo.InvariantCulture)).Append('[');
                }
                w.Append(descriptor.GlyphIndexToPdfWidth(cid).ToString(CultureInfo.InvariantCulture));
            }
            if (glyphIndices.Length > 0)
                w.Append(']');
            w.Append(']');
            _descendantFont.Elements.SetValue(PdfCIDFont.Keys.W, new PdfLiteral(w.ToString()));

        }
        _descendantFont.PrepareForSave();
        ToUnicode.PrepareForSave();
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public new sealed class Keys : PdfFont.Keys
    {
        /// <summary>
        /// (Required) The type of PDF object that this dictionary describes;
        /// must be Font for a font dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "Font")]
        public new const string Type = "/Type";

        /// <summary>
        /// (Required) The type of font; must be Type0 for a Type 0 font.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public new const string Subtype = "/Subtype";

        /// <summary>
        /// (Required) The PostScript name of the font. In principle, this is an arbitrary
        /// name, since there is no font program associated directly with a Type 0 font
        /// dictionary. The conventions described here ensure maximum compatibility
        /// with existing Acrobat products.
        /// If the descendant is a Type 0 CIDFont, this name should be the concatenation
        /// of the CIDFont’s BaseFont name, a hyphen, and the CMap name given in the
        /// Encoding entry (or the CMapName entry in the CMap). If the descendant is a
        /// Type 2 CIDFont, this name should be the same as the CIDFont’s BaseFont name.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public new const string BaseFont = "/BaseFont";

        /// <summary>
        /// (Required) The name of a predefined CMap, or a stream containing a CMap
        /// that maps character codes to font numbers and CIDs. If the descendant is a
        /// Type 2 CIDFont whose associated TrueType font program is not embedded
        /// in the PDF file, the Encoding entry must be a predefined CMap name.
        /// </summary>
        [KeyInfo(KeyType.StreamOrName | KeyType.Required)]
        public const string Encoding = "/Encoding";

        /// <summary>
        /// (Required) A one-element array specifying the CIDFont dictionary that is the
        /// descendant of this Type 0 font.
        /// </summary>
        [KeyInfo(KeyType.Array | KeyType.Required)]
        public const string DescendantFonts = "/DescendantFonts";

        /// <summary>
        /// ((Optional) A stream containing a CMap file that maps character codes to
        /// Unicode values.
        /// </summary>
        [KeyInfo(KeyType.Stream | KeyType.Optional)]
        public const string ToUnicode = "/ToUnicode";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta
        {
            get
            {
                _meta ??= CreateMeta(typeof(Keys));
                return _meta;
            }
        }
        private static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
