using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   PDF has no way to write NaN or infinity. A viewer handed one stops drawing rather than
///   complaining, so the page arrives blank or half-finished with nothing to say why - and the
///   drawing call that produced it returned perfectly happily, possibly hours earlier.
///   <para>
///   Two defects found while building the demonstration app were exactly this and nothing else:
///   every column, bar, line and area chart drew its whole plot area at NaN because the axis
///   renderers read a field the property would have filled in, and every DataMatrix built by a
///   constructor that leaves Size unset drew its modules at negative infinity. Neither threw.
///   The renderer now refuses the operator instead, which turns both into a stack trace at the
///   drawing call.
///   </para>
/// </summary>
public class NonFiniteCoordinateTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ALineDrawnToANumberThatIsNotOneIsRefused(double poison)
    {
        var drawing = () => Draw(gfx =>
            gfx.DrawLine(XPens.Black, 10, 10, poison, 40));

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a finite number*");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ARectangleOfANonFiniteSizeIsRefused(double poison)
    {
        var drawing = () => Draw(gfx =>
            gfx.DrawRectangle(XBrushes.Black, 10, 10, poison, 40));

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a finite number*");
    }

    [Fact]
    public void AnEllipseIsRefusedToo()
    {
        // Curves go out through a different formatting method than lines and rectangles do, so
        // the check has to be on each rather than on one of them.
        var drawing = () => Draw(gfx =>
            gfx.DrawEllipse(XBrushes.Black, double.NaN, 10, 40, 40));

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a finite number*");
    }

    [Fact]
    public void TextDrawnAtANonFinitePositionIsRefused()
    {
        var font = new XFont("Liberation Sans", 12);

        var drawing = () => Draw(gfx =>
            gfx.DrawString("nowhere", font, XBrushes.Black, new XPoint(double.NaN, 100)));

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a finite number*");
    }

    [Fact]
    public void ATransformThatDividesByZeroIsCaughtAtTheFirstThingDrawnThroughIt()
    {
        // The chart defect in miniature. Nothing passed to DrawLine is a NaN; the scale is, and
        // every coordinate that goes through it becomes one. Catching this at the writer is the
        // whole point - the caller's arguments look fine.
        var range = 0.0;
        var scale = 100 / range;                  // infinity
        var drawing = () => Draw(gfx =>
        {
            gfx.ScaleTransform(scale, scale);
            gfx.DrawLine(XPens.Black, 0, 0, 10, 10);
        });

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a finite number*");
    }

    [Fact]
    public void TheMessageQuotesTheOperatorItWouldHaveWritten()
    {
        // "NaN NaN m" names the operator, and where the NaN falls among the operands says which
        // coordinate went wrong. Without that the message would send a reader to the whole of a
        // content stream rather than to one line of their own code.
        var drawing = () => Draw(gfx =>
            gfx.DrawLine(XPens.Black, double.NaN, 10, 40, 40));

        // The quoted operator, not merely the word: the sentence after it says "PDF cannot express
        // NaN or infinity", so matching on "NaN" alone passes whether or not the operator is there
        // at all - which is the one thing this test exists to check.
        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*\"NaN *m\"*");
    }

    [Fact]
    public void ADashPatternThatIsNotNumbersIsRefused()
    {
        // The custom dash pattern is assembled as text a piece at a time rather than formatted in
        // one go, so it reaches the content stream by a route of its own and needs its own check.
        var pen = new XPen(XColors.Black, 2) { DashPattern = new[] { 3.0, double.NaN } };

        var drawing = () => Draw(gfx => gfx.DrawLine(pen, 10, 10, 100, 10));

        drawing.Should().Throw<InvalidOperationException>()
            .WithMessage("*not a finite number*");
    }

    [Fact]
    public void OrdinaryDrawingIsUnaffected()
    {
        var font = new XFont("Liberation Sans", 12);

        var drawing = () => Draw(gfx =>
        {
            gfx.DrawLine(XPens.Black, 10, 10, 100, 100);
            gfx.DrawRectangle(XBrushes.Black, 10, 120, 80, 40);
            gfx.DrawEllipse(XBrushes.Black, 10, 180, 80, 40);
            gfx.DrawString("here", font, XBrushes.Black, new XPoint(10, 250));
            gfx.ScaleTransform(0.5, 0.5);
            gfx.DrawLine(XPens.Black, 10, 10, 100, 100);
        });

        drawing.Should().NotThrow();
    }

    static void Draw(Action<XGraphics> draw)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using var gfx = XGraphics.FromPdfPage(page);
        draw(gfx);
    }
}
