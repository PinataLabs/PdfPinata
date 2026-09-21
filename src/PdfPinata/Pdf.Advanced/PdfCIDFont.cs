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

using PdfPinata.Drawing;
using PdfPinata.Pdf.Metadata;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Represents a CIDFont dictionary.
/// </summary>
internal class PdfCIDFont : PdfFont
{
    public PdfCIDFont(PdfDocument document)
        : base(document)
    { }

    public PdfCIDFont(PdfDocument document, PdfFontDescriptor fontDescriptor, XFont font)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Font");
        Elements.SetName(Keys.Subtype, "/CIDFontType2");
        var cid = new PdfDictionary();
        cid.Elements.SetString("/Ordering", "Identity");
        cid.Elements.SetString("/Registry", "Adobe");
        cid.Elements.SetInteger("/Supplement", 0);
        Elements.SetValue(Keys.CIDSystemInfo, cid);

        FontDescriptor = fontDescriptor;
        // ReSharper disable once DoNotCallOverridableMethodsInConstructor
        Owner._irefTable.Add(fontDescriptor);
        Elements[Keys.FontDescriptor] = fontDescriptor.Reference;

        FontEncoding = font.PdfOptions.FontEncoding;
    }

    public PdfCIDFont(PdfDocument document, PdfFontDescriptor fontDescriptor)
        : base(document)
    {
        Elements.SetName(Keys.Type, "/Font");
        Elements.SetName(Keys.Subtype, "/CIDFontType2");
        var cid = new PdfDictionary();
        cid.Elements.SetString("/Ordering", "Identity");
        cid.Elements.SetString("/Registry", "Adobe");
        cid.Elements.SetInteger("/Supplement", 0);
        Elements.SetValue(Keys.CIDSystemInfo, cid);

        FontDescriptor = fontDescriptor;
        // ReSharper disable once DoNotCallOverridableMethodsInConstructor
        Owner._irefTable.Add(fontDescriptor);
        Elements[Keys.FontDescriptor] = fontDescriptor.Reference;

        FontEncoding = PdfFontEncoding.Unicode;
    }

    public string BaseFont
    {
        get => Elements.GetName(Keys.BaseFont);
        set => Elements.SetName(Keys.BaseFont, value);
    }

    /// <summary>
    /// Prepares the object to get saved.
    /// </summary>
    internal override void PrepareForSave()
    {
        base.PrepareForSave();

        // The subtype cannot be settled in the constructor: which outlines the font has is only
        // known once its face has been read. CIDFontType2 means glyf outlines, CIDFontType0
        // means CFF ones, and a viewer reading the program is entitled to be told which.
        var postscriptOutlines = FontDescriptor.Descriptor.FontFace.IsPostscriptOutlines;
        Elements.SetName(Keys.Subtype, postscriptOutlines ? "/CIDFontType0" : "/CIDFontType2");

        if (postscriptOutlines)
        {
            // Meaningless for CFF outlines, where the font program maps CIDs to glyphs itself, and
            // ISO 32000-1 Table 117 allows the entry on a Type 2 CIDFont only.
            Elements.Remove(Keys.CIDToGIDMap);
        }
        else
        {
            // Identity, and said out loud rather than left to the default. This library writes the
            // glyph indices themselves as the character codes — that is what Identity-H encoding
            // means here — so a CID *is* a glyph index and no mapping stream is called for. ISO
            // 32000-1 makes the entry optional with exactly this default, but PDF/A and PDF/UA both
            // require it to be present: a reader must not have to know the default to know which
            // glyph a code draws. veraPDF fails a document without it under PDF/A-1 6.3.3.2,
            // PDF/A-2 and PDF/A-3 6.2.11.3.2, and PDF/UA-1 7.21.3.2 alike.
            Elements.SetName(Keys.CIDToGIDMap, "/Identity");
        }

        // CID fonts must always be embedded. A font with CFF outlines is embedded whole,
        // because it cannot be subsetted, and so is a TrueType face whose licence forbids
        // subsetting when the document honours it; other TrueType outlines are subsetted first.
        EmbedFontProgram(true);

        // PDF/A-1 asks a /CIDSet of a subset alone, and one listing only the glyphs drawn would
        // misdescribe a program holding all of them.
        if (EmbedsSubset && PdfConformanceWriter.RequiresCidSet(Owner.Options.Conformance))
            EmbedCidSet();
        else
            FontDescriptor.Elements.Remove(PdfFontDescriptor.Keys.CIDSet);
    }

    /// <summary>
    /// Writes the descriptor's <c>/CIDSet</c>: a bit per CID, set when the subset holds it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PDF/A-1 clause 6.3.5 asks every subset CIDFont for one, and every CID font this library
    /// writes is a subset by name — the base font is always given the six-letter tag. PDF/A-2
    /// dropped the requirement as redundant, which is why this is written for PDF/A-1 alone rather
    /// than for every document: it is bytes in the file that only one profile has a use for, and
    /// nothing else reads it.
    /// </para>
    /// <para>
    /// A CID here is a glyph index, because the encoding is Identity — the same fact that lets
    /// <c>/CIDToGIDMap</c> be <c>/Identity</c> above. CID 0 is set whether or not anything drew it:
    /// <c>.notdef</c> is in every font program by construction, and the set describes what the
    /// program holds rather than what the page used.
    /// </para>
    /// </remarks>
    void EmbedCidSet()
    {
        var cids = CMapInfo.GetGlyphIndices();
        var highest = 0;
        foreach (var cid in cids)
        {
            if (cid > highest)
                highest = cid;
        }

        // The highest CID has to land inside the array, so the length is that index's byte plus one.
        var bits = new byte[highest / 8 + 1];
        bits[0] |= 0x80;

        foreach (var cid in cids)
            bits[cid / 8] |= (byte)(0x80 >> (cid % 8));

        var set = new PdfDictionary(Owner);
        Owner.Internals.AddObject(set);
        set.CreateStream(bits);

        FontDescriptor.Elements[PdfFontDescriptor.Keys.CIDSet] = set.Reference;
    }

    /// <summary>
    /// Predefined keys of this dictionary.
    /// </summary>
    public new sealed class Keys : PdfFont.Keys
    {
        /// <summary>
        /// (Required) The type of PDF object that this dictionary describes;
        /// must be Font for a CIDFont dictionary.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required, FixedValue = "Font")]
        public new const string Type = "/Type";

        /// <summary>
        /// (Required) The type of CIDFont; CIDFontType0 or CIDFontType2.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public new const string Subtype = "/Subtype";

        /// <summary>
        /// (Required) The PostScript name of the CIDFont. For Type 0 CIDFonts, this
        /// is usually the value of the CIDFontName entry in the CIDFont program. For
        /// Type 2 CIDFonts, it is derived the same way as for a simple TrueType font;
        /// In either case, the name can have a subset prefix if appropriate.
        /// </summary>
        [KeyInfo(KeyType.Name | KeyType.Required)]
        public new const string BaseFont = "/BaseFont";

        /// <summary>
        /// (Required) A dictionary containing entries that define the character collection
        /// of the CIDFont.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.Required)]
        public const string CIDSystemInfo = "/CIDSystemInfo";

        /// <summary>
        /// (Required; must be an indirect reference) A font descriptor describing the
        /// CIDFont’s default metrics other than its glyph widths.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.MustBeIndirect, typeof(PdfFontDescriptor))]
        public new const string FontDescriptor = "/FontDescriptor";

        /// <summary>
        /// (Optional) The default width for glyphs in the CIDFont.
        /// Default value: 1000.
        /// </summary>
        [KeyInfo(KeyType.Integer)]
        public const string DW = "/DW";

        /// <summary>
        /// (Optional) A description of the widths for the glyphs in the CIDFont. The
        /// array’s elements have a variable format that can specify individual widths
        /// for consecutive CIDs or one width for a range of CIDs.
        /// Default value: none (the DW value is used for all glyphs).
        /// </summary>
        [KeyInfo(KeyType.Array, typeof(PdfArray))]
        public const string W = "/W";

        /// <summary>
        /// (Optional; applies only to CIDFonts used for vertical writing) An array of two
        /// numbers specifying the default metrics for vertical writing.
        /// Default value: [880 −1000].
        /// </summary>
        [KeyInfo(KeyType.Array)]
        public const string DW2 = "/DW2";

        /// <summary>
        /// (Optional; applies only to CIDFonts used for vertical writing) A description
        /// of the metrics for vertical writing for the glyphs in the CIDFont.
        /// Default value: none (the DW2 value is used for all glyphs).
        /// </summary>
        [KeyInfo(KeyType.Array, typeof(PdfArray))]
        public const string W2 = "/W2";

        /// <summary>
        /// (Optional; Type 2 CIDFonts only) A specification of the mapping from CIDs
        /// to glyph indices. If the value is a stream, the bytes in the stream contain the
        /// mapping from CIDs to glyph indices: the glyph index for a particular CID
        /// value c is a 2-byte value stored in bytes 2 × c and 2 × c + 1, where the first
        /// byte is the high-order byte. If the value of CIDToGIDMap is a name, it must
        /// be Identity, indicating that the mapping between CIDs and glyph indices is
        /// the identity mapping.
        /// Default value: Identity.
        /// This entry may appear only in a Type 2 CIDFont whose associated True-Type font
        /// program is embedded in the PDF file.
        /// </summary>
        [KeyInfo(KeyType.Dictionary | KeyType.StreamOrName)]
        public const string CIDToGIDMap = "/CIDToGIDMap";

        /// <summary>
        /// Gets the KeysMeta for these keys.
        /// </summary>
        internal static DictionaryMeta Meta => _meta ?? (_meta = CreateMeta(typeof(Keys)));

        static DictionaryMeta _meta;
    }

    /// <summary>
    /// Gets the KeysMeta of this dictionary type.
    /// </summary>
    internal override DictionaryMeta Meta => Keys.Meta;
}
