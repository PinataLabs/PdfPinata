using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The whole of the arithmetic behind a page resize lives in <see cref="PageFit"/>, away from
///   the PDF it gets applied to. This is where it is pinned down: the tests that resize a real
///   page check that the transform reaches the content, its annotations and the destinations that
///   point at it, and take for granted that the transform itself is right.
///   <para>
///   Everything here is in PDF user space - origin at the bottom left, y running up the page.
///   </para>
/// </summary>
public class PageFitTests
{
    const double A4Width = 595;
    const double A4Height = 842;
    const double A5Width = 420;
    const double A5Height = 595;

    /// <summary>
    ///   Where the four corners of a rectangle end up under a transform, as
    ///   (bottom left, top right). Corners say what a matrix does in terms anyone can check;
    ///   its six components do not.
    /// </summary>
    static (XPoint BottomLeft, XPoint TopRight) CornersOf(XRect source, XMatrix matrix)
    {
        return (matrix.Transform(new XPoint(source.X, source.Y)),
            matrix.Transform(new XPoint(source.X + source.Width, source.Y + source.Height)));
    }

    static XRect A4 => new(0, 0, A4Width, A4Height);
    static XRect A5 => new(0, 0, A5Width, A5Height);

    [Fact]
    public void FitShrinksAnA4PageOntoA5WithoutLosingAnyOfIt()
    {
        var matrix = PageFit.Calculate(A4, A5, PageResizeOptions.Default);

        var (bottomLeft, topRight) = CornersOf(A4, matrix);

        // A4 and A5 are not quite the same shape, so a uniform scale leaves a little slack. It
        // is the height that has it, and being centred it is shared between top and bottom.
        var scale = Math.Min(A5Width / A4Width, A5Height / A4Height);
        var slack = (A5Height - A4Height * scale) / 2;

        bottomLeft.X.Should().BeApproximately(0, 1e-9);
        bottomLeft.Y.Should().BeApproximately(slack, 1e-9);
        topRight.X.Should().BeApproximately(A5Width, 1e-9);
        topRight.Y.Should().BeApproximately(A5Height - slack, 1e-9);
    }

    [Fact]
    public void FitGrowsAnA5PageOntoA4WithoutLosingAnyOfIt()
    {
        var matrix = PageFit.Calculate(A5, A4, PageResizeOptions.Default);

        var (bottomLeft, topRight) = CornersOf(A5, matrix);

        var scale = Math.Min(A4Width / A5Width, A4Height / A5Height);
        var slack = (A4Width - A5Width * scale) / 2;

        bottomLeft.X.Should().BeApproximately(slack, 1e-9);
        bottomLeft.Y.Should().BeApproximately(0, 1e-9);
        topRight.X.Should().BeApproximately(A4Width - slack, 1e-9);
        topRight.Y.Should().BeApproximately(A4Height, 1e-9);
    }

    [Fact]
    public void FitScalesBothAxesTheSame()
    {
        var matrix = PageFit.Calculate(A4, A5, PageResizeOptions.Default);

        matrix.M11.Should().BeApproximately(matrix.M22, 1e-9,
            "a uniform scale is the whole point of Fit - anything else distorts the page");
    }

    [Fact]
    public void FillCoversTheTargetAndLetsTheRestOverflow()
    {
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Fill;

        var matrix = PageFit.Calculate(A4, A5, options);
        var (bottomLeft, topRight) = CornersOf(A4, matrix);

        // Uniform, and large enough that neither axis leaves a gap.
        matrix.M11.Should().BeApproximately(matrix.M22, 1e-9);
        (topRight.X - bottomLeft.X).Should().BeGreaterThanOrEqualTo(A5Width - 1e-9);
        (topRight.Y - bottomLeft.Y).Should().BeGreaterThanOrEqualTo(A5Height - 1e-9);

        // And at least one axis overflows, or this would have been Fit.
        var overflows = topRight.X - bottomLeft.X > A5Width + 1e-9 ||
                        topRight.Y - bottomLeft.Y > A5Height + 1e-9;
        overflows.Should().BeTrue();
    }

    [Fact]
    public void StretchPutsTheCornersExactlyOnTheTargetCorners()
    {
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.Stretch;

        var matrix = PageFit.Calculate(A4, A5, options);
        var (bottomLeft, topRight) = CornersOf(A4, matrix);

        bottomLeft.X.Should().BeApproximately(0, 1e-9);
        bottomLeft.Y.Should().BeApproximately(0, 1e-9);
        topRight.X.Should().BeApproximately(A5Width, 1e-9);
        topRight.Y.Should().BeApproximately(A5Height, 1e-9);

        matrix.M11.Should().NotBeApproximately(matrix.M22, 1e-6,
            "A4 and A5 are of different proportions, so stretching one onto the other distorts it");
    }

    [Fact]
    public void NoneDoesNotScaleAtAll()
    {
        var options = PageResizeOptions.Crop;

        var matrix = PageFit.Calculate(A4, A5, options);
        var (bottomLeft, topRight) = CornersOf(A4, matrix);

        (topRight.X - bottomLeft.X).Should().BeApproximately(A4Width, 1e-9);
        (topRight.Y - bottomLeft.Y).Should().BeApproximately(A4Height, 1e-9);
    }

