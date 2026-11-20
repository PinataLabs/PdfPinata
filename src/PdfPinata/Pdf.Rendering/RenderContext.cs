using System;
using System.Collections.Generic;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// What one render keeps that every <see cref="ContentInterpreter"/> of it shares: the one that
/// draws the page, and each one that records content drawn apart, such as a tiling pattern's
/// cell.
/// </summary>
/// <remarks>
/// What is shared is what must be counted or kept once for the whole render: where problems are
/// reported, the limits on content that draws content, and everything read from the file so far.
/// What an interpreter has of its own is what belongs to where it draws: its device, its state
/// stack and its surface.
/// </remarks>
internal sealed class RenderContext
{
    /// <summary>
    /// How many shadings and how many tiling patterns a render keeps. One past it is read each
    /// time it is painted with, and its shader says it is not shared.
    /// </summary>
    internal const int MaximumShaders = 1024;

    /// <summary>How many colours one uncoloured tiling pattern is kept in.</summary>
    internal const int MaximumTints = 32;

    private readonly Dictionary<object, ReadShading> _shadings = new(ReferenceIdentity.Instance);
    private readonly Dictionary<object, List<(PagePaint? Tint, PageTiling Tiling)>> _tilings = new(ReferenceIdentity.Instance);

    internal RenderContext(RenderFontCache fonts, Func<PdfDictionary, PageGlyphSource> glyphSources)
    {
        Fonts = fonts;
        GlyphSources = glyphSources;
    }

    internal DiagnosticSink Sink { get; } = new();

    internal RenderFontCache Fonts { get; }

    internal Func<PdfDictionary, PageGlyphSource> GlyphSources { get; }

    /// <summary>Counts forms and pattern cells together: either may draw the other.</summary>
    internal FormVisits Forms { get; } = new();

    /// <summary>
    /// Each form's or cell's content as it was read the first time it was drawn, or null where it
    /// could not be decoded. A form drawn a thousand times on a page is decoded and parsed once.
    /// </summary>
    internal Dictionary<object, ContentInterpreter.ParsedContent> FormContent { get; } = new(ReferenceIdentity.Instance);

    /// <summary>
    /// Each image XObject's samples as they were decoded the first time it was drawn, or null where
    /// they could not be. See <c>ContentInterpreter.ImageOf</c>.
    /// </summary>
    internal Dictionary<object, PageImage> Images { get; } = new(ReferenceIdentity.Instance);

    /// <summary>How many bytes of samples <see cref="Images"/> holds.</summary>
    internal long ImageBytesKept { get; set; }

    internal PdfDocument Document { get; set; }

    /// <summary>Read from the document the first time content asks about a group.</summary>
    internal OptionalContent OptionalContent { get; set; }

    /// <summary>The device the page is drawn onto, which decodes for content that is recorded too.</summary>
    internal PageDevice Decoder { get; set; }

    /// <summary>
    /// A shading dictionary as it was read the first time it was painted with, which may be null
    /// for one that could not be read.
    /// </summary>
    internal ReadShading ShadingOf(PdfDictionary shading, string subject)
    {
        var key = ReferenceIdentity.KeyOf(shading);
        if (_shadings.TryGetValue(key, out var read))
            return read;

        var kept = _shadings.Count < MaximumShaders;
        read = Shadings.Read(shading, kept, Sink, subject);
        if (kept)
            _shadings.Add(key, read);
        return read;
    }

    /// <summary>The tiling a pattern was recorded as in a colour, or null where it has not been.</summary>
    internal PageTiling TilingOf(PdfDictionary pattern, PagePaint? tint)
    {
        if (_tilings.TryGetValue(ReferenceIdentity.KeyOf(pattern), out var tints))
        {
            foreach (var (kept, tiling) in tints)
            {
                if (Nullable.Equals(kept, tint))
                    return tiling;
            }
        }

        return null;
    }

    /// <summary>Whether a tiling recorded now for a pattern in a colour would be kept.</summary>
    internal bool KeepsTiling(PdfDictionary pattern) =>
        _tilings.TryGetValue(ReferenceIdentity.KeyOf(pattern), out var tints)
            ? tints.Count < MaximumTints
            : _tilings.Count < MaximumShaders;

    internal void Keep(PdfDictionary pattern, PagePaint? tint, PageTiling tiling)
    {
        var key = ReferenceIdentity.KeyOf(pattern);
        if (!_tilings.TryGetValue(key, out var tints))
            _tilings.Add(key, tints = []);
        tints.Add((tint, tiling));
    }
}
