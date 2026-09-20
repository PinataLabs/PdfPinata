using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   A pen strokes and a brush fills, and both come in two kinds: the ones a caller makes, which
///   can be adjusted afterwards, and the pre-defined ones handed out by <see cref="XPens"/> and
///   <see cref="XBrushes"/>, which cannot. That second kind is the part worth pinning. They are
///   handed out from static properties, so a caller who changed one would be changing it for
///   every other caller in the process; each of them therefore refuses every setter it has,
///   rather than sharing one mutable instance or quietly copying itself.
/// </summary>
public class XPenAndBrushTests
{
    // ----- pens ----------------------------------------------------------------------------------

    [Fact]
    public void APenIsOneUnitWideAndSolidUnlessItIsToldOtherwise()
    {
        var pen = new XPen(XColors.Red);

        pen.Color.Should().Be(XColors.Red);
        pen.Width.Should().Be(1);
        pen.LineJoin.Should().Be(XLineJoin.Miter);
        pen.LineCap.Should().Be(XLineCap.Flat);
        pen.DashStyle.Should().Be(XDashStyle.Solid);
        pen.DashOffset.Should().Be(0);
        pen.DashPattern.Should().BeEmpty("a solid pen has no pattern rather than a null one");
        pen.Overprint.Should().BeFalse();
        pen.Brush.Should().BeNull();
    }

    [Fact]
    public void APenCanBeGivenItsWidthUpFront()
    {
        new XPen(XColors.Red, 4.5).Width.Should().Be(4.5);
    }

    [Fact]
    public void APenCanStrokeWithABrushInsteadOfAColour()
    {
        var brush = new XSolidBrush(XColors.Blue);

        new XPen(brush).Brush.Should().BeSameAs(brush);
        new XPen(brush).Width.Should().Be(1);
        new XPen(brush, 3).Width.Should().Be(3);
    }

    [Fact]
    public void EverySettingOnAPenCanBeChangedAfterTheFact()
    {
        var pen = new XPen(XColors.Red)
        {
            Color = XColors.Green,
            Width = 3,
            LineJoin = XLineJoin.Round,
            LineCap = XLineCap.Round,
            MiterLimit = 5,
            DashOffset = 2,
            Overprint = true
        };

        pen.Color.Should().Be(XColors.Green);
        pen.Width.Should().Be(3);
        pen.LineJoin.Should().Be(XLineJoin.Round);
        pen.LineCap.Should().Be(XLineCap.Round);
        pen.MiterLimit.Should().Be(5);
        pen.DashOffset.Should().Be(2);
        pen.Overprint.Should().BeTrue();
    }

    [Fact]
    public void GivingAPenAColourTakesAwayItsBrushAndTheOtherWayRound()
    {
        // The two are alternatives rather than layers - a pen strokes with one or the other, so
        // setting either has to clear the other or the renderer would have to guess.
        var pen = new XPen(new XSolidBrush(XColors.Blue));

        pen.Color = XColors.Green;
        pen.Brush.Should().BeNull();

        var brush = new XSolidBrush(XColors.Blue);
        pen.Brush = brush;
        pen.Color.Should().Be(XColor.Empty);
    }

    [Fact]
    public void GivingAPenADashPatternMakesItACustomDashedPen()
    {
        var pen = new XPen(XColors.Red) { DashPattern = new double[] { 3, 1, 1, 1 } };

        pen.DashStyle.Should().Be(XDashStyle.Custom);
        pen.DashPattern.Should().Equal(new double[] { 3, 1, 1, 1 });
    }

