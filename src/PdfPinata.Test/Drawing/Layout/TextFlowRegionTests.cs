using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   A block of text with things standing in it, asked what room a line has.
/// </summary>
/// <remarks>
///   The block runs from 0 to 100 across and 0 to 200 down throughout, and the band asked about is
///   the one from 100 to 112 unless a test says otherwise — so every number below reads against
///   those without arithmetic.
/// </remarks>
public class TextFlowRegionTests
{
    private const double Tolerance = 0.001;

    private static readonly XRect Block = new(0, 0, 100, 200);
    private static readonly FlowBand Band = new(100, 112);

    private static (double Start, double End)[] RoomIn(TextFlowRegion region, FlowBand? band = null)
    {
        return region.GetAvailableIntervals(band ?? Band)
            .Select(run => (run.Start, run.End))
            .ToArray();
    }

    private static TextFlowRegion BlockWith(params IFlowObstacle[] obstacles)
    {
        var region = new TextFlowRegion(Block);
        foreach (var obstacle in obstacles)
            region.With(obstacle);
        return region;
    }

    /// <summary>A rectangle standing across the band the tests ask about.</summary>
    private static RectangleObstacle Standing(double x, double width, double padding = 0)
    {
        return new RectangleObstacle(new XRect(x, 90, width, 40), padding);
    }

    // ----- nothing in the way ---------------------------------------------------------------------

    [Fact]
    public void AnEmptyBlockIsFreeEndToEnd()
    {
        RoomIn(BlockWith()).Should().Equal((0d, 100d));
    }

    [Fact]
    public void AnObstacleTheBandDoesNotReachTakesNothing()
    {
        // Above the band and below it. The obstacle exists, it is simply somewhere else.
        RoomIn(BlockWith(new RectangleObstacle(new XRect(0, 0, 60, 50)))).Should().Equal((0d, 100d));
        RoomIn(BlockWith(new RectangleObstacle(new XRect(0, 150, 60, 50)))).Should().Equal((0d, 100d));
    }

    [Fact]
    public void AnObstacleWhoseFootIsLevelWithTheBandTopIsAboveIt()
    {
        // Touching counts for nothing, so obstacles stacked end to end never both claim one band.
        RoomIn(BlockWith(new RectangleObstacle(new XRect(0, 0, 60, 100)))).Should().Equal((0d, 100d));
    }

    [Fact]
    public void AnObstacleReachingOnePointIntoTheBandTakesItsRoom()
    {
        // The ascender rule: the band is the line's box, so a line whose baseline clears an obstacle
        // can still have the top of a letter inside it.
        RoomIn(BlockWith(new RectangleObstacle(new XRect(0, 0, 60, 101)))).Should().Equal((60d, 100d));
    }

    // ----- one obstacle, from each direction ------------------------------------------------------

    [Fact]
    public void AnObstacleAtTheLeftLeavesTheRoomToItsRight()
    {
        RoomIn(BlockWith(Standing(0, 30))).Should().Equal((30d, 100d));
    }

    [Fact]
    public void AnObstacleAtTheRightLeavesTheRoomToItsLeft()
    {
        RoomIn(BlockWith(Standing(70, 30))).Should().Equal((0d, 70d));
    }

    [Fact]
    public void AnObstacleInTheMiddleLeavesARunEitherSide()
    {
        // Two runs, honestly reported. Which one a line goes in is the layout loop's decision and
        // not this layer's.
        RoomIn(BlockWith(Standing(30, 30))).Should().Equal((0d, 30d), (60d, 100d));
    }

