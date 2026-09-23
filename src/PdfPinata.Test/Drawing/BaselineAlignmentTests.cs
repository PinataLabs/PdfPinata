using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   A baseline-aligned string is anchored to the top edge of its layout rectangle and reads
///   nothing else from it, so a height is surplus information rather than a contradiction.
///   Passing one used to throw — which made <see cref="XStringFormats.Default" />, being
///   BaseLineLeft, throw on the most natural overload there is.
/// </summary>
public class BaselineAlignmentTests
{
    private const double FontSize = 24;
    private const double Left = 20;
    private const double Top = 60;
    private const double Width = 300;

    private static XFont Plain => new XFont("Arial", FontSize, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

    [Fact]
    public void ARectangleWithHeightNoLongerThrows()
    {
        var draw = () => PageShowing(new XRect(Left, Top, Width, 20), XStringFormats.BaseLineLeft);

        draw.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(200)]
    public void TheBaselineSitsOnTheTopEdgeWhateverTheHeight(double height)
    {
        var page = PageShowing(new XRect(Left, Top, Width, height), XStringFormats.BaseLineLeft);

        // PDF measures up the page and XGraphics measures down it, so the top edge of a
        // rectangle at y = 60 is 60 points below the top of the page.
        BaselineOf(page).Y.Should().BeApproximately(page.Height.Point - Top, 0.001);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(200)]
    public void HeightDoesNotMoveTheText(double height)
    {
        // The requirement, and what catches a height leaking into the arithmetic: the same
        // string, the same format, the same origin, a different height.
        var flat = BaselineOf(PageShowing(new XRect(Left, Top, Width, 0), XStringFormats.BaseLineLeft));
        var tall = BaselineOf(PageShowing(new XRect(Left, Top, Width, height), XStringFormats.BaseLineLeft));

        tall.X.Should().BeApproximately(flat.X, 0.001);
        tall.Y.Should().BeApproximately(flat.Y, 0.001);
    }

    [Fact]
    public void AZeroHeightRectangleIsDrawnWhereItAlwaysWas()
    {
        // Unchanged by the removal of the guard: the case that always worked still works, and
        // is still measured against the same edge.
        var page = PageShowing(new XRect(Left, Top, Width, 0), XStringFormats.BaseLineLeft);

        BaselineOf(page).X.Should().BeApproximately(Left, 0.001);
        BaselineOf(page).Y.Should().BeApproximately(page.Height.Point - Top, 0.001);
    }

    [Fact]
    public void TheDefaultFormatWorksWithAnOrdinaryRectangle()
    {
        // The trap this change exists to remove: the format a caller reaches for when they are
        // not thinking about formats is BaseLineLeft, and it used to refuse an ordinary rectangle.
        var draw = () => PageShowing(new XRect(Left, Top, Width, 20), XStringFormats.Default);

        draw.Should().NotThrow();

        var page = PageShowing(new XRect(Left, Top, Width, 20), XStringFormats.Default);
        BaselineOf(page).Y.Should().BeApproximately(page.Height.Point - Top, 0.001);
    }

    [Fact]
    public void HorizontalAlignmentStillAppliesWhenTheRectangleHasAHeight()
    {
        // Only the vertical dimension is being ignored. The width still places the text.
        var textWidth = MeasuredWidth();

        var rectangle = new XRect(Left, Top, Width, 20);
        var rightPage = PageShowing(rectangle, XStringFormats.BaseLineRight);
        var centrePage = PageShowing(rectangle, XStringFormats.BaseLineCenter);

        var right = BaselineOf(rightPage);
        var centre = BaselineOf(centrePage);

        right.X.Should().BeApproximately(Left + Width - textWidth, 0.001);
        centre.X.Should().BeApproximately(Left + (Width - textWidth) / 2, 0.001);

        // ...and both still sit on the rectangle's top edge, as the left-aligned one does.
        right.Y.Should().BeApproximately(rightPage.Height.Point - Top, 0.001);
        centre.Y.Should().BeApproximately(centrePage.Height.Point - Top, 0.001);
    }

    [Fact]
    public void HorizontalAlignmentIsUnaffectedByTheHeight()
    {
        var flat = BaselineOf(PageShowing(new XRect(Left, Top, Width, 0), XStringFormats.BaseLineRight));
        var tall = BaselineOf(PageShowing(new XRect(Left, Top, Width, 200), XStringFormats.BaseLineRight));

        tall.X.Should().BeApproximately(flat.X, 0.001);
        tall.Y.Should().BeApproximately(flat.Y, 0.001);
    }

    private static double MeasuredWidth()
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        return gfx.MeasureString(Text, Plain, XStringFormats.Default).Width;
    }

    private const string Text = "Handles";

    private static (double X, double Y) BaselineOf(PdfPage page) => TextBaselines.PositionsOf(page)[0];

    private static PdfPage PageShowing(XRect layoutRectangle, XStringFormat format)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString(Text, Plain, XBrushes.Black, layoutRectangle, format);
        return page;
    }
}
