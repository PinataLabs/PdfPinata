using System;
using System.IO;
using PdfPinata.Fonts;
using PdfPinata.Fonts.OpenType;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The glyphs of a TrueType font program embedded in the file as <c>/FontFile2</c>, chosen by
/// the PDF's rules for a simple TrueType font or a <c>CIDFontType2</c> font.
/// </summary>
/// <remarks>
/// <para>
/// The font program is read here, glyph by glyph, and never handed to a platform's font engine:
/// DirectWrite and FreeType draw a broken subset differently from each other, and a page has to
/// look the same everywhere.
/// </para>
/// <para>
/// Nothing is read until the first outline is asked for, so a font used only for invisible text
/// costs nothing, and a font program that cannot be read is reported against each glyph shown in
/// it, by <see cref="RenderFont"/>, rather than thrown where the font is selected.
/// </para>
/// </remarks>
internal sealed class EmbeddedTrueTypeGlyphSource : PageGlyphSource
{
    private readonly PdfDictionary _font;
    private readonly PdfDictionary _descendant;
    private readonly PdfDictionary _descriptor;
    private readonly PdfDictionary _fontFile;

    private TrueTypeGlyphs _glyphs;
    private string _unreadable;
    private SimpleFontGlyphs _simple;
    private bool _cidMapRead;

    /// <summary>The <c>/CIDToGIDMap</c> stream, or null for <c>/Identity</c>.</summary>
    private byte[] _cidToGid;

    /// <summary>Reads nothing: the font program is read when the first outline is asked for.</summary>
    /// <param name="font">The font dictionary a <c>Tf</c> names.</param>
    /// <param name="descendant">The descendant of a composite font, or null for a simple one.</param>
    /// <param name="descriptor">The font descriptor that holds <paramref name="fontFile"/>.</param>
    /// <param name="fontFile">The <c>/FontFile2</c> stream.</param>
    internal EmbeddedTrueTypeGlyphSource(PdfDictionary font, PdfDictionary descendant, PdfDictionary descriptor,
        PdfDictionary fontFile)
    {
        _font = font;
        _descendant = descendant;
        _descriptor = descriptor;
        _fontFile = fontFile;
    }

    public override PagePath GetOutline(int code)
    {
        var glyphs = Glyphs();
        var glyph = _descendant != null ? GlyphOfCid(code) : (_simple ??= new SimpleFontGlyphs(glyphs, _font, _descriptor)).GlyphOf(code);

        // Glyph 0 is .notdef, which is what a font draws for a character it has no glyph for.
        if (glyph <= 0)
            return null;

        var outline = glyphs.GetOutline(glyph);
        return outline == null ? null : TrueTypeOutlinePaths.ToPagePath(outline, glyphs.UnitsPerEm);
    }

    /// <summary>
    /// The font program, read the first time it is needed. One that cannot be read throws for
    /// every glyph asked for, with the same message, and is not read again.
    /// </summary>
    private TrueTypeGlyphs Glyphs()
    {
        if (_glyphs != null)
            return _glyphs;
        if (_unreadable != null)
            throw new InvalidDataException(_unreadable);

        try
        {
            if (!PdfContentStreams.TryGetContent(_fontFile, out var bytes))
                throw new InvalidDataException("The embedded font program's stream cannot be decoded.");
            return _glyphs = TrueTypeGlyphs.Read(bytes);
        }
        catch (InvalidDataException ex)
        {
            _unreadable = ex.Message;
            throw;
        }
    }

    /// <summary>
    /// The glyph of a CID, through <c>/CIDToGIDMap</c>: <c>/Identity</c>, which is what an absent
    /// entry means, or a stream of two bytes for each CID. A CID past the end of the stream has no glyph.
    /// </summary>
    private int GlyphOfCid(int cid)
    {
        if (!_cidMapRead)
        {
            var map = PdfReference.Dereference(_descendant.Elements["/CIDToGIDMap"]) as PdfDictionary;
            if (map != null && !PdfContentStreams.TryGetContent(map, out _cidToGid))
                throw new InvalidDataException("The font's /CIDToGIDMap stream cannot be decoded.");
            _cidMapRead = true;
        }

        if (_cidToGid == null)
            return cid;

        var at = 2 * cid;
        return at + 1 < _cidToGid.Length ? _cidToGid[at] << 8 | _cidToGid[at + 1] : 0;
    }

    /// <summary>
    /// The glyph of each code of a simple TrueType font, by ISO 32000-1 9.6.6.4: through the
    /// encoding's glyph names and the (3,1) <c>cmap</c> for a nonsymbolic font, through the (3,0)
    /// <c>cmap</c> for a symbolic one, and through whatever else the font program offers after that.
    /// </summary>
    /// <remarks>
    /// Real files often say a font is symbolic when it is not, or the reverse, and embed subsets
    /// with one <c>cmap</c> subtable or none. So both ways are tried for every font, the one the
    /// flags name first, which is what the viewers that draw these files do.
    /// </remarks>
    private sealed class SimpleFontGlyphs
    {
        private readonly TrueTypeGlyphs _glyphs;
        private readonly bool _symbolic;
        private readonly string _baseEncoding;
        private readonly string[] _differences = new string[256];
        private readonly TrueTypeCharacterMap _unicode;
        private readonly TrueTypeCharacterMap _symbol;
        private readonly TrueTypeCharacterMap _macintosh;
        private readonly bool _hasNoCharacterMap;
        private readonly int[] _chosen = new int[256];

