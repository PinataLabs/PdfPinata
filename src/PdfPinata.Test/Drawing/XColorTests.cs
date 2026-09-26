using System;
using System.ComponentModel;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XColor"/> holds a colour in three representations at once - RGB, CMYK and grey -
///   and keeps all three in step, so that the same colour can be written into a PDF in whichever
///   space the caller asked for. Every setter therefore recalculates the other two, and the
///   conversions between them are lossy in one direction and not the other. What is pinned here
///   is which representation each way in trusts, and that the round trips that are meant to be
///   exact are.
///   <para>
///   <see cref="XColorTests"/> covers the arithmetic; <see cref="XKnownColorTests"/> covers the
///   table of named colours and the resource manager that reads it.
///   </para>
/// </summary>
public class XColorTests
{
    [Fact]
    public void APackedArgbValueUnpacksIntoItsFourBytes()
    {
        var color = XColor.FromArgb(unchecked((int)0x80FF8040));

        color.A.Should().BeApproximately(0x80 / 255.0, 1e-6);
        color.R.Should().Be(0xFF);
        color.G.Should().Be(0x80);
        color.B.Should().Be(0x40);
        color.ColorSpace.Should().Be(XColorSpace.Rgb);
    }

    [Fact]
    public void TheUnsignedOverloadPacksTheSameWayTheSignedOneDoes()
    {
        XColor.FromArgb(0x80FF8040u).Should().Be(XColor.FromArgb(unchecked((int)0x80FF8040)));
    }

    [Fact]
    public void ThreeComponentsMeanFullyOpaque()
    {
        var color = XColor.FromArgb(10, 20, 30);

        color.A.Should().Be(1);
        color.R.Should().Be(10);
        color.G.Should().Be(20);
        color.B.Should().Be(30);
    }

    [Fact]
    public void FourComponentsIncludeTheTransparency()
    {
        var color = XColor.FromArgb(0, 10, 20, 30);

        color.A.Should().Be(0);
        color.R.Should().Be(10);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(256, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, 256, 0)]
    [InlineData(0, 0, 0, -1)]
    public void AComponentOutsideAByteIsRefused(int alpha, int red, int green, int blue)
    {
        var act = () => XColor.FromArgb(alpha, red, green, blue);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AComponentOutsideAByteIsRefusedByTheThreeComponentOverloadToo()
    {
        var act = () => XColor.FromArgb(0, 0, 256);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GivingAnExistingColourAnAlphaLeavesEverythingElseAsItWas()
    {
        var opaque = XColor.FromArgb(10, 20, 30);

        var translucent = XColor.FromArgb(128, opaque);

        translucent.A.Should().BeApproximately(128 / 255.0, 1e-6);
        translucent.R.Should().Be(10);
        translucent.G.Should().Be(20);
        translucent.B.Should().Be(30);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 255, 255, 255)]
    [InlineData(0, 0, 0, 1, 0, 0, 0)]
    [InlineData(1, 0, 0, 0, 0, 255, 255)]
    [InlineData(0, 1, 0, 0, 255, 0, 255)]
    [InlineData(0, 0, 1, 0, 255, 255, 0)]
    public void ACmykColourAlsoKnowsWhatItIsInRgb(
        double cyan, double magenta, double yellow, double black, int red, int green, int blue)
    {
        var color = XColor.FromCmyk(cyan, magenta, yellow, black);

        color.ColorSpace.Should().Be(XColorSpace.Cmyk);
        color.A.Should().Be(1);
        color.R.Should().Be((byte)red);
        color.G.Should().Be((byte)green);
        color.B.Should().Be((byte)blue);
    }

    [Fact]
    public void ACmykComponentOutsideItsRangeIsClampedRatherThanRefused()
    {
        var color = XColor.FromCmyk(2, -1, 0.5, 2, -3);

        color.A.Should().Be(1);
        color.C.Should().Be(0);
        color.M.Should().BeApproximately(0.5, 1e-6);
        color.Y.Should().Be(1);
        color.K.Should().Be(0);
    }

    [Fact]
    public void ACmykColourCanBeTranslucentToo()
    {
        XColor.FromCmyk(0.5, 0, 0, 0, 0).A.Should().BeApproximately(0.5, 1e-6);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 127)]
    [InlineData(1, 255)]
    public void AGreyColourIsTheSameNumberOnAllThreeRgbChannels(double gray, int expected)
    {
        var color = XColor.FromGrayScale(gray);

        color.ColorSpace.Should().Be(XColorSpace.GrayScale);
        color.GS.Should().BeApproximately(gray, 1e-6);
        color.R.Should().Be((byte)expected);
        color.G.Should().Be((byte)expected);
        color.B.Should().Be((byte)expected);
        color.K.Should().BeApproximately(1 - gray, 1e-6);
    }

