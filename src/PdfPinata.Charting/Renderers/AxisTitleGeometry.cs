using PdfPinata.Drawing;

namespace PdfPinata.Charting.Renderers;

/// <summary>
/// Where a rotated axis title caption is centred and by how much it is turned.
/// </summary>
/// <remarks>
/// <see cref="AxisTitleRenderer.Draw"/> reaches this only by turning the surface and drawing on
/// it, which is what put a rotated caption out of reach of anything but comparing content-stream
/// bytes - <c>ShownText</c> follows the text matrix, not the transformation matrix, and a rotated
/// caption is drawn under a rotate of its own. This is the arithmetic behind that rotate, kept
/// apart from the drawing so a test can ask for it directly. See
/// docs/specs/charting-renderer-seam.md.
/// </remarks>
internal readonly struct RotatedCaptionLayout
{
  internal RotatedCaptionLayout(XPoint anchor, double rotationDegrees)
  {
    Anchor = anchor;
    RotationDegrees = rotationDegrees;
  }

  /// <summary>The point the caption is centred on, in the strip's own coordinates.</summary>
  internal XPoint Anchor { get; }

  /// <summary>
  /// The rotation the caption is turned by - the same value and the same sign
  /// <c>XGraphics.RotateTransform</c> is then called with.
  /// </summary>
  internal double RotationDegrees { get; }
}

/// <summary>
/// Computes where a rotated axis title caption lands, given the strip an axis has reserved for
/// it and the caption's own size, orientation and alignment.
/// </summary>
/// <remarks>
/// The seam <c>docs/specs/charting-renderer-seam.md</c> asks for: renderer parameters in,
/// computed geometry out, no <c>XGraphics</c> and nothing drawn.
/// </remarks>
internal static class AxisTitleGeometry
{
  /// <summary>
  /// The centre a rotated caption is placed on and the angle it is turned by, for the given strip,
  /// caption size, orientation and alignment.
  /// </summary>
  internal static RotatedCaptionLayout RotatedCaption(
    XRect strip, XSize captionSize, double orientationDegrees,
    HorizontalAlignment alignment, VerticalAlignment verticalAlignment)
  {
    double x;
    switch (alignment)
    {
      case HorizontalAlignment.Center:
        x = strip.X + strip.Width / 2;
        break;

      case HorizontalAlignment.Right:
        x = strip.X + strip.Width - captionSize.Width / 2;
        break;

      case HorizontalAlignment.Left:
      default:
        x = strip.X + captionSize.Width / 2;
        break;
    }

    double y;
    switch (verticalAlignment)
    {
      case VerticalAlignment.Center:
        y = strip.Y + strip.Height / 2;
        break;

      case VerticalAlignment.Bottom:
        y = strip.Y + strip.Height - captionSize.Height / 2;
        break;

      case VerticalAlignment.Top:
      default:
        y = strip.Y + captionSize.Height / 2;
        break;
    }

    return new RotatedCaptionLayout(new XPoint(x, y), -orientationDegrees);
  }
}
