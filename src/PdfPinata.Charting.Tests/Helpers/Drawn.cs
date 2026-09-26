using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;

namespace PdfPinata.Charting.Tests.Helpers;

/// <summary>
///   Draws a chart and hands the page back as a reader sees it.
/// </summary>
/// <remarks>
///   Every renderer under test is internal, and this repository carries no
///   <c>InternalsVisibleTo</c>. They are reached the way anything else reaches them: by handing a
///   <see cref="Chart"/> to a <see cref="ChartFrame"/> and letting it pick the renderer, run
///   Init/Format/Draw and emit content-stream operators. That is also the only route a caller of
///   the package has, so what is covered here is covered as it is actually used.
///
///   Saved and reopened rather than read off the renderer, so that what the assertions look at is
///   the content stream as it was written rather than as it stood while it was being built.
///
///   The frame is drawn with <see cref="ChartFrame.DrawChart"/> rather than
///   <see cref="ChartFrame.Draw"/>. Draw puts a rounded shadow, a gradient-filled border and a
///   translate around the chart before it starts, and every one of those adds operators that an
///   assertion about the chart would then have to step over. DrawChart adds a translate and
///   nothing else.
/// </remarks>
internal static class Drawn
{
    /// <summary>The size the chart is given, unless a test asks for another.</summary>
    /// <remarks>
    ///   Large enough that a plot area survives the axes taking their room out of it, and square
    ///   enough that a bar chart and a column chart both have somewhere to put their labels. A
    ///   frame too small for its axes draws its axes and nothing inside them, which
    ///   <c>ColumnPlotAreaTests.AFrameTooSmallForItsAxesDrawsNothingInThePlotArea</c> covers.
    /// </remarks>
    internal const double DefaultWidth = 400;

    /// <summary>The height the chart is given, unless a test asks for another.</summary>
    internal const double DefaultHeight = 300;

    /// <summary>The chart, drawn on a page of its own, saved and read back.</summary>
    internal static PdfPage Page(Chart chart) => Page(chart, DefaultWidth, DefaultHeight);

    /// <summary>The chart, drawn at the given size on a page of its own, saved and read back.</summary>
    internal static PdfPage Page(Chart chart, double width, double height)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = width + 2 * Margin;
        page.Height = height + 2 * Margin;

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            var frame = new ChartFrame(new XRect(Margin, Margin, width, height));
            frame.Add(chart);
            frame.DrawChart(gfx);
        }

        return document.Reopened().Pages[0];
    }

    /// <summary>
    ///   The gap left between the frame and the edge of the page, so that a chart drawing at its
    ///   own origin is not also drawing at the page's.
    /// </summary>
    private const double Margin = 20;
}
