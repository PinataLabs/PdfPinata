#region Copyright
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

namespace PdfPinata;

/// <summary>
/// Specifies how the content of a page is mapped into a page of a different size.
/// </summary>
public enum PageFitMode
{
    /// <summary>
    /// Scales uniformly until the whole of the source fits inside the target. Where the two
    /// differ in shape the content is left with slack on one pair of sides, placed by the
    /// alignment. Nothing is lost and nothing is distorted, which is why this is the default.
    /// </summary>
    Fit,

    /// <summary>
    /// Scales uniformly until the target is covered. Where the two differ in shape the content
    /// overflows on one pair of sides and the overflow is cropped, the alignment deciding which
    /// part of it goes. Nothing is distorted, but something is lost.
    /// </summary>
    Fill,

    /// <summary>
    /// Scales each axis on its own so that the source lands exactly on the target. Nothing is
    /// lost and nothing is cropped, but the content is distorted whenever the two differ in
    /// shape - circles come out as ellipses and text comes out stretched.
    /// </summary>
    Stretch,

    /// <summary>
    /// Does not scale at all. The source is placed in the target at its original size by the
    /// alignment, and whatever falls outside the target is cropped. This is the mode to ask for
    /// when the intent is to crop or to pad rather than to resize.
    /// </summary>
    None,
}
