using System;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using Xunit;

namespace PdfPinata.Test.Dom;

/// <summary>
///   <see cref="Color"/> as a value: the two ways it can be spelled, what it will and will not let
///   be changed afterwards, and the equality that has to agree with <see cref="Color.GetHashCode"/>
///   across both spellings.
///   <para>
///   A colour is either RGB or CMYK and says which, and the two never compare equal even when they
///   name the same ink — the same four CMYK numbers are a different colour on every press, and the
///   library refuses to pretend otherwise.
///   </para>
/// </summary>
public class ColorValueTests
{
    // ----- the two spellings --------------------------------------------------------------------

    [Fact]
    public void AnRgbColourKnowsItsPartsAndIsNotCmyk()
    {
        var color = new Color(0x80, 0x10, 0x20, 0x30);

        color.IsCmyk.Should().BeFalse();
        color.A.Should().Be(0x80u);
        color.R.Should().Be(0x10u);
        color.G.Should().Be(0x20u);
        color.B.Should().Be(0x30u);
        color.Argb.Should().Be(0x80102030u);
        color.RGB.Should().Be(0x80102030u);
    }

    [Fact]
    public void ACmykColourClampsEveryPartIntoItsRange()
    {
        var color = new Color(200, -5, 50, 120, -1);

        color.IsCmyk.Should().BeTrue();
        color.Alpha.Should().Be(100);
        color.C.Should().Be(0);
        color.M.Should().Be(50);
        color.Y.Should().Be(100);
        color.K.Should().Be(0);
    }

    [Fact]
    public void ACmykColourWithNoAlphaGivenIsOpaque()
    {
        Color.FromCmyk(10, 20, 30, 40).Alpha.Should().Be(100);
        Color.FromCmyk(50, 10, 20, 30, 40).Alpha.Should().Be(50);
    }

    [Fact]
    public void AColourCanBeBuiltFromAnotherWithANewTransparency()
    {
        var opaque = new Color(0x10, 0x20, 0x30);

        var translucent = Color.FromRgbColor(0x40, opaque);

        translucent.A.Should().Be(0x40u);
        translucent.R.Should().Be(0x10u);
        translucent.G.Should().Be(0x20u);
        translucent.B.Should().Be(0x30u);

        var cmyk = Color.FromCmyk(10, 20, 30, 40);
        var fainter = Color.FromCmykColor(25, cmyk);

        fainter.IsCmyk.Should().BeTrue();
        fainter.Alpha.Should().Be(25);
        fainter.C.Should().Be(cmyk.C);
        fainter.K.Should().Be(cmyk.K);
    }

    // ----- what may be changed afterwards -------------------------------------------------------

    [Fact]
    public void AnRgbColourCanBeGivenANewValueEitherWayRound()
    {
        var byArgb = new Color(0x010203) { Argb = 0xFF445566 };
        var byRgb = new Color(0x010203) { RGB = 0xFF445566 };

        byArgb.R.Should().Be(0x44u);
        byRgb.R.Should().Be(0x44u);
        byArgb.Should().Be(byRgb);
    }

    [Fact]
    public void ACmykColourRefusesToBeGivenAnRgbValue()
    {
        var cmyk = Color.FromCmyk(10, 20, 30, 40);

        var settingArgb = () =>
        {
            var copy = cmyk;
            copy.Argb = 0xFF000000;
        };
        settingArgb.Should().Throw<InvalidOperationException>();

        var settingRgb = () =>
        {
            var copy = cmyk;
            copy.RGB = 0xFF000000;
        };
        settingRgb.Should().Throw<InvalidOperationException>();
    }

    // ----- equality -----------------------------------------------------------------------------

    [Fact]
    public void TwoColoursOfDifferentKindsAreNeverEqualEvenWhenTheyLookTheSame()
    {
        var cmyk = Color.FromCmyk(0, 0, 0, 100);
        var rgb = new Color(cmyk.Argb);

        (cmyk == rgb).Should().BeFalse();
        (cmyk != rgb).Should().BeTrue();
        cmyk.Equals(rgb).Should().BeFalse();
        cmyk.Equals((object)rgb).Should().BeFalse();
    }

