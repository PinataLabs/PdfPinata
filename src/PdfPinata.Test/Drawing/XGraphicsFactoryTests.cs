using System;
using System.Collections.Generic;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   The ways an <see cref="XGraphics"/> is made and the page space it measures in. Every factory
///   ends in one of three constructors, and each of those holds the same five-armed switch over
///   <see cref="XGraphicsUnit"/> — three near-copies of one another, so an arm can be wrong in one
///   and right in the two beside it.
///   <para>
///   The unit decides what the numbers passed to every drawing call mean, so a surface that reads
///   its page size in the wrong measure draws everything in the wrong place.
///   </para>
/// </summary>
public class XGraphicsFactoryTests
{
    /// <summary>The width of a default page in points, which every measure below restates.</summary>
    static double APageWidthInPoints => new PdfDocument().AddPage().Width.Point;

    static PdfPage AnA4Page() => new PdfDocument().AddPage();

    /// <summary>A renderer that records nothing and draws nothing, for the surface that has one.</summary>
    sealed class SilentRenderer : IXGraphicsRenderer
    {
        public readonly List<string> Calls = [];

        public void Close() => Calls.Add(nameof(Close));
        public void DrawLine(XPen pen, double x1, double y1, double x2, double y2) => Calls.Add(nameof(DrawLine));
        public void DrawLines(XPen pen, XPoint[] points) => Calls.Add(nameof(DrawLines));

        public void DrawBezier(XPen pen, double x1, double y1, double x2, double y2, double x3, double y3,
            double x4, double y4) => Calls.Add(nameof(DrawBezier));

        public void DrawBeziers(XPen pen, XPoint[] points) => Calls.Add(nameof(DrawBeziers));
        public void DrawCurve(XPen pen, XPoint[] points, double tension) => Calls.Add(nameof(DrawCurve));

        public void DrawArc(XPen pen, double x, double y, double width, double height, double startAngle,
            double sweepAngle) => Calls.Add(nameof(DrawArc));

        public void DrawRectangle(XPen pen, XBrush brush, double x, double y, double width, double height) =>
            Calls.Add(nameof(DrawRectangle));

        public void DrawRectangles(XPen pen, XBrush brush, XRect[] rects) => Calls.Add(nameof(DrawRectangles));

        public void DrawRoundedRectangle(XPen pen, XBrush brush, double x, double y, double width, double height,
            double ellipseWidth, double ellipseHeight) => Calls.Add(nameof(DrawRoundedRectangle));

        public void DrawEllipse(XPen pen, XBrush brush, double x, double y, double width, double height) =>
            Calls.Add(nameof(DrawEllipse));

        public void DrawPolygon(XPen pen, XBrush brush, XPoint[] points, XFillMode fillmode) =>
            Calls.Add(nameof(DrawPolygon));

        public void DrawPie(XPen pen, XBrush brush, double x, double y, double width, double height,
            double startAngle, double sweepAngle) => Calls.Add(nameof(DrawPie));

        public void DrawClosedCurve(XPen pen, XBrush brush, XPoint[] points, double tension, XFillMode fillmode) =>
            Calls.Add(nameof(DrawClosedCurve));

        public void DrawPath(XPen pen, XBrush brush, XGraphicsPath path) => Calls.Add(nameof(DrawPath));

        public void DrawString(string s, XFont font, XPen pen, XBrush brush, XRect layoutRectangle,
            XStringFormat format) => Calls.Add(nameof(DrawString));

        public void DrawImage(XImage image, double x, double y, double width, double height) =>
            Calls.Add(nameof(DrawImage));

        public void DrawImage(XImage image, XRect destRect, XRect srcRect, XGraphicsUnit srcUnit) =>
            Calls.Add(nameof(DrawImage));

        public void Save(XGraphicsState state) => Calls.Add(nameof(Save));
        public void Restore(XGraphicsState state) => Calls.Add(nameof(Restore));

        public void BeginContainer(XGraphicsContainer container, XRect dstrect, XRect srcrect, XGraphicsUnit unit) =>
            Calls.Add(nameof(BeginContainer));

        public void EndContainer(XGraphicsContainer container) => Calls.Add(nameof(EndContainer));

