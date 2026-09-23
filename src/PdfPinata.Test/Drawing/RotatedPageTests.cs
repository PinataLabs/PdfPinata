using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.IO;
using Xunit;

namespace PdfPinata.Test.Drawing;

/// <summary>
///   A page can carry a /Rotate entry that tells the viewer to turn it. Drawing on such a page
///   has to land where the reader sees it, not where the media box happens to hold it.
///   See https://github.com/ststeiger/PdfSharpCore/issues/464.
/// </summary>
public class RotatedPageTests
{
    private const double MediaBoxWidth = 612;
    private const double MediaBoxHeight = 792;

    /// <summary>
    ///   The corner of the media box that the viewer shows at the top left of a page turned by
    ///   the given number of degrees. Drawing at the origin has to end up there.
    /// </summary>
    public static TheoryData<int, double, double> TopLeftCorners => new()
    {
        { 0, 0, MediaBoxHeight },                    // stored top left
        { 90, 0, 0 },                                // stored bottom left
        { 180, MediaBoxWidth, 0 },                   // stored bottom right
        { 270, MediaBoxWidth, MediaBoxHeight } // stored top right
    };

    [Theory]
    [MemberData(nameof(TopLeftCorners))]
    public void DrawingAtTheOriginLandsWhereTheViewerShowsTheTopLeftCorner(int rotate, double x, double y)
    {
        var page = ImportedPageWith(rotate);

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("X", new XFont("Arial", 12), XBrushes.Black, new XPoint(0, 0));

        var drawnAt = WhereTheTextLandedInTheStoredPage(page);

        drawnAt.X.Should().BeApproximately(x, 0.001);
        drawnAt.Y.Should().BeApproximately(y, 0.001);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void TheVisiblePageIsAsWideAndAsHighAsTheCallerIsTold(int rotate)
    {
        var page = ImportedPageWith(rotate);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            // Drawing at the opposite corner of the page the caller was told about has to stay
            // inside the media box, whichever way the page is turned.
            gfx.DrawString("X", new XFont("Arial", 12), XBrushes.Black,
                new XPoint(gfx.PageSize.Width, gfx.PageSize.Height));
        }

        var drawnAt = WhereTheTextLandedInTheStoredPage(page);

        drawnAt.X.Should().BeInRange(-0.001, MediaBoxWidth + 0.001);
        drawnAt.Y.Should().BeInRange(-0.001, MediaBoxHeight + 0.001);
    }

    [Theory]
    [MemberData(nameof(TopLeftCorners))]
    public void APageTurnedAfterItWasCreatedBehavesLikeAnImportedOne(int rotate, double x, double y)
    {
        // /Rotate is read when a page is imported, but it can also be set on a page in hand.
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = MediaBoxWidth;
        page.Height = MediaBoxHeight;
        page.Rotate = rotate;

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("X", new XFont("Arial", 12), XBrushes.Black, new XPoint(0, 0));

        var drawnAt = WhereTheTextLandedInTheStoredPage(page);
        drawnAt.X.Should().BeApproximately(x, 0.001);
        drawnAt.Y.Should().BeApproximately(y, 0.001);
    }

    [Theory]
    [InlineData(0, MediaBoxWidth, MediaBoxHeight)]
    [InlineData(90, MediaBoxHeight, MediaBoxWidth)]
    [InlineData(180, MediaBoxWidth, MediaBoxHeight)]
    [InlineData(270, MediaBoxHeight, MediaBoxWidth)]
    public void APageReportsTheSizeTheViewerShows(int rotate, double width, double height)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = MediaBoxWidth;
        page.Height = MediaBoxHeight;
        page.Rotate = rotate;

