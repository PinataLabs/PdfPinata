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

namespace PinataLayout.DocumentObjectModel;

/// <summary>
/// Specifies the level of a paragraph.
/// </summary>
public enum OutlineLevel
{
    /// <summary>Ordinary text, at no outline level.</summary>
    BodyText,
    /// <summary>The first outline level.</summary>
    Level1,
    /// <summary>The second outline level.</summary>
    Level2,
    /// <summary>The third outline level.</summary>
    Level3,
    /// <summary>The fourth outline level.</summary>
    Level4,
    /// <summary>The fifth outline level.</summary>
    Level5,
    /// <summary>The sixth outline level.</summary>
    Level6,
    /// <summary>The seventh outline level.</summary>
    Level7,
    /// <summary>The eighth outline level.</summary>
    Level8,
    /// <summary>The ninth outline level.</summary>
    Level9,
}