        public SimpleFontGlyphs(TrueTypeGlyphs glyphs, PdfDictionary font, PdfDictionary descriptor)
        {
            _glyphs = glyphs;
            var encoding = PdfReference.Dereference(font.Elements["/Encoding"]);
            if (PdfItemValues.TryGetName(encoding, out var name))
            {
                _baseEncoding = name;
            }
            else if (encoding is PdfDictionary dictionary)
            {
                PdfItemValues.TryGetName(dictionary.Elements["/BaseEncoding"], out _baseEncoding);
                ReadDifferences(dictionary.Elements.GetArray("/Differences"));
            }

            // ISO 32000-1 9.6.6.4: a font that names WinAnsiEncoding or MacRomanEncoding is read
            // by glyph name whatever its flags say, as one whose Nonsymbolic flag is set is.
            var flags = PdfItemValues.TryGetNumber(descriptor.Elements["/Flags"], out var stated) ? (int)stated : 0;
            _symbolic = (flags & SymbolicFlag) != 0 && (flags & NonsymbolicFlag) == 0
                && _baseEncoding is not ("/WinAnsiEncoding" or "/MacRomanEncoding");

            // A nonsymbolic font that names no base encoding has the standard one.
            if (_baseEncoding == null && !_symbolic)
                _baseEncoding = "/StandardEncoding";

            _unicode = glyphs.CharacterMap(3, 1) ?? glyphs.CharacterMap(3, 10) ?? glyphs.CharacterMap(0, 3)
                ?? glyphs.CharacterMap(0, 4);
            _symbol = glyphs.CharacterMap(3, 0);
            _macintosh = glyphs.CharacterMap(1, 0);
            _hasNoCharacterMap = _unicode == null && _symbol == null && _macintosh == null;

            Array.Fill(_chosen, -1);
        }

        /// <summary>The glyph of a code, or 0 where the font has none for it.</summary>
        public int GlyphOf(int code)
        {
            if ((uint)code > 255)
                return 0;
            if (_chosen[code] < 0)
                _chosen[code] = Choose(code);
            return _chosen[code];
        }

        private int Choose(int code)
        {
            // .notdef is a name for no glyph, and is not a reason to look the code up some other way.
            if (_differences[code] == ".notdef")
                return 0;

            var glyph = _symbolic ? BySymbol(code) : ByName(code);
            if (glyph == 0)
                glyph = _symbolic ? ByName(code) : BySymbol(code);

            // A subset with no cmap at all is one whose producer numbered its glyphs by the codes.
            if (glyph == 0 && _hasNoCharacterMap && code < _glyphs.GlyphCount)
                glyph = code;
            return glyph;
        }

        /// <summary>The code's glyph name, from <c>/Differences</c> or the base encoding, to a character, to a glyph.</summary>
        private int ByName(int code)
        {
            var name = _differences[code];
            var character = name != null ? GlyphNames.CharacterOf(name) : GlyphNames.CharacterOf(_baseEncoding, code);

            var glyph = character > 0 && _unicode != null ? _unicode.GlyphOf(character) : 0;
            if (glyph == 0 && name != null)
                glyph = Math.Max(0, _glyphs.GlyphNamed(name));
            if (glyph == 0 && name == null)
            {
                foreach (var alias in GlyphNames.NamesOf(character))
                {
                    glyph = Math.Max(0, _glyphs.GlyphNamed(alias));
                    if (glyph != 0)
                        break;
                }
            }
            if (glyph == 0 && character > 0 && _macintosh != null)
            {
                var macCode = GlyphNames.MacRomanCodeOf(character);
                if (macCode >= 0)
                    glyph = _macintosh.GlyphOf(macCode);
            }

            return glyph;
        }

        /// <summary>
        /// The code itself, looked up in the (3,0) subtable where a symbol font's codes lie at
        /// 0xF000 and above as often as not, then in the (1,0) one, then as a character.
        /// </summary>
        private int BySymbol(int code)
        {
            if (_symbol != null)
            {
                foreach (var high in SymbolPages)
                {
                    var glyph = _symbol.GlyphOf(high | code);
                    if (glyph != 0)
                        return glyph;
                }
            }

            var mapped = _macintosh?.GlyphOf(code) ?? 0;
            if (mapped == 0 && _unicode != null)
                mapped = _unicode.GlyphOf(code);
            return mapped;
        }

        private void ReadDifferences(PdfArray differences)
        {
            if (differences == null)
                return;

            var code = -1;
            foreach (var item in differences.Elements)
            {
                var value = PdfReference.Dereference(item);
                if (PdfItemValues.TryGetNumber(value, out var number))
                {
                    code = number is >= 0 and <= 255 ? (int)number : -1;
                }
                else if (PdfItemValues.TryGetName(value, out var glyphName) && code is >= 0 and <= 255)
                {
                    _differences[code++] = glyphName.Substring(1);
                }
            }
        }

        private static readonly int[] SymbolPages = [0x0000, 0xF000, 0xF100, 0xF200];

        private const int SymbolicFlag = 1 << 2;
        private const int NonsymbolicFlag = 1 << 5;
    }
}