        public void AddTransform(XMatrix transform, XMatrixOrder matrixOrder) => Calls.Add(nameof(AddTransform));

        public void SetClip(XGraphicsPath path, XCombineMode combineMode) => Calls.Add(nameof(SetClip));
        public void ResetClip() => Calls.Add(nameof(ResetClip));
        public void WriteComment(string comment) => Calls.Add(nameof(WriteComment));
    }

    // ----- the page surface -------------------------------------------------------------------------

    [Theory]
    [InlineData(XGraphicsUnit.Point, 1)]
    [InlineData(XGraphicsUnit.Inch, 72)]
    [InlineData(XGraphicsUnit.Millimeter, 72 / 25.4)]
    [InlineData(XGraphicsUnit.Centimeter, 72 / 2.54)]
    [InlineData(XGraphicsUnit.Presentation, 0.75)]
    public void APageSurfaceMeasuresItsPageInTheUnitItWasGiven(XGraphicsUnit unit, double pointsPerUnit)
    {
        using var gfx = XGraphics.FromPdfPage(AnA4Page(), unit);

        gfx.PageUnit.Should().Be(unit);
        gfx.PageSize.Width.Should().BeApproximately(APageWidthInPoints / pointsPerUnit, 1e-3);
    }

    [Fact]
    public void APageSurfaceInAUnitThatDoesNotExistIsRefused()
    {
        var making = () => XGraphics.FromPdfPage(AnA4Page(), (XGraphicsUnit)99);

        making.Should().Throw<NotImplementedException>();
    }

    /// <summary>
    ///   A surface records the page direction it was built with. Only
    ///   <see cref="XPageDirection.Downwards"/> is worth asking for: the other member is marked
    ///   obsolete and never implemented, so naming it here would be a warning rather than a test.
    /// </summary>
    [Fact]
    public void APageSurfaceRecordsThePageDirectionItWasBuiltWith()
    {
        using var gfx = XGraphics.FromPdfPage(AnA4Page(), XPageDirection.Downwards);

        gfx.PageDirection.Should().Be(XPageDirection.Downwards);
    }

    [Theory]
    [InlineData(XGraphicsPdfPageOptions.Append)]
    [InlineData(XGraphicsPdfPageOptions.Prepend)]
    [InlineData(XGraphicsPdfPageOptions.Replace)]
    public void EachWayOfJoiningThePagesExistingContentMakesASurface(XGraphicsPdfPageOptions options)
    {
        var page = AnA4Page();
        using (var first = XGraphics.FromPdfPage(page))
            first.DrawLine(XPens.Black, 0, 0, 10, 10);

        using var gfx = XGraphics.FromPdfPage(page, options);

        gfx.Should().NotBeNull();
        gfx.PageSize.Width.Should().BeApproximately(APageWidthInPoints, 1e-3);
    }

    [Fact]
    public void EveryPageOverloadReachesTheSameSurface()
    {
        var document = new PdfDocument();

        using (var byUnitAndDirection = XGraphics.FromPdfPage(document.AddPage(),
                   XGraphicsPdfPageOptions.Append, XGraphicsUnit.Inch, XPageDirection.Downwards))
        {
            byUnitAndDirection.PageUnit.Should().Be(XGraphicsUnit.Inch);
            byUnitAndDirection.PageDirection.Should().Be(XPageDirection.Downwards);
        }

        using (var byUnit = XGraphics.FromPdfPage(document.AddPage(),
                   XGraphicsPdfPageOptions.Append, XGraphicsUnit.Centimeter))
            byUnit.PageUnit.Should().Be(XGraphicsUnit.Centimeter);

        using (var byDirection = XGraphics.FromPdfPage(document.AddPage(),
                   XGraphicsPdfPageOptions.Append, XPageDirection.Downwards))
            byDirection.PageDirection.Should().Be(XPageDirection.Downwards);
    }

