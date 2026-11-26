using System;
using PdfPinata.Internal;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// One font dictionary, ready to set text with: how its shown strings split into codes, how far
/// each code advances, and the outline of each glyph.
/// </summary>
/// <remarks>
/// <para>
/// <b>This keeps nothing that grows</b>: references to the font's own objects, a few numbers and
/// its glyph source. The widths, the outlines and everything else read from the font are kept by
/// the document's <see cref="RenderFontCache"/>, under the object each was read from, and asked
/// for there each time. That is the rule the cache states, and a collection added here breaks it.
/// </para>
/// <para>
/// A glyph's outline is one <see cref="PagePath"/> however often it is drawn, which is what gives
/// the path the identity a device caches on. Outlines are kept by glyph and not by code, so codes
/// that draw one glyph hold one path, and only while the cache has room for them.
/// </para>
/// </remarks>
internal sealed class RenderFont
{
    private readonly RenderFontCache _cache;
    private readonly FontWidths _widths;
    private readonly VerticalMetrics _vertical;

    private const string NoGlyph = "The font has no glyph for a code the text shows.";

    private RenderFont(RenderFontCache cache, bool isComposite, FontWidths widths, VerticalMetrics vertical)
    {
        _cache = cache;
        IsComposite = isComposite;
        _widths = widths;
        _vertical = vertical;
    }

    /// <summary>Whether this is a Type 0 font, whose codes are two bytes and name CIDs.</summary>
    internal bool IsComposite { get; }

    /// <summary>How many bytes of a shown string make one code.</summary>
    internal int BytesPerCode => IsComposite ? 2 : 1;

    /// <summary>
    /// Whether the text is set in vertical writing, down the page: a composite font whose CMap is
    /// <c>Identity-V</c>.
    /// </summary>
    internal bool IsVertical => _vertical != null;

    /// <summary>
    /// Where the outlines come from, or null where this version has no source for the font, in
    /// which case <see cref="UnsupportedReason"/> says why.
    /// </summary>
    internal PageGlyphSource Source { get; private set; }

    /// <summary>Why the font has no <see cref="Source"/>, where it has none.</summary>
    internal string UnsupportedReason { get; private set; }

    /// <summary>
    /// What about the font's encoding is not read as written, or null. The text is still set,
    /// by the reading that is right for most such fonts.
    /// </summary>
    internal string EncodingNote { get; private set; }

    /// <summary>Reads a font dictionary.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names.</param>
    /// <param name="custom">Asked for the font's glyph source before the library's own, or null.</param>
    /// <param name="cache">What keeps everything read from the font.</param>
    internal static RenderFont From(PdfDictionary font, Func<PdfDictionary, PageGlyphSource> custom, RenderFontCache cache)
    {
        var isComposite = font.Elements.GetNameOrEmpty("/Subtype") == "/Type0";
        var descendants = isComposite ? font.Elements.GetArray("/DescendantFonts") : null;
        var descendant = descendants != null && descendants.Elements.Count > 0
            ? PdfReference.Dereference(descendants.Elements[0]) as PdfDictionary
            : null;

        // A composite font with no descendant states no widths, and every CID takes the default.
        var metricsOf = isComposite ? descendant ?? new PdfDictionary() : null;
        var vertical = isComposite && PdfItemValues.TryGetName(font.Elements["/Encoding"], out var cmap) && cmap == "/Identity-V"
            ? VerticalMetrics.Of(metricsOf)
            : null;
        var renderFont = new RenderFont(cache, isComposite, FontWidths.Of(font, metricsOf), vertical);
        if (isComposite)
            renderFont.EncodingNote = EncodingNoteOf(font);

        try
        {
            renderFont.Source = custom?.Invoke(font);
        }
        catch (Exception ex) when (IsAboutTheFont(ex))
        {
            renderFont.UnsupportedReason = ex.Message;
            return renderFont;
        }

        if (renderFont.Source == null)
        {
            renderFont.Source = BuiltInGlyphSources.Select(font, descendant, cache, out var why);
            renderFont.UnsupportedReason = why;
        }

        return renderFont;
    }

