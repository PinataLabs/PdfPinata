using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Fonts;
using PdfPinata.HarfBuzz;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Fonts;

/// <summary>
///   What a shaped run has to leave behind in the written document: the glyphs it chose have to be
///   embedded and given widths, and <c>/ToUnicode</c> has to say which characters each of them
///   stands for even when that is more than one.
/// </summary>
/// <remarks>
///   Both of those used to be derived from the characters of the string, one glyph per character,
///   which is exactly the assumption shaping breaks. A glyph the <c>cmap</c> would never have
///   produced - a ligature, a composed accent - was drawn without being embedded, without a width,
///   and with nothing saying what it meant.
///   <para>
///     The stub shapers here answer for one sentinel string each and decline every other run, so
///     that installing one cannot disturb whatever else is drawing beside it. See
///     <c>TextShapingSeamTests</c> for why that matters.
///   </para>
/// </remarks>
[Collection(TextShapingCollection.Name)]
public class ShapedFontEmbeddingTests
{
    // ----- installing a shaper for one string only -----------------------------------------------

    sealed class SelectiveShaper : ITextShaper
    {
        readonly string _mine;
        readonly Func<ShapingFont, IReadOnlyList<ShapedGlyph>> _glyphs;

        internal SelectiveShaper(string mine, Func<ShapingFont, IReadOnlyList<ShapedGlyph>> glyphs)
        {
            _mine = mine;
            _glyphs = glyphs;
        }

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
            => text.SequenceEqual(_mine.AsSpan())
                ? new ShapedRun(_glyphs(font), font.UnitsPerEm, direction)
                : null;
    }

    sealed class Installed : IDisposable
    {
        internal Installed(ITextShaper shaper) => GlobalFontSettings.TextShaper = shaper;

        public void Dispose() => GlobalFontSettings.TextShaper = null;
    }

    // ----- drawing and reading back --------------------------------------------------------------

    /// <summary>Draws one string, saves, and reopens - so that everything written at save time is.</summary>
    static PdfDocument Written(string text, string familyName = "Arial")
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString(text, new XFont(familyName, 20), XBrushes.Black, new XPoint(20, 40));

