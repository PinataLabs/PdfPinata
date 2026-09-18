#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
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
using System.Buffers;
using System.Diagnostics;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;

namespace PdfPinata.Drawing;

/// <summary>
/// Bunch of functions that do not have a better place.
/// </summary>
static class FontHelper
{
    /// <summary>
    /// Whether this face is having its boldness simulated because the family had no bold file.
    /// </summary>
    /// <remarks>
    /// A property of the face rather than of the string, which is the whole point of asking it
    /// this way: a string that fell back is drawn out of more than one face, and the face the
    /// caller named having no bold says nothing about whether the face that rescued it has one.
    /// </remarks>
    internal static bool SimulatesBold(XFont font)
        => (font.GlyphTypeface.StyleSimulations & XStyleSimulations.BoldSimulation)
           == XStyleSimulations.BoldSimulation;

    /// <summary>
    /// The extra character spacing bold simulation costs on this face, which is zero unless it is
    /// being simulated.
    /// </summary>
    /// <remarks>
    /// Simulation strokes the glyphs and widens them by a character spacing of its own, so it
    /// counts exactly the way <see cref="XStringFormat.CharacterSpacing"/> does - and the caller's
    /// spacing is added to it rather than replaced by it, or asking for a spacing on a
    /// simulated-bold font would quietly un-bolden it. Read by the measuring path, by
    /// <c>PdfGraphicsState.RealizeFont</c> and by the renderer's per-segment fallback writer,
    /// which have to agree or a line is drawn at a width nothing measured.
    /// </remarks>
    internal static double BoldSimulationSpacing(XFont font)
        => SimulatesBold(font) ? font.Size * Const.BoldEmphasis : 0;

