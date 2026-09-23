#region Copyright
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
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

using System;
using System.Diagnostics;
using PdfPinata.Drawing;

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Provides functionality to measure the width of text during document design time.
/// </summary>
public sealed class TextMeasurement
{
    /// <summary>
    /// Initializes a new instance of the TextMeasurement class with the specified font.
    /// </summary>
    public TextMeasurement(XGraphics graphics, Font font)
    {
        this.font = font ?? throw new ArgumentNullException(nameof(font));
        this.graphics = graphics;
    }

    /// <summary>
    /// Returns the size of the bounding box of the specified text.
    /// </summary>
    public XSize MeasureString(string text, UnitType unitType)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!Enum.IsDefined(unitType))
            throw new ArgumentException($@"'{unitType}' is not a defined value of {nameof(UnitType)}.", nameof(unitType));

        var size = graphics.MeasureString(text, XFont);
        switch (unitType)
        {
            case UnitType.Point:
                break;

            case UnitType.Centimeter:
                size.Width = (float)(size.Width * 2.54 / 72);
                size.Height = (float)(size.Height * 2.54 / 72);
                break;

            case UnitType.Inch:
                size.Width /= 72;
                size.Height /= 72;
                break;

            case UnitType.Millimeter:
                size.Width = (float)(size.Width * 25.4 / 72);
                size.Height = (float)(size.Height * 25.4 / 72);
                break;

            case UnitType.Pica:
                size.Width /= 12;
                size.Height /= 12;
                break;

            default:
                Debug.Assert(false, "Missing unit type");
                break;
        }
        return size;
    }

    /// <summary>
    /// Returns the size of the bounding box of the specified text in point.
    /// </summary>
    public XSize MeasureString(string text)
    {
        return MeasureString(text, UnitType.Point);
    }

    /// <summary>
    /// Gets or sets the font used for measurement.
    /// </summary>
    public Font Font
    {
        get => font;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (font == value)
                return;

            font = value;
            xFont = null;
        }
    }

    /// <summary>
    /// Gets the xfont used for measurement.
    /// </summary>
    private XFont XFont
    {
        get {
            if (xFont != null)
                return xFont;

            var style = XFontStyle.Regular;
            if (Font.Bold)
            {
                style |= XFontStyle.Bold;
            }
            if (Font.Italic)
            {
                style |= XFontStyle.Italic;
            }

            xFont = new XFont(Font.Name, Font.Size, style);

            return xFont;
        }
    }

    private Font font;
    private XFont xFont;
    private XGraphics graphics;
}
