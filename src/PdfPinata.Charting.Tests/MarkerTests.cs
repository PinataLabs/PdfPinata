using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The marks a line chart puts on each of its data points.
/// </summary>
/// <remarks>
///   <c>MarkerRenderer.Draw</c> builds one closed path per data point in the shape the series
///   asks for, centred on the point, and paints it twice: filled in the marker's background colour,
///   then outlined half a point wide in its foreground colour. <c>LineChartRenderer.InitSeries</c>
///   decides what it is asked for when the series says nothing - a black outline, a fill in the
///   line's own colour, seven points across, and a style taken from the series' place in the chart.
///
///   The shape is read back as the path the page paints, through <see cref="PaintedPaths"/>: how
///   many corners it has, how wide and tall it is, and where it sits against the point the line
///   passes through - which is read off the line itself, through <see cref="StrokedLines"/>.
///   Positions in the content stream are y-up, so a point's top is its largest y.
/// </remarks>
public class MarkerTests
{
    /// <summary>The marker size the shape tests ask for: large, so the geometry is easy to read.</summary>
    private const double Size = 10;

    private static readonly string Background = PaintedRectangles.ColourOf(XColors.Blue);
    private static readonly string Foreground = PaintedRectangles.ColourOf(XColors.Red);

    // ----- the shapes -----

    /// <summary>
    ///   Each style is its own polygon: the number of corners tells them apart, and the extent
    ///   says the size was honoured. A triangle is equilateral, so it is wider than it is tall; a
    ///   dash is a third as tall as it is wide; a star's outer points lie on a circle of the
    ///   marker's size, so it falls a little short of it both ways.
    /// </summary>
    [Theory]
    [InlineData(MarkerStyle.Square, 4, 1.0, 1.0)]
    [InlineData(MarkerStyle.Diamond, 4, 1.0, 1.0)]
    [InlineData(MarkerStyle.Triangle, 3, 1.1547, 1.0)]
    [InlineData(MarkerStyle.Plus, 12, 1.0, 1.0)]
    [InlineData(MarkerStyle.X, 12, 1.0, 1.0)]
    [InlineData(MarkerStyle.Dash, 4, 1.0, 1.0 / 3)]
    [InlineData(MarkerStyle.Star, 10, 0.9511, 0.9045)]
    public void EachMarkerStyleIsAPolygonOfItsOwnShape(MarkerStyle style, int corners, double widthPerSize,
        double heightPerSize)
    {
        var page = Drawn.Page(LineWith(style, 1.0, 3.0, 2.0));
        var markers = PaintedPaths.FilledIn(page, Background);

        markers.Should().HaveCount(3, "one marker per data point");
        foreach (var marker in markers)
        {
            marker.Curves.Should().Be(0);
            marker.DistinctPoints.Should().Be(corners);
            marker.Width.Should().BeApproximately(Size * widthPerSize, 0.01);
            marker.Height.Should().BeApproximately(Size * heightPerSize, 0.01);
        }
    }

    [Fact]
    public void ACircleMarkerIsDrawnWithCurvesAsWideAndTallAsItsSize()
    {
        var page = Drawn.Page(LineWith(MarkerStyle.Circle, 1.0, 3.0, 2.0));
        var markers = PaintedPaths.FilledIn(page, Background);

        markers.Should().HaveCount(3);
        foreach (var marker in markers)
        {
            marker.Curves.Should().Be(4, "an ellipse is drawn as four quarter arcs");
            marker.Width.Should().BeApproximately(Size, 0.01);
            marker.Height.Should().BeApproximately(Size, 0.01);
        }
    }

    /// <summary>
    ///   A diamond is a square turned on its corner: its corners are at the middle of each side of
    ///   the square it fits in, not at that square's corners.
    /// </summary>
    [Fact]
    public void ADiamondHasItsCornersAtTheMiddleOfEachSide()
    {
        var marker = PaintedPaths.FilledIn(Drawn.Page(LineWith(MarkerStyle.Diamond, 1.0, 3.0)), Background)[0];

        marker.Points.Should().Contain(p => Near(p.X, marker.CentreX) && Near(p.Y, marker.Top));
        marker.Points.Should().Contain(p => Near(p.X, marker.CentreX) && Near(p.Y, marker.Bottom));
        marker.Points.Should().Contain(p => Near(p.X, marker.Left) && Near(p.Y, marker.CentreY));
        marker.Points.Should().Contain(p => Near(p.X, marker.Right) && Near(p.Y, marker.CentreY));
        marker.Points.Should().NotContain(p => Near(p.X, marker.Left) && Near(p.Y, marker.Top));
    }