    /// <summary>
    ///   A 100 x 100 source in a 300 x 200 box at its own size leaves 200 of horizontal slack and
    ///   100 of vertical, so every alignment lands on a round number and the nine cases can be
    ///   read at a glance.
    /// </summary>
    public static TheoryData<PageAlignment, double, double> Alignments => new TheoryData<PageAlignment, double, double>
    {
        { PageAlignment.BottomLeft, 0d, 0d },
        { PageAlignment.BottomCenter, 100d, 0d },
        { PageAlignment.BottomRight, 200d, 0d },
        { PageAlignment.MiddleLeft, 0d, 50d },
        { PageAlignment.MiddleCenter, 100d, 50d },
        { PageAlignment.MiddleRight, 200d, 50d },
        { PageAlignment.TopLeft, 0d, 100d },
        { PageAlignment.TopCenter, 100d, 100d },
        { PageAlignment.TopRight, 200d, 100d }
    };

    [Theory]
    [MemberData(nameof(Alignments))]
    public void AlignmentDecidesWhereTheSlackGoes(PageAlignment alignment, double expectedX, double expectedY)
    {
        var source = new XRect(0, 0, 100, 100);
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.None;
        options.Alignment = alignment;

        var matrix = PageFit.Calculate(source, new XRect(0, 0, 300, 200), options);
        var (bottomLeft, _) = CornersOf(source, matrix);

        bottomLeft.X.Should().BeApproximately(expectedX, 1e-9);
        bottomLeft.Y.Should().BeApproximately(expectedY, 1e-9);
    }

    [Fact]
    public void TopMeansTheTopOfThePageAndNotTheSmallerYCoordinate()
    {
        // Worth its own test because XRect calls the side with the smaller y its Top, and PDF
        // does not. Aligning to the top must push the content up, away from the origin.
        var source = new XRect(0, 0, 100, 100);
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.None;
        options.Alignment = PageAlignment.TopLeft;

        var matrix = PageFit.Calculate(source, new XRect(0, 0, 100, 500), options);
        var (bottomLeft, topRight) = CornersOf(source, matrix);

        bottomLeft.Y.Should().BeApproximately(400, 1e-9);
        topRight.Y.Should().BeApproximately(500, 1e-9);
    }

    [Fact]
    public void AMarginIsTakenOffTheTargetBeforeTheContentIsFitted()
    {
        var source = new XRect(0, 0, 100, 100);
        var options = PageResizeOptions.Default;
        options.Margin = 10;

        var matrix = PageFit.Calculate(source, new XRect(0, 0, 200, 200), options);
        var (bottomLeft, topRight) = CornersOf(source, matrix);

        bottomLeft.X.Should().BeApproximately(10, 1e-9);
        bottomLeft.Y.Should().BeApproximately(10, 1e-9);
        topRight.X.Should().BeApproximately(190, 1e-9);
        topRight.Y.Should().BeApproximately(190, 1e-9);
    }