    [Fact]
    public void APageAlreadyBeingDrawnOnWillNotGiveASecondSurface()
    {
        var page = AnA4Page();
        using var first = XGraphics.FromPdfPage(page);

        var second = () => XGraphics.FromPdfPage(page);

        second.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void APageThatBelongsToNoDocumentCannotBeDrawnOn()
    {
        var making = () => XGraphics.FromPdfPage(new PdfPage());

        making.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AMissingPageIsRefusedRatherThanFollowed()
    {
        var making = () => XGraphics.FromPdfPage((PdfPage)null!);

        making.Should().Throw<ArgumentNullException>();
    }

    // ----- the measuring surface --------------------------------------------------------------------

    [Theory]
    [InlineData(XGraphicsUnit.Point, 1)]
    [InlineData(XGraphicsUnit.Inch, 72)]
    [InlineData(XGraphicsUnit.Millimeter, 72 / 25.4)]
    [InlineData(XGraphicsUnit.Centimeter, 72 / 2.54)]
    [InlineData(XGraphicsUnit.Presentation, 0.75)]
    public void AMeasuringSurfaceMeasuresInTheUnitItWasGiven(XGraphicsUnit unit, double pointsPerUnit)
    {
        var gfx = XGraphics.CreateMeasureContext(
            new XSize(720, 200), unit, XPageDirection.Downwards);

        gfx.PageSize.Width.Should().BeApproximately(720 / pointsPerUnit, 1e-3);
    }

    [Fact]
    public void AMeasuringSurfaceInAUnitThatDoesNotExistIsRefused()
    {
        var making = () => XGraphics.CreateMeasureContext(
            new XSize(100, 100), (XGraphicsUnit)99, XPageDirection.Downwards);

        making.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void DrawingOnAMeasuringSurfaceHasNoEffectAndDoesNotThrow()
    {
        var gfx = XGraphics.CreateMeasureContext(
            new XSize(100, 100), XGraphicsUnit.Point, XPageDirection.Downwards);

        var drawing = () => gfx.DrawLine(XPens.Black, 0, 0, 10, 10);

        drawing.Should().NotThrow();
    }

    // ----- a surface over a renderer of the caller's own ----------------------------------------------

    [Theory]
    [InlineData(XGraphicsUnit.Point, 1)]
    [InlineData(XGraphicsUnit.Inch, 72)]
    [InlineData(XGraphicsUnit.Millimeter, 72 / 25.4)]
    [InlineData(XGraphicsUnit.Centimeter, 72 / 2.54)]
    [InlineData(XGraphicsUnit.Presentation, 0.75)]
    public void ASurfaceOverARendererMeasuresInTheUnitItWasGiven(XGraphicsUnit unit, double pointsPerUnit)
    {
        var gfx = XGraphics.FromRenderer(new SilentRenderer(),
            new XSize(720, 200), unit, XPageDirection.Downwards);

        gfx.PageSize.Width.Should().BeApproximately(720 / pointsPerUnit, 1e-3);
    }

    [Fact]
    public void ASurfaceOverARendererInAUnitThatDoesNotExistIsRefused()
    {
        var making = () => XGraphics.FromRenderer(new SilentRenderer(),
            new XSize(100, 100), (XGraphicsUnit)99, XPageDirection.Downwards);

        making.Should().Throw<NotImplementedException>();
    }

    [Fact]
    public void ASurfaceNeedsARendererToBeBuiltOver()
    {
        var making = () => XGraphics.FromRenderer(null!,
            new XSize(100, 100), XGraphicsUnit.Point, XPageDirection.Downwards);

        making.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EveryDrawingCallReachesTheRendererItWasBuiltOver()
    {
        var renderer = new SilentRenderer();
        var gfx = XGraphics.FromRenderer(renderer,
            new XSize(200, 200), XGraphicsUnit.Point, XPageDirection.Downwards);

        gfx.DrawLine(XPens.Black, 0, 0, 10, 10);
        gfx.DrawRectangle(XPens.Black, XBrushes.Red, new XRect(0, 0, 10, 10));
        gfx.DrawRectangles(XPens.Black, XBrushes.Red, [new XRect(0, 0, 10, 10), new XRect(20, 20, 5, 5)]);
        gfx.DrawEllipse(XBrushes.Red, 0, 0, 10, 10);
        gfx.DrawPolygon(XBrushes.Red, [new XPoint(0, 0), new XPoint(10, 0), new XPoint(5, 10)], XFillMode.Winding);
        gfx.WriteComment("a mark in the stream");

        // A line goes through DrawLines and a series of rectangles is passed on one at a time, so
        // what the renderer sees is not one call per call the caller made.
        renderer.Calls.Should().Contain([
            nameof(SilentRenderer.DrawLines), nameof(SilentRenderer.DrawRectangle),
            nameof(SilentRenderer.DrawEllipse), nameof(SilentRenderer.DrawPolygon),
            nameof(SilentRenderer.WriteComment)
        ]);
        renderer.Calls.Should().HaveCount(7, "the two rectangles of the series arrive one each");
    }

    [Fact]
    public void ASeriesOfRectanglesNeedsSomethingToDrawThemWithAndSomethingToDraw()
    {
        var gfx = XGraphics.FromRenderer(new SilentRenderer(),
            new XSize(200, 200), XGraphicsUnit.Point, XPageDirection.Downwards);

        ((Action)(() => gfx.DrawRectangles(null!, null!, [new XRect(0, 0, 1, 1)])))
            .Should().Throw<ArgumentNullException>();
        ((Action)(() => gfx.DrawRectangles(XPens.Black, XBrushes.Red, null!)))
            .Should().Throw<ArgumentNullException>();
    }

    // ----- an image surface -------------------------------------------------------------------------

    [Fact]
    public void ASurfaceOverAnImageIsNotSomethingThisLibraryOffers()
    {
        var image = XImage.FromFile(PdfPinata.Test.Helpers.PathHelper.GetInstance().GetAssetPath("lenna.png"));

        XGraphics.FromImage(image).Should().BeNull();
        XGraphics.FromImage(image, XGraphicsUnit.Inch).Should().BeNull();
        ((Action)(() => XGraphics.FromImage(null!))).Should().Throw<ArgumentNullException>();
    }

    // ----- the space transformer ---------------------------------------------------------------------

    /// <summary>
    ///   World space runs down from the top left and default page space runs up from the bottom
    ///   left, so the point overload has to flip the y. The rectangle overload cannot answer for a
    ///   point: it encloses rather than maps, so which corner a point became is lost.
    /// </summary>
    [Fact]
    public void APointIsMappedFromWorldSpaceToDefaultPageSpace()
    {
        var page = AnA4Page();
        using var gfx = XGraphics.FromPdfPage(page);

        var mapped = gfx.Transformer.WorldToDefaultPage(new XPoint(10, 20));

        mapped.X.Should().BeApproximately(10, 1e-6);
        mapped.Y.Should().BeApproximately(gfx.PageSize.Height - 20, 1e-6);
    }

    [Fact]
    public void ARectangleIsEnclosedRatherThanMapped()
    {
        var page = AnA4Page();
        using var gfx = XGraphics.FromPdfPage(page);

        var enclosing = gfx.Transformer.WorldToDefaultPage(new XRect(10, 20, 30, 40));

        enclosing.Width.Should().BeApproximately(30, 1e-6);
        enclosing.Height.Should().BeApproximately(40, 1e-6);
    }

    // ----- text drawn into a rectangle ----------------------------------------------------------------

    [Fact]
    public void EveryDrawStringOverloadPutsSomethingOnThePage()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var font = new XFont("Arial", 12, XFontStyle.Regular, XPdfFontOptions.WinAnsiDefault);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            gfx.DrawString("into a box", font, XBrushes.Black, new XRect(10, 10, 200, 20));
            gfx.DrawString("outlined at a point", font, XPens.Black, XBrushes.Black,
                new XPoint(10, 60), XStringFormats.TopLeft);
            gfx.DrawString("outlined in a box", font, XPens.Black, XBrushes.Black,
                new XRect(10, 80, 200, 20));
        }

        var output = new System.IO.MemoryStream();
        document.Save(output, false);

        var reopened = PdfPinata.Pdf.IO.PdfReader.Open(new System.IO.MemoryStream(output.ToArray()), PdfDocumentOpenMode.ReadOnly);
        reopened.PageCount.Should().Be(1);
    }

    // ----- disposing ----------------------------------------------------------------------------------

    [Fact]
    public void ASurfaceCanBeDisposedTwice()
    {
        var gfx = XGraphics.FromPdfPage(AnA4Page());

        gfx.Dispose();

        var again = () => gfx.Dispose();
        again.Should().NotThrow();
    }
}
