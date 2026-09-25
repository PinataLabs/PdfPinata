using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The angles of an exploded pie's wedges, read back from the paths the page fills.
/// </summary>
/// <remarks>
///   <c>XGraphics.DrawPie</c> writes a wedge as a move to the centre of its circle, a line out to
///   where its arc starts, and the Bézier curves of the arc - so the first point of the path is
///   the centre, the second the start of the arc and the last its end, and the angle between the
///   two radii is the wedge's sweep. The page's y axis runs the other way from the one the wedge
///   was drawn in, which turns an angle round but does not change its size.
/// </remarks>
public class PieExplodedPlotAreaTests
{
    /// <summary>
    ///   Each wedge of an exploded pie gives up a gap of two degrees to the one before it, so that
    ///   the wedges stand apart. The gap used to move the wedge round by two degrees without taking
    ///   anything from its sweep - <c>Math.Max(sweep, sweep - gap)</c> is always the sweep - so
    ///   every wedge still met the next, and the whole pie was merely turned.
    /// </summary>
    [Fact]
    public void EachWedgeIsNarrowerThanItsShareByTheGap()
    {
        var wedges = Wedges(Charts.Of(ChartType.PieExploded2D, 1.0, 1.0, 2.0));

        wedges.Select(wedge => wedge.Sweep).Should().SatisfyRespectively(
            sweep => sweep.Should().BeApproximately(90 - Gap, AngleTolerance),
            sweep => sweep.Should().BeApproximately(90 - Gap, AngleTolerance),
            sweep => sweep.Should().BeApproximately(180 - Gap, AngleTolerance));
    }

    /// <summary>
    ///   The gap is shared evenly between the two sides of a wedge, so that the wedge is pushed out
    ///   along the middle of what is drawn of it rather than a degree to one side, and neighbouring
    ///   wedges are the gap apart.
    /// </summary>
    [Fact]
    public void NeighbouringWedgesAreTheGapApart()
    {
        var wedges = Wedges(Charts.Of(ChartType.PieExploded2D, 1.0, 1.0, 1.0, 1.0));

        for (var idx = 0; idx < wedges.Count; idx++)
        {
            var next = wedges[(idx + 1) % wedges.Count];
            Between(wedges[idx].End, next.Start).Should().BeApproximately(Gap, AngleTolerance);
        }
    }

    /// <summary>
    ///   A share narrower than two gaps keeps half of itself, rather than being swallowed by the gap
    ///   and leaving a value that is in the data and nowhere on the page. The share beside it, at
    ///   well over half the pie, gives up the whole gap like any other.
    /// </summary>
    [Fact]
    public void AShareNarrowerThanTheGapIsStillDrawn()
    {
        // One part in 400 is 0.9 of a degree, and the other 399 are 359.1.
        var wedges = Wedges(Charts.Of(ChartType.PieExploded2D, 399.0, 1.0));

        wedges.Select(wedge => wedge.Sweep).Should().SatisfyRespectively(
            sweep => sweep.Should().BeApproximately(359.1 - Gap, AngleTolerance),
            sweep => sweep.Should().BeApproximately(0.45, AngleTolerance));
    }

    /// <summary>
    ///   A pie of one share has no neighbour to stand apart from, and is drawn whole rather than
    ///   with a notch the width of the gap.
    /// </summary>
    [Fact]
    public void ASingleShareIsTheWholePie()
    {
        var wedge = Wedges(Charts.Of(ChartType.PieExploded2D, 5.0)).Should().ContainSingle().Subject;

        wedge.Sweep.Should().BeApproximately(360, AngleTolerance);
    }

    /// <summary>
    ///   Filled wedges, in the order the pie draws them, each as the direction of its two radii
    ///   and the angle its arc sweeps through, all in degrees.
    /// </summary>
    /// <remarks>
    ///   The sweep is followed along the arc rather than read off the two radii, which cannot tell
    ///   a wedge of 357 degrees from one of 3. The arc is written a quarter of a circle or less to
    ///   each curve, so the end points of the curves - the first point after the centre, then every
    ///   third - are never half a turn apart, and the turn from each to the next is unambiguous.
    /// </remarks>
    private static List<(double Start, double End, double Sweep)> Wedges(Chart chart)
    {
        var page = Drawn.Page(chart);
        return
        [
            .. PaintedPaths.On(page)
                .Where(path => path.Filled && path.Curves > 0)
                .Select(path =>
                {
                    var centre = path.Points[0];
                    var directions = new List<double>();
                    for (var idx = 1; idx < path.Points.Count; idx += 3)
                        directions.Add(Direction(centre, path.Points[idx]));

                    var sweep = 0.0;
                    for (var idx = 1; idx < directions.Count; idx++)
                        sweep += Turn(directions[idx - 1], directions[idx]);

                    return (directions[0], directions[^1], Math.Abs(sweep));
                })
        ];
    }

    private static double Direction((double X, double Y) from, (double X, double Y) to) =>
        Math.Atan2(to.Y - from.Y, to.X - from.X) * 180 / Math.PI;

    /// <summary>The signed turn from one direction to another, the short way round.</summary>
    private static double Turn(double from, double to)
    {
        var turn = (to - from) % 360;
        if (turn > 180)
            turn -= 360;
        else if (turn <= -180)
            turn += 360;
        return turn;
    }

    /// <summary>The angle from one direction round to another, the short way, however the page's axes turn them.</summary>
    private static double Between(double from, double to) => Math.Abs(Turn(from, to));

    /// <summary>The gap the renderer leaves between one wedge and the next, in degrees.</summary>
    private const double Gap = 2;

    /// <summary>
    ///   The content stream writes a point to four significant figures, which on a pie a hundred
    ///   points across places a radius to within a few hundredths of a degree.
    /// </summary>
    private const double AngleTolerance = 0.1;
}
