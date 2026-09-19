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

using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Represents the base for all specialized axis renderer. Initialization common too all
/// axis renderer should come here.
/// </summary>
internal abstract class AxisRenderer : Renderer
{
  /// <summary>
  /// Initializes a new instance of the AxisRenderer class with the specified renderer parameters.
  /// </summary>
  internal AxisRenderer(RendererParameters parms) : base(parms)
  {
  }

  /// <summary>
  /// Initializes the axis title of the rendererInfo. All missing font attributes will be taken
  /// from the specified defaultFont.
  /// </summary>
  protected static void InitAxisTitle(AxisRendererInfo rendererInfo, XFont defaultFont)
  {
    if (rendererInfo.axis.title != null)
    {
      var atri = new AxisTitleRendererInfo();
      rendererInfo.axisTitleRendererInfo = atri;

      atri.axisTitle = rendererInfo.axis.title;
      atri.AxisTitleText = rendererInfo.axis.title.caption;
      atri.AxisTitleAlignment = rendererInfo.axis.title.alignment;
      atri.AxisTitleVerticalAlignment = rendererInfo.axis.title.verticalAlignment;
      atri.AxisTitleFont = Converter.ToXFont(rendererInfo.axis.title.font, defaultFont);
      var fontColor = XColors.Black;
      if (rendererInfo.axis.title.font != null && !rendererInfo.axis.title.font.color.IsEmpty)
        fontColor = rendererInfo.axis.title.font.color;
      atri.AxisTitleBrush = new XSolidBrush(fontColor);
      atri.AxisTitleOrientation = rendererInfo.axis.title.orientation;
    }
  }

  /// <summary>
  /// Initializes the tick labels of the rendererInfo. All missing font attributes will be taken
  /// from the specified defaultFont.
  /// </summary>
  protected void InitTickLabels(AxisRendererInfo rendererInfo, XFont defaultFont)
  {
    if (rendererInfo.axis.tickLabels != null)
    {
      rendererInfo.TickLabelsFont = Converter.ToXFont(rendererInfo.axis.tickLabels.font, defaultFont);
      var fontColor = XColors.Black;
      if (rendererInfo.axis.tickLabels.font != null && !rendererInfo.axis.tickLabels.font.color.IsEmpty)
        fontColor = rendererInfo.axis.tickLabels.font.color;
      rendererInfo.TickLabelsBrush = new XSolidBrush(fontColor);

      rendererInfo.TickLabelsFormat = rendererInfo.axis.tickLabels.format;
      if (rendererInfo.TickLabelsFormat == null)
        rendererInfo.TickLabelsFormat = GetDefaultTickLabelsFormat();
    }
    else
    {
      rendererInfo.TickLabelsFont = defaultFont;
      rendererInfo.TickLabelsBrush = new XSolidBrush(XColors.Black);
      rendererInfo.TickLabelsFormat = GetDefaultTickLabelsFormat();
    }
  }
    
  /// <summary>
  /// Initializes the line format of the rendererInfo.
  /// </summary>
  protected static void InitAxisLineFormat(AxisRendererInfo rendererInfo)
  {
    if (rendererInfo.axis.minorTickMarkInitialized)
      rendererInfo.MinorTickMark = rendererInfo.axis.MinorTickMark;

    if (rendererInfo.axis.majorTickMarkInitialized)
      rendererInfo.MajorTickMark = rendererInfo.axis.MajorTickMark;
    else
      rendererInfo.MajorTickMark = TickMarkType.Outside;

    if (rendererInfo.MinorTickMark != TickMarkType.None)
      rendererInfo.MinorTickMarkLineFormat = Converter.ToXPen(rendererInfo.axis.lineFormat, XColors.Black, DefaultMinorTickMarkLineWidth);

    if (rendererInfo.MajorTickMark != TickMarkType.None)
      rendererInfo.MajorTickMarkLineFormat = Converter.ToXPen(rendererInfo.axis.lineFormat, XColors.Black, DefaultMajorTickMarkLineWidth);

    if (rendererInfo.axis.lineFormat != null)
    {
      rendererInfo.LineFormat = Converter.ToXPen(rendererInfo.axis.LineFormat, XColors.Black, DefaultLineWidth);
      if (!rendererInfo.axis.majorTickMarkInitialized)
        rendererInfo.MajorTickMark = TickMarkType.Outside;
    }
  }
    
