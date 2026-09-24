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
/// This class represents an axis in a chart.
/// </summary>
public partial class Axis : ChartObject
{
  /// <summary>
  /// Initializes a new instance of the Axis class.
  /// </summary>
  public Axis()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Axis class with the specified parent.
  /// </summary>
  internal Axis(DocumentObject parent) : base(parent) { }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Axis Clone()
  {
    return (Axis)DeepCopy();
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
      title ??= new AxisTitle(this);

      return title;
    }
    set
    {
      SetParent(value);
      title = value;
    }
  }
  [DV]
  internal AxisTitle title;

  /// <summary>
  /// Gets or sets the minimum value of the axis.
  /// </summary>
  public double MinimumScale
  {
    get => minimumScale ?? 0;
    set => minimumScale = value;
  }
  [DV]
  internal double? minimumScale;

  /// <summary>
  /// Gets or sets the maximum value of the axis.
  /// </summary>
  public double MaximumScale
  {
    get => maximumScale ?? 0;
    set => maximumScale = value;
  }
  [DV]
  internal double? maximumScale;

  /// <summary>
  /// Gets or sets the interval of the primary tick.
  /// </summary>
  public double MajorTick
  {
    get => majorTick ?? 0;
    set => majorTick = value;
  }
  [DV]
  internal double? majorTick;

  /// <summary>
  /// Gets or sets the interval of the secondary tick.
  /// </summary>
  public double MinorTick
  {
    get => minorTick ?? 0;
    set => minorTick = value;
  }
  [DV]
  internal double? minorTick;

  /// <summary>
  /// Gets or sets the type of the primary tick mark.
  /// </summary>
  public TickMarkType MajorTickMark
  {
    get => majorTickMark ?? default;
    set => majorTickMark = EnumGuard.Checked(value);
  }
  [DV]
  internal TickMarkType? majorTickMark;

  /// <summary>
  /// Gets or sets the type of the secondary tick mark.
  /// </summary>
  public TickMarkType MinorTickMark
  {
    get => minorTickMark ?? default;
    set => minorTickMark = EnumGuard.Checked(value);
  }
  [DV]
  internal TickMarkType? minorTickMark;

  /// <summary>
  /// Gets the label of the primary tick.
  /// </summary>
  public TickLabels TickLabels
  {
    get
    {
      tickLabels ??= new TickLabels(this);

      return tickLabels;
    }
    set
    {
      SetParent(value);
      tickLabels = value;
    }
  }
  [DV]
  internal TickLabels tickLabels;

  /// <summary>
  /// Gets the format of the axis line.
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
  /// Gets the primary gridline object.
  /// </summary>
  public Gridlines MajorGridlines
  {
    get
    {
      majorGridlines ??= new Gridlines(this);

      return majorGridlines;
    }
    set
    {
      SetParent(value);
      majorGridlines = value;
    }
  }
  [DV]
  internal Gridlines majorGridlines;

  /// <summary>
  /// Gets the secondary gridline object.
  /// </summary>
  public Gridlines MinorGridlines
  {
    get
    {
      minorGridlines ??= new Gridlines(this);

      return minorGridlines;
    }
    set
    {
      SetParent(value);
      minorGridlines = value;
    }
  }
  [DV]
  internal Gridlines minorGridlines;

  /// <summary>
  /// Gets or sets, whether the axis has a primary gridline object.
  /// </summary>
  public bool HasMajorGridlines
  {
    get => hasMajorGridlines ?? false;
    set => hasMajorGridlines = value;
  }
  [DV]
  internal bool? hasMajorGridlines;

  /// <summary>
  /// Gets or sets, whether the axis has a secondary gridline object.
  /// </summary>
  public bool HasMinorGridlines
  {
    get => hasMinorGridlines ?? false;
    set => hasMinorGridlines = value;
  }
  [DV]
  internal bool? hasMinorGridlines;
  #endregion

  /// <summary>
  /// Determines whether the specified gridlines object is a MajorGridlines or an MinorGridlines.
  /// </summary>
  internal string CheckGridlines(Gridlines gridlines)
  {
    if (majorGridlines != null && gridlines == majorGridlines)
      return "MajorGridlines";
    if (minorGridlines != null && gridlines == minorGridlines)
      return "MinorGridlines";

    return "";
  }

  #region Internal
  /// <summary>
  /// Converts Axis into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    var chartObject = parent as Chart;

    // ReSharper disable once PossibleNullReferenceException
    serializer.WriteLine("\\" + chartObject.CheckAxis(this));
    var pos = serializer.BeginAttributes();

    serializer.WriteSimpleAttributeIfSet("MinimumScale", minimumScale);
    serializer.WriteSimpleAttributeIfSet("MaximumScale", maximumScale);
    serializer.WriteSimpleAttributeIfSet("MajorTick", majorTick);
    serializer.WriteSimpleAttributeIfSet("MinorTick", minorTick);
    serializer.WriteSimpleAttributeIfSet("HasMajorGridLines", hasMajorGridlines);
    serializer.WriteSimpleAttributeIfSet("HasMinorGridLines", hasMinorGridlines);
    serializer.WriteSimpleAttributeIfSet("MajorTickMark", majorTickMark);
    serializer.WriteSimpleAttributeIfSet("MinorTickMark", minorTickMark);

    serializer.SerializeUnlessNull(this, "Title", title);
    serializer.SerializeUnlessNull(this, "LineFormat", lineFormat);
    serializer.SerializeUnlessNull(this, "MajorGridlines", majorGridlines);
    serializer.SerializeUnlessNull(this, "MinorGridlines", minorGridlines);
    serializer.SerializeUnlessNull(this, "TickLabels", tickLabels);

    serializer.EndAttributes(pos);
  }

  #endregion
}
