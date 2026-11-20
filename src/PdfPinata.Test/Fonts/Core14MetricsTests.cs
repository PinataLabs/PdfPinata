using AwesomeAssertions;
using PdfPinata.Fonts;
using TUnit.Core;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   The standard 14 fonts' widths and encodings, generated from Adobe's Core14 AFM files. Each
///   expected number is read off the AFM file it names.
/// </summary>
public class Core14MetricsTests
{
    private static Core14Font Font(string name)
    {
        for (var i = 0; i < 14; i++)
        {
            if (Core14Metrics.NameOf((Core14Font)i).Replace("-", "") == name)
                return (Core14Font)i;
        }

        throw new System.ArgumentException(name);
    }

    [Test]
    [Arguments("Helvetica", "A", 667)]
    [Arguments("Helvetica", "space", 278)]
    [Arguments("HelveticaBold", "A", 722)]
    [Arguments("HelveticaOblique", "A", 667)]
    [Arguments("TimesRoman", "space", 250)]
    [Arguments("TimesRoman", "A", 722)]
    [Arguments("TimesItalic", "A", 611)]
    [Arguments("Symbol", "space", 250)]
    [Arguments("Symbol", "universal", 713)]
    [Arguments("Symbol", "alpha", 631)]
    [Arguments("ZapfDingbats", "a1", 974)]
    [Arguments("ZapfDingbats", "a2", 961)]
    public void AGlyphHasTheWidthItsAfmGives(string fontName, string glyph, int expected)
    {
        var font = Font(fontName);
        Core14Metrics.TryGetWidth(font, glyph, out var width).Should().BeTrue();
        width.Should().Be(expected);
    }

    [Test]
    [Arguments("Courier")]
    [Arguments("CourierBold")]
    [Arguments("CourierOblique")]
    [Arguments("CourierBoldOblique")]
    public void EveryCourierGlyphIsSixHundred(string fontName)
    {
        var font = Font(fontName);
        foreach (var glyph in new[] { "A", "i", "space", "W", "period" })
        {
            Core14Metrics.TryGetWidth(font, glyph, out var width).Should().BeTrue(glyph);
            width.Should().Be(600, glyph);
        }
    }

    [Test]
    public void AGlyphTheFontLacksHasNoWidth()
    {
        Core14Metrics.TryGetWidth(Core14Font.Symbol, "A", out _).Should().BeFalse();
        Core14Metrics.TryGetWidth(Core14Font.Helvetica, "alpha", out _).Should().BeFalse();
        Core14Metrics.TryGetWidth(Core14Font.Helvetica, "", out _).Should().BeFalse();
        Core14Metrics.TryGetWidth(Core14Font.Helvetica, "a1", out _).Should().BeFalse();
    }

    [Test]
    public void TheTextFontsCarryStandardEncoding()
    {
        Core14Metrics.GlyphNameOf(Core14Font.Helvetica, 'A').Should().Be("A");
        Core14Metrics.GlyphNameOf(Core14Font.TimesBold, 32).Should().Be("space");
        Core14Metrics.GlyphNameOf(Core14Font.Courier, 0x27).Should().Be("quoteright");
        Core14Metrics.GlyphNameOf(Core14Font.Courier, 0x60).Should().Be("quoteleft");
        Core14Metrics.GlyphNameOf(Core14Font.Helvetica, 0).Should().BeNull();
        Core14Metrics.GlyphNameOf(Core14Font.Helvetica, 256).Should().BeNull();
    }

    [Test]
    public void SymbolAndZapfDingbatsCarryTheirOwnEncodings()
    {
        Core14Metrics.GlyphNameOf(Core14Font.Symbol, 'a').Should().Be("alpha");
        Core14Metrics.GlyphNameOf(Core14Font.Symbol, 0x22).Should().Be("universal");
        Core14Metrics.GlyphNameOf(Core14Font.ZapfDingbats, 0x21).Should().Be("a1");
        Core14Metrics.IsSymbolic(Core14Font.Symbol).Should().BeTrue();
        Core14Metrics.IsSymbolic(Core14Font.Helvetica).Should().BeFalse();
    }

