using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using SixLabors.Fonts;
#if IMAGESHARP4
using SixLabors.Fonts.Rendering;
#else
using SixLabors.Fonts.Unicode;
#endif

namespace PdfPinata.Utils;

/// <summary>
/// Reads glyph outlines through SixLabors.Fonts, so that
/// <see cref="PdfPinata.Drawing.XGraphicsPath"/>.AddString can produce real geometry.
/// Register it once before adding text to a path:
/// <c>GlobalFontSettings.GlyphOutlineProvider = new ImageSharpGlyphOutlineProvider();</c>
/// </summary>
/// <remarks>
/// SixLabors.Fonts renders a glyph as a series of calls on an <see cref="IGlyphRenderer"/> - move,
/// line, quadratic, cubic, end - so the translation is a direct one with no path to walk. It
/// decodes both TrueType and PostScript (CFF) outlines.
/// <para>
/// A weight or a slant the font has no file for is simulated when text is <i>drawn</i>, by stroking
/// or skewing. Nothing simulates it here: an outline is the shape the font actually holds.
/// </para>
/// </remarks>
public sealed class ImageSharpGlyphOutlineProvider : IGlyphOutlineProvider
{
    /// <summary>
    /// Points per inch, so that a font size in points comes out as a size in points and the
    /// outlines need no scaling afterwards.
    /// </summary>
    private const float PointsPerInch = 72f;

#if IMAGESHARP4
    /// <summary>
    /// How many times finer than a point Fonts 3 lays the outlines out, which they are divided back
    /// down by. Fonts 3 rounds every TrueType point to a 64th of a pixel, as a rasteriser does, and
    /// at 72 dots per inch that is a 64th of a point: enough to move an edge by a third of a font
    /// unit at 48 points, to somewhere neither the font, Fonts 1 nor Skia puts it. At 64 times the
    /// resolution the rounding is a 4096th of a point. The layout is linear while nothing is hinted,
    /// so dividing the result back down is exact but for that rounding.
    /// </summary>
    private const float Oversampling = 64f;
#endif

    /// <inheritdoc />
    public IEnumerable<XGlyphOutline> GetOutlines(string text, string familyName, bool isBold, bool isItalic,
        double emSize)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        // Through the registered resolver, never around it: a provider that picked its own face
        // would one day disagree with the text that was drawn.
        var resolver = GlobalFontSettings.FontResolver;
        var info = resolver.ResolveTypeface(familyName, isBold, isItalic);
        if (info == null)
            throw new InvalidOperationException(
                "The font resolver has no face for the family '" + familyName + "'.");

        var fontBytes = resolver.GetFont(info.FaceName);

        var collection = new FontCollection();
        using var stream = new MemoryStream(fontBytes);
        var family = collection.Add(stream);
        var font = family.CreateFont((float)emSize, FontStyle.Regular);

#if IMAGESHARP4
        // Fonts 3 can put the alphabetic baseline itself on the origin, and that is the line this
        // seam measures from, so there is nothing to work out. Fonts 1 cannot; see BaselineOf.
        //
        // Colour glyphs as Fonts 1 drew them, from COLR version 0 layers, which are outlines of
        // other glyphs. Fonts 3 would also draw COLR version 1 and SVG glyphs, whose paints,
        // clips and backgrounds an outline cannot hold: merged into one fill, a layer that is only
        // a paint comes out as a filled box. And Fonts 3.2.0 hands an SVG glyph's arcs over with a
        // vertical radius of zero, so a circle arrives as a straight line. Without them, such a
        // glyph is drawn from its ordinary outline, which is what PdfPinata.ImageSharp draws.
        // See #305.
        var options = new TextOptions(font)
        {
            Dpi = PointsPerInch * Oversampling,
            Origin = Vector2.Zero,
            TextBaseline = TextBaseline.Alphabetic,
            ColorFontSupport = ColorFontSupport.ColrV0
        };

        var collector = new OutlineCollector(0, Oversampling);
        TextRenderer.RenderTo(collector, text, options);
#else
        var options = new TextOptions(font) { Dpi = PointsPerInch, Origin = Vector2.Zero };

        var collector = new OutlineCollector(BaselineOf(font, text), 1);
        TextRenderer.RenderTextTo(collector, text, options);
#endif
        return collector.Outlines;
    }

