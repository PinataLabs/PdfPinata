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

using System;
using System.ComponentModel;

namespace PdfPinata.Charting;

/// <summary>
/// This class represents an axis in a chart.
/// </summary>
public class Axis : ChartObject
{
  /// <summary>
  /// Initializes a new instance of the Axis class with the specified parent.
  /// </summary>
  internal Axis(DocumentObject parent) : base(parent) {}
    
  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Axis Clone()
  {
    return (Axis)DeepCopy();
  }

  /// <summary>
  /// Implements the deep copy of the object.
  /// </summary>
  protected override object DeepCopy()
  {
    var axis = (Axis)base.DeepCopy();
    if (axis.title != null)
    {
      axis.title = axis.title.Clone();
      axis.title.parent = axis;
    }
    if (axis.tickLabels != null)
    {
      axis.tickLabels = axis.tickLabels.Clone();
      axis.tickLabels.parent = axis;
    }
    if (axis.lineFormat != null)
    {
      axis.lineFormat = axis.lineFormat.Clone();
      axis.lineFormat.parent = axis;
    }
    if (axis.majorGridlines != null)
    {
      axis.majorGridlines = axis.majorGridlines.Clone();
      axis.majorGridlines.parent = axis;
    }
    if (axis.minorGridlines != null)
    {
      axis.minorGridlines = axis.minorGridlines.Clone();
      axis.minorGridlines.parent = axis;
    }
    return axis;
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets the title of the axis.
  /// </summary>
  public AxisTitle Title
  {
    get
    {
      if (title == null)
        title = new AxisTitle(this);

      return title;
    }
  }
  internal AxisTitle title;

  /// <summary>
  /// Gets or sets the minimum value of the axis.
  /// </summary>
  public double MinimumScale
  {
    get => minimumScale;
    set => minimumScale = value;
  }
  internal double minimumScale = double.NaN;

  /// <summary>
  /// Gets or sets the maximum value of the axis.
  /// </summary>
  public double MaximumScale
  {
    get => maximumScale;
    set => maximumScale = value;
  }
  internal double maximumScale = double.NaN;

  /// <summary>
  /// Gets or sets the interval of the primary tick.
  /// </summary>
  public double MajorTick
  {
    get => majorTick;
    set => majorTick = value;
  }
  internal double majorTick = double.NaN;

  /// <summary>
  /// Gets or sets the interval of the secondary tick.
  /// </summary>
  public double MinorTick
  {
    get => minorTick;
    set => minorTick = value;
  }
  internal double minorTick = double.NaN;

  /// <summary>
  /// Gets or sets the type of the primary tick mark.
  /// </summary>
  public TickMarkType MajorTickMark
  {
    get => majorTickMark;
    set
    {
      if (!Enum.IsDefined(value))
        throw new InvalidEnumArgumentException("value", (int)value, typeof(TickMarkType));
      majorTickMark = value;
      MajorTickMarkInitialized = true;
    }
  }
  internal TickMarkType majorTickMark;
  internal bool MajorTickMarkInitialized;

  /// <summary>
  /// Gets or sets the type of the secondary tick mark.
  /// </summary>
  public TickMarkType MinorTickMark
  {
    get => minorTickMark;
    set
    {
      if (!Enum.IsDefined(value))
        throw new InvalidEnumArgumentException("value", (int)value, typeof(TickMarkType));
      minorTickMark = value;
      MinorTickMarkInitialized = true;
    }
  }
  internal TickMarkType minorTickMark;
  internal bool MinorTickMarkInitialized;

  /// <summary>
  /// Gets the label of the primary tick.
  /// </summary>
  public TickLabels TickLabels
  {
    get
    {
      if (tickLabels == null)
        tickLabels = new TickLabels(this);

      return tickLabels;
    }
  }
  internal TickLabels tickLabels;

  /// <summary>
  /// Gets the format of the axis line.
  /// </summary>
  public LineFormat LineFormat
  {
    get
    {
      if (lineFormat == null)
        lineFormat = new LineFormat(this);

      return lineFormat;
    }
  }
  internal LineFormat lineFormat;

  /// <summary>
  /// Gets the primary gridline object.
  /// </summary>
  public Gridlines MajorGridlines
  {
    get
    {
      if (majorGridlines == null)
        majorGridlines = new Gridlines(this);

      return majorGridlines;
    }
  }
  internal Gridlines majorGridlines;
    
  /// <summary>
  /// Gets the secondary gridline object.
  /// </summary>
  public Gridlines MinorGridlines
  {
    get
    {
      if (minorGridlines == null)
        minorGridlines = new Gridlines(this);

      return minorGridlines;
    }
  }
  internal Gridlines minorGridlines;
    
  /// <summary>
  /// Gets or sets, whether the axis has a primary gridline object.
  /// </summary>
  public bool HasMajorGridlines
  {
    get => hasMajorGridlines;
    set => hasMajorGridlines = value;
  }
  internal bool hasMajorGridlines;

  /// <summary>
  /// Gets or sets, whether the axis has a secondary gridline object.
  /// </summary>
  public bool HasMinorGridlines
  {
    get => hasMinorGridlines;
    set => hasMinorGridlines = value;
  }
  internal bool hasMinorGridlines;
  #endregion
}
