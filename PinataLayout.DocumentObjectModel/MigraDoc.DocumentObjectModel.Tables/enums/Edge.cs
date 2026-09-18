#region MigraDoc - Creating Documents on the Fly
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

namespace PinataLayout.DocumentObjectModel.Tables;

/// <summary>
/// Combinable flags to set Borders using the SetEdge function.
/// </summary>
[Flags]
public enum Edge
{
    /// <summary>The top border.</summary>
    Top = 0x0001,
    /// <summary>The left border.</summary>
    Left = 0x0002,
    /// <summary>The bottom border.</summary>
    Bottom = 0x0004,
    /// <summary>The right border.</summary>
    Right = 0x0008,
    /// <summary>The horizontal borders between the cells of the range.</summary>
    Horizontal = 0x0010,
    /// <summary>The vertical borders between the cells of the range.</summary>
    Vertical = 0x0020,
    /// <summary>The diagonal running from the top left corner to the bottom right.</summary>
    DiagonalDown = 0x0040,
    /// <summary>The diagonal running from the bottom left corner to the top right.</summary>
    DiagonalUp = 0x0080,
    /// <summary>All four outer borders.</summary>
    Box = Top | Left | Bottom | Right,
    /// <summary>Both sets of borders between the cells of the range.</summary>
    Interior = Horizontal | Vertical,
    /// <summary>Both diagonals.</summary>
    Cross = DiagonalDown | DiagonalUp,
}
