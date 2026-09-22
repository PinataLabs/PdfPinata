#region Copyright
//
// Authors:
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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

using PdfPinata.Charting;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering.ChartMapper;

internal static class FontMapper
{
  // Only what the DOM font sets: a property it leaves unset is left unset here too, so that the
  // chart's font can supply it. Mapping a style and then a font of its own over it used to answer
  // false for every bold and italic the second had not set, and an empty colour for its colour.
  static void MapObject(Font font, DocumentObjectModel.Font domFont)
  {
    if (!domFont.IsNull("Bold"))
      font.Bold = domFont.Bold;
    if (!domFont.Color.IsEmpty)
      font.Color = ColorHelper.ToXColor(domFont.Color, domFont.Document.UseCmykColor);
    if (!domFont.IsNull("Italic"))
      font.Italic = domFont.Italic;
    if (!domFont.IsNull("Name"))
      font.Name = domFont.Name;
    if (!domFont.IsNull("Size"))
      font.Size = domFont.Size.Point;
    if (!domFont.IsNull("Subscript"))
      font.Subscript = domFont.Subscript;
    if (!domFont.IsNull("Superscript"))
      font.Superscript = domFont.Superscript;
    if (!domFont.IsNull("Strikethrough"))
      font.Strikethrough = (Strikethrough)domFont.Strikethrough;
    if (!domFont.IsNull("Underline"))
      font.Underline = (Underline)domFont.Underline;
  }

  internal static void Map(Font font, DocumentObjectModel.Document domDocument, string domStyleName)
  {
    var domStyle = domDocument.Styles[domStyleName];
    if (domStyle != null)
    {
      MapObject(font, domStyle.Font);
    }
  }

  internal static void Map(Font font, DocumentObjectModel.Font domFont)
  {
    MapObject(font, domFont);
  }
}
