using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XGraphics"/> is the drawing surface: the whole library, PinataLayout included, reaches
///   the page through it. Almost every shape it can draw comes in three spellings - stroke it,
///   fill it, or do both - and each of those in two more, taking a rectangle or four numbers. That
///   is a lot of overloads whose only job is to forward, and the way they go wrong is by
///   forwarding to the wrong place or by disagreeing with each other about what is required.
///   <para>
///   So the tests here are mostly of the form "these two spellings put the same ink on the page",
///   read back out of the content stream, plus the argument checks that decide whether a caller
///   gets an exception or a silently empty page.
///   </para>
/// </summary>
public class XGraphicsSurfaceTests
{
    static readonly XPoint[] ThreePoints =
    {
        new(100, 100), new(200, 150), new(300, 100)
    };

    static readonly XPoint[] FourBezierPoints =
    {
        new(100, 100), new(150, 50), new(250, 50), new(300, 100)
    };

    static PdfPage PageShowing(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            draw(gfx);
        return page;
    }

    /// <summary>
    ///   The shape a page draws, as text: every operator that builds geometry, and nothing that
    ///   decides how it is painted. That is what "the same shape" means throughout this class -
    ///   stroking and filling the same rectangle are the same shape painted two ways, and the
    ///   comparison has to see the first part and not the second.
    /// </summary>
    /// <remarks>
    ///   Read out of the content stream rather than through <see cref="PathGeometry"/>, because a
    ///   rectangle is written as a single <c>re</c> operator and never becomes path points at all.
    /// </remarks>
    static string ShapeOf(Action<XGraphics> draw)
    {
        var lines = ContentOf(PageShowing(draw)).Split('\n')
            .Select(line => line.Trim())
            .Where(line => Regex.IsMatch(line, @"(^|\s)(re|m|l|c|v|y|h)$"));
        return string.Join("\n", lines);
    }

    static int CountOf(string shape, string @operator) =>
        Regex.Count(shape, @"(^|\s)" + @operator + "$", RegexOptions.Multiline);

    /// <summary>
    ///   The graphics state operators a page writes, in order: every literal <c>q</c> and <c>Q</c>
    ///   and nothing else. <see cref="ShapeOf"/> reads the geometry operators the same way; this is
    ///   that reading applied to the pair that opens and closes a saved state.
    /// </summary>
    /// <remarks>
    ///   Read as bytes rather than through <see cref="XGraphics.GraphicsStateLevel"/>, because that
    ///   counter lives above the renderer and would say the same thing whatever the renderer wrote.
    /// </remarks>
    static string StateOf(Action<XGraphics> draw) =>
        string.Concat(ContentOf(PageShowing(draw)).Split('\n')
            .Select(line => line.Trim())
            .Where(line => line is "q" or "Q"));

    /// <summary>
    ///   The graphics state nesting depth each shape is drawn at, one number per <c>re</c> operator,
    ///   in the order they are drawn: how many <c>q</c> operators stand open above that shape.
    /// </summary>
    static int[] DepthsOf(Action<XGraphics> draw)
    {
        var depths = new List<int>();
        var depth = 0;
        foreach (var line in ContentOf(PageShowing(draw)).Split('\n').Select(line => line.Trim()))
        {
            if (line is "q")
                depth++;
            else if (line is "Q")
                depth--;
            else if (Regex.IsMatch(line, @"(^|\s)re$"))
                depths.Add(depth);
        }
        return depths.ToArray();
    }

    static int PointCount(Action<XGraphics> draw) => PathGeometry.PointsOf(PageShowing(draw)).Count;

    static XRect Bounds(Action<XGraphics> draw) => PathGeometry.BoundsOf(PageShowing(draw));

    static string ContentOf(PdfPage page) => Encoding.ASCII.GetString(PageContent.Of(page));

    static XGraphics OnAPage() => XGraphics.FromPdfPage(new PdfDocument().AddPage());

    // ----- the surface itself --------------------------------------------------------------------

    [Fact]
    public void ASurfaceOnAPageIsInPointAndTheSizeOfThatPage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = 400;
        page.Height = 600;

        using var gfx = XGraphics.FromPdfPage(page);

