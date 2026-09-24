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
using System.Collections.Generic;

//using Fixed = System.Int32;
//using FWord = System.Int16;
//using UFWord = System.UInt16;

namespace PdfPinata.Fonts.OpenType;

/// <summary>
/// This table contains information that describes the glyphs in the font in the TrueType outline format.
/// Information regarding the rasterizer (scaler) refers to the TrueType rasterizer.
/// http://www.microsoft.com/typography/otspec/glyf.htm
/// </summary>
internal class GlyphDataTable : OpenTypeFontTable
{
    public const string Tag = TableTagNames.Glyf;

    internal byte[] GlyphTable;

    public GlyphDataTable()
        : base(null, Tag)
    {
        DirectoryEntry.Tag = TableTagNames.Glyf;
    }

    public GlyphDataTable(OpenTypeFontface fontData)
        : base(fontData, Tag)
    {
        DirectoryEntry.Tag = TableTagNames.Glyf;
        Read();
    }

    /// <summary>
    /// Converts the bytes in a handy representation
    /// </summary>
    public static void Read()
    {
        // not yet needed...
    }

    /// <summary>
    /// Gets the data of the specified glyph.
    /// </summary>
    public byte[] GetGlyphData(int glyph)
    {
        var start = GetOffset(glyph);
        var next = GetOffset(glyph + 1);
        var count = next - start;
        var bytes = new byte[count];
        Buffer.BlockCopy(_fontData.FontSource.Bytes, start, bytes, 0, count);
        return bytes;
    }

    /// <summary>
    /// Gets the size of the byte array that defines the glyph.
    /// </summary>
    public int GetGlyphSize(int glyph)
    {
        return GetOffset(glyph + 1) - GetOffset(glyph);
    }

    /// <summary>
    /// Gets the offset of the specified glyph relative to the first byte of the font image.
    /// </summary>
    public int GetOffset(int glyph)
    {
        return DirectoryEntry.Offset + _fontData.loca.LocationTable[glyph];
    }

    /// <summary>
    /// Adds for all composite glyphs the glyphs the composite one is made of.
    /// </summary>
    public void CompleteGlyphClosure(Dictionary<int, object> glyphs)
    {
        // A component of a composite glyph may be composite itself, so every glyph reached
        // here has to be expanded in its turn. Walking a snapshot of the set this was called
        // with stops one level down and leaves the subset without the glyphs the components
        // below that are drawn from. The queue ends because a glyph joins it only when it is
        // new to the dictionary, and there are finitely many glyphs in the font.
        var pending = new Queue<int>(glyphs.Keys);
        if (!glyphs.ContainsKey(0))
        {
            glyphs.Add(0, null);
            pending.Enqueue(0);
        }
        while (pending.Count > 0)
            AddCompositeGlyphs(glyphs, pending, pending.Dequeue());
    }

    /// <summary>
    /// If the specified glyph is a composite glyph add the glyphs it is made of to the glyph table.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
    private void AddCompositeGlyphs(Dictionary<int, object> glyphs, Queue<int> pending, int glyph)
    {
        //int start = fontData.loca.GetOffset(glyph);
        var start = GetOffset(glyph);
        // Has no contour?
        if (start == GetOffset(glyph + 1))
            return;
        _fontData.Position = start;
        int numContours = _fontData.ReadShort();
        // Is not a composite glyph?
        if (numContours >= 0)
            return;
        _fontData.SeekOffset(8);
        for (; ; )
        {
            int flags = _fontData.ReadUFWord();
            int cGlyph = _fontData.ReadUFWord();
            if (!glyphs.ContainsKey(cGlyph))
            {
                glyphs.Add(cGlyph, null);
                pending.Enqueue(cGlyph);
            }
            if ((flags & MORE_COMPONENTS) == 0)
                return;
            _fontData.SeekOffset(ComponentArgumentsLength(flags));
        }
    }

    /// <summary>
    /// How many bytes of arguments and transformation follow a component's flags and glyph index.
    /// </summary>
    private static int ComponentArgumentsLength(int flags)
    {
        var offset = (flags & ARG_1_AND_2_ARE_WORDS) == 0 ? 2 : 4;
        if ((flags & WE_HAVE_A_SCALE) != 0)
            offset += 2;
        else if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0)
            offset += 4;
        if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0)
            offset += 8;
        return offset;
    }

    /// <summary>
    /// Prepares the font table to be compiled into its binary representation.
    /// </summary>
    public override void PrepareForCompilation()
    {
        base.PrepareForCompilation();

        if (DirectoryEntry.Length == 0)
            DirectoryEntry.Length = GlyphTable.Length;
        DirectoryEntry.CheckSum = CalcChecksum(GlyphTable);
    }

    /// <summary>
    /// Converts the font into its binary representation.
    /// </summary>
    public override void Write(OpenTypeFontWriter writer)
    {
        writer.Write(GlyphTable, 0, DirectoryEntry.PaddedLength);
    }

    // ReSharper disable InconsistentNaming
    // Constants from OpenType spec.
    private const int ARG_1_AND_2_ARE_WORDS = 1;
    private const int WE_HAVE_A_SCALE = 8;
    private const int MORE_COMPONENTS = 32;
    private const int WE_HAVE_AN_X_AND_Y_SCALE = 64;
    private const int WE_HAVE_A_TWO_BY_TWO = 128;
    // ReSharper restore InconsistentNaming
}
