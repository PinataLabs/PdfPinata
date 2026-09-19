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
/// Specifies the alignment of a text string relative to its layout rectangle
/// </summary>
public enum XLineAlignment  // same values as System.Drawing.StringAlignment (except BaseLine)
{
    /// <summary>
    /// Specifies the text be aligned near the layout.
    /// In a left-to-right layout, the near position is left. In a right-to-left layout, the near
    /// position is right.
    /// </summary>
    Near = 0,

    /// <summary>
    /// Specifies that text is aligned in the center of the layout rectangle.
    /// </summary>
    Center = 1,

    /// <summary>
    /// Specifies that text is aligned far from the origin position of the layout rectangle.
    /// In a left-to-right layout, the far position is right. In a right-to-left layout, the far
    /// position is left. 
    /// </summary>
    Far = 2,

    /// <summary>
    /// Specifies that text is aligned relative to its base line.
    /// With this option the layout rectangle must have a height of 0.
    /// </summary>
    BaseLine = 3,

    // The three below are the values the HTML canvas textBaseline has and the four above do not.
    // They place the text against its own metrics and take no notice of the height of the layout
    // rectangle, which is what makes them different from Near and Far rather than spellings of
    // them: Near and Far are the top and bottom of the rectangle, these are the top and bottom of
    // the text. For a rectangle of no height the two amount to the same thing.

    /// <summary>
    /// Specifies that text hangs below its position, as canvas <c>hanging</c> does - the top of
    /// the ascent sits on the line.
    /// </summary>
    Hanging = 4,

    /// <summary>
    /// Specifies that text sits above its position, as canvas <c>ideographic</c> does - the bottom
    /// of the descent sits on the line.
    /// </summary>
    Ideographic = 5,

    /// <summary>
    /// Specifies that text is centred on its x-height, as canvas <c>svg-middle</c> does - half the
    /// height of a lowercase x sits above the line.
    /// </summary>
    SvgMiddle = 6
}
