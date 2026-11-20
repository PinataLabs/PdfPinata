using PdfPinata.Drawing;
using PdfPinata.Pdf.Rendering;
using SkiaSharp;

namespace PdfPinata.Rendering.Skia;

/// <summary>How a page is rendered: which box, at what scale, which part of it, and onto what.</summary>
public sealed class PageRenderOptions
{
    /// <summary>
    /// How many pixels a point is drawn as. 1, the default, is 72 to the inch; 300 to the inch is
    /// 300 / 72.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>Which of the page's boxes is drawn. The default is the effective crop box, which is what a viewer shows.</summary>
    public PageBox Box { get; init; } = PageBox.Crop;

    /// <summary>
    /// The rectangle to draw, in points from the top left of the page as displayed: after the
    /// box, <c>/Rotate</c> and <c>/UserUnit</c> are applied. Null, the default, draws the whole box.
    /// A region may reach outside the page, where only the background is drawn.
    /// </summary>
    public XRect? Region { get; init; }

    /// <summary>
    /// What the region is painted with before the page is drawn. White by default.
    /// <see cref="SKColors.Transparent"/> paints nothing.
    /// </summary>
    public SKColor Background { get; init; } = SKColors.White;

    /// <summary>Whether edges are smoothed. On by default.</summary>
    public bool AntiAlias { get; init; } = true;
}
