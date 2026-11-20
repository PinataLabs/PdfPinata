using System;
using System.Collections.Generic;
using PdfPinata.Internal;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// One font dictionary, ready to set text with: how its shown strings split into codes, how far
/// each code advances, and the outline of each glyph.
/// </summary>
/// <remarks>
/// Made once for a font dictionary and kept, so that a glyph's outline is one
/// <see cref="PagePath"/> however often it is drawn. That is what gives the path the identity a
/// device caches on. Outlines are kept by glyph and not by code, so codes that draw one glyph hold
/// one path, and only up to <see cref="KeptPointBudget"/> points of them.
/// </remarks>
internal sealed class RenderFont
{
    private readonly FontWidths _widths;
    private readonly VerticalMetrics _vertical;
    private readonly Dictionary<int, (PagePath Outline, string Why)> _outlines = new();
    private int _keptPoints;

    /// <summary>
    /// The most points of outline one font keeps, about 20 MB. A glyph that would take the font
    /// past it is decoded each time it is drawn and handed over unshared, which costs time and
    /// not memory: a /CIDToGIDMap or a loca table can name one glyph of 65,536 points tens of
    /// thousands of times in a few hundred bytes.
    /// </summary>
    internal const int KeptPointBudget = 1 << 20;

    private const string NoGlyph = "The font has no glyph for a code the text shows.";

    private RenderFont(bool isComposite, FontWidths widths, VerticalMetrics vertical)
    {
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
    internal static RenderFont From(PdfDictionary font, Func<PdfDictionary, PageGlyphSource> custom)
    {
        var isComposite = font.Elements.GetNameOrEmpty("/Subtype") == "/Type0";
        var descendants = isComposite ? font.Elements.GetArray("/DescendantFonts") : null;
        var descendant = descendants != null && descendants.Elements.Count > 0
            ? PdfReference.Dereference(descendants.Elements[0]) as PdfDictionary
            : null;

        // A composite font with no descendant states no widths, and every CID takes the default.
        var metricsOf = isComposite ? descendant ?? new PdfDictionary() : null;
        var vertical = isComposite && PdfItemValues.TryGetName(font.Elements["/Encoding"], out var cmap) && cmap == "/Identity-V"
            ? VerticalMetrics.From(metricsOf)
            : null;
        var renderFont = new RenderFont(isComposite, FontWidths.From(font, metricsOf), vertical);
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
            renderFont.Source = BuiltInGlyphSources.Select(font, descendant, out var why);
            renderFont.UnsupportedReason = why;
        }

        return renderFont;
    }

    /// <summary>
    /// How far a code advances, in text space before the type size is applied: the font
    /// dictionary's own width, or else what the glyph source knows, or else nothing.
    /// </summary>
    internal double WidthOf(int code)
    {
        var stated = _widths.Of(code);
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
    internal (double Advance, double X, double Y) VerticalMetricsOf(int code) => _vertical.Of(code, WidthOf(code));

    /// <summary>
    /// The outline of a code's glyph: the same instance every time while the font is within
    /// <see cref="KeptPointBudget"/>, empty for a glyph that draws nothing, and null where the font
    /// has no such glyph or no source at all.
    /// </summary>
    /// <param name="code">The character code of a simple font, or the CID of a composite one.</param>
    /// <param name="why">Why there is no glyph, where there is none.</param>
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

        if (_outlines.TryGetValue(glyph, out var kept))
        {
            why = kept.Why;
            return kept.Outline;
        }

        PagePath outline = null;
        why = null;
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

        var points = outline?.Points.Length ?? 0;
        if (points > KeptPointBudget - _keptPoints)
            return outline;

        _keptPoints += points;
        outline = outline?.Shared();
        _outlines[glyph] = (outline, why);
        return outline;
    }

    /// <summary>
    /// Whether an exception from a glyph source says the font could not be read, which is
    /// recorded, and not that the process is out of memory or a seam is unset, which are not.
    /// </summary>
    private static bool IsAboutTheFont(Exception ex) => !Unrecoverable.Is(ex) && ex is not UnsetSeamException;

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

/// <summary>
/// The fonts of one document that page rendering has read, each made once and kept, so that a
/// font program is parsed once however many pages use it.
/// </summary>
/// <remarks>
/// <para>
/// The key is the font dictionary's reference, which a document holds one instance of for an
/// object. A font dictionary written in place has no reference, and is its own key: it belongs to
/// the one resource dictionary it is written in.
/// </para>
/// <para>
/// Nothing here is synchronised. One page of a document is rendered at a time, as
/// <see cref="PageRendering.Render"/> says.
/// </para>
/// </remarks>
internal sealed class RenderFontCache
{
    private readonly Dictionary<object, RenderFont> _fonts = new(ReferenceIdentity.Instance);

    /// <summary>The font for a font dictionary, read the first time it is asked for. Never null.</summary>
    internal RenderFont For(PdfDictionary font, Func<PdfDictionary, PageGlyphSource> custom)
    {
        var key = ReferenceIdentity.KeyOf(font);
        if (!_fonts.TryGetValue(key, out var renderFont))
            _fonts[key] = renderFont = RenderFont.From(font, custom);
        return renderFont;
    }
}
