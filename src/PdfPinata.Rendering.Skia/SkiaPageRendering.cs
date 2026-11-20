using System;
using System.IO;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Rendering;
using SkiaSharp;

namespace PdfPinata.Rendering.Skia;

/// <summary>
/// Draws the pages of an existing PDF onto SkiaSharp: a canvas, a picture, a bitmap or an encoded
/// image.
/// </summary>
/// <remarks>
/// <para>
/// <b>One page of a <see cref="PdfDocument"/> is rendered at a time.</b> The document reads its
/// objects as they are asked for and keeps the fonts rendering has read, and neither is safe from
/// two threads. Pages of different documents render side by side.
/// </para>
/// <para>
/// The caller owns every canvas, picture and bitmap: what these methods return is the caller's to
/// dispose.
/// </para>
/// <para>
/// Nothing about the file throws. Content that cannot be drawn is skipped and the rest of the page
/// is drawn. <see cref="Render(PdfPage, SKCanvas, PageRenderOptions)"/> and the <c>ToImage</c>
/// methods say what was skipped, in the <see cref="PageRenderResult"/> they return; the methods
/// that return a picture or a bitmap do not, and a caller who needs to know renders onto a canvas
/// of their own.
/// </para>
/// </remarks>
public static class SkiaPageRendering
{
    /// <summary>
    /// Draws a page onto a canvas, with the top left of the region at the canvas's origin.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="canvas">
    /// What to draw onto, under whatever matrix and clip it has. The region is painted with the
    /// background and the drawing is clipped to it. The canvas's matrix, clip and save stack are as
    /// they were when this returns.
    /// </param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <returns>What was left out of the page, and why.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="canvas"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The scale is not a positive finite number, or the box is not a box.</exception>
    /// <exception cref="ArgumentException">The region has no area.</exception>
    /// <exception cref="InvalidOperationException">
    /// The page draws a font that has no font program and <c>GlobalFontSettings.FontResolver</c> is not set.
    /// </exception>
    public static PageRenderResult Render(this PdfPage page, SKCanvas canvas, PageRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(canvas);
        var view = ViewOf(page, options);
        return Render(page, canvas, view, options ?? Defaults);
    }

    /// <summary>Draws a page of a document onto a canvas.</summary>
    /// <inheritdoc cref="Render(PdfPage, SKCanvas, PageRenderOptions)"/>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">Which page, counting from 0.</param>
    /// <param name="canvas">What to draw onto.</param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> names no page of the document.</exception>
    public static PageRenderResult Render(this PdfDocument document, int pageIndex, SKCanvas canvas,
        PageRenderOptions options = null) =>
        PageOf(document, pageIndex).Render(canvas, options);

    /// <summary>
    /// Records a page as a picture, whose cull rectangle is the region in pixels.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <returns>The picture. The caller disposes it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The scale is not a positive finite number, or the box is not a box.</exception>
    /// <exception cref="ArgumentException">The region has no area.</exception>
    /// <exception cref="InvalidOperationException">
    /// The page draws a font that has no font program and <c>GlobalFontSettings.FontResolver</c> is not set.
    /// </exception>
    public static SKPicture ToSKPicture(this PdfPage page, PageRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        var view = ViewOf(page, options);
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(SKRect.Create(view.PixelWidth, view.PixelHeight));
        Render(page, canvas, view, options ?? Defaults);
        return recorder.EndRecording();
    }

    /// <summary>Records a page of a document as a picture.</summary>
    /// <inheritdoc cref="ToSKPicture(PdfPage, PageRenderOptions)"/>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">Which page, counting from 0.</param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> names no page of the document.</exception>
    public static SKPicture ToSKPicture(this PdfDocument document, int pageIndex, PageRenderOptions options = null) =>
        PageOf(document, pageIndex).ToSKPicture(options);

    /// <summary>
    /// Draws a page into a new bitmap that is just large enough to hold the region.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <returns>The bitmap, in the platform's colour type, its alpha premultiplied. The caller disposes it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The scale is not a positive finite number, the box is not a box, or the bitmap would be too
    /// large to allocate.
    /// </exception>
    /// <exception cref="ArgumentException">The region has no area.</exception>
    /// <exception cref="InvalidOperationException">
    /// The page draws a font that has no font program and <c>GlobalFontSettings.FontResolver</c> is not set.
    /// </exception>
    public static SKBitmap ToSKBitmap(this PdfPage page, PageRenderOptions options = null) =>
        ToSKBitmap(page, options, out _);

