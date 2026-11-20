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
/// device caches on.
/// </remarks>
internal sealed class RenderFont
{
    private readonly FontWidths _widths;
    private readonly Dictionary<int, PagePath> _outlines = new();

    private RenderFont(bool isComposite, FontWidths widths)
    {
        IsComposite = isComposite;
        _widths = widths;
    }

    /// <summary>Whether this is a Type 0 font, whose codes are two bytes and name CIDs.</summary>
    internal bool IsComposite { get; }

    /// <summary>How many bytes of a shown string make one code.</summary>
    internal int BytesPerCode => IsComposite ? 2 : 1;

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
        var renderFont = new RenderFont(isComposite,
            FontWidths.From(font, isComposite ? descendant ?? new PdfDictionary() : null));
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
    /// The outline of a code's glyph: the same instance every time, empty for a glyph that draws
    /// nothing, and null where the font has no such glyph or no source at all.
    /// </summary>
    /// <param name="code">The character code of a simple font, or the CID of a composite one.</param>
    /// <param name="why">Why there is no glyph, where there is none.</param>
    internal PagePath OutlineOf(int code, out string why)
    {
        why = null;
        if (Source == null)
        {
            why = UnsupportedReason;
            return null;
        }

        if (_outlines.TryGetValue(code, out var outline))
        {
            if (outline == null)
                why = "The font has no glyph for a code the text shows.";
            return outline;
        }

        try
        {
            outline = Source.GetOutline(code)?.Shared();
            if (outline == null)
                why = "The font has no glyph for a code the text shows.";
        }
        catch (Exception ex) when (IsAboutTheFont(ex))
        {
            why = ex.Message;
        }

        _outlines[code] = outline;
        return outline;
    }

    /// <summary>
    /// Whether an exception from a glyph source says the font could not be read, which is
    /// recorded, and not that the process is out of memory or a seam is unset, which are not.
    /// </summary>
    private static bool IsAboutTheFont(Exception ex) => !Unrecoverable.Is(ex) && ex is not UnsetSeamException;

    /// <summary>
    /// A composite font's codes are read as two bytes that are the CID, which is what
    /// <c>Identity-H</c> says and what this library writes. Any other CMap is read the same way
    /// until CMaps are, and vertical writing is set horizontally.
    /// </summary>
    private static string EncodingNoteOf(PdfDictionary font)
    {
        PdfItemValues.TryGetName(font.Elements["/Encoding"], out var encoding);
        return encoding switch
        {
            "/Identity-H" => null,
            "/Identity-V" => "Vertical writing is not set yet. The text is set horizontally.",
            _ => "The font's CMap is not read yet. Its codes are read as two bytes that are the CID."
        };
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
    private readonly Dictionary<object, RenderFont> _fonts = new(ByReference.Instance);

    /// <summary>The font for a font dictionary, read the first time it is asked for. Never null.</summary>
    internal RenderFont For(PdfDictionary font, Func<PdfDictionary, PageGlyphSource> custom)
    {
        var key = (object)font.Reference ?? font;
        if (!_fonts.TryGetValue(key, out var renderFont))
            _fonts[key] = renderFont = RenderFont.From(font, custom);
        return renderFont;
    }

    /// <summary>
    /// Compares keys by which object they are. <c>ReferenceEqualityComparer</c> does the same and
    /// is not in netstandard2.1.
    /// </summary>
    private sealed class ByReference : IEqualityComparer<object>
    {
        internal static readonly ByReference Instance = new();

        bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

        int IEqualityComparer<object>.GetHashCode(object obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
