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
/// Represents a formatted value on the data series.
/// </summary>
public partial class Point : ChartObject
{
  /// <summary>
  /// Initializes a new instance of the Point class.
  /// </summary>
  internal Point()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Point class with a real value.
  /// </summary>
  public Point(double value)
    : this()
  {
    Value = value;
  }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Point Clone()
  {
    return (Point)DeepCopy();
  }

  #endregion

  #region Properties
  /// <summary>
  /// Gets the line format of the data point's border.
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
  /// Gets the filling format of the data point.
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
  /// The actual value of the data point.
  /// </summary>
  public double Value
  {
    get => value ?? 0;
    set => this.value = value;
  }
  [DV]
  internal double? value;
  #endregion

  #region Internal
  /// <summary>
  /// Converts Point into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    if (!IsNull("LineFormat") || !IsNull("FillFormat"))
    {
      serializer.WriteLine("");
      serializer.WriteLine("\\point");
      var pos = serializer.BeginAttributes();

      if (!IsNull("LineFormat"))
        lineFormat.Serialize(serializer);
      if (!IsNull("FillFormat"))
        fillFormat.Serialize(serializer);

      serializer.EndAttributes(pos);
      serializer.BeginContent();
      serializer.WriteLine(Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
      serializer.EndContent();
    }
    else
    {
      serializer.Write(Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    serializer.Write(", ");
  }

  #endregion
}