    /// <summary>
    /// Measure string directly from font data.
    /// </summary>
    public static XSize MeasureString(string text, XFont font, XStringFormat stringFormat)
    {
        XSize size = new XSize();

        OpenTypeDescriptor descriptor = FontDescriptorCache.GetOrCreateDescriptorFor(font) as OpenTypeDescriptor;
        if (descriptor != null)
        {
            // Height is the sum of ascender and descender.
            var singleLineHeight = (descriptor.Ascender + descriptor.Descender) * font.Size / font.UnitsPerEm;
            var lineGapHeight = (descriptor.LineSpacing - descriptor.Ascender - descriptor.Descender) * font.Size / font.UnitsPerEm;

            Debug.Assert(descriptor.Ascender > 0);

            XStringFormat format = stringFormat ?? XStringFormats.Default;

            // Unsure how to deal with white space. Currently count as regular character.
            double wordSpacing = format.WordSpacing;

            // A glyph advances by its own width plus the character spacing, and a space by the word
            // spacing on top of that; the horizontal scaling then applies to the lot. PDF 32000-1
            // section 9.4.4.
            //
            // The character spacing is asked per segment rather than once for the string, because
            // bold simulation is a property of the face and a string that fell back is drawn out of
            // more than one. Measuring the whole line at the first face's simulation would widen
            // glyphs a fallback with a real bold is not widened by, and the line would be laid out
            // at a width the page does not draw.
            double SegmentWidth(ShapedSegment segment)
                => segment.Run.WidthAt(segment.Font.Size)
                   + segment.Run.Glyphs.Count
                     * (format.CharacterSpacing + BoldSimulationSpacing(segment.Font));

            double LineWidth(ShapedText shaped, int spaces)
            {
                double points = 0;
                for (int idx = 0; idx < shaped.Segments.Count; idx++)
                    points += SegmentWidth(shaped.Segments[idx]);

                return points + spaces * wordSpacing;
            }

            // What the line is really as wide as, asked of the shaping seam rather than counted
            // one character-width at a time. With no shaper registered the answer is the same sum
            // it always was; with one, it is the sum after kerning and ligatures, which is the
            // whole point - a measurement the drawing path would disagree with is worse than none.
            // Asked of ShapeText rather than Shape so that a line changing script, direction or
            // face part way is measured as the several runs it is drawn as, which is not the same
            // width - and it comes back in points rather than design units because two faces need
            // not share an em.
            double MeasureLine(ReadOnlySpan<char> line, int spaces)
            {
                var shaped = TextShaping.ShapeText(line, font, descriptor, format.TextDirection);
                return LineWidth(shaped, spaces);
            }

            int length = text.Length;

            // Nothing here needs rewriting before it can be shaped, so the string is shaped where
            // it stands. Much the commonest case, and the one the layout engine measures every
            // word of, so it is worth not copying for.
            bool plain = true;
            int plainSpaces = 0;
            for (int idx = 0; idx < length; idx++)
            {
                char ch = text[idx];
                if (ch < 32)
                {
                    plain = false;
                    break;
                }
                if (ch == ' ')
                    plainSpaces++;
            }

            if (plain)
            {
                size.Width = MeasureLine(text.AsSpan(), plainSpaces) * format.HorizontalScaling / 100;
                size.Height = singleLineHeight;
            }
            else
            {
                var height = singleLineHeight;
                double maxWidth = 0;

                // The line with its line feeds taken out, its tabs turned into spaces and its
                // other control characters dropped - the three things the loop below used to do
                // to a character on its way to the cmap, which a shaper must not be asked to do
                // anything with.
                char[] line = ArrayPool<char>.Shared.Rent(length);
                try
                {
                    int lineLength = 0;
                    int spaceCount = 0;
                    for (int idx = 0; idx < length; idx++)
                    {
                        char ch = text[idx];

                        // Handle line feed ( \n)
                        if (ch == 10)
                        {
                            if (idx < (length - 1))
                            {
                                maxWidth = Math.Max(maxWidth,
                                    MeasureLine(new ReadOnlySpan<char>(line, 0, lineLength), spaceCount));
                                lineLength = 0;
                                spaceCount = 0;
                                height += lineGapHeight + singleLineHeight;
                            }

                            continue;
                        }

                        // A tab becomes a space and every other control character is dropped -
                        // the rule read from the one place that states it, because
                        // XGraphicsPdfRenderer.DrawString now filters through the same call and
                        // the two must not drift apart again.
                        if (!TextNormalization.TryNormalize(ch, out ch))
                            continue;

                        if (ch == ' ')
                            spaceCount++;

                        line[lineLength++] = ch;
                    }
                    maxWidth = Math.Max(maxWidth,
                        MeasureLine(new ReadOnlySpan<char>(line, 0, lineLength), spaceCount));
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(line);
                }

                // What? size.Width = maxWidth * font.Size * (font.Italic ? 1 : 1) / descriptor.UnitsPerEm;
                size.Width = maxWidth * format.HorizontalScaling / 100;
                size.Height = height;
            }
        }
        Debug.Assert(descriptor != null, "No OpenTypeDescriptor.");

        return size;
    }

    /// <summary>
    /// Calculates an Adler32 checksum combined with the buffer length
    /// in a 64 bit unsigned integer.
    /// </summary>
    public static ulong CalcChecksum(byte[] buffer)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));

        const uint prime = 65521; // largest prime smaller than 65536
        uint s1 = 0;
        uint s2 = 0;
        int length = buffer.Length;
        int offset = 0;
        while (length > 0)
        {
            int n = 3800;
            if (n > length)
                n = length;
            length -= n;
            while (--n >= 0)
            {
                s1 += buffer[offset++];
                s2 = s2 + s1;
            }
            s1 %= prime;
            s2 %= prime;
        }
        ulong ul1 = (ulong)s2 << 16;
        ul1 = ul1 | s1;
        ulong ul2 = (ulong)buffer.Length;
        return (ul1 << 32) | ul2;
    }

    public static XFontStyle CreateStyle(bool isBold, bool isItalic)
    {
        return (isBold ? XFontStyle.Bold : 0) | (isItalic ? XFontStyle.Italic : 0);
    }
}