    /// <summary>
    /// How far a code advances, in text space before the type size is applied: the font
    /// dictionary's own width, or else what the glyph source knows, or else nothing.
    /// </summary>
    /// <exception cref="FontDataNotReadException">The widths are not kept, and the page has read all the font data it may.</exception>
    internal double WidthOf(int code)
    {
        var stated = _widths.Of(code, _cache);
        if (stated != null)
            return stated.Value;

        try
        {
            return Source != null && Source.TryGetAdvance(code, out var advance) && double.IsFinite(advance) ? advance : 0;
        }
        catch (Exception ex) when (IsAboutTheFont(ex))
        {
            return 0;
        }
    }

    /// <summary>
    /// How a code of a font in vertical writing is placed and advanced, in text space before the
    /// type size is applied: the advance, negative for down the page, and the position vector that
    /// is put on the pen. Asked only where <see cref="IsVertical"/>.
    /// </summary>
    /// <exception cref="FontDataNotReadException">The metrics are not kept, and the page has read all the font data it may.</exception>
    internal (double Advance, double X, double Y) VerticalMetricsOf(int code) => _vertical.Of(code, WidthOf(code), _cache);

    /// <summary>
    /// The outline of a code's glyph: the same instance every time where the cache had room to
    /// keep it, empty for a glyph that draws nothing, and null where the font has no such glyph
    /// or no source at all.
    /// </summary>
    /// <param name="code">The character code of a simple font, or the CID of a composite one.</param>
    /// <param name="why">Why there is no glyph, where there is none.</param>
    /// <exception cref="FontDataNotReadException">
    /// What the glyph is read from is not kept, and the page has read all the font data it may. That
    /// is true of the page and not of the glyph, so it is not kept as the glyph's answer.
    /// </exception>
    internal PagePath OutlineOf(int code, out string why)
    {
        if (Source == null)
        {
            why = UnsupportedReason;
            return null;
        }

        int glyph;
        try
        {
            glyph = Source.GlyphOf(code);
        }
        catch (Exception ex) when (IsAboutTheFont(ex))
        {
            why = ex.Message;
            return null;
        }

        if (glyph < 0)
        {
            why = NoGlyph;
            return null;
        }

        var owner = Source.OutlineOwner;
        if (_cache.TryGetOutline(owner, glyph, out var kept, out why))
            return kept;

        PagePath outline = null;
        try
        {
            outline = Source.OutlineOfGlyph(glyph);
            if (outline == null)
                why = NoGlyph;
        }
        catch (Exception ex) when (IsAboutTheFont(ex))
        {
            why = ex.Message;
        }

        return _cache.KeepOutline(owner, glyph, outline, why);
    }

    /// <summary>
    /// Whether an exception from a glyph source says the font could not be read, which is
    /// recorded, and not that the process is out of memory, a seam is unset or the page has read
    /// all the font data it may, which are not.
    /// </summary>
    private static bool IsAboutTheFont(Exception ex) =>
        !Unrecoverable.Is(ex) && ex is not UnsetSeamException and not FontDataNotReadException;

    /// <summary>
    /// A composite font's codes are read as two bytes that are the CID, which is what
    /// <c>Identity-H</c> and <c>Identity-V</c> say and what this library writes. Any other CMap,
    /// named or embedded, is read the same way until CMaps are, and is set horizontally.
    /// </summary>
    private static string EncodingNoteOf(PdfDictionary font)
    {
        PdfItemValues.TryGetName(font.Elements["/Encoding"], out var encoding);
        return encoding is "/Identity-H" or "/Identity-V"
            ? null
            : "The font's CMap is not read yet. Its codes are read as two bytes that are the CID.";
    }
}