        gfx.PageUnit.Should().Be(XGraphicsUnit.Point);
        gfx.PageSize.Width.Should().BeApproximately(400, 1e-6);
        gfx.PageSize.Height.Should().BeApproximately(600, 1e-6);
        gfx.PageOrigin.Should().Be(new XPoint(0, 0));
        gfx.PageDirection.Should().Be(XPageDirection.Downwards);
        gfx.PdfPage.Should().BeSameAs(page);
        gfx.GraphicsStateLevel.Should().Be(0);
    }

    [Fact]
    public void TheOnlyPageDirectionAndOriginThereAreCanBeSetAndNothingElseCan()
    {
        using var gfx = OnAPage();

        var setDirection = () => gfx.PageDirection = XPageDirection.Downwards;
        var setOrigin = () => gfx.PageOrigin = new XPoint(0, 0);
        // Obsolete on purpose - the point of the test is that asking for it is refused.
#pragma warning disable CS0618
        var setUpwards = () => gfx.PageDirection = XPageDirection.Upwards;
#pragma warning restore CS0618
        var moveOrigin = () => gfx.PageOrigin = new XPoint(10, 10);

        setDirection.Should().NotThrow();
        setOrigin.Should().NotThrow();
        setUpwards.Should().Throw<NotImplementedException>();
        moveOrigin.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void ASurfaceCanBeMadeJustToMeasureWithAndDrawsNowhere()
    {
        // PinataLayout measures text long before it knows what page it will land on, so it needs a
        // surface with no page behind it.
        using var gfx = XGraphics.CreateMeasureContext(
            new XSize(400, 600), XGraphicsUnit.Point, XPageDirection.Downwards);

        gfx.PageSize.Should().Be(new XSize(400, 600));
        gfx.MeasureString("Handles", new XFont("Arial", 12)).Width.Should().BeGreaterThan(0);

        // Nothing to draw on, and asking anyway must not throw - the shape is simply discarded.
        var draw = () => gfx.DrawRectangle(XPens.Black, 10, 10, 100, 100);
        draw.Should().NotThrow();
    }

    [Fact]
    public void DisposingASurfaceTwiceIsHarmless()
    {
        var gfx = OnAPage();

        gfx.Dispose();
        var again = () => gfx.Dispose();

        again.Should().NotThrow();
    }

    [Fact]
    public void TheSmoothingModeAndThePinataLayoutEncodingHackAreRememberedAsGiven()
    {
        using var gfx = OnAPage();

        gfx.SmoothingMode = XSmoothingMode.HighQuality;
        gfx.MUH = PdfFontEncoding.Unicode;

        gfx.SmoothingMode.Should().Be(XSmoothingMode.HighQuality);
        gfx.MUH.Should().Be(PdfFontEncoding.Unicode);
    }

    [Fact]
    public void ACommentGoesIntoTheContentStreamWithoutDrawingAnything()
    {
        var page = PageShowing(gfx =>
        {
            gfx.WriteComment("a landmark");
            gfx.DrawLine(XPens.Black, 100, 100, 200, 200);
        });

        ContentOf(page).Should().Contain("a landmark");
        PathGeometry.PointsOf(page).Should().HaveCount(2, "the comment is not ink");
    }

    [Fact]
    public void ACommentOfNothingIsRefused()
    {
        using var gfx = OnAPage();

        var act = () => gfx.WriteComment(null);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- lines and curves ----------------------------------------------------------------------

    [Fact]
    public void ALineIsTheSameLineWhicheverWayItIsAskedFor()
    {
        ShapeOf(gfx => gfx.DrawLine(XPens.Black, new XPoint(100, 100), new XPoint(300, 200)))
            .Should().Be(ShapeOf(gfx => gfx.DrawLine(XPens.Black, 100, 100, 300, 200)));
    }

    [Fact]
    public void ASeriesOfLinesIsTheSameWhetherItIsPointsOrLooseNumbers()
    {
        ShapeOf(gfx => gfx.DrawLines(XPens.Black, ThreePoints))
            .Should().Be(ShapeOf(gfx => gfx.DrawLines(XPens.Black, 100, 100, 200, 150, 300, 100)));
    }

    [Fact]
    public void ASeriesOfLinesNeedsTwoPointsToBeALine()
    {
        using var gfx = OnAPage();

        var noPen = () => gfx.DrawLines(null, ThreePoints);
        var noPoints = () => gfx.DrawLines(XPens.Black, null);
        var onePoint = () => gfx.DrawLines(XPens.Black, new[] { new XPoint(1, 1) });
        var noNumbers = () => gfx.DrawLines(XPens.Black, 1, 1, null);

        noPen.Should().Throw<ArgumentNullException>();
        noPoints.Should().Throw<ArgumentNullException>();
        onePoint.Should().Throw<ArgumentException>()
            .Which.ParamName.Should().Be("points", "the parameter name and the message were once the wrong way round");
        noNumbers.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ABezierIsTheSameCurveWhicheverWayItIsAskedFor()
    {
        ShapeOf(gfx => gfx.DrawBezier(XPens.Black,
                new XPoint(100, 100), new XPoint(150, 50), new XPoint(250, 50), new XPoint(300, 100)))
            .Should().Be(ShapeOf(gfx => gfx.DrawBezier(XPens.Black, 100, 100, 150, 50, 250, 50, 300, 100)));
    }

    [Fact]
    public void ASingleBezierIsTheSameAsAChainOfOne()
    {
        ShapeOf(gfx => gfx.DrawBeziers(XPens.Black, FourBezierPoints))
            .Should().Be(ShapeOf(gfx => gfx.DrawBezier(XPens.Black, 100, 100, 150, 50, 250, 50, 300, 100)));
    }

    [Fact]
    public void AChainOfNoBeziersDrawsNothingAndAChainOfTheWrongLengthIsRefused()
    {
        using var gfx = OnAPage();

        PointCount(g => g.DrawBeziers(XPens.Black, Array.Empty<XPoint>())).Should().Be(0);

        var noPen = () => gfx.DrawBeziers(null, FourBezierPoints);
        var wrongLength = () => gfx.DrawBeziers(XPens.Black, new XPoint[5]);

        noPen.Should().Throw<ArgumentNullException>();
        wrongLength.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ASplineIsDrawnAtHalfTensionUnlessAskedOtherwise()
    {
        ShapeOf(gfx => gfx.DrawCurve(XPens.Black, ThreePoints))
            .Should().Be(ShapeOf(gfx => gfx.DrawCurve(XPens.Black, ThreePoints, 0.5)));
    }

    [Fact]
    public void ASplineCanBeDrawnThroughPartOfAnArrayOfPoints()
    {
        var many = new[]
        {
            new XPoint(0, 0), new XPoint(100, 100), new XPoint(200, 150), new XPoint(300, 100),
            new XPoint(400, 400)
        };

        ShapeOf(gfx => gfx.DrawCurve(XPens.Black, many, 1, 3, 0.5))
            .Should().Be(ShapeOf(gfx => gfx.DrawCurve(XPens.Black, ThreePoints, 0.5)));
    }

    [Fact]
    public void ASplineNeedsTwoPointsAndAPen()
    {
        using var gfx = OnAPage();

        var noPen = () => gfx.DrawCurve(null, ThreePoints);
        var noPoints = () => gfx.DrawCurve(XPens.Black, null, 0.5);
        var onePoint = () => gfx.DrawCurve(XPens.Black, new[] { new XPoint(1, 1) });

        noPen.Should().Throw<ArgumentNullException>();
        noPoints.Should().Throw<ArgumentNullException>();
        onePoint.Should().Throw<ArgumentException>();
    }

    // ----- arcs ----------------------------------------------------------------------------------

    [Fact]
    public void AnArcIsTheSameArcWhetherItIsGivenARectangleOrFourNumbers()
    {
        ShapeOf(gfx => gfx.DrawArc(XPens.Black, new XRect(100, 100, 200, 200), 0, 90))
            .Should().Be(ShapeOf(gfx => gfx.DrawArc(XPens.Black, 100, 100, 200, 200, 0, 90)));
    }

    [Fact]
    public void AnArcThatGoesAllTheWayRoundIsDrawnAsAnEllipse()
    {
        // Which matters: an arc of exactly 360 degrees drawn as an arc would start and end at the
        // same point and could come out as nothing at all.
        ShapeOf(gfx => gfx.DrawArc(XPens.Black, 100, 100, 200, 200, 0, 360))
            .Should().Be(ShapeOf(gfx => gfx.DrawEllipse(XPens.Black, 100, 100, 200, 200)));

        ShapeOf(gfx => gfx.DrawArc(XPens.Black, 100, 100, 200, 200, 0, -400))
            .Should().Be(ShapeOf(gfx => gfx.DrawEllipse(XPens.Black, 100, 100, 200, 200)));
    }

    [Fact]
    public void AnArcNeedsAPen()
    {
        using var gfx = OnAPage();

        var act = () => gfx.DrawArc(null, 100, 100, 200, 200, 0, 90);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- rectangles ----------------------------------------------------------------------------

    [Fact]
    public void ARectangleIsTheSameRectangleWhicheverWayItIsAskedFor()
    {
        var rect = new XRect(100, 100, 200, 50);

        ShapeOf(gfx => gfx.DrawRectangle(XPens.Black, rect))
            .Should().Be(ShapeOf(gfx => gfx.DrawRectangle(XPens.Black, 100, 100, 200, 50)));
        ShapeOf(gfx => gfx.DrawRectangle(XBrushes.Black, rect))
            .Should().Be(ShapeOf(gfx => gfx.DrawRectangle(XBrushes.Black, 100, 100, 200, 50)));
        ShapeOf(gfx => gfx.DrawRectangle(XPens.Black, XBrushes.Black, rect))
            .Should().Be(ShapeOf(gfx => gfx.DrawRectangle(XPens.Black, XBrushes.Black, 100, 100, 200, 50)));
    }

    [Fact]
    public void StrokingAndFillingARectanglePutTheSameFourCornersOnThePage()
    {
        var stroked = ShapeOf(gfx => gfx.DrawRectangle(XPens.Black, 100, 100, 200, 50));

        ShapeOf(gfx => gfx.DrawRectangle(XBrushes.Black, 100, 100, 200, 50)).Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawRectangle(XPens.Black, XBrushes.Black, 100, 100, 200, 50))
            .Should().Be(stroked);
    }

    [Fact]
    public void ARectangleNeedsAPenOrABrush()
    {
        using var gfx = OnAPage();

        var neither = () => gfx.DrawRectangle(null, null, 0, 0, 10, 10);

        neither.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("pen", "a parameter name names one parameter, the pen coming first");
    }

    [Fact]
    public void SeveralRectanglesAreSeveralRectangles()
    {
        var rects = new[]
        {
            new XRect(100, 100, 50, 50), new XRect(200, 100, 50, 50), new XRect(300, 100, 50, 50)
        };

        CountOf(ShapeOf(gfx => gfx.DrawRectangles(XPens.Black, rects)), "re").Should().Be(3);
        CountOf(ShapeOf(gfx => gfx.DrawRectangles(XBrushes.Black, rects)), "re").Should().Be(3);
        CountOf(ShapeOf(gfx => gfx.DrawRectangles(XPens.Black, XBrushes.Black, rects)), "re")
            .Should().Be(3);

        // And all three spellings put the same three rectangles there.
        var stroked = ShapeOf(gfx => gfx.DrawRectangles(XPens.Black, rects));
        ShapeOf(gfx => gfx.DrawRectangles(XBrushes.Black, rects)).Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawRectangles(XPens.Black, XBrushes.Black, rects)).Should().Be(stroked);
    }

    [Fact]
    public void ARoundedRectangleIsTheSameShapeWhicheverWayItIsAskedFor()
    {
        var rect = new XRect(100, 100, 200, 100);
        var corner = new XSize(40, 40);

        ShapeOf(gfx => gfx.DrawRoundedRectangle(XPens.Black, rect, corner))
            .Should().Be(ShapeOf(gfx => gfx.DrawRoundedRectangle(XPens.Black, 100, 100, 200, 100, 40, 40)));
        ShapeOf(gfx => gfx.DrawRoundedRectangle(XBrushes.Black, rect, corner))
            .Should().Be(ShapeOf(gfx => gfx.DrawRoundedRectangle(XBrushes.Black, 100, 100, 200, 100, 40, 40)));
        ShapeOf(gfx => gfx.DrawRoundedRectangle(XPens.Black, XBrushes.Black, rect, corner))
            .Should().Be(ShapeOf(gfx => gfx.DrawRoundedRectangle(
                XPens.Black, XBrushes.Black, 100, 100, 200, 100, 40, 40)));
    }

    [Fact]
    public void ARoundedRectangleFillsTheBoxItIsGiven()
    {
        // A rounded rectangle is built as a path rather than written as a rectangle operator, so
        // unlike a square one it can be measured point by point.
        var rounded = Bounds(gfx => gfx.DrawRoundedRectangle(XPens.Black, 100, 100, 200, 100, 40, 40));

        rounded.Width.Should().BeApproximately(200, 1e-3);
        rounded.Height.Should().BeApproximately(100, 1e-3);
    }

    // ----- ellipses ------------------------------------------------------------------------------

    [Fact]
    public void AnEllipseIsTheSameEllipseWhicheverWayItIsAskedFor()
    {
        var rect = new XRect(100, 100, 200, 100);

        ShapeOf(gfx => gfx.DrawEllipse(XPens.Black, rect))
            .Should().Be(ShapeOf(gfx => gfx.DrawEllipse(XPens.Black, 100, 100, 200, 100)));
        ShapeOf(gfx => gfx.DrawEllipse(XBrushes.Black, rect))
            .Should().Be(ShapeOf(gfx => gfx.DrawEllipse(XBrushes.Black, 100, 100, 200, 100)));
        ShapeOf(gfx => gfx.DrawEllipse(XPens.Black, XBrushes.Black, rect))
            .Should().Be(ShapeOf(gfx => gfx.DrawEllipse(XPens.Black, XBrushes.Black, 100, 100, 200, 100)));
    }

    [Fact]
    public void AnEllipseFillsTheBoxItIsGiven()
    {
        var bounds = Bounds(gfx => gfx.DrawEllipse(XPens.Black, 100, 100, 200, 100));

        bounds.Width.Should().BeApproximately(200, 1e-3);
        bounds.Height.Should().BeApproximately(100, 1e-3);
    }

    [Fact]
    public void AnEllipseNeedsAPenOrABrush()
    {
        using var gfx = OnAPage();

        var act = () => gfx.DrawEllipse(null, null, 0, 0, 10, 10);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- polygons ------------------------------------------------------------------------------

    [Fact]
    public void APolygonIsTheSameShapeStrokedOrFilled()
    {
        var stroked = ShapeOf(gfx => gfx.DrawPolygon(XPens.Black, ThreePoints));

        ShapeOf(gfx => gfx.DrawPolygon(XBrushes.Black, ThreePoints, XFillMode.Alternate))
            .Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawPolygon(XPens.Black, XBrushes.Black, ThreePoints, XFillMode.Winding))
            .Should().Be(stroked);
    }

    [Fact]
    public void APolygonNeedsTwoPointsAndSomethingToDrawWith()
    {
        using var gfx = OnAPage();

        var noPen = () => gfx.DrawPolygon(null, ThreePoints);
        var noBrush = () => gfx.DrawPolygon(null, ThreePoints, XFillMode.Alternate);
        var neither = () => gfx.DrawPolygon(null, null, ThreePoints, XFillMode.Alternate);
        var noPoints = () => gfx.DrawPolygon(XPens.Black, null);
        var onePoint = () => gfx.DrawPolygon(XPens.Black, new[] { new XPoint(1, 1) });

        noPen.Should().Throw<ArgumentNullException>();
        noBrush.Should().Throw<ArgumentNullException>();
        neither.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("pen");
        noPoints.Should().Throw<ArgumentNullException>();
        onePoint.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("points");
    }

    // ----- pies ----------------------------------------------------------------------------------

    [Fact]
    public void APieIsTheSameShapeWhicheverWayItIsAskedFor()
    {
        var rect = new XRect(100, 100, 200, 200);

        ShapeOf(gfx => gfx.DrawPie(XPens.Black, rect, 0, 90))
            .Should().Be(ShapeOf(gfx => gfx.DrawPie(XPens.Black, 100, 100, 200, 200, 0, 90)));
        ShapeOf(gfx => gfx.DrawPie(XBrushes.Black, rect, 0, 90))
            .Should().Be(ShapeOf(gfx => gfx.DrawPie(XBrushes.Black, 100, 100, 200, 200, 0, 90)));
        ShapeOf(gfx => gfx.DrawPie(XPens.Black, XBrushes.Black, rect, 0, 90))
            .Should().Be(ShapeOf(gfx => gfx.DrawPie(XPens.Black, XBrushes.Black, 100, 100, 200, 200, 0, 90)));
    }

    [Fact]
    public void APieNeedsAPenOrABrush()
    {
        using var gfx = OnAPage();

        var noPen = () => gfx.DrawPie((XPen)null, 0, 0, 10, 10, 0, 90);
        var noBrush = () => gfx.DrawPie((XBrush)null, 0, 0, 10, 10, 0, 90);
        var neither = () => gfx.DrawPie(null, null, 0, 0, 10, 10, 0, 90);

        noPen.Should().Throw<ArgumentNullException>();
        noBrush.Should().Throw<ArgumentNullException>();
        neither.Should().Throw<ArgumentNullException>();
    }

    // ----- closed curves -------------------------------------------------------------------------

    [Fact]
    public void AClosedCurveIsTheSameShapeStrokedOrFilledOrBoth()
    {
        var stroked = ShapeOf(gfx => gfx.DrawClosedCurve(XPens.Black, ThreePoints));

        ShapeOf(gfx => gfx.DrawClosedCurve(XPens.Black, ThreePoints, 0.5)).Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawClosedCurve(XBrushes.Black, ThreePoints)).Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawClosedCurve(XBrushes.Black, ThreePoints, XFillMode.Alternate))
            .Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawClosedCurve(XBrushes.Black, ThreePoints, XFillMode.Alternate, 0.5))
            .Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawClosedCurve(XPens.Black, XBrushes.Black, ThreePoints)).Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawClosedCurve(XPens.Black, XBrushes.Black, ThreePoints, XFillMode.Alternate))
            .Should().Be(stroked);
    }

    [Fact]
    public void AClosedCurveOfNoPointsDrawsNothingAndOneOfOnePointIsRefused()
    {
        using var gfx = OnAPage();

        PointCount(g => g.DrawClosedCurve(XPens.Black, Array.Empty<XPoint>())).Should().Be(0);

        var onePoint = () => gfx.DrawClosedCurve(XPens.Black, new[] { new XPoint(1, 1) });
        var neither = () => gfx.DrawClosedCurve(null, null, ThreePoints, XFillMode.Alternate, 0.5);

        onePoint.Should().Throw<ArgumentException>();
        neither.Should().Throw<ArgumentNullException>();
    }

    // ----- paths ---------------------------------------------------------------------------------

    [Fact]
    public void APathIsTheSameShapeStrokedOrFilledOrBoth()
    {
        static XGraphicsPath Rectangle()
        {
            var path = new XGraphicsPath();
            path.AddRectangle(100, 100, 200, 50);
            return path;
        }

        var stroked = ShapeOf(gfx => gfx.DrawPath(XPens.Black, Rectangle()));

        ShapeOf(gfx => gfx.DrawPath(XBrushes.Black, Rectangle())).Should().Be(stroked);
        ShapeOf(gfx => gfx.DrawPath(XPens.Black, XBrushes.Black, Rectangle())).Should().Be(stroked);
    }

    [Fact]
    public void APathNeedsToExistAndNeedsAPenOrABrush()
    {
        using var gfx = OnAPage();
        var path = new XGraphicsPath();

        var noPen = () => gfx.DrawPath((XPen)null, path);
        var noBrush = () => gfx.DrawPath((XBrush)null, path);
        var neither = () => gfx.DrawPath(null, null, path);
        var noPathToStroke = () => gfx.DrawPath(XPens.Black, null);
        var noPathToFill = () => gfx.DrawPath(XBrushes.Black, null);
        var noPathAtAll = () => gfx.DrawPath(XPens.Black, XBrushes.Black, null);

        noPen.Should().Throw<ArgumentNullException>();
        noBrush.Should().Throw<ArgumentNullException>();
        neither.Should().Throw<ArgumentNullException>();
        noPathToStroke.Should().Throw<ArgumentNullException>();
        noPathToFill.Should().Throw<ArgumentNullException>();
        noPathAtAll.Should().Throw<ArgumentNullException>();
    }

    // ----- the transform -------------------------------------------------------------------------

    [Fact]
    public void ASurfaceStartsWithNoTransformOfItsOwn()
    {
        using var gfx = OnAPage();

        gfx.Transform.IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void ATransformIsWrittenIntoThePageRatherThanBakedIntoTheCoordinates()
    {
        // The shape reaches the page in the coordinates the caller named, and the transform goes
        // with it as a concatenate-matrix operator for the reader to apply. Worth stating,
        // because it is why reading path points back out of a page says nothing about what
        // transform was in force when they were written.
        var moved = PageShowing(gfx =>
        {
            gfx.TranslateTransform(50, 0);
            gfx.DrawEllipse(XPens.Black, 100, 100, 200, 50);
        });
        var unmoved = PageShowing(gfx => gfx.DrawEllipse(XPens.Black, 100, 100, 200, 50));

        PathGeometry.BoundsOf(moved).Should().Be(PathGeometry.BoundsOf(unmoved));
        CountOf(ContentOf(moved), "cm").Should().Be(CountOf(ContentOf(unmoved), "cm") + 1);
    }

    [Fact]
    public void EachKindOfTransformReachesThePageAsItsOwnMatrix()
    {
        static string TransformsIn(Action<XGraphics> apply)
        {
            var page = PageShowing(gfx =>
            {
                apply(gfx);
                gfx.DrawEllipse(XPens.Black, 100, 100, 100, 50);
            });
            var lines = ContentOf(page).Split('\n')
                .Select(line => line.Trim())
                .Where(line => Regex.IsMatch(line, @"(^|\s)cm$"));
            return string.Join("\n", lines);
        }

        var plain = TransformsIn(_ => { });

        TransformsIn(gfx => gfx.ScaleTransform(2)).Should().NotBe(plain);
        TransformsIn(gfx => gfx.RotateTransform(30)).Should().NotBe(plain);
        TransformsIn(gfx => gfx.ShearTransform(1, 0)).Should().NotBe(plain);
        TransformsIn(gfx => gfx.MultiplyTransform(new XMatrix(2, 0, 0, 2, 5, 5))).Should().NotBe(plain);
        TransformsIn(gfx => gfx.ScaleTransform(2, 2))
            .Should().Be(TransformsIn(gfx => gfx.ScaleTransform(2)));
    }

    [Fact]
    public void EveryWayOfNamingATransformReachesTheSameMatrix()
    {
        // The overloads that take an order and the ones that do not are meant to agree when the
        // order asked for is the one the short form uses, which is Prepend throughout.
        static XMatrix After(Action<XGraphics> apply)
        {
            using var gfx = OnAPage();
            apply(gfx);
            return gfx.Transform;
        }

        After(gfx => gfx.TranslateTransform(10, 20))
            .Should().Be(After(gfx => gfx.TranslateTransform(10, 20, XMatrixOrder.Prepend)));
        After(gfx => gfx.ScaleTransform(2, 3))
            .Should().Be(After(gfx => gfx.ScaleTransform(2, 3, XMatrixOrder.Prepend)));
        After(gfx => gfx.ScaleTransform(2))
            .Should().Be(After(gfx => gfx.ScaleTransform(2, XMatrixOrder.Prepend)));
        After(gfx => gfx.ScaleTransform(2, 2))
            .Should().Be(After(gfx => gfx.ScaleTransform(2)));
        After(gfx => gfx.RotateTransform(30))
            .Should().Be(After(gfx => gfx.RotateTransform(30, XMatrixOrder.Prepend)));
        After(gfx => gfx.RotateAtTransform(30, new XPoint(10, 10)))
            .Should().Be(After(gfx => gfx.RotateAtTransform(30, new XPoint(10, 10), XMatrixOrder.Prepend)));
        After(gfx => gfx.ShearTransform(1, 0))
            .Should().Be(After(gfx => gfx.ShearTransform(1, 0, XMatrixOrder.Prepend)));
        After(gfx => gfx.MultiplyTransform(new XMatrix(2, 0, 0, 2, 5, 5)))
            .Should().Be(After(gfx => gfx.MultiplyTransform(new XMatrix(2, 0, 0, 2, 5, 5), XMatrixOrder.Prepend)));
        After(gfx => gfx.ScaleAtTransform(2, 2, 10, 10))
            .Should().Be(After(gfx => gfx.ScaleAtTransform(2, 2, new XPoint(10, 10))));
        After(gfx => gfx.SkewAtTransform(10, 0, 10, 10))
            .Should().Be(After(gfx => gfx.SkewAtTransform(10, 0, new XPoint(10, 10))));
    }

    [Fact]
    public void ScalingAboutAPointLeavesThatPointWhereItIs()
    {
        using var gfx = OnAPage();

        gfx.ScaleAtTransform(2, 2, 100, 100);

        gfx.Transform.Transform(new XPoint(100, 100)).Should().Be(new XPoint(100, 100));
        gfx.Transform.Transform(new XPoint(101, 100)).Should().Be(new XPoint(102, 100));
    }

    // ----- saving and restoring ------------------------------------------------------------------

    [Fact]
    public void RestoringPutsTheTransformBackAsItWas()
    {
        using var gfx = OnAPage();

        var state = gfx.Save();
        gfx.GraphicsStateLevel.Should().Be(1);
        gfx.TranslateTransform(50, 50);
        gfx.Transform.IsIdentity.Should().BeFalse();

        gfx.Restore(state);

        gfx.GraphicsStateLevel.Should().Be(0);
        gfx.Transform.IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void RestoringWithNoArgumentUndoesTheMostRecentSave()
    {
        using var gfx = OnAPage();

        gfx.Save();
        gfx.TranslateTransform(10, 10);
        gfx.Save();
        gfx.TranslateTransform(10, 10);
        gfx.GraphicsStateLevel.Should().Be(2);

        gfx.Restore();
        gfx.GraphicsStateLevel.Should().Be(1);
        gfx.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(10, 10));

        gfx.Restore();
        gfx.GraphicsStateLevel.Should().Be(0);
        gfx.Transform.IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void RestoringWithNothingSavedIsRefused()
    {
        using var gfx = OnAPage();

        var noSave = () => gfx.Restore();
        var noState = () => gfx.Restore(null);

        noSave.Should().Throw<InvalidOperationException>();
        noState.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AContainerSavesTheStateTheSameWayASaveDoes()
    {
        using var gfx = OnAPage();

        var container = gfx.BeginContainer();
        gfx.GraphicsStateLevel.Should().Be(1);
        gfx.TranslateTransform(50, 50);

        gfx.EndContainer(container);

        gfx.GraphicsStateLevel.Should().Be(0);
        gfx.Transform.IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void AContainerCanMapOneRectangleOntoAnother()
    {
        using var gfx = OnAPage();

        gfx.BeginContainer(new XRect(100, 100, 200, 200), new XRect(0, 0, 100, 100),
            XGraphicsUnit.Point);

        // The source box is scaled by two and moved to where the destination box is, so the
        // source origin lands on the destination corner.
        gfx.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(100, 100));
        gfx.Transform.Transform(new XPoint(100, 100)).Should().Be(new XPoint(300, 300));
    }

    [Fact]
    public void AContainerInAnyUnitButPointIsRefusedAndEndingNothingIsToo()
    {
        using var gfx = OnAPage();

        var inMillimetres = () => gfx.BeginContainer(
            new XRect(0, 0, 1, 1), new XRect(0, 0, 1, 1), XGraphicsUnit.Millimeter);
        var endNothing = () => gfx.EndContainer(null);

        inMillimetres.Should().Throw<ArgumentException>();
        endNothing.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SavingAndRestoringWriteTheLiteralQAndQOperators()
    {
        void Box(XGraphics gfx) => gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);

        // Two of them are the page's own: the renderer opens page space and then world space before
        // the first thing is drawn, and closes both when the page ends.
        StateOf(Box).Should().Be("qqQQ");

        StateOf(gfx =>
        {
            Box(gfx);
            var state = gfx.Save();
            Box(gfx);
            gfx.Restore(state);
            Box(gfx);
        }).Should().Be("qqqQQQ");

        // A container is the same pair of operators; only the token the caller hands back differs.
        StateOf(gfx =>
        {
            Box(gfx);
            var container = gfx.BeginContainer();
            Box(gfx);
            gfx.EndContainer(container);
            Box(gfx);
        }).Should().Be("qqqQQQ");
    }

    [Fact]
    public void ASaveNeverRestoredIsClosedWhenThePageEnds()
    {
        StateOf(gfx =>
        {
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
            gfx.Save();
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
            gfx.Save();
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
        }).Should().Be("qqqqQQQQ");
    }

    [Fact]
    public void RestoringAnOuterStateClosesEveryStateSavedInsideIt()
    {
        void Nested(XGraphics gfx, Action<XGraphics, XGraphicsState, XGraphicsState> restore)
        {
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
            var outer = gfx.Save();
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
            var inner = gfx.Save();
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
            restore(gfx, outer, inner);
            gfx.DrawRectangle(XPens.Black, 10, 10, 20, 20);
        }

        // Handing back the inner state closes one level, and the last shape is drawn one deep.
        DepthsOf(gfx => Nested(gfx, (g, _, inner) => g.Restore(inner)))
            .Should().Equal(2, 3, 4, 3);

        // Handing back the outer one closes both, so the last shape is drawn back at page level.
        DepthsOf(gfx => Nested(gfx, (g, outer, _) => g.Restore(outer)))
            .Should().Equal(2, 3, 4, 2);
    }

    // ----- clipping ------------------------------------------------------------------------------

    [Fact]
    public void ClippingToARectangleIsClippingToTheRectangleAsAPath()
    {
        var toRect = ContentOf(PageShowing(gfx =>
        {
            gfx.IntersectClip(new XRect(100, 100, 200, 200));
            gfx.DrawRectangle(XPens.Black, 0, 0, 500, 500);
        }));

        var toPath = ContentOf(PageShowing(gfx =>
        {
            var path = new XGraphicsPath();
            path.AddRectangle(new XRect(100, 100, 200, 200));
            gfx.IntersectClip(path);
            gfx.DrawRectangle(XPens.Black, 0, 0, 500, 500);
        }));

        toRect.Should().Be(toPath);
        toRect.Should().Contain("W");
    }

    [Fact]
    public void ClippingToNothingIsRefused()
    {
        using var gfx = OnAPage();

        var act = () => gfx.IntersectClip(null);

        act.Should().Throw<ArgumentNullException>();
    }
}
