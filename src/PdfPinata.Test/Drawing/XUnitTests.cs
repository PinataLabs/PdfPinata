using System;
using System.Globalization;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XUnit"/> is the type every page size, margin and offset in this library is
///   eventually written in, and it carries its unit of measure around with it rather than
///   normalising to point on the way in. That makes two things worth pinning: that each of the
///   five getters converts from each of the five stored types correctly - twenty-five answers,
///   not five - and that the type survives the journeys a value takes through the API, because a
///   value that silently arrives as point when it was written as millimetres is off by a factor
///   of nearly three and still looks plausible.
/// </summary>
public class XUnitTests
{
    /// <summary>
    ///   One length, written five ways. Every conversion below is checked against this row, so
    ///   the arithmetic is stated once and the tests only say which way round they read it.
    /// </summary>
    const double OneInchInPoint = 72;
    const double OneInchInMillimeter = 25.4;
    const double OneInchInCentimeter = 2.54;
    const double OneInchInPresentation = 96;

    static readonly XUnit[] OneInchEachWay =
    {
        XUnit.FromPoint(OneInchInPoint),
        XUnit.FromInch(1),
        XUnit.FromMillimeter(OneInchInMillimeter),
        XUnit.FromCentimeter(OneInchInCentimeter),
        XUnit.FromPresentation(OneInchInPresentation)
    };