  /// <summary>
  /// Initializes the gridlines of the rendererInfo.
  /// </summary>
  protected static void InitGridlines(AxisRendererInfo rendererInfo)
  {
    if (rendererInfo.axis.minorGridlines != null)
    {
      rendererInfo.MinorGridlinesLineFormat =
        Converter.ToXPen(rendererInfo.axis.minorGridlines.lineFormat, XColors.Black, DefaultGridLineWidth);
    }
    else if (rendererInfo.axis.hasMinorGridlines)
    {
      // No minor gridlines object are given, but user asked for.
      rendererInfo.MinorGridlinesLineFormat = new XPen(XColors.Black, DefaultGridLineWidth);
    }

    if (rendererInfo.axis.majorGridlines != null)
    {
      rendererInfo.MajorGridlinesLineFormat =
        Converter.ToXPen(rendererInfo.axis.majorGridlines.lineFormat, XColors.Black, DefaultGridLineWidth);
    }
    else if (rendererInfo.axis.hasMajorGridlines)
    {
      // No major gridlines object are given, but user asked for.
      rendererInfo.MajorGridlinesLineFormat = new XPen(XColors.Black, DefaultGridLineWidth);
    }
  }

  /// <summary>
  /// Default width for a variety of lines.
  /// </summary>
  protected const double DefaultLineWidth = 0.4; // 0.15 mm

  /// <summary>
  /// Default width for a gridlines.
  /// </summary>
  protected const double DefaultGridLineWidth = 0.15;

  /// <summary>
  /// Default width for major tick marks.
  /// </summary>
  protected const double DefaultMajorTickMarkLineWidth = 1;

  /// <summary>
  /// Default width for minor tick marks.
  /// </summary>
  protected const double DefaultMinorTickMarkLineWidth = 1;

  /// <summary>
  /// Default width of major tick marks.
  /// </summary>
  protected const double DefaultMajorTickMarkWidth = 4.3; // 1.5 mm

  /// <summary>
  /// Default width of minor tick marks.
  /// </summary>
  protected const double DefaultMinorTickMarkWidth = 2.8; // 1 mm

  /// <summary>
  /// Default width of space between label and tick mark.
  /// </summary>
  protected const double SpaceBetweenLabelAndTickmark = 2.1; // 0.7 mm

  protected abstract string GetDefaultTickLabelsFormat();

  /// <summary>
  /// Turns a tick mark type into the two endpoints of the little line it draws, one
  /// <paramref name="width"/> away from <paramref name="edge"/>. <paramref name="direction"/> is
  /// +1 where growing away from the edge means a larger coordinate and -1 where it means a
  /// smaller one. Shared by <see cref="XAxisRenderer.GetTickMarkPos"/> and
  /// <see cref="YAxisRenderer.GetTickMarkPos"/>, which otherwise each carried this switch twice
  /// over - once per orientation - and so four times between the pair of them; a fifth
  /// <see cref="TickMarkType"/> now wants changing here once rather than in four places. Which
  /// value comes out as "start" and which as "end" is arbitrary: both callers use them only as
  /// the two ends of a drawn line, which does not care which end is which.
  /// </summary>
  protected static void GetTickMarkEndpoints(TickMarkType type, double edge, double width, int direction,
    out double start, out double end)
  {
    switch (type)
    {
      case TickMarkType.Inside:
        start = edge;
        end = edge - direction * width;
        break;

      case TickMarkType.Outside:
        start = edge;
        end = edge + direction * width;
        break;

      case TickMarkType.Cross:
        start = edge + direction * width;
        end = edge - direction * width;
        break;

      default: // TickMarkType.None
        start = 0;
        end = 0;
        break;
    }
  }
}