    /// <summary>
    ///   A triangle points up: one corner at the top, over the point, and two at the bottom.
    /// </summary>
    [Fact]
    public void ATrianglePointsUp()
    {
        var marker = PaintedPaths.FilledIn(Drawn.Page(LineWith(MarkerStyle.Triangle, 1.0, 3.0)), Background)[0];

        marker.Points.Count(p => Near(p.Y, marker.Top)).Should().BeGreaterThan(0);
        marker.Points.Where(p => Near(p.Y, marker.Top)).Should().OnlyContain(p => Near(p.X, marker.CentreX));
        marker.Points.Where(p => Near(p.Y, marker.Bottom)).Select(p => Math.Round(p.X, 2)).Distinct()
            .Should().HaveCount(2);
    }

    /// <summary>
    ///   Every marker is centred across its data point, and every one but the star is centred up
    ///   and down it too. The star is drawn from its top point, which sits half the marker's size
    ///   above the data point, and its lower points reach less far below.
    /// </summary>
    [Theory]
    [InlineData(MarkerStyle.Square, 0.5)]
    [InlineData(MarkerStyle.Diamond, 0.5)]
    [InlineData(MarkerStyle.Triangle, 0.5)]
    [InlineData(MarkerStyle.Plus, 0.5)]
    [InlineData(MarkerStyle.X, 0.5)]
    [InlineData(MarkerStyle.Circle, 0.5)]
    [InlineData(MarkerStyle.Dash, 1.0 / 6)]
    [InlineData(MarkerStyle.Star, 0.5)]
    public void AMarkerSitsOnItsDataPoint(MarkerStyle style, double topAbovePointPerSize)
    {
        var page = Drawn.Page(LineWith(style, 1.0, 3.0, 2.0));
        var points = DataPoints(page, Background);
        var markers = PaintedPaths.FilledIn(page, Background);

        markers.Should().HaveCount(points.Count);
        for (var idx = 0; idx < points.Count; idx++)
        {
            markers[idx].CentreX.Should().BeApproximately(points[idx].X, 0.01);
            markers[idx].Top.Should().BeApproximately(points[idx].Y + Size * topAbovePointPerSize, 0.01);
        }
    }

    [Fact]
    public void AMarkerStyledNoneDrawsNoMarkerButStillTheLine()
    {
        var page = Drawn.Page(LineWith(MarkerStyle.None, 1.0, 3.0, 2.0));

        PaintedPaths.FilledIn(page, Background).Should().BeEmpty();
        PaintedPaths.StrokedIn(page, Foreground).Should().BeEmpty();
        DataPoints(page, Background).Should().HaveCount(3, "the line is drawn through every point regardless");
    }

    // ----- colours and size -----

    /// <summary>
    ///   Each marker is filled in its background colour and then outlined, half a point wide, in
    ///   its foreground colour along exactly the same path.
    /// </summary>
    [Fact]
    public void AMarkerIsFilledInItsBackgroundAndOutlinedInItsForeground()
    {
        var page = Drawn.Page(LineWith(MarkerStyle.Square, 1.0, 3.0, 2.0));
        var paths = PaintedPaths.On(page);

        var fills = paths.Where(p => p.Filled && p.FillColour == Background).ToList();
        var outlines = paths.Where(p => p.Stroked && p.StrokeColour == Foreground).ToList();

        fills.Should().HaveCount(3);
        outlines.Should().HaveCount(3);
        for (var idx = 0; idx < fills.Count; idx++)
        {
            outlines[idx].Filled.Should().BeFalse("the outline is a stroke of its own over the fill");
            outlines[idx].LineWidth.Should().BeApproximately(0.5, 0.001);
            outlines[idx].Points.Should().Equal(fills[idx].Points);
            IndexOf(paths, outlines[idx]).Should().BeGreaterThan(IndexOf(paths, fills[idx]),
                "the outline goes on top of the fill");
        }
    }

    /// <summary>
    ///   Left to itself a marker is outlined in black and filled in the colour of the line it is
    ///   on, which comes from the chart's own palette.
    /// </summary>
    [Fact]
    public void AMarkerGivenNoColoursIsOutlinedInBlackAndFilledInTheLinesColour()
    {
        var chart = Charts.Of(ChartType.Line, 1.0, 3.0, 2.0);
        chart.SeriesCollection[0].MarkerStyle = MarkerStyle.Square;
        chart.SeriesCollection[0].MarkerSize = Size;

        var page = Drawn.Page(chart);
        var lineColour = SeriesLines(page).Select(line => line.Colour).Distinct().Single();

        lineColour.Should().NotBe(PaintedRectangles.Black);
        PaintedPaths.FilledIn(page, lineColour).Should().HaveCount(3);
        PaintedPaths.On(page)
            .Where(p => p.Stroked && p.StrokeColour == PaintedRectangles.Black && Math.Abs(p.LineWidth - 0.5) < 0.001)
            .Should().HaveCount(3);
    }

    /// <summary>
    ///   A marker's background colour is also the line's: a series that names one is drawn in it
    ///   end to end, rather than in the palette colour its place in the chart would have given it.
    /// </summary>
    [Fact]
    public void AMarkerBackgroundColourColoursTheLineToo()
    {
        var page = Drawn.Page(LineWith(MarkerStyle.Square, 1.0, 3.0, 2.0));

        SeriesLines(page).Should().HaveCount(2).And.OnlyContain(line => line.Colour == Background);
    }

