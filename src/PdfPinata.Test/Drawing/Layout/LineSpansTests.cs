using System;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Drawing.Layout;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   The widest run of a line that nothing stands in.
/// </summary>
/// <remarks>
///   Tested directly as well as through <c>ObstructedAreaTests</c>, which reaches the same
///   arithmetic through PinataLayout's area. Two reasons it earns its own tests: it is public API, and
///   an area cannot produce some of the inputs it has to accept — an obstacle hanging outside the
///   line, obstacles given in any order, and a caller passing nothing at all.
///   <para>
///     The line runs from 0 to 100 throughout, so every number below reads against it without
///     arithmetic.
///   </para>
/// </remarks>
public class LineSpansTests
{
    private const double Left = 0;
    private const double Right = 100;
    private const double Tolerance = 0.001;

    private static (bool Found, double Start, double Width) WidestFree(params (double Start, double End)[] blocked)
    {
        var found = LineSpans.TryWidestFree(Left, Right, [..blocked], Tolerance,
            out var start, out var width);
        return (found, start, width);
    }

    // ----- one obstacle, from each direction ------------------------------------------------------

    [Fact]
    public void AnUnobstructedLineIsFreeEndToEnd()
    {
        WidestFree().Should().Be((true, 0d, 100d));
    }

    [Fact]
    public void AnObstacleAtTheLeftLeavesTheRoomToItsRight()
    {
        WidestFree((0, 30)).Should().Be((true, 30d, 70d));
    }

    [Fact]
    public void AnObstacleAtTheRightLeavesTheRoomToItsLeft()
    {
        WidestFree((70, 100)).Should().Be((true, 0d, 70d));
    }

    [Fact]
    public void AnObstacleInTheMiddleLeavesTheWiderSide()
    {
        // Free runs of 30 and 40. The wider wins and the other is left empty, which is the decision
        // both engines take rather than a limitation of the scan.
        WidestFree((30, 60)).Should().Be((true, 60d, 40d));
    }

    [Fact]
    public void AnObstacleSpanningTheWholeLineLeavesNothing()
    {
        WidestFree((0, 100)).Found.Should().BeFalse();
    }

    // ----- obstacles the line does not contain ----------------------------------------------------

    [Fact]
    public void AnObstacleHangingOffTheLeftIsCountedOnlyWhereItOverlaps()
    {
        WidestFree((-40, 25)).Should().Be((true, 25d, 75d));
    }

    [Fact]
    public void AnObstacleHangingOffTheRightIsCountedOnlyWhereItOverlaps()
    {
        WidestFree((80, 250)).Should().Be((true, 0d, 80d));
    }

    [Fact]
    public void AnObstacleEntirelyClearOfTheLineTakesNothingFromIt()
    {
        WidestFree((150, 200)).Should().Be((true, 0d, 100d));
    }

    // ----- more than one --------------------------------------------------------------------------

    [Fact]
    public void TwoObstaclesLeaveTheWidestOfTheThreeRuns()
    {
        // Runs of 20, 25 and 30.
        WidestFree((20, 45), (70, 100)).Should().Be((true, 45d, 25d));
    }

    [Fact]
    public void ObstaclesAreConsideredWhateverOrderTheyArriveIn()
    {
        var inOrder = WidestFree((10, 20), (40, 50), (80, 90));
        var jumbled = WidestFree((80, 90), (10, 20), (40, 50));

        jumbled.Should().Be(inOrder);
    }

    [Fact]
    public void OverlappingObstaclesCountAsOne()
    {
        WidestFree((20, 60), (40, 70)).Should().Be((true, 70d, 30d));
    }

    [Fact]
    public void AnObstacleSwallowedByAWiderOneTakesNothingExtra()
    {
        // Sorted by where they start, so the wide one comes first and the narrow one sits inside it.
        // The cursor has to keep the furthest right edge it has seen rather than the last one.
        WidestFree((10, 80), (20, 30)).Should().Be((true, 80d, 20d));
    }

    [Fact]
    public void ObstaclesCoveringTheLineBetweenThemLeaveNothing()
    {
        WidestFree((0, 60), (55, 100)).Found.Should().BeFalse();
    }

    // ----- the tolerance --------------------------------------------------------------------------