    [Fact]
    public void ADashPatternIsCopiedInSoLaterChangesToTheArrayDoNotReachThePen()
    {
        var pattern = new double[] { 3, 1 };
        var pen = new XPen(XColors.Red) { DashPattern = pattern };

        pattern[0] = 99;

        pen.DashPattern.Should().Equal(new double[] { 3, 1 });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ADashOfNoLengthIsRefused(double dash)
    {
        var act = () => new XPen(XColors.Red) { DashPattern = new[] { 3, dash } };

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnEmptyDashPatternIsAllowedAndStillSwitchesTheStyleToCustom()
    {
        var pen = new XPen(XColors.Red) { DashPattern = Array.Empty<double>() };

        pen.DashStyle.Should().Be(XDashStyle.Custom);
        pen.DashPattern.Should().BeEmpty();
    }

    [Fact]
    public void ACopiedPenCarriesEverySettingAndItsOwnCopyOfThePattern()
    {
        var original = new XPen(XColors.Red, 3)
        {
            LineJoin = XLineJoin.Bevel,
            LineCap = XLineCap.Square,
            DashOffset = 1.5,
            DashPattern = new double[] { 4, 2 }
        };

        var copy = original.Clone();

        copy.Color.Should().Be(original.Color);
        copy.Width.Should().Be(original.Width);
        copy.LineJoin.Should().Be(original.LineJoin);
        copy.LineCap.Should().Be(original.LineCap);
        copy.DashOffset.Should().Be(original.DashOffset);
        copy.DashStyle.Should().Be(original.DashStyle);
        copy.DashPattern.Should().Equal(new double[] { 4, 2 });
        copy.DashPattern.Should().NotBeSameAs(original.DashPattern);

        copy.Width = 10;
        original.Width.Should().Be(3);
    }

    [Fact]
    public void CopyingAPenWithNoDashPatternIsNotAnError()
    {
        var copy = new XPen(new XPen(XColors.Red));

        copy.DashPattern.Should().BeEmpty();
    }

    /// <summary>
    ///   Every setter on a pre-defined pen. Each of them has to refuse, because the pen is handed
    ///   out from a static property and changing one would change it for everybody.
    /// </summary>
    static readonly Action[] WaysOfChangingAPredefinedPen =
    {
        () => XPens.Black.Color = XColors.Red,
        () => XPens.Black.Brush = new XSolidBrush(XColors.Red),
        () => XPens.Black.Width = 5,
        () => XPens.Black.LineJoin = XLineJoin.Round,
        () => XPens.Black.LineCap = XLineCap.Round,
        () => XPens.Black.MiterLimit = 5,
        () => XPens.Black.DashStyle = XDashStyle.Dot,
        () => XPens.Black.DashOffset = 1,
        () => XPens.Black.DashPattern = new double[] { 1, 1 },
        () => XPens.Black.Overprint = true
    };

    public static TheoryData<int> EachWayOfChangingAPredefinedPen()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < WaysOfChangingAPredefinedPen.Length; index++)
            data.Add(index);
        return data;
    }

    [Theory]
    [MemberData(nameof(EachWayOfChangingAPredefinedPen))]
    public void APredefinedPenRefusesToBeChanged(int index)
    {
        WaysOfChangingAPredefinedPen[index].Should().Throw<ArgumentException>()
            .WithMessage("*XPen*");
    }

    [Fact]
    public void APredefinedPenIsAFreshObjectEveryTimeItIsAskedFor()
    {
        // Which is why refusing the setters is the only protection there is: two callers holding
        // "the" black pen are not holding the same one, so a copy-on-write would not help either.
        XPens.Black.Should().NotBeSameAs(XPens.Black);
    }

    [Fact]
    public void ACopyOfAPredefinedPenCanBeChangedFreely()
    {
        var pen = XPens.Black.Clone();

        pen.Width = 5;

        pen.Width.Should().Be(5);
        XPens.Black.Width.Should().Be(1);
    }

    // ----- solid brushes -------------------------------------------------------------------------

    [Fact]
    public void ABrushIsTheColourItWasGivenAndNothingElse()
    {
        var brush = new XSolidBrush(XColors.Red);

        brush.Color.Should().Be(XColors.Red);
        brush.Overprint.Should().BeFalse();
    }

    [Fact]
    public void ABrushMadeWithNoColourIsEmptyRatherThanUnusable()
    {
        new XSolidBrush().Color.Should().Be(XColor.Empty);
    }

    [Fact]
    public void ABrushCanBeRecolouredAfterTheFact()
    {
        var brush = new XSolidBrush(XColors.Red) { Color = XColors.Green, Overprint = true };

        brush.Color.Should().Be(XColors.Green);
        brush.Overprint.Should().BeTrue();
    }

    [Fact]
    public void ACopiedBrushIsTheSameColourAndGoesItsOwnWayAfterwards()
    {
        var original = new XSolidBrush(XColors.Red);

        var copy = new XSolidBrush(original);
        copy.Color = XColors.Green;

        copy.Color.Should().Be(XColors.Green);
        original.Color.Should().Be(XColors.Red);
    }

    [Fact]
    public void APredefinedBrushRefusesToBeRecolouredOrOverprinted()
    {
        var recolour = () => XBrushes.Black.Color = XColors.Red;
        var overprint = () => XBrushes.Black.Overprint = true;

        recolour.Should().Throw<ArgumentException>().WithMessage("*XSolidBrush*");
        overprint.Should().Throw<ArgumentException>().WithMessage("*XSolidBrush*");
    }

    [Fact]
    public void ACopyOfAPredefinedBrushCanBeRecoloured()
    {
        var brush = new XSolidBrush(XBrushes.Black);

        brush.Color = XColors.Red;

        brush.Color.Should().Be(XColors.Red);
        XBrushes.Black.Color.Should().Be(XColors.Black);
    }

