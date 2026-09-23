using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XStringFormat"/> now carries the PDF text state parameters - character spacing,
///   word spacing, horizontal scaling, text rise and an oblique angle - and
///   <see cref="XGraphics.MeasureString(string, XFont, XStringFormat)"/> answers through them.
///   <para>
///   That it answers through them at all is the point. The three-argument overload took a format
///   and then threw it away, passing <see cref="XStringFormats.Default"/> on to the font code,
///   which ignored its own format parameter in turn. Nothing noticed while the format held only
///   alignment, because where a string sits does not change how wide it is. Every property added
///   here does change how wide it is, and a width measured without them is what decides where a
///   line wraps.
///   </para>
///   <para>
///   These are measurements only. That the same settings reach the page is
///   <see cref="TextStateOperatorTests"/>, and that they do something once there is
///   <see cref="TextStateRenderingTests"/>.
///   </para>
/// </summary>
public class TextStateMeasurementTests
{
    /// <summary>
    ///   Liberation Sans, served by PinnedFontResolver, so the advance widths below are the same
    ///   on every machine.
    /// </summary>
    private static XFont Font => new XFont("Arial", 12);

    private static XGraphics NewGraphics()
    {
        var document = new PdfDocument();
        return XGraphics.FromPdfPage(document.AddPage());
    }

    private static XStringFormat Format() => XStringFormats.Default;

    private const double Tolerance = 1e-9;

    [Fact]
    public void ADefaultFormatMeasuresTheSameAsNoFormatAtAll()
    {
        var gfx = NewGraphics();

        var withFormat = gfx.MeasureString("Hello world", Font, Format()).Width;
        var withoutFormat = gfx.MeasureString("Hello world", Font).Width;

        withFormat.Should().BeApproximately(withoutFormat, Tolerance);
    }

    [Fact]
    public void CharacterSpacingWidensTheTextByOneSpacingForEveryGlyph()
    {
        var gfx = NewGraphics();
        const string text = "Hello";   // five glyphs

        var plain = gfx.MeasureString(text, Font, Format()).Width;

        var format = Format();
        format.CharacterSpacing = 2;
        var spaced = gfx.MeasureString(text, Font, format).Width;

        // PDF adds the character spacing after every glyph shown, the last one included, so five
        // glyphs buy five spacings rather than the four gaps between them.
        (spaced - plain).Should().BeApproximately(text.Length * 2, Tolerance);
    }

    [Fact]
    public void ANegativeCharacterSpacingTightensTheText()
    {
        var gfx = NewGraphics();

        var plain = gfx.MeasureString("Hello", Font, Format()).Width;

        var format = Format();
        format.CharacterSpacing = -0.5;
        var tightened = gfx.MeasureString("Hello", Font, format).Width;

        (plain - tightened).Should().BeApproximately(5 * 0.5, Tolerance);
    }

    [Fact]
    public void WordSpacingWidensTheTextOnlyWhereThereAreSpaces()
    {
        var gfx = NewGraphics();
        const string text = "a b c";   // two spaces

        var plain = gfx.MeasureString(text, Font, Format()).Width;

        var format = Format();
        format.WordSpacing = 3;
        var spaced = gfx.MeasureString(text, Font, format).Width;

        (spaced - plain).Should().BeApproximately(2 * 3, Tolerance);
    }

    [Fact]
    public void WordSpacingLeavesTextWithoutSpacesAlone()
    {
        var gfx = NewGraphics();

        var plain = gfx.MeasureString("abc", Font, Format()).Width;

        var format = Format();
        format.WordSpacing = 100;

        gfx.MeasureString("abc", Font, format).Width.Should().BeApproximately(plain, Tolerance);
    }

    [Fact]
    public void ATabIsMeasuredAsASpaceAndTakesTheWordSpacingWithIt()
    {
        var gfx = NewGraphics();

        var format = Format();
        format.WordSpacing = 4;

        // The font code maps a tab to a space before looking a glyph up. Whatever one thinks of
        // that, the word spacing has to follow the same rule or the two disagree.
        var tabbed = gfx.MeasureString("a\tb", Font, format).Width;
        var spaced = gfx.MeasureString("a b", Font, format).Width;

        tabbed.Should().BeApproximately(spaced, Tolerance);
    }

    [Fact]
    public void HorizontalScalingScalesTheGlyphsAndTheSpacingAlike()
    {
        var gfx = NewGraphics();

        var format = Format();
        format.CharacterSpacing = 2;
        format.WordSpacing = 3;
        var fullSize = gfx.MeasureString("a b c", Font, format).Width;

        format.HorizontalScaling = 50;
        var halfSize = gfx.MeasureString("a b c", Font, format).Width;

        // Tz multiplies the whole displacement, not just the glyph advances, so halving it halves
        // a width that already has both spacings in it.
        halfSize.Should().BeApproximately(fullSize / 2, Tolerance);
    }

    [Fact]
    public void HorizontalScalingLeavesTheHeightAlone()
    {
        var gfx = NewGraphics();

        var plain = gfx.MeasureString("Hello", Font, Format()).Height;

        var format = Format();
        format.HorizontalScaling = 250;

        gfx.MeasureString("Hello", Font, format).Height.Should().BeApproximately(plain, Tolerance);
    }

