using AwesomeAssertions;
using PdfPinata.Charting.Renderers;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   Where a rotated axis title caption lands, asked of <c>AxisTitleGeometry</c> directly rather
///   than through a chart, a save, a reparse and a content-stream comparison.
/// </summary>
/// <remarks>
///   docs/specs/charting-renderer-seam.md names this the acceptance test for the seam: a rotated
///   caption is drawn under a rotate of its own, so <c>ShownText</c> - which follows the text
///   matrix rather than the transformation matrix - cannot see where it landed, and
///   <c>AxisTitleTests</c> compared whole content streams instead. These tests ask the same
///   questions of the geometry alone: a position and an angle, with no page, no
///   <c>XGraphics</c> and nothing drawn.
/// </remarks>
public class AxisTitleGeometryTests
{
    /// <summary>
    ///   The strip a real axis reserves for a rotated caption is exactly as wide as the caption
    ///   itself - that is how much room the axis took from the plot area for it - so this is the
    ///   shape every one of these tests hands to the geometry.
    /// </summary>
    private static readonly XSize Caption = new(90, 12);

    [Fact]
    public void ACentredCaptionSitsInTheMiddleOfItsStrip()
    {
        var strip = new XRect(10, 20, 200, 80);

        var layout = AxisTitleGeometry.RotatedCaption(
            strip, Caption, 90, HorizontalAlignment.Center, VerticalAlignment.Center);

        layout.Anchor.Should().Be(new XPoint(strip.X + strip.Width / 2, strip.Y + strip.Height / 2));
    }

    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Right)]
    public void AligningARotatedCaptionAcrossTheAxisMovesItNowhere(HorizontalAlignment alignment)
    {
        var strip = new XRect(0, 0, Caption.Width, 40);

        var moved = AxisTitleGeometry.RotatedCaption(
            strip, Caption, 90, alignment, VerticalAlignment.Center);
        var centred = AxisTitleGeometry.RotatedCaption(
            strip, Caption, 90, HorizontalAlignment.Center, VerticalAlignment.Center);

        moved.Anchor.Should().Be(centred.Anchor);
    }

    [Fact]
    public void EachVerticalAlignmentPutsARotatedCaptionSomewhereOfItsOwn()
    {
        var strip = new XRect(0, 0, Caption.Width, 100);

        double AnchorY(VerticalAlignment alignment) => AxisTitleGeometry.RotatedCaption(
            strip, Caption, 90, HorizontalAlignment.Center, alignment).Anchor.Y;

        var positions = new[]
        {
            AnchorY(VerticalAlignment.Top),
            AnchorY(VerticalAlignment.Center),
            AnchorY(VerticalAlignment.Bottom)
        };

        positions.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-45)]
    public void TheCaptionIsTurnedByTheNegationOfItsOrientation(double orientationDegrees)
    {
        var strip = new XRect(0, 0, Caption.Width, 40);

        var layout = AxisTitleGeometry.RotatedCaption(
            strip, Caption, orientationDegrees, HorizontalAlignment.Center, VerticalAlignment.Center);

        layout.RotationDegrees.Should().Be(-orientationDegrees);
    }
}
