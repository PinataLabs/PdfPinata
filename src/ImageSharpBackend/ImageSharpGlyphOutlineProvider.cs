using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using SixLabors.Fonts;
#if IMAGESHARP4
using SixLabors.Fonts.Rendering;
#endif
using SixLabors.Fonts.Unicode;

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

        var options = new TextOptions(font) { Dpi = PointsPerInch, Origin = Vector2.Zero };

        var collector = new OutlineCollector(BaselineOf(font, text));
#if IMAGESHARP4
        TextRenderer.RenderTo(collector, text, options);
#else
        TextRenderer.RenderTextTo(collector, text, options);
#endif
        return collector.Outlines;
    }

    /// <summary>
    /// How far below the origin the baseline of the laid-out line sits, in points.
    /// </summary>
    /// <remarks>
    /// The layout puts the top of the line box on the origin while this seam measures from the
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

#if IMAGESHARP4
        // Fonts 3 answers one glyph rather than a list, and asks for a palette; null is the font's
        // default, which is what spec #305 settles on.
        if (!metrics.TryGetGlyphMetrics(codePoint, TextAttributes.None, TextDecorations.None,
                LayoutMode.HorizontalTopBottom, ColorFontSupport.None, null, out var glyph))
            return 0;

        return glyph.TopSideBearing < 0 ? -glyph.TopSideBearing : 0;
#else
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
#endif
    }

    /// <summary>
    /// Collects what SixLabors.Fonts draws into one <see cref="XGlyphOutline"/> per glyph.
    /// </summary>
    private sealed class OutlineCollector : IGlyphRenderer
    {
        private readonly double _baseline;
        private readonly List<XGlyphOutline> _outlines = [];
        private List<XGlyphSegment> _segments = [];
        private XPoint _current;

        internal OutlineCollector(double baseline)
        {
            _baseline = baseline;
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
            _segments.Add(XGlyphSegment.StartAt(_current));
        }

        public void LineTo(Vector2 point)
        {
            _current = At(point);
            _segments.Add(XGlyphSegment.LineTo(_current));
        }

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
        {
            var end = At(point);
            _segments.Add(XGlyphSegment.QuadraticTo(_current, At(secondControlPoint), end));
            _current = end;
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        {
            _current = At(point);
            _segments.Add(XGlyphSegment.CurveTo(At(secondControlPoint), At(thirdControlPoint), _current));
        }

#if IMAGESHARP4
        public void ArcTo(float radiusX, float radiusY, float rotation, bool largeArc, bool sweep, Vector2 point)
        {
            // TODO(#308): spec #305 adds an arc to the outline as cubic Béziers. Until then an arc is
            // refused rather than silently dropped, which would leave a hole in the glyph.
            throw new NotImplementedException("ArcTo is not yet translated into the glyph outline (#308).");
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
        private XPoint At(Vector2 point) => new(point.X, _baseline - point.Y);
    }
}