    [Theory]
    [InlineData(2, 1, 255)]
    [InlineData(-1, 0, 0)]
    public void AGreyOutsideItsRangeIsClampedIntoIt(double given, double expected, int expectedChannel)
    {
        // The way this went wrong is worth remembering: the constructor clamped into the field and
        // then assigned the raw value over the top, so nothing clamped at all. A grey of 2 gave a
        // black of -1 and an R of 254, because 2 x 255 does not fit in a byte and the cast is
        // unchecked - a colour lighter than white came out very slightly grey.
        var color = XColor.FromGrayScale(given);

        color.GS.Should().Be(expected);
        color.R.Should().Be((byte)expectedChannel);
        color.G.Should().Be((byte)expectedChannel);
        color.B.Should().Be((byte)expectedChannel);
        color.K.Should().Be(1 - expected);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(0.25)]
    public void BuildingAGreyAndAssigningOneComeToTheSameColour(double gray)
    {
        // The setter always clamped; the constructor is what did not. They are two ways of saying
        // the same thing and have to agree.
        var built = XColor.FromGrayScale(gray);

        var assigned = XColor.FromArgb(255, 0, 0);
        assigned.GS = gray;

        built.Should().Be(assigned);
    }

    [Fact]
    public void SettingARgbComponentRecalculatesTheCmykOne()
    {
        // Pure red has no cyan in it, all the magenta and yellow there is, and no black. Setting
        // the red channel has to bring the CMYK side along or the same colour would be written
        // differently depending on which space the page asked for.
        var color = XColor.FromArgb(0, 0, 0);

        color.R = 255;
        color.G = 0;
        color.B = 0;

        color.ColorSpace.Should().Be(XColorSpace.Rgb);
        color.C.Should().Be(0);
        color.M.Should().Be(1);
        color.Y.Should().Be(1);
        color.K.Should().Be(0);
    }

    [Fact]
    public void BlackIsTheOneRgbColourThatIsAllBlackAndNothingElse()
    {
        var black = XColor.FromArgb(0, 0, 0);

        black.C.Should().Be(0);
        black.M.Should().Be(0);
        black.Y.Should().Be(0);
        black.K.Should().Be(1);
        black.GS.Should().Be(0, "grey is how light a colour is, and black has no light in it");
    }

    public static TheoryData<string, XColor, double> WhiteAndBlackEachWayIn() => new()
    {
        { "white from RGB", XColor.FromArgb(255, 255, 255), 1 },
        { "white from CMYK", XColor.FromCmyk(0, 0, 0, 0), 1 },
        { "white from grey", XColor.FromGrayScale(1), 1 },
        { "black from RGB", XColor.FromArgb(0, 0, 0), 0 },
        { "black from CMYK", XColor.FromCmyk(0, 0, 0, 1), 0 },
        { "black from grey", XColor.FromGrayScale(0), 0 },
    };

    [Theory]
    [MemberData(nameof(WhiteAndBlackEachWayIn))]
    public void GreyMeansHowLightAColourIsWhicheverWayItWasBuilt(string route, XColor color, double expected)
    {
        // RgbChanged used to store the black ink it found as the grey - white 0, black 1 - where
        // the CMYK and grey ways in store lightness. Issue #187.
        color.GS.Should().Be(expected, route);
    }

    [Fact]
    public void AnRgbColoursGreyIsItsWeightedLightness()
    {
        // A grey is its own lightness exactly, and anything else weighs its channels as the CMYK
        // way in weighs the inks - green counts for most and blue for least.
        XColor.FromArgb(128, 128, 128).GS.Should().BeApproximately(128 / 255.0, 1e-7);
        XColor.FromArgb(255, 0, 0).GS.Should().BeApproximately(0.3, 1e-6);
        XColor.FromArgb(0, 255, 0).GS.Should().BeApproximately(0.59, 1e-6);
        XColor.FromArgb(0, 0, 255).GS.Should().BeApproximately(0.11, 1e-6);
    }

