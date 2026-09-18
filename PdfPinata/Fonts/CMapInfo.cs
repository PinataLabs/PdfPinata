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
using System.Collections.Generic;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Fonts;

/// <summary>
/// Helper class that determines the characters used in a particular font.
/// </summary>
internal class CMapInfo
{
    public CMapInfo(OpenTypeDescriptor descriptor)
    {
        Debug.Assert(descriptor != null);
        _descriptor = descriptor;
    }
    internal OpenTypeDescriptor _descriptor;

    /// <summary>
    /// Adds the characters of the specified string to the hashtable.
    /// </summary>
    public void AddChars(string text)
    {
        if (text != null)
        {
            bool symbol = _descriptor.FontFace.cmap.symbol;
            int length = text.Length;
            for (int idx = 0; idx < length; idx++)
            {
                char ch = text[idx];
                if (!CharacterToGlyphIndex.ContainsKey(ch))
                {
                    char ch2 = ch;
                    if (symbol)
                    {
                        // Remap ch for symbol fonts.
                        ch2 = (char)(ch | (_descriptor.FontFace.os2.usFirstCharIndex & 0xFF00));  // @@@ refactor
                    }
                    int glyphIndex = _descriptor.CharCodeToGlyphIndex(ch2);
                    CharacterToGlyphIndex.Add(ch, glyphIndex);
                    GlyphIndices[glyphIndex] = null;
                    MinChar = (char)Math.Min(MinChar, ch);
                    MaxChar = (char)Math.Max(MaxChar, ch);
                }
            }
        }
    }

    /// <summary>
    /// Adds the glyphs of a shaped run, and what each of them stands for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The counterpart of <see cref="AddChars"/> for text that went through a shaper, and it has to
    /// be a counterpart rather than an addition: a shaper chooses glyphs the <c>cmap</c> never
    /// would. A ligature or a composed accent is one glyph standing for characters that each have a
    /// glyph of their own, and recording the characters' glyphs instead would embed and describe
    /// glyphs the page does not draw while leaving out the one it does.
    /// </para>
    /// <para>
    /// The characters a glyph stands for are read from the cluster indices around it, which is the
    /// only place that information exists. Where the same glyph turns up meaning two different
    /// things, the shorter reading wins - a glyph reachable as one character is better described by
    /// that character than by the two-character sequence that also composes to it - and choosing by
    /// length rather than by arrival keeps the written file the same whichever string was drawn
    /// first.
    /// </para>
    /// </remarks>
    public void AddShapedRun(ShapedRun run, string text)
    {
        if (run == null || text == null)
            return;

        var glyphs = run.Glyphs;
        for (int idx = 0; idx < glyphs.Count; idx++)
        {
            int glyphIndex = glyphs[idx].GlyphId;
            GlyphIndices[glyphIndex] = null;

            string characters = TextShaping.CharactersOf(run, idx, text);
            if (characters.Length == 0)
                continue;

            if (!GlyphIndexToCharacters.TryGetValue(glyphIndex, out string known)
                || characters.Length < known.Length)
            {
                GlyphIndexToCharacters[glyphIndex] = characters;
            }
        }
    }

    /// <summary>
    /// Adds the glyphIndices to the hashtable.
    /// </summary>
    public void AddGlyphIndices(string glyphIndices)
    {
        if (glyphIndices != null)
        {
            int length = glyphIndices.Length;
            for (int idx = 0; idx < length; idx++)
            {
                int glyphIndex = glyphIndices[idx];
                GlyphIndices[glyphIndex] = null;
            }
        }
    }

    /// <summary>
    /// Adds a ANSI characters.
    /// </summary>
    internal void AddAnsiChars()
    {
        byte[] ansi = new byte[256 - 32];
        for (int idx = 0; idx < 256 - 32; idx++)
            ansi[idx] = (byte)(idx + 32);
        string text = PdfEncoders.WinAnsiEncoding.GetString(ansi, 0, ansi.Length);
        AddChars(text);
    }

    internal bool Contains(char ch)
    {
        return CharacterToGlyphIndex.ContainsKey(ch);
    }

    public char[] Chars
    {
        get
        {
            char[] chars = new char[CharacterToGlyphIndex.Count];
            CharacterToGlyphIndex.Keys.CopyTo(chars, 0);
            Array.Sort(chars);
            return chars;
        }
    }

    public int[] GetGlyphIndices()
    {
        int[] indices = new int[GlyphIndices.Count];
        GlyphIndices.Keys.CopyTo(indices, 0);
        Array.Sort(indices);
        return indices;
    }

    public char MinChar = char.MaxValue;
    public char MaxChar = char.MinValue;
    public Dictionary<char, int> CharacterToGlyphIndex = new();
    public Dictionary<int, object> GlyphIndices = new();

    /// <summary>
    /// What each glyph of a shaped run stands for, which is not always one character. Filled by
    /// <see cref="AddShapedRun"/> and read by the <c>/ToUnicode</c> writer, which is the only
    /// place a ligature can be told apart from the characters it swallowed.
    /// </summary>
    public Dictionary<int, string> GlyphIndexToCharacters = new();

    /// <summary>
    /// Every glyph this font has been asked to draw, with the characters it stands for - the two
    /// sources merged, and the shorter reading of a glyph preferred over the longer for the reason
    /// given on <see cref="AddShapedRun"/>.
    /// </summary>
    internal Dictionary<int, string> GlyphMeanings()
    {
        var meanings = new Dictionary<int, string>();

        foreach (var entry in CharacterToGlyphIndex)
            meanings[entry.Value] = entry.Key.ToString();

        foreach (var entry in GlyphIndexToCharacters)
        {
            if (!meanings.TryGetValue(entry.Key, out string known) || entry.Value.Length < known.Length)
                meanings[entry.Key] = entry.Value;
        }

        return meanings;
    }
}
