using System;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>Which of a page's boxes page rendering draws.</summary>
public enum PageBox
{
    /// <summary>The effective crop box, which is what a viewer shows.</summary>
    Crop,

    /// <summary>The media box.</summary>
    Media,

    /// <summary>The effective bleed box.</summary>
    Bleed,

    /// <summary>The effective trim box.</summary>
    Trim,

    /// <summary>The effective art box.</summary>
    Art
}

/// <summary>
/// Where a page lands on a device: one of its boxes, turned by <c>/Rotate</c>, scaled by
/// <c>/UserUnit</c> and by the caller's scale, cut to a region, with y turned downwards.
/// </summary>
/// <remarks>
/// It is worked out from the page dictionary alone and reads no content. Working it out is PDF
/// work, which is why it is here and not in each device.
/// </remarks>
[Experimental("PPEXP001")]
public sealed class PageView
{
    private PageView(XMatrix transform, XRect box, XRect region, XSize displayedSize, double scale,
        bool boxIsAssumed)
    {
        Transform = transform;
        Box = box;
        Region = region;
        DisplayedSize = displayedSize;
        Scale = scale;
        BoxIsAssumed = boxIsAssumed;
    }

    /// <summary>
    /// Whether the page states no box with any area, so that <see cref="Box"/> is the one
    /// assumed for it. Page rendering reports it.
    /// </summary>
    internal bool BoxIsAssumed { get; }

    /// <summary>
    /// Maps the page's default user space to device space. Page rendering starts every page from
    /// it.
    /// </summary>
    public XMatrix Transform { get; }

    /// <summary>
    /// The box that is drawn, in default user space. Page rendering clips to it before it draws
    /// anything.
    /// </summary>
    public XRect Box { get; }

    /// <summary>
    /// The region that is drawn, in points from the top left of the page as displayed.
    /// </summary>
    public XRect Region { get; }

    /// <summary>
    /// The size of the page as displayed, in points: the box after <c>/Rotate</c> and
    /// <c>/UserUnit</c>.
    /// </summary>
    public XSize DisplayedSize { get; }

    /// <summary>How many device units a point is drawn as. 1 is 72 to the inch.</summary>
    public double Scale { get; }

    /// <summary>
    /// How many device units wide the region is drawn, rounded up to a whole number and at least
    /// 1: the width of a bitmap that holds it.
    /// </summary>
    public int PixelWidth => Pixels(Region.Width);

    /// <summary>
    /// How many device units high the region is drawn, rounded up to a whole number and at least
    /// 1: the height of a bitmap that holds it.
    /// </summary>
    public int PixelHeight => Pixels(Region.Height);

    /// <summary>The view of a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="box">Which of the page's boxes is drawn.</param>
    /// <param name="scale">How many device units a point is drawn as. 1 is 72 to the inch.</param>
    /// <param name="region">
    /// The rectangle to draw, in points from the top left of the page as displayed, which is after
    /// the box, <c>/Rotate</c> and <c>/UserUnit</c> are applied. Null draws the whole box. A
    /// region may reach outside the page, where nothing is drawn.
    /// </param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="scale"/> is not a positive finite number, or <paramref name="box"/> is not
    /// a box.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="region"/> has no area.</exception>
    /// <remarks>
    /// Those are the caller's mistakes. Nothing about the file throws: a box the page states
    /// badly falls back as the effective boxes do, a page that states no box with any area is
    /// taken to be US Letter, as viewers take it, a <c>/Rotate</c> that is not a quarter turn is
    /// no turn, and a <c>/UserUnit</c> that is not a positive number is 1.
    /// </remarks>
    public static PageView Of(PdfPage page, PageBox box = PageBox.Crop, double scale = 1, XRect? region = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (!(scale > 0) || double.IsInfinity(scale))
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "The scale is a positive finite number.");

        var rectangle = BoxOf(page, box);
        var boxIsAssumed = !HasArea(rectangle);
        if (boxIsAssumed)
            rectangle = new XRect(0, 0, 612, 792);

        var userUnit = page.UserUnit;
        var width = rectangle.Width * userUnit;
        var height = rectangle.Height * userUnit;
        var turn = (page.Rotate % 360 + 360) % 360;
        var displayed = turn is 90 or 270 ? new XSize(height, width) : new XSize(width, height);

        var drawn = region ?? new XRect(0, 0, displayed.Width, displayed.Height);
        if (!HasArea(drawn))
            throw new ArgumentException("The region to draw has no area.", nameof(region));

        // The box's corner to the origin and user units to points, with y turned downwards.
        var transform = new XMatrix(userUnit, 0, 0, -userUnit, -rectangle.X * userUnit,
            height + rectangle.Y * userUnit);

        // The quarter turns clockwise a viewer displays the page by, each keeping the page's top
        // left at the origin.
        transform = XMatrix.Multiply(transform, turn switch
        {
            90 => new XMatrix(0, 1, -1, 0, height, 0),
            180 => new XMatrix(-1, 0, 0, -1, width, height),
            270 => new XMatrix(0, -1, 1, 0, 0, width),
            _ => XMatrix.Identity
        });

        transform = XMatrix.Multiply(transform, new XMatrix(scale, 0, 0, scale, -drawn.X * scale, -drawn.Y * scale));
        return new PageView(transform, rectangle, drawn, displayed, scale, boxIsAssumed);
    }

    private static bool HasArea(XRect rectangle) =>
        rectangle.Width > 0 && rectangle.Height > 0 && double.IsFinite(rectangle.Width + rectangle.Height
                                                                      + rectangle.X + rectangle.Y);

    private static XRect BoxOf(PdfPage page, PageBox box)
    {
        var rectangle = box switch
        {
            PageBox.Crop => page.EffectiveCropBox,
            PageBox.Media => page.EffectiveMediaBox,
            PageBox.Bleed => page.EffectiveBleedBox,
            PageBox.Trim => page.EffectiveTrimBox,
            PageBox.Art => page.EffectiveArtBox,
            _ => throw new ArgumentOutOfRangeException(nameof(box), box, "Not a page box.")
        };

        // A box the page does not state is the media box, where that has any area.
        if (rectangle.IsEmpty)
            rectangle = page.EffectiveMediaBox;

        var x1 = Math.Min(rectangle.X1, rectangle.X2);
        var y1 = Math.Min(rectangle.Y1, rectangle.Y2);
        return new XRect(x1, y1, Math.Abs(rectangle.X2 - rectangle.X1), Math.Abs(rectangle.Y2 - rectangle.Y1));
    }

    // Less a hair before rounding up, so that a size that is a whole number but for the last
    // bit of a double does not gain a pixel.
    private int Pixels(double points) => (int)Math.Min(int.MaxValue, Math.Max(1, Math.Ceiling(points * Scale - 1e-9)));
}
