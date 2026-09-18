using System;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing.Layout;
using Xunit;

namespace PdfPinata.Test.Drawing.Layout;

/// <summary>
///   The pieces text flow is expressed in: a run, the slice of a page a line occupies, and a set of
///   runs with things taken out of it.
/// </summary>
/// <remarks>
///   No formatter and no page anywhere in here. This is the layer where correctness is cheap to
///   establish and expensive to debug through a rendered page, which is the whole reason it is a
///   layer.
/// </remarks>
public class FlowGeometryTests
{
    const double Tolerance = 0.001;

    static (double Start, double End)[] Runs(IntervalSet set)
    {
        return set.Select(run => (run.Start, run.End)).ToArray();
    }

    // ----- a run ----------------------------------------------------------------------------------

    [Fact]
    public void ARunKnowsHowFarItRuns()
    {
        var run = new XInterval(20, 50);

        run.Start.Should().Be(20);
        run.End.Should().Be(50);
        run.Width.Should().Be(30);
        run.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ARunOfNoWidthIsEmptyButIsNotAnError()
    {
        // It is an ordinary measurement - two obstacles meeting exactly leave one of these - and
        // saying so is different from refusing to describe it.
        new XInterval(20, 20).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ARunEndingBeforeItStartsIsRefused()
    {
        var build = () => new XInterval(50, 20);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 10, 10, 20, false)]   // touching end to end
    [InlineData(0, 10, 9, 20, true)]
    [InlineData(0, 10, 20, 30, false)]
    [InlineData(0, 100, 20, 30, true)]   // one inside the other
    public void RunsOverlapOnlyWhereTheyShareWidth(double aStart, double aEnd,
        double bStart, double bEnd, bool expected)
    {
        new XInterval(aStart, aEnd).Overlaps(new XInterval(bStart, bEnd)).Should().Be(expected);
    }

    [Fact]
    public void TheOverlapOfTwoRunsIsTheirCommonPart()
    {
        new XInterval(0, 50).Intersect(new XInterval(30, 80)).Should().Be(new XInterval(30, 50));
    }

    [Fact]
    public void TheOverlapOfRunsThatDoNotMeetIsEmpty()
    {
        new XInterval(0, 20).Intersect(new XInterval(50, 80)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TwoRunsOfTheSameExtentAreTheSameRun()
    {
        (new XInterval(10, 20) == new XInterval(10, 20)).Should().BeTrue();
        (new XInterval(10, 20) != new XInterval(10, 21)).Should().BeTrue();
    }

    // ----- the slice a line occupies --------------------------------------------------------------

    [Fact]
    public void ABandKnowsHowDeepItIs()
    {
        var band = new FlowBand(100, 112);

        band.Top.Should().Be(100);
        band.Bottom.Should().Be(112);
        band.Height.Should().Be(12);
    }

    [Fact]
    public void ABandEndingAboveWhereItStartsIsRefused()
    {
        // y runs down the page here as it does everywhere else in layout.
        var build = () => new FlowBand(112, 100);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, 50, false)]      // wholly above
    [InlineData(0, 100, false)]     // foot exactly level with the band's top
    [InlineData(0, 101, true)]      // one point of ascender inside it
    [InlineData(111, 200, true)]    // one point of descender inside it
    [InlineData(112, 200, false)]   // head exactly level with the band's bottom
    [InlineData(150, 200, false)]   // wholly below
    [InlineData(102, 105, true)]    // wholly inside
    public void ABandIsOverlappedByAnythingStandingInsideIt(double top, double bottom, bool expected)
    {
        // Touching counts for nothing, both ways, so obstacles stacked end to end never both claim
        // one band.
        new FlowBand(100, 112).Overlaps(top, bottom).Should().Be(expected);
    }

    // ----- a set of runs --------------------------------------------------------------------------

    [Fact]
    public void ASetPutsItsRunsInOrder()
    {
        var set = IntervalSet.Of(new XInterval(60, 80), new XInterval(0, 20));

        Runs(set).Should().Equal((0d, 20d), (60d, 80d));
    }

    [Fact]
    public void ASetMergesRunsThatOverlap()
    {
        var set = IntervalSet.Of(new XInterval(0, 40), new XInterval(20, 70));

        Runs(set).Should().Equal((0d, 70d));
    }

    [Fact]
    public void ASetMergesRunsThatMeetEndToEnd()
    {
        // Two runs touching are one run. Left apart they would offer a caller two narrow spans
        // where there is one wide one, and the widest-run rule would then pick a shorter line than
        // the room allows.
        var set = IntervalSet.Of(new XInterval(0, 40), new XInterval(40, 70));

        Runs(set).Should().Equal((0d, 70d));
    }

    [Fact]
    public void ASetDropsRunsCoveringNothing()
    {
        var set = IntervalSet.Of(new XInterval(10, 10), new XInterval(20, 30));

        Runs(set).Should().Equal((20d, 30d));
    }

    [Fact]
    public void ARunSwallowedByAWiderOneAddsNothing()
    {
        var set = IntervalSet.Of(new XInterval(0, 100), new XInterval(20, 30));

        Runs(set).Should().Equal((0d, 100d));
    }

    // ----- taking things out of a set -------------------------------------------------------------

    [Fact]
    public void TakingSomethingFromTheMiddleLeavesTwoRuns()
    {
        // The property the whole abstraction exists for: the geometry answers honestly that there
        // are two, and what to do about that is the layout loop's business.
        var left = IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(40, 60) });

        Runs(left).Should().Equal((0d, 40d), (60d, 100d));
    }

