#region Copyright
//
// Authors:
//   Ben Askren
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


// ReSharper disable RedundantNameQualifier because it is required for hybrid build

namespace PdfPinata.Drawing;

/// <summary>
/// Defines a Brush with a radial gradient: the first colour on a first circle, the second colour
/// on a second, and a blend between them. With one centre and a first radius of zero it is the
/// familiar spot of colour fading out to a ring.
/// </summary>
/// <remarks>
/// It is written as a type 3 (radial) shading pattern. Nothing is painted inside the first circle
/// or outside the second unless <see cref="XBaseGradientBrush.ExtendLeft"/> or
/// <see cref="XBaseGradientBrush.ExtendRight"/> says to, so a gradient filling a rectangle
/// usually wants <see cref="XBaseGradientBrush.ExtendRight"/> for its corners.
/// </remarks>
public sealed class XRadialGradientBrush : XBaseGradientBrush
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XRadialGradientBrush"/> class.
    /// </summary>
    public XRadialGradientBrush(XPoint center1, XPoint center2, double r1, double r2, XColor color1, XColor color2) : base(color1, color2)
    {
        Center1 = center1;
        Center2 = center2;
        R1 = r1;
        R2 = r2;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XRadialGradientBrush"/> class.
    /// </summary>
    public XRadialGradientBrush(XPoint center, double r1, double r2, XColor color1, XColor color2) : base(color1, color2)
    {
        Center1 = center;
        Center2 = center;
        R1 = r1;
        R2 = r2;
    }

    internal XPoint Center1, Center2;
    internal double R1, R2;
}
