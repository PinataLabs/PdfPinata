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
using PdfPinata.Pdf.Internal;
using PdfPinata.Drawing;

namespace PdfPinata.Fonts.OpenType;

/// <summary>
/// The OpenType font descriptor.
/// Currently the only font type PDFsharp supports.
/// </summary>
internal sealed class OpenTypeDescriptor : FontDescriptor
{
    /// <summary>
    /// New...
    /// </summary>
    public OpenTypeDescriptor(string fontDescriptorKey, string name, OpenTypeFontface fontface)
        : base(fontDescriptorKey)
    {
        FontFace = fontface;
        FontName = name;
        Initialize();
    }

    public OpenTypeDescriptor(string fontDescriptorKey, XFont font)
        : base(fontDescriptorKey)
    {
        FontFace = font.GlyphTypeface.Fontface;
        FontName = font.Name;
        Initialize();
    }

    internal OpenTypeDescriptor(string fontDescriptorKey, string idName, byte[] fontData)
        : base(fontDescriptorKey)
    {
        FontFace = new OpenTypeFontface(fontData, idName);
        // Try to get real name form name table
        if (idName.Contains("XPS-Font-") && FontFace.name != null && FontFace.name.Name.Length != 0)
        {
            var tag = String.Empty;
            if (idName.IndexOf('+') == 6)
                tag = idName[..6];
            idName = tag + "+" + FontFace.name.Name;
            if (FontFace.name.Style.Length != 0)
                idName += "," + FontFace.name.Style;
            //idName = idName.Replace(" ", "");
        }
        FontName = idName;
        Initialize();
    }

    internal OpenTypeFontface FontFace;

