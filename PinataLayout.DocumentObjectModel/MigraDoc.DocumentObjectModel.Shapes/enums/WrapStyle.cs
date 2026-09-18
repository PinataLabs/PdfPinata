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

namespace PinataLayout.DocumentObjectModel.Shapes;

/// <summary>
/// Specifies how the shape object should be placed between the other elements.
/// </summary>
/// <remarks>
/// The four side-wrapping values are appended rather than inserted, so that the three that came
/// before them keep the numbers they have always had.
/// </remarks>
public enum WrapStyle
{
    /// <summary>
    /// The object will be placed between its predecessor and its successor.
    /// </summary>
    TopBottom,

    /// <summary>
    /// The object will be ignored when the other elements are placed.
    /// </summary>
    None,

    /// <summary>
    /// The object will be ignored when the other elements are placed.
    /// </summary>
    Through,

    /// <summary>
    /// The text flows down the <b>left</b> of the object, which therefore sits to the right of it.
    /// </summary>
    /// <remarks>
    /// <b>The name is the side the text occupies, not the side the object sits on.</b> The opposite
    /// reading is equally natural, and a caller who guesses wrong gets a page that looks deliberate
    /// and is backwards. This is the convention word processors use and it is stated here because
    /// nothing about the name gives it away.
    /// </remarks>
    Left,

    /// <summary>
    /// The text flows down the <b>right</b> of the object, which therefore sits to the left of it.
    /// </summary>
    /// <remarks>
    /// As with <see cref="Left"/>, the name is the side the <i>text</i> occupies.
    /// </remarks>
    Right,

    /// <summary>
    /// The text flows down whichever side of the object has the more room.
    /// </summary>
    /// <remarks>
    /// What a caller wants for an object positioned by alignment rather than by measurement, where
    /// which side is roomier is not known when the document is written.
    /// </remarks>
    Largest,

    /// <summary>
    /// The text may flow down either side of the object.
    /// </summary>
    /// <remarks>
    /// A line is given one span of the width available to it rather than every span, so a line level
    /// with the object is filled on one side and left empty on the other - which today makes this
    /// produce the same layout as <see cref="Largest"/>. The two are kept apart because they say
    /// different things, and would part company if a line were ever laid out across several spans.
    /// </remarks>
    Both,
}