        var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.ReadOnly);
    }

    static string ContentOf(PdfDocument document) =>
        Encoding.ASCII.GetString(PageContent.Of(document.Pages[0]));

    static PdfItem Resolve(PdfItem item) => item is PdfReference reference ? reference.Value : item;

    static PdfDictionary CompositeFontOf(PdfDocument document)
    {
        var resources = Resolve(document.Pages[0].Elements["/Resources"]) as PdfDictionary;
        var fonts = Resolve(resources?.Elements["/Font"]) as PdfDictionary;

        return fonts.Elements.KeyNames
            .Select(key => Resolve(fonts.Elements[key.Value]) as PdfDictionary)
            .Single(font => font != null && font.Elements.GetName("/Subtype") == "/Type0");
    }

    /// <summary>The glyph identifiers the descendant font's /W array gives a width for.</summary>
    static IReadOnlyCollection<int> GlyphsGivenAWidth(PdfDocument document)
    {
        var font = CompositeFontOf(document);
        var descendants = Resolve(font.Elements["/DescendantFonts"]) as PdfArray;
        var descendant = Resolve(descendants.Elements[0]) as PdfDictionary;
        var widths = descendant.Elements["/W"].ToString();

        // "[300[1000]301[1000]]" - a glyph identifier, then its width in a bracket of its own.
        return Regex.Matches(widths, @"(\d+)\s*\[")
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToList();
    }

    /// <summary>
    ///   The characters each glyph stands for, read out of the font's /ToUnicode CMap. Reads both
    ///   the bfrange entries a one-to-one mapping is written as and the bfchar entries a glyph
    ///   standing for more than one character needs.
    /// </summary>
    static IReadOnlyDictionary<int, string> Meanings(PdfDocument document)
    {
        var meanings = new Dictionary<int, string>();
        var map = Resolve(CompositeFontOf(document).Elements["/ToUnicode"]) as PdfDictionary;
        var cmap = Encoding.UTF8.GetString(map.Stream.UnfilteredValue);

        // Only inside the blocks. The codespacerange above them is two codes side by side and
        // reads as an entry to anything scanning the whole stream, which is how this helper first
        // came to report a control character as the meaning of a glyph.
        foreach (Match block in Blocks(cmap, "bfrange"))
        {
            foreach (Match entry in Regex.Matches(block.Groups[1].Value,
                         @"<([0-9A-Fa-f]{4})><([0-9A-Fa-f]{4})><([0-9A-Fa-f]{4})>"))
            {
                var from = Convert.ToInt32(entry.Groups[1].Value, 16);
                var to = Convert.ToInt32(entry.Groups[2].Value, 16);
                var character = Convert.ToInt32(entry.Groups[3].Value, 16);

                for (var code = from; code <= to; code++)
                    meanings[code] = ((char)(character + code - from)).ToString();
            }
        }

        // A bfchar entry is a code and a UTF-16BE string of any length: "<012C><00660069>".
        foreach (Match block in Blocks(cmap, "bfchar"))
        {
            foreach (Match entry in Regex.Matches(block.Groups[1].Value,
                         @"<([0-9A-Fa-f]{4})>\s*<((?:[0-9A-Fa-f]{4})+)>"))
            {
                var code = Convert.ToInt32(entry.Groups[1].Value, 16);
                var destination = entry.Groups[2].Value;

                meanings[code] = string.Concat(Enumerable.Range(0, destination.Length / 4)
                    .Select(idx => (char)Convert.ToInt32(destination.Substring(idx * 4, 4), 16)));
            }
        }

        return meanings;
    }

    static MatchCollection Blocks(string cmap, string kind) =>
        Regex.Matches(cmap, $"begin{kind}(.*?)end{kind}", RegexOptions.Singleline);

    // ----- the glyphs a shaper chose have to be in the file ---------------------------------------

    const string WidthSentinel = "ShapedWidthProbe";

    [Fact]
    public void AGlyphOnlyTheShaperKnowsAboutIsStillGivenAWidth()
    {
        // Glyph 300 is a real glyph of the face and one the cmap would never return for any of
        // these characters - which is exactly the position a ligature is in.
        using var _ = new Installed(new SelectiveShaper(WidthSentinel,
            font => new[] { new ShapedGlyph(300, 0, 1000) }));

        GlyphsGivenAWidth(Written(WidthSentinel)).Should().Contain(300,
            "a glyph drawn without a width in /W falls back to the default width, and a glyph "
            + "missing from the subset is not there to draw at all");
    }

    [Fact]
    public void TheGlyphsAShaperDidNotChooseAreNotCarriedAlongForNothing()
    {
        using var _ = new Installed(new SelectiveShaper(WidthSentinel,
            font => new[] { new ShapedGlyph(300, 0, 1000) }));

        var widths = GlyphsGivenAWidth(Written(WidthSentinel));

        widths.Should().Equal(new[] { 300 },
            "the run is one glyph, so the subset is one glyph - the characters that were never "
            + "looked up in the cmap have no business being embedded");
    }

    // ----- and /ToUnicode has to say what they mean -----------------------------------------------

    const string MeaningSentinel = "ShapedProbeAB";

    [Fact]
    public void AGlyphThatStandsForSeveralCharactersSaysAllOfThemInToUnicode()
    {
        // Two glyphs for thirteen characters: the first cluster runs from 0 up to the second
        // cluster at 11, so glyph 300 means "ShapedProbe" and glyph 301 means "AB".
        using var _ = new Installed(new SelectiveShaper(MeaningSentinel, font => new[]
        {
            new ShapedGlyph(300, 0, 1000),
            new ShapedGlyph(301, 11, 1000)
        }));

        var meanings = Meanings(Written(MeaningSentinel));

        meanings.Should().ContainKey(300).WhoseValue.Should().Be("ShapedProbe");
        meanings.Should().ContainKey(301).WhoseValue.Should().Be("AB");
    }

    [Fact]
    public void AnUnshapedRunStillMapsEveryGlyphToItsOneCharacter()
    {
        // The regression guard: with no shaper registered nothing about the written font changes,
        // and copying text out of the document goes on working the way it always did.
        var meanings = Meanings(Written("Wave"));

        string.Concat("Wave".Select(character => meanings.Values.Contains(character.ToString())))
            .Should().NotContain("False");
        meanings.Values.Should().OnlyContain(meaning => meaning.Length == 1);
    }

    // ----- the same thing, with a real shaper -----------------------------------------------------

    sealed class OnlyFor : ITextShaper, IDisposable
    {
        readonly string _mine;
        readonly HarfBuzzTextShaper _shaper = new HarfBuzzTextShaper();

        internal OnlyFor(string mine) => _mine = mine;

        public ShapedRun Shape(ReadOnlySpan<char> text, ShapingFont font, XTextDirection direction,
            string script, string language)
            => text.SequenceEqual(_mine.AsSpan())
                ? _shaper.Shape(text, font, direction, script, language)
                : null;

        public void Dispose() => _shaper.Dispose();
    }

    // "e" and a combining acute accent, which the face composes into its precomposed e-acute.
    const string ComposedSentinel = "\u0065\u0301";

    [Fact]
    public void HarfBuzzComposesAnAccentAndTheDocumentSaysWhatTheGlyphMeant()
    {
        using var shaper = new OnlyFor(ComposedSentinel);
        using var _ = new Installed(shaper);

        var document = Written(ComposedSentinel);
        var meanings = Meanings(document);

        meanings.Should().HaveCount(1, "two characters came back as one glyph");
        meanings.Values.Single().Should().Be(ComposedSentinel,
            "and the one glyph has to say it stands for both of them, or text copied out of the "
            + "document loses the accent");
        GlyphsGivenAWidth(document).Should().HaveCount(1);
    }

    // ----- and the whole of it, in Arabic ---------------------------------------------------------

    // Served by PinnedFontResolver itself. See the note on ArabicFamilyName: a family registered on
    // first use means whatever the first caller in the assembly made it mean.
    const string ArabicFamily = PinnedFontResolver.ArabicFamilyName;

    // The word "arabi", whose letters join and two of whose dots are marks the font places against
    // the letter they belong to. Escapes rather than literals, so that a source file mixing
    // right-to-left text with left-to-right code cannot be misread.
    const string ArabicSentinel = "\u0639\u0631\u0628\u064A";

    [Fact]
    public void ArabicIsWrittenWithItsMarksPlacedAndItsLettersStillReadable()
    {

        using var shaper = new OnlyFor(ArabicSentinel);
        using var _ = new Installed(shaper);

        var document = Written(ArabicSentinel, ArabicFamily);
        var content = ContentOf(document);
        var meanings = Meanings(document);

        // Six glyphs for four characters: each letter, and a mark for the dots of two of them.
        GlyphsGivenAWidth(document).Should().HaveCount(6,
            "every glyph drawn has to be embedded and given a width, marks included");

        content.Should().Contain("TJ",
            "a mark is displaced sideways from the pen, which needs a TJ array");
        content.Should().Contain(" Ts",
            "and downwards, which no TJ array can express - only text rise can");
        content.Should().Contain("0 Ts",
            "put back afterwards, because it would otherwise carry into everything drawn after it");

        meanings.Values.Distinct().Should().BeEquivalentTo(
            ArabicSentinel.Select(character => character.ToString()),
            "a mark shares its letter's cluster, so the pair says the one letter it drew - and "
            + "the four characters of the word are all recoverable from the six glyphs");
    }

    // "salam", the word this library has drawn backwards for as long as anyone has complained
    // about it. None of its four letters carries a mark, so every glyph stands for exactly one
    // character and the order they are drawn in is readable straight off the document.
    const string Salam = "\u0633\u0644\u0627\u0645";

    [Fact]
    public void ShapedRightToLeftTextIsDrawnJoinedUpAndInTheOrderItIsRead()
    {

        using var shaper = new OnlyFor(Salam);
        using var _ = new Installed(shaper);

        var document = Written(Salam, ArabicFamily);
        var meanings = Meanings(document);

        // Every glyph on the page, in the order the renderer wrote it, read back as the character
        // the document itself says it stands for.
        var drawn = string.Concat(DrawnText.Glyphs(document.Pages[0])
            .Select(glyph => meanings[glyph]));

        drawn.Should().Be(new string(Salam.Reverse().ToArray()),
            "the glyphs are painted left to right along the page, so a right-to-left word is "
            + "painted last letter first - drawing them in the order they were written is the "
            + "whole of the oldest complaint against this library");

        // The letters joined as well as turned round, which is the other half and the half that
        // needs the shaper: four characters, four glyphs, and not the four the cmap would give.
        var unjoined = Salam.Select(letter => Written(letter.ToString(), ArabicFamily))
            .SelectMany(one => DrawnText.Glyphs(one.Pages[0]))
            .ToArray();

        DrawnText.Glyphs(document.Pages[0]).Should().NotBeEquivalentTo(unjoined,
            "a letter in the middle of a word is not the letter standing on its own");
    }
}