    [Fact]
    public void TwoCmykColoursAreEqualWhenEveryPartAgrees()
    {
        var one = Color.FromCmyk(50, 10, 20, 30, 40);
        var same = Color.FromCmyk(50, 10, 20, 30, 40);
        var other = Color.FromCmyk(50, 10, 20, 30, 41);

        one.Equals(same).Should().BeTrue();
        one.Equals((object)same).Should().BeTrue();
        (one == same).Should().BeTrue();
        (one == other).Should().BeFalse();
        one.GetHashCode().Should().Be(same.GetHashCode());
    }

    [Fact]
    public void TwoRgbColoursAreEqualWhenTheirArgbAgrees()
    {
        var one = new Color(0xFF102030);
        var same = new Color(0xFF102030);

        one.Equals(same).Should().BeTrue();
        one.GetHashCode().Should().Be(same.GetHashCode());
    }

    [Fact]
    public void AColourIsNotEqualToSomethingThatIsNotAColour()
    {
        Colors.Black.Equals("black").Should().BeFalse();
        Colors.Black.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void TheEmptyColourIsTheOnlyEmptyOne()
    {
        Color.Empty.IsEmpty.Should().BeTrue();
        Colors.Black.IsEmpty.Should().BeFalse();
    }

    // ----- parsing ------------------------------------------------------------------------------

    [Fact]
    public void AColourIsParsedFromItsNameCaseInsensitively()
    {
        Color.Parse("Red").Should().Be(Colors.Red);
        Color.Parse("red").Should().Be(Colors.Red);
    }

    [Fact]
    public void AColourIsParsedFromAHexadecimalOrADecimalNumber()
    {
        Color.Parse("0xFF102030").Argb.Should().Be(0xFF102030u);
        Color.Parse("0Xff102030").Argb.Should().Be(0xFF102030u);
        Color.Parse("255").Argb.Should().Be(255u);
    }

    [Fact]
    public void AColourThatIsNeitherANameNorANumberIsRefused()
    {
        var parsing = () => Color.Parse("chartreusish");

        parsing.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AColourMustNotBeNullOrEmptyToBeParsed()
    {
        ((Action)(() => Color.Parse(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => Color.Parse(""))).Should().Throw<ArgumentException>();
    }

    // ----- the value model ----------------------------------------------------------------------

    [Fact]
    public void AColourSetOnADomObjectCanBeEmptiedAgain()
    {
        var font = new Document().AddSection().AddParagraph("x").Format.Font;
        font.Color = Colors.Red;

        font.IsNull("Color").Should().BeFalse();

        font.SetNull("Color");

        font.IsNull("Color").Should().BeTrue();
        font.Color.Should().Be(Color.Empty);
    }

    // ----- transparency mixed down ---------------------------------------------------------------

    [Fact]
    public void AnOpaqueColourIsAlreadyItsOwnMixedTransparencyColour()
    {
        var opaque = new Color(0xFF, 0x10, 0x20, 0x30);

        opaque.GetMixedTransparencyColor().Should().Be(opaque);
    }

    [Fact]
    public void ATranslucentColourIsMixedTowardsWhiteAndComesBackOpaque()
    {
        var half = new Color(0x80, 0x00, 0x00, 0x00);

        var mixed = half.GetMixedTransparencyColor();

        mixed.A.Should().Be(0xFFu);
        mixed.R.Should().BeGreaterThan(0u).And.BeLessThan(0xFFu);
        mixed.R.Should().Be(mixed.G).And.Be(mixed.B);
    }

    [Fact]
    public void AFullyTransparentColourMixesDownToWhite()
    {
        var invisible = new Color(0x00, 0x00, 0x00, 0x00);

        invisible.GetMixedTransparencyColor().Argb.Should().Be(0xFFFFFFFFu);
    }

    // ----- printing -----------------------------------------------------------------------------

    [Fact]
    public void ACmykColourPrintsItsAlphaOnlyWhenItHasOne()
    {
        Color.FromCmyk(10, 20, 30, 40).ToString().Should().Be("CMYK(10,20,30,40)");
        Color.FromCmyk(50, 10, 20, 30, 40).ToString().Should().Be("CMYK(50,10,20,30,40)");
    }
}
