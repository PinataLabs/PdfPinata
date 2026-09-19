using System;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel.IO;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   <see cref="Borders"/> on its own terms: the six borders it holds, the defaults it gives them,
///   the one setter that writes four properties at once, and the "cleared" state that is neither
///   null nor set.
/// </summary>
public class BordersTests
{
    static Document RoundTrip(Document document) =>
        DdlReader.DocumentFromString(DdlWriter.WriteToString(document));

    [Fact]
    public void ABorderExistsOnlyOnceItHasBeenGivenSomething()
    {
        var borders = new Borders();
        borders.HasBorder(BorderType.Top).Should().BeFalse();

        borders.Top.Width = 1;

        borders.HasBorder(BorderType.Top).Should().BeTrue();
        borders.HasBorder(BorderType.Bottom).Should().BeFalse();
    }

    [Fact]
    public void AskingAboutABorderTypeThatDoesNotExistIsRefused()
    {
        var ask = () => new Borders().HasBorder((BorderType)99);

        ask.Should().Throw<ArgumentException>().WithParameterName("type");
    }

    [Fact]
    public void EachOfTheSixBordersIsMadeOnFirstReadAndKeptAfterwards()
    {
        var borders = new Borders();

        Border[] first = [borders.Top, borders.Left, borders.Bottom, borders.Right, borders.DiagonalUp, borders.DiagonalDown];
        Border[] second = [borders.Top, borders.Left, borders.Bottom, borders.Right, borders.DiagonalUp, borders.DiagonalDown];

        first.Should().OnlyHaveUniqueItems().And.Equal(second);
    }

    [Fact]
    public void AnAssignedBorderIsTheOneKept()
    {
        var borders = new Borders();
        var top = new Border();
        var left = new Border();
        var bottom = new Border();
        var right = new Border();
        var up = new Border();
        var down = new Border();

        borders.Top = top;
        borders.Left = left;
        borders.Bottom = bottom;
        borders.Right = right;
        borders.DiagonalUp = up;
        borders.DiagonalDown = down;

        new[] { borders.Top, borders.Left, borders.Bottom, borders.Right, borders.DiagonalUp, borders.DiagonalDown }
            .Should().Equal(top, left, bottom, right, up, down);
    }

    [Fact]
    public void EnumeratingBordersVisitsAllSixSlotsWhetherSetOrNot()
    {
        var borders = new Borders();
        var top = borders.Top;
        var diagonal = borders.DiagonalDown;

        var enumerator = (Borders.BorderEnumerator)((System.Collections.IEnumerable)borders).GetEnumerator();
        var seen = new System.Collections.Generic.List<Border>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        seen.Should().HaveCount(6);
        seen.Where(b => b != null).Should().BeEquivalentTo([top, diagonal]);

        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        ((System.Collections.IEnumerator)enumerator).Current.Should().Be(enumerator.Current);
    }

    [Fact]
    public void DistanceSetsAllFourDistancesAtOnce()
    {
        var borders = new Borders { Distance = 3 };

        borders.DistanceFromTop.Point.Should().Be(3);
        borders.DistanceFromBottom.Point.Should().Be(3);
        borders.DistanceFromLeft.Point.Should().Be(3);
        borders.DistanceFromRight.Point.Should().Be(3);
    }

    [Fact]
    public void ClearedBordersAreNotNullAndSetNullForgetsTheClearing()
    {
        var borders = new Borders();
        borders.IsNull().Should().BeTrue();

        borders.ClearAll();

        borders.BordersCleared.Should().BeTrue();
        borders.IsNull().Should().BeFalse();

        borders.SetNull();

        borders.BordersCleared.Should().BeFalse();
        borders.IsNull().Should().BeTrue();

        borders.BordersCleared = true;
        borders.IsNull().Should().BeFalse();
    }

    [Fact]
    public void ACloneIsDeep()
    {
        var borders = new Borders { Width = 2, Color = Colors.Red };
        borders.Top.Style = BorderStyle.Dot;

        var clone = borders.Clone();

        clone.Width.Point.Should().Be(2);
        clone.Color.Should().Be(Colors.Red);
        clone.Top.Should().NotBeSameAs(borders.Top);
        clone.Top.Style.Should().Be(BorderStyle.Dot);
    }

    [Fact]
    public void EverythingBordersCanSayIsWrittenAndReadBack()
    {
        var document = new Document();
        var borders = document.AddSection().AddParagraph("bordered").Format.Borders;
        borders.Visible = true;
        borders.Style = BorderStyle.DashLargeGap;
        borders.Width = 1.5;
        borders.Color = Colors.Navy;
        borders.DistanceFromTop = 1;
        borders.DistanceFromBottom = 2;
        borders.DistanceFromLeft = 3;
        borders.DistanceFromRight = 4;
        borders.Top.Width = 5;
        borders.Left.Color = Colors.Red;
        borders.Bottom.Style = BorderStyle.Dot;
        borders.Right.Visible = false;
        borders.DiagonalUp.Width = 6;
        borders.DiagonalDown.Width = 7;

        var again = ((Paragraph)RoundTrip(document).LastSection.Elements[0]).Format.Borders;

        again.Visible.Should().BeTrue();
        again.Style.Should().Be(BorderStyle.DashLargeGap);
        again.Width.Point.Should().Be(1.5);
        again.Color.Should().Be(Colors.Navy);
        (again.DistanceFromTop.Point, again.DistanceFromBottom.Point,
                again.DistanceFromLeft.Point, again.DistanceFromRight.Point)
            .Should().Be((1.0, 2.0, 3.0, 4.0));
        again.Top.Width.Point.Should().Be(5);
        again.Left.Color.Should().Be(Colors.Red);
        again.Bottom.Style.Should().Be(BorderStyle.Dot);
        again.Right.Visible.Should().BeFalse();
        again.DiagonalUp.Width.Point.Should().Be(6);
        again.DiagonalDown.Width.Point.Should().Be(7);
    }

    [Fact]
    public void ClearedBordersAreWrittenAsNull()
    {
        var document = new Document();
        document.AddSection().AddParagraph("plain").Format.Borders.ClearAll();

        DdlWriter.WriteToString(document).Should().Contain("Borders = null");
    }
}
