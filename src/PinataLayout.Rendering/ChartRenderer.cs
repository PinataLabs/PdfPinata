#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
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

using System;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Tables;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PdfPinata.Pdf.Structure;

namespace PinataLayout.Rendering;

/// <summary>
/// Renders a chart to an XGraphics object.
/// </summary>
internal class ChartRenderer : ShapeRenderer
{
  internal ChartRenderer(XGraphics gfx, Chart chart, FieldInfos fieldInfos)
    : base(gfx, chart, fieldInfos)
  {
    this.chart = chart;
    var chartRenderInfo = new ChartRenderInfo
    {
      shape = shape
    };
    renderInfo = chartRenderInfo;
  }

  internal ChartRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
    : base(gfx, renderInfo, fieldInfos)
  {
    chart = (Chart)renderInfo.DocumentObject;
  }

  private FormattedTextArea GetFormattedTextArea(TextArea area, XUnit width)
  {
    if (area == null)
      return null;

    var formattedTextArea = new FormattedTextArea(DocumentRenderer, area, fieldInfos);

    if (!double.IsNaN(width))
      formattedTextArea.InnerWidth = width;

    formattedTextArea.Format(Gfx);
    return formattedTextArea;
  }

  private FormattedTextArea GetFormattedTextArea(TextArea area)
  {
    return GetFormattedTextArea(area, double.NaN);
  }

  private void GetLeftRightVerticalPosition(out XUnit top, out XUnit bottom)
  {
    //REM: Line width is still ignored while layouting charts.
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
    top = contentArea.Y;

    if (formatInfo.formattedHeader != null)
      top += formatInfo.formattedHeader.InnerHeight;

    bottom = contentArea.Y + contentArea.Height;
    if (formatInfo.formattedFooter != null)
      bottom -= formatInfo.formattedFooter.InnerHeight;
  }

  private Rectangle GetLeftRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
    XUnit top;
    XUnit bottom;
    GetLeftRightVerticalPosition(out top, out bottom);

    var left = contentArea.X;
    var width = formatInfo.formattedLeft.InnerWidth;