    [Fact]
    public void TakingSomethingFromTheLeftLeavesTheRest()
    {
        Runs(IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(0, 30) }))
            .Should().Equal((30d, 100d));
    }

    [Fact]
    public void TakingSomethingFromTheRightLeavesTheRest()
    {
        Runs(IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(70, 100) }))
            .Should().Equal((0d, 70d));
    }

    [Fact]
    public void TakingTheWholeThingLeavesNothing()
    {
        IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(0, 100) }).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TakingSomethingThatHangsOffTheEndTakesOnlyWhatOverlaps()
    {
        Runs(IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(-40, 25) }))
            .Should().Equal((25d, 100d));

        Runs(IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(80, 250) }))
            .Should().Equal((0d, 80d));
    }

    [Fact]
    public void TakingSomethingEntirelyOutsideTakesNothing()
    {
        Runs(IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(150, 200) }))
            .Should().Equal((0d, 100d));
    }

    [Fact]
    public void ThingsTakenOutMayArriveInAnyOrderAndMayOverlapEachOther()
    {
        var jumbled = IntervalSet.Of(0, 100)
            .Subtract(new[] { new XInterval(70, 90), new XInterval(20, 40), new XInterval(30, 50) });

        Runs(jumbled).Should().Equal((0d, 20d), (50d, 70d), (90d, 100d));
    }

    [Fact]
    public void TakingSomethingFromASetOfSeveralRunsCarvesEachOfThem()
    {
        var set = IntervalSet.Of(new XInterval(0, 40), new XInterval(60, 100));

        Runs(set.Subtract(new[] { new XInterval(20, 80) }))
            .Should().Equal((0d, 20d), (80d, 100d));
    }

    [Fact]
    public void TakingNothingLeavesTheSetAsItWas()
    {
        var set = IntervalSet.Of(0, 100);

        set.Subtract(new XInterval[0]).Should().BeSameAs(set);
    }

    [Fact]
    public void ASetIsNotChangedByWhatIsTakenFromIt()
    {
        // Immutable: a line's room is worked out once and read by several things after that.
        var set = IntervalSet.Of(0, 100);

        set.Subtract(new[] { new XInterval(40, 60) });

        Runs(set).Should().Equal((0d, 100d));
    }

    // ----- picking a run to lay a line in ---------------------------------------------------------

    [Fact]
    public void TheWidestRunIsTheOneOffered()
    {
        var left = IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(30, 60) });

        left.TryWidest(Tolerance, out XInterval widest).Should().BeTrue();
        widest.Should().Be(new XInterval(60, 100));
    }

    [Fact]
    public void ATieGoesToTheRunFurthestLeft()
    {
        var left = IntervalSet.Of(0, 100).Subtract(new[] { new XInterval(40, 60) });

        left.TryWidest(Tolerance, out XInterval widest).Should().BeTrue();
        widest.Should().Be(new XInterval(0, 40));
    }

    [Fact]
    public void AnEmptySetOffersNothing()
    {
        IntervalSet.Empty.TryWidest(Tolerance, out _).Should().BeFalse();
    }

    [Fact]
    public void ARunNarrowerThanTheToleranceIsNoRoomAtAll()
    {
        var slivers = IntervalSet.Of(0, 100)
            .Subtract(new[] { new XInterval(0, 50), new XInterval(50.0005, 100) });

        slivers.IsEmpty.Should().BeFalse("the run is there");
        slivers.TryWidest(Tolerance, out _).Should().BeFalse("but it is not room");
    }

    [Fact]
    public void ANegativeToleranceIsRefused()
    {
        var pick = () => IntervalSet.Of(0, 100).TryWidest(-1, out _);

        pick.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ----- coordinates that are not numbers -------------------------------------------------------

    /// <summary>
    ///   Every comparison against <c>NaN</c> is false, so it walks through an ordering test
    ///   untouched and does its damage later — which is why these are refused where they enter
    ///   rather than where they are noticed.
    /// </summary>
    public static TheoryData<double> NotRealNumbers => new TheoryData<double>
    {
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity,
    };

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void ARunThatDoesNotStartAtARealCoordinateIsRefused(double value)
    {
        var build = () => new XInterval(value, 100);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void ARunThatDoesNotEndAtARealCoordinateIsRefused(double value)
    {
        var build = () => new XInterval(0, value);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void ABandThatDoesNotStartAtARealDepthIsRefused(double value)
    {
        var build = () => new FlowBand(value, 100);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void ABandThatDoesNotEndAtARealDepthIsRefused(double value)
    {
        var build = () => new FlowBand(0, value);

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [MemberData(nameof(NotRealNumbers))]
    public void AToleranceThatIsNotARealWidthIsRefused(double value)
    {
        // NaN in particular is not merely useless here: it seeds the running widest, and nothing
        // compares less than or equal to it, so the first run examined would be taken however
        // narrow it was and the tolerance would stop applying at all.
        var pick = () => IntervalSet.Of(0, 100).TryWidest(value, out _);

        pick.Should().Throw<ArgumentOutOfRangeException>();
    }
}
