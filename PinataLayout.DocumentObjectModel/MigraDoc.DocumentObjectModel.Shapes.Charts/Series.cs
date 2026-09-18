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

using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.DocumentObjectModel.Shapes.Charts;

/// <summary>
/// Represents a series of data on the chart.
/// </summary>
public partial class Series : ChartObject
{
  /// <summary>
  /// Initializes a new instance of the Series class.
  /// </summary>
  public Series()
  {
  }

  #region Methods
  /// <summary>
  /// Creates a deep copy of this object.
  /// </summary>
  public new Series Clone()
  {
    return (Series)DeepCopy();
  }

  /// <summary>
  /// Adds a blank to the series.
  /// </summary>
  public void AddBlank()
  {
    this.Elements.AddBlank();
  }

  /// <summary>
  /// Adds a real value to the series.
  /// </summary>
  public Point Add(double value)
  {
    return this.Elements.Add(value);
  }

  /// <summary>
  /// Adds an array of real values to the series.
  /// </summary>
  public void Add(params double[] values)
  {
    this.Elements.Add(values);
  }
  #endregion

  #region Properties
  /// <summary>
  /// The actual value container of the series.
  /// </summary>
  public SeriesElements Elements
  {
    get
    {
      if (this.seriesElements == null)
        this.seriesElements = new SeriesElements(this);

      return this.seriesElements;
    }
    set
    {
      SetParent(value);
      this.seriesElements = value;
    }
  }
  [DV]
  internal SeriesElements seriesElements;

  /// <summary>
  /// Gets or sets the name of the series which will be used in the legend.
  /// </summary>
  public string Name
  {
    get => this.name ?? "";
    set => this.name = value;
  }
  [DV]
  internal string name;

  /// <summary>
  /// Gets the line format of the border of each data.
  /// </summary>
  public LineFormat LineFormat
  {
    get
    {
      if (this.lineFormat == null)
        this.lineFormat = new LineFormat(this);

      return this.lineFormat;
    }
    set
    {
      SetParent(value);
      this.lineFormat = value;
    }
  }
  [DV]
  internal LineFormat lineFormat;

  /// <summary>
  /// Gets the background filling of the data.
  /// </summary>
  public FillFormat FillFormat
  {
    get
    {
      if (this.fillFormat == null)
        this.fillFormat = new FillFormat(this);

      return this.fillFormat;
    }
    set
    {
      SetParent(value);
      this.fillFormat = value;
    }
  }
  [DV]
  internal FillFormat fillFormat;

  /// <summary>
  /// Gets or sets the size of the marker in a line chart.
  /// </summary>
  public Unit MarkerSize
  {
    get => this.markerSize;
    set => this.markerSize = value;
  }
  [DV]
  internal Unit markerSize = Unit.NullValue;

  /// <summary>
  /// Gets or sets the style of the marker in a line chart.
  /// </summary>
  public MarkerStyle MarkerStyle
  {
    get => this.markerStyle ?? default;
    set => this.markerStyle = EnumGuard.Checked(value);
  }
  [DV]
  internal MarkerStyle? markerStyle;

  /// <summary>
  /// Gets or sets the foreground color of the marker in a line chart.
  /// </summary>
  public Color MarkerForegroundColor
  {
    get => this.markerForegroundColor;
    set => this.markerForegroundColor = value;
  }
  [DV]
  internal Color markerForegroundColor = Color.Empty;

  /// <summary>
  /// Gets or sets the background color of the marker in a line chart.
  /// </summary>
  public Color MarkerBackgroundColor
  {
    get => this.markerBackgroundColor;
    set => this.markerBackgroundColor = value;
  }
  [DV]
  internal Color markerBackgroundColor = Color.Empty;

  /// <summary>
  /// Gets or sets the chart type of the series if it's intended to be different than the global chart type.
  /// </summary>
  public ChartType ChartType
  {
    get => this.chartType ?? default;
    set => this.chartType = EnumGuard.Checked(value);
  }
  [DV]
  internal ChartType? chartType;

  /// <summary>
  /// Gets the DataLabel of the series.
  /// </summary>
  public DataLabel DataLabel
  {
    get
    {
      if (this.dataLabel == null)
        this.dataLabel = new DataLabel(this);

      return this.dataLabel;
    }
    set
    {
      SetParent(value);
      this.dataLabel = value;
    }
  }
  [DV]
  internal DataLabel dataLabel;

  /// <summary>
  /// Gets or sets whether the series has a DataLabel.
  /// </summary>
  public bool HasDataLabel
  {
    get => this.hasDataLabel ?? false;
    set => this.hasDataLabel = value;
  }
  [DV]
  internal bool? hasDataLabel;

  /// <summary>
  /// Gets the elementcount of the series.
  /// </summary>
  public int Count
  {
    get
    {
      if (this.seriesElements != null)
        return this.seriesElements.Count;

      return 0;
    }
  }
  #endregion

  #region Internal
  /// <summary>
  /// Converts Series into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteLine("\\series");

    int pos = serializer.BeginAttributes();

    if (this.name != null)
      serializer.WriteSimpleAttribute("Name", this.Name);

    if (!this.markerSize.IsNull)
      serializer.WriteSimpleAttribute("MarkerSize", this.MarkerSize);
    if (this.markerStyle != null)
      serializer.WriteSimpleAttribute("MarkerStyle", this.MarkerStyle);

    if (!this.markerBackgroundColor.IsNull)
      serializer.WriteSimpleAttribute("MarkerBackgroundColor", this.MarkerBackgroundColor);
    if (!this.markerForegroundColor.IsNull)
      serializer.WriteSimpleAttribute("MarkerForegroundColor", this.MarkerForegroundColor);

    if (this.chartType != null)
      serializer.WriteSimpleAttribute("ChartType", this.ChartType);

    if (this.hasDataLabel != null)
      serializer.WriteSimpleAttribute("HasDataLabel", this.HasDataLabel);

    if (!this.IsNull("LineFormat"))
      this.lineFormat.Serialize(serializer);
    if (!this.IsNull("FillFormat"))
      this.fillFormat.Serialize(serializer);
    if (!this.IsNull("DataLabel"))
      this.dataLabel.Serialize(serializer);

    serializer.EndAttributes(pos);

    serializer.BeginContent();
    this.seriesElements.Serialize(serializer);
    serializer.WriteLine("");
    serializer.EndContent();
  }

  #endregion
}