    return new Rectangle(left, top, width, bottom - top);
  }

  private Rectangle GetRightRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
    XUnit top;
    XUnit bottom;
    GetLeftRightVerticalPosition(out top, out bottom);

    XUnit left = contentArea.X + contentArea.Width - formatInfo.formattedRight.InnerWidth;
    var width = formatInfo.formattedRight.InnerWidth;

    return new Rectangle(left, top, width, bottom - top);
  }

  private Rectangle GetHeaderRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;

    var left = contentArea.X;
    var top = contentArea.Y;
    var width = contentArea.Width;
    var height = formatInfo.formattedHeader.InnerHeight;

    return new Rectangle(left, top, width, height);
  }

  private Rectangle GetFooterRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;

    var left = contentArea.X;
    XUnit top = contentArea.Y + contentArea.Height - formatInfo.formattedFooter.InnerHeight;
    var width = contentArea.Width;
    var height = formatInfo.formattedFooter.InnerHeight;

    return new Rectangle(left, top, width, height);
  }

  private Rectangle GetTopRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;

    XUnit left;
    XUnit right;
    GetTopBottomHorizontalPosition(out left, out right);

    var top = contentArea.Y;
    if (formatInfo.formattedHeader != null)
      top += formatInfo.formattedHeader.InnerHeight;

    var height = formatInfo.formattedTop.InnerHeight;

    return new Rectangle(left, top, right - left, height);
  }

  private Rectangle GetBottomRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;

    XUnit left;
    XUnit right;
    GetTopBottomHorizontalPosition(out left, out right);

    XUnit top = contentArea.Y + contentArea.Height - formatInfo.formattedBottom.InnerHeight;
    if (formatInfo.formattedFooter != null)
      top -= formatInfo.formattedFooter.InnerHeight;

    var height = formatInfo.formattedBottom.InnerHeight;
    return new Rectangle(left, top, right - left, height);
  }

  private Rectangle GetPlotRect()
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
    var top = contentArea.Y;
    if (formatInfo.formattedHeader != null)
      top += formatInfo.formattedHeader.InnerHeight;

    if (formatInfo.formattedTop != null)
      top += formatInfo.formattedTop.InnerHeight;

    XUnit bottom = contentArea.Y + contentArea.Height;
    if (formatInfo.formattedFooter != null)
      bottom -= formatInfo.formattedFooter.InnerHeight;

    if (formatInfo.formattedBottom != null)
      bottom -= formatInfo.formattedBottom.InnerHeight;

    var left = contentArea.X;
    if (formatInfo.formattedLeft != null)
      left += formatInfo.formattedLeft.InnerWidth;

    XUnit right = contentArea.X + contentArea.Width;
    if (formatInfo.formattedRight != null)
      right -= formatInfo.formattedRight.InnerWidth;

    return new Rectangle(left, top, right - left, bottom - top);
  }

  internal override void Format(Area area, FormatInfo previousFormatInfo)
  {
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;

    var textArea = (TextArea)chart.GetValue("HeaderArea", GV.ReadOnly);
    formatInfo.formattedHeader = GetFormattedTextArea(textArea, chart.Width.Point);

    textArea = (TextArea)chart.GetValue("FooterArea", GV.ReadOnly);
    formatInfo.formattedFooter = GetFormattedTextArea(textArea, chart.Width.Point);

    textArea = (TextArea)chart.GetValue("LeftArea", GV.ReadOnly);
    formatInfo.formattedLeft = GetFormattedTextArea(textArea);

    textArea = (TextArea)chart.GetValue("RightArea", GV.ReadOnly);
    formatInfo.formattedRight = GetFormattedTextArea(textArea);

    textArea = (TextArea)chart.GetValue("TopArea", GV.ReadOnly);
    formatInfo.formattedTop = GetFormattedTextArea(textArea, GetTopBottomWidth());

    textArea = (TextArea)chart.GetValue("BottomArea", GV.ReadOnly);
    formatInfo.formattedBottom = GetFormattedTextArea(textArea, GetTopBottomWidth());

    base.Format(area, previousFormatInfo);
    formatInfo.chartFrame = ChartMapper.ChartMapper.Map(chart);
  }


  private static XUnit AlignVertically(VerticalAlignment vAlign, XUnit top, XUnit bottom, XUnit height)
  {
    switch (vAlign)
    {
      case VerticalAlignment.Bottom:
        return bottom - height;
      case VerticalAlignment.Center:
        return (top + bottom - height) / 2;
      default:
        return top;
    }
  }

  /// <summary>
  /// Gets the width of the top and bottom area.
  /// Used while formatting.
  /// </summary>
  /// <returns>The width of the top and bottom area</returns>
  private XUnit GetTopBottomWidth()
  {
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
    XUnit width = chart.Width.Point;
    if (formatInfo.formattedRight != null)
      width -= formatInfo.formattedRight.InnerWidth;
    if (formatInfo.formattedLeft != null)
      width -= formatInfo.formattedLeft.InnerWidth;
    return width;
  }

  /// <summary>
  /// Gets the horizontal boundaries of the top and bottom area.
  /// Used while rendering.
  /// </summary>
  /// <param name="left">The left boundary of the top and bottom area</param>
  /// <param name="right">The right boundary of the top and bottom area</param>
  private void GetTopBottomHorizontalPosition(out XUnit left, out XUnit right)
  {
    var contentArea = renderInfo.LayoutInfo.ContentArea;
    var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
    left = contentArea.X;
    right = contentArea.X + contentArea.Width;

    if (formatInfo.formattedRight != null)
      right -= formatInfo.formattedRight.InnerWidth;
    if (formatInfo.formattedLeft != null)
      left += formatInfo.formattedLeft.InnerWidth;
  }

  private void RenderArea(FormattedTextArea area, Rectangle rect)
  {
    if (area == null)
      return;


    var textArea = area.textArea;


    var fillRenderer = new FillFormatRenderer((FillFormat)textArea.GetValue("FillFormat", GV.ReadOnly), Gfx);
    fillRenderer.Render(rect.X, rect.Y, rect.Width, rect.Height);

    var top = rect.Y;
    top += textArea.TopPadding;
    XUnit bottom = rect.Y + rect.Height;
    bottom -= textArea.BottomPadding;
    top = AlignVertically(textArea.VerticalAlignment, top, bottom, area.ContentHeight);

    var left = rect.X;
    left += textArea.LeftPadding;

    var renderInfos = area.GetRenderInfos();
    RenderByInfos(left, top, renderInfos);

    var lineRenderer = new LineFormatRenderer((LineFormat)textArea.GetValue("LineFormat", GV.ReadOnly), Gfx);
    lineRenderer.Render(rect.X, rect.Y, rect.Width, rect.Height);
  }

  internal override void Render()
  {
    using (Tagger.Artifact(Gfx))
      RenderFilling();

    // A chart is a picture of data, and to a reader who cannot see it that is all it is: axis labels
    // and data labels read out in drawing order say nothing about the shape they describe. So it is
    // one figure standing or falling on its description, exactly as an image is — see
    // Shape.AlternativeText for why an undescribed one is furniture rather than a figure with
    // nothing to say.
    Tagger.EndList();
    using (BeginStructure())
    {
      var formatInfo = (ChartFormatInfo)renderInfo.FormatInfo;
      if (formatInfo.formattedHeader != null)
        RenderArea(formatInfo.formattedHeader, GetHeaderRect());

      if (formatInfo.formattedFooter != null)
        RenderArea(formatInfo.formattedFooter, GetFooterRect());

      if (formatInfo.formattedTop != null)
        RenderArea(formatInfo.formattedTop, GetTopRect());

      if (formatInfo.formattedBottom != null)
        RenderArea(formatInfo.formattedBottom, GetBottomRect());

      if (formatInfo.formattedLeft != null)
        RenderArea(formatInfo.formattedLeft, GetLeftRect());

      if (formatInfo.formattedRight != null)
        RenderArea(formatInfo.formattedRight, GetRightRect());

      var plotArea = (PlotArea)chart.GetValue("PlotArea", GV.ReadOnly);
      if (plotArea != null)
        RenderPlotArea(plotArea, GetPlotRect());
    }

    using (Tagger.Artifact(Gfx))
      RenderLine();
  }

  /// <summary>
  /// Opens the scope the chart is drawn in: a figure when it has been described, an artifact when it
  /// has not.
  /// </summary>
  private IDisposable BeginStructure()
  {
    if (chart.IsNull("AlternativeText") || string.IsNullOrEmpty(chart.AlternativeText))
      return Tagger.Artifact(Gfx);

    var scope = Tagger.Block(Gfx, chart, PdfTag.Figure, out var element);
    if (element != null)
      element.AlternateText = chart.AlternativeText;

    return scope;
  }

  private void RenderPlotArea(PlotArea area, Rectangle rect)
  {
    var chartFrame = ((ChartFormatInfo)renderInfo.FormatInfo).chartFrame;

    var top = rect.Y;
    top += area.TopPadding;

    XUnit bottom = rect.Y + rect.Height;
    bottom -= area.BottomPadding;

    var left = rect.X;
    left += area.LeftPadding;

    XUnit right = rect.X + rect.Width;
    right -= area.RightPadding;

    chartFrame.Location = new XPoint(left, top);
    chartFrame.Size = new XSize(right - left, bottom - top);
    chartFrame.DrawChart(Gfx);
  }
  private Chart chart;
}