    void Initialize()
    {
        // TODO: Respect embedding restrictions.
        //bool embeddingRestricted = fontData.os2.fsType == 0x0002;

        //fontName = image.n
        ItalicAngle = FontFace.post.italicAngle;

        XMin = FontFace.head.xMin;
        YMin = FontFace.head.yMin;
        XMax = FontFace.head.xMax;
        YMax = FontFace.head.yMax;

        UnderlinePosition = FontFace.post.underlinePosition;
        UnderlineThickness = FontFace.post.underlineThickness;
        StrikeoutPosition = FontFace.os2.yStrikeoutPosition;
        StrikeoutSize = FontFace.os2.yStrikeoutSize;

        // No documetation found how to get the set vertical stems width from the
        // TrueType tables.
        // The following formula comes from PDFlib Lite source code. Acrobat 5.0 sets
        // /StemV to 0 always. I think the value doesn't matter.
        //float weight = (float)(image.os2.usWeightClass / 65.0f);
        //stemV = (int)(50 + weight * weight);  // MAGIC
        StemV = 0;

        // PDFlib states that some Apple fonts miss the OS/2 table.
        Debug.Assert(FontFace.os2 != null, "TrueType font has no OS/2 table.");

        UnitsPerEm = FontFace.head.unitsPerEm;

        // Calculate Ascent, Descent, Leading and LineSpacing like in WPF Source Code (see FontDriver.ReadBasicMetrics)

        // OS/2 is an optional table, but we can't determine if it is existing in this font.
        var os2SeemsToBeEmpty = FontFace.os2.sTypoAscender == 0 && FontFace.os2.sTypoDescender == 0 && FontFace.os2.sTypoLineGap == 0;
        //Debug.Assert(!os2SeemsToBeEmpty); // Are there fonts without OS/2 table?

        var dontUseWinLineMetrics = (FontFace.os2.fsSelection & 128) != 0;
        if (!os2SeemsToBeEmpty && dontUseWinLineMetrics)
        {
            // Comment from WPF: The font specifies that the sTypoAscender, sTypoDescender, and sTypoLineGap fields are valid and
            // should be used instead of winAscent and winDescent.
            int typoAscender = FontFace.os2.sTypoAscender;
            int typoDescender = FontFace.os2.sTypoDescender;
            int typoLineGap = FontFace.os2.sTypoLineGap;

            // Comment from WPF: We include the line gap in the ascent so that white space is distributed above the line. (Note that
            // the typo line gap is a different concept than "external leading".)
            Ascender = typoAscender + typoLineGap;
            // Comment from WPF: Typo descent is a signed value where the positive direction is up. It is therefore typically negative.
            // A signed typo descent would be quite unusual as it would indicate the descender was above the baseline
            Descender = -typoDescender;
            LineSpacing = typoAscender + typoLineGap - typoDescender;
        }
        else
        {
            // Comment from WPF: get the ascender field
            int ascender = FontFace.hhea.ascender;
            // Comment from WPF: get the descender field; this is measured in the same direction as ascender and is therefore
            // normally negative whereas we want a positive value; however some fonts get the sign wrong
            // so instead of just negating we take the absolute value.
            int descender = Math.Abs(FontFace.hhea.descender);
            // Comment from WPF: get the lineGap field and make sure it's >= 0
            int lineGap = Math.Max((short)0, FontFace.hhea.lineGap);

            if (!os2SeemsToBeEmpty)
            {
                // Comment from WPF: we could use sTypoAscender, sTypoDescender, and sTypoLineGap which are supposed to represent
                // optimal typographic values not constrained by backwards compatibility; however, many fonts get
                // these fields wrong or get them right only for Latin text; therefore we use the more reliable
                // platform-specific Windows values. We take the absolute value of the win32descent in case some
                // fonts get the sign wrong.
                int winAscent = FontFace.os2.usWinAscent;
                var winDescent = FontFace.os2.usWinDescent;

                Ascender = winAscent;
                Descender = winDescent;
                // Comment from WPF: The following calculation for designLineSpacing is per [....]. The default line spacing
                // should be the sum of the Mac ascender, descender, and lineGap unless the resulting value would
                // be less than the cell height (winAscent + winDescent) in which case we use the cell height.
                // See also http://www.microsoft.com/typography/otspec/recom.htm.
                // Note that in theory it's valid for the baseline-to-baseline distance to be less than the cell
                // height. However, Windows has never allowed this for Truetype fonts, and fonts built for Windows
                // sometimes rely on this behavior and get the hha values wrong or set them all to zero.
                LineSpacing = Math.Max(lineGap + ascender + descender, winAscent + winDescent);
            }
            else
            {
                Ascender = ascender;
                Descender = descender;
                LineSpacing = ascender + descender + lineGap;
            }
        }

        Debug.Assert(Descender >= 0);

        var cellHeight = Ascender + Descender;
        var externalLeading = LineSpacing - cellHeight;
        Leading = externalLeading;

        // sCapHeight and sxHeight are only valid if Version >= 2
        if (FontFace.os2.version >= 2 && FontFace.os2.sCapHeight != 0)
            CapHeight = FontFace.os2.sCapHeight;
        else
            CapHeight = Ascender;

        if (FontFace.os2.version >= 2 && FontFace.os2.sxHeight != 0)
            XHeight = FontFace.os2.sxHeight;
        else
            XHeight = (int)(0.66 * Ascender);

        //flags = image.
        var ansi = PdfEncoders.WinAnsiEncoding; // System.Text.Encoding.Default;
        var bytes = new byte[256];

        var symbol = FontFace.cmap.symbol;
        Widths = new int[256];
        for (var idx = 0; idx < 256; idx++)
        {
            bytes[idx] = (byte)idx;
            // PDFlib handles some font flaws here...
            // We wait for bug reports.

            var ch = (char)idx;
            var s = ansi.GetString(bytes, idx, 1);
            if (s.Length != 0)
            {
                ch = s[0];
            }

            //Debug.Assert(ch == idx);

            //int glyphIndex;
            //if (symbol)
            //{
            //    glyphIndex = idx + (FontFace.os2. usFirstCharIndex & 0xFF00);
            //    glyphIndex = CharCodeToGlyphIndex((char)glyphIndex);
            //}
            //else
            //{
            //    //Debug.Assert(idx + (fontData.os2.usFirstCharIndex & 0xFF00) == idx);
            //    //glyphIndex = CharCodeToGlyphIndex((char)idx);
            //    glyphIndex = CharCodeToGlyphIndex(ch);
            //}

            if (symbol)
            {
                // Remap ch for symbol fonts.
                ch = (char)(ch | (FontFace.os2.usFirstCharIndex & 0xFF00));  // @@@ refactor
            }
            var glyphIndex = CharCodeToGlyphIndex(ch);
            Widths[idx] = GlyphIndexToPdfWidth(glyphIndex);
        }
    }
    public int[] Widths;

    /// <summary>
    /// Gets a value indicating whether this instance belongs to a bold font.
    /// </summary>
    public override bool IsBoldFace =>
        // usWeightClass 700 is Bold
        //Debug.Assert((fontData.os2.usWeightClass >= 700) == ((fontData.os2.fsSelection & (ushort)OS2Table.FontSelectionFlags.Bold) != 0));
        FontFace.os2.IsBold;

    /// <summary>
    /// Gets a value indicating whether this instance belongs to an italic font.
    /// </summary>
    public override bool IsItalicFace => FontFace.os2.IsItalic;

    internal int DesignUnitsToPdf(double value)
    {
        return (int)Math.Round(value * 1000.0 / FontFace.head.unitsPerEm);
    }

