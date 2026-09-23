using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering.Tests.Helpers;
using Xunit;

namespace PinataLayout.Rendering.Tests;

/// <summary>
///   A cell with a rounded corner is drawn with an arc joining two of its edges, and the arc can
///   only be one width. The renderer settles that before it draws by copying whichever of the two
///   corner borders the caller described onto the other, so the cell's inner width comes out the
///   same whichever edge was the one set.
/// </summary>
/// <remarks>
///   The copy is made onto the cell's own border objects rather than onto a working copy, so a
///   document that has been laid out carries the answer and these tests can read it back. That is
///   also the trap: laying out a document changes it, which is why each arrangement below is built
///   fresh.
/// </remarks>
public class RoundedCornerBorderTests
{
    private const double Heavy = 8;

    /// <summary>
    ///   A table of one cell, its corner rounded and one of the two edges that meet there
    ///   described.
    /// </summary>
    private static Cell CellRounded(RoundedCorner corner, BorderType described)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(4));
        var cell = table.AddRow()[0];
        cell.AddParagraph("Corner");
        cell.RoundedCorner = corner;

        var border = described switch
        {
            BorderType.Left => cell.Borders.Left,
            BorderType.Right => cell.Borders.Right,
            BorderType.Top => cell.Borders.Top,
            _ => cell.Borders.Bottom
        };
        border.Visible = true;
        border.Width = Unit.FromPoint(Heavy);

        Rendered.Of(document);
        return cell;
    }

    /// <summary>
    ///   The vertical edge is the one the renderer reads from when both are available, so
    ///   describing it alone has to carry its width onto the horizontal one.
    /// </summary>
    [Theory]
    [InlineData(RoundedCorner.TopLeft, BorderType.Left)]
    [InlineData(RoundedCorner.TopRight, BorderType.Right)]
    public void AWidthOnTheVerticalEdgeReachesTheTopEdgeOfTheSameCorner(RoundedCorner corner, BorderType described)
    {
        var cell = CellRounded(corner, described);

        cell.Borders.Top.Visible.Should().BeTrue();
        cell.Borders.Top.Width.Point.Should().BeApproximately(Heavy, 0.01);
    }

    [Theory]
    [InlineData(RoundedCorner.BottomLeft, BorderType.Left)]
    [InlineData(RoundedCorner.BottomRight, BorderType.Right)]
    public void AWidthOnTheVerticalEdgeReachesTheBottomEdgeOfTheSameCorner(RoundedCorner corner, BorderType described)
    {
        var cell = CellRounded(corner, described);

        cell.Borders.Bottom.Visible.Should().BeTrue();
        cell.Borders.Bottom.Width.Point.Should().BeApproximately(Heavy, 0.01);
    }

    /// <summary>
    ///   And the other way about: describing only the horizontal edge carries it onto the vertical
    ///   one, since the renderer takes whichever of the two is visible as the source.
    /// </summary>
    [Theory]
    [InlineData(RoundedCorner.TopLeft, BorderType.Top)]
    [InlineData(RoundedCorner.BottomLeft, BorderType.Bottom)]
    public void AWidthOnTheHorizontalEdgeReachesTheLeftEdgeOfTheSameCorner(RoundedCorner corner, BorderType described)
    {
        var cell = CellRounded(corner, described);

        cell.Borders.Left.Visible.Should().BeTrue();
        cell.Borders.Left.Width.Point.Should().BeApproximately(Heavy, 0.01);
    }

    [Theory]
    [InlineData(RoundedCorner.TopRight, BorderType.Top)]
    [InlineData(RoundedCorner.BottomRight, BorderType.Bottom)]
    public void AWidthOnTheHorizontalEdgeReachesTheRightEdgeOfTheSameCorner(RoundedCorner corner, BorderType described)
    {
        var cell = CellRounded(corner, described);

        cell.Borders.Right.Visible.Should().BeTrue();
        cell.Borders.Right.Width.Point.Should().BeApproximately(Heavy, 0.01);
    }

    /// <summary>
    ///   The copy reaches only the corner that was rounded. A width on the left edge of a cell
    ///   rounded at the top left says nothing about its bottom edge, and a renderer that equalized
    ///   all four would quietly thicken the rest of the table.
    /// </summary>
    [Fact]
    public void TheEdgesAwayFromTheRoundedCornerAreLeftAlone()
    {
        var cell = CellRounded(RoundedCorner.TopLeft, BorderType.Left);

        cell.Borders.Bottom.Width.Point.Should().NotBe(Heavy);
        cell.Borders.Right.Width.Point.Should().NotBe(Heavy);
    }

    /// <summary>
    ///   A cell with no rounded corner is left as it was described, which is the early return the
    ///   great majority of cells take.
    /// </summary>
    [Fact]
    public void ACellWithNoRoundedCornerKeepsTheEdgesItWasGiven()
    {
        var cell = CellRounded(RoundedCorner.None, BorderType.Left);

        cell.Borders.Left.Width.Point.Should().BeApproximately(Heavy, 0.01);
        cell.Borders.Top.Visible.Should().BeFalse();
    }

    /// <summary>
    ///   Neither edge described means there is nothing to copy, and the renderer says so by
    ///   returning before it asks for either border — asking would bring one into existence and
    ///   give the cell an edge the caller never wrote.
    /// </summary>
    [Fact]
    public void ACornerWithNeitherEdgeDescribedGainsNoBorder()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(4));
        var cell = table.AddRow()[0];
        cell.AddParagraph("Corner");
        cell.RoundedCorner = RoundedCorner.TopLeft;

        Rendered.Of(document);

        cell.Borders.HasBorder(BorderType.Left).Should().BeFalse();
        cell.Borders.HasBorder(BorderType.Top).Should().BeFalse();
    }

    /// <summary>
    ///   The style and the colour travel with the width, or the arc would be drawn in one and its
    ///   continuation in another.
    /// </summary>
    [Fact]
    public void TheStyleAndColourTravelWithTheWidth()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.AddColumn(Unit.FromCentimeter(4));
        var cell = table.AddRow()[0];
        cell.AddParagraph("Corner");
        cell.RoundedCorner = RoundedCorner.TopLeft;
        cell.Borders.Left.Visible = true;
        cell.Borders.Left.Width = Unit.FromPoint(Heavy);
        cell.Borders.Left.Style = BorderStyle.Dot;
        cell.Borders.Left.Color = Colors.Firebrick;

        Rendered.Of(document);

        cell.Borders.Top.Style.Should().Be(BorderStyle.Dot);
        cell.Borders.Top.Color.Should().Be(Colors.Firebrick);
    }
}