#if !IMAGESHARP4
    /// <summary>
    /// How far below the origin the baseline of the laid-out line sits, in points.
    /// </summary>
    /// <remarks>
    /// Fonts 1 puts the top of the line box on the origin while this seam measures from the
    /// baseline, so the distance between the two has to be worked out the way the layout works it
    /// out. Three things go into it, and leaving any of them out moves every glyph up or down the
    /// page:
    /// <list type="bullet">
    /// <item>the <b>horizontal</b> ascender - text set across the page is measured by the
    /// horizontal metrics, which need not be the same numbers as the vertical ones;</item>
    /// <item>half of the font's leading, because the layout centres the em box inside the line
    /// box and so puts half of whatever is left over above the text;</item>
    /// <item>however far the tallest glyph of the run rises above the ascender, which is what a
    /// negative top side bearing means. It is zero for ordinary Latin text and is not zero for a
    /// capital carrying an accent.</item>
    /// </list>
    /// </remarks>
    private static double BaselineOf(Font font, string text)
    {
        var metrics = font.FontMetrics;
        var header = metrics.HorizontalMetrics;

        var halfLeading = (header.LineHeight - metrics.UnitsPerEm) / 2.0;
        var aboveTheAscender = OvershootOf(metrics, text);

        return (header.Ascender + aboveTheAscender - halfLeading) * font.Size / metrics.UnitsPerEm;
    }

    /// <summary>
    /// How far the tallest glyph of the text rises above the font's ascender, in design units.
    /// </summary>
    private static double OvershootOf(FontMetrics metrics, string text)
    {
        double overshoot = 0;

        for (var idx = 0; idx < text.Length; idx++)
        {
            var codePoint = char.IsHighSurrogate(text[idx]) && idx + 1 < text.Length
                ? new CodePoint(char.ConvertToUtf32(text[idx], text[++idx]))
                : new CodePoint(text[idx]);

            overshoot = Math.Max(overshoot, OvershootOfCharacter(metrics, codePoint));
        }

        return overshoot;
    }

    /// <summary>
    /// How far the tallest glyph drawn for one character rises above the font's ascender, in
    /// design units; zero when it does not.
    /// </summary>
    private static double OvershootOfCharacter(FontMetrics metrics, CodePoint codePoint)
    {
        // Whitespace is skipped by the layout, and so has no say in where the line sits.
        if (CodePoint.IsWhiteSpace(codePoint))
            return 0;

        if (!metrics.TryGetGlyphMetrics(codePoint, TextAttributes.None, TextDecorations.None,
                LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out var glyphs))
            return 0;

        double overshoot = 0;
        foreach (var glyph in glyphs)
        {
            if (glyph.TopSideBearing < 0)
                overshoot = Math.Max(overshoot, -glyph.TopSideBearing);
        }

        return overshoot;
    }