    /// <summary>
    /// Maps a unicode to the index of the corresponding glyph.
    /// See OpenType spec "cmap - Character To Glyph Index Mapping Table / Format 4: Segment mapping to delta values"
    /// for details about this a little bit strange looking algorithm.
    /// </summary>
    public int CharCodeToGlyphIndex(char value)
    {
        var cmap4 = FontFace.cmap.cmap4;
        var segCount = cmap4.segCountX2 / 2;
        int seg;
        for (seg = 0; seg < segCount; seg++)
        {
            if (value <= cmap4.endCount[seg])
                break;
        }
        Debug.Assert(seg < segCount);

        if (value < cmap4.startCount[seg])
            return 0;

        if (cmap4.idRangeOffs[seg] == 0)
            return (value + cmap4.idDelta[seg]) & 0xFFFF;

        var idx = cmap4.idRangeOffs[seg] / 2 + (value - cmap4.startCount[seg]) - (segCount - seg);
        Debug.Assert(idx >= 0 && idx < cmap4.glyphCount);

        if (cmap4.glyphIdArray[idx] == 0)
            return 0;

        return (cmap4.glyphIdArray[idx] + cmap4.idDelta[seg]) & 0xFFFF;
    }

    /// <summary>
    /// Maps a Unicode code point to the index of the corresponding glyph, reaching past the basic
    /// multilingual plane when the face carries a <c>cmap</c> format 12 subtable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A code point inside the basic multilingual plane is answered out of the format 4 subtable
    /// exactly as it always was, even when the face has a format 12 one as well. Format 12 is a
    /// superset in principle and the two agree in practice, but "in practice" is not a reason to
    /// change which glyph every existing document draws, so it is consulted only where format 4
    /// cannot answer at all.
    /// </para>
    /// <para>
    /// A face with no format 12 subtable answers <c>.notdef</c> for an astral code point, which is
    /// what this library has always done - the difference is that it now says so once for the
    /// character rather than twice for the two halves of its surrogate pair.
    /// </para>
    /// </remarks>
    public int CharCodeToGlyphIndex(int codePoint)
    {
        if (codePoint <= 0xFFFF)
            return CharCodeToGlyphIndex((char)codePoint);

        var cmap12 = FontFace.cmap.cmap12;
        return cmap12 == null ? 0 : cmap12.CharCodeToGlyphIndex(codePoint);
    }

    /// <summary>
    /// Whether the face can draw the given code point at all - that is, whether it maps to
    /// anything other than <c>.notdef</c>.
    /// </summary>
    public bool HasGlyph(int codePoint) => CharCodeToGlyphIndex(codePoint) != 0;

    /// <summary>
    /// Converts the width of a glyph identified by its index to PDF design units.
    /// </summary>
    public int GlyphIndexToPdfWidth(int glyphIndex)
    {
        int numberOfHMetrics = FontFace.hhea.numberOfHMetrics;
        int unitsPerEm = FontFace.head.unitsPerEm;

        // glyphIndex >= numberOfHMetrics means the font is mono-spaced and all glyphs have the same width
        if (glyphIndex >= numberOfHMetrics)
            glyphIndex = numberOfHMetrics - 1;

        int width = FontFace.hmtx.Metrics[glyphIndex].advanceWidth;

        // Sometimes the unitsPerEm is 1000, sometimes a power of 2.
        if (unitsPerEm == 1000)
            return width;
        return width * 1000 / unitsPerEm; // normalize
    }

    public int PdfWidthFromCharCode(char ch)
    {
        var idx = CharCodeToGlyphIndex(ch);
        var width = GlyphIndexToPdfWidth(idx);
        return width;
    }

    /// <summary>
    ///   //Converts the width of a glyph identified by its index to PDF design units.
    /// </summary>
    public double GlyphIndexToEmfWidth(int glyphIndex, double emSize)
    {
        int numberOfHMetrics = FontFace.hhea.numberOfHMetrics;
        int unitsPerEm = FontFace.head.unitsPerEm;

        // glyphIndex >= numberOfHMetrics means the font is mono-spaced and all glyphs have the same width
        if (glyphIndex >= numberOfHMetrics)
            glyphIndex = numberOfHMetrics - 1;

        int width = FontFace.hmtx.Metrics[glyphIndex].advanceWidth;

        return width * emSize / unitsPerEm; // normalize
    }

    /// <summary>
    ///   //Converts the width of a glyph identified by its index to PDF design units.
    /// </summary>
    public int GlyphIndexToWidth(int glyphIndex)
    {
        int numberOfHMetrics = FontFace.hhea.numberOfHMetrics;

        // glyphIndex >= numberOfHMetrics means the font is mono-spaced and all glyphs have the same width
        if (glyphIndex >= numberOfHMetrics)
            glyphIndex = numberOfHMetrics - 1;

        int width = FontFace.hmtx.Metrics[glyphIndex].advanceWidth;
        return width;
    }
}
