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

namespace PdfPinata.Charting;

/// <summary>
/// Represents charts with different types.
/// </summary>
public class Chart : DocumentObject
{
  /// <summary>
  /// Initializes a new instance of the Chart class.
  /// </summary>
  public Chart()
  {
  }

  /// <summary>
  /// Initializes a new instance of the Chart class with the specified parent.
  /// </summary>
  internal Chart(DocumentObject parent) : base(parent) {}

  /// <summary>
  /// Initializes a new instance of the Chart class with the specified chart type.
  /// </summary>
  public Chart(ChartType type) : this()
  {
    Type = type;
  }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Chart Clone()
  {
    return (Chart)DeepCopy();
  }

  /// <summary>
  /// Implements the deep copy of the object.
  /// </summary>
  protected override object DeepCopy()
  {
    var chart = (Chart)base.DeepCopy();
    if (chart.font != null)
    {
      chart.font = chart.font.Clone();
      chart.font.parent = chart;
    }
    if (chart.legend != null)
    {
      chart.legend = chart.legend.Clone();
      chart.legend.parent = chart;
    }
    if (chart.xAxis != null)
    {
      chart.xAxis = chart.xAxis.Clone();
      chart.xAxis.parent = chart;
    }
    if (chart.yAxis != null)
    {
      chart.yAxis = chart.yAxis.Clone();
      chart.yAxis.parent = chart;
    }
    if (chart.zAxis != null)
    {
      chart.zAxis = chart.zAxis.Clone();
      chart.zAxis.parent = chart;
    }
    if (chart.seriesCollection != null)
    {
      chart.seriesCollection = chart.seriesCollection.Clone();
      chart.seriesCollection.parent = chart;
    }
    if (chart.xValues != null)
    {
      chart.xValues = chart.xValues.Clone();
      chart.xValues.parent = chart;
    }
    if (chart.plotArea != null)
    {
      chart.plotArea = chart.plotArea.Clone();
      chart.plotArea.parent = chart;
    }
    if (chart.dataLabel != null)
    {
      chart.dataLabel = chart.dataLabel.Clone();
      chart.dataLabel.parent = chart;
    }
    return chart;
  }

  /// <summary>
  /// Determines the type of the given axis.
  /// </summary>
  internal string CheckAxis (Axis axis)
  {
    if (xAxis != null && axis == xAxis)
      return "xaxis";
    if (yAxis != null && axis == yAxis)
      return "yaxis";
    if (zAxis != null && axis == zAxis)
      return "zaxis";

    return "";
  }
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the base type of the chart.
  /// ChartType of the series can be overwritten.
  /// </summary>
  public ChartType Type
  {
    get => type;
    set => type = value;
  }
  internal ChartType type;

  /// <summary>
  /// Gets or sets the font for the chart. This will be the default font for all objects which are
  /// part of the chart.
  /// </summary>
  public Font Font
  {
    get 
    {
      if (font == null)
        font = new Font(this);

      return font;
    }
  }
  internal Font font;

  /// <summary>
  /// Gets the legend of the chart.
  /// </summary>
  public Legend Legend
  {
    get
    {
      if (legend == null)
        legend = new Legend(this);

      return legend;
    }
  }
  internal Legend legend;

  /// <summary>
  /// Gets the X-Axis of the Chart.
  /// </summary>
  public Axis XAxis
  {
    get
    {
      if (xAxis == null)
        xAxis = new Axis(this);

      return xAxis;
    }
  }
  internal Axis xAxis;

  /// <summary>
  /// Gets the Y-Axis of the Chart.
  /// </summary>
  public Axis YAxis
  {
    get
    {
      if (yAxis == null)
        yAxis = new Axis(this);

      return yAxis;
    }
  }
  internal Axis yAxis;

  /// <summary>
  /// Gets the Z-Axis of the Chart.
  /// </summary>
  public Axis ZAxis
  {
    get
    {
      if (zAxis == null)
        zAxis = new Axis(this);

      return zAxis;
    }
  }
  internal Axis zAxis;

  /// <summary>
  /// Gets the collection of the data series.
  /// </summary>
  public SeriesCollection SeriesCollection
  {
    get
    {
      if (seriesCollection == null)
        seriesCollection = new SeriesCollection(this);

      return seriesCollection;
    }
  }
  internal SeriesCollection seriesCollection;

  /// <summary>
  /// Gets the collection of the values written on the X-Axis.
  /// </summary>
  public XValues XValues
  {
    get
    {
      if (xValues == null)
        xValues = new XValues(this);

      return xValues;
    }
  }
  internal XValues xValues;

  /// <summary>
  /// Gets the plot (drawing) area of the chart.
  /// </summary>
  public PlotArea PlotArea
  {
    get
    {
      if (plotArea == null)
        plotArea = new PlotArea(this);

      return plotArea;
    }
  }
  internal PlotArea plotArea;

  /// <summary>
  /// Gets or sets a value defining how blanks in the data series should be shown.
  /// </summary>
  public BlankType DisplayBlanksAs
  {
    get => displayBlanksAs;
    set => displayBlanksAs = value;
  }
  internal BlankType displayBlanksAs;

  /// <summary>
  /// Gets the DataLabel of the chart.
  /// </summary>
  public DataLabel DataLabel
  {
    get 
    {
      if (dataLabel == null)
        dataLabel = new DataLabel(this);

      return dataLabel;
    }
  }
  internal DataLabel dataLabel;

  /// <summary>
  /// Gets or sets whether the chart has a DataLabel.
  /// </summary>
  public bool HasDataLabel
  {
    get => hasDataLabel;
    set => hasDataLabel = value;
  }
  internal bool hasDataLabel;
  #endregion
}
