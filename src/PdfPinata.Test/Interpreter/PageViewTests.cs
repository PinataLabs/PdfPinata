using System;
using AwesomeAssertions;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using TUnit.Core;

namespace PdfPinata.Test.Interpreter;

/// <summary>
/// Where a page lands on a device: the box, the turn, the user unit, the scale and the region.
/// </summary>
public class PageViewTests
{
    private static PdfPage Page(Action<PdfPage> configure = null)
    {
        var page = Drawn.PageOf("");
        configure?.Invoke(page);
        return page;
    }

    private static XPoint Map(PageView view, double x, double y) => view.Transform.Transform(new XPoint(x, y));

    [Test]
    public void APageIsDrawnWithItsTopLeftAtTheOriginAndYDownwards()
    {
        var view = PageView.Of(Page());

        Map(view, 0, 100).Should().Be(new XPoint(0, 0));
        Map(view, 200, 0).Should().Be(new XPoint(200, 100));
        view.DisplayedSize.Should().Be(new XSize(200, 100));
        (view.PixelWidth, view.PixelHeight).Should().Be((200, 100));
        view.Box.Should().Be(new XRect(0, 0, 200, 100));
    }

    [Test]
    public void TheCropBoxIsWhatIsDrawnAndItsCornerIsTheOrigin()
    {
        var view = PageView.Of(Page(page => page.CropBox = new PdfRectangle(new XPoint(50, 20), new XPoint(150, 80))));

        Map(view, 50, 80).Should().Be(new XPoint(0, 0));
        Map(view, 150, 20).Should().Be(new XPoint(100, 60));
        view.Box.Should().Be(new XRect(50, 20, 100, 60));
        (view.PixelWidth, view.PixelHeight).Should().Be((100, 60));
    }

    [Test]
    public void TheMediaBoxIsDrawnWhenItIsAskedFor()
    {
        var page = Page(p => p.CropBox = new PdfRectangle(new XPoint(50, 20), new XPoint(150, 80)));

        PageView.Of(page, PageBox.Media).Box.Should().Be(new XRect(0, 0, 200, 100));
    }

    [Test]
    [Arguments(90, 100, 200, 0.0, 0.0, 100.0, 0.0)]
    [Arguments(180, 200, 100, 200.0, 0.0, 200.0, 100.0)]
    [Arguments(270, 100, 200, 100.0, 200.0, 0.0, 200.0)]
    [Arguments(-90, 100, 200, 100.0, 200.0, 0.0, 200.0)]
    public void ATurnedPageIsDrawnTheWayItIsDisplayed(int rotate, int width, int height, double originX,
        double originY, double topLeftX, double topLeftY)
    {
        var view = PageView.Of(Page(page => page.Elements.SetInteger("/Rotate", rotate)));

        view.DisplayedSize.Should().Be(new XSize(width, height));
        (view.PixelWidth, view.PixelHeight).Should().Be((width, height));

        // The lower left corner of the page, and the upper left. A quarter turn clockwise puts
        // the page's left edge along the top, so its lower left corner is the device's origin.
        Map(view, 0, 0).Should().Be(new XPoint(originX, originY));
        Map(view, 0, 100).Should().Be(new XPoint(topLeftX, topLeftY));
    }

    [Test]
    public void TheUserUnitScalesThePage()
    {
        var view = PageView.Of(Page(page => page.Elements.SetReal("/UserUnit", 2)));

        Map(view, 200, 0).Should().Be(new XPoint(400, 200));
        view.DisplayedSize.Should().Be(new XSize(400, 200));
    }

    [Test]
    public void AUserUnitThatIsNotAPositiveNumberIsOne()
    {
        var view = PageView.Of(Page(page => page.Elements.SetReal("/UserUnit", -3)));

        view.DisplayedSize.Should().Be(new XSize(200, 100));
    }

    [Test]
    public void TheScaleIsDeviceUnitsToThePoint()
    {
        var view = PageView.Of(Page(), scale: 1.5);

        Map(view, 200, 0).Should().Be(new XPoint(300, 150));
        (view.PixelWidth, view.PixelHeight).Should().Be((300, 150));
        view.DisplayedSize.Should().Be(new XSize(200, 100), "the displayed size is in points");
    }

