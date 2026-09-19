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
        // model carries, taken through its byte value.
        var document = new Document();
        document.AddSection().AddParagraph().AddCharacter('A');

        Glyphs.On(Rendered.FirstPageOf(document)).Should().Equal(GlyphsFor("A"));
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
