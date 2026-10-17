using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.Test.Helpers;
using PdfPinata.Utils;
using SixLabors.Fonts;
using SixLabors.Fonts.Rendering;
using SixLabors.Fonts.Unicode;
using TUnit.Core;

namespace PdfPinata.ImageSharp4.Tests;

/// <summary>
///   SixLabors.Fonts 3 can hand a glyph outline an elliptical arc, which Fonts 1 never did, and
///   <see cref="XGlyphOutline"/> has no arc: only lines and cubic Béziers. The backend therefore
///   draws each arc as the Béziers that trace it. Leaving one out would leave a straight chord
///   where the curve should be - a glyph with a bite out of it, or a circle with nothing inside.
///   <para>
///     No glyph the backend draws reaches an arc today, and these tests say why. An arc comes only
///     from an OpenType SVG glyph's circles, ellipses and arc commands, and the backend draws SVG
///     glyphs from their ordinary outlines, as Fonts 1 and so PdfPinata.ImageSharp always did -
///     not least because Fonts 3.2.0 hands those arcs over with a vertical radius of zero. So the
///     arc conversion is tested here directly, through <see cref="IGlyphRenderer"/> on the
///     backend's own renderer, which is a private class and is made by reflection; and an SVG glyph
///     drawn with arcs is tested end to end for what it does come out as.
///   </para>
/// </summary>
public class ArcGlyphOutlineTests
{
    private const double EmSize = 48;
    private const int UnitsPerEm = 2048;

    private const double CentreX = 30, CentreY = 20, Radius = 10;

    // ----- the conversion, through IGlyphRenderer ------------------------------------------------

