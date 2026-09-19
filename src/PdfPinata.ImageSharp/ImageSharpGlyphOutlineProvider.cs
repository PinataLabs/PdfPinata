using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using SixLabors.Fonts;
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
    const float PointsPerInch = 72f;

    /// <inheritdoc />
    public IEnumerable<XGlyphOutline> GetOutlines(string text, string familyName, bool isBold, bool isItalic,
        double emSize)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<XGlyphOutline>();

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
        TextRenderer.RenderTextTo(collector, text, options);
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
    static double BaselineOf(Font font, string text)
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
    static double OvershootOf(FontMetrics metrics, string text)
    {
        double overshoot = 0;

        for (var idx = 0; idx < text.Length; idx++)
        {
            var codePoint = char.IsHighSurrogate(text[idx]) && idx + 1 < text.Length
                ? new CodePoint(char.ConvertToUtf32(text[idx], text[++idx]))
                : new CodePoint(text[idx]);

            // Whitespace is skipped by the layout, and so has no say in where the line sits.
            if (CodePoint.IsWhiteSpace(codePoint))
                continue;

            if (!metrics.TryGetGlyphMetrics(codePoint, TextAttributes.None, TextDecorations.None,
                    LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out var glyphs))
                continue;

            foreach (var glyph in glyphs)
            {
                if (glyph.TopSideBearing < 0)
                    overshoot = Math.Max(overshoot, -glyph.TopSideBearing);
            }
        }

        return overshoot;
    }

    /// <summary>
    /// Collects what SixLabors.Fonts draws into one <see cref="XGlyphOutline"/> per glyph.
    /// </summary>
    sealed class OutlineCollector : IGlyphRenderer
    {
        readonly double _baseline;
        readonly List<XGlyphOutline> _outlines = new List<XGlyphOutline>();
        List<XGlyphSegment> _segments = new List<XGlyphSegment>();
        XPoint _current;

        internal OutlineCollector(double baseline)
        {
            _baseline = baseline;
        }

        internal IReadOnlyList<XGlyphOutline> Outlines => _outlines;

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
        {
            _segments = new List<XGlyphSegment>();
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
            // Exactly, rather than by subdivision: a quadratic through q from p0 to p2 is the
            // cubic with controls at p0 + 2/3(q - p0) and p2 + 2/3(q - p2).
            var control = At(secondControlPoint);
            var end = At(point);
            _segments.Add(XGlyphSegment.CurveTo(
                Lerp(_current, control, 2.0 / 3.0),
                Lerp(end, control, 2.0 / 3.0),
                end));
            _current = end;
        }

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        {
            _current = At(point);
            _segments.Add(XGlyphSegment.CurveTo(At(secondControlPoint), At(thirdControlPoint), _current));
        }

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

        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
        {
            // Underlines and strikeouts are drawn by the text path, not carved into a glyph.
        }

        /// <summary>
        ///   A laid-out point in the seam's space: measured from the baseline, and the right way
        ///   up. SixLabors measures y downwards from the top of the line, as a raster does.
        /// </summary>
        XPoint At(Vector2 point) => new XPoint(point.X, _baseline - point.Y);

        static XPoint Lerp(XPoint from, XPoint to, double fraction) =>
            new XPoint(from.X + fraction * (to.X - from.X), from.Y + fraction * (to.Y - from.Y));
    }
}
