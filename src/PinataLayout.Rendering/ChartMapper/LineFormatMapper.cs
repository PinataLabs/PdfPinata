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

/// <summary>
/// The LineFormatMapper class.
/// </summary>
public class LineFormatMapper
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LineFormatMapper"/> class.
    /// </summary>
    public LineFormatMapper()
    {
    }

    private static void MapObject(LineFormat lineFormat, DocumentObjectModel.Shapes.LineFormat domLineFormat)
    {
        if (domLineFormat.Color.IsEmpty)
            lineFormat.Color = XColor.Empty;
        else
        {
            lineFormat.Color = ColorHelper.ToXColor(domLineFormat.Color, domLineFormat.Document.UseCmykColor);
        }
        lineFormat.DashStyle = domLineFormat.DashStyle switch
        {
            DocumentObjectModel.Shapes.DashStyle.Dash => XDashStyle.Dash,
            DocumentObjectModel.Shapes.DashStyle.DashDot => XDashStyle.DashDot,
            DocumentObjectModel.Shapes.DashStyle.DashDotDot => XDashStyle.DashDotDot,
            DocumentObjectModel.Shapes.DashStyle.Solid => XDashStyle.Solid,
            DocumentObjectModel.Shapes.DashStyle.SquareDot => XDashStyle.Dot,
            _ => XDashStyle.Solid
        };
        switch (domLineFormat.Style)
        {
            case DocumentObjectModel.Shapes.LineStyle.Single:
                lineFormat.Style = LineStyle.Single;
                break;
        }
        lineFormat.Visible = domLineFormat.Visible;
        if (domLineFormat.IsNull("Visible"))
            lineFormat.Visible = true;
        lineFormat.Width = domLineFormat.Width.Point;
    }

    internal static void Map(LineFormat lineFormat, DocumentObjectModel.Shapes.LineFormat domLineFormat)
    {
        MapObject(lineFormat, domLineFormat);
    }
}