    [Fact]
    public void WhiteAndBlackAreEachOneColourWhicheverWayTheyWereBuilt()
    {
        // Equality compares every component, the derived grey included, and the grey was the one
        // thing that told an RGB black from a CMYK or grey one. Issue #187.
        XColor.FromArgb(0, 0, 0).Should().Be(XColor.FromCmyk(0, 0, 0, 1));
        XColor.FromArgb(0, 0, 0).Should().Be(XColor.FromGrayScale(0));
        XColor.FromArgb(255, 255, 255).Should().Be(XColor.FromCmyk(0, 0, 0, 0));
        XColor.FromArgb(255, 255, 255).Should().Be(XColor.FromGrayScale(1));
    }

    [Fact]
    public void AnRgbColourDeclaredGreyKeepsTheGreyItsChannelsSay()
    {
        // Changing the space changes no component, so what a grey writer reads - GS - has to
        // already agree with the RGB channels it was derived from.
        var black = XColor.FromArgb(0, 0, 0);
        var white = XColor.FromArgb(255, 255, 255);

        black.ColorSpace = XColorSpace.GrayScale;
        white.ColorSpace = XColorSpace.GrayScale;

        black.GS.Should().Be(0);
        white.GS.Should().Be(1);
    }

    [Fact]
    public void SettingACmykComponentRecalculatesTheRgbOne()
    {
        var color = XColor.FromCmyk(0, 0, 0, 0);

        color.C = 1;

        color.ColorSpace.Should().Be(XColorSpace.Cmyk);
        color.R.Should().Be(0);
        color.G.Should().Be(255);
        color.B.Should().Be(255);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    [InlineData(0.25, 0.25)]
    public void EveryCmykSetterClampsToItsRange(double given, double expected)
    {
        var color = XColor.FromCmyk(0, 0, 0, 0);

        color.C = given;
        color.M = given;
        color.Y = given;
        color.K = given;

        color.C.Should().BeApproximately(expected, 1e-6);
        color.M.Should().BeApproximately(expected, 1e-6);
        color.Y.Should().BeApproximately(expected, 1e-6);
        color.K.Should().BeApproximately(expected, 1e-6);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    [InlineData(0.25, 0.25)]
    public void TheGreySetterClampsToItsRangeAndTakesTheColourWithIt(double given, double expected)
    {
        var color = XColor.FromArgb(255, 0, 0);

        color.GS = given;

        color.GS.Should().BeApproximately(expected, 1e-6);
        color.ColorSpace.Should().Be(XColorSpace.GrayScale);
        color.R.Should().Be(color.G);
        color.G.Should().Be(color.B);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 1)]
    [InlineData(0.25, 0.25)]
    public void TheAlphaSetterClampsToItsRange(double given, double expected)
    {
        var color = XColor.FromArgb(255, 0, 0);

        color.A = given;

        color.A.Should().BeApproximately(expected, 1e-6);
    }

    [Fact]
    public void TheColourSpaceCanBeChangedWithoutChangingTheColour()
    {
        // The space is only a decision about how to write the colour out, so setting it leaves
        // every component alone.
        var color = XColor.FromArgb(10, 20, 30);

        color.ColorSpace = XColorSpace.Cmyk;

        color.ColorSpace.Should().Be(XColorSpace.Cmyk);
        color.R.Should().Be(10);
    }

    [Fact]
    public void AColourSpaceThatDoesNotExistIsRefused()
    {
        var act = () => { var color = XColor.FromArgb(0, 0, 0); color.ColorSpace = (XColorSpace)99; };

        act.Should().Throw<InvalidEnumArgumentException>();
    }

    [Theory]
    [InlineData(255, 0, 0, 0)]
    [InlineData(0, 255, 0, 120)]
    [InlineData(0, 0, 255, 240)]
    [InlineData(255, 255, 0, 60)]
    [InlineData(128, 128, 128, 0)]
    public void HueIsMeasuredInDegreesRoundTheColourWheel(int red, int green, int blue, double expected)
    {
        XColor.FromArgb(red, green, blue).GetHue().Should().BeApproximately(expected, 1e-6);
    }

    [Fact]
    public void SaturationIsZeroForGreyAndOneForAPureHue()
    {
        XColor.FromArgb(128, 128, 128).GetSaturation().Should().Be(0);
        XColor.FromArgb(255, 0, 0).GetSaturation().Should().BeApproximately(1, 1e-6);

        // A pale red is still fully saturated, and it takes the other of the two formulas to say
        // so, because its lightness is above the halfway mark.
        XColor.FromArgb(255, 128, 128).GetSaturation().Should().BeApproximately(1, 1e-6);
    }

    [Fact]
    public void BrightnessRunsFromBlackToWhite()
    {
        XColor.FromArgb(0, 0, 0).GetBrightness().Should().Be(0);
        XColor.FromArgb(255, 255, 255).GetBrightness().Should().Be(1);
        XColor.FromArgb(255, 0, 0).GetBrightness().Should().BeApproximately(0.5, 1e-6);
    }

    [Fact]
    public void TwoColoursAreEqualWhenEveryComponentIs()
    {
        var color = XColor.FromArgb(10, 20, 30);

        (color == XColor.FromArgb(10, 20, 30)).Should().BeTrue();
        (color != XColor.FromArgb(10, 20, 30)).Should().BeFalse();
        (color != XColor.FromArgb(10, 20, 31)).Should().BeTrue();
        color.Equals(XColor.FromArgb(10, 20, 30)).Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        color.Equals("not a colour").Should().BeFalse();
        color.GetHashCode().Should().Be(XColor.FromArgb(10, 20, 30).GetHashCode());
    }

    [Fact]
    public void RedBuiltFromRgbAndRedBuiltFromCmykAreTheSameColour()
    {
        // Equality takes in every channel, the derived grey included. The grey used to be the one
        // that told these apart - from RGB it was the black ink the conversion found, from CMYK a
        // weighted lightness - and now both ways in weigh a colour with no black in it alike.
        // Two ways in are only equal where both conversions are exact, which is worth knowing
        // before using an XColor as a dictionary key or deduplicating a palette: a colour with
        // black in it goes through a truncation to a byte on one side and not the other.
        var asRgb = XColor.FromArgb(255, 0, 0);
        var asCmyk = XColor.FromCmyk(0, 1, 1, 0);

        asCmyk.R.Should().Be(asRgb.R);
        asCmyk.G.Should().Be(asRgb.G);
        asCmyk.B.Should().Be(asRgb.B);
        asCmyk.C.Should().Be(asRgb.C);
        asCmyk.M.Should().Be(asRgb.M);
        asCmyk.GS.Should().Be(asRgb.GS);

        (asRgb == asCmyk).Should().BeTrue();
        asRgb.GetHashCode().Should().Be(asCmyk.GetHashCode());
    }

    [Fact]
    public void TheDefaultColourIsTheEmptyOne()
    {
        new XColor().IsEmpty.Should().BeTrue();
        XColor.Empty.IsEmpty.Should().BeTrue();
        XColor.FromArgb(0, 0, 0, 0).IsEmpty.Should().BeFalse(
            "fully transparent black still went through the RGB conversion, which leaves black " +
            "ink on the CMYK side, and empty has none");
    }

    [Fact]
    public void AskingForAColourByNameIsNotImplementedAndSaysSoByGivingBackNothing()
    {
        // Upstream left FromName returning Empty rather than throwing. Pinned so that the day it
        // grows a real lookup, the change shows up here rather than as a mysteriously working
        // caller.
        XColor.FromName("Red").IsEmpty.Should().BeTrue();
        XColor.FromName("nonsense").IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void EveryComponentSurvivesTheSerializerRoundTrip()
    {
        // RgbCmykG exists so that XmlSerializer can carry all three representations across
        // rather than reconstructing two of them from the third and losing the difference.
        var original = XColor.FromCmyk(0.5, 0.25, 0.75, 0.125, 0.0625);

        var copy = new XColor { RgbCmykG = original.RgbCmykG };

        copy.R.Should().Be(original.R);
        copy.G.Should().Be(original.G);
        copy.B.Should().Be(original.B);
        copy.C.Should().BeApproximately(original.C, 1e-6);
        copy.M.Should().BeApproximately(original.M, 1e-6);
        copy.Y.Should().BeApproximately(original.Y, 1e-6);
        copy.K.Should().BeApproximately(original.K, 1e-6);
        copy.GS.Should().BeApproximately(original.GS, 1e-6);
        copy.A.Should().BeApproximately(original.A, 1e-6);
    }
}