    [Fact]
    public void ARunNarrowerThanTheToleranceIsNoRoomAtAll()
    {
        WidestFree((0, 50), (50.0005, 100)).Found.Should().BeFalse();
    }

    [Fact]
    public void ARunWiderThanTheToleranceIsRoom()
    {
        var free = WidestFree((0, 50), (50.01, 100));

        free.Found.Should().BeTrue();
        free.Width.Should().BeApproximately(0.01, 1e-9);
    }

    [Fact]
    public void ALineOfNoWidthHasNoRoom()
    {
        LineSpans.TryWidestFree(50, 50, [(0, 10)], Tolerance,
                out _, out _)
            .Should().BeFalse();
    }

    // ----- the edges ------------------------------------------------------------------------------

    [Fact]
    public void ATieGoesToTheRunFurthestLeft()
    {
        // Two free runs of exactly 40. The comparison is strictly greater than, so the first found
        // keeps it — and the first found is the leftmost, because the spans are sorted.
        WidestFree((40, 60)).Should().Be((true, 0d, 40d));
    }

    [Fact]
    public void AnObstacleTouchingAnotherEndToEndLeavesNoRunBetweenThem()
    {
        WidestFree((10, 40), (40, 70)).Should().Be((true, 70d, 30d));
    }

    [Fact]
    public void TheSpansGivenAreLeftAsTheyWere()
    {
        // This used to sort the caller's list in place, which was worth an allocation when the scan
        // was hand-rolled here and needed the order. The scan is IntervalSet's now and orders its
        // own copy, so the argument is no longer touched at all.
        var blocked = new List<(double Start, double End)> { (80, 90), (10, 20) };

        LineSpans.TryWidestFree(Left, Right, blocked, Tolerance, out _, out _);

        blocked.Should().Equal((80, 90), (10, 20));
    }

    [Fact]
    public void ASpanGivenEndFirstIsReadTheWayRoundItWasMeant()
    {
        WidestFree((30, 0)).Should().Be((true, 30d, 70d));
    }

    // ----- what the arguments have to be ----------------------------------------------------------

    [Fact]
    public void AMissingListOfSpansIsRefused()
    {
        // No list at all, which is a caller mistake. An *empty* list is not: it says the line has
        // nothing standing in it, and AnUnobstructedLineIsFreeEndToEnd is that case.
        var scan = () => LineSpans.TryWidestFree(Left, Right, null, Tolerance, out _, out _);

        scan.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ALineEndingLeftOfWhereItStartsIsRefused()
    {
        var scan = () => LineSpans.TryWidestFree(Right, Left, [],
            Tolerance, out _, out _);

        // A line of no width is allowed and answers "no room" - see ALineOfNoWidthHasNoRoom - but
        // one of negative width is a mistake rather than an answer.
        scan.Should().Throw<ArgumentOutOfRangeException>();
    }

    public static TheoryData<double> NotRealNumbers => [
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity
    ];

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void ALineThatDoesNotRunBetweenRealCoordinatesIsRefused(double value)
    {
        // Named here rather than left to the interval the line becomes, so the message points at
        // the argument the caller passed. NaN reaches this at all because it passes the ordering
        // test above: every comparison against NaN is false.
        var fromLeft = () => LineSpans.TryWidestFree(value, Right, [],
            Tolerance, out _, out _);
        var fromRight = () => LineSpans.TryWidestFree(Left, value, [],
            Tolerance, out _, out _);

        fromLeft.Should().Throw<ArgumentOutOfRangeException>();
        fromRight.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void AToleranceThatIsNotARealWidthIsRefused(double value)
    {
        var scan = () => LineSpans.TryWidestFree(Left, Right, [],
            value, out _, out _);

        scan.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ANegativeToleranceIsRefused()
    {
        var scan = () => LineSpans.TryWidestFree(Left, Right, [],
            -1, out _, out _);

        // The test at the end is "wider than the tolerance", so a negative one would let a run of
        // no width count as room - the opposite of what a tolerance is for.
        scan.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AToleranceOfNothingAllowsAnyWidthAtAll()
    {
        var found = LineSpans.TryWidestFree(Left, Right, [],
            0, out _, out var width);

        found.Should().BeTrue();
        width.Should().Be(Right - Left);
    }
}
