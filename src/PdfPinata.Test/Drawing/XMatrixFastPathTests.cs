using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XMatrix"/> keeps a private flag beside its six numbers saying whether it is the
///   identity, a translation, a scale, both, or something more general, and most of its members
///   take a short cut on that flag rather than doing the whole multiplication. A short cut that
///   disagrees with the numbers is wrong without looking wrong, so each test here starts from a
///   matrix of one particular kind and checks the answer against arithmetic done by hand.
///   <see cref="XMatrixTests"/> has the general behaviour; this is the per-kind paths it leaves.
/// </summary>
public class XMatrixFastPathTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void AppendingATranslationToAGeneralMatrixMovesWhatItAlreadyDid()
    {
        // A quarter turn, which is none of the special kinds, so the offsets are added to as
        // they stand rather than the matrix being rebuilt.
        var matrix = new XMatrix(0, 1, -1, 0, 0, 0);

        matrix.TranslateAppend(10, 20);

        matrix.GetElements().Should().Equal(0, 1, -1, 0, 10, 20);
        // (1, 0) turns to (0, 1) and then moves by (10, 20).
        matrix.Transform(new XPoint(1, 0)).Should().Be(new XPoint(10, 21));
    }

    [Theory]
    [InlineData(XMatrixOrder.Append)]
    [InlineData(XMatrixOrder.Prepend)]
    public void TranslatingTheIdentityWithEitherOrderIsAPlainTranslation(XMatrixOrder order)
    {
        var matrix = new XMatrix();

        matrix.Translate(10, 20, order);

        matrix.GetElements().Should().Equal(1, 0, 0, 1, 10, 20);
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(11, 21));
    }

    [Fact]
    public void RotatingWithThePrependOrderTurnsThePointBeforeTheRestOfTheMatrixSeesIt()
    {
        var matrix = new XMatrix(1, 0, 0, 1, 10, 0);

        matrix.Rotate(90, XMatrixOrder.Prepend);

        // Turned first, so the offset is not turned with it: the quarter turn is 0, 1, -1, 0 and
        // the translation stays where it was.
        var elements = matrix.GetElements();
        elements.Should().HaveCount(6);
        elements.Zip(new double[] { 0, 1, -1, 0, 10, 0 })
            .Should().AllSatisfy(pair => pair.First.Should().BeApproximately(pair.Second, Tolerance));

        var moved = matrix.Transform(new XPoint(1, 0));
        moved.X.Should().BeApproximately(10, Tolerance);
        moved.Y.Should().BeApproximately(1, Tolerance);

        var named = new XMatrix(1, 0, 0, 1, 10, 0);
        named.RotatePrepend(90);
        var byName = named.Transform(new XPoint(1, 0));
        byName.X.Should().BeApproximately(moved.X, Tolerance);
        byName.Y.Should().BeApproximately(moved.Y, Tolerance);
    }

    [Fact]
    public void SettingTheScaleOfATranslationMakesItAScaleAndATranslation()
    {
        var matrix = new XMatrix(1, 0, 0, 1, 5, 0) { M11 = 2 };

        matrix.GetElements().Should().Equal(2, 0, 0, 1, 5, 0);
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(7, 1));
    }

    [Fact]
    public void SettingTheScaleOfAGeneralMatrixLeavesItGeneral()
    {
        var matrix = new XMatrix(1, 2, 3, 4, 0, 0) { M11 = 5 };

        // x' = 5·1 + 3·1, y' = 2·1 + 4·1: the shear terms still count.
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(8, 6));
    }

    [Fact]
    public void SettingAShearTermOfTheIdentityMakesItAShear()
    {
        var matrix = new XMatrix { M21 = 1 };

        matrix.GetElements().Should().Equal(1, 0, 1, 1, 0, 0);
        matrix.Transform(new XPoint(0, 1)).Should().Be(new XPoint(1, 1));
        matrix.Transform(new XPoint(1, 0)).Should().Be(new XPoint(1, 0));
    }

    [Fact]
    public void SettingTheVerticalScaleOfTheIdentityAndThenOfTheScaleItBecame()
    {
        var matrix = new XMatrix { M22 = 3 };
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(1, 3));

        matrix.M22 = 4;
        matrix.GetElements().Should().Equal(1, 0, 0, 4, 0, 0);
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(1, 4));
    }

    [Fact]
    public void SettingTheOffsetsOfTheIdentityOneAtATime()
    {
        var horizontal = new XMatrix { OffsetX = 5 };
        horizontal.GetElements().Should().Equal(1, 0, 0, 1, 5, 0);
        horizontal.Transform(new XPoint(1, 1)).Should().Be(new XPoint(6, 1));

        var vertical = new XMatrix { OffsetY = 5 };
        vertical.GetElements().Should().Equal(1, 0, 0, 1, 0, 5);
        vertical.Transform(new XPoint(1, 1)).Should().Be(new XPoint(1, 6));
    }

    [Fact]
    public void SettingTheOffsetsOfAScaleMakesItAScaleAndATranslation()
    {
        var matrix = new XMatrix(2, 0, 0, 3, 0, 0) { OffsetX = 10 };
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 3));

        matrix.OffsetY = 20;
        matrix.GetElements().Should().Equal(2, 0, 0, 3, 10, 20);
        matrix.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 23));
    }

    [Fact]
    public void ATranslationDoesNotMoveAVector()
    {
        var translation = new XMatrix(1, 0, 0, 1, 10, 20);

        translation.Transform(new XVector(3, 4)).Should().Be(new XVector(3, 4));
    }

    [Fact]
    public void AGeneralMatrixTransformsAVectorByItsFourTermsAlone()
    {
        var matrix = new XMatrix(1, 2, 3, 4, 10, 20);

        // x' = 1·1 + 3·1, y' = 2·1 + 4·1, and no offset.
        matrix.Transform(new XVector(1, 1)).Should().Be(new XVector(4, 6));

        var vectors = new[] { new XVector(1, 0), new XVector(0, 1) };
        matrix.Transform(vectors);
        vectors.Should().Equal(new XVector(1, 2), new XVector(3, 4));
    }

    [Fact]
    public void TheIdentityLeavesAnArrayOfPointsWhereItWas()
    {
        var points = new[] { new XPoint(1, 2), new XPoint(-3, 4) };

        XMatrix.Identity.Transform(points);

        points.Should().Equal(new XPoint(1, 2), new XPoint(-3, 4));
    }

    [Fact]
    public void ATranslationMovesARectangleWithoutResizingIt()
    {
        var translation = new XMatrix(1, 0, 0, 1, 10, 20);

        XRect.Transform(new XRect(1, 2, 3, 4), translation).Should().Be(new XRect(11, 22, 3, 4));
    }

    [Fact]
    public void AScaleResizesARectangleAndMovesItsCorner()
    {
        var scale = new XMatrix(2, 0, 0, 3, 0, 0);

        XRect.Transform(new XRect(1, 2, 3, 4), scale).Should().Be(new XRect(2, 6, 6, 12));
    }

    [Fact]
    public void AScaleAndATranslationScalesTheRectangleFirst()
    {
        var both = new XMatrix(2, 0, 0, 3, 10, 20);

        XRect.Transform(new XRect(1, 2, 3, 4), both).Should().Be(new XRect(12, 26, 6, 12));
    }

    [Fact]
    public void AMirroringScaleFlipsARectangleAndKeepsItsSizePositive()
    {
        // A y-flip is what every page transform with its origin at the bottom left is made of.
        // The corners (1, 2) and (4, 6) land on (-1, -4) and (-4, -12), so the rectangle they
        // span starts at the smaller of each pair.
        var mirror = new XMatrix(-1, 0, 0, -2, 0, 0);

        XRect.Transform(new XRect(1, 2, 3, 4), mirror).Should().Be(new XRect(-4, -12, 3, 8));
    }

    [Fact]
    public void AFlipAndATranslationTransformARectangleInPlace()
    {
        var flip = new XMatrix(1, 0, 0, -1, 0, 100);
        var rect = new XRect(10, 20, 30, 40);

        rect.Transform(flip);

        rect.Should().Be(new XRect(10, 40, 30, 40));
    }

    [Fact]
    public void TheIdentityLeavesARectangleAndNothingMovesTheEmptyOne()
    {
        XRect.Transform(new XRect(1, 2, 3, 4), XMatrix.Identity).Should().Be(new XRect(1, 2, 3, 4));
        XRect.Transform(XRect.Empty, new XMatrix(2, 0, 0, 2, 5, 5)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ToStringWithoutAFormatProviderWritesTheSixNumbers()
    {
        // The list separator comes from the current culture, so it is pinned here; the change is
        // per thread and touches no other test.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var matrix = new XMatrix(1, 2, 3, 4, 5.5, 6);

            matrix.ToString().Should().Be("1,2,3,4,5.5,6");
            XMatrix.Parse(matrix.ToString()).Should().Be(matrix);
            XMatrix.Identity.ToString().Should().Be("Identity");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ACultureWithADecimalCommaSeparatesTheNumbersWithSemicolons()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");

        new XMatrix(1.5, 0, 0, 2, 3, 4).ToString(german).Should().Be("1,5;0;0;2;3;4");
    }

    /// <summary>
    ///   The members upstream marked obsolete as errors, because GDI+ and WPF disagree about
    ///   whether the bare name appends or prepends. No new code can compile a call to one, but an
    ///   assembly compiled before they were marked still binds to them, and what it gets is a
    ///   refusal rather than a guess at the order.
    /// </summary>
    [Theory]
    [InlineData("Translate(Double,Double)")]
    [InlineData("Scale(Double)")]
    [InlineData("ScaleAt(Double,Double,Double,Double)")]
    [InlineData("Rotate(Double)")]
    [InlineData("RotateAt(Double,Double,Double)")]
    [InlineData("RotateAt(Double,XPoint)")]
    [InlineData("Shear(Double,Double)")]
    [InlineData("Skew(Double,Double)")]
    public void AnObsoleteMemberThatNamesNoOrderRefusesAndLeavesTheMatrixAlone(string signature)
    {
        var method = ObsoleteMember(signature);
        object boxed = new XMatrix(2, 0, 0, 3, 4, 5);

        var call = () => method.Invoke(boxed, Arguments(method));

        call.Should().Throw<TargetInvocationException>().WithInnerException<InvalidOperationException>();
        ((XMatrix)boxed).GetElements().Should().Equal(2, 0, 0, 3, 4, 5);
    }

    [Fact]
    public void TheOneObsoleteMemberThatStillWorksScalesBeforeTheRestOfTheMatrix()
    {
        // Scale(x, y) was left working, and it prepends, which is what GDI+ means by the bare
        // name and so what an old caller expects of it.
        var method = ObsoleteMember("Scale(Double,Double)");
        object boxed = new XMatrix(1, 0, 0, 1, 10, 0);

        method.Invoke(boxed, [2.0, 2.0]);

        var prepended = new XMatrix(1, 0, 0, 1, 10, 0);
        prepended.ScalePrepend(2, 2);
        ((XMatrix)boxed).Should().Be(prepended);
        ((XMatrix)boxed).Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 2));
    }

    private static MethodInfo ObsoleteMember(string signature)
    {
        var method = typeof(XMatrix).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))})" == signature);

        method.GetCustomAttribute<ObsoleteAttribute>().Should().NotBeNull()
            .And.Subject.As<ObsoleteAttribute>().IsError.Should().BeTrue();
        return method;
    }

    private static object[] Arguments(MethodInfo method)
    {
        return [..method.GetParameters()
            .Select(p => p.ParameterType == typeof(XPoint) ? (object)new XPoint(1, 1) : 1.0)];
    }
}
