using System;
using System.Globalization;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XMatrix"/> is the affine transform behind every <c>cm</c> operator this library
///   writes, and the one thing about it that is easy to get backwards is the order: appending a
///   transform makes it happen <em>after</em> everything already in the matrix, prepending makes
///   it happen first. Upstream left both <c>Rotate(angle)</c> and friends obsolete-with-error
///   precisely because GDI+ and WPF disagreed on which of the two the bare name meant, so the
///   tests below say what each pair does in terms of where a point ends up rather than in terms
///   of the six numbers.
///   <para>
///   The convention throughout is x' = M11·x + M21·y + OffsetX, y' = M12·x + M22·y + OffsetY, so
///   a positive rotation turns anticlockwise in a y-upwards space.
///   </para>
/// </summary>
public class XMatrixTests
{
    private static XMatrix TranslateThenScale()
    {
        var matrix = new XMatrix();
        matrix.TranslateAppend(10, 0);
        matrix.ScaleAppend(2, 2);
        return matrix;
    }

    private static XMatrix ScaleThenTranslate()
    {
        var matrix = new XMatrix();
        matrix.TranslateAppend(10, 0);
        matrix.ScalePrepend(2, 2);
        return matrix;
    }

    [Fact]
    public void AFreshMatrixIsTheIdentityAndLeavesAPointWhereItIs()
    {
        var matrix = new XMatrix();

        matrix.IsIdentity.Should().BeTrue();
        matrix.Should().Be(XMatrix.Identity);
        matrix.Transform(new XPoint(3, 4)).Should().Be(new XPoint(3, 4));
        matrix.GetElements().Should().Equal(new double[] { 1, 0, 0, 1, 0, 0 });
    }

    [Fact]
    public void AMatrixThatHappensToBeTheIdentityIsRecognisedAsOne()
    {
        // The struct carries a type flag as a fast path, and a matrix built from its six numbers
        // starts out with that flag unset. Recognising the identity anyway is what keeps the
        // fast path from being wrong about equality.
        var spelledOut = new XMatrix(1, 0, 0, 1, 0, 0);

        spelledOut.IsIdentity.Should().BeTrue();
        (spelledOut == XMatrix.Identity).Should().BeTrue();
        (spelledOut != XMatrix.Identity).Should().BeFalse();
    }

