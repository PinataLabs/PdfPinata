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
