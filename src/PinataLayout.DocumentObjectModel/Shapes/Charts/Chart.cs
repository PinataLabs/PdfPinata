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
using PinataLayout.DocumentObjectModel.Visitors;

namespace PinataLayout.DocumentObjectModel.Shapes.Charts;

/// <summary>
/// Represents charts with different types.
/// </summary>
public partial class Chart : Shape, IVisitable
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
  internal Chart(DocumentObject parent) : base(parent) { }

  /// <summary>
  /// Initializes a new instance of the Chart class with the specified chart type.
  /// </summary>
  public Chart(ChartType type)
    : this()
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
  #endregion

  #region Properties
  /// <summary>
  /// Gets or sets the base type of the chart.
  /// ChartType of the series can be overwritten.
  /// </summary>
  public ChartType Type
  {
    get => type ?? default;
    set => type = EnumGuard.Checked(value);
  }
  [DV]
  internal ChartType? type;

  /// <summary>
  /// Gets or sets the default style name of the whole chart.
  /// </summary>
  public string Style
  {
    get => style ?? "";
    set => style = value;
  }
  [DV]
  internal string style;

  /// <summary>
  /// Gets the default paragraph format of the whole chart.
  /// </summary>
  public ParagraphFormat Format
  {
    get
    {
      format ??= new ParagraphFormat(this);

      return format;
    }
    set
    {
      SetParent(value);
      format = value;
    }
  }
  [DV]
  internal ParagraphFormat format;

  /// <summary>
  /// Gets the X-Axis of the Chart.
  /// </summary>
  public Axis XAxis
  {
    get
    {
      xAxis ??= new Axis(this);

      return xAxis;
    }
    set
    {
      SetParent(value);
      xAxis = value;
    }
  }
  [DV]
  internal Axis xAxis;

  /// <summary>
  /// Gets the Y-Axis of the Chart.
  /// </summary>
  public Axis YAxis
  {
    get
    {
      yAxis ??= new Axis(this);

      return yAxis;
    }
    set
    {
      SetParent(value);
      yAxis = value;
    }
  }
  [DV]
  internal Axis yAxis;

  /// <summary>
  /// Gets the Z-Axis of the Chart.
  /// </summary>
  public Axis ZAxis
  {
    get
    {
      zAxis ??= new Axis(this);

      return zAxis;
    }
    set
    {
      SetParent(value);
      zAxis = value;
    }
  }
  [DV]
  internal Axis zAxis;

  /// <summary>
  /// Gets the collection of the data series.
  /// </summary>
  public SeriesCollection SeriesCollection
  {
    get
    {
      seriesCollection ??= new SeriesCollection(this);

      return seriesCollection;
    }
    set
    {
      SetParent(value);
      seriesCollection = value;
    }
  }
  [DV]
  internal SeriesCollection seriesCollection;

  /// <summary>
  /// Gets the collection of the values written on the X-Axis.
  /// </summary>
  public XValues XValues
  {
    get
    {
      xValues ??= new XValues(this);

      return xValues;
    }
    set
    {
      SetParent(value);
      xValues = value;
    }
  }
  [DV]
  internal XValues xValues;

  /// <summary>
  /// Gets the header area of the chart.
  /// </summary>
  public TextArea HeaderArea
  {
    get
    {
      headerArea ??= new TextArea(this);

      return headerArea;
    }
    set
    {
      SetParent(value);
      headerArea = value;
    }
  }
  [DV]
  internal TextArea headerArea;

  /// <summary>
  /// Gets the bottom area of the chart.
  /// </summary>
  public TextArea BottomArea
  {
    get
    {
      bottomArea ??= new TextArea(this);

      return bottomArea;
    }
    set
    {
      SetParent(value);
      bottomArea = value;
    }
  }
  [DV]
  internal TextArea bottomArea;

  /// <summary>
  /// Gets the top area of the chart.
  /// </summary>
  public TextArea TopArea
  {
    get
    {
      topArea ??= new TextArea(this);

      return topArea;
    }
    set
    {
      SetParent(value);
      topArea = value;
    }
  }
  [DV]
  internal TextArea topArea;

  /// <summary>
  /// Gets the footer area of the chart.
  /// </summary>
  public TextArea FooterArea
  {
    get
    {
      footerArea ??= new TextArea(this);

      return footerArea;
    }
    set
    {
      SetParent(value);
      footerArea = value;
    }
  }
  [DV]
  internal TextArea footerArea;

  /// <summary>
  /// Gets the left area of the chart.
  /// </summary>
  public TextArea LeftArea
  {
    get
    {
      leftArea ??= new TextArea(this);

      return leftArea;
    }
    set
    {
      SetParent(value);
      leftArea = value;
    }
  }
  [DV]
  internal TextArea leftArea;

  /// <summary>
  /// Gets the right area of the chart.
  /// </summary>
  public TextArea RightArea
  {
    get
    {
      rightArea ??= new TextArea(this);

      return rightArea;
    }
    set
    {
      SetParent(value);
      rightArea = value;
    }
  }
  [DV]
  internal TextArea rightArea;

  /// <summary>
  /// Gets the plot (drawing) area of the chart.
  /// </summary>
  public PlotArea PlotArea
  {
    get
    {
      plotArea ??= new PlotArea(this);

      return plotArea;
    }
    set
    {
      SetParent(value);
      plotArea = value;
    }
  }
  [DV]
  internal PlotArea plotArea;

  /// <summary>
  /// Gets or sets a value defining how blanks in the data series should be shown.
  /// </summary>
  public BlankType DisplayBlanksAs
  {
    get => displayBlanksAs ?? default;
    set => displayBlanksAs = EnumGuard.Checked(value);
  }
  [DV]
  internal BlankType? displayBlanksAs;

  /// <summary>
  /// Gets or sets whether XAxis Labels should be merged.
  /// </summary>
  public bool PivotChart
  {
    get => pivotChart ?? false;
    set => pivotChart = value;
  }
  [DV]
  internal bool? pivotChart;

  /// <summary>
  /// Gets the DataLabel of the chart.
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
  [DV]
  internal DataLabel dataLabel;

  /// <summary>
  /// Gets or sets whether the chart has a DataLabel.
  /// </summary>
  public bool HasDataLabel
  {
    get => hasDataLabel ?? false;
    set => hasDataLabel = value;
  }
  [DV]
  internal bool? hasDataLabel;
  #endregion

  /// <summary>
  /// Determines the type of the given axis.
  /// </summary>
  internal string CheckAxis(Axis axis)
  {
    if (xAxis != null && axis == xAxis)
      return "xaxis";
    if (yAxis != null && axis == yAxis)
      return "yaxis";
    if (zAxis != null && axis == zAxis)
      return "zaxis";

    return "";
  }

  /// <summary>
  /// Determines the type of the given textarea.
  /// </summary>
  internal string CheckTextArea(TextArea textArea)
  {
    if (headerArea != null && textArea == headerArea)
      return "headerarea";
    if (footerArea != null && textArea == footerArea)
      return "footerarea";
    if (leftArea != null && textArea == leftArea)
      return "leftarea";
    if (rightArea != null && textArea == rightArea)
      return "rightarea";
    if (topArea != null && textArea == topArea)
      return "toparea";
    if (bottomArea != null && textArea == bottomArea)
      return "bottomarea";

    return "";
  }

  #region Internal
  /// <summary>
  /// Converts Chart into DDL.
  /// </summary>
  internal override void Serialize(Serializer serializer)
  {
    serializer.WriteLine("\\chart(" + Type + ")");
    var pos = serializer.BeginAttributes();

    base.Serialize(serializer);
    WriteIfSet(serializer, "DisplayBlanksAs", displayBlanksAs);
    WriteIfSet(serializer, "PivotChart", pivotChart);
    WriteIfSet(serializer, "HasDataLabel", hasDataLabel);

    if (style != null)
      serializer.WriteSimpleAttribute("Style", Style);
    if (!IsNull("Format"))
      format.Serialize(serializer, "Format", null);
    SerializeIfSet(serializer, "DataLabel", dataLabel);
    serializer.EndAttributes(pos);

    serializer.BeginContent();

    SerializeIfSet(serializer, "PlotArea", plotArea);
    SerializeIfSet(serializer, "HeaderArea", headerArea);
    SerializeIfSet(serializer, "FooterArea", footerArea);
    SerializeIfSet(serializer, "TopArea", topArea);
    SerializeIfSet(serializer, "BottomArea", bottomArea);
    SerializeIfSet(serializer, "LeftArea", leftArea);
    SerializeIfSet(serializer, "RightArea", rightArea);

    SerializeIfSet(serializer, "XAxis", xAxis);
    SerializeIfSet(serializer, "YAxis", yAxis);
    SerializeIfSet(serializer, "ZAxis", zAxis);

    SerializeIfSet(serializer, "SeriesCollection", seriesCollection);
    SerializeIfSet(serializer, "XValues", xValues);

    serializer.EndContent();
  }

  /// <summary>
  /// Writes a value unless it was left unset.
  /// </summary>
  private static void WriteIfSet<T>(Serializer serializer, string valueName, T? value) where T : struct
  {
    if (value != null)
      serializer.WriteSimpleAttribute(valueName, value.Value);
  }

  /// <summary>
  /// Writes the child object held under <paramref name="name"/> unless it is null.
  /// </summary>
  private void SerializeIfSet(Serializer serializer, string name, DocumentObject child)
  {
    if (!IsNull(name))
      child.Serialize(serializer);
  }

  /// <summary>
  /// Allows the visitor object to visit the document object and it's child objects.
  /// </summary>
  void IVisitable.AcceptVisitor(DocumentObjectVisitor visitor, bool visitChildren)
  {
    visitor.VisitChart(this);
    if (!visitChildren)
      return;

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    ((IVisitable)bottomArea)?.AcceptVisitor(visitor, visitChildren);

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    ((IVisitable)footerArea)?.AcceptVisitor(visitor, visitChildren);

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    ((IVisitable)headerArea)?.AcceptVisitor(visitor, visitChildren);

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    ((IVisitable)leftArea)?.AcceptVisitor(visitor, visitChildren);

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    ((IVisitable)rightArea)?.AcceptVisitor(visitor, visitChildren);

    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
    ((IVisitable)topArea)?.AcceptVisitor(visitor, visitChildren);
  }

  #endregion
}