    [Fact]
    public void AMarkerGivenNoSizeIsSevenPointsAcross()
    {
        var chart = Charts.Of(ChartType.Line, 1.0, 3.0);
        chart.SeriesCollection[0].MarkerStyle = MarkerStyle.Square;
        chart.SeriesCollection[0].MarkerBackgroundColor = XColors.Blue;

        var markers = PaintedPaths.FilledIn(Drawn.Page(chart), Background);

        markers.Should().HaveCount(2).And.OnlyContain(m => Near(m.Width, 7) && Near(m.Height, 7));
    }

    /// <summary>
    ///   The size is an <see cref="XUnit"/>, and a marker is drawn at its length in points, so the
    ///   same marker given in millimetres comes out at the same size as in points.
    /// </summary>
    [Fact]
    public void AMarkerSizeGivenInMillimetresIsDrawnAtItsLengthInPoints()
    {
        var chart = LineWith(MarkerStyle.Square, 1.0, 3.0);
        chart.SeriesCollection[0].MarkerSize = XUnit.FromMillimeter(5);

        var markers = PaintedPaths.FilledIn(Drawn.Page(chart), Background);

        markers.Should().HaveCount(2).And.OnlyContain(m => Near(m.Width, 5 * 72 / 25.4));
    }

    // ----- styles nobody chose -----

    /// <summary>
    ///   A series that names no style takes one from its place in the chart - circle, then dash,
    ///   then diamond - so that series drawn in similar colours can still be told apart.
    /// </summary>
    [Fact]
    public void SeriesThatNameNoStyleTakeTheStylesInTurn()
    {
        var colours = new[] { XColors.Blue, XColors.Green, XColors.Purple };
        var chart = Charts.OfSeries(ChartType.Line, new[] { 1.0, 2.0 }, new[] { 2.0, 3.0 }, new[] { 3.0, 1.0 });
        for (var idx = 0; idx < colours.Length; idx++)
        {
            chart.SeriesCollection[idx].MarkerBackgroundColor = colours[idx];
            chart.SeriesCollection[idx].MarkerSize = Size;
        }

        var page = Drawn.Page(chart);
        var first = PaintedPaths.FilledIn(page, PaintedRectangles.ColourOf(colours[0]))[0];
        var second = PaintedPaths.FilledIn(page, PaintedRectangles.ColourOf(colours[1]))[0];
        var third = PaintedPaths.FilledIn(page, PaintedRectangles.ColourOf(colours[2]))[0];

        first.Curves.Should().BeGreaterThan(0, "the first series is marked with circles");
        second.Curves.Should().Be(0);
        second.Height.Should().BeApproximately(Size / 3, 0.01, "the second series is marked with dashes");
        third.DistinctPoints.Should().Be(4);
        third.Height.Should().BeApproximately(Size, 0.01, "the third series is marked with diamonds");
        third.Points.Should().Contain(p => Near(p.X, third.CentreX) && Near(p.Y, third.Top));
    }

    // ----- helpers -----

    /// <summary>
    ///   A line chart of one series in the given marker style, at <see cref="Size"/>, filled blue
    ///   and outlined red so that the markers are the only paths painted in either colour.
    /// </summary>
    private static Chart LineWith(MarkerStyle style, params double[] values)
    {
        var chart = Charts.Of(ChartType.Line, values);
        var series = chart.SeriesCollection[0];
        series.MarkerStyle = style;
        series.MarkerSize = Size;
        series.MarkerForegroundColor = XColors.Red;
        series.MarkerBackgroundColor = XColors.Blue;
        return chart;
    }

    /// <summary>
    ///   The segments of the series' line. The plot area strokes it at a tenth of the width of
    ///   anything else on the chart, which is what tells it from the axes and the outlines.
    /// </summary>
    private static IReadOnlyList<StrokedLines.Line> SeriesLines(PdfPage page) =>
        StrokedLines.Of(page).Where(line => line.Width < 0.2).ToList();

    /// <summary>The points the series' line passes through, in order, read off the line itself.</summary>
    private static IReadOnlyList<(double X, double Y)> DataPoints(PdfPage page, string colour)
    {
        var segments = SeriesLines(page).Where(line => line.Colour == colour).ToList();
        var points = new List<(double X, double Y)>();
        if (segments.Count == 0)
            return points;

        points.Add((segments[0].X1, segments[0].Y1));
        foreach (var segment in segments)
            points.Add((segment.X2, segment.Y2));
        return points;
    }

    private static int IndexOf(IReadOnlyList<PaintedPaths.Path> paths, PaintedPaths.Path path)
    {
        for (var idx = 0; idx < paths.Count; idx++)
        {
            if (ReferenceEquals(paths[idx], path))
                return idx;
        }
        return -1;
    }

    private static bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.01;
}
