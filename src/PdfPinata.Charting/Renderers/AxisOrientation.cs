using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Which way an axis runs across the page. Everything an axis renderer computes is the same
/// arithmetic for both; this is the one piece of data that says which page coordinate a value
/// becomes, which way a tick points, and which dimension a tick label is measured against.
/// </summary>
internal enum AxisOrientation
{
  /// <summary>The axis runs left to right, as a column chart's category axis does.</summary>
  Horizontal,

  /// <summary>The axis runs bottom to top, as a bar chart's category axis does.</summary>
  Vertical
}

/// <summary>
/// Where a point of a chart with a category and a value axis lies in its plot area's chart space,
/// given which way the category axis runs. A column chart's category axis runs across and its value
/// axis up, so a point is (category, value); a bar chart is the same chart turned on its side, and a
/// point is (value, category). The plot area's matrix takes it from there to the page, so this is
/// the one place the two orientations differ in where a column, a bar, a gridline or the zero line
/// is drawn.
/// </summary>
internal static class PlotOrientation
{
  /// <summary>
  /// The point in chart space for a position on the category axis and one on the value axis, the
  /// category axis running as <paramref name="categoryAxis"/> says.
  /// </summary>
  internal static XPoint At(this AxisOrientation categoryAxis, double category, double value)
    => categoryAxis == AxisOrientation.Horizontal ? new XPoint(category, value) : new XPoint(value, category);
}
