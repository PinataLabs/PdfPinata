#region Copyright
//
// Authors:
//   Klaus Potzesny
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

namespace PdfPinata.Drawing.BarCodes;

/// <summary>
/// The standard distances between the marks of an OMR code, each a whole number of print lines.
/// </summary>
public enum MarkDistance
{
    /// <summary>
    /// 1/6 inch (12 pt), one line when printing at 6 lpi.
    /// </summary>
    Inch1_6,

    /// <summary>
    /// 2/6 inch (24 pt), two lines when printing at 6 lpi.
    /// </summary>
    Inch2_6,

    /// <summary>
    /// 2/8 inch (18 pt), two lines when printing at 8 lpi (line height 9 pt).
    /// </summary>
    Inch2_8
}