    // ----- gradient brushes ----------------------------------------------------------------------

    [Fact]
    public void ALinearGradientRunsBetweenTwoPointsOrAcrossARectangle()
    {
        var betweenPoints = new XLinearGradientBrush(
            new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);
        var acrossARectangle = new XLinearGradientBrush(
            new XRect(0, 0, 100, 50), XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal);

        betweenPoints.Should().NotBeNull();
        acrossARectangle.Should().NotBeNull();
    }

    [Fact]
    public void AGradientAcrossARectangleWithNoAreaIsRefused()
    {
        var noWidth = () => new XLinearGradientBrush(
            new XRect(0, 0, 0, 50), XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal);
        var noHeight = () => new XLinearGradientBrush(
            new XRect(0, 0, 100, 0), XColors.Red, XColors.Blue, XLinearGradientMode.Horizontal);

        noWidth.Should().Throw<ArgumentException>();
        noHeight.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AGradientModeThatDoesNotExistIsRefused()
    {
        var act = () => new XLinearGradientBrush(
            new XRect(0, 0, 100, 50), XColors.Red, XColors.Blue, (XLinearGradientMode)99);

        act.Should().Throw<System.ComponentModel.InvalidEnumArgumentException>();
    }

    [Fact]
    public void ARadialGradientCanHaveOneCentreOrTwo()
    {
        new XRadialGradientBrush(new XPoint(50, 50), 0, 40, XColors.Red, XColors.Blue)
            .Should().NotBeNull();
        new XRadialGradientBrush(new XPoint(50, 50), new XPoint(60, 60), 0, 40, XColors.Red, XColors.Blue)
            .Should().NotBeNull();
    }

    [Fact]
    public void AGradientStartsWithNoTransformOfItsOwn()
    {
        var brush = new XLinearGradientBrush(
            new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);

        brush.Transform.IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void EachWayOfTransformingAGradientChangesItsMatrix()
    {
        var brush = new XLinearGradientBrush(
            new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);

        brush.TranslateTransform(10, 20);
        brush.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(10, 20));

        brush.ScaleTransform(2, 2);
        brush.Transform.Transform(new XPoint(1, 1)).Should().Be(new XPoint(12, 22));

        brush.ResetTransform();
        brush.Transform.IsIdentity.Should().BeTrue();
    }

    [Fact]
    public void EveryTransformOnAGradientAlsoTakesAnExplicitOrder()
    {
        // Prepending and appending give different answers once there is more than one transform,
        // so both spellings of each have to be reachable.
        var prepended = new XLinearGradientBrush(
            new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);
        prepended.ScaleTransform(2, 2);
        prepended.TranslateTransform(10, 0, XMatrixOrder.Prepend);

        var appended = new XLinearGradientBrush(
            new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);
        appended.ScaleTransform(2, 2);
        appended.TranslateTransform(10, 0, XMatrixOrder.Append);

        prepended.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(20, 0));
        appended.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(10, 0));
    }

    [Fact]
    public void AGradientCanBeRotatedScaledAndMultipliedByAMatrix()
    {
        var brush = new XLinearGradientBrush(
            new XPoint(0, 0), new XPoint(100, 0), XColors.Red, XColors.Blue);

        brush.RotateTransform(90);
        var turned = brush.Transform.Transform(new XPoint(1, 0));
        turned.X.Should().BeApproximately(0, 1e-12);
        turned.Y.Should().BeApproximately(1, 1e-12);

        brush.ResetTransform();
        brush.RotateTransform(90, XMatrixOrder.Append);
        brush.Transform.IsIdentity.Should().BeFalse();

        brush.ResetTransform();
        brush.ScaleTransform(2, 2, XMatrixOrder.Append);
        brush.Transform.Transform(new XPoint(1, 1)).Should().Be(new XPoint(2, 2));

        brush.ResetTransform();
        brush.MultiplyTransform(new XMatrix(1, 0, 0, 1, 5, 5));
        brush.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(5, 5));

        brush.ResetTransform();
        brush.MultiplyTransform(new XMatrix(1, 0, 0, 1, 5, 5), XMatrixOrder.Append);
        brush.Transform.Transform(new XPoint(0, 0)).Should().Be(new XPoint(5, 5));
    }

    [Fact]
    public void AGradientCanBeHandedAMatrixOutright()
    {
        var brush = new XRadialGradientBrush(new XPoint(50, 50), 0, 40, XColors.Red, XColors.Blue)
        {
            Transform = new XMatrix(2, 0, 0, 2, 0, 0)
        };

        brush.Transform.Transform(new XPoint(1, 1)).Should().Be(new XPoint(2, 2));
    }
}