    [Test]
    public void ARegionIsInPointsFromTheTopLeftOfThePageAsDisplayed()
    {
        var page = Page(p => p.Elements.SetInteger("/Rotate", 90));

        var view = PageView.Of(page, scale: 2, region: new XRect(10, 20, 30, 40));

        // The displayed point (10, 20) is the device's origin. On a page turned a quarter
        // clockwise, 10 across and 20 down is 20 along the page's x and 10 up its y.
        Map(view, 20, 10).Should().Be(new XPoint(0, 0));
        (view.PixelWidth, view.PixelHeight).Should().Be((60, 80));
        view.Region.Should().Be(new XRect(10, 20, 30, 40));
    }

    [Test]
    [Arguments(0, 200.0, 120.0, 50.0, 80.0, 150.0, 20.0)]
    [Arguments(90, 120.0, 200.0, 50.0, 20.0, 150.0, 80.0)]
    [Arguments(180, 200.0, 120.0, 150.0, 20.0, 50.0, 80.0)]
    [Arguments(270, 120.0, 200.0, 150.0, 80.0, 50.0, 20.0)]
    public void ATurnACropBoxOffTheOriginAUserUnitAScaleAndARegionAllApplyTogether(int rotate, double width,
        double height, double topLeftX, double topLeftY, double bottomRightX, double bottomRightY)
    {
        // A crop box of 100 by 60 whose corner is at (50, 20), in units of two points.
        var page = Page(p =>
        {
            p.CropBox = new PdfRectangle(new XPoint(50, 20), new XPoint(150, 80));
            p.Elements.SetInteger("/Rotate", rotate);
            p.Elements.SetReal("/UserUnit", 2);
        });

        // The corners of the crop box that are displayed top left and bottom right, in the
        // page's own coordinates, which a turn moves round the box.
        var whole = PageView.Of(page);
        whole.DisplayedSize.Should().Be(new XSize(width, height));
        Rounded(Map(whole, topLeftX, topLeftY)).Should().Be(new XPoint(0, 0));
        Rounded(Map(whole, bottomRightX, bottomRightY)).Should().Be(new XPoint(width, height));

        // A region 10 points in from the displayed top left, at two device units to the point.
        var region = PageView.Of(page, PageBox.Crop, 2, new XRect(10, 10, 50, 50));
        Rounded(Map(region, topLeftX, topLeftY)).Should().Be(new XPoint(-20, -20));
        Rounded(Map(region, bottomRightX, bottomRightY)).Should().Be(new XPoint(2 * width - 20, 2 * height - 20));
        (region.PixelWidth, region.PixelHeight).Should().Be((100, 100));
    }

    private static XPoint Rounded(XPoint point) => new(Math.Round(point.X, 6), Math.Round(point.Y, 6));

    [Test]
    public void TheSizeInPixelsIsRoundedUp()
    {
        var view = PageView.Of(Page(), scale: 0.333);

        (view.PixelWidth, view.PixelHeight).Should().Be((67, 34));
    }

    [Test]
    public void AScaleThatIsNotAPositiveFiniteNumberIsTheCallersMistake()
    {
        var page = Page();

        FluentActions.Invoking(() => PageView.Of(page, scale: 0)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PageView.Of(page, scale: double.NaN)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => PageView.Of(page, scale: double.PositiveInfinity)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ARegionWithNoAreaIsTheCallersMistake()
    {
        FluentActions.Invoking(() => PageView.Of(Page(), region: new XRect(0, 0, 0, 10))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void ThePageIsClippedToTheBoxThatIsDrawn()
    {
        var page = Page(p => p.CropBox = new PdfRectangle(new XPoint(50, 20), new XPoint(150, 80)));
        var device = new RecordingDevice();

        PageRendering.Render(page, device);

        device.Lines.Should().Equal("save", "clip nonzero M0,60 L100,60 L100,0 L0,0 Z", "restore");
    }

    [Test]
    public void TheViewGivenIsTheOneThePageIsDrawnUnder()
    {
        var page = Drawn.PageOf("0 0 10 10 re f");
        var device = new RecordingDevice();

        PageRendering.Render(page, device, PageView.Of(page, scale: 2));

        device.Body.Should().Equal("fill nonzero M0,200 L20,200 L20,180 L0,180 Z rgb(0,0,0)");
    }
}
