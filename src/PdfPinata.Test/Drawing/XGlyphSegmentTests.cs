using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <c>XGlyphSegment.QuadraticTo</c> is the one copy of the quadratic-to-cubic conversion both
///   shipped glyph-outline providers used to carry their own of. These pin the arithmetic directly,
///   with no font and no backend, so a slip in it is reported as the conversion being wrong rather
///   than as a glyph that is slightly out of place.
/// </summary>
public class XGlyphSegmentTests
{
    private const double Precision = 1e-12;

    [Fact]
    public void AQuadraticIsACurveEndingWhereTheQuadraticEnds()
    {
        var segment = XGlyphSegment.QuadraticTo(new XPoint(0, 0), new XPoint(3, 6), new XPoint(9, 0));

        segment.Kind.Should().Be(XGlyphSegmentKind.Curve);
        segment.End.Should().Be(new XPoint(9, 0));
    }

    [Fact]
    public void TheControlsAreTwoThirdsOfTheWayFromEachEndTowardsTheQuadraticsControl()
    {
        // p0 + 2/3(q - p0) = (0,0) + 2/3(3,6) = (2,4); p2 + 2/3(q - p2) = (9,0) + 2/3(-6,6) = (5,4).
        var segment = XGlyphSegment.QuadraticTo(new XPoint(0, 0), new XPoint(3, 6), new XPoint(9, 0));

        segment.Control1.X.Should().BeApproximately(2, Precision);
        segment.Control1.Y.Should().BeApproximately(4, Precision);
        segment.Control2.X.Should().BeApproximately(5, Precision);
        segment.Control2.Y.Should().BeApproximately(4, Precision);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.125)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void TheCubicTracesTheQuadraticExactlyRatherThanApproximately(double t)
    {
        // Off the origin and in every direction, so a conversion that forgot the start point, or
        // measured towards the wrong end, cannot pass by accident.
        var start = new XPoint(-12.5, 40);
        var control = new XPoint(31, -7.25);
        var end = new XPoint(18, 22.5);

        var segment = XGlyphSegment.QuadraticTo(start, control, end);

        var quadratic = Quadratic(start, control, end, t);
        var cubic = Cubic(start, segment.Control1, segment.Control2, segment.End, t);
        cubic.X.Should().BeApproximately(quadratic.X, Precision);
        cubic.Y.Should().BeApproximately(quadratic.Y, Precision);
    }

    [Fact]
    public void AQuadraticWhoseControlLiesOnTheLineIsAStraightCurve()
    {
        var segment = XGlyphSegment.QuadraticTo(new XPoint(0, 0), new XPoint(3, 3), new XPoint(6, 6));

        segment.Control1.X.Should().BeApproximately(2, Precision);
        segment.Control1.Y.Should().BeApproximately(2, Precision);
        segment.Control2.X.Should().BeApproximately(4, Precision);
        segment.Control2.Y.Should().BeApproximately(4, Precision);
    }

    private static XPoint Quadratic(XPoint p0, XPoint q, XPoint p2, double t)
    {
        var u = 1 - t;
        return new XPoint(
            u * u * p0.X + 2 * u * t * q.X + t * t * p2.X,
            u * u * p0.Y + 2 * u * t * q.Y + t * t * p2.Y);
    }

    private static XPoint Cubic(XPoint p0, XPoint c1, XPoint c2, XPoint p3, double t)
    {
        var u = 1 - t;
        return new XPoint(
            u * u * u * p0.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * p3.X,
            u * u * u * p0.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * p3.Y);
    }
}
