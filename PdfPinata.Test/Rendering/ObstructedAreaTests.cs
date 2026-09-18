using AwesomeAssertions;
using PinataLayout.Rendering;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   An area with things standing in it, tested on its own with no renderer in sight.
/// </summary>
/// <remarks>
///   This is the whole of what makes text flow beside a shape: <c>GetFittingRect</c> is already
///   asked once per line, and every other thing the paragraph loop does is expressed in terms of
///   what it answers. Which makes it the piece where correctness is cheap to establish here and
///   expensive to debug through a rendered page.
///   <para>
///     The area is 100 wide and 100 tall at the origin throughout, so every number below can be
///     read against it without arithmetic.
///   </para>
/// </remarks>
public class ObstructedAreaTests
{
    static Area Bounds => AreaProbe.Rectangle(0, 0, 100, 100);

    // ----- one obstacle, from each direction ------------------------------------------------------

    [Fact]
    public void AnObstacleAtTheLeftLeavesTheRoomToItsRight()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 0, 30, 40));

        area.FittingRect(0, 10).Bounds().Should().Be((30, 0, 70, 10));
    }

    [Fact]
    public void AnObstacleAtTheRightLeavesTheRoomToItsLeft()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(70, 0, 30, 40));

        area.FittingRect(0, 10).Bounds().Should().Be((0, 0, 70, 10));
    }

    [Fact]
    public void AnObstacleInTheMiddleLeavesTheWiderOfTheTwoSides()
    {
        // 40 to the left of it, 25 to the right. One rectangle comes back, and it is the wider.
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(40, 0, 35, 40));

        area.FittingRect(0, 10).Bounds().Should().Be((0, 0, 40, 10));
    }

    [Fact]
    public void AnObstacleInTheMiddleLeavesTheWiderSideWhicheverSideThatIs()
    {
        // The mirror of the case above, so that "the wider" is not passing by choosing the left.
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(25, 0, 35, 40));

        area.FittingRect(0, 10).Bounds().Should().Be((60, 0, 40, 10));
    }

    [Fact]
    public void AnObstacleSpanningTheFullWidthLeavesNowhereToPutTheLine()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 0, 100, 40));

        // Not an error. The paragraph moves down past whatever is standing here.
        area.FittingRect(0, 10).Should().BeNull();
    }

    [Fact]
    public void AnObstacleWiderThanTheAreaAlsoLeavesNowhere()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(-20, 0, 200, 40));

        area.FittingRect(0, 10).Should().BeNull();
    }

    // ----- obstacles the band does not reach ------------------------------------------------------

    [Fact]
    public void AnObstacleAboveTheBandDoesNotNarrowIt()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 0, 30, 20));

        // The obstacle ends at 20; the band starts at 20.
        area.FittingRect(20, 10).Bounds().Should().Be((0, 20, 100, 10));
    }

    [Fact]
    public void AnObstacleBelowTheBandDoesNotNarrowIt()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 50, 30, 20));

        area.FittingRect(20, 10).Bounds().Should().Be((0, 20, 100, 10));
    }

    [Fact]
    public void AnObstacleOverlappingTheBandByAnyAmountNarrowsIt()
    {
        // Ends one point into the band. Overlap is by the band's box and not by a line within it:
        // a line whose baseline falls below an obstacle still has ascenders inside it.
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 0, 30, 21));

        area.FittingRect(20, 10).Bounds().Should().Be((30, 20, 70, 10));
    }

    [Fact]
    public void ATallObstacleNarrowsEveryBandItCrosses()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 10, 30, 50));

        area.FittingRect(0, 10).Bounds().Should().Be((0, 0, 100, 10), "above it");
        area.FittingRect(15, 10).Bounds().Should().Be((30, 15, 70, 10), "across it");
        area.FittingRect(45, 10).Bounds().Should().Be((30, 45, 70, 10), "still across it");
        area.FittingRect(60, 10).Bounds().Should().Be((0, 60, 100, 10), "below it");
    }

    // ----- more than one -------------------------------------------------------------------------

    [Fact]
    public void TwoObstaclesAtOppositeEdgesLeaveTheRoomBetweenThem()
    {
        var area = AreaProbe.Obstructed(Bounds,
            AreaProbe.Rectangle(0, 0, 20, 40),
            AreaProbe.Rectangle(80, 0, 20, 40));

        area.FittingRect(0, 10).Bounds().Should().Be((20, 0, 60, 10));
    }

    [Fact]
    public void TwoObstaclesLeaveTheWidestOfTheThreeGaps()
    {
        // Gaps of 20, 15 and 35. The last one wins.
        var area = AreaProbe.Obstructed(Bounds,
            AreaProbe.Rectangle(20, 0, 10, 40),
            AreaProbe.Rectangle(45, 0, 20, 40));

        area.FittingRect(0, 10).Bounds().Should().Be((65, 0, 35, 10));
    }

    [Fact]
    public void OverlappingObstaclesAreTreatedAsOne()
    {
        var area = AreaProbe.Obstructed(Bounds,
            AreaProbe.Rectangle(10, 0, 30, 40),
            AreaProbe.Rectangle(30, 0, 30, 40));

        // Together they block 10 to 60, leaving 40 to the right and 10 to the left.
        area.FittingRect(0, 10).Bounds().Should().Be((60, 0, 40, 10));
    }

    [Fact]
    public void ObstaclesGivenOutOfOrderGiveTheSameAnswer()
    {
        var forwards = AreaProbe.Obstructed(Bounds,
            AreaProbe.Rectangle(20, 0, 10, 40),
            AreaProbe.Rectangle(45, 0, 20, 40));

        var backwards = AreaProbe.Obstructed(Bounds,
            AreaProbe.Rectangle(45, 0, 20, 40),
            AreaProbe.Rectangle(20, 0, 10, 40));

        backwards.FittingRect(0, 10).Bounds().Should().Be(forwards.FittingRect(0, 10).Bounds());
    }

    [Fact]
    public void OnlyTheObstaclesInTheBandCount()
    {
        var area = AreaProbe.Obstructed(Bounds,
            AreaProbe.Rectangle(0, 0, 40, 20),
            AreaProbe.Rectangle(60, 50, 40, 20));

        area.FittingRect(0, 10).Bounds().Should().Be((40, 0, 60, 10), "only the first is in this band");
        area.FittingRect(52, 10).Bounds().Should().Be((0, 52, 60, 10), "only the second is in this one");
        area.FittingRect(30, 10).Bounds().Should().Be((0, 30, 100, 10), "neither is in this one");
    }

    // ----- no obstacles at all --------------------------------------------------------------------

    [Fact]
    public void AnAreaWithNothingInItAnswersExactlyAsARectangleDoes()
    {
        var obstructed = AreaProbe.Obstructed(Bounds);
        var plain = Bounds;

        obstructed.FittingRect(20, 10).Bounds().Should().Be(plain.FittingRect(20, 10).Bounds());
    }

    [Fact]
    public void ABandOffTheBottomHasNowhereToGoWhateverStandsInTheArea()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 0, 30, 40));

        // The same answer a rectangle gives, for the same reason: past the bottom is past the
        // bottom whether anything is standing in the way or not.
        area.FittingRect(95, 10).Should().BeNull();
        Bounds.FittingRect(95, 10).Should().BeNull();
    }

    // ----- the parts kept deliberately simple -----------------------------------------------------

    [Fact]
    public void UnitingWithAnObstructedAreaGivesAPlainRectangleCoveringBoth()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 0, 30, 40));

        var united = area.UnitedWith(AreaProbe.Rectangle(50, 50, 100, 100));

        // Deliberate: Unite takes bounding boxes already, and every caller uses it for render-info
        // geometry where a bounding box is what is wanted. An area with holes has no union that is
        // also an area with holes, short of something no caller would read correctly.
        united.IsObstructed().Should().BeFalse();
        united.Bounds().Should().Be((0, 0, 150, 150));
    }

    [Fact]
    public void LoweringKeepsTheObstaclesWhereTheyStand()
    {
        var area = AreaProbe.Obstructed(Bounds, AreaProbe.Rectangle(0, 30, 30, 40));

        var lowered = area.Lowered(20);

        lowered.Bounds().Should().Be((0, 20, 100, 80));
        lowered.IsObstructed().Should().BeTrue();

        // The obstacle is in page coordinates and did not move with the area's top.
        lowered.FittingRect(35, 10).Bounds().Should().Be((30, 35, 70, 10));
        lowered.FittingRect(75, 10).Bounds().Should().Be((0, 75, 100, 10));
    }

    [Fact]
    public void AnAreaWithNoWidthAndNothingInItStillAnswersWithItself()
    {
        // The band is clear, so this answers with the area however narrow the area is. The scan the
        // obstructed path shares with XTextFormatter would answer null here, because it judges a
        // run against the tolerance and this one is narrower than that - which is why the clear
        // case is settled before the scan is reached rather than folded into it.
        var area = AreaProbe.Obstructed(AreaProbe.Rectangle(0, 0, 0, 100));

        area.FittingRect(0, 10).Bounds().Should().Be((0, 0, 0, 10));
    }

    [Fact]
    public void ChangingTheListAfterwardsCannotMoveAnObstacle()
    {
        var obstacle = AreaProbe.Rectangle(0, 0, 30, 40);
        var area = AreaProbe.Obstructed(Bounds, obstacle);

        obstacle.X = 500;
        obstacle.Width = 1;

        // The obstacles are copied in. Text laid out around one cannot have it moved out from
        // under it by a caller still holding the rectangle.
        area.FittingRect(0, 10).Bounds().Should().Be((30, 0, 70, 10));
    }
}
