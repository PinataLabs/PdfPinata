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
        Elements.AddBlank();
    }

    /// <summary>
    /// Adds a real value to the series.
    /// </summary>
    public Point Add(double value)
    {
        return Elements.Add(value);
    }

    /// <summary>
    /// Adds an array of real values to the series.
    /// </summary>
    public void Add(params double[] values)
    {
        Elements.Add(values);
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
            seriesElements ??= new SeriesElements(this);

            return seriesElements;
        }
        set
        {
            SetParent(value);
            seriesElements = value;
        }
    }

    [DV] internal SeriesElements seriesElements;

    /// <summary>
    /// Gets or sets the name of the series which will be used in the legend.
    /// </summary>
    public string Name
    {
        get => name ?? "";
        set => name = value;
    }

    [DV] internal string name;

    /// <summary>
    /// Gets the line format of the border of each data.
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

    [DV] internal LineFormat lineFormat;

    /// <summary>
    /// Gets the background filling of the data.
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

    [DV] internal FillFormat fillFormat;

    /// <summary>
    /// Gets or sets the size of the marker in a line chart.
    /// </summary>
    public Unit MarkerSize
    {
        get => markerSize;
        set => markerSize = value;
    }

    [DV] internal Unit markerSize = Unit.NullValue;

    /// <summary>
    /// Gets or sets the style of the marker in a line chart.
    /// </summary>
    public MarkerStyle MarkerStyle
    {
        get => markerStyle ?? default;
        set => markerStyle = EnumGuard.Checked(value);
    }

    [DV] internal MarkerStyle? markerStyle;

    /// <summary>
    /// Gets or sets the foreground color of the marker in a line chart.
    /// </summary>
    public Color MarkerForegroundColor
    {
        get => markerForegroundColor;
        set => markerForegroundColor = value;
    }

    [DV] internal Color markerForegroundColor = Color.Empty;

    /// <summary>
    /// Gets or sets the background color of the marker in a line chart.
    /// </summary>
    public Color MarkerBackgroundColor
    {
        get => markerBackgroundColor;
        set => markerBackgroundColor = value;
    }

    [DV] internal Color markerBackgroundColor = Color.Empty;

    /// <summary>
    /// Gets or sets the chart type of the series if it's intended to be different than the global chart type.
    /// </summary>
    public ChartType ChartType
    {
        get => chartType ?? default;
        set => chartType = EnumGuard.Checked(value);
    }

    [DV] internal ChartType? chartType;

    /// <summary>
    /// Gets the DataLabel of the series.
    /// </summary>
    public DataLabel DataLabel
    {
        get
        {
            dataLabel ??= new DataLabel(this);

            return dataLabel;
        }
        set
        {
            SetParent(value);
            dataLabel = value;
        }
    }

    [DV] internal DataLabel dataLabel;

    /// <summary>
    /// Gets or sets whether the series has a DataLabel.
    /// </summary>
    public bool HasDataLabel
    {
        get => hasDataLabel ?? false;
        set => hasDataLabel = value;
    }

    [DV] internal bool? hasDataLabel;

    /// <summary>
    /// Gets the elementcount of the series.
    /// </summary>
    public int Count => seriesElements?.Count ?? 0;

    #endregion

    #region Internal

    /// <summary>
    /// Converts Series into DDL.
    /// </summary>
    internal override void Serialize(Serializer serializer)
    {
        serializer.WriteLine("\\series");

        var pos = serializer.BeginAttributes();

        if (name != null)
            serializer.WriteSimpleAttribute("Name", Name);

        if (!markerSize.IsNull)
            serializer.WriteSimpleAttribute("MarkerSize", MarkerSize);
        serializer.WriteSimpleAttributeIfSet("MarkerStyle", markerStyle);

        if (!markerBackgroundColor.IsNull)
            serializer.WriteSimpleAttribute("MarkerBackgroundColor", MarkerBackgroundColor);
        if (!markerForegroundColor.IsNull)
            serializer.WriteSimpleAttribute("MarkerForegroundColor", MarkerForegroundColor);

        serializer.WriteSimpleAttributeIfSet("ChartType", chartType);
        serializer.WriteSimpleAttributeIfSet("HasDataLabel", hasDataLabel);

        serializer.SerializeUnlessNull(this, "LineFormat", lineFormat);
        serializer.SerializeUnlessNull(this, "FillFormat", fillFormat);
        serializer.SerializeUnlessNull(this, "DataLabel", dataLabel);

        serializer.EndAttributes(pos);

        serializer.BeginContent();
        seriesElements.Serialize(serializer);
        serializer.WriteLine("");
        serializer.EndContent();
    }

    #endregion
}