        page.Width.Point.Should().BeApproximately(width, 0.001);
        page.Height.Point.Should().BeApproximately(height, 0.001);
        // Whichever way it is turned, the media box keeps the size it was given.
        page.StoredSizeOfMediaBox().Should().Be(new XSize(MediaBoxWidth, MediaBoxHeight));
    }

    [Fact]
    public void SettingTheSizeOfATurnedPageSetsTheSizeTheViewerShows()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Rotate = 90;

        page.Width = 1000;
        page.Height = 500;

        page.Width.Point.Should().BeApproximately(1000, 0.001);
        page.Height.Point.Should().BeApproximately(500, 0.001);
        // The viewer turns the page, so the media box holds the two the other way round.
        page.StoredSizeOfMediaBox().Should().Be(new XSize(500, 1000));
    }

    [Fact]
    public void AnUnrotatedPageIsDrawnOnExactlyAsBefore()
    {
        var page = ImportedPageWith(0);

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("X", new XFont("Arial", 12), XBrushes.Black, new XPoint(100, 150));

        // No turn is needed, so no transformation is written at all.
        ContentReader.ReadContent(page).OfType<COperator>()
            .Should().NotContain(op => op.OpCode.OpCodeName == OpCodeName.cm);
        var drawnAt = WhereTheTextLandedInTheStoredPage(page);
        drawnAt.X.Should().BeApproximately(100, 0.001);
        drawnAt.Y.Should().BeApproximately(MediaBoxHeight - 150, 0.001);
    }

    [Fact]
    public void ANewLandscapePageIsDrawnOnExactlyAsBefore()
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = MediaBoxWidth;
        page.Height = MediaBoxHeight;
        page.Orientation = PageOrientation.Landscape;

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString("X", new XFont("Arial", 12), XBrushes.Black, new XPoint(0, 0));

        // The media box of such a page is turned when it is written, so nothing else is needed.
        ContentReader.ReadContent(page).OfType<COperator>()
            .Should().NotContain(op => op.OpCode.OpCodeName == OpCodeName.cm);
        // Its height is the width of the media box that is held in memory.
        WhereTheTextLandedInTheStoredPage(page).Y.Should().BeApproximately(MediaBoxWidth, 0.001);
    }

    /// <summary>
    ///   Builds a page carrying the given /Rotate entry and reads it back, so that it arrives
    ///   through the import path, which is the only one that reads the entry.
    /// </summary>
    private static PdfPage ImportedPageWith(int rotate)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = MediaBoxWidth;
        page.Height = MediaBoxHeight;
        page.Rotate = rotate;

        using var stream = new MemoryStream();
        document.Save(stream, false);
        stream.Position = 0;
        return PdfPinata.Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }

    /// <summary>
    ///   Walks the content of the page and returns the position of the first string it shows,
    ///   in the coordinates of the media box, by applying every transformation on the way.
    /// </summary>
    private static XPoint WhereTheTextLandedInTheStoredPage(PdfPage page)
    {
        var transformations = new List<XMatrix>();
        var position = new XPoint();
        foreach (var op in ContentReader.ReadContent(page).OfType<COperator>())
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.cm:
                    transformations.Add(new XMatrix(
                        Number(op.Operands[0]), Number(op.Operands[1]), Number(op.Operands[2]),
                        Number(op.Operands[3]), Number(op.Operands[4]), Number(op.Operands[5])));
                    break;
                case OpCodeName.BT:
                    position = new XPoint();
                    break;
                case OpCodeName.Td:
                    position += new XVector(Number(op.Operands[0]), Number(op.Operands[1]));
                    break;
                case OpCodeName.Tj:
                    // The matrix written last is the one applied first.
                    for (var i = transformations.Count - 1; i >= 0; i--)
                        position = transformations[i].Transform(position);
                    return position;
            }
        }
        return new XPoint(double.NaN, double.NaN);
    }

    private static double Number(CObject operand)
    {
        return operand is CReal real ? real.Value : ((CInteger)operand).Value;
    }
}

internal static class PdfPageExtensions
{
    /// <summary>
    ///   The size the media box entry holds, which is not the size the page reports when it is
    ///   turned.
    /// </summary>
    public static XSize StoredSizeOfMediaBox(this PdfPage page)
    {
        return new XSize(page.MediaBox.Width, page.MediaBox.Height);
    }
}