    [Fact]
    public void AutoRotateTurnsThePageWhenTheTargetIsOfTheOppositeShape()
    {
        var options = PageResizeOptions.Default;
        options.AutoRotate = true;

        var landscapeA4 = new XRect(0, 0, A4Height, A4Width);
        var matrix = PageFit.Calculate(A4, landscapeA4, options, out var turned);

        turned.Should().BeTrue();

        // Turned a quarter clockwise, the same way a /Rotate entry of 90 turns a page: the
        // corner that was at the top left comes to rest at the top right.
        var wasTopLeft = matrix.Transform(new XPoint(0, A4Height));
        wasTopLeft.X.Should().BeApproximately(A4Height, 1e-9);
        wasTopLeft.Y.Should().BeApproximately(A4Width, 1e-9);

        // And it fills the box, which is the reason for turning it rather than shrinking it.
        var wasBottomLeft = matrix.Transform(new XPoint(0, 0));
        var wasBottomRight = matrix.Transform(new XPoint(A4Width, 0));
        wasBottomLeft.Should().BeEquivalentTo(new XPoint(0, A4Width),
            o => o.Using<double>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 1e-9))
                .WhenTypeIs<double>());
        wasBottomRight.Should().BeEquivalentTo(new XPoint(0, 0),
            o => o.Using<double>(ctx => ctx.Subject.Should().BeApproximately(ctx.Expectation, 1e-9))
                .WhenTypeIs<double>());
    }

    [Fact]
    public void AutoRotateLeavesThePageAloneWhenTheShapesAlreadyAgree()
    {
        var options = PageResizeOptions.Default;
        options.AutoRotate = true;

        PageFit.Calculate(A4, A5, options, out var turned);

        turned.Should().BeFalse("both are portrait, so there is nothing to gain by turning one");
    }

    [Fact]
    public void AutoRotateIsOffUnlessItIsAskedFor()
    {
        var landscapeA4 = new XRect(0, 0, A4Height, A4Width);

        PageFit.Calculate(A4, landscapeA4, PageResizeOptions.Default, out var turned);

        turned.Should().BeFalse();
    }

    [Fact]
    public void ASquareIsOfNeitherShapeSoItIsNeverTurned()
    {
        var options = PageResizeOptions.Default;
        options.AutoRotate = true;
        var square = new XRect(0, 0, 500, 500);

        PageFit.Calculate(square, A4, options, out var turned);

        turned.Should().BeFalse();
    }

    [Fact]
    public void ASourceAwayFromTheOriginIsBroughtOntoTheTarget()
    {
        // A media box is allowed an origin other than (0, 0) and real documents have them, so
        // the transform has to move the content as well as scale it.
        var source = new XRect(50, 100, A4Width, A4Height);

        var matrix = PageFit.Calculate(source, A5, PageResizeOptions.Default);
        var (bottomLeft, topRight) = CornersOf(source, matrix);

        var scale = Math.Min(A5Width / A4Width, A5Height / A4Height);
        var slack = (A5Height - A4Height * scale) / 2;

        bottomLeft.X.Should().BeApproximately(0, 1e-9);
        bottomLeft.Y.Should().BeApproximately(slack, 1e-9);
        topRight.X.Should().BeApproximately(A5Width, 1e-9);
        topRight.Y.Should().BeApproximately(A5Height - slack, 1e-9);
    }

    [Fact]
    public void ASourceAwayFromTheOriginIsBroughtOntoTheTargetWhenTurnedToo()
    {
        var source = new XRect(50, 100, A4Width, A4Height);
        var options = PageResizeOptions.Default;
        options.AutoRotate = true;
        var landscapeA4 = new XRect(0, 0, A4Height, A4Width);

        var matrix = PageFit.Calculate(source, landscapeA4, options, out var turned);

        turned.Should().BeTrue();

        var wasTopLeft = matrix.Transform(new XPoint(source.X, source.Y + source.Height));
        wasTopLeft.X.Should().BeApproximately(A4Height, 1e-9);
        wasTopLeft.Y.Should().BeApproximately(A4Width, 1e-9);
    }

    [Fact]
    public void TheTargetRectangleNeedNotBeAtTheOriginEither()
    {
        var source = new XRect(0, 0, 100, 100);
        var options = PageResizeOptions.Default;
        options.Fit = PageFitMode.None;
        options.Alignment = PageAlignment.BottomLeft;

        var matrix = PageFit.Calculate(source, new XRect(30, 40, 200, 200), options);
        var (bottomLeft, _) = CornersOf(source, matrix);

        bottomLeft.X.Should().BeApproximately(30, 1e-9);
        bottomLeft.Y.Should().BeApproximately(40, 1e-9);
    }

    [Fact]
    public void NullOptionsAreTheDefaultOptions()
    {
        var withNull = PageFit.Calculate(A4, A5, null);
        var withDefault = PageFit.Calculate(A4, A5, PageResizeOptions.Default);

        withNull.Should().Be(withDefault);
    }

    [Fact]
    public void TheDefaultOptionsAreAFreshInstanceEveryTime()
    {
        // They are handed out as a property rather than held in a field, so that changing one
        // setting on them does not change it for every later caller.
        var first = PageResizeOptions.Default;
        first.Margin = 50;

        PageResizeOptions.Default.Margin.Point.Should().Be(0);
    }

    [Fact]
    public void TheCropPresetKeepsTheHeadOfThePage()
    {
        var options = PageResizeOptions.Crop;

        options.Fit.Should().Be(PageFitMode.None);
        options.Alignment.Should().Be(PageAlignment.TopLeft,
            "the setter this replaces cropped from the bottom left and lost the heading, which " +
            "was an artefact of the origin rather than anybody's intent");

        var matrix = PageFit.Calculate(A4, A5, options);
        var (bottomLeft, topRight) = CornersOf(A4, matrix);

        // The top of the A4 page is at the top of the A5 one, and it is the foot that hangs off.
        topRight.Y.Should().BeApproximately(A5Height, 1e-9);
        bottomLeft.Y.Should().BeApproximately(A5Height - A4Height, 1e-9);
    }

    [Fact]
    public void ASourceWithNoAreaIsRefused()
    {
        var act = () => PageFit.Calculate(new XRect(0, 0, 0, 100), A5, PageResizeOptions.Default);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ATargetWithNoAreaIsRefused()
    {
        var act = () => PageFit.Calculate(A4, new XRect(0, 0, 100, 0), PageResizeOptions.Default);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AMarginThatLeavesNoRoomIsRefusedRatherThanScalingTheContentToNothing()
    {
        var options = PageResizeOptions.Default;
        options.Margin = 300;

        var act = () => PageFit.Calculate(A4, A5, options);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ANegativeMarginIsRefused()
    {
        var options = PageResizeOptions.Default;
        options.Margin = -10;

        var act = () => PageFit.Calculate(A4, A5, options);

        act.Should().Throw<ArgumentException>();
    }
}
