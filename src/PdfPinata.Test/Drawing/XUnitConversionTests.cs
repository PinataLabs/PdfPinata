using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The half of <see cref="XUnit"/> that changes it rather than reads it: the five setters, each
///   of which replaces the measure as well as the value, and <see cref="XUnit.ConvertType"/>,
///   which keeps the length and changes the measure it is stated in.
///   <para>
///   The five readers are five near-identical switches, one per measure, and so are the setters.
///   <see cref="XUnitTests"/> covers what a unit is and how it parses; this covers the conversions
///   between them, where an arm of one switch can be wrong without the one beside it noticing.
///   </para>
/// </summary>
public class XUnitConversionTests
{
    const double PointsPerInch = 72;
    const double CentimetresPerInch = 2.54;
    const double MillimetresPerInch = 25.4;
    const double PresentationUnitsPerInch = 96;

    [Fact]
    public void AnInchReadsTheSameLengthInEveryMeasure()
    {
        var inch = XUnit.FromInch(1);

        inch.Point.Should().BeApproximately(PointsPerInch, 1e-9);
        inch.Inch.Should().BeApproximately(1, 1e-9);
        inch.Centimeter.Should().BeApproximately(CentimetresPerInch, 1e-9);
        inch.Millimeter.Should().BeApproximately(MillimetresPerInch, 1e-9);
        inch.Presentation.Should().BeApproximately(PresentationUnitsPerInch, 1e-9);
    }

    [Theory]
    [InlineData(XGraphicsUnit.Point, PointsPerInch)]
    [InlineData(XGraphicsUnit.Inch, 1)]
    [InlineData(XGraphicsUnit.Centimeter, CentimetresPerInch)]
    [InlineData(XGraphicsUnit.Millimeter, MillimetresPerInch)]
    [InlineData(XGraphicsUnit.Presentation, PresentationUnitsPerInch)]
    public void EveryMeasureReadsBackAsTheSameInch(XGraphicsUnit type, double value)
    {
        var unit = new XUnit(value, type);

        unit.Inch.Should().BeApproximately(1, 1e-9);
        unit.Type.Should().Be(type);
    }

    // ----- the setters ------------------------------------------------------------------------------

    [Fact]
    public void SettingAMeasureSetsTheValueAndTheMeasureTogether()
    {
        var byPoint = XUnit.FromInch(5);
        byPoint.Point = 36;
        byPoint.Type.Should().Be(XGraphicsUnit.Point);
        byPoint.Inch.Should().BeApproximately(0.5, 1e-9);

        var byInch = XUnit.FromPoint(5);
        byInch.Inch = 2;
        byInch.Type.Should().Be(XGraphicsUnit.Inch);
        byInch.Point.Should().BeApproximately(144, 1e-9);

        var byCentimetre = XUnit.FromPoint(5);
        byCentimetre.Centimeter = CentimetresPerInch;
        byCentimetre.Type.Should().Be(XGraphicsUnit.Centimeter);
        byCentimetre.Inch.Should().BeApproximately(1, 1e-9);

        var byMillimetre = XUnit.FromPoint(5);
        byMillimetre.Millimeter = MillimetresPerInch;
        byMillimetre.Type.Should().Be(XGraphicsUnit.Millimeter);
        byMillimetre.Inch.Should().BeApproximately(1, 1e-9);

        // The presentation setter is the odd one out and deliberately so - see
        // XUnitTests.SettingPresentationLeavesTheUnitCallingItselfPoint, which pins it.
    }

    // ----- converting the value itself ---------------------------------------------------------------

    [Theory]
    [InlineData(XGraphicsUnit.Point, PointsPerInch)]
    [InlineData(XGraphicsUnit.Inch, 1)]
    [InlineData(XGraphicsUnit.Centimeter, CentimetresPerInch)]
    [InlineData(XGraphicsUnit.Millimeter, MillimetresPerInch)]
    [InlineData(XGraphicsUnit.Presentation, PresentationUnitsPerInch)]
    public void ConvertingAnInchKeepsTheLengthAndChangesTheMeasure(XGraphicsUnit type, double expected)
    {
        var unit = XUnit.FromInch(1);

        unit.ConvertType(type);

        unit.Type.Should().Be(type);
        unit.Value.Should().BeApproximately(expected, 1e-9);
        unit.Inch.Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void ConvertingToTheMeasureItAlreadyHasChangesNothing()
    {
        var unit = XUnit.FromCentimeter(3);

        unit.ConvertType(XGraphicsUnit.Centimeter);

        unit.Value.Should().Be(3);
    }

    [Fact]
    public void ConvertingToAMeasureThatDoesNotExistIsRefused()
    {
        var unit = XUnit.FromPoint(1);

        var converting = () => unit.ConvertType((XGraphicsUnit)99);

        converting.Should().Throw<ArgumentException>();
    }

    // ----- a measure that is not one of the five ------------------------------------------------------

    /// <summary>
    ///   A unit cannot be built in a measure that does not exist, which is what keeps the five
    ///   switches inside it from ever reaching their default arm.
    /// </summary>
    [Fact]
    public void AUnitCannotBeBuiltInAMeasureThatDoesNotExist()
    {
        var building = () => new XUnit(1, (XGraphicsUnit)99);

        building.Should().Throw<ArgumentException>();
    }

    // ----- what a unit is worth as a number ------------------------------------------------------------

    [Fact]
    public void AUnitComparesByItsValueAndItsMeasureTogether()
    {
        var oneInch = XUnit.FromInch(1);
        var sameInch = XUnit.FromInch(1);
        var seventyTwoPoints = XUnit.FromPoint(72);

        (oneInch == sameInch).Should().BeTrue();
        oneInch.Equals(sameInch).Should().BeTrue();
        oneInch.GetHashCode().Should().Be(sameInch.GetHashCode());

        (oneInch == seventyTwoPoints).Should().BeFalse("the same length said two different ways");
        (oneInch != seventyTwoPoints).Should().BeTrue();
        oneInch.Equals("an inch").Should().BeFalse();
    }

    [Fact]
    public void AUnitUsedAsANumberIsItsLengthInPoints()
    {
        double asNumber = XUnit.FromInch(1);

        asNumber.Should().BeApproximately(PointsPerInch, 1e-9);
    }

    [Fact]
    public void ANumberUsedAsAUnitIsThatManyPoints()
    {
        XUnit fromInteger = 72;
        XUnit fromDouble = 72.0;

        fromInteger.Type.Should().Be(XGraphicsUnit.Point);
        fromInteger.Inch.Should().BeApproximately(1, 1e-9);
        fromDouble.Inch.Should().BeApproximately(1, 1e-9);
    }
}