    [Fact]
    public void TextRiseAndAnObliqueAngleDoNotChangeHowWideTheTextIs()
    {
        var gfx = NewGraphics();

        var plain = gfx.MeasureString("Hello", Font, Format());

        var format = Format();
        format.TextRise = 6;
        format.ObliqueAngle = 20;
        var moved = gfx.MeasureString("Hello", Font, format);

        // Both move the glyphs without changing their advance. Ts raises the baseline and the
        // oblique angle skews the text matrix; neither touches the width.
        moved.Width.Should().BeApproximately(plain.Width, Tolerance);
        moved.Height.Should().BeApproximately(plain.Height, Tolerance);
    }

    [Fact]
    public void SpacingIsCountedPerLineAndTheWidestLineDecidesTheWidth()
    {
        var gfx = NewGraphics();

        var format = Format();
        format.CharacterSpacing = 5;

        var firstLine = gfx.MeasureString("ab", Font, format).Width;
        var secondLine = gfx.MeasureString("cdef", Font, format).Width;
        var together = gfx.MeasureString("ab\ncdef", Font, format).Width;

        // The spacing belongs to the line it is on. Summing it over the whole string and charging
        // it to the widest line would make this come out four spacings too wide.
        together.Should().BeApproximately(Math.Max(firstLine, secondLine), Tolerance);
    }

    [Fact]
    public void ALineFeedTakesNoCharacterSpacingOfItsOwn()
    {
        var gfx = NewGraphics();

        var format = Format();
        format.CharacterSpacing = 5;

        // "abc" on one line, and the same three glyphs split across two. The line feed draws no
        // glyph, so it must not be paid a spacing.
        var oneLine = gfx.MeasureString("abc", Font, format).Width;
        var split = gfx.MeasureString("abc\nabc", Font, format).Width;

        split.Should().BeApproximately(oneLine, Tolerance);
    }

    // ----- the characters measured are the characters drawn --------------------------------------
    //
    // The per-character half of the slow path - a tab becoming a space, every other control
    // character dropped - now lives in TextNormalization, which XGraphicsPdfRenderer.DrawString
    // calls too. These say the measuring side of it is unchanged by the move; that drawing now
    // agrees is TextStateOperatorTests.

    [Fact]
    public void ATabIsMeasuredAsASingleSpace()
    {
        var gfx = NewGraphics();

        var tabbed = gfx.MeasureString("Hand\tgloves", Font, Format()).Width;
        var spaced = gfx.MeasureString("Hand gloves", Font, Format()).Width;

        tabbed.Should().BeApproximately(spaced, Tolerance);
    }

    [Fact]
    public void ATabIsPaidTheWordSpacingASpaceIsPaid()
    {
        var gfx = NewGraphics();

        var format = Format();
        format.WordSpacing = 7;

        // It is a space by the time anything counts spaces, so it earns the word spacing too -
        // and the renderer breaks its run at the same place for the same reason.
        var tabbed = gfx.MeasureString("Hand\tgloves", Font, format).Width;
        var spaced = gfx.MeasureString("Hand gloves", Font, format).Width;

        tabbed.Should().BeApproximately(spaced, Tolerance);
    }

    [Theory]
    [InlineData('\r')]
    [InlineData('\v')]
    [InlineData('\f')]
    [InlineData((char)27)]
    public void AControlCharacterOtherThanATabAndALineFeedCostsNothingAtAll(char ch)
    {
        var gfx = NewGraphics();

        var format = Format();
        format.CharacterSpacing = 5;

        // Dropped rather than looked up, so it is not even paid a character spacing. \n is the
        // one exception, and it is a line break rather than a character - see the two tests above.
        var plain = gfx.MeasureString("abc", Font, format).Width;
        var carrying = gfx.MeasureString("ab" + ch + "c", Font, format).Width;

        carrying.Should().BeApproximately(plain, Tolerance);
    }

    [Fact]
    public void HorizontalScalingRejectsZeroAndNegativeValues()
    {
        var format = Format();

        // A run of zero or negative width has no measurement anything can lay out against.
        format.Invoking(f => f.HorizontalScaling = 0).Should().Throw<ArgumentOutOfRangeException>();
        format.Invoking(f => f.HorizontalScaling = -100).Should().Throw<ArgumentOutOfRangeException>();

        format.HorizontalScaling.Should().Be(100);
    }

    [Fact]
    public void ObliqueAngleRejectsAQuarterTurn()
    {
        var format = Format();

        // At 90 degrees the skew is infinite and the glyphs collapse onto a line.
        format.Invoking(f => f.ObliqueAngle = 90).Should().Throw<ArgumentOutOfRangeException>();
        format.Invoking(f => f.ObliqueAngle = -90).Should().Throw<ArgumentOutOfRangeException>();

        format.ObliqueAngle.Should().Be(0);
    }

    [Fact]
    public void EveryTextStateDefaultsToLeavingTheTextAsItIs()
    {
        var format = Format();

        format.CharacterSpacing.Should().Be(0);
        format.WordSpacing.Should().Be(0);
        format.HorizontalScaling.Should().Be(100);
        format.TextRise.Should().Be(0);
        format.ObliqueAngle.Should().Be(0);
    }

    [Fact]
    public void APresetCarriesItsOwnTextStateRatherThanSharingOne()
    {
        // Every XStringFormats preset builds a new instance, which is what makes it safe to hang
        // mutable text state off the type at all. If they were shared, setting a spacing on one
        // would set it on every string drawn anywhere.
        var first = XStringFormats.TopLeft;
        first.CharacterSpacing = 7;

        XStringFormats.TopLeft.CharacterSpacing.Should().Be(0);
    }
}
