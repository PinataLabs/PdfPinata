#region Copyright
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

using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel.Shapes.Charts;

/// <summary>
/// Represents the area where the actual chart is drawn.
/// </summary>
public partial class PlotArea : ChartObject
{
  /// <summary>
  /// Initializes a new instance of the PlotArea class.
  /// </summary>
  internal PlotArea()
  {
  }

  /// <summary>
  /// Initializes a new instance of the PlotArea class with the specified parent.
  /// </summary>
  internal PlotArea(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new PlotArea Clone()
  {
    return (PlotArea)DeepCopy();
  }

  #endregion

  #region Properties
  /// <summary>
  /// Gets the line format of the plot area's border.
  /// </summary>
  public LineFormat LineFormat
  {
    get
    {
      lineFormat ??= new LineFormat(this);

      return lineFormat;
    }
    set
    {
      SetParent(value);
      lineFormat = value;
    }
  }
  [DV]
  internal LineFormat lineFormat;

  /// <summary>
  /// Gets the background filling of the plot area.
  /// </summary>
  public FillFormat FillFormat
  {
    get
    {
      fillFormat ??= new FillFormat(this);

      return fillFormat;
    }
    set
    {
      SetParent(value);
      fillFormat = value;
    }
  }
  [DV]
  internal FillFormat fillFormat;

  /// <summary>
  /// Gets or sets the left padding of the area.
  /// </summary>
  public Unit LeftPadding
  {
    get => leftPadding;
    set => leftPadding = value;
  }
  [DV]
  internal Unit leftPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the right padding of the area.
  /// </summary>
  public Unit RightPadding
  {
    get => rightPadding;
    set => rightPadding = value;
  }
  [DV]
  internal Unit rightPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the top padding of the area.
  /// </summary>
  public Unit TopPadding
  {
    get => topPadding;
    set => topPadding = value;
  }
  [DV]
  internal Unit topPadding = Unit.NullValue;

  /// <summary>
  /// Gets or sets the bottom padding of the area.
  /// </summary>
  public Unit BottomPadding
  {
    get => bottomPadding;
    set => bottomPadding = value;
  }
  [DV]
  internal Unit bottomPadding = Unit.NullValue;
  #endregion

  #region Internal
  /// <summary>
  /// Converts PlotArea into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteLine("\\plotarea");
    var pos = serializer.BeginAttributes();

    if (!topPadding.IsNull)
      serializer.WriteSimpleAttribute("TopPadding", TopPadding);
    if (!leftPadding.IsNull)
      serializer.WriteSimpleAttribute("LeftPadding", LeftPadding);
    if (!rightPadding.IsNull)
      serializer.WriteSimpleAttribute("RightPadding", RightPadding);
    if (!bottomPadding.IsNull)
      serializer.WriteSimpleAttribute("BottomPadding", BottomPadding);

    if (!IsNull("LineFormat"))
      lineFormat.Serialize(serializer);
    if (!IsNull("FillFormat"))
      fillFormat.Serialize(serializer);

    serializer.EndAttributes(pos);

    serializer.BeginContent();
    serializer.EndContent();
  }

  #endregion
}
