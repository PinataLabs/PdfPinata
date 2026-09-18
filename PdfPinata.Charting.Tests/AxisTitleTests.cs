using System;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   The caption written alongside an axis.
/// </summary>
/// <remarks>
///   <c>AxisTitleRenderer</c> draws the caption twice over: upright, where it goes into a rectangle
///   with its alignment handed to a string format, and rotated, where it goes into a rectangle
///   centred on the origin and the surface is turned under it. The rotated path is the longer of
///   the two and is what a chart with a caption up its side takes, which is the ordinary case.
///
///   It serves the value axis only, despite its name. The category axis renderers draw their own
///   caption inline at the end of their Draw, reading neither the alignment nor the orientation -
///   so half the tests here are about which of the two ways a caption is drawn, and what is lost
///   by taking the other one.
///
///   The other half of the renderer is that a title takes room. It is measured during Format and
///   the axis subtracts what it measured from what is left for the plot area - so the surest sign
///   that a title was laid out at all is that the columns moved.
/// </remarks>
public class AxisTitleTests
{
    [Fact]
    public void ACaptionOnEitherAxisIsWrittenOnThePage()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.XAxis.Title.Caption = "Across";
        chart.YAxis.Title.Caption = "Up";

        ShownText.On(Drawn.Page(chart)).Should().Contain("Across").And.Contain("Up");
    }

    [Fact]
    public void ACaptionOnTheCategoryAxisTakesRoomFromThePlotArea()
    {
        var untitled = Charts.Of(ChartType.Column2D, 1.0, 3.0);

        var titled = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        titled.XAxis.Title.Caption = "Across";

        var withoutTitle = PaintedRectangles.FilledOn(Drawn.Page(untitled))[0];
        var withTitle = PaintedRectangles.FilledOn(Drawn.Page(titled))[0];

        // The title is written under the category labels, so the plot area loses height from the
        // bottom and the columns start higher up the page and are shorter.
        withTitle.Y.Should().BeGreaterThan(withoutTitle.Y);
        withTitle.Height.Should().BeLessThan(withoutTitle.Height);
    }

    [Fact]
    public void ACaptionOnTheValueAxisTakesRoomFromThePlotArea()
    {
        var untitled = Charts.Of(ChartType.Column2D, 1.0, 3.0);

        var titled = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        titled.YAxis.Title.Caption = "Up";

        var withoutTitle = PaintedRectangles.FilledOn(Drawn.Page(untitled))[0];
        var withTitle = PaintedRectangles.FilledOn(Drawn.Page(titled))[0];

        // The value axis runs up the left, so its title costs width and the columns move right.
        withTitle.X.Should().BeGreaterThan(withoutTitle.X);
        withTitle.Width.Should().BeLessThan(withoutTitle.Width);
    }

    /// <summary>
    ///   A rotated title is measured across its turned extent rather than its written one, so a
    ///   long caption turned on its side costs the plot area its height rather than its width.
    ///   That is the whole point of turning it, and it is the branch of Format that does the
    ///   turning.
    /// </summary>
    [Fact]
    public void ARotatedCaptionCostsThePlotAreaLessWidthThanAnUprightOne()
    {
        var upright = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        upright.YAxis.Title.Caption = "A rather long caption";

        var rotated = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        rotated.YAxis.Title.Caption = "A rather long caption";
        rotated.YAxis.Title.Orientation = 90;

        var written = PaintedRectangles.FilledOn(Drawn.Page(upright))[0];
        var turned = PaintedRectangles.FilledOn(Drawn.Page(rotated))[0];

        turned.X.Should().BeLessThan(written.X);
        turned.Width.Should().BeGreaterThan(written.Width);
    }

    [Fact]
    public void ARotatedCaptionIsStillWrittenOnThePage()
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.Title.Caption = "Rotated";
        chart.YAxis.Title.Orientation = 90;

        ShownText.On(Drawn.Page(chart)).Should().Contain("Rotated");
    }

    /// <summary>
    ///   Across the axis, a rotated caption does not move, and there is nowhere for it to move to:
    ///   the strip the axis set aside for its title is exactly as wide as the title, because that
    ///   is how much room the axis took from the plot area for it. All three alignments put the
    ///   caption in the middle of that strip, which is the only place it fits.
    /// </summary>
    /// <remarks>
    ///   Left used to come out elsewhere - it put the caption's centre on the strip's near edge,
    ///   so half of it hung outside the space the layout had reserved. Landing where the other two
    ///   land is the correction rather than a loss.
    /// </remarks>
    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Right)]
    public void AligningARotatedCaptionAcrossTheAxisMovesItNowhere(HorizontalAlignment alignment)
    {
        RotatedCaption(alignment, VerticalAlignment.Center)
            .Should().Be(RotatedCaption(HorizontalAlignment.Center, VerticalAlignment.Center));
    }

    /// <summary>
    ///   Each of the three vertical alignments puts a rotated caption somewhere of its own, which
    ///   for a caption turned up the side of the chart is where along the axis it sits.
    /// </summary>
    /// <remarks>
    ///   Centring it and pushing it to the bottom used to come to the same place. The two cases
    ///   are written separately - <c>y + height / 2</c> against
    ///   <c>y + height - layout.Height / 2</c> - but the layout rectangle was the strip the axis
    ///   set aside rather than the caption itself, so the height being halved was the same height
    ///   on both sides of the subtraction and the second expression reduced to the first. It is
    ///   now the caption's own height, which is what those offsets were always measuring against.
    /// </remarks>
    [Fact]
    public void EachVerticalAlignmentPutsARotatedCaptionSomewhereOfItsOwn()
    {
        var top = RotatedCaption(HorizontalAlignment.Center, VerticalAlignment.Top);
        var centre = RotatedCaption(HorizontalAlignment.Center, VerticalAlignment.Center);
        var bottom = RotatedCaption(HorizontalAlignment.Center, VerticalAlignment.Bottom);

        new[] { top, centre, bottom }.Distinct().Should().HaveCount(3);
    }

    /// <summary>
    ///   An upright caption is placed by handing its alignment to a string format instead of by
    ///   turning the surface, and vertically the three answers are distinct: the value axis's
    ///   title is drawn into a rectangle as tall as the axis, so there is room in it to move up
    ///   and down.
    /// </summary>
    [Theory]
    [InlineData(VerticalAlignment.Top)]
    [InlineData(VerticalAlignment.Bottom)]
    public void AligningAnUprightCaptionVerticallyMovesItUpOrDownTheAxis(VerticalAlignment alignment)
    {
        UprightCaptionPosition(alignment)
            .Should().NotBe(UprightCaptionPosition(VerticalAlignment.Center));
    }

    /// <summary>
    ///   Horizontally it moves nowhere: the same rectangle is only as wide as the caption
    ///   measured, so there is no slack across it to align within and all three answers land on
    ///   the same point.
    /// </summary>
    /// <remarks>
    ///   A constraint rather than a defect, and the same one that holds of a rotated caption: the
    ///   strip is its own width because that is how much room the axis took from the plot area for
    ///   it. A caller can move a value-axis caption along its axis but not across it, and giving
    ///   the setting somewhere to move to would mean reserving width the caption does not need.
    /// </remarks>
    [Theory]
    [InlineData(HorizontalAlignment.Left)]
    [InlineData(HorizontalAlignment.Right)]
    public void AligningAnUprightCaptionAcrossMovesItNowhere(HorizontalAlignment alignment)
    {
        UprightCaptionPosition(alignment)
            .Should().Be(UprightCaptionPosition(HorizontalAlignment.Center));
    }

    /// <summary>
    ///   The category axis reads both settings too. It used to draw its own caption inline and
    ///   look at neither, so a caption there was written flat and in the middle whatever it was
    ///   asked for - the two axes took different code paths to draw the same kind of object. It
    ///   now hands the caption to this renderer as the value axis always has.
    /// </summary>
    [Fact]
    public void TheCategoryAxisReadsItsCaptionsAlignmentAndOrientationToo()
    {
        var plain = CategoryCaption(title => { });
        var aligned = CategoryCaption(title => title.Alignment = HorizontalAlignment.Right);
        var rotated = CategoryCaption(title => title.Orientation = 90);

        aligned.Should().NotBe(plain);
        rotated.Should().NotBe(plain);
    }

    /// <summary>
    ///   And it aligns within the width of the axis, so the three alignments run left to right
    ///   in the order they name.
    /// </summary>
    [Fact]
    public void AligningACategoryAxisCaptionMovesItAlongTheAxis()
    {
        var left = CategoryCaption(title => title.Alignment = HorizontalAlignment.Left);
        var centre = CategoryCaption(title => title.Alignment = HorizontalAlignment.Center);
        var right = CategoryCaption(title => title.Alignment = HorizontalAlignment.Right);

        left.X.Should().BeLessThan(centre.X);
        centre.X.Should().BeLessThan(right.X);
    }

    /// <summary>
    ///   A caption turned on its side reserves the room it takes turned, not the room it would
    ///   have taken lying flat. A long caption is tall when it is rotated and short when it is
    ///   not, so the plot area loses height to one and almost none to the other.
    /// </summary>
    /// <remarks>
    ///   Both axes measure their title through <c>AxisTitleRenderer.Format</c>, which is the only
    ///   place that accounts for the orientation. The category axis used to measure the string
    ///   itself and so reserved the wrong extent for a rotated caption.
    /// </remarks>
    [Fact]
    public void ARotatedCategoryAxisCaptionReservesTheRoomItTakesTurned()
    {
        var flat = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        flat.XAxis.Title.Caption = "A rather long caption";

        var turned = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        turned.XAxis.Title.Caption = "A rather long caption";
        turned.XAxis.Title.Orientation = 90;

        var lyingDown = PaintedRectangles.FilledOn(Drawn.Page(flat))[0];
        var standingUp = PaintedRectangles.FilledOn(Drawn.Page(turned))[0];

        standingUp.Y.Should().BeGreaterThan(lyingDown.Y);
        standingUp.Height.Should().BeLessThan(lyingDown.Height);
    }

    [Fact]
    public void AnEmptyCaptionCostsThePlotAreaNothing()
    {
        var untitled = Charts.Of(ChartType.Column2D, 1.0, 3.0);

        var emptyTitle = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        emptyTitle.YAxis.Title.Caption = "";

        var withoutTitle = PaintedRectangles.FilledOn(Drawn.Page(untitled))[0];
        var withEmptyTitle = PaintedRectangles.FilledOn(Drawn.Page(emptyTitle))[0];

        withEmptyTitle.X.Should().Be(withoutTitle.X);
        withEmptyTitle.Width.Should().Be(withoutTitle.Width);
    }

    [Fact]
    public void ABarChartsAxisTitlesAreWrittenTheSameWay()
    {
        var chart = Charts.Of(ChartType.Bar2D, 3.0, 6.0);
        chart.XAxis.Title.Caption = "Categories";
        chart.YAxis.Title.Caption = "Values";

        ShownText.On(Drawn.Page(chart)).Should().Contain("Categories").And.Contain("Values");
    }

    /// <summary>
    ///   The page a rotated caption is drawn on, as bytes.
    /// </summary>
    /// <remarks>
    ///   A rotated caption is positioned by transforming the surface under it rather than by
    ///   moving the text on it, and PdfPinata may realize a transform either as a <c>cm</c> or
    ///   by folding it into the coordinates it then writes. Reading the position out of the text
    ///   operators alone would therefore see the move in one case and miss it in the other. The
    ///   whole content stream sees it in both, and since the alignment is the only thing these
    ///   comparisons vary, a difference anywhere in it came from the alignment.
    /// </remarks>
    private static string RotatedCaption(
        HorizontalAlignment alignment, VerticalAlignment verticalAlignment)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.Title.Caption = "Rotated";
        chart.YAxis.Title.Orientation = 90;
        chart.YAxis.Title.Alignment = alignment;
        chart.YAxis.Title.VerticalAlignment = verticalAlignment;

        return Encoding.ASCII.GetString(PageContent.Of(Drawn.Page(chart)));
    }

    /// <summary>
    ///   The value axis's caption, left upright, which is the branch of this renderer that hands
    ///   the alignment to a string format rather than transforming the surface.
    /// </summary>
    private static (double X, double Y) UprightCaptionPosition(HorizontalAlignment alignment)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.Title.Caption = "Up";
        chart.YAxis.Title.Alignment = alignment;

        return PositionOfCaption(chart, "Up");
    }

    private static (double X, double Y) UprightCaptionPosition(VerticalAlignment alignment)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.YAxis.Title.Caption = "Up";
        chart.YAxis.Title.VerticalAlignment = alignment;

        return PositionOfCaption(chart, "Up");
    }

    private static (double X, double Y) CategoryCaption(Action<AxisTitle> arrange)
    {
        var chart = Charts.Of(ChartType.Column2D, 1.0, 3.0);
        chart.XAxis.Title.Caption = "Across";
        arrange(chart.XAxis.Title);

        return PositionOfCaption(chart, "Across");
    }

    private static (double X, double Y) PositionOfCaption(Chart chart, string caption)
    {
        var run = ShownText.RunsOn(Drawn.Page(chart)).Single(shown => shown.Text == caption);
        return (run.X, run.Y);
    }
}
