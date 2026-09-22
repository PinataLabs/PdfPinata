using System;
using System.Globalization;
using AwesomeAssertions;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="Unit"/> is the length every measurement in the DOM is written in, and it is a
///   value type that remembers which measure it was given in as well as how long it is. Both
///   halves matter: 2.54cm and 72pt are the same length, and a document that says one of them
///   should not be written back out saying the other.
///   <para>
///   It is also the type a caller is most likely to meet by accident, because a string converts to
///   it implicitly. <c>section.PageSetup.LeftMargin = "2cm"</c> is the ordinary way to write a
///   margin, and that conversion parses a suffix, a decimal separator and a sign at runtime with
///   nothing at compile time to catch a mistake.
///   </para>
/// </summary>
public class UnitTests
{
    // 72 points to the inch, 2.54 centimetres to the inch, 12 points to the pica. Everything below
    // follows from those three.
    const double PointsPerInch = 72;
    const double CentimetresPerInch = 2.54;

    // ----- what a unit is worth in every measure -------------------------------------------------

    [Fact]
    public void ALengthGivenInPointsIsThatManyPoints()
    {
        var unit = Unit.FromPoint(72);

        unit.Point.Should().Be(72);
        unit.Inch.Should().BeApproximately(1, 1e-6);
        unit.Centimeter.Should().BeApproximately(2.54, 1e-6);
        unit.Millimeter.Should().BeApproximately(25.4, 1e-6);
        unit.Pica.Should().BeApproximately(6, 1e-6);
    }

    public static TheoryData<UnitType, double, double> EveryMeasureOfAnInch => new()
    {
        { UnitType.Point, PointsPerInch, PointsPerInch },
        { UnitType.Inch, 1, PointsPerInch },
        { UnitType.Centimeter, CentimetresPerInch, PointsPerInch },
        { UnitType.Millimeter, CentimetresPerInch * 10, PointsPerInch },
        { UnitType.Pica, 6, PointsPerInch }
    };

    [Theory]
    [MemberData(nameof(EveryMeasureOfAnInch))]
    public void OneInchIsOneInchWhicheverMeasureItIsGivenIn(
        UnitType type, double value, double expectedPoints)
    {
        var unit = new Unit(value, type);

        unit.Type.Should().Be(type);
        unit.Value.Should().BeApproximately(value, 1e-6);
        unit.Point.Should().BeApproximately(expectedPoints, 1e-4);
    }

