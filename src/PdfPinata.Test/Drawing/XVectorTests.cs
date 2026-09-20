using System;
using System.Globalization;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XVector"/> carries the displacement arithmetic the path and arc code is written
///   in - <see cref="XVector.Normalize"/> and <see cref="XVector.AngleBetween"/> in particular are
///   what decides which way an arc bulges. See <see cref="XPointTests"/> for the point side of the
///   same arithmetic.
/// </summary>
public class XVectorTests
{
    [Fact]
    public void VectorsAddAndSubtractComponentwise()
    {
        (new XVector(1, 2) + new XVector(3, 4)).Should().Be(new XVector(4, 6));
        (new XVector(1, 2) - new XVector(3, 4)).Should().Be(new XVector(-2, -2));
        XVector.Add(new XVector(1, 2), new XVector(3, 4)).Should().Be(new XVector(4, 6));
        XVector.Subtract(new XVector(1, 2), new XVector(3, 4)).Should().Be(new XVector(-2, -2));
    }

    [Fact]
    public void AVectorPlusAPointIsThePointDisplaced()
    {
        (new XVector(3, 4) + new XPoint(10, 20)).Should().Be(new XPoint(13, 24));
        XVector.Add(new XVector(3, 4), new XPoint(10, 20)).Should().Be(new XPoint(13, 24));
    }

    [Fact]
    public void NegatingAVectorTurnsItRoundWhicheverWayItIsWritten()
    {
        var negated = -new XVector(3, -4);
        negated.Should().Be(new XVector(-3, 4));

        var inPlace = new XVector(3, -4);
        inPlace.Negate();
        inPlace.Should().Be(negated);
    }

    [Fact]
    public void ScalingWorksFromEitherSideAndDividingIsScalingByTheReciprocal()
    {
        (new XVector(3, 4) * 2).Should().Be(new XVector(6, 8));
        (2 * new XVector(3, 4)).Should().Be(new XVector(6, 8));
        XVector.Multiply(new XVector(3, 4), 2).Should().Be(new XVector(6, 8));
        XVector.Multiply(2, new XVector(3, 4)).Should().Be(new XVector(6, 8));
        (new XVector(3, 4) / 2).Should().Be(new XVector(1.5, 2));
        XVector.Divide(new XVector(3, 4), 2).Should().Be(new XVector(1.5, 2));
    }

    [Fact]
    public void MultiplyingTwoVectorsIsTheirDotProduct()
    {
        (new XVector(1, 2) * new XVector(3, 4)).Should().Be(11);
        XVector.Multiply(new XVector(1, 2), new XVector(3, 4)).Should().Be(11);
    }

    [Fact]
    public void CrossProductAndDeterminantAreTheSameSignedArea()
    {
        XVector.CrossProduct(new XVector(1, 0), new XVector(0, 1)).Should().Be(1);
        XVector.CrossProduct(new XVector(0, 1), new XVector(1, 0)).Should().Be(-1);
        XVector.Determinant(new XVector(1, 2), new XVector(3, 4))
            .Should().Be(XVector.CrossProduct(new XVector(1, 2), new XVector(3, 4)));
    }

    [Fact]
    public void MultiplyingByAMatrixTransformsTheDisplacementWithoutTranslatingIt()
    {
        // A vector has no position, so the offset part of the matrix must not reach it - that is
        // the whole difference between transforming a vector and transforming a point.
        var matrix = new XMatrix();
        matrix.ScaleAppend(2, 3);
        matrix.TranslateAppend(100, 200);
        var vector = new XVector(1, 1);

        (vector * matrix).Should().Be(new XVector(2, 3));
        XVector.Multiply(vector, matrix).Should().Be(new XVector(2, 3));
        matrix.Transform(vector).Should().Be(new XVector(2, 3));
    }

    [Fact]
    public void LengthAndLengthSquaredMeasureTheSameVector()
    {
        var vector = new XVector(3, 4);

        vector.Length.Should().BeApproximately(5, 1e-12);
        vector.LengthSquared.Should().BeApproximately(25, 1e-12);
    }

    [Fact]
    public void NormalizingLeavesTheDirectionAndMakesTheLengthOne()
    {
        var vector = new XVector(3, 4);

        vector.Normalize();

        vector.Length.Should().BeApproximately(1, 1e-12);
        vector.X.Should().BeApproximately(0.6, 1e-12);
        vector.Y.Should().BeApproximately(0.8, 1e-12);
    }

    [Fact]
    public void NormalizingDividesByTheLargerComponentFirstSoAVeryLongVectorDoesNotOverflow()
    {
        // Squaring 1e200 would be infinity, and the vector would normalize to NaN. Dividing by
        // the larger component first is what keeps that from happening, and it is the only
        // reason Normalize is written in two steps.
        var vector = new XVector(3e200, 4e200);

        vector.Normalize();

        vector.X.Should().BeApproximately(0.6, 1e-12);
        vector.Y.Should().BeApproximately(0.8, 1e-12);
    }

    [Theory]
    [InlineData(1, 0, 0, 1, 90)]
    [InlineData(0, 1, 1, 0, -90)]
    [InlineData(1, 0, 1, 0, 0)]
    [InlineData(1, 0, -1, 0, 180)]
    public void AngleBetweenIsMeasuredInDegreesAndSignedByWhichWayItTurns(
        double x1, double y1, double x2, double y2, double expected)
    {
        XVector.AngleBetween(new XVector(x1, y1), new XVector(x2, y2))
            .Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void AVectorConvertsToASizeByDroppingItsSignsAndToAPointAsItStands()
    {
        ((XSize)new XVector(-3, -4)).Should().Be(new XSize(3, 4));
        ((XPoint)new XVector(-3, -4)).Should().Be(new XPoint(-3, -4));
    }

    [Fact]
    public void XAndYCanBeSetAfterTheFact()
    {
        var vector = new XVector { X = 4, Y = 5 };

        vector.X.Should().Be(4);
        vector.Y.Should().Be(5);
    }

    [Fact]
    public void TwoVectorsAreEqualWhenBothComponentsAre()
    {
        var vector = new XVector(1, 2);

        (vector == new XVector(1, 2)).Should().BeTrue();
        (vector != new XVector(1, 2)).Should().BeFalse();
        (vector != new XVector(1, 3)).Should().BeTrue();
        vector.Equals(new XVector(1, 2)).Should().BeTrue();
        vector.Equals((object)new XVector(1, 2)).Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        vector.Equals("not a vector").Should().BeFalse();
        XVector.Equals(vector, new XVector(1, 3)).Should().BeFalse();
        vector.GetHashCode().Should().Be(new XVector(1, 2).GetHashCode());
    }

    [Fact]
    public void AVectorIsWrittenAsTwoNumbersAndReadBackTheSameWay()
    {
        var vector = new XVector(1.5, -2.5);

        var text = vector.ToString(CultureInfo.InvariantCulture);

        text.Should().Be("1.5,-2.5");
        XVector.Parse(text).Should().Be(vector);
    }

    [Fact]
    public void AFormatStringIsAppliedToBothComponents()
    {
        IFormattable vector = new XVector(1.23456, 2.34567);

        vector.ToString("0.0", CultureInfo.InvariantCulture).Should().Be("1.2,2.3");
    }
}
