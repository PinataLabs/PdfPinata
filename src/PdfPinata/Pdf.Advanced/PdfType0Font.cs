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
    /// </remarks>
    static bool IsSubsetted(OpenTypeDescriptor descriptor)
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
    static bool HasSubsetPrefix(string baseFont)
    {
        // The getter answers a PDF name, which carries its solidus; a name being assembled here may
        // not have one yet.
        int start = baseFont.Length > 0 && baseFont[0] == '/' ? 1 : 0;

        if (baseFont.Length < start + 7 || baseFont[start + 6] != '+')
            return false;

        for (int index = start; index < start + 6; index++)
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

        OpenTypeDescriptor ttDescriptor = (OpenTypeDescriptor)FontDescriptorCache.GetOrCreateDescriptorFor(font);
        FontDescriptor = new PdfFontDescriptor(document, ttDescriptor);
        _fontOptions = font.PdfOptions;
        Debug.Assert(_fontOptions != null);

        _cmapInfo = new CMapInfo(ttDescriptor);
        _descendantFont = new PdfCIDFont(document, FontDescriptor, font);
        _descendantFont.CMapInfo = _cmapInfo;

        // Create ToUnicode map
        _toUnicode = new PdfToUnicodeMap(document, _cmapInfo);
        document.Internals.AddObject(_toUnicode);
        Elements.Add(Keys.ToUnicode, _toUnicode);

        BaseFont = font.GlyphTypeface.GetBaseName();

        // CID fonts are always embedded, but not always subsetted, and the tag says which. Only
        // TrueType outlines are cut down; CFF ones go in whole because they cannot be subsetted.
        if (IsSubsetted(ttDescriptor))
            BaseFont = CreateEmbeddedFontSubsetName(BaseFont);

        FontDescriptor.FontName = BaseFont;
        _descendantFont.BaseFont = BaseFont;

        PdfArray descendantFonts = new PdfArray(document);
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

        OpenTypeDescriptor ttDescriptor = (OpenTypeDescriptor)FontDescriptorCache.GetOrCreateDescriptor(idName, fontData);
        FontDescriptor = new PdfFontDescriptor(document, ttDescriptor);
        _fontOptions = new XPdfFontOptions(PdfFontEncoding.Unicode);
        Debug.Assert(_fontOptions != null);

        _cmapInfo = new CMapInfo(ttDescriptor);
        _descendantFont = new PdfCIDFont(document, FontDescriptor, fontData);
        _descendantFont.CMapInfo = _cmapInfo;

        // Create ToUnicode map
        _toUnicode = new PdfToUnicodeMap(document, _cmapInfo);
        document.Internals.AddObject(_toUnicode);
        Elements.Add(Keys.ToUnicode, _toUnicode);

        //BaseFont = ttDescriptor.FontName.Replace(" ", "");
        BaseFont = ttDescriptor.FontName;

        // As above, and this constructor asks a second question: it is handed both the bytes and the
        // name by a caller, and a name that already carries a tag came from a font that was already
        // a subset when it arrived. Tagging it twice would say it had been cut down twice.
        if (IsSubsetted(ttDescriptor) && !HasSubsetPrefix(BaseFont))
            BaseFont = CreateEmbeddedFontSubsetName(BaseFont);

        FontDescriptor.FontName = BaseFont;
        _descendantFont.BaseFont = BaseFont;

        PdfArray descendantFonts = new PdfArray(document);
        Owner._irefTable.Add(_descendantFont);
        descendantFonts.Elements.Add(_descendantFont.Reference);
        Elements[Keys.DescendantFonts] = descendantFonts;
    }

    XPdfFontOptions FontOptions => _fontOptions;

    XPdfFontOptions _fontOptions;

    public string BaseFont
    {
        get => Elements.GetName(Keys.BaseFont);
        set => Elements.SetName(Keys.BaseFont, value);
    }

    internal PdfCIDFont DescendantFont => _descendantFont;

    readonly PdfCIDFont _descendantFont;

    internal override void PrepareForSave()
    {
        base.PrepareForSave();

        // Use GetGlyphIndices to create the widths array.
        OpenTypeDescriptor descriptor = (OpenTypeDescriptor)FontDescriptor._descriptor;
        StringBuilder w = new StringBuilder("[");
        if (_cmapInfo != null)
        {
            int[] glyphIndices = _cmapInfo.GetGlyphIndices();
            int count = glyphIndices.Length;
            int[] glyphWidths = new int[count];

            for (int idx = 0; idx < count; idx++)
                glyphWidths[idx] = descriptor.GlyphIndexToPdfWidth(glyphIndices[idx]);

            //TODO: optimize order of indices

            for (int idx = 0; idx < count; idx++)
                w.AppendFormat("{0}[{1}]", glyphIndices[idx], glyphWidths[idx]);
            w.Append(']');
            _descendantFont.Elements.SetValue(PdfCIDFont.Keys.W, new PdfLiteral(w.ToString()));

        }
        _descendantFont.PrepareForSave();
        _toUnicode.PrepareForSave();
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
                if (_meta == null)
                    _meta = CreateMeta(typeof(Keys));
                return _meta;
            }
        }
        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
