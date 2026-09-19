using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   <see cref="XRect"/> is the rectangle the whole library measures in - media boxes, image
///   placements, text layout boxes and clipping regions are all one of these. Two of its habits
///   are easy to get wrong and expensive when they are: it has a distinguished <em>empty</em>
///   value stored as a negative width rather than as a flag, and every mutating member refuses to
///   touch that value rather than quietly making a real rectangle out of it. The other habit worth
///   stating out loud is that <see cref="XRect.Top"/> is the smaller y-coordinate, which is the
///   top of a screen and the <em>bottom</em> of a PDF page.
/// </summary>
public class XRectTests
{
    [Fact]
    public void ARectangleIsWhereItWasPutAndAsBigAsItWasMade()
    {
        var rect = new XRect(10, 20, 30, 40);

        rect.X.Should().Be(10);
        rect.Y.Should().Be(20);
        rect.Width.Should().Be(30);
        rect.Height.Should().Be(40);
        rect.Left.Should().Be(10);
        rect.Top.Should().Be(20);
        rect.Right.Should().Be(40);
        rect.Bottom.Should().Be(60);
        rect.Location.Should().Be(new XPoint(10, 20));
        rect.Size.Should().Be(new XSize(30, 40));
    }

    [Fact]
    public void TheFourCornersAndTheCentreAreWhereTheyShouldBe()
    {
        var rect = new XRect(10, 20, 30, 40);

        rect.TopLeft.Should().Be(new XPoint(10, 20));
        rect.TopRight.Should().Be(new XPoint(40, 20));
        rect.BottomLeft.Should().Be(new XPoint(10, 60));
        rect.BottomRight.Should().Be(new XPoint(40, 60));
        rect.Center.Should().Be(new XPoint(25, 40));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void ARectangleCannotBeBuiltWithANegativeExtent(double width, double height)
    {
        var act = () => new XRect(0, 0, width, height);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TwoCornersMakeARectangleWhicheverWayRoundTheyAreGiven()
    {
        var oneWay = new XRect(new XPoint(10, 20), new XPoint(40, 60));
        var theOther = new XRect(new XPoint(40, 60), new XPoint(10, 20));

        oneWay.Should().Be(new XRect(10, 20, 30, 40));
        theOther.Should().Be(oneWay);
    }

    [Fact]
    public void ACornerAndADisplacementMakeTheSameRectangleAsTwoCorners()
    {
        new XRect(new XPoint(10, 20), new XVector(30, 40)).Should().Be(new XRect(10, 20, 30, 40));
        new XRect(new XPoint(10, 20), new XVector(-30, -40)).Should().Be(new XRect(-20, -20, 30, 40));
    }

    [Fact]
    public void ASizeOnItsOwnMakesARectangleAtTheOrigin()
    {
        new XRect(new XSize(30, 40)).Should().Be(new XRect(0, 0, 30, 40));
    }

    [Fact]
    public void ARectangleBuiltOnTheEmptySizeIsTheEmptyRectangle()
    {
        new XRect(XSize.Empty).Should().Be(XRect.Empty);
        new XRect(new XPoint(10, 20), XSize.Empty).Should().Be(XRect.Empty);
    }

    [Fact]
    public void FromLtrbTakesTheSidesRatherThanTheExtents()
    {
        XRect.FromLTRB(10, 20, 40, 60).Should().Be(new XRect(10, 20, 30, 40));
    }

    [Fact]
    public void TheEmptyRectangleIsEmptyAndItsSidesRunTheWrongWayRoundOnPurpose()
    {
        // Right and Bottom answer negative infinity so that an empty rectangle loses every
        // comparison it takes part in, which is what makes Intersect and Union work without
        // special-casing it in every caller.
        XRect.Empty.IsEmpty.Should().BeTrue();
        XRect.Empty.Right.Should().Be(double.NegativeInfinity);
        XRect.Empty.Bottom.Should().Be(double.NegativeInfinity);
        XRect.Empty.Size.Should().Be(XSize.Empty);
        XRect.Empty.GetHashCode().Should().Be(0);
        new XRect(0, 0, 0, 0).IsEmpty.Should().BeFalse("a rectangle with no area still has a place");
    }

    /// <summary>
    ///   Every member that would turn the empty rectangle into a real one. They all have to
    ///   refuse, because the empty rectangle is stored as a corner at positive infinity with a
    ///   negative extent, and half-assigning that leaves a rectangle nobody can reason about.
    /// </summary>
    static readonly Action[] WaysOfChangingARectangle =
    {
        () => { var rect = XRect.Empty; rect.X = 1; },
        () => { var rect = XRect.Empty; rect.Y = 1; },
        () => { var rect = XRect.Empty; rect.Width = 1; },
        () => { var rect = XRect.Empty; rect.Height = 1; },
        () => { var rect = XRect.Empty; rect.Location = new XPoint(1, 1); },
        () => { var rect = XRect.Empty; rect.Size = new XSize(1, 1); },
        () => { var rect = XRect.Empty; rect.Offset(1, 1); },
        () => { var rect = XRect.Empty; rect.Offset(new XVector(1, 1)); },
        () => { var rect = XRect.Empty; rect.Inflate(1, 1); }
    };

    public static TheoryData<int> EachWayOfChangingARectangle()
    {
        var data = new TheoryData<int>();
        for (var index = 0; index < WaysOfChangingARectangle.Length; index++)
            data.Add(index);
        return data;
    }

    [Theory]
    [MemberData(nameof(EachWayOfChangingARectangle))]
    public void TheEmptyRectangleRefusesToBeChangedIntoARealOne(int index)
    {
        WaysOfChangingARectangle[index].Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ARectangleRefusesANegativeExtentAfterTheFactToo(bool testWidth)
    {
        var act = () =>
        {
            var rect = new XRect(0, 0, 10, 10);
            if (testWidth)
                rect.Width = -1;
            else
                rect.Height = -1;
        };

        act.Should().Throw<ArgumentException>()
            .WithMessage(testWidth ? "WidthCannotBeNegative" : "HeightCannotBeNegative");
    }

    [Fact]
    public void LocationAndSizeCanBeSetOnARectangleThatIsNotEmpty()
    {
        var rect = new XRect(0, 0, 10, 10);

        rect.Location = new XPoint(5, 6);
        rect.Size = new XSize(20, 30);

        rect.Should().Be(new XRect(5, 6, 20, 30));
    }

    [Fact]
    public void GivingARectangleTheEmptySizeEmptiesTheRectangle()
    {
        var rect = new XRect(0, 0, 10, 10);

        rect.Size = XSize.Empty;

        rect.Should().Be(XRect.Empty);
    }

    [Theory]
    [InlineData(15, 25, true)]
    [InlineData(10, 20, true)]
    [InlineData(40, 60, true)]
    [InlineData(9, 25, false)]
    [InlineData(15, 61, false)]
    public void ContainsCountsThePointsOnTheEdgeAsInside(double x, double y, bool expected)
    {
        var rect = new XRect(10, 20, 30, 40);

        rect.Contains(x, y).Should().Be(expected);
        rect.Contains(new XPoint(x, y)).Should().Be(expected);
    }

    [Fact]
    public void TheEmptyRectangleContainsNothingAndIsContainedByNothing()
    {
        XRect.Empty.Contains(0, 0).Should().BeFalse();
        XRect.Empty.Contains(new XRect(0, 0, 1, 1)).Should().BeFalse();
        new XRect(0, 0, 10, 10).Contains(XRect.Empty).Should().BeFalse();
        XRect.Empty.IntersectsWith(new XRect(0, 0, 1, 1)).Should().BeFalse();
        new XRect(0, 0, 10, 10).IntersectsWith(XRect.Empty).Should().BeFalse();
    }

    [Fact]
    public void ARectangleContainsAnotherOnlyWhenItCoversAllOfIt()
    {
        var rect = new XRect(0, 0, 100, 100);

        rect.Contains(new XRect(10, 10, 10, 10)).Should().BeTrue();
        rect.Contains(rect).Should().BeTrue("a rectangle covers itself");
        rect.Contains(new XRect(90, 90, 20, 20)).Should().BeFalse();
    }

    [Fact]
    public void TwoRectanglesThatOnlyTouchAlongAnEdgeStillCountAsIntersecting()
    {
        new XRect(0, 0, 10, 10).IntersectsWith(new XRect(10, 0, 10, 10)).Should().BeTrue();
        new XRect(0, 0, 10, 10).IntersectsWith(new XRect(11, 0, 10, 10)).Should().BeFalse();
    }

    [Fact]
    public void IntersectingKeepsTheOverlapAndNothingElse()
    {
        var overlap = XRect.Intersect(new XRect(0, 0, 100, 100), new XRect(50, 50, 100, 100));

        overlap.Should().Be(new XRect(50, 50, 50, 50));

        var inPlace = new XRect(0, 0, 100, 100);
        inPlace.Intersect(new XRect(50, 50, 100, 100));
        inPlace.Should().Be(overlap);
    }

    [Fact]
    public void IntersectingTwoRectanglesThatMissEachOtherLeavesNothing()
    {
        XRect.Intersect(new XRect(0, 0, 10, 10), new XRect(100, 100, 10, 10)).Should().Be(XRect.Empty);
    }

    [Fact]
    public void UnionCoversBothRectangles()
    {
        XRect.Union(new XRect(0, 0, 10, 10), new XRect(90, 90, 10, 10))
            .Should().Be(new XRect(0, 0, 100, 100));
    }

    [Fact]
    public void UnionWithTheEmptyRectangleIsTheOtherRectangleWhicheverSideItIsOn()
    {
        XRect.Union(XRect.Empty, new XRect(1, 2, 3, 4)).Should().Be(new XRect(1, 2, 3, 4));
        XRect.Union(new XRect(1, 2, 3, 4), XRect.Empty).Should().Be(new XRect(1, 2, 3, 4));
    }

    [Fact]
    public void UnionWithSomethingInfinitelyWideStaysInfinitelyWide()
    {
        var infinite = new XRect(0, 0, double.PositiveInfinity, double.PositiveInfinity);

        var union = XRect.Union(new XRect(10, 10, 10, 10), infinite);

        union.Width.Should().Be(double.PositiveInfinity);
        union.Height.Should().Be(double.PositiveInfinity);
    }

    [Fact]
    public void UnionWithAPointStretchesTheRectangleToReachIt()
    {
        XRect.Union(new XRect(0, 0, 10, 10), new XPoint(20, 30)).Should().Be(new XRect(0, 0, 20, 30));

        var inPlace = new XRect(0, 0, 10, 10);
        inPlace.Union(new XPoint(-5, -5));
        inPlace.Should().Be(new XRect(-5, -5, 15, 15));
    }

    [Fact]
    public void OffsetMovesTheRectangleAndLeavesItsSizeAlone()
    {
        XRect.Offset(new XRect(0, 0, 10, 10), 5, 6).Should().Be(new XRect(5, 6, 10, 10));
        XRect.Offset(new XRect(0, 0, 10, 10), new XVector(5, 6)).Should().Be(new XRect(5, 6, 10, 10));

        var inPlace = new XRect(0, 0, 10, 10);
        inPlace.Offset(new XVector(5, 6));
        inPlace.Should().Be(new XRect(5, 6, 10, 10));
    }

    [Fact]
    public void AddingAPointOffsetsTheRectangleAndSubtractingOneUndoesIt()
    {
        var rect = new XRect(10, 20, 30, 40);

        (rect + new XPoint(5, 6)).Should().Be(new XRect(15, 26, 30, 40));
        (rect - new XPoint(5, 6)).Should().Be(new XRect(5, 14, 30, 40));
    }

    [Fact]
    public void InflatingGrowsTheRectangleInEveryDirectionAtOnce()
    {
        // The amount is applied to each side, so the width grows by twice what is asked for.
        XRect.Inflate(new XRect(10, 10, 10, 10), 5, 5).Should().Be(new XRect(5, 5, 20, 20));
        XRect.Inflate(new XRect(10, 10, 10, 10), new XSize(5, 5)).Should().Be(new XRect(5, 5, 20, 20));

        var inPlace = new XRect(10, 10, 10, 10);
        inPlace.Inflate(new XSize(5, 5));
        inPlace.Should().Be(new XRect(5, 5, 20, 20));
    }

    [Fact]
    public void ShrinkingARectanglePastNothingLeavesNothing()
    {
        XRect.Inflate(new XRect(0, 0, 10, 10), -6, -6).Should().Be(XRect.Empty);
    }

    [Fact]
    public void ScalingMovesTheCornerAsWellAsTheExtent()
    {
        var rect = new XRect(10, 20, 30, 40);
        rect.Scale(2, 3);

        rect.Should().Be(new XRect(20, 60, 60, 120));
    }

    [Fact]
    public void ScalingByANegativeNumberReflectsTheRectangleRatherThanGivingItANegativeWidth()
    {
        var rect = new XRect(10, 20, 30, 40);

        rect.Scale(-1, -1);

        rect.Should().Be(new XRect(-40, -60, 30, 40));
        rect.Width.Should().BePositive();
        rect.Height.Should().BePositive();
    }

    [Fact]
    public void ScalingTheEmptyRectangleLeavesItEmpty()
    {
        var rect = XRect.Empty;

        rect.Scale(2, 2);

        rect.Should().Be(XRect.Empty);
    }

    [Fact]
    public void TransformingARectangleGivesTheBoxAroundWhereItLands()
    {
        // A rotation takes a rectangle to something that is not one, so the answer is the
        // smallest upright rectangle that still contains it.
        var matrix = new XMatrix();
        matrix.RotateAppend(45);

        var transformed = XRect.Transform(new XRect(0, 0, 10, 10), matrix);

        var halfDiagonal = Math.Sqrt(200) / 2;
        transformed.Width.Should().BeApproximately(Math.Sqrt(200), 1e-9);
        transformed.Height.Should().BeApproximately(Math.Sqrt(200), 1e-9);
        transformed.Center.X.Should().BeApproximately(0, 1e-9);
        transformed.Center.Y.Should().BeApproximately(halfDiagonal, 1e-9);

        var inPlace = new XRect(0, 0, 10, 10);
        inPlace.Transform(matrix);
        inPlace.Should().Be(transformed);
    }

    [Fact]
    public void TwoRectanglesAreEqualWhenAllFourNumbersAre()
    {
        var rect = new XRect(1, 2, 3, 4);

        (rect == new XRect(1, 2, 3, 4)).Should().BeTrue();
        (rect != new XRect(1, 2, 3, 4)).Should().BeFalse();
        (rect != new XRect(1, 2, 3, 5)).Should().BeTrue();
        rect.Equals(new XRect(1, 2, 3, 4)).Should().BeTrue();
        rect.Equals((object)new XRect(1, 2, 3, 4)).Should().BeTrue();
        // ReSharper disable once SuspiciousTypeConversion.Global
        rect.Equals("not a rectangle").Should().BeFalse();
        XRect.Equals(rect, new XRect(1, 2, 3, 4)).Should().BeTrue();
        XRect.Equals(XRect.Empty, XRect.Empty).Should().BeTrue();
        XRect.Equals(XRect.Empty, rect).Should().BeFalse();
        rect.GetHashCode().Should().Be(new XRect(1, 2, 3, 4).GetHashCode());
    }

    [Fact]
    public void ARectangleIsWrittenAsFourNumbersAndReadBackTheSameWay()
    {
        var rect = new XRect(1.5, 2.5, 3.5, 4.5);

        var text = rect.ToString(CultureInfo.InvariantCulture);

        text.Should().Be("1.5,2.5,3.5,4.5");
        XRect.Parse(text).Should().Be(rect);
    }

    [Fact]
    public void TheEmptyRectangleIsWrittenByNameAndReadBackByName()
    {
        XRect.Empty.ToString(CultureInfo.InvariantCulture).Should().Be("Empty");
        XRect.Parse("Empty").Should().Be(XRect.Empty);
    }

    [Fact]
    public void AFormatStringIsAppliedToAllFourNumbers()
    {
        IFormattable rect = new XRect(1.23456, 2.34567, 3.45678, 4.56789);

        rect.ToString("0.0", CultureInfo.InvariantCulture).Should().Be("1.2,2.3,3.5,4.6");
    }

    /// <summary>
    ///   Deserialization writes the fields directly and never runs the constructors that refuse a
    ///   negative extent, so the struct checks again once it has been read. The fields are set here
    ///   by reflection, which is what a formatter does.
    /// </summary>
    [Fact]
    public void ADeserializedRectangleWithANegativeExtentIsRefused()
    {
        object rect = new XRect(1, 2, 3, 4);
        // ReSharper disable once PossibleNullReferenceException
        typeof(XRect).GetField("_width", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(rect, -1.0);

        var deserialized = () => ((IDeserializationCallback)rect).OnDeserialization(null);

        deserialized.Should().Throw<SerializationException>();
    }

    [Fact]
    public void ADeserializedRectangleThatAConstructorCouldHaveMadeIsAccepted()
    {
        foreach (var rect in new[] { new XRect(1, 2, 3, 4), new XRect(0, 0, 0, 0), XRect.Empty })
        {
            var deserialized = () => ((IDeserializationCallback)rect).OnDeserialization(null);

            deserialized.Should().NotThrow();
        }
    }
}