#endif

    /// <summary>
    /// Collects what SixLabors.Fonts draws into one <see cref="XGlyphOutline"/> per glyph.
    /// </summary>
    private sealed class OutlineCollector : IGlyphRenderer
    {
        private readonly double _baseline;
        private readonly double _scale;
        private readonly List<XGlyphOutline> _outlines = [];
        private List<XGlyphSegment> _segments = [];
        private XPoint _current;

        /// <summary>The same point as <see cref="_current"/>, where Fonts laid it out.</summary>
        private Vector2 _laidOut;

        internal OutlineCollector(double baseline, double scale)
        {
            _baseline = baseline;
            _scale = scale;
        }

        internal IReadOnlyList<XGlyphOutline> Outlines => _outlines;

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
        {
            _segments = [];
            return true;
        }

        public void EndGlyph() => _outlines.Add(new XGlyphOutline(_segments));

        public void BeginFigure()
        {
            // Nothing to record: the MoveTo that follows begins the figure.
        }

        public void MoveTo(Vector2 point)
        {
            _current = At(point);
            _laidOut = point;
            _segments.Add(XGlyphSegment.StartAt(_current));
        }

        public void LineTo(Vector2 point)
        {
            _current = At(point);
            _laidOut = point;
            _segments.Add(XGlyphSegment.LineTo(_current));
        }

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
        {
            var end = At(point);
            _segments.Add(XGlyphSegment.QuadraticTo(_current, At(secondControlPoint), end));
            _current = end;
            _laidOut = point;
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        {
            _current = At(point);
            _laidOut = point;
            _segments.Add(XGlyphSegment.CurveTo(At(secondControlPoint), At(thirdControlPoint), _current));
        }

#if IMAGESHARP4
        /// <summary>
        /// Adds an elliptical arc as the cubic Béziers that trace it, one for each quarter turn or
        /// part of one. Fonts 3 draws a circle or an ellipse in an SVG glyph this way, and an outline
        /// has nothing but lines and cubics, so an arc left out would leave a gap in the figure.
        /// </summary>
        /// <remarks>
        /// The arc is given SVG's way, by its two ends, and is worked out in the same space Fonts
        /// lays it out in, y downwards, before <see cref="At"/> turns each point the right way up:
        /// that mapping is a reflection, which takes an arc to an arc. The conversion follows SVG 1.1
        /// appendix F.6, including what it says to do with radii too small to reach and with a zero
        /// radius, which is what <see cref="IGlyphRenderer.ArcTo"/> asks for too.
        /// </remarks>
        public void ArcTo(float radiusX, float radiusY, float rotation, bool largeArc, bool sweep, Vector2 point)
        {
            var (x1, y1) = ((double)_laidOut.X, (double)_laidOut.Y);
            var (x2, y2) = ((double)point.X, (double)point.Y);

            double rx = Math.Abs(radiusX), ry = Math.Abs(radiusY);
            if (x1 == x2 && y1 == y2)
                return;
            if (rx == 0 || ry == 0)
            {
                LineTo(point);
                return;
            }

            var phi = rotation * Math.PI / 180;
            double cos = Math.Cos(phi), sin = Math.Sin(phi);

            // F.6.5 step 1: the start point in a frame centred between the ends and turned by phi.
            var dx = (x1 - x2) / 2;
            var dy = (y1 - y2) / 2;
            var x1P = cos * dx + sin * dy;
            var y1P = -sin * dx + cos * dy;

            // F.6.6: radii too small to span the ends are scaled up until they just do.
            var lambda = x1P * x1P / (rx * rx) + y1P * y1P / (ry * ry);
            if (lambda > 1)
            {
                rx *= Math.Sqrt(lambda);
                ry *= Math.Sqrt(lambda);
            }

            // Step 2: the centre in that frame, on the side the two flags choose.
            var numerator = rx * rx * ry * ry - rx * rx * y1P * y1P - ry * ry * x1P * x1P;
            var denominator = rx * rx * y1P * y1P + ry * ry * x1P * x1P;
            var coefficient = Math.Sqrt(Math.Max(0, numerator / denominator)) * (largeArc == sweep ? -1 : 1);
            var cxP = coefficient * rx * y1P / ry;
            var cyP = -coefficient * ry * x1P / rx;

            // Step 3: the centre in the layout's own frame.
            var cx = cos * cxP - sin * cyP + (x1 + x2) / 2;
            var cy = sin * cxP + cos * cyP + (y1 + y2) / 2;

            // Step 4: where the arc starts, and how far it turns, on the unit circle.
            var start = Math.Atan2((y1P - cyP) / ry, (x1P - cxP) / rx);
            var turn = Math.Atan2((-y1P - cyP) / ry, (-x1P - cxP) / rx) - start;
            if (sweep && turn < 0)
                turn += 2 * Math.PI;
            else if (!sweep && turn > 0)
                turn -= 2 * Math.PI;

            // A quarter turn or less per curve keeps a cubic within a few millionths of the arc.
            var pieces = (int)Math.Ceiling(Math.Abs(turn) / (Math.PI / 2) - 1e-9);
            if (pieces < 1)
            {
                // Ends too close together for the arithmetic to find a turn between them.
                LineTo(point);
                return;
            }

            var step = turn / pieces;
            var handle = 4.0 / 3.0 * Math.Tan(step / 4);

            Vector2 OnEllipse(double ux, double uy) => new(
                (float)(cx + rx * ux * cos - ry * uy * sin),
                (float)(cy + rx * ux * sin + ry * uy * cos));

            for (var piece = 0; piece < pieces; piece++)
            {
                var a = start + piece * step;
                var b = a + step;
                double cosA = Math.Cos(a), sinA = Math.Sin(a), cosB = Math.Cos(b), sinB = Math.Sin(b);

                CubicBezierTo(
                    OnEllipse(cosA - handle * sinA, sinA + handle * cosA),
                    OnEllipse(cosB + handle * sinB, sinB - handle * cosB),
                    // The last curve ends exactly where the arc was asked to, not where the sum of
                    // the steps rounds to.
                    piece == pieces - 1 ? point : OnEllipse(cosB, sinB));
            }
        }

        // A colour glyph paints its layers and groups one over another. An outline has one fill, so
        // every layer's geometry goes into it and the painting instructions are ignored.

        public void BeginLayer(Paint paint, FillRule fillRule)
        {
            // Nothing to do: see above.
        }

        public void EndLayer()
        {
            // Nothing to do: see above.
        }

        public void BeginGroup(CompositeMode mode)
        {
            // Nothing to do: see above.
        }

        public void EndGroup()
        {
            // Nothing to do: see above.
        }
#endif

        public void EndFigure() => _segments.Add(XGlyphSegment.Close());

        public void BeginText(in FontRectangle bounds)
        {
            // Nothing to do: the outlines are collected per glyph.
        }

        public void EndText()
        {
            // Nothing to do: the outlines are collected per glyph.
        }

        public TextDecorations EnabledDecorations() => TextDecorations.None;

#if IMAGESHARP4
        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness,
            ReadOnlyMemory<float> intersections)
#else
        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
#endif
        {
            // Underlines and strikeouts are drawn by the text path, not carved into a glyph.
        }

        /// <summary>
        ///   A laid-out point in the seam's space: measured from the baseline, and the right way
        ///   up. SixLabors measures y downwards from the top of the line, as a raster does.
        /// </summary>
        private XPoint At(Vector2 point) => new(point.X / _scale, _baseline - point.Y / _scale);
    }
}