    /// <summary>Draws a page of a document into a new bitmap.</summary>
    /// <inheritdoc cref="ToSKBitmap(PdfPage, PageRenderOptions)"/>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">Which page, counting from 0.</param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> names no page of the document.</exception>
    public static SKBitmap ToSKBitmap(this PdfDocument document, int pageIndex, PageRenderOptions options = null) =>
        PageOf(document, pageIndex).ToSKBitmap(options);

    /// <summary>
    /// Draws a page and writes it to a stream as an encoded image, PNG or JPEG.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="stream">Where to write the image. It is written to and left open.</param>
    /// <param name="format">The format to encode in.</param>
    /// <param name="quality">
    /// From 0 to 100, for a format that loses detail, such as JPEG. A lossless format ignores it.
    /// </param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <returns>What was left out of the page, and why.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The quality is not from 0 to 100, the scale is not a positive finite number, the box is not
    /// a box, or the bitmap would be too large to allocate.
    /// </exception>
    /// <exception cref="ArgumentException">The region has no area.</exception>
    /// <exception cref="NotSupportedException">SkiaSharp cannot encode <paramref name="format"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The page draws a font that has no font program and <c>GlobalFontSettings.FontResolver</c> is not set.
    /// </exception>
    public static PageRenderResult ToImage(this PdfPage page, Stream stream, SKEncodedImageFormat format,
        int quality = 100, PageRenderOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, 100);
        using var bitmap = ToSKBitmap(page, options, out var result);
        if (!bitmap.Encode(stream, format, quality))
            throw new NotSupportedException($"SkiaSharp cannot encode an image as {format}.");
        return result;
    }

    /// <summary>Draws a page of a document and writes it to a stream as an encoded image.</summary>
    /// <inheritdoc cref="ToImage(PdfPage, Stream, SKEncodedImageFormat, int, PageRenderOptions)"/>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">Which page, counting from 0.</param>
    /// <param name="stream">Where to write the image.</param>
    /// <param name="format">The format to encode in.</param>
    /// <param name="quality">From 0 to 100, for a format that loses detail.</param>
    /// <param name="options">How to render it, or null for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pageIndex"/> names no page of the document, or the quality is not from 0 to 100.
    /// </exception>
    public static PageRenderResult ToImage(this PdfDocument document, int pageIndex, Stream stream,
        SKEncodedImageFormat format, int quality = 100, PageRenderOptions options = null) =>
        PageOf(document, pageIndex).ToImage(stream, format, quality, options);

    private static readonly PageRenderOptions Defaults = new();

    private static PageView ViewOf(PdfPage page, PageRenderOptions options)
    {
        options ??= Defaults;
        return PageView.Of(page, options.Box, options.Scale, options.Region);
    }

    private static PageRenderResult Render(PdfPage page, SKCanvas canvas, PageView view, PageRenderOptions options)
    {
        // Whole pixels, so that the last column and row are painted and clipped all the way across
        // rather than blended into whatever the canvas held.
        var region = SKRect.Create(view.PixelWidth, view.PixelHeight);
        var saved = canvas.Save();
        try
        {
            using (var background = new SKPaint { Color = options.Background, Style = SKPaintStyle.Fill })
                canvas.DrawRect(region, background);
            canvas.ClipRect(region, SKClipOperation.Intersect, antialias: false);

            using var device = new SkiaPageDevice(canvas, options.AntiAlias);
            return PageRendering.Render(page, device, view, annotations: options.Annotations);
        }
        finally
        {
            canvas.RestoreToCount(saved);
        }
    }

    private static SKBitmap ToSKBitmap(PdfPage page, PageRenderOptions options, out PageRenderResult result)
    {
        ArgumentNullException.ThrowIfNull(page);
        var view = ViewOf(page, options);
        var bitmap = new SKBitmap();
        if (!bitmap.TryAllocPixels(new SKImageInfo(view.PixelWidth, view.PixelHeight)))
        {
            bitmap.Dispose();
            throw new ArgumentOutOfRangeException(nameof(options),
                $"A bitmap of {view.PixelWidth} by {view.PixelHeight} pixels could not be allocated. Draw a smaller region or at a smaller scale.");
        }

        try
        {
            bitmap.Erase(SKColors.Transparent);
            using (var canvas = new SKCanvas(bitmap))
                result = Render(page, canvas, view, options ?? Defaults);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static PdfPage PageOf(PdfDocument document, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (pageIndex < 0 || pageIndex >= document.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex,
                $"The document has {document.PageCount} pages, counted from 0.");
        }

        return document.Pages[pageIndex];
    }
}