    [Fact]
    public void SetIdentityThrowsAwayWhateverTheMatrixWasDoing()
    {
        var matrix = new XMatrix(2, 0, 0, 3, 4, 5);

        matrix.SetIdentity();

        matrix.IsIdentity.Should().BeTrue();
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(1, 1));
        matrix.GetElements().Should().Equal(new double[] { 1, 0, 0, 1, 0, 0 });
        matrix.M11.Should().Be(1);
        matrix.M22.Should().Be(1);
        matrix.OffsetX.Should().Be(0);
    }

    [Fact]
    public void TheSixNumbersAreWhereTheyWerePut()
    {
        var matrix = new XMatrix(1, 2, 3, 4, 5, 6);

        matrix.M11.Should().Be(1);
        matrix.M12.Should().Be(2);
        matrix.M21.Should().Be(3);
        matrix.M22.Should().Be(4);
        matrix.OffsetX.Should().Be(5);
        matrix.OffsetY.Should().Be(6);
        matrix.GetElements().Should().Equal(new double[] { 1, 2, 3, 4, 5, 6 });
    }

    [Fact]
    public void EachOfTheSixNumbersCanBeSetOnItsOwn()
    {
        var matrix = new XMatrix
        {
            M11 = 1, M12 = 2, M21 = 3, M22 = 4, OffsetX = 5, OffsetY = 6
        };

        matrix.GetElements().Should().Equal(new double[] { 1, 2, 3, 4, 5, 6 });
    }

    [Fact]
    public void TransformingAPointAppliesTheScaleAndThenTheOffset()
    {
        var matrix = new XMatrix(2, 0, 0, 3, 10, 20);

        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 23));
    }

    [Fact]
    public void TransformingAVectorLeavesTheOffsetOutOfIt()
    {
        // A vector is a displacement rather than a place, so translating it must do nothing.
        var matrix = new XMatrix(2, 0, 0, 3, 10, 20);

        matrix.Transform(new XVector(1, 1)).Should().Be(new XVector(2, 3));
    }

    [Fact]
    public void AppendingMakesTheNewTransformHappenLast()
    {
        // Move ten to the right, then scale everything by two: the offset is scaled too.
        TranslateThenScale().Transform(new XPoint(1, 1)).Should().Be(new XPoint(22, 2));
    }

    [Fact]
    public void PrependingMakesTheNewTransformHappenFirst()
    {
        // Scale by two, then move ten to the right: the offset is not scaled.
        ScaleThenTranslate().Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 2));
    }

    [Fact]
    public void MultiplyingTwoMatricesAppliesTheLeftOneFirst()
    {
        var translate = new XMatrix(1, 0, 0, 1, 10, 0);
        var scale = new XMatrix(2, 0, 0, 2, 0, 0);

        (translate * scale).Transform(new XPoint(1, 1)).Should().Be(new XPoint(22, 2));
        (scale * translate).Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 2));
        XMatrix.Multiply(translate, scale).Should().Be(translate * scale);
    }

    [Fact]
    public void AppendAndPrependAreTheSameMultiplicationFromEitherSide()
    {
        var translate = new XMatrix(1, 0, 0, 1, 10, 0);
        var scale = new XMatrix(2, 0, 0, 2, 0, 0);

        var appended = translate;
        appended.Append(scale);
        appended.Should().Be(translate * scale);

        var prepended = translate;
        prepended.Prepend(scale);
        prepended.Should().Be(scale * translate);
    }

    [Fact]
    public void TheOrderArgumentSaysTheSameThingAsTheMethodNameDoes()
    {
        var scale = new XMatrix(2, 0, 0, 2, 0, 0);

        var appended = new XMatrix(1, 0, 0, 1, 10, 0);
        appended.Multiply(scale, XMatrixOrder.Append);
        appended.Transform(new XPoint(1, 1)).Should().Be(new XPoint(22, 2));

        var prepended = new XMatrix(1, 0, 0, 1, 10, 0);
        prepended.Multiply(scale, XMatrixOrder.Prepend);
        prepended.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 2));
    }

    [Fact]
    public void TranslatingWithAnExplicitOrderAgreesWithTheNamedMethods()
    {
        var appended = new XMatrix(2, 0, 0, 2, 0, 0);
        appended.Translate(10, 0, XMatrixOrder.Append);
        appended.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 2));

        var prepended = new XMatrix(2, 0, 0, 2, 0, 0);
        prepended.Translate(10, 0, XMatrixOrder.Prepend);
        prepended.Transform(new XPoint(1, 1)).Should().Be(new XPoint(22, 2));
    }

    [Fact]
    public void TranslatePrependMovesThePointBeforeTheRestOfTheMatrixSeesIt()
    {
        var matrix = new XMatrix(2, 0, 0, 2, 0, 0);

        matrix.TranslatePrepend(10, 0);

        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(22, 2));
    }

    [Fact]
    public void ScalingByOneNumberScalesBothAxesByIt()
    {
        var appended = new XMatrix();
        appended.ScaleAppend(3);
        appended.Transform(new XPoint(1, 2)).Should().Be(new XPoint(3, 6));

        var prepended = new XMatrix();
        prepended.ScalePrepend(3);
        prepended.Transform(new XPoint(1, 2)).Should().Be(new XPoint(3, 6));

        var withOrder = new XMatrix();
        withOrder.Scale(3, XMatrixOrder.Append);
        withOrder.Should().Be(appended);
    }

    [Fact]
    public void ScalingAboutAPointLeavesThatPointWhereItIs()
    {
        var appended = new XMatrix();
        appended.ScaleAtAppend(2, 2, 10, 10);

        appended.Transform(new XPoint(10, 10)).Should().Be(new XPoint(10, 10));
        appended.Transform(new XPoint(11, 10)).Should().Be(new XPoint(12, 10));

        var prepended = new XMatrix();
        prepended.ScaleAtPrepend(2, 2, 10, 10);
        prepended.Should().Be(appended, "on the identity there is nothing for the order to matter to");
    }

    [Fact]
    public void ScalingWithAnExplicitOrderAgreesWithTheNamedMethods()
    {
        var appended = new XMatrix(1, 0, 0, 1, 10, 0);
        appended.Scale(2, 2, XMatrixOrder.Append);
        appended.Transform(new XPoint(1, 1)).Should().Be(new XPoint(22, 2));

        var prepended = new XMatrix(1, 0, 0, 1, 10, 0);
        prepended.Scale(2, 2, XMatrixOrder.Prepend);
        prepended.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 2));
    }

    [Fact]
    public void APositiveRotationTurnsAnticlockwise()
    {
        var matrix = new XMatrix();
        matrix.RotateAppend(90);

        var turned = matrix.Transform(new XPoint(1, 0));

        turned.X.Should().BeApproximately(0, 1e-12);
        turned.Y.Should().BeApproximately(1, 1e-12);
    }

    [Fact]
    public void RotatingBySomethingOverAFullTurnIsTheSameAsRotatingByTheRemainder()
    {
        var once = new XMatrix();
        once.RotateAppend(30);
        var again = new XMatrix();
        again.RotateAppend(390);

        again.M11.Should().BeApproximately(once.M11, 1e-12);
        again.M12.Should().BeApproximately(once.M12, 1e-12);
    }

    [Fact]
    public void RotatingAboutAPointLeavesThatPointWhereItIs()
    {
        var matrix = new XMatrix();
        matrix.RotateAtAppend(90, 10, 10);

        var centre = matrix.Transform(new XPoint(10, 10));
        centre.X.Should().BeApproximately(10, 1e-12);
        centre.Y.Should().BeApproximately(10, 1e-12);

        var moved = matrix.Transform(new XPoint(11, 10));
        moved.X.Should().BeApproximately(10, 1e-12);
        moved.Y.Should().BeApproximately(11, 1e-12);
    }

    [Fact]
    public void RotatingAboutAPointReadsTheSameWhetherTheCentreIsTwoNumbersOrAPoint()
    {
        var byNumbers = new XMatrix(2, 0, 0, 2, 5, 5);
        byNumbers.RotateAtAppend(30, 10, 10);

        var byPoint = new XMatrix(2, 0, 0, 2, 5, 5);
        byPoint.RotateAtAppend(30, new XPoint(10, 10));

        byPoint.Should().Be(byNumbers);

        var prependedByNumbers = new XMatrix(2, 0, 0, 2, 5, 5);
        prependedByNumbers.RotateAtPrepend(30, 10, 10);

        var prependedByPoint = new XMatrix(2, 0, 0, 2, 5, 5);
        prependedByPoint.RotateAtPrepend(30, new XPoint(10, 10));

        prependedByPoint.Should().Be(prependedByNumbers);
        prependedByPoint.Should().NotBe(byPoint, "the scale is in the way, so the order shows");
    }

    [Fact]
    public void RotatingWithAnExplicitOrderAgreesWithTheNamedMethods()
    {
        var appended = new XMatrix(2, 0, 0, 2, 5, 5);
        appended.RotateAt(30, new XPoint(10, 10), XMatrixOrder.Append);

        var named = new XMatrix(2, 0, 0, 2, 5, 5);
        named.RotateAtAppend(30, new XPoint(10, 10));

        appended.Should().Be(named);
    }

    [Fact]
    public void RotatePrependTurnsThePointBeforeTheRestOfTheMatrixSeesIt()
    {
        var prepended = new XMatrix(1, 0, 0, 1, 10, 0);
        prepended.RotatePrepend(90);

        var moved = prepended.Transform(new XPoint(1, 0));
        moved.X.Should().BeApproximately(10, 1e-12);
        moved.Y.Should().BeApproximately(1, 1e-12);
    }

    [Fact]
    public void RotatingWithTheOrderArgumentTurnsTheMatrixTheSameWay()
    {
        var withOrder = new XMatrix(1, 0, 0, 1, 0, 0);
        withOrder.Rotate(90, XMatrixOrder.Append);

        var named = new XMatrix();
        named.RotateAppend(90);

        withOrder.M11.Should().BeApproximately(named.M11, 1e-12);
        withOrder.M12.Should().BeApproximately(named.M12, 1e-12);
        withOrder.M21.Should().BeApproximately(named.M21, 1e-12);
        withOrder.M22.Should().BeApproximately(named.M22, 1e-12);
    }

    [Fact]
    public void ShearingSlantsOneAxisAlongTheOther()
    {
        var matrix = new XMatrix();
        matrix.ShearAppend(1, 0);

        // A shear in x displaces a point by its own y, so the x-axis is untouched and everything
        // above it leans to the right.
        matrix.Transform(new XPoint(1, 0)).Should().Be(new XPoint(1, 0));
        matrix.Transform(new XPoint(0, 1)).Should().Be(new XPoint(1, 1));
    }

    [Fact]
    public void ShearingWithAnExplicitOrderAgreesWithTheNamedMethods()
    {
        // The scale has to be unequal on the two axes, or shearing before and after it come to
        // the same matrix and the test would pass whichever way round the code had it.
        var appended = new XMatrix(2, 0, 0, 3, 0, 0);
        appended.Shear(1, 0, XMatrixOrder.Append);
        var named = new XMatrix(2, 0, 0, 3, 0, 0);
        named.ShearAppend(1, 0);
        appended.Should().Be(named);

        var prepended = new XMatrix(2, 0, 0, 3, 0, 0);
        prepended.ShearPrepend(1, 0);
        prepended.Should().NotBe(named, "the scale is in the way, so the order shows");
    }

    [Fact]
    public void SkewingIsShearingMeasuredInDegrees()
    {
        // A 45 degree skew slants by exactly one unit per unit, which is a shear of one.
        var skewed = new XMatrix();
        skewed.SkewAppend(45, 0);

        var sheared = new XMatrix();
        sheared.ShearAppend(1, 0);

        skewed.M21.Should().BeApproximately(sheared.M21, 1e-12);
        skewed.Transform(new XPoint(0, 1)).X.Should().BeApproximately(1, 1e-12);
    }

    [Fact]
    public void SkewPrependAndSkewAppendDifferOnceThereIsSomethingToOrderAgainst()
    {
        var appended = new XMatrix(2, 0, 0, 1, 0, 0);
        appended.SkewAppend(45, 0);

        var prepended = new XMatrix(2, 0, 0, 1, 0, 0);
        prepended.SkewPrepend(45, 0);

        appended.Should().NotBe(prepended);
    }

    [Fact]
    public void TransformingAnArrayOfPointsChangesThemAllInPlace()
    {
        var matrix = new XMatrix(2, 0, 0, 2, 1, 1);
        var points = new[] { new XPoint(0, 0), new XPoint(1, 1) };

        matrix.Transform(points);

        points.Should().Equal(new XPoint(1, 1), new XPoint(3, 3));
    }

    [Fact]
    public void TransformingAnArrayOfVectorsChangesThemAllInPlaceAndIgnoresTheOffset()
    {
        var matrix = new XMatrix(2, 0, 0, 2, 1, 1);
        var vectors = new[] { new XVector(0, 0), new XVector(1, 1) };

        matrix.Transform(vectors);

        vectors.Should().Equal(new XVector(0, 0), new XVector(2, 2));
    }

    [Fact]
    public void TransformingNothingIsNotAnError()
    {
        var matrix = new XMatrix(2, 0, 0, 2, 1, 1);

        var points = () => matrix.Transform((XPoint[])null);
        var vectors = () => matrix.Transform((XVector[])null);

        points.Should().NotThrow();
        vectors.Should().NotThrow();
    }

    [Fact]
    public void TransformPointsIsTheSameTransformButInsistsOnHavingSomeToTransform()
    {
        var matrix = new XMatrix(2, 0, 0, 2, 1, 1);
        var points = new[] { new XPoint(0, 0), new XPoint(1, 1) };

        matrix.TransformPoints(points);

        points.Should().Equal(new XPoint(1, 1), new XPoint(3, 3));

        var act = () => matrix.TransformPoints(null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TransformPointsByTheIdentityLeavesThemAlone()
    {
        var points = new[] { new XPoint(1, 2) };

        XMatrix.Identity.TransformPoints(points);

        points.Should().Equal(new XPoint(1, 2));
    }

    [Fact]
    public void TheDeterminantIsTheAreaAMatrixMultipliesBy()
    {
        XMatrix.Identity.Determinant.Should().Be(1);
        new XMatrix(1, 0, 0, 1, 5, 6).Determinant.Should().Be(1, "a translation moves area without changing it");
        new XMatrix(2, 0, 0, 3, 0, 0).Determinant.Should().Be(6);
        new XMatrix(2, 0, 0, 3, 5, 6).Determinant.Should().Be(6);
        new XMatrix(1, 2, 3, 4, 0, 0).Determinant.Should().Be(-2);
    }

    [Fact]
    public void AMatrixThatFlattensThePlaneHasNoInverse()
    {
        var flattening = new XMatrix(1, 1, 1, 1, 0, 0);

        flattening.HasInverse.Should().BeFalse();

        var act = () => { var copy = flattening; copy.Invert(); };
        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    ///   One matrix of each shape <see cref="XMatrix.Invert"/> recognises: the identity, a pure
    ///   translation, a pure scale, a scale with a translation, and one that fits none of those
    ///   and has to go through the general formula.
    /// </summary>
    private static readonly XMatrix[] InvertibleMatrices =
    {
        XMatrix.Identity,
        new(1, 0, 0, 1, 10, 20),
        new(2, 0, 0, 4, 0, 0),
        new(2, 0, 0, 4, 10, 20),
        new(1, 2, 3, 4, 5, 6)
    };

    public static TheoryData<int> EachInvertibleMatrix()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < InvertibleMatrices.Length; index++)
            data.Add(index);
        return data;
    }

    [Theory]
    [MemberData(nameof(EachInvertibleMatrix))]
    public void InvertingAMatrixUndoesIt(int index)
    {
        var matrix = InvertibleMatrices[index];

        // Every branch of Invert has its own arithmetic - the flags say whether the matrix is a
        // translation, a scale, both, or something that needs the general formula - so each is
        // checked the only way that means anything: by putting a point through both.
        matrix.HasInverse.Should().BeTrue();
        var point = new XPoint(7, 11);
        var moved = matrix.Transform(point);

        var inverse = matrix;
        inverse.Invert();
        var back = inverse.Transform(moved);

        back.X.Should().BeApproximately(point.X, 1e-9);
        back.Y.Should().BeApproximately(point.Y, 1e-9);
    }

    [Fact]
    public void TwoMatricesAreEqualWhenAllSixNumbersAre()
    {
        var matrix = new XMatrix(1, 2, 3, 4, 5, 6);

        (matrix == new XMatrix(1, 2, 3, 4, 5, 6)).Should().BeTrue();
        (matrix != new XMatrix(1, 2, 3, 4, 5, 6)).Should().BeFalse();
        (matrix != new XMatrix(1, 2, 3, 4, 5, 7)).Should().BeTrue();
        matrix.Equals(new XMatrix(1, 2, 3, 4, 5, 6)).Should().BeTrue();
        matrix.Equals((object)new XMatrix(1, 2, 3, 4, 5, 6)).Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        matrix.Equals("not a matrix").Should().BeFalse();
        XMatrix.Equals(matrix, new XMatrix(1, 2, 3, 4, 5, 6)).Should().BeTrue();
        XMatrix.Equals(XMatrix.Identity, XMatrix.Identity).Should().BeTrue();
        XMatrix.Equals(XMatrix.Identity, matrix).Should().BeFalse();
        matrix.GetHashCode().Should().Be(new XMatrix(1, 2, 3, 4, 5, 6).GetHashCode());
        XMatrix.Identity.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void AMatrixIsWrittenAsSixNumbersAndReadBackTheSameWay()
    {
        var matrix = new XMatrix(1, 2, 3, 4, 5, 6);

        var text = matrix.ToString(CultureInfo.InvariantCulture);

        text.Should().Be("1,2,3,4,5,6");
        XMatrix.Parse(text).Should().Be(matrix);
    }

    [Fact]
    public void TheIdentityIsWrittenByNameAndReadBackByName()
    {
        XMatrix.Identity.ToString(CultureInfo.InvariantCulture).Should().Be("Identity");
        XMatrix.Parse("Identity").Should().Be(XMatrix.Identity);
    }

    [Fact]
    public void AFormatStringIsAppliedToAllSixNumbers()
    {
        IFormattable matrix = new XMatrix(1.11, 2.22, 3.33, 4.44, 5.17, 6.28);

        matrix.ToString("0.0", CultureInfo.InvariantCulture).Should().Be("1.1,2.2,3.3,4.4,5.2,6.3");
    }
}
