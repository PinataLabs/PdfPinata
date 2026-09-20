using System;
using System.Globalization;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XPoint"/>, <see cref="XVector"/> and <see cref="XSize"/> are the three two-number
///   structs the drawing API is written in, and they are deliberately not interchangeable: a point
///   is somewhere, a vector is a displacement and a size is an extent that cannot go negative.
///   The arithmetic between them says which is which - point minus point is a vector, point plus
///   vector is a point - and it is only ever checked here, because every other test in the suite
///   takes it for granted.
/// </summary>
public class XPointTests
{
    [Fact]
    public void APointPlusAVectorIsAPointFurtherAlong()
    {
        var moved = new XPoint(10, 20) + new XVector(3, -4);

        moved.Should().Be(new XPoint(13, 16));
        XPoint.Add(new XPoint(10, 20), new XVector(3, -4)).Should().Be(moved);
    }

    [Fact]
    public void APointMinusAVectorGoesBackTheOtherWay()
    {
        var moved = new XPoint(10, 20) - new XVector(3, -4);

        moved.Should().Be(new XPoint(7, 24));
        XPoint.Subtract(new XPoint(10, 20), new XVector(3, -4)).Should().Be(moved);
    }

    [Fact]
    public void APointMinusAPointIsTheVectorBetweenThem()
    {
        var between = new XPoint(13, 16) - new XPoint(10, 20);

        between.Should().Be(new XVector(3, -4));
        XPoint.Subtract(new XPoint(13, 16), new XPoint(10, 20)).Should().Be(between);
    }

    [Fact]
    public void APointPlusASizeMovesItByTheExtent()
    {
        (new XPoint(10, 20) + new XSize(3, 4)).Should().Be(new XPoint(13, 24));
    }

    [Fact]
    public void OffsetMovesThePointInPlace()
    {
        var point = new XPoint(1, 2);

        point.Offset(10, 20);

        point.Should().Be(new XPoint(11, 22));
    }

    [Fact]
    public void ScalingAPointMultipliesBothCoordinatesWhicheverSideTheNumberIsOn()
    {
        (new XPoint(3, 4) * 2).Should().Be(new XPoint(6, 8));
        (2 * new XPoint(3, 4)).Should().Be(new XPoint(6, 8));
    }

    [Fact]
    public void MultiplyingByAMatrixIsTheSameAsAskingTheMatrixToTransformIt()
    {
        var matrix = new XMatrix();
        matrix.TranslateAppend(5, 7);
        matrix.ScaleAppend(2, 3);
        var point = new XPoint(1, 1);

        (point * matrix).Should().Be(matrix.Transform(point));
        XPoint.Multiply(point, matrix).Should().Be(matrix.Transform(point));
    }

    [Fact]
    public void APointConvertsToASizeByDroppingItsSigns()
    {
        // A size may not be negative, so the conversion takes absolute values rather than
        // throwing on a point in the third quadrant.
        ((XSize)new XPoint(-3, -4)).Should().Be(new XSize(3, 4));
    }

    [Fact]
    public void APointConvertsToAVectorAsItStands()
    {
        ((XVector)new XPoint(-3, -4)).Should().Be(new XVector(-3, -4));
    }

    [Fact]
    public void XAndYCanBeSetAfterTheFact()
    {
        var point = new XPoint { X = 4, Y = 5 };

        point.X.Should().Be(4);
        point.Y.Should().Be(5);
    }

    [Fact]
    public void TwoPointsAreEqualWhenBothCoordinatesAre()
    {
        var point = new XPoint(1, 2);

        (point == new XPoint(1, 2)).Should().BeTrue();
        (point != new XPoint(1, 2)).Should().BeFalse();
        (point == new XPoint(1, 3)).Should().BeFalse();
        point.Equals(new XPoint(1, 2)).Should().BeTrue();
        point.Equals((object)new XPoint(1, 2)).Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        point.Equals("not a point").Should().BeFalse();
        XPoint.Equals(point, new XPoint(1, 2)).Should().BeTrue();
        point.GetHashCode().Should().Be(new XPoint(1, 2).GetHashCode());
    }

    [Fact]
    public void APointIsWrittenAsTwoNumbersAndReadBackTheSameWay()
    {
        var point = new XPoint(1.5, -2.5);

        var text = point.ToString(CultureInfo.InvariantCulture);

        text.Should().Be("1.5,-2.5");
        XPoint.Parse(text).Should().Be(point);
    }

    [Fact]
    public void AFormatStringIsAppliedToBothCoordinates()
    {
        IFormattable point = new XPoint(1.23456, 2.34567);

        point.ToString("0.0", CultureInfo.InvariantCulture).Should().Be("1.2,2.3");
    }

    [Fact]
    public void ASpaceSeparatedListParsesIntoAnArrayOfPoints()
    {
        var points = XPoint.ParsePoints("1,2 3,4 5,6");

        points.Should().Equal(new XPoint(1, 2), new XPoint(3, 4), new XPoint(5, 6));
    }

    [Fact]
    public void ParsingPointsFromNothingIsRefusedRatherThanReturningNothing()
    {
        var act = () => XPoint.ParsePoints(null);

        act.Should().Throw<ArgumentNullException>();
    }
}
