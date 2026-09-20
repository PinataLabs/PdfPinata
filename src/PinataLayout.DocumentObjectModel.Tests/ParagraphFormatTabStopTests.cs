using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   The tab stops a <see cref="ParagraphFormat"/> carries, which it offers twice over: once on
///   the <see cref="TabStops"/> collection itself and once as four <c>AddTabStop</c> overloads and
///   a <c>RemoveTabStop</c> that forward to it. The forwarding methods are the ones a caller
///   actually writes, and each one has to reach the right overload underneath.
///   <para>
///   <see cref="ParagraphFormat.HasTabStops"/> is the reason the collection is not simply created
///   on first use: asking whether a format has any must not be what gives it some.
///   </para>
/// </summary>
public class ParagraphFormatTabStopTests
{
    static ParagraphFormat AFormat() => new Document().AddSection().AddParagraph("x").Format;

    [Fact]
    public void AFormatHasNoTabStopsUntilOneIsAskedFor()
    {
        var format = AFormat();

        format.HasTabStops.Should().BeFalse();

        format.AddTabStop("2cm");

        format.HasTabStops.Should().BeTrue();
    }

    [Fact]
    public void ATabStopIsAddedAtAPositionAlone()
    {
        var format = AFormat();

        var stop = format.AddTabStop("2cm");

        stop.Position.Centimeter.Should().BeApproximately(2, 1e-4);
        format.TabStops.Count.Should().Be(1);
    }

    [Fact]
    public void ATabStopIsAddedWithAnAlignment()
    {
        var format = AFormat();

        var stop = format.AddTabStop("3cm", TabAlignment.Right);

        stop.Alignment.Should().Be(TabAlignment.Right);
    }

    [Fact]
    public void ATabStopIsAddedWithALeader()
    {
        var format = AFormat();

        var stop = format.AddTabStop("4cm", TabLeader.Dots);

        stop.Leader.Should().Be(TabLeader.Dots);
    }

    [Fact]
    public void ATabStopIsAddedWithBothAnAlignmentAndALeader()
    {
        var format = AFormat();

        var stop = format.AddTabStop("5cm", TabAlignment.Decimal, TabLeader.Lines);

        stop.Alignment.Should().Be(TabAlignment.Decimal);
        stop.Leader.Should().Be(TabLeader.Lines);
    }

    [Fact]
    public void ATabStopBuiltElsewhereCanBeHandedOver()
    {
        var format = AFormat();
        var stop = new TabStop("6cm");

        format.Add(stop);

        format.TabStops.Count.Should().Be(1);
        format.TabStops[0].Position.Centimeter.Should().BeApproximately(6, 1e-4);
    }

    /// <summary>
    ///   Removing is not deleting. A tab stop taken out of a format is kept in the collection and
    ///   marked as not a tab, because the format may be a style that another style inherits from —
    ///   and a stop that simply vanished would let the base style's own stop come back. The
    ///   serialized DDL is where the difference shows: the removed position is written as a
    ///   <c>TabStops -=</c> line rather than dropped.
    /// </summary>
    [Fact]
    public void ARemovedTabStopStaysInTheCollectionAndIsWrittenOutAsRemoved()
    {
        var document = new Document();
        var format = document.AddSection().AddParagraph("x").Format;
        format.AddTabStop("2cm");
        format.AddTabStop("4cm");

        format.RemoveTabStop("2cm");

        format.TabStops.Count.Should().Be(2);
        DdlWriter.WriteToString(document).Should().Contain("TabStops -= \"2cm\"");
    }

    [Fact]
    public void ClearAllLeavesAMarkerRatherThanNoTabStopsAtAll()
    {
        var format = AFormat();
        format.AddTabStop("2cm");

        format.ClearAll();

        // ClearAll is how a derived style says "none", which is not the same as saying nothing:
        // the collection stays, so the style it inherits from cannot put its own stops back.
        format.HasTabStops.Should().BeTrue();
        format.TabStops.TabsCleared.Should().BeTrue();
    }

    // ----- the composite properties a caller may assign outright ----------------------------------

    [Fact]
    public void EachCompositePartOfAFormatCanBeAssignedWholesale()
    {
        var format = AFormat();
        var other = AFormat();
        other.Font.Name = "Arial";
        other.Borders.Width = "1pt";
        other.Shading.Color = Colors.LightGray;
        other.AddTabStop("7cm");

        format.Font = other.Font.Clone();
        format.Borders = other.Borders.Clone();
        format.Shading = other.Shading.Clone();
        format.TabStops = other.TabStops.Clone();

        format.Font.Name.Should().Be("Arial");
        format.Borders.Width.Point.Should().BeApproximately(1, 1e-4);
        format.Shading.Color.Should().Be(Colors.LightGray);
        format.TabStops.Count.Should().Be(1);
    }

    [Fact]
    public void TabStopsAreWrittenOutWithTheFormatTheyBelongTo()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph("x");
        paragraph.Format.AddTabStop("2cm", TabAlignment.Right, TabLeader.Dots);

        var ddl = DdlWriter.WriteToString(document);

        ddl.Should().Contain("TabStops");
        ddl.Should().Contain("Dots");
    }
}
