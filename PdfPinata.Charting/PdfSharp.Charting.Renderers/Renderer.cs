#region Copyright
//
// Authors:
//   Niklas Schneider (mailto:Niklas.Schneider@PdfPinata.com)
//
// Copyright (c) 2005-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
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

using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Base class of all renderers.
/// </summary>
internal abstract class Renderer
{
  /// <summary>
  /// Initializes a new instance of the Renderer class with the specified renderer parameters.
  /// </summary>
  internal Renderer(RendererParameters rendererParms)
  {
    this.rendererParms = rendererParms;
  }

  /// <summary>
  /// Derived renderer should return an initialized and renderer specific rendererInfo,
  /// e. g. XAxisRenderer returns an new instance of AxisRendererInfo class.
  /// </summary>
  internal virtual RendererInfo Init()
  {
    return null;
  }

  /// <summary>
  /// Layouts and calculates the space used by the renderer's drawing item.
  /// </summary>
  internal virtual void Format()
  {
    // nothing to do
  }

  /// <summary>
  /// Draws the item.
  /// </summary>
  internal abstract void Draw();

  /// <summary>
  /// Whether an area has no room to draw anything in.
  /// </summary>
  /// <remarks>
  /// Asked of the plot area, by every renderer that draws inside it, before it draws. The test
  /// used to be XRect.IsEmpty, which means the rectangle is the empty one - a width below zero -
  /// rather than that it has no room. It was reached by a frame too small for its own axes, whose
  /// layout subtracted more than it had and produced exactly that; and since an extent below zero
  /// is now taken as no extent, so that XRect is not handed a negative one, nothing produces the
  /// empty rectangle any more and the test has to be the one that was meant.
  /// </remarks>
  protected static bool HasNoRoom(XRect area) => area.Width <= 0 || area.Height <= 0;

  /// <summary>
  /// Holds all necessary rendering information.
  /// </summary>
  protected RendererParameters rendererParms;
}
