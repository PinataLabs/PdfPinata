#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
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

using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Abstract base class to serve as a layoutable unit.
/// </summary>
public class LayoutInfo
{
  internal LayoutInfo()
  {
  }
  /// <summary>
  /// Gets or sets the height necessary to start the document object.
  /// </summary>
  internal XUnit StartingHeight
  {
    get => startingHeight;
    set => startingHeight = value;
  }
  /// <summary>Backing field for <see cref="StartingHeight"/>.</summary>
  protected XUnit startingHeight;

  /// <summary>
  /// Gets or sets the height necessary to end the document object.
  /// </summary>
  internal XUnit TrailingHeight
  {
    get => trailingHeight;
    set => trailingHeight = value;
  }
  private XUnit trailingHeight;

  /// <summary>
  /// Indicates whether the document object shall be kept on one page
  /// with its successor.
  /// </summary>
  internal bool KeepWithNext
  {
    get => keepWithNext;
    set => keepWithNext = value;
  }
  private bool keepWithNext;

  /// <summary>
  /// Indicates whether the document object shall be kept together on one page.
  /// </summary>
  internal bool KeepTogether
  {
    get => keepTogether;
    set => keepTogether = value;
  }
  private bool keepTogether;

  /// <summary>
  /// The space that shall be kept free above the element's content.
  /// </summary>
  internal virtual XUnit MarginTop
  {
    get => marginTop;
    set => marginTop = value;
  }
  private XUnit marginTop;

  /// <summary>
  /// The space that shall be kept free right to the element's content.
  /// </summary>
  internal XUnit MarginRight
  {
    get => marginRight;
    set => marginRight = value;
  }
  private XUnit marginRight;

  /// <summary>
  /// The space that shall be kept free below the element's content.
  /// </summary>
  internal XUnit MarginBottom
  {
    get => marginBottom;
    set => marginBottom = value;
  }
  private XUnit marginBottom;

  /// <summary>
  /// The space that shall be kept free left to the element's content.
  /// </summary>
  internal XUnit MarginLeft
  {
    get => marginLeft;
    set => marginLeft = value;
  }
  private XUnit marginLeft;

  /// <summary>
  /// Gets or sets the Area needed by the content (including padding and borders for e.g. paragraphs).
  /// </summary>
  public Area ContentArea
  {
    get => contentArea;
    set => contentArea = value;
  }
  private Area contentArea;


  /// <summary>
  /// Gets or sets the a value indicating whether the element shall appear on a new page.
  /// </summary>
  internal bool PageBreakBefore
  {
    get => pageBreakBefore;
    set => pageBreakBefore = value;
  }
  private bool pageBreakBefore;


  /// <summary>
  /// Gets or sets the reference point for horizontal positioning.
  /// </summary>
  /// <remarks>Default value is AreaBoundary.</remarks>
  internal HorizontalReference HorizontalReference
  {
    get => horizontalReference;
    set => horizontalReference = value;
  }
  HorizontalReference horizontalReference;

  /// <summary>
  /// Gets or sets the reference point for vertical positioning.
  /// </summary>
  /// <remarks>Default value is PreviousElement.</remarks>
  internal VerticalReference VerticalReference
  {
    get => verticalReference;
    set => verticalReference = value;
  }
  VerticalReference verticalReference;

  /// <summary>
  /// Gets or sets the horizontal alignment of the element.
  /// </summary>
  /// <remarks>Default value is Near.</remarks>
  internal ElementAlignment HorizontalAlignment
  {
    get => horizontalAlignment;
    set => horizontalAlignment = value;
  }
  ElementAlignment horizontalAlignment;

  /// <summary>
  /// Gets or sets the vertical alignment of the element.
  /// </summary>
  /// <remarks>Default value is Near.</remarks>
  internal ElementAlignment VerticalAlignment
  {
    get => verticalAlignment;
    set => verticalAlignment = value;
  }
  ElementAlignment verticalAlignment;

  /// <summary>
  /// Gets or sets the floating behavior of surrounding elements.
  /// </summary>
  /// <remarks>Default value is TopBottom.</remarks>
  internal Floating Floating
  {
    get => floating;
    set => floating = value;
  }
  Floating floating;

  /// <summary>
  /// Gets or sets the top position of the element.
  /// </summary>
  internal XUnit Top
  {
    get => top;
    set => top = value;
  }
  XUnit top;

  /// <summary>
  /// Gets or sets the left position of the element.
  /// </summary>
  internal XUnit Left
  {
    get => left;
    set => left = value;
  }
  XUnit left;

  /// <summary>
  /// Gets or sets the minimum width of the element.
  /// </summary>
  internal XUnit MinWidth
  {
    get => minWidth;
    set => minWidth = value;
  }
  XUnit minWidth;
}