    [Test]
    public void EveryCodeOfEveryFontHasAWidth()
    {
        for (var f = 0; f < 14; f++)
        {
            var font = (Core14Font)f;
            var named = 0;
            for (var code = 0; code < 256; code++)
            {
                var glyph = Core14Metrics.GlyphNameOf(font, code);
                if (glyph == null)
                    continue;
                named++;
                Core14Metrics.TryGetWidth(font, glyph, out _).Should().BeTrue($"{Core14Metrics.NameOf(font)} code {code} is {glyph}");
            }

            named.Should().BeGreaterThan(140, Core14Metrics.NameOf(font));
        }

    }

    [Test]
    [Arguments("Helvetica", "Helvetica")]
    [Arguments("Helvetica-BoldOblique", "HelveticaBoldOblique")]
    [Arguments("Times-Roman", "TimesRoman")]
    [Arguments("ZapfDingbats", "ZapfDingbats")]
    [Arguments("Symbol", "Symbol")]
    [Arguments("Arial", "Helvetica")]
    [Arguments("ArialMT", "Helvetica")]
    [Arguments("Arial,Bold", "HelveticaBold")]
    [Arguments("Arial-BoldMT", "HelveticaBold")]
    [Arguments("Arial,Italic", "HelveticaOblique")]
    [Arguments("Arial-BoldItalicMT", "HelveticaBoldOblique")]
    [Arguments("TimesNewRoman", "TimesRoman")]
    [Arguments("TimesNewRoman,Bold", "TimesBold")]
    [Arguments("TimesNewRoman,Italic", "TimesItalic")]
    [Arguments("TimesNewRoman,BoldItalic", "TimesBoldItalic")]
    [Arguments("TimesNewRomanPSMT", "TimesRoman")]
    [Arguments("TimesNewRomanPS-BoldMT", "TimesBold")]
    [Arguments("Times New Roman", "TimesRoman")]
    [Arguments("CourierNew", "Courier")]
    [Arguments("CourierNew,Bold", "CourierBold")]
    [Arguments("CourierNewPS-BoldItalicMT", "CourierBoldOblique")]
    [Arguments("ABCDEF+Helvetica", "Helvetica")]
    [Arguments("helvetica-bold", "HelveticaBold")]
    [Arguments("Symbol,Bold", "Symbol")]
    [Arguments("Symbol,Italic", "Symbol")]
    [Arguments("Symbol,BoldItalic", "Symbol")]
    [Arguments("Symbol-Bold", "Symbol")]
    [Arguments("Symbol-Italic", "Symbol")]
    [Arguments("Symbol-BoldItalic", "Symbol")]
    [Arguments("SymbolMT", "Symbol")]
    [Arguments("ABCDEF+SymbolMT", "Symbol")]
    [Arguments("HelveticaLTStd-Roman", "Helvetica")]
    [Arguments("HelveticaLTStd-Bold", "HelveticaBold")]
    [Arguments("HelveticaLTStd-Obl", "HelveticaOblique")]
    [Arguments("HelveticaLTStd-BoldObl", "HelveticaBoldOblique")]
    public void AnAliasResolvesToOneOfThe14(string baseFont, string expected)
    {
        Core14Metrics.TryResolve(baseFont, out var font).Should().BeTrue();
        font.Should().Be(Font(expected));
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
    [Arguments("Arial-Black")]
    [Arguments("ArialNarrow")]
    [Arguments("Calibri")]
    [Arguments("ArialUnicodeMS")]
    [Arguments("ArialBlack")]
    [Arguments("HelveticaLTStd-Light")]
    [Arguments("SymbolSet")]
    [Arguments("Wingdings")]
    public void ANameThatIsNotOneOfThe14DoesNotResolve(string baseFont)
    {
        Core14Metrics.TryResolve(baseFont, out _).Should().BeFalse();
    }

    [Test]
    public void EachOfThe14NamesResolvesToItself()
    {
        for (var f = 0; f < 14; f++)
        {
            var font = (Core14Font)f;
            Core14Metrics.TryResolve(Core14Metrics.NameOf(font), out var resolved).Should().BeTrue();
            resolved.Should().Be(font);
        }

    }
}