    public static TheoryData<int> EachWayOfWritingOneInch()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < OneInchEachWay.Length; index++)
            data.Add(index);
        return data;
    }

    [Theory]
    [MemberData(nameof(EachWayOfWritingOneInch))]
    public void EveryGetterReadsTheSameLengthWhicheverUnitItWasStoredIn(int index)
    {
        var unit = OneInchEachWay[index];

        unit.Point.Should().BeApproximately(OneInchInPoint, 1e-9);
        unit.Inch.Should().BeApproximately(1, 1e-9);
        unit.Millimeter.Should().BeApproximately(OneInchInMillimeter, 1e-9);
        unit.Centimeter.Should().BeApproximately(OneInchInCentimeter, 1e-9);
        unit.Presentation.Should().BeApproximately(OneInchInPresentation, 1e-9);
    }

    [Theory]
    [MemberData(nameof(EachWayOfWritingOneInch))]
    public void TheRawValueAndTypeAreWhatWasStoredRatherThanAConversionOfIt(int index)
    {
        var unit = OneInchEachWay[index];

        // Value is documented as the number with no conversion applied, so reading it back
        // through the getter that matches Type has to give the same number.
        var throughItsOwnGetter = unit.Type switch
        {
            XGraphicsUnit.Point => unit.Point,
            XGraphicsUnit.Inch => unit.Inch,
            XGraphicsUnit.Millimeter => unit.Millimeter,
            XGraphicsUnit.Centimeter => unit.Centimeter,
            XGraphicsUnit.Presentation => unit.Presentation,
            _ => throw new InvalidOperationException()
        };

        unit.Value.Should().Be(throughItsOwnGetter);
    }

    [Fact]
    public void SettingAGetterAlsoSetsTheTypeItBelongsTo()
    {
        // Each setter is documented as storing the value in its own unit, so an XUnit that was
        // millimetres and is then assigned inches is inches afterwards, not converted millimetres.
        var unit = XUnit.FromMillimeter(100);

        unit.Inch = 2;

        unit.Type.Should().Be(XGraphicsUnit.Inch);
        unit.Value.Should().Be(2);
        unit.Point.Should().BeApproximately(144, 1e-9);
    }

    /// <summary>
    ///   The presentation setter is the fifth of five and behaves like the other four. This test
    ///   used to assert the opposite - that the unit called itself Point afterwards - on the
    ///   grounds that PinataLayout reads Type back to decide what to write. It does, but the type it
    ///   reads is <c>PinataLayout.DocumentObjectModel.Unit</c> and its <c>UnitType</c>, which has no
    ///   presentation member and is not this type at all. Nothing anywhere assigns
    ///   <see cref="XUnit.Presentation"/>, which is why a copy of the Point setter sat here
    ///   unnoticed.
    /// </summary>
    [Fact]
    public void SettingPresentationLeavesTheUnitCallingItselfPresentation()
    {
        var unit = XUnit.FromInch(1);

        unit.Presentation = 96;

        unit.Type.Should().Be(XGraphicsUnit.Presentation);
        unit.Value.Should().Be(96);
        unit.Inch.Should().BeApproximately(1, 1e-9, "ninety-six presentation units are an inch");
        unit.Point.Should().BeApproximately(72, 1e-9);
    }

    [Theory]
    [InlineData(XGraphicsUnit.Point)]
    [InlineData(XGraphicsUnit.Inch)]
    [InlineData(XGraphicsUnit.Millimeter)]
    [InlineData(XGraphicsUnit.Centimeter)]
    [InlineData(XGraphicsUnit.Presentation)]
    public void ConvertTypeKeepsTheLengthAndChangesOnlyHowItIsWritten(XGraphicsUnit type)
    {
        var unit = XUnit.FromInch(1);

        unit.ConvertType(type);

        unit.Type.Should().Be(type);
        unit.Point.Should().BeApproximately(OneInchInPoint, 1e-9);
    }

    [Fact]
    public void ConvertingToTheTypeItAlreadyHasIsLeftAlone()
    {
        var unit = XUnit.FromCentimeter(3);

        unit.ConvertType(XGraphicsUnit.Centimeter);

        unit.Value.Should().Be(3);
        unit.Type.Should().Be(XGraphicsUnit.Centimeter);
    }

    [Fact]
    public void ConvertingToAUnitThatDoesNotExistIsRefused()
    {
        var unit = XUnit.FromPoint(1);

        var act = () => unit.ConvertType((XGraphicsUnit)99);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AUnitCannotBeBuiltOnATypeThatDoesNotExist()
    {
        var act = () => new XUnit(1, (XGraphicsUnit)99);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("type");
    }

    [Theory]
    [InlineData("3cm", XGraphicsUnit.Centimeter, 3)]
    [InlineData("3 cm", XGraphicsUnit.Centimeter, 3)]
    [InlineData("2in", XGraphicsUnit.Inch, 2)]
    [InlineData("15mm", XGraphicsUnit.Millimeter, 15)]
    [InlineData("10pt", XGraphicsUnit.Point, 10)]
    [InlineData("10pu", XGraphicsUnit.Presentation, 10)]
    [InlineData("10", XGraphicsUnit.Point, 10)]
    [InlineData("-2.5cm", XGraphicsUnit.Centimeter, -2.5)]
    [InlineData("+2.5CM", XGraphicsUnit.Centimeter, 2.5)]
    public void AStringCarriesItsUnitWithIt(string text, XGraphicsUnit expectedType, double expectedValue)
    {
        XUnit unit = text;

        unit.Type.Should().Be(expectedType);
        unit.Value.Should().BeApproximately(expectedValue, 1e-9);
    }

    [Fact]
    public void ACommaIsReadAsADecimalPointWhateverTheCurrentCulture()
    {
        // The conversion replaces ',' with '.' before parsing, so that a German-entered "2,5cm"
        // is 2.5 centimetres and not a parse failure. Worth a test because the parse itself is
        // pinned to the invariant culture, which would otherwise reject it.
        XUnit unit = "2,5cm";

        unit.Type.Should().Be(XGraphicsUnit.Centimeter);
        unit.Value.Should().BeApproximately(2.5, 1e-9);
    }

    [Fact]
    public void ParseIsTheSameConversionUnderAnotherName()
    {
        XUnit.Parse("3cm").Should().Be((XUnit)"3cm");
    }

    [Fact]
    public void AStringWithNoNumberInItIsRefused()
    {
        var act = () => XUnit.Parse("cm");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AStringWithAUnitNobodyKnowsIsRefused()
    {
        var act = () => XUnit.Parse("3furlongs");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnIntAndADoubleBothArriveAsPoint()
    {
        XUnit fromInt = 5;
        XUnit fromDouble = 5.5;

        fromInt.Type.Should().Be(XGraphicsUnit.Point);
        fromInt.Value.Should().Be(5);
        fromDouble.Type.Should().Be(XGraphicsUnit.Point);
        fromDouble.Value.Should().Be(5.5);
    }

    [Fact]
    public void ConvertingToDoubleGivesThePointValueRatherThanTheRawOne()
    {
        // The implicit conversion is what every drawing call sees, and a length in centimetres
        // has to reach the page as point or every coordinate is out by a factor of 28.
        double asDouble = XUnit.FromCentimeter(2.54);

        asDouble.Should().BeApproximately(OneInchInPoint, 1e-9);
    }

    [Theory]
    [InlineData(XGraphicsUnit.Point, "pt")]
    [InlineData(XGraphicsUnit.Inch, "in")]
    [InlineData(XGraphicsUnit.Millimeter, "mm")]
    [InlineData(XGraphicsUnit.Centimeter, "cm")]
    [InlineData(XGraphicsUnit.Presentation, "pu")]
    public void ToStringWritesTheRawValueAndTheSuffixOfItsOwnUnit(XGraphicsUnit type, string suffix)
    {
        var unit = new XUnit(3, type);

        unit.ToString().Should().Be("3" + suffix);
    }

    [Fact]
    public void ToStringRoundTripsThroughTheStringConversion()
    {
        var unit = XUnit.FromMillimeter(12.5);

        XUnit again = unit.ToString();

        again.Should().Be(unit);
    }

    [Fact]
    public void AFormatProviderDecidesHowTheNumberIsWrittenButNotTheSuffix()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");

        XUnit.FromCentimeter(2.5).ToString(german).Should().Be("2,5cm");
    }

    [Fact]
    public void AFormatStringIsAppliedToTheNumberAlone()
    {
        IFormattable unit = XUnit.FromPoint(1.23456);

        unit.ToString("0.00", CultureInfo.InvariantCulture).Should().Be("1.23pt");
    }

    [Fact]
    public void TwoUnitsAreEqualOnlyWhenBothTheNumberAndTheUnitAgree()
    {
        // Documented as a memberwise comparison: one inch and 72 point are the same length but
        // not the same XUnit, because the type is part of what the value means to PinataLayout.
        var oneInch = XUnit.FromInch(1);
        var seventyTwoPoint = XUnit.FromPoint(72);

        (oneInch == seventyTwoPoint).Should().BeFalse();
        (oneInch != seventyTwoPoint).Should().BeTrue();
        oneInch.Equals(seventyTwoPoint).Should().BeFalse();

        (oneInch == XUnit.FromInch(1)).Should().BeTrue();
        oneInch.Equals(XUnit.FromInch(1)).Should().BeTrue();
        oneInch.GetHashCode().Should().Be(XUnit.FromInch(1).GetHashCode());
    }

    [Fact]
    public void SomethingThatIsNotAUnitIsNotEqualToOne()
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        XUnit.FromPoint(1).Equals("1pt").Should().BeFalse();
    }

    [Fact]
    public void ZeroIsZeroPoint()
    {
        XUnit.Zero.Value.Should().Be(0);
        XUnit.Zero.Type.Should().Be(XGraphicsUnit.Point);
        XUnit.Zero.Should().Be(new XUnit());
    }
}