    [Theory]
    [MemberData(nameof(EveryMeasureOfAnInch))]
    public void TheNamedConstructorsAgreeWithTheOneThatTakesAType(
        UnitType type, double value, double expectedPoints)
    {
        var named = type switch
        {
            UnitType.Point => Unit.FromPoint(value),
            UnitType.Inch => Unit.FromInch(value),
            UnitType.Centimeter => Unit.FromCentimeter(value),
            UnitType.Millimeter => Unit.FromMillimeter(value),
            UnitType.Pica => Unit.FromPica(value),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        named.Type.Should().Be(type);
        named.Point.Should().BeApproximately(expectedPoints, 1e-4);
    }

    [Fact]
    public void ALengthWithNoMeasureNamedIsInPoints()
    {
        // The single-argument constructor is the one every implicit numeric conversion goes
        // through, so points are what a bare number means everywhere in the DOM.
        new Unit(18).Type.Should().Be(UnitType.Point);
        new Unit(18).Point.Should().Be(18);
    }

    // ----- changing which measure it is kept in ---------------------------------------------------

    [Theory]
    [InlineData(UnitType.Point)]
    [InlineData(UnitType.Inch)]
    [InlineData(UnitType.Centimeter)]
    [InlineData(UnitType.Millimeter)]
    [InlineData(UnitType.Pica)]
    public void ConvertingToAnotherMeasureKeepsTheLength(UnitType type)
    {
        var unit = Unit.FromCentimeter(5);
        var lengthInPoints = unit.Point;

        unit.ConvertType(type);

        unit.Type.Should().Be(type, "the measure is what changed");
        unit.Point.Should().BeApproximately(lengthInPoints, 1e-3, "the length is not");
    }

    [Fact]
    public void ConvertingToTheMeasureItIsAlreadyInChangesNothing()
    {
        var unit = Unit.FromInch(3);

        unit.ConvertType(UnitType.Inch);

        unit.Value.Should().Be(3);
        unit.Type.Should().Be(UnitType.Inch);
    }

    [Fact]
    public void ConvertingToAMeasureThatIsNotOneIsRefused()
    {
        var unit = Unit.FromPoint(10);

        var act = () => unit.ConvertType((UnitType)999);

        act.Should().Throw<ArgumentException>();
    }

    // ----- reading a length out of a string --------------------------------------------------------

    [Theory]
    [InlineData("3cm", UnitType.Centimeter, 3)]
    [InlineData("3mm", UnitType.Millimeter, 3)]
    [InlineData("3in", UnitType.Inch, 3)]
    [InlineData("3pc", UnitType.Pica, 3)]
    [InlineData("3pt", UnitType.Point, 3)]
    [InlineData("3", UnitType.Point, 3)]
    public void EverySuffixNamesTheMeasureItStandsFor(string text, UnitType type, double value)
    {
        Unit unit = text;

        unit.Type.Should().Be(type);
        unit.Value.Should().BeApproximately(value, 1e-6);
    }

    [Theory]
    [InlineData("2CM", UnitType.Centimeter)]
    [InlineData("2In", UnitType.Inch)]
    [InlineData("2PT", UnitType.Point)]
    public void TheSuffixIsReadWhateverCaseItIsWrittenIn(string text, UnitType type)
    {
        ((Unit)text).Type.Should().Be(type);
    }

    [Theory]
    [InlineData("  4cm  ", 4)]
    [InlineData("4 cm", 4)]
    [InlineData("-4cm", -4)]
    [InlineData("+4cm", 4)]
    [InlineData("4.5cm", 4.5)]
    public void SpaceAndSignAndPointAreAllAllowedAroundTheNumber(string text, double value)
    {
        ((Unit)text).Value.Should().BeApproximately(value, 1e-6);
    }

    [Fact]
    public void ACommaIsReadAsADecimalPointWhereverTheMachineIs()
    {
        // Written for the German keyboard, and load-bearing for everyone: the DOM's own serializer
        // writes invariant text, so a comma can only have come from a person, and reading it as a
        // thousands separator would silently multiply the length.
        ((Unit)"4,5cm").Value.Should().BeApproximately(4.5, 1e-6);
    }

    [Fact]
    public void ASuffixThatNamesNoMeasureIsRefused()
    {
        var act = () => { _ = (Unit)"5furlongs"; };

        act.Should().Throw<ArgumentException>().WithMessage("*furlongs*");
    }

    [Fact]
    public void SomethingThatIsNotANumberAtAllIsRefused()
    {
        var act = () => { _ = (Unit)"wide"; };

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    ///   The conversion from string is the only reason <c>unit == null</c> compiles: null converts
    ///   to string and string converts to Unit, so the comparison binds to
    ///   <c>operator ==(Unit, Unit)</c> rather than lifting to <c>Unit?</c>, and the compiler
    ///   cannot warn about it. The guard is what turns a NullReferenceException with nothing to say
    ///   into a sentence naming the mistake.
    /// </summary>
    [Fact]
    public void ANullStringSaysWhatWentWrongRatherThanFailingBlankly()
    {
        var act = () => { _ = (Unit)(string)null; };

        act.Should().Throw<ArgumentNullException>().WithMessage("*IsEmpty*");
    }

    [Fact]
    public void ParseReadsWhatTheConversionReads()
    {
        Unit.Parse("2.5in").Should().Be((Unit)"2.5in");
    }

    // ----- writing a length back out ---------------------------------------------------------------

    [Theory]
    [InlineData(UnitType.Point, "3")]
    [InlineData(UnitType.Centimeter, "3cm")]
    [InlineData(UnitType.Millimeter, "3mm")]
    [InlineData(UnitType.Inch, "3in")]
    [InlineData(UnitType.Pica, "3pc")]
    public void ALengthIsWrittenWithTheSuffixOfItsOwnMeasure(UnitType type, string expected)
    {
        // Points carry no suffix, which is why a bare number reads back as points.
        new Unit(3, type).ToString().Should().Be(expected);
    }

    [Fact]
    public void ALengthIsWrittenInvariantlySoItCanBeReadAnywhere()
    {
        // The serializer writes this text into a DDL file, and a file written on a machine whose
        // decimal separator is a comma has to be readable on one whose separator is a point.
        Unit.FromCentimeter(4.5).ToString().Should().Be("4.5cm");
    }

    [Theory]
    [InlineData("3cm")]
    [InlineData("3mm")]
    [InlineData("2.5in")]
    [InlineData("6pc")]
    [InlineData("18")]
    public void ALengthSurvivesBeingWrittenAndReadAgain(string text)
    {
        Unit original = text;

        Unit again = original.ToString();

        again.Type.Should().Be(original.Type);
        again.Value.Should().BeApproximately(original.Value, 1e-5);
    }

    [Fact]
    public void AFormatIsAppliedToTheNumberAndTheSuffixStillFollows()
    {
        Unit.FromCentimeter(4.567).ToString("0.0").Should().Be("4.6cm");
        Unit.FromCentimeter(4.5).ToString(CultureInfo.InvariantCulture).Should().Be("4.5cm");
    }

    [Fact]
    public void AnUnsetLengthIsWrittenAsZeroWithNoSuffixAtAll()
    {
        Unit.Empty.ToString().Should().Be("0");
        Unit.Empty.ToString("0.00").Should().Be("0.00");
    }

    [Fact]
    public void AnUnsetLengthIsAZeroInTheFormatAndCultureAskedFor()
    {
        // Not always "0": the zero is formatted like any number would be, only without a suffix.
        IFormattable empty = Unit.Empty;

        empty.ToString("F2", CultureInfo.InvariantCulture).Should().Be("0.00");
        empty.ToString("F2", CultureInfo.GetCultureInfo("de-DE")).Should().Be("0,00");
        Unit.Empty.ToString(CultureInfo.GetCultureInfo("de-DE")).Should().Be("0");

        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Unit.Empty.ToString("F2").Should().Be("0.00");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // ----- nothing, and zero, which are not the same thing ------------------------------------------

    [Fact]
    public void AnUnsetLengthIsEmptyAndALengthOfZeroIsNot()
    {
        // The distinction is what lets the value model tell "no margin was given" from "a margin of
        // nothing was given", which decides whether a style's margin is inherited or overridden.
        Unit.Empty.IsEmpty.Should().BeTrue();
        Unit.Zero.IsEmpty.Should().BeFalse();
        Unit.FromPoint(0).IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void AnUnsetLengthMeasuresNothing()
    {
        Unit.Empty.Point.Should().Be(0);
        Unit.Empty.Value.Should().Be(0);
    }

    [Fact]
    public void ADefaultUnitIsTheEmptyOne()
    {
        default(Unit).IsEmpty.Should().BeTrue();
        default(Unit).Should().Be(Unit.Empty);
    }

    // ----- comparing and converting ------------------------------------------------------------------

    [Fact]
    public void TwoLengthsAreEqualWhenTheyAreWrittenTheSameWay()
    {
        // Equality is by the number and the measure, not by the length: this is a value type
        // standing in for what the document says, and the document says "1in" or "72pt".
        Unit.FromInch(1).Should().Be(Unit.FromInch(1));
        // ReSharper disable once EqualExpressionComparison
        (Unit.FromInch(1) == Unit.FromInch(1)).Should().BeTrue();
        (Unit.FromInch(1) != Unit.FromPoint(72)).Should().BeTrue();
    }

    [Fact]
    public void EqualLengthsAgreeOnTheirHashCode()
    {
        Unit.FromCentimeter(3).GetHashCode().Should().Be(Unit.FromCentimeter(3).GetHashCode());
    }

    [Fact]
    public void SomethingThatIsNotAUnitIsNotEqualToOne()
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        Unit.FromPoint(3).Equals("3").Should().BeFalse();
        Unit.FromPoint(3).Equals(null).Should().BeFalse();
    }

    [Theory]
    [InlineData(12)]
    [InlineData(-12)]
    [InlineData(0)]
    public void AnIntBecomesThatManyPoints(int value)
    {
        Unit unit = value;

        unit.Type.Should().Be(UnitType.Point);
        unit.Point.Should().Be(value);
    }

    [Fact]
    public void ADoubleAndAFloatBothBecomePointsToo()
    {
        Unit fromDouble = 12.5;
        Unit fromFloat = 12.5f;

        fromDouble.Point.Should().BeApproximately(12.5, 1e-6);
        fromFloat.Point.Should().BeApproximately(12.5, 1e-6);
        fromDouble.Type.Should().Be(UnitType.Point);
    }

    [Fact]
    public void ALengthUsedAsANumberIsItsLengthInPoints()
    {
        // Which is the conversion that makes arithmetic on margins work, and the reason a length in
        // centimetres added to one in points comes out in points rather than throwing.
        double asDouble = Unit.FromInch(1);
        float asFloat = Unit.FromInch(1);

        asDouble.Should().BeApproximately(72, 1e-4);
        asFloat.Should().BeApproximately(72, 1e-3f);
    }

    [Fact]
    public void SettingTheValueLeavesTheMeasureAlone()
    {
        var unit = Unit.FromCentimeter(1);

        unit.Value = 5;

        unit.Type.Should().Be(UnitType.Centimeter);
        unit.Centimeter.Should().BeApproximately(5, 1e-6);
    }

    // ----- setting a length in one measure and reading it in another ---------------------------------

    [Fact]
    public void ALengthCanBeSetThroughAnyOfItsMeasures()
    {
        // Each of these setters converts and re-labels, so the object ends up in the measure it was
        // last written in rather than the one it started in.
        var unit = Unit.FromPoint(0);

        unit.Centimeter = 2.54;
        unit.Type.Should().Be(UnitType.Centimeter);
        unit.Inch.Should().BeApproximately(1, 1e-6);

        unit.Millimeter = 25.4;
        unit.Type.Should().Be(UnitType.Millimeter);
        unit.Inch.Should().BeApproximately(1, 1e-6);

        unit.Inch = 2;
        unit.Type.Should().Be(UnitType.Inch);
        unit.Point.Should().BeApproximately(144, 1e-4);

        unit.Pica = 6;
        unit.Type.Should().Be(UnitType.Pica);
        unit.Point.Should().BeApproximately(72, 1e-4);

        unit.Point = 36;
        unit.Type.Should().Be(UnitType.Point);
        unit.Inch.Should().BeApproximately(0.5, 1e-6);
    }
}