    [Test]
    public void ACircleOfTwoArcsIsOneClosedFigureOfCurves()
    {
        var segments = Circle(sweep: true).Segments;

        segments[0].Kind.Should().Be(XGlyphSegmentKind.Start);
        segments[^1].Kind.Should().Be(XGlyphSegmentKind.Close);
        segments.Skip(1).SkipLast(1).Should().OnlyContain(segment => segment.Kind == XGlyphSegmentKind.Curve,
            "an arc is a curve, and a straight line here is an arc that was dropped");
        // A half turn is two quarter turns, and a quarter turn is as much as one cubic is asked for.
        segments.Count(segment => segment.Kind == XGlyphSegmentKind.Curve).Should().Be(4);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void EveryPointOfTheCircleLiesOnTheCircle(bool sweep)
    {
        // Both ways round, since the sweep flag picks which of two centres the arc turns about.
        foreach (var point in Flattened(Circle(sweep)).SelectMany(figure => figure))
        {
            Distance(point, Centre()).Should().BeApproximately(Radius, Radius * 1e-3,
                "({0}, {1}) is a point of the outline", point.X, point.Y);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public void TheCircleEnclosesTheAreaOfACircle(bool sweep)
    {
        var area = Flattened(Circle(sweep)).Sum(SignedArea);

        Math.Abs(area).Should().BeApproximately(Math.PI * Radius * Radius, Math.PI * Radius * Radius * 1e-3,
            "a circle with a chord where an arc should be encloses half of this, or nothing");
    }

    [Test]
    public void TheTwoSweepsWindTheCircleOppositeWays()
    {
        // Which way a figure winds decides how it combines with others under the nonzero rule; a
        // hole in a glyph is a figure wound the other way.
        Math.Sign(Flattened(Circle(sweep: true)).Sum(SignedArea))
            .Should().Be(-Math.Sign(Flattened(Circle(sweep: false)).Sum(SignedArea)));
    }

    [Test]
    public void AQuarterArcOfAnEllipseEndsWhereItWasAskedToAndPassesThroughItsMidpoint()
    {
        // An ellipse twice as wide as it is tall, turned through 30 degrees. From the end of its
        // major axis to the end of its minor axis is a quarter of it, whichever pair is chosen.
        const double rx = 20, ry = 10, degrees = 30;
        var phi = degrees * Math.PI / 180;
        Vector2 OnEllipse(double t) => new(
            (float)(CentreX + rx * Math.Cos(t) * Math.Cos(phi) - ry * Math.Sin(t) * Math.Sin(phi)),
            (float)(CentreY + rx * Math.Cos(t) * Math.Sin(phi) + ry * Math.Sin(t) * Math.Cos(phi)));

        var outline = Draw(renderer =>
        {
            renderer.MoveTo(OnEllipse(0));
            renderer.ArcTo((float)rx, (float)ry, (float)degrees, largeArc: false, sweep: true, OnEllipse(Math.PI / 2));
        });

        var curve = outline.Segments.Single(segment => segment.Kind == XGlyphSegmentKind.Curve);
        var end = At(OnEllipse(Math.PI / 2));
        curve.End.X.Should().BeApproximately(end.X, 1e-4);
        curve.End.Y.Should().BeApproximately(end.Y, 1e-4);

        var start = outline.Segments[0].End;
        var middle = OnCubic(start, curve.Control1, curve.Control2, curve.End, 0.5);
        var expected = At(OnEllipse(Math.PI / 4));
        Distance(middle, expected).Should().BeLessThan(0.01);
    }

    [Test]
    public void RadiiTooSmallToReachAreScaledUpUntilTheyDo()
    {
        // SVG's rule, and IGlyphRenderer's: a half circle asked for with half the radius it needs.
        var outline = Draw(renderer =>
        {
            renderer.MoveTo(new Vector2((float)(CentreX + Radius), (float)CentreY));
            renderer.ArcTo((float)Radius / 2, (float)Radius / 2, 0, largeArc: false, sweep: true,
                new Vector2((float)(CentreX - Radius), (float)CentreY));
        });

        foreach (var point in Flattened(outline).SelectMany(figure => figure))
            Distance(point, Centre()).Should().BeApproximately(Radius, Radius * 1e-3);
    }

    [Test]
    public void AZeroRadiusIsAStraightLine()
    {
        var end = new Vector2((float)(CentreX - Radius), (float)CentreY);
        var outline = Draw(renderer =>
        {
            renderer.MoveTo(new Vector2((float)(CentreX + Radius), (float)CentreY));
            renderer.ArcTo((float)Radius, 0, 0, largeArc: false, sweep: true, end);
        });

        outline.Segments.Select(segment => segment.Kind)
            .Should().Equal(XGlyphSegmentKind.Start, XGlyphSegmentKind.Line, XGlyphSegmentKind.Close);
        outline.Segments[1].End.Should().Be(At(end));
    }

    // ----- an SVG glyph, end to end ---------------------------------------------------------------

    [Test]
    public void AGlyphTheFontDrawsWithSvgArcsIsDrawnFromItsOrdinaryOutline()
    {
        const string family = "Liberation Sans with an SVG circle for its o";
        var font = File.ReadAllBytes(PathHelper.GetInstance().GetAssetPath("Fonts", "LiberationSans-Regular.ttf"));
        PinnedFontResolver.Register(family, FontDrawingTheOAsACircle(font));

        var provider = new ImageSharpGlyphOutlineProvider();
        var withSvg = provider.GetOutlines("o", family, false, false, EmSize).Single();
        var plain = provider.GetOutlines("o", "Arial", false, false, EmSize).Single();

        // Liberation Sans's own "o": two figures, the bowl and its counter, and so no hole where
        // there should not be one. It is also what PdfPinata.ImageSharp draws, since Fonts 1 reads
        // no SVG table at all.
        withSvg.Segments.Should().Equal(plain.Segments);
        Flattened(withSvg).Should().HaveCount(2);
    }

    // ----- arrangements -----

    private static XPoint Centre() => new(CentreX, -CentreY);

    /// <summary>A point as the backend's renderer reports it: the baseline at zero, y upwards.</summary>
    private static XPoint At(Vector2 point) => new(point.X, -point.Y);

    /// <summary>A circle drawn as SVG draws one, and as Fonts 3 hands a <c>&lt;circle&gt;</c> on: two half turns.</summary>
    private static XGlyphOutline Circle(bool sweep) => Draw(renderer =>
    {
        renderer.MoveTo(new Vector2((float)(CentreX + Radius), (float)CentreY));
        renderer.ArcTo((float)Radius, (float)Radius, 0, largeArc: true, sweep,
            new Vector2((float)(CentreX - Radius), (float)CentreY));
        renderer.ArcTo((float)Radius, (float)Radius, 0, largeArc: true, sweep,
            new Vector2((float)(CentreX + Radius), (float)CentreY));
    });

    /// <summary>
    ///   One figure drawn on the backend's own renderer, with the baseline at zero and nothing
    ///   scaled, and the outline it collected.
    /// </summary>
    private static XGlyphOutline Draw(Action<IGlyphRenderer> figure)
    {
        var type = typeof(ImageSharpGlyphOutlineProvider).GetNestedType("OutlineCollector", BindingFlags.NonPublic)
                   ?? throw new InvalidOperationException("The backend's renderer is not where this test looks for it.");
        var renderer = (IGlyphRenderer)Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.NonPublic, null, [0.0, 1.0], null)!;

        renderer.BeginGlyph(default, default);
        renderer.BeginFigure();
        figure(renderer);
        renderer.EndFigure();
        renderer.EndGlyph();

        var outlines = (IReadOnlyList<XGlyphOutline>)type
            .GetProperty("Outlines", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(renderer)!;
        return outlines.Single();
    }

    private static byte[] FontDrawingTheOAsACircle(byte[] font)
    {
        var collection = new FontCollection();
        using var stream = new MemoryStream(font);
        var metrics = collection.Add(stream).CreateFont(12).FontMetrics;
        metrics.UnitsPerEm.Should().Be(UnitsPerEm);
        metrics.TryGetGlyphMetrics(new CodePoint('o'), TextAttributes.None, TextDecorations.None,
            LayoutMode.HorizontalTopBottom, ColorFontSupport.None, null, out var glyph).Should().BeTrue();

        // No viewBox: an SVG glyph is then in font units, from the glyph origin, y downwards.
        var document = "<svg xmlns=\"http://www.w3.org/2000/svg\">"
                       + $"<g id=\"glyph{glyph.GlyphId}\"><circle cx=\"600\" cy=\"-700\" r=\"500\"/></g></svg>";
        return SvgGlyphFont.With(font, glyph.GlyphId, document);
    }

    /// <summary>Each figure of the outline as a polygon, its curves cut into short straight pieces.</summary>
    private static List<List<XPoint>> Flattened(XGlyphOutline outline)
    {
        var figures = new List<List<XPoint>>();
        List<XPoint> figure = null;
        var current = new XPoint();

        foreach (var segment in outline.Segments)
        {
            switch (segment.Kind)
            {
                case XGlyphSegmentKind.Start:
                    figure = [segment.End];
                    figures.Add(figure);
                    current = segment.End;
                    break;
                case XGlyphSegmentKind.Line:
                    figure!.Add(segment.End);
                    current = segment.End;
                    break;
                case XGlyphSegmentKind.Curve:
                    for (var step = 1; step <= 32; step++)
                        figure!.Add(OnCubic(current, segment.Control1, segment.Control2, segment.End, step / 32.0));
                    current = segment.End;
                    break;
            }
        }

        return figures;
    }

    private static XPoint OnCubic(XPoint p0, XPoint p1, XPoint p2, XPoint p3, double t)
    {
        var u = 1 - t;
        return new XPoint(
            u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
            u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y);
    }

    /// <summary>The shoelace formula, over a figure taken as closed.</summary>
    private static double SignedArea(List<XPoint> figure)
    {
        double sum = 0;
        for (var i = 0; i < figure.Count; i++)
        {
            var a = figure[i];
            var b = figure[(i + 1) % figure.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }

        return sum / 2;
    }

    private static double Distance(XPoint a, XPoint b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
