using System;
using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The other half of <see cref="Unit"/>: reading a length back in a measure other than the one
///   it was written in, and converting the value itself from one to another.
///   <para>
///   Every one of the five measures has to answer for all five, which is twenty-five conversions
///   written out as five switch statements that are near-copies of one another — so a mistake in
///   one arm of one of them is invisible from the arm beside it. <see cref="UnitTests"/> covers
///   what a unit is; this covers what it converts to.
///   </para>
/// </summary>
public class UnitConversionTests
{
    /// <summary>One inch, spelled five ways. Every conversion below is a way of saying this.</summary>
    private const double PointsPerInch = 72;
    private const double CentimetresPerInch = 2.54;
    private const double MillimetresPerInch = 25.4;
    private const double PicasPerInch = 6;

    [Fact]
    public void AnInchReadsTheSameLengthInEveryMeasure()
    {
        var inch = Unit.FromInch(1);

        inch.Inch.Should().BeApproximately(1, 1e-4);
        inch.Point.Should().BeApproximately(PointsPerInch, 1e-4);
        inch.Centimeter.Should().BeApproximately(CentimetresPerInch, 1e-4);
        inch.Millimeter.Should().BeApproximately(MillimetresPerInch, 1e-4);
        inch.Pica.Should().BeApproximately(PicasPerInch, 1e-4);
    }

    [Fact]
    public void SeventyTwoPointsReadTheSameLengthInEveryMeasure()
    {
        var point = Unit.FromPoint(PointsPerInch);

        point.Inch.Should().BeApproximately(1, 1e-4);
        point.Point.Should().BeApproximately(PointsPerInch, 1e-4);
        point.Centimeter.Should().BeApproximately(CentimetresPerInch, 1e-4);
        point.Millimeter.Should().BeApproximately(MillimetresPerInch, 1e-4);
        point.Pica.Should().BeApproximately(PicasPerInch, 1e-4);
    }

    [Fact]
    public void TwoAndAHalfCentimetresReadTheSameLengthInEveryMeasure()
    {
        var centimetre = Unit.FromCentimeter(CentimetresPerInch);

        centimetre.Inch.Should().BeApproximately(1, 1e-4);
        centimetre.Point.Should().BeApproximately(PointsPerInch, 1e-4);
        centimetre.Centimeter.Should().BeApproximately(CentimetresPerInch, 1e-4);
        centimetre.Millimeter.Should().BeApproximately(MillimetresPerInch, 1e-4);
        centimetre.Pica.Should().BeApproximately(PicasPerInch, 1e-4);
    }

    [Fact]
    public void TwentyFiveMillimetresReadTheSameLengthInEveryMeasure()
    {
        var millimetre = Unit.FromMillimeter(MillimetresPerInch);

        millimetre.Inch.Should().BeApproximately(1, 1e-4);
        millimetre.Point.Should().BeApproximately(PointsPerInch, 1e-4);
        millimetre.Centimeter.Should().BeApproximately(CentimetresPerInch, 1e-4);
        millimetre.Millimeter.Should().BeApproximately(MillimetresPerInch, 1e-4);
        millimetre.Pica.Should().BeApproximately(PicasPerInch, 1e-4);
    }

    [Fact]
    public void SixPicasReadTheSameLengthInEveryMeasure()
    {
        var pica = Unit.FromPica(PicasPerInch);

        pica.Inch.Should().BeApproximately(1, 1e-4);
        pica.Point.Should().BeApproximately(PointsPerInch, 1e-4);
        pica.Centimeter.Should().BeApproximately(CentimetresPerInch, 1e-4);
        pica.Millimeter.Should().BeApproximately(MillimetresPerInch, 1e-4);
        pica.Pica.Should().BeApproximately(PicasPerInch, 1e-4);
    }

    [Fact]
    public void AnUninitializedUnitIsNoLengthAtAllInEveryMeasure()
    {
        var empty = Unit.Empty;

        empty.IsEmpty.Should().BeTrue();
        empty.Point.Should().Be(0);
        empty.Centimeter.Should().Be(0);
        empty.Inch.Should().Be(0);
        empty.Millimeter.Should().Be(0);
        empty.Pica.Should().Be(0);
    }

    // ----- converting the value itself ------------------------------------------------------------

    [Theory]
    [InlineData(UnitType.Point, PointsPerInch)]
    [InlineData(UnitType.Centimeter, CentimetresPerInch)]
    [InlineData(UnitType.Inch, 1)]
    [InlineData(UnitType.Millimeter, MillimetresPerInch)]
    [InlineData(UnitType.Pica, PicasPerInch)]
    public void ConvertingAnInchKeepsTheLengthAndChangesTheMeasure(UnitType type, double expected)
    {
        var unit = Unit.FromInch(1);

        unit.ConvertType(type);

        unit.Type.Should().Be(type);
        unit.Value.Should().BeApproximately(expected, 1e-4);
        unit.Inch.Should().BeApproximately(1, 1e-4);
    }

    [Fact]
    public void ConvertingToTheMeasureItAlreadyHasChangesNothing()
    {
        var unit = Unit.FromCentimeter(3);

        unit.ConvertType(UnitType.Centimeter);

        unit.Value.Should().Be(3);
        unit.Type.Should().Be(UnitType.Centimeter);
    }

    [Fact]
    public void ConvertingToAMeasureThatDoesNotExistIsRefused()
    {
        var unit = Unit.FromPoint(1);

        var converting = () => unit.ConvertType((UnitType)99);

        converting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AUnitCannotBeBuiltWithAMeasureThatDoesNotExist()
    {
        var building = () => new Unit(1, (UnitType)99);

        building.Should().Throw<ArgumentException>();
    }

    // ----- printing --------------------------------------------------------------------------------

    [Theory]
    [InlineData(UnitType.Point, "3")]
    [InlineData(UnitType.Centimeter, "3cm")]
    [InlineData(UnitType.Inch, "3in")]
    [InlineData(UnitType.Millimeter, "3mm")]
    [InlineData(UnitType.Pica, "3pc")]
    public void AUnitPrintsItsMeasureAfterItsValueAndPointPrintsNone(UnitType type, string expected)
    {
        new Unit(3, type).ToString().Should().Be(expected);
    }

    [Fact]
    public void AUnitPrintsThroughAFormatAndAFormatProvider()
    {
        var unit = Unit.FromCentimeter(2.5);

        unit.ToString(CultureInfo.InvariantCulture).Should().Be("2.5cm");
        unit.ToString("0.00").Should().Be("2.50cm");
        ((IFormattable)unit).ToString("0.0", CultureInfo.InvariantCulture).Should().Be("2.5cm");
    }

    [Fact]
    public void AnEmptyUnitPrintsAZeroWhicheverWayItIsAsked()
    {
        var empty = Unit.Empty;

        empty.ToString().Should().Be("0");
        empty.ToString(CultureInfo.InvariantCulture).Should().Be("0");
        empty.ToString("0.00").Should().Be("0.00");
        ((IFormattable)empty).ToString("0.00", CultureInfo.InvariantCulture).Should().Be("0.00");
    }
}
