#region Copyright
//
// Authors:
//   Niklas Schneider (mailto:Niklas.Schneider@PdfPinata.com)
//
// Copyright (c) 2005-2009 empira Software GmbH, Cologne (Germany)
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

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Provides functions which converts Charting.DOM objects into PdfPinata.Drawing objects.
/// </summary>
internal class Converter
{
  /// <summary>
  /// Creates a XFont based on the font and the fonts it inherits from. Whatever none of them sets
  /// is taken from the defaultFont parameter.
  /// </summary>
  internal static XFont ToXFont(Font font, XFont defaultFont)
  {
    if (font == null)
      return defaultFont;

    var fontFamily = font.ResolvedName;
    if (fontFamily == "")
      fontFamily = defaultFont.Name;

    // Bold and italic are replaced rather than added to, so that a font saying false is drawn
    // regular under a chart whose font is bold.
    var fontStyle = defaultFont.Style & ~(XFontStyle.Bold | XFontStyle.Italic);
    if (font.ResolvedBold ?? defaultFont.Bold)
      fontStyle |= XFontStyle.Bold;
    if (font.ResolvedItalic ?? defaultFont.Italic)
      fontStyle |= XFontStyle.Italic;

    var size = font.ResolvedSize;
    if (size == 0)
      size = defaultFont.Size;

    return new XFont(fontFamily, size, fontStyle);
  }

  /// <summary>
  /// Creates a XPen based on the specified line format. If not specified color and width will be taken
  /// from the defaultColor and defaultWidth parameter.
  /// </summary>
  internal static XPen ToXPen(LineFormat lineFormat, XColor defaultColor, double defaultWidth)
  {
    return ToXPen(lineFormat, defaultColor, defaultWidth, XDashStyle.Solid);
  }

  /// <summary>
  /// Creates a XPen based on the specified line format. If not specified color and width will be taken
  /// from the defaultPen parameter.
  /// </summary>
  internal static XPen ToXPen(LineFormat lineFormat, XPen defaultPen)
  {
    return ToXPen(lineFormat, defaultPen.Color, defaultPen.Width, defaultPen.DashStyle);
  }

  /// <summary>
  /// Creates a XPen based on the specified line format. If not specified color, width and dash style
  /// will be taken from the defaultColor, defaultWidth and defaultDashStyle parameters.
  /// </summary>
  internal static XPen ToXPen(LineFormat lineFormat, XColor defaultColor, double defaultWidth, XDashStyle defaultDashStyle)
  {
    XPen pen;
    if (lineFormat == null)
    {
      pen = new XPen(defaultColor, defaultWidth) { DashStyle = defaultDashStyle };
    }
    else
    {
      var color = defaultColor;
      if (!lineFormat.Color.IsEmpty)
        color = lineFormat.Color;

      var width = lineFormat.Width.Point;
      if (!lineFormat.Visible)
        width = 0;
      if (lineFormat.Visible && width == 0)
        width = defaultWidth;

      pen = new XPen(color, width)
      {
        DashStyle = lineFormat.dashStyle,
        DashOffset = 10 * width
      };
    }
    return pen;
  }

  /// <summary>
  /// Creates a XBrush based on the specified fill format. If not specified, color will be taken
  /// from the defaultColor parameter.
  /// </summary>
  internal static XBrush ToXBrush(FillFormat fillFormat, XColor defaultColor)
  {
    if (fillFormat == null || fillFormat.color.IsEmpty)
      return new XSolidBrush(defaultColor);
    return new XSolidBrush(fillFormat.color);
  }

  /// <summary>
  /// Creates a XBrush based on the specified font color. If not specified, color will be taken
  /// from the defaultColor parameter.
  /// </summary>
  internal static XBrush ToXBrush(Font font, XColor defaultColor)
  {
    var color = font?.ResolvedColor ?? XColor.Empty;
    return new XSolidBrush(color.IsEmpty ? defaultColor : color);
  }
}
