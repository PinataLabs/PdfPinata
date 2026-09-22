using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.Rendering.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   Two paths through <c>ParagraphRenderer</c> that nothing reached: the named symbols, which
///   are the one kind of element whose text is decided by the renderer rather than carried in the
///   model, and the decimal-aligned tab, which is the only tab whose position depends on what is
///   written after it as well as before.
/// </summary>
public class SymbolAndDecimalTabTests
{
    static Document ADocumentShowing(params object[] pieces)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        foreach (var piece in pieces)
        {
            if (piece is string text)
                paragraph.AddText(text);
            else
                paragraph.AddCharacter((SymbolName)piece);
        }
        return document;
    }

    /// <summary>The glyphs a paragraph showing the given text draws, for comparison.</summary>
    static System.Collections.Generic.IReadOnlyList<int> GlyphsFor(string text) =>
        Glyphs.On(Rendered.FirstPageOf(ADocumentShowing(text)));

    // ----- GetSymbol -------------------------------------------------------------------------

    /// <summary>
    ///   Each named symbol draws the character it stands for. The renderer holds the mapping, so
    ///   the only way to see it is to compare against a paragraph carrying that character as
    ///   text - which is what <see cref="Glyphs"/> exists for, the fonts being Identity-H.
    /// </summary>
    [Theory]
    [InlineData(SymbolName.Euro, "€")]
    [InlineData(SymbolName.Copyright, "©")]
    [InlineData(SymbolName.Trademark, "™")]
    [InlineData(SymbolName.RegisteredTrademark, "®")]
    [InlineData(SymbolName.Bullet, "•")]
    [InlineData(SymbolName.Not, "¬")]
    [InlineData(SymbolName.EmDash, "—")]
    [InlineData(SymbolName.EnDash, "–")]
    public void EveryNamedSymbolDrawsTheCharacterItStandsFor(SymbolName symbol, string expected)
    {
        var page = Rendered.FirstPageOf(ADocumentShowing(symbol));

        Glyphs.On(page).Should().Equal(GlyphsFor(expected));
    }

    [Fact]
    public void TheSymbolsAreAllDifferentFromOneAnother()
    {
        // A mapping that answered the same character for two of them would satisfy every case
        // above that happened to be checked against the right one, and this catches that.
        var drawn = new[]
        {
            SymbolName.Euro, SymbolName.Copyright, SymbolName.Trademark,
            SymbolName.RegisteredTrademark, SymbolName.Bullet, SymbolName.Not,
            SymbolName.EmDash, SymbolName.EnDash
        }.Select(symbol => string.Join(",", Glyphs.On(Rendered.FirstPageOf(ADocumentShowing(symbol)))));

        drawn.Distinct().Should().HaveCount(8);
    }

    [Fact]
    public void ASymbolSitsBetweenTheTextEitherSideOfIt()
    {
        var page = Rendered.FirstPageOf(ADocumentShowing("a", SymbolName.Euro, "b"));

        Glyphs.On(page).Should().Equal(GlyphsFor("a€b"));
    }

    [Fact]
    public void ACharacterGivenByNumberDrawsThatCharacter()
    {
        // The default arm: anything that is not one of the named symbols is the character the
        // model carries.
        var document = new Document();
        document.AddSection().AddParagraph().AddCharacter('A');

        Glyphs.On(Rendered.FirstPageOf(document)).Should().Equal(GlyphsFor("A"));
    }

    /// <summary>
    ///   A character beyond ASCII draws itself too. The default arm used to take the character's
    ///   low byte and decode that one byte as UTF-8, which is only the character it was below 128:
    ///   'é' is 0xE9, a lead byte with nothing after it, and came out as U+FFFD, and anything above
    ///   U+00FF lost its high byte before that.
    /// </summary>
    [Theory]
    [InlineData('é')]
    [InlineData('ß')]
    [InlineData('Ω')]
    [InlineData('€')]
    public void ACharacterBeyondAsciiGivenByNumberDrawsThatCharacter(char ch)
    {
        var document = new Document();
        document.AddSection().AddParagraph().AddCharacter(ch);

        Glyphs.On(Rendered.FirstPageOf(document)).Should().Equal(GlyphsFor(ch.ToString()));
    }

    [Fact]
    public void ACharacterAboveTheBasicMultilingualPlaneIsDrawnWhole()
    {
        // Reachable only through SymbolName, which keeps the whole code where Char keeps 16 bits.
        // The face has no glyph for it, so what is compared is that one character is drawn rather
        // than nothing: the low 16 bits of U+1F600 used to end in a zero byte, which was dropped.
        var document = new Document();
        document.AddSection().AddParagraph().AddCharacter((SymbolName)0x1F600);

        Glyphs.On(Rendered.FirstPageOf(document)).Should().Equal(GlyphsFor("\U0001F600"));
    }

    /// <summary>
    ///   A repeated symbol is drawn once per repeat, and no more. It used to be drawn Count
    ///   squared times - four bullets for a count of two, nine for three - because GetSymbol
    ///   already answers the character as many times as it repeats and the renderer repeated that
    ///   again. The formatter measured Count of them, so the extras were drawn into a width that
    ///   had not been reserved for them. See the backlog spec's finding F17.
    /// </summary>
    [Theory]
    [InlineData(1, "•")]
    [InlineData(2, "••")]
    [InlineData(3, "•••")]
    [InlineData(5, "•••••")]
    public void ARepeatedSymbolIsDrawnAsManyTimesAsItSaysItIs(int count, string expected)
    {
        var document = new Document();
        document.AddSection().AddParagraph().AddCharacter(SymbolName.Bullet, count);

        Glyphs.On(Rendered.FirstPageOf(document)).Should().Equal(GlyphsFor(expected));
    }

    [Fact]
    public void ARepeatedSymbolTakesTheWidthTheFormatterReservedForIt()
    {
        // The consequence of drawing more than were measured: the text after the symbols has to
        // begin where the symbols actually end.
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddCharacter(SymbolName.Bullet, 3);
        paragraph.AddText("after");

        var runs = TextBaselines.PositionsOf(Rendered.FirstPageOf(document));
        var reference = new Document();
        var referenceParagraph = reference.AddSection().AddParagraph();
        referenceParagraph.AddText("•••");
        referenceParagraph.AddText("after");
        var expected = TextBaselines.PositionsOf(Rendered.FirstPageOf(reference));

        runs[^1].X.Should().BeApproximately(expected[^1].X, 0.01);
    }

    // ----- the non-breakable blank ------------------------------------------------------------

    [Fact]
    public void ANonBreakableBlankDrawsTheNoBreakSpace()
    {
        // It used to have no character at all: the symbol carries no code, so GetSymbol fell to
        // its default arm and answered U+0000, which text normalization drops before a glyph is
        // looked up - so the blank drew nothing and took no room.
        var page = Rendered.FirstPageOf(ADocumentShowing("a", SymbolName.NonBreakableBlank, "b"));

        Glyphs.On(page).Should().Equal(GlyphsFor("a b"));
    }

    [Fact]
    public void ANonBreakableBlankTakesTheRoomOfASpace()
    {
        var withTheSymbol = TextBaselines.PositionsOf(
            Rendered.FirstPageOf(ADocumentShowing("a", SymbolName.NonBreakableBlank, "b")));
        var withABlank = TextBaselines.PositionsOf(Rendered.FirstPageOf(ADocumentShowing("a b")));

        withTheSymbol[^1].X.Should().BeApproximately(withABlank[^1].X, 0.01);
    }

    /// <summary>
    ///   A paragraph sixty points wide, which holds "aaaa bbbb" but not "aaaa bbbb cccc", with the
    ///   last two words joined by <paramref name="join"/>.
    /// </summary>
    static PdfPinata.Pdf.PdfPage ThreeWordsJoinedBy(object join)
    {
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.LeftMargin = Unit.FromPoint(10);
        section.PageSetup.RightMargin = Unit.FromPoint(10);
        section.PageSetup.PageWidth = Unit.FromPoint(80);
        var paragraph = section.AddParagraph();
        paragraph.AddText("aaaa bbbb");
        if (join is string text)
            paragraph.AddText(text);
        else
            paragraph.AddCharacter((SymbolName)join);
        paragraph.AddText("cccc");
        return Rendered.FirstPageOf(document);
    }

    [Fact]
    public void ALineIsNotBrokenAtANonBreakableBlank()
    {
        // The breaking blank first, to show the measure really does break between the last two
        // words: "aaaa bbbb" stays together and "cccc" goes down.
        var broken = Glyphs.PlacedOn(ThreeWordsJoinedBy(" "));
        broken[1].Y.Should().Be(broken[0].Y);
        broken[^1].Y.Should().BeLessThan(broken[0].Y);

        // Joined by a blank a line may not be broken at, "bbbb" has to go down with "cccc".
        var joined = Glyphs.PlacedOn(ThreeWordsJoinedBy(SymbolName.NonBreakableBlank));
        joined[1].Y.Should().BeLessThan(joined[0].Y, "\"bbbb\" goes down to the line \"cccc\" is on");
        joined[1].Y.Should().Be(joined[^1].Y);
    }

    [Fact]
    public void ARunTooLongForAnyLineIsStillBrokenAtItsNonBreakableBlank()
    {
        // The last resort, as for a word longer than the measure: a run of words joined by
        // non-breakable blanks that no line can hold is set from the start of a line and broken
        // where it has to be, rather than refusing to lay out.
        var document = new Document();
        var section = document.AddSection();
        section.PageSetup.LeftMargin = Unit.FromPoint(10);
        section.PageSetup.RightMargin = Unit.FromPoint(10);
        section.PageSetup.PageWidth = Unit.FromPoint(80);
        var paragraph = section.AddParagraph();
        paragraph.AddText("aaaa");
        paragraph.AddCharacter(SymbolName.NonBreakableBlank);
        paragraph.AddText("bbbb");
        paragraph.AddCharacter(SymbolName.NonBreakableBlank);
        paragraph.AddText("cccc");

        var placed = Glyphs.PlacedOn(Rendered.FirstPageOf(document));

        placed[0].Y.Should().BeGreaterThan(placed[^1].Y);
    }

    // ----- the decimal-aligned tab ------------------------------------------------------------

    /// <summary>
    ///   A paragraph with a decimal tab stop, tabbing to it and then writing the number given.
    ///   The renderer has to look ahead past the tab for the decimal separator, because where the
    ///   text starts depends on how much of it comes before the point.
    /// </summary>
    static Document ANumberOnADecimalTab(string number)
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(6), TabAlignment.Decimal);
        paragraph.AddText("label");
        paragraph.AddTab();
        paragraph.AddText(number);
        return document;
    }

    static double WhereTheNumberStarts(string number)
    {
        var runs = TextBaselines.PositionsOf(Rendered.FirstPageOf(ANumberOnADecimalTab(number)));
        return runs.Max(run => run.X);
    }

    [Fact]
    public void ANumberOnADecimalTabIsSetSoItsPointLandsOnTheStop()
    {
        // The whole point of a decimal tab: however many digits come before the separator, the
        // separator itself is in the same place. A number with more of them therefore starts
        // further left.
        WhereTheNumberStarts("1.5").Should().BeGreaterThan(WhereTheNumberStarts("1234.5"));
    }

    [Fact]
    public void TwoNumbersWithTheSameDigitsBeforeThePointStartTogether()
    {
        WhereTheNumberStarts("12.3").Should()
            .BeApproximately(WhereTheNumberStarts("45.6789"), 0.01,
                "what follows the point does not move the point");
    }

    [Fact]
    public void ANumberWithNoPointIsTreatedAsThoughItEndedInOne()
    {
        // There is nothing after the separator, so the whole of it sits before the stop - the
        // same place a number with the same digits and a point would start.
        WhereTheNumberStarts("123").Should()
            .BeApproximately(WhereTheNumberStarts("123.4"), 0.01);
    }

    [Fact]
    public void ADecimalTabWithNothingAfterItIsStillLaidOut()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.Format.TabStops.AddTabStop(Unit.FromCentimeter(6), TabAlignment.Decimal);
        paragraph.AddText("label");
        paragraph.AddTab();

        var render = () => Rendered.FirstPageOf(document);

        render.Should().NotThrow();
    }

    [Fact]
    public void ADecimalTabFollowedByWordsRatherThanANumberIsStillLaidOut()
    {
        var render = () => Rendered.FirstPageOf(ANumberOnADecimalTab("no digits here"));

        render.Should().NotThrow();
    }
}
