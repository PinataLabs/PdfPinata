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
/// one path, and only while the document's <see cref="RenderFontCache"/> has room for them.
/// </remarks>
internal sealed class RenderFont
{
    private readonly RenderFontCache _cache;
    private readonly FontWidths _widths;
    private readonly VerticalMetrics _vertical;
    private readonly Dictionary<int, (PagePath Outline, string Why)> _outlines = new();

    private const string NoGlyph = "The font has no glyph for a code the text shows.";

    private RenderFont(RenderFontCache cache, bool isComposite, FontWidths widths, VerticalMetrics vertical)
    {
        _cache = cache;
        IsComposite = isComposite;
        _widths = widths;
        _vertical = vertical;
    }

    /// <summary>How many points of outline this font keeps, all of them charged to the cache.</summary>
    internal long KeptPoints { get; private set; }

    /// <summary>How many bytes of font program the source holds, as last charged to the cache.</summary>
    internal long ProgramBytes { get; private set; }

    /// <summary>When the font was last selected, by the cache's clock.</summary>
    internal long LastUsed { get; set; }

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
    /// <param name="cache">What the font charges the outlines it keeps and the program it holds to.</param>
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
            ? VerticalMetrics.From(metricsOf)
            : null;
        var renderFont = new RenderFont(cache, isComposite, FontWidths.From(font, metricsOf), vertical);
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
        finally
        {
            ChargeProgram();
        }
    }

    /// <summary>
    /// How a code of a font in vertical writing is placed and advanced, in text space before the
    /// type size is applied: the advance, negative for down the page, and the position vector that
    /// is put on the pen. Asked only where <see cref="IsVertical"/>.
    /// </summary>
    internal (double Advance, double X, double Y) VerticalMetricsOf(int code) => _vertical.Of(code, WidthOf(code));

    /// <summary>
    /// The outline of a code's glyph: the same instance every time where the cache had room to
    /// keep it, empty for a glyph that draws nothing, and null where the font has no such glyph
    /// or no source at all.
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
        finally
        {
            ChargeProgram();
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
        finally
        {
            ChargeProgram();
        }

        var points = outline?.Points.Length ?? 0;
        if (!_cache.TryKeep(points))
            return outline;

        KeptPoints += points;
        outline = outline?.Shared();
        _outlines[glyph] = (outline, why);
        return outline;
    }

    /// <summary>
    /// Lets go of the font program the source holds, which it reads again if it is asked for
    /// another glyph. The outlines kept are not touched, so a device's paths keep their identity.
    /// </summary>
    /// <returns>How many bytes were let go of.</returns>
    internal long ReleaseProgram()
    {
        var released = ProgramBytes;
        Source?.Release();
        ProgramBytes = 0;
        return released;
    }

    /// <summary>Tells the cache how much font program the source holds now, where that has changed.</summary>
    private void ChargeProgram()
    {
        var held = Source?.HeldBytes ?? 0;
        if (held == ProgramBytes)
            return;

        var change = held - ProgramBytes;
        ProgramBytes = held;
        _cache.ProgramChanged(this, change);
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
/// The fonts of one document that page rendering has read, so that a font program is parsed once
/// however many pages use it, and what all of them together may hold.
/// </summary>
/// <remarks>
/// <para>
/// The key is the font dictionary's reference, which a document holds one instance of for an
/// object. A font dictionary written in place has no reference, and is its own key: it belongs to
/// the one resource dictionary it is written in.
/// </para>
/// <para>
/// <b>Memory is bounded for the document, not for each font</b>, because a font dictionary costs
/// about 150 bytes of file and can name a font program of a few kilobytes whose glyphs fill any
/// budget a font is given. Three bounds hold it:
/// </para>
/// <list type="bullet">
/// <item><b>Outlines, within a page:</b> at most <see cref="OutlinePointBudget"/> points kept for all
/// fonts together. A glyph past it is decoded each time it is drawn and handed over unshared. No
/// outline is let go of during a page, so a device that keeps a path for each shared one keeps no
/// more than the budget's worth.</item>
/// <item><b>Font programs, at any time:</b> at most <see cref="ProgramByteBudget"/> bytes held for all
/// fonts together. Past it, the programs of the fonts least recently selected are let go of, and
/// read again if they are asked for. That changes no path a device has been handed.</item>
/// <item><b>Fonts, between pages:</b> where outlines or programs are over half their budget when a
/// page begins, the fonts least recently selected are forgotten, whole, until both are under it.
/// No font is in use between pages, and the next page that needs one reads it again.</item>
/// </list>
/// <para>
/// Nothing here is synchronised. One page of a document is rendered at a time, as
/// <see cref="PageRendering.Render"/> says.
/// </para>
/// </remarks>
internal sealed class RenderFontCache
{
    /// <summary>
    /// The most points of outline a document keeps for all its fonts, about 17 MiB. A
    /// /CIDToGIDMap or a loca table can name one glyph of 65,536 points tens of thousands of times
    /// in a few hundred bytes, and font dictionaries that share a font program cost little more.
    /// </summary>
    internal const int OutlinePointBudget = 1 << 20;

    /// <summary>
    /// The most bytes of decoded font program a document holds for all its fonts: room for two
    /// CJK fonts embedded whole beside the fonts of a page of Latin text, so that a page setting
    /// lines in each in turn does not read them again at every turn.
    /// </summary>
    internal const long ProgramByteBudget = 64L << 20;

    private readonly Dictionary<object, RenderFont> _fonts = new(ReferenceIdentity.Instance);
    private long _clock;
    private long _keptPoints;
    private long _programBytes;

    /// <summary>The font for a font dictionary, read the first time it is asked for. Never null.</summary>
    internal RenderFont For(PdfDictionary font, Func<PdfDictionary, PageGlyphSource> custom)
    {
        var key = ReferenceIdentity.KeyOf(font);
        if (!_fonts.TryGetValue(key, out var renderFont))
            _fonts[key] = renderFont = RenderFont.From(font, custom, this);
        renderFont.LastUsed = ++_clock;
        return renderFont;
    }

    /// <summary>
    /// Called as a page begins: forgets the fonts least recently selected while outlines or
    /// programs are over half their budget, so that a long render keeps room for each new page.
    /// </summary>
    internal void BeginPage()
    {
        if (!OverHalf())
            return;

        var oldestFirst = new List<KeyValuePair<object, RenderFont>>(_fonts);
        oldestFirst.Sort((a, b) => a.Value.LastUsed.CompareTo(b.Value.LastUsed));
        foreach (var (key, font) in oldestFirst)
        {
            if (!OverHalf())
                break;
            _fonts.Remove(key);
            _keptPoints -= font.KeptPoints;
            _programBytes -= font.ProgramBytes;
        }
    }

    /// <summary>Takes room for an outline of so many points, where there is room.</summary>
    internal bool TryKeep(int points)
    {
        if (points > OutlinePointBudget - _keptPoints)
            return false;
        _keptPoints += points;
        return true;
    }

    /// <summary>
    /// Records that a font's source holds more or less font program, and where the total is then
    /// over budget, lets go of the programs of the fonts least recently selected but that one.
    /// </summary>
    internal void ProgramChanged(RenderFont font, long change)
    {
        _programBytes += change;
        if (_programBytes <= ProgramByteBudget)
            return;

        var holding = new List<RenderFont>();
        foreach (var other in _fonts.Values)
        {
            if (other != font && other.ProgramBytes > 0)
                holding.Add(other);
        }

        holding.Sort((a, b) => a.LastUsed.CompareTo(b.LastUsed));
        foreach (var other in holding)
        {
            if (_programBytes <= ProgramByteBudget)
                break;
            _programBytes -= other.ReleaseProgram();
        }
    }

    private bool OverHalf() => _keptPoints > OutlinePointBudget / 2 || _programBytes > ProgramByteBudget / 2;
}
