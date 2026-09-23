using System;
using System.Collections.Generic;
using System.IO;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using SkiaSharp;

namespace PdfPinata.Utils;

/// <summary>
/// Reads glyph outlines through SkiaSharp, so that
/// <see cref="PdfPinata.Drawing.XGraphicsPath"/>.AddString can produce real geometry.
/// Register it once before adding text to a path:
/// <c>GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();</c>
/// </summary>
/// <remarks>
/// Skia decodes both TrueType and PostScript (CFF) outlines, so a family of either kind produces a
/// path; that is why the outlines come from a backend at all rather than from a <c>glyf</c> decoder
/// written here.
/// <para>
/// A weight or a slant the font has no file for is simulated when text is <i>drawn</i>, by stroking
/// or skewing. Nothing simulates it here: an outline is the shape the font actually holds.
/// </para>
/// </remarks>
public sealed class SkiaGlyphOutlineProvider : IGlyphOutlineProvider
{
    /// <inheritdoc />
    public IEnumerable<XGlyphOutline> GetOutlines(string text, string familyName, bool isBold, bool isItalic,
        double emSize)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<XGlyphOutline>();

        // Through the registered resolver, never around it: a provider that picked its own face
        // would one day disagree with the text that was drawn.
        var resolver = GlobalFontSettings.FontResolver;
        var info = resolver.ResolveTypeface(familyName, isBold, isItalic)
                   ?? throw new InvalidOperationException(
                       "The font resolver has no face for the family '" + familyName + "'.");

        var fontBytes = resolver.GetFont(info.FaceName);

        using var stream = new MemoryStream(fontBytes);
        using var typeface = SKTypeface.FromStream(stream)
                             ?? throw new InvalidOperationException(
                                 "SkiaSharp could not read the font '" + info.FaceName + "'.");
        // ReSharper disable once UsingStatementResourceInitialization
        using var font = new SKFont(typeface, (float)emSize)
        {
            // Hinting and rounded advances are for fitting glyphs to a grid of pixels. There is no
            // grid here: these outlines become a path in a PDF, drawn at whatever size the reader
            // decides. Left at Skia's defaults the advances come back rounded to whole points on a
            // platform whose font host hints - Linux does, Windows does not - so the same document
            // built on two machines put its glyphs in different places, and a path drawn beside
            // text drawn by DrawString drifted away from it, since that measures the font file
            // directly and never rounds.
            Hinting = SKFontHinting.None,
            LinearMetrics = true,
            Subpixel = true
        };

        var glyphs = font.GetGlyphs(text);
        var advances = font.GetGlyphWidths(glyphs);

        var outlines = new List<XGlyphOutline>(glyphs.Length);
        double pen = 0;
        for (var idx = 0; idx < glyphs.Length; idx++)
        {
            using var path = font.GetGlyphPath(glyphs[idx]);
            outlines.Add(OutlineOf(path, pen));

            // The run is advanced here so that the caller has one origin to place rather than one
            // per glyph. Advances come from the same font file the text is measured from.
            pen += idx < advances.Length ? advances[idx] : 0;
        }

        return outlines;
    }

    /// <summary>
    /// Walks a Skia path into segments of our own, turning the y axis over on the way.
    /// </summary>
    /// <remarks>
    /// Skia measures y downwards, so a glyph's outline comes out with the parts above the baseline
    /// at negative y. The seam is defined the way a font is - y upwards - so every ordinate is
    /// negated here.
    /// <para>
    /// Quadratics convert to cubics exactly, with controls at <c>p0 + 2/3(q - p0)</c> and
    /// <c>p2 + 2/3(q - p2)</c>, rather than being subdivided into something merely close.
    /// </para>
    /// </remarks>
    private static XGlyphOutline OutlineOf(SKPath path, double pen)
    {
        var segments = new List<XGlyphSegment>();
        if (path == null)
            return new XGlyphOutline(segments);

        var points = new SKPoint[4];
        var start = new XPoint();
        var current = new XPoint();

        using var iterator = path.CreateIterator(false);
        SKPathVerb verb;
        while ((verb = iterator.Next(points)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    current = start = At(points[0], pen);
                    segments.Add(XGlyphSegment.StartAt(current));
                    break;

                case SKPathVerb.Line:
                    current = At(points[1], pen);
                    segments.Add(XGlyphSegment.LineTo(current));
                    break;

                case SKPathVerb.Quad:
                case SKPathVerb.Conic:
                {
                    // A conic is a rational quadratic. Font outlines are not drawn with them, so
                    // treating one as its unweighted quadratic is a fallback that never fires
                    // rather than an approximation anyone relies on.
                    var control = At(points[1], pen);
                    var end = At(points[2], pen);
                    segments.Add(XGlyphSegment.CurveTo(
                        Lerp(current, control, 2.0 / 3.0),
                        Lerp(end, control, 2.0 / 3.0),
                        end));
                    current = end;
                    break;
                }

                case SKPathVerb.Cubic:
                    current = At(points[3], pen);
                    segments.Add(XGlyphSegment.CurveTo(At(points[1], pen), At(points[2], pen), current));
                    break;

                case SKPathVerb.Close:
                    segments.Add(XGlyphSegment.Close());
                    current = start;
                    break;
            }
        }

        return new XGlyphOutline(segments);
    }

    /// <summary>A Skia point in the seam's space: advanced along the run, and the right way up.</summary>
    private static XPoint At(SKPoint point, double pen) => new(pen + point.X, -point.Y);

    /// <summary>The point a fraction of the way from one point towards another.</summary>
    private static XPoint Lerp(XPoint from, XPoint to, double fraction) =>
        new(from.X + fraction * (to.X - from.X), from.Y + fraction * (to.Y - from.Y));
}
