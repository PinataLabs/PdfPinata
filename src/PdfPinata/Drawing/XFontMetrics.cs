#region Copyright
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

namespace PdfPinata.Drawing;

/// <summary>
/// Collects information of a font.
/// </summary>
public sealed class XFontMetrics
{
    internal XFontMetrics(string name, int unitsPerEm, int ascent, int descent, int leading, int lineSpacing,
        int capHeight, int xHeight, int stemV, int stemH, int averageWidth, int maxWidth ,
        int underlinePosition, int underlineThickness, int strikethroughPosition, int strikethroughThickness)
    {
        Name = name;
        UnitsPerEm = unitsPerEm;
        Ascent = ascent;
        Descent = descent;
        Leading = leading;
        LineSpacing = lineSpacing;
        CapHeight = capHeight;
        XHeight = xHeight;
        StemV = stemV;
        StemH = stemH;
        AverageWidth = averageWidth;
        MaxWidth = maxWidth;
        UnderlinePosition = underlinePosition;
        UnderlineThickness = underlineThickness;
        StrikethroughPosition = strikethroughPosition;
        StrikethroughThickness = strikethroughThickness;
    }

    /// <summary>
    /// Gets the font name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the ascent value.
    /// </summary>
    public int UnitsPerEm { get; }

    /// <summary>
    /// Gets the ascent value.
    /// </summary>
    public int Ascent { get; }

    /// <summary>
    /// Gets the descent value.
    /// </summary>
    public int Descent { get; }

    /// <summary>
    /// Gets the average width.
    /// </summary>
    public int AverageWidth { get; }

    /// <summary>
    /// Gets the height of capital letters.
    /// </summary>
    public int CapHeight { get; }

    /// <summary>
    /// Gets the leading value.
    /// </summary>
    public int Leading { get; }

    /// <summary>
    /// Gets the line spacing value.
    /// </summary>
    public int LineSpacing { get; }

    /// <summary>
    /// Gets the maximum width of a character.
    /// </summary>
    public int MaxWidth { get; }

    /// <summary>
    /// Gets an internal value.
    /// </summary>
    public int StemH { get; }

    /// <summary>
    /// Gets an internal value.
    /// </summary>
    public int StemV { get; }

    /// <summary>
    /// Gets the height of a lower-case character.
    /// </summary>
    public int XHeight { get; }

    /// <summary>
    /// Gets the underline position.
    /// </summary>
    public int UnderlinePosition { get; }

    /// <summary>
    /// Gets the underline thicksness.
    /// </summary>
    public int UnderlineThickness { get; }

    /// <summary>
    /// Gets the strikethrough position.
    /// </summary>
    public int StrikethroughPosition { get; }

    /// <summary>
    /// Gets the strikethrough thicksness.
    /// </summary>
    public int StrikethroughThickness { get; }
}
