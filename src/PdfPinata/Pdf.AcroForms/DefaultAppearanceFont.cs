using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.AcroForms;

/// <summary>
/// The family and style a field's <c>/DA</c> asks its text to be drawn in: the key the string
/// sets with <c>Tf</c>, looked up in the form's <c>/DR /Font</c>, and the <c>/BaseFont</c> found
/// there turned into something a font resolver is asked for. Issue #353.
/// </summary>
/// <remarks>
/// <para>
/// Only the name is used. The font in <c>/DR</c> is very often a subset, which holds the glyphs of
/// the text it was made for and cannot draw a new value, so it says which family the form was
/// designed in and nothing more. A form made in a designer says <c>/ABCDEF+ArialMT</c> or
/// <c>/Arial,Bold</c>, and a value filled in later is drawn in Arial, bold.
/// </para>
/// <para>
/// A name is cut up in the order PDF producers build it: a six-letter subset tag
/// (<c>ABCDEF+</c>, ISO 32000-1 9.6.4); then a style after a comma (<c>,Bold</c>), as a
/// Windows font is named, or after the last hyphen (<c>-BoldMT</c>), as a PostScript font is - but
/// only where what follows the hyphen is a style, so <c>Arial-Black</c> stays a family; then the
/// <c>MT</c>, <c>PSMT</c> or <c>PS</c> a family is sometimes left with. What is left is the family
/// exactly as written: <c>TimesNewRomanPSMT</c> asks for <c>TimesNewRoman</c>, not for a family
/// with spaces in it, because a name read from a file is a guess already and a second guess at
/// where its words split is not one the form made.
/// </para>
/// <para>
/// The fourteen standard faces, and the abbreviations a form uses for them, ask for the families a
/// platform installs in their place: Arial for Helvetica, Times New Roman for Times and Courier New
/// for Courier, which have their metrics, and then the standard name itself. The substitute comes
/// first because it is the one a machine is likely to have, and a resolver that answers an unknown
/// family with some face of its own - <c>FontResolverBase</c> does, unless it is told to answer null
/// - would otherwise be asked for Helvetica, which Windows does not install, and answer a face the
/// form never meant; before this a <c>/Helv</c> field was drawn in the default font, Arial. Symbol
/// and ZapfDingbats ask for nothing, since no family a resolver has stands in for them, and are
/// drawn in the resolver's default font exactly as they were.
/// </para>
/// </remarks>
internal sealed class DefaultAppearanceFont
{
    private DefaultAppearanceFont(IReadOnlyList<string> families, XFontStyle style)
    {
        Families = families;
        Style = style;
    }

    /// <summary>
    /// The families to ask the resolver for, the first it answers for being the one used.
    /// </summary>
    public IReadOnlyList<string> Families { get; }

