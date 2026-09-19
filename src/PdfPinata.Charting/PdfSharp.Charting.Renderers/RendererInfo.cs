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
using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the base class of all renderer infos.
/// Renderer infos are used to hold all necessary information and time consuming calculations
/// between rendering cycles.
/// </summary>
internal abstract class RendererInfo
{
}

/// <summary>
/// Base class for all renderer infos which defines an area.
/// </summary>
internal abstract class AreaRendererInfo : RendererInfo
{
  /// <summary>
  /// Gets or sets the x coordinate of this rectangle.
  /// </summary>
  internal virtual double X
  {
    get => this.rect.X;
    set => this.rect.X = value;
  }

  /// <summary>
  /// Gets or sets the y coordinate of this rectangle.
  /// </summary>
  internal virtual double Y
  {
    get => this.rect.Y;
    set => this.rect.Y = value;
  }

  /// <summary>
  /// Gets or sets the width of this rectangle. A width below zero is taken as no width.
  /// </summary>
  internal virtual double Width
  {
    get => this.rect.Width;
    set => this.rect.Width = NotBelowZero(value);
  }

  /// <summary>
  /// Gets or sets the height of this rectangle. A height below zero is taken as no height.
  /// </summary>
  internal virtual double Height
  {
    get => this.rect.Height;
    set => this.rect.Height = NotBelowZero(value);
  }

  /// <summary>
  /// An extent, with anything below zero taken as nothing.
  /// </summary>
  /// <remarks>
  /// Every layout here works by subtraction - the plot area is the frame less the axes, and an
  /// axis is the frame less its labels and its title. A frame smaller than what its own axes
  /// measured leaves a negative remainder, and XRect answers a negative extent by throwing, so
  /// a chart drawn a little too small threw ArgumentException from three frames below the caller
  /// and named a rectangle rather than the chart.
  ///
  /// Nothing was ever going to be drawn in that case. Both plot area renderers open by returning
  /// when the plot area is empty, which reads as a decision already taken that a chart with no
  /// room draws nothing; this is what lets them reach it.
  /// </remarks>
  protected static double NotBelowZero(double extent) => extent < 0 ? 0 : extent;

  /// <summary>
  /// Gets the area's size.
  /// </summary>
  internal XSize Size
  {
    get => this.rect.Size;
    set => this.rect.Size = value;
  }

  /// <summary>
  /// Gets the area's rectangle.
  /// </summary>
  internal XRect Rect
  {
    get => this.rect;
    set => this.rect = value;
  }
  XRect rect;
}

/// <summary>
/// A ChartRendererInfo stores information of all main parts of a chart like axis renderer info or
/// plotarea renderer info.
/// </summary>
internal class ChartRendererInfo : AreaRendererInfo
{
  internal Chart Chart;

  internal AxisRendererInfo XAxisRendererInfo;
  internal AxisRendererInfo YAxisRendererInfo;
  //internal AxisRendererInfo zAxisRendererInfo; // not yet used
  internal PlotAreaRendererInfo PlotAreaRendererInfo;
  internal LegendRendererInfo LegendRendererInfo;
  internal SeriesRendererInfo[] SeriesRendererInfos;

  /// <summary>
  /// Gets the chart's default font for rendering.
  /// </summary>
  internal XFont DefaultFont
  {
    get
    {
      if (defaultFont == null)
        defaultFont = Converter.ToXFont(this.Chart.font, new XFont("Arial", 12, XFontStyle.Regular));

      return defaultFont;
    }
  }
  XFont defaultFont;

  /// <summary>
  /// Gets the chart's default font for rendering data labels.
  /// </summary>
  internal XFont DefaultDataLabelFont
  {
    get
    {
      if (defaultDataLabelFont == null)
        defaultDataLabelFont = Converter.ToXFont(this.Chart.font, new XFont("Arial", 10, XFontStyle.Regular));

      return defaultDataLabelFont;
    }
  }
  XFont defaultDataLabelFont;
}

/// <summary>
/// A CombinationRendererInfo stores information for rendering combination of charts.
/// </summary>
internal class CombinationRendererInfo : ChartRendererInfo
{
  internal SeriesRendererInfo[] CommonSeriesRendererInfos;
  internal SeriesRendererInfo[] AreaSeriesRendererInfos;
  internal SeriesRendererInfo[] ColumnSeriesRendererInfos;
  internal SeriesRendererInfo[] LineSeriesRendererInfos;
}

/// <summary>
/// PointRendererInfo is used to render one single data point which is part of a data series.
/// </summary>
internal class PointRendererInfo : RendererInfo
{
  internal Point Point;

  /// <summary>
  /// The value this point plots, or NaN if there is nothing to plot.
  /// </summary>
  /// <remarks>
  /// A series may hold a blank - that is what Series.AddBlank adds - and a blank is a null in the
  /// element collection rather than a Point carrying a special value. Reading the value through
  /// here rather than through <see cref="Point"/> is what keeps a blank from being dereferenced.
  ///
  /// NaN, because a blank is already the same thing to a renderer as a point whose value is NaN:
  /// there is nothing to draw and nothing to add to a total. Every comparison against NaN is
  /// false, so a blank falls out of a range test on its own, and the IsNaN tests already written
  /// against missing values now catch both kinds of missing.
  /// </remarks>
  internal double Value => this.Point == null ? double.NaN : this.Point.value;

  internal XPen LineFormat;
  internal XBrush FillFormat;
}

/// <summary>
/// Represents one sector of a series used by a pie chart.
/// </summary>
internal class SectorRendererInfo : PointRendererInfo
{
  internal XRect Rect;
  internal double StartAngle;
  internal double SweepAngle;
}

/// <summary>
/// Represents one data point of a series and the corresponding rectangle.
/// </summary>
internal class ColumnRendererInfo : PointRendererInfo
{
  internal XRect Rect;
}

