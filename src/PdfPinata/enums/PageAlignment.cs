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
/// Where content sits in a box larger than itself, and which part of it is kept when the box is
/// smaller.
/// <para>
/// Named as the reader sees the page: <see cref="TopLeft"/> is the corner a heading is in,
/// whichever way up the coordinates run underneath.
/// </para>
/// </summary>
public enum PageAlignment
{
    /// <summary>The top left corner of the content meets the top left corner of the box.</summary>
    TopLeft,

    /// <summary>The content is centred left to right and sits against the top of the box.</summary>
    TopCenter,

    /// <summary>The top right corner of the content meets the top right corner of the box.</summary>
    TopRight,

    /// <summary>The content sits against the left of the box, centred top to bottom.</summary>
    MiddleLeft,

    /// <summary>The content is centred in both directions. The default.</summary>
    MiddleCenter,

    /// <summary>The content sits against the right of the box, centred top to bottom.</summary>
    MiddleRight,

    /// <summary>The bottom left corner of the content meets the bottom left corner of the box.</summary>
    BottomLeft,

    /// <summary>The content is centred left to right and sits against the bottom of the box.</summary>
    BottomCenter,

    /// <summary>The bottom right corner of the content meets the bottom right corner of the box.</summary>
    BottomRight
}