    public XFontStyle Style { get; }

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    // The name a Tf operator sets. /DA is read from files, so the number is matched one way only,
    // as the font size is in PdfAcroField, and a name is whatever runs to a delimiter.
    private static readonly Regex FontKey = new(
        $@"/(?<key>[^\s/\[\]()<>{{}}%]+)\s+{PdfAcroField.NumberStart}{PdfAcroField.Number}\s+Tf\b",
        RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex SubsetTag = new(
        @"^[A-Z]{6}\+", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex NameEscape = new(
        "#([0-9A-Fa-f]{2})", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex Suffix = new(
        "(?:PSMT|MT|PS)$", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly string[] Helvetica = ["Arial", "Helvetica"];
    private static readonly string[] Times = ["Times New Roman", "Times"];
    private static readonly string[] Courier = ["Courier New", "Courier"];

    // What a standard face, or the PostScript spelling of a family with spaces in it that a form
    // is likely to name, is asked for as. Keys are lower case. An empty list is a face that names
    // no family at all.
    private static readonly Dictionary<string, (string[] Families, XFontStyle Style)> Standard =
        new(StringComparer.Ordinal)
        {
            ["helv"] = (Helvetica, XFontStyle.Regular),
            ["hebo"] = (Helvetica, XFontStyle.Bold),
            ["heob"] = (Helvetica, XFontStyle.Italic),
            ["hebi"] = (Helvetica, XFontStyle.BoldItalic),
            ["helvetica"] = (Helvetica, XFontStyle.Regular),
            ["tiro"] = (Times, XFontStyle.Regular),
            ["tibo"] = (Times, XFontStyle.Bold),
            ["tiit"] = (Times, XFontStyle.Italic),
            ["tibi"] = (Times, XFontStyle.BoldItalic),
            ["times"] = (Times, XFontStyle.Regular),
            ["timesnewroman"] = (Times, XFontStyle.Regular),
            ["cour"] = (Courier, XFontStyle.Regular),
            ["cobo"] = (Courier, XFontStyle.Bold),
            ["coob"] = (Courier, XFontStyle.Italic),
            ["cobi"] = (Courier, XFontStyle.BoldItalic),
            ["courier"] = (Courier, XFontStyle.Regular),
            ["couriernew"] = (Courier, XFontStyle.Regular),
            ["zadb"] = ([], XFontStyle.Regular),
            ["zapfdingbats"] = ([], XFontStyle.Regular),
            ["symbol"] = ([], XFontStyle.Regular),
        };

    /// <summary>
    /// What <paramref name="defaultAppearance"/> asks for, found through <paramref name="form"/>'s
    /// <c>/DR</c>, or null when it names no font, the form has no such font, or the font's name
    /// asks for no family.
    /// </summary>
    /// <remarks>
    /// Reads only. <see cref="PdfAcroForm.DefaultResources"/> makes <c>/DR</c> if the form has none,
    /// and reading a field must not change the file.
    /// </remarks>
    public static DefaultAppearanceFont Read(string defaultAppearance, PdfAcroForm form)
    {
        if (string.IsNullOrEmpty(defaultAppearance) || form == null)
            return null;

        var key = LastFontKey(defaultAppearance);
        if (key == null)
            return null;

        // A name in a content stream is written with #hh escapes, which the lexer has already
        // resolved in the dictionary's keys.
        key = NameEscape.Replace(key, escape => ((char)Convert.ToInt32(escape.Groups[1].Value, 16)).ToString());

        var font = form.Elements.GetDictionary(PdfAcroForm.Keys.DR)?
            .Elements.GetDictionary("/Font")?
            .Elements.GetDictionary("/" + key);
        if (font == null)
            return null;

        // A composite font's own name is the descendant's with the encoding on the end.
        // An array with nothing in it is a malformed file as well, and GetDictionary(0) throws on one.
        var descendants = font.Elements.GetArray("/DescendantFonts");
        var named = descendants is { Elements.Count: > 0 } ? descendants.Elements.GetDictionary(0) ?? font : font;

        // A /BaseFont that is not a name is a malformed file, and a drawing does not fail for one.
        return PdfItemValues.TryGetName(named.Elements["/BaseFont"], out var baseFont)
            ? FromBaseFont(PdfName.WithoutSolidus(baseFont))
            : null;
    }

    /// <summary>
    /// What a <c>/BaseFont</c> asks for, or null when it names nothing a resolver could be asked
    /// for.
    /// </summary>
    private static DefaultAppearanceFont FromBaseFont(string baseFont)
    {
        var name = SubsetTag.Replace(baseFont.Trim(), "");
        var style = XFontStyle.Regular;

        var comma = name.IndexOf(',');
        if (comma >= 0)
        {
            // "Arial,Bold": whatever follows is the style, and is read for the words in it, since
            // a Windows name is written how its author liked: "Bold Italic", "BoldItalicMT", "Semibold".
            var words = name[(comma + 1)..].ToLowerInvariant();
            style = (words.Contains("bold") ? XFontStyle.Bold : XFontStyle.Regular)
                | (words.Contains("italic") || words.Contains("oblique") ? XFontStyle.Italic : XFontStyle.Regular);
            name = name[..comma];
        }
        else
        {
            var hyphen = name.LastIndexOf('-');
            if (hyphen > 0 && StyleOf(name[(hyphen + 1)..]) is { } fromHyphen)
            {
                style = fromHyphen;
                name = name[..hyphen];
            }
        }

        // Only a PostScript name has these suffixes to lose: a name with a space in it is a family
        // as the platform spells it, and "Gill Sans MT" is the whole of one.
        name = name.Trim();
        var family = name.Contains(' ') ? name : Suffix.Replace(name, "");
        if (family.Length == 0)
            family = name;
        if (family.Length == 0)
            return null;

        if (Standard.TryGetValue(family.ToLowerInvariant(), out var standard))
        {
            return standard.Families.Length == 0
                ? null
                : new DefaultAppearanceFont(standard.Families, style | standard.Style);
        }

        return new DefaultAppearanceFont([family], style);
    }

    /// <summary>
    /// The style a suffix names - <c>Bold</c>, <c>BoldMT</c>, <c>Oblique</c>, <c>Roman</c> - or
    /// null when it is not one, which is how <c>Arial-Black</c> is told from <c>Arial-Bold</c>.
    /// </summary>
    private static XFontStyle? StyleOf(string text)
    {
        var word = Suffix.Replace(text.Trim(), "").ToLowerInvariant();

        return word switch
        {
            "" or "regular" or "roman" or "normal" or "book" => XFontStyle.Regular,
            "bold" => XFontStyle.Bold,
            "italic" or "oblique" => XFontStyle.Italic,
            "bolditalic" or "boldoblique" or "italicbold" => XFontStyle.BoldItalic,
            _ => null
        };
    }

    /// <summary>
    /// The font a <c>/DA</c> string sets last, which is the one in force when the text is shown.
    /// </summary>
    private static string LastFontKey(string defaultAppearance)
    {
        string key = null;
        foreach (Match match in FontKey.Matches(defaultAppearance))
            key = match.Groups["key"].Value;
        return key;
    }
}