/// <summary>
/// Stores rendering specific information for one data label entry.
/// </summary>
internal class DataLabelEntryRendererInfo : AreaRendererInfo
{
  internal string Text;
}

/// <summary>
/// Stores data label specific rendering information.
/// </summary>
internal class DataLabelRendererInfo : RendererInfo
{
  internal DataLabelEntryRendererInfo[] Entries;

  internal string Format;
  internal XFont Font;
  internal XBrush FontColor;
  internal DataLabelPosition Position;
  internal DataLabelType Type;
}

/// <summary>
/// SeriesRendererInfo holds all data series specific rendering information.
/// </summary>
internal class SeriesRendererInfo : RendererInfo
{
  internal Series Series;

  internal DataLabelRendererInfo DataLabelRendererInfo;
  internal PointRendererInfo[] PointRendererInfos;

  internal XPen LineFormat;
  internal XBrush FillFormat;

  // Used if ChartType is set to Line
  internal MarkerRendererInfo MarkerRendererInfo;

  /// <summary>
  /// Gets the sum of all points in PointRendererInfo.
  /// </summary>
  internal double SumOfPoints
  {
    get
    {
      double sum = 0;
      foreach (var pri in this.PointRendererInfos)
      {
        if (!double.IsNaN(pri.Value))
          sum += Math.Abs(pri.Value);
      }
      return sum;
    }
  }
}

/// <summary>
/// Represents a description of a marker for a line chart.
/// </summary>
internal class MarkerRendererInfo : RendererInfo
{
  internal XUnit MarkerSize;
  internal MarkerStyle MarkerStyle;
  internal XColor MarkerForegroundColor;
  internal XColor MarkerBackgroundColor;
}

/// <summary>
/// An AxisRendererInfo holds all axis specific rendering information.
/// </summary>
internal class AxisRendererInfo : AreaRendererInfo
{
  internal Axis Axis;

  internal double MinimumScale;
  internal double MaximumScale;
  internal double MajorTick;
  internal double MinorTick;
  internal TickMarkType MinorTickMark;
  internal TickMarkType MajorTickMark;
  internal double MajorTickMarkWidth;
  internal double MinorTickMarkWidth;
  internal XPen MajorTickMarkLineFormat;
  internal XPen MinorTickMarkLineFormat;

  //Gridlines
  internal XPen MajorGridlinesLineFormat;
  internal XPen MinorGridlinesLineFormat;

  //AxisTitle
  internal AxisTitleRendererInfo AxisTitleRendererInfo;

  //TickLabels
  internal string TickLabelsFormat;
  internal XFont TickLabelsFont;
  internal XBrush TickLabelsBrush;
  internal double TickLabelsHeight;

  //LineFormat
  internal XPen LineFormat;

  //Chart.XValues, used for X axis only.
  internal XValues XValues;

  /// <summary>
  /// Sets the x coordinate of the inner rectangle.
  /// </summary>
  internal override double X
  {
    set
    {
      base.X = value;
      this.InnerRect.X = value;
    }
  }

  /// <summary>
  /// Sets the y coordinate of the inner rectangle.
  /// </summary>
  internal override double Y
  {
    set
    {
      base.Y = value;
      this.InnerRect.Y = value + this.LabelSize.Height / 2;
    }
  }

  /// <summary>
  /// Sets the height of the inner rectangle.
  /// </summary>
  internal override double Height
  {
    set
    {
      base.Height = value;
      this.InnerRect.Height = NotBelowZero(value - (this.InnerRect.Y - this.Y));
    }
  }

  /// <summary>
  /// Sets the width of the inner rectangle.
  /// </summary>
  internal override double Width
  {
    set
    {
      base.Width = value;
      this.InnerRect.Width = NotBelowZero(value - this.LabelSize.Width / 2);
    }
  }
  internal XRect InnerRect;
  internal XSize LabelSize;
}

internal class AxisTitleRendererInfo : AreaRendererInfo
{
  internal AxisTitle AxisTitle;

  internal string AxisTitleText;
  internal XFont AxisTitleFont;
  internal XBrush AxisTitleBrush;
  internal double AxisTitleOrientation;
  internal HorizontalAlignment AxisTitleAlignment;
  internal VerticalAlignment AxisTitleVerticalAlignment;
  internal XSize AxisTitleSize;
}

/// <summary>
/// Represents one description of a legend entry.
/// </summary>
internal class LegendEntryRendererInfo : AreaRendererInfo
{
  internal SeriesRendererInfo SeriesRendererInfo;
  internal LegendRendererInfo LegendRendererInfo;

  internal string EntryText;

  /// <summary>
  /// Size for the marker only.
  /// </summary>
  internal XSize MarkerSize;
  internal XPen MarkerPen;
  internal XBrush MarkerBrush;

  /// <summary>
  /// Width for marker area. Extra spacing for line charts are considered.
  /// </summary>
  internal XSize MarkerArea;

  /// <summary>
  /// Size for text area.
  /// </summary>
  internal XSize TextSize;
}

/// <summary>
/// Stores legend specific rendering information.
/// </summary>
internal class LegendRendererInfo : AreaRendererInfo
{
  internal Legend Legend;

  internal XFont Font;
  internal XBrush FontColor;
  internal XPen BorderPen;
  internal LegendEntryRendererInfo[] Entries;
}

/// <summary>
/// Stores rendering information common to all plot area renderers.
/// </summary>
internal class PlotAreaRendererInfo : AreaRendererInfo
{
  internal PlotArea PlotArea;

  /// <summary>
  /// Saves the plot area's matrix.
  /// </summary>
  internal XMatrix Matrix;

  internal XPen LineFormat;
  internal XBrush FillFormat;
}