    [Fact]
    public void AnObstacleSpanningTheBlockLeavesNothing()
    {
        var room = BlockWith(Standing(0, 100)).GetAvailableIntervals(Band);

        room.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AnObstacleWiderThanTheBlockLeavesNothing()
    {
        BlockWith(Standing(-50, 400)).Should().Match<TextFlowRegion>(
            region => region.GetAvailableIntervals(Band).IsEmpty);
    }

    // ----- more than one --------------------------------------------------------------------------

    [Fact]
    public void TwoObstaclesLeaveTheRunsBetweenAndBesideThem()
    {
        RoomIn(BlockWith(Standing(20, 20), Standing(60, 15)))
            .Should().Equal((0d, 20d), (40d, 60d), (75d, 100d));
    }

    [Fact]
    public void TwoObstaclesThatOverlapCountAsOne()
    {
        RoomIn(BlockWith(Standing(20, 30), Standing(40, 30))).Should().Equal((0d, 20d), (70d, 100d));
    }

    [Fact]
    public void TwoObstaclesCoveringTheBlockBetweenThemLeaveNothing()
    {
        BlockWith(Standing(0, 60), Standing(55, 45))
            .GetAvailableIntervals(Band).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ObstaclesMayBeAddedInAnyOrder()
    {
        var oneWay = RoomIn(BlockWith(Standing(20, 20), Standing(60, 15)));
        var theOther = RoomIn(BlockWith(Standing(60, 15), Standing(20, 20)));

        theOther.Should().Equal(oneWay);
    }

    // ----- the padding ----------------------------------------------------------------------------

    [Fact]
    public void PaddingHoldsTheTextOffHorizontally()
    {
        RoomIn(BlockWith(Standing(20, 30, padding: 5))).Should().Equal((0d, 15d), (55d, 100d));
    }

    [Fact]
    public void PaddingHoldsTheTextOffVerticallyToo()
    {
        // The obstacle's foot is exactly level with the band's top, so without padding the band is
        // clear. With it, the line is pushed past instead of clearing the obstacle by a hair.
        var justAbove = new RectangleObstacle(new XRect(0, 0, 60, 100), padding: 4);

        RoomIn(BlockWith(justAbove)).Should().Equal((60d + 4, 100d));
    }

    [Fact]
    public void PaddingIsPartOfTheRoomReserved()
    {
        var obstacle = new RectangleObstacle(new XRect(20, 90, 30, 40), padding: 5);

        obstacle.Bounds.Should().Be(new XRect(20, 90, 30, 40));
        obstacle.Padding.Should().Be(5);
        obstacle.Reserved.Should().Be(new XRect(15, 85, 40, 50));
    }

    [Fact]
    public void NegativePaddingIsRefused()
    {
        // It would shrink the obstacle and let text run over the thing it was given to avoid. A
        // caller wanting the text closer passes a smaller rectangle.
        var build = () => new RectangleObstacle(new XRect(0, 0, 10, 10), -1);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ----- what a line does with the answer -------------------------------------------------------

    [Fact]
    public void TheWiderSideOfAnObstacleIsTheOneOffered()
    {
        var room = BlockWith(Standing(20, 30)).GetAvailableIntervals(Band);

        room.Should().HaveCount(2, "both sides are reported");
        room.TryWidest(Tolerance, out var widest).Should().BeTrue();
        widest.Should().Be(new XInterval(50, 100), "and the roomier one is chosen");
    }

    [Fact]
    public void ABandWithNothingLeftInItOffersNoRun()
    {
        BlockWith(Standing(0, 100)).GetAvailableIntervals(Band)
            .TryWidest(Tolerance, out _).Should().BeFalse();
    }

    // ----- the edges ------------------------------------------------------------------------------

    [Fact]
    public void ABandBelowTheBlockIsStillAnsweredHorizontally()
    {
        // Deliberate. An empty answer has to mean "something is standing here, move down past it",
        // because that is what the caller does about it. Running out of block is the caller's own
        // business, and it already knows how to tell.
        RoomIn(BlockWith(), new FlowBand(500, 512)).Should().Equal((0d, 100d));
    }

    [Fact]
    public void AnObstacleOfNoWidthTakesNothing()
    {
        RoomIn(BlockWith(Standing(40, 0))).Should().Equal((0d, 100d));
    }

    [Fact]
    public void AddingNothingAsAnObstacleIsRefused()
    {
        var add = () => new TextFlowRegion(Block).With(null);

        add.Should().Throw<ArgumentNullException>();
    }
}
