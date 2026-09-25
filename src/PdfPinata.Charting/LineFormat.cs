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

namespace PdfPinata.Charting;

/// <summary>
/// Defines the format of a line in a shape object.
/// </summary>
public class LineFormat : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the LineFormat class.
  /// </summary>
  public LineFormat()
  {
  }

  /// <summary>
  /// Initializes a new instance of the LineFormat class with the specified parent.
  /// </summary>
  internal LineFormat(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new LineFormat Clone()
  {
    return (LineFormat)DeepCopy();
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets a value indicating whether the line should be visible.
  /// </summary>
  public bool Visible
  {
    get => visible;
    set { visible = value; isSet = true; }
  }
  internal bool visible;

  /// <summary>
  /// Gets or sets the width of the line in XUnit.
  /// </summary>
  public XUnit Width
  {
    get => width;
    set { width = value; isSet = true; }
  }
  internal XUnit width;

  /// <summary>
  /// Gets or sets the color of the line.
  /// </summary>
  public XColor Color
  {
    get => color;
    set { color = value; isSet = true; }
  }
  internal XColor color = XColor.Empty;

  /// <summary>
  /// Gets or sets the dash style of the line.
  /// </summary>
  public XDashStyle DashStyle
  {
    get => dashStyle;
    set { dashStyle = value; isSet = true; }
  }
  internal XDashStyle dashStyle;

  /// <summary>
  /// Gets or sets the style of the line.
  /// </summary>
  public LineStyle Style
  {
    get => style;
    set { style = value; isSet = true; }
  }
  internal LineStyle style;

  /// <summary>
  /// Whether any of the properties above has been assigned. A format the caller only read into
  /// existence - Point.LineFormat creates one the first time it is read - has not been, and is
  /// not a line format the caller gave. Copied by Clone with the rest of the fields.
  /// </summary>
  internal bool isSet;
  #endregion
}
