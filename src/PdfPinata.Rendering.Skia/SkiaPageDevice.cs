using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Rendering;
using SkiaSharp;

namespace PdfPinata.Rendering.Skia;

/// <summary>
/// A <see cref="PageDevice"/> that draws onto an <see cref="SKCanvas"/>.
/// </summary>
/// <remarks>
/// <para>
/// Each call's transform is put onto the canvas whole, after the matrix the canvas had when the
/// device was made. So a caller who has translated or scaled the canvas before rendering finds the
/// page drawn under that, and the device composes nothing of its own.
/// </para>
/// <para>
/// <see cref="Save"/> and <see cref="Restore"/> are the canvas's own. A render leaves the canvas's
/// save stack as it found it.
/// </para>
/// <para>
/// Dispose it when the render is done: it keeps a Skia path for each glyph outline it has drawn
/// and a Skia shader for each shading and pattern. It does not own the canvas.
/// </para>
/// <para>
/// It draws every shader page rendering hands over: gradients as Skia's own, a mesh and a tiling
/// pattern's cell each as a picture that Skia repeats or does not.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public sealed class SkiaPageDevice : PageDevice, IDisposable
{
    private readonly SKCanvas _canvas;
    private readonly SKMatrix _base;
    private readonly bool _antiAlias;
    private readonly SKPaint _paint;
    private readonly Dictionary<PagePath, SKPath> _sharedPaths = [];
    private readonly Dictionary<PageShader, SKShader> _sharedShaders = [];

    // The most samples one image may have, as the core limits one, so that a JPEG whose header
    // claims a vast size is refused before its pixels are allocated.
    private const long MaximumSamples = 1L << 28;

    /// <summary>Initializes a device over a canvas.</summary>
    /// <param name="canvas">What to draw onto. Its current matrix is where the page's device space begins.</param>
    /// <param name="antiAlias">Whether fills, strokes, clips and images are drawn with smoothed edges.</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> is null.</exception>
    public SkiaPageDevice(SKCanvas canvas, bool antiAlias = true)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        _canvas = canvas;
        _base = canvas.TotalMatrix;
        _antiAlias = antiAlias;
        _paint = new SKPaint { IsAntialias = antiAlias };
    }

    /// <inheritdoc />
    public override void Save() => _canvas.Save();

    /// <inheritdoc />
    public override void Restore() => _canvas.Restore();

    /// <inheritdoc />
    public override void Clip(PagePath path, XMatrix transform, XFillMode fillMode)
    {
        SetMatrix(transform);
        var skPath = PathOf(path, fillMode, out var owned);
        using (owned)
            _canvas.ClipPath(skPath, SKClipOperation.Intersect, _antiAlias);
    }

    /// <inheritdoc />
    public override void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint)
    {
        SetMatrix(transform);
        ResetPaint(SKPaintStyle.Fill, paint);
        var skPath = PathOf(path, fillMode, out var owned);
        using (owned)
            _canvas.DrawPath(skPath, _paint);
    }

    /// <inheritdoc />
    public override void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint)
    {
        SetMatrix(transform);
        ResetPaint(SKPaintStyle.Stroke, paint);
        DrawStroke(path, stroke);
    }

    /// <inheritdoc />
    public override void DrawImage(PageImage image, XMatrix transform, PagePaint paint)
    {
        ArgumentNullException.ThrowIfNull(image);
        SetMatrix(transform);

        // Skia paints an alpha-only image in the paint's colour, which is what a stencil mask is,
        // and multiplies any other image by the paint's alpha alone.
        var stencil = image.Format == PageImageFormat.Alpha8;
        ResetPaint(SKPaintStyle.Fill, stencil ? paint : new PagePaint(1, 1, 1, paint.Alpha));
        DrawSamples(image);
    }

    /// <inheritdoc />
    public override bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint)
    {
        using var shader = ShaderOf(paint, transform, out var known);
        if (shader == null)
            return known;

        SetMatrix(transform);
        ResetPaint(SKPaintStyle.Fill, shader, paint.Alpha);
        var skPath = PathOf(path, fillMode, out var owned);
        using (owned)
            _canvas.DrawPath(skPath, _paint);
        _paint.Shader = null;
        return true;
    }

    /// <inheritdoc />
    public override bool TryStroke(PagePath path, XMatrix transform, PageStroke stroke, PageShaderPaint paint)
    {
        using var shader = ShaderOf(paint, transform, out var known);
        if (shader == null)
            return known;

        SetMatrix(transform);
        ResetPaint(SKPaintStyle.Stroke, shader, paint.Alpha);
        DrawStroke(path, stroke);
        _paint.Shader = null;
        return true;
    }

    /// <inheritdoc />
    public override bool TryDrawStencil(PageImage mask, XMatrix transform, PageShaderPaint paint)
    {
        ArgumentNullException.ThrowIfNull(mask);
        if (mask.Format != PageImageFormat.Alpha8)
            return false;

        using var shader = ShaderOf(paint, transform, out var known);
        if (shader == null)
            return known;

        // Skia paints an alpha-only image with the paint's shader, as it does with its colour.
        SetMatrix(transform);
        ResetPaint(SKPaintStyle.Fill, shader, paint.Alpha);
        DrawSamples(mask);
        _paint.Shader = null;
        return true;
    }

    // The path stroked with the paint as it is set, which is a colour's or a shader's.
    private void DrawStroke(PagePath path, PageStroke stroke)
    {
        _paint.StrokeWidth = (float)stroke.Width;
        _paint.StrokeCap = stroke.LineCap switch
        {
            XLineCap.Round => SKStrokeCap.Round,
            XLineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };
        _paint.StrokeJoin = stroke.LineJoin switch
        {
            XLineJoin.Round => SKStrokeJoin.Round,
            XLineJoin.Bevel => SKStrokeJoin.Bevel,
            _ => SKStrokeJoin.Miter
        };
        _paint.StrokeMiter = (float)stroke.MiterLimit;

        var skPath = PathOf(path, XFillMode.Winding, out var owned);
        using (owned)
        using (var dash = DashOf(stroke))
        {
            _paint.PathEffect = dash;
            _canvas.DrawPath(skPath, _paint);
            _paint.PathEffect = null;
        }
    }

    // The samples at the origin, under the matrix and with the paint as they are set.
    private void DrawSamples(PageImage image)
    {
        var stencil = image.Format == PageImageFormat.Alpha8;
        var info = stencil
            ? new SKImageInfo(image.Width, image.Height, SKColorType.Alpha8, SKAlphaType.Premul)
            : new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var skImage = SKImage.FromPixelCopy(info, image.Data.Span);
        if (skImage == null)
            throw new InvalidOperationException($"SkiaSharp could not make an image of {image.Width} by {image.Height} samples.");

        var sampling = _antiAlias
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        _canvas.DrawImage(skImage, 0, 0, sampling, _paint);
    }

    /// <summary>
    /// Decodes a JPEG with <see cref="SKCodec"/>, which turns a CMYK or an Adobe-inverted JPEG to
    /// RGB itself.
    /// </summary>
    /// <param name="codec">What <paramref name="data"/> is encoded as.</param>
    /// <param name="data">The encoded bytes.</param>
    /// <returns>The samples, or null when they are not a JPEG SkiaSharp can decode.</returns>
    public override PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data)
    {
        if (codec != PageImageCodec.Jpeg || data.IsEmpty)
            return null;

        using var skData = SKData.CreateCopy(data.Span);
        using var decoder = SKCodec.Create(skData);
        if (decoder == null || decoder.EncodedFormat != SKEncodedImageFormat.Jpeg)
            return null;

        var size = decoder.Info;
        if (size.Width < 1 || size.Height < 1 || (long)size.Width * size.Height > MaximumSamples)
            return null;

        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        var result = decoder.GetPixels(info, pixels);

        // A truncated JPEG still decodes the rows it has, and the rest are left as they were made.
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            return null;

        return PageImage.FromRgba32(size.Width, size.Height, pixels);
    }

    /// <summary>Frees the paths, the shaders and the paint the device made. The canvas is left alone.</summary>
    public void Dispose()
    {
        foreach (var path in _sharedPaths.Values)
            path.Dispose();
        _sharedPaths.Clear();
        foreach (var shader in _sharedShaders.Values)
            shader.Dispose();
        _sharedShaders.Clear();
        _paint.Dispose();
    }

    private void SetMatrix(XMatrix transform) => _canvas.SetMatrix(_base.PreConcat(Matrix(transform)));

    private static SKMatrix Matrix(XMatrix transform) => new(
        (float)transform.M11, (float)transform.M21, (float)transform.OffsetX,
        (float)transform.M12, (float)transform.M22, (float)transform.OffsetY,
        0, 0, 1);

    private void ResetPaint(SKPaintStyle style, SKShader shader, double alpha)
    {
        _paint.Style = style;
        _paint.Color = SKColors.Black.WithAlpha(Byte(alpha));
        _paint.Shader = shader;
    }

    // The shader a call paints with, placed for the geometry the call draws: the canvas matrix
    // is the geometry's, so the shader's own space is taken through to the geometry's.
    // Null is nothing to draw with: either there is nothing to draw, or, where known is false,
    // the shader is of a kind this device does not draw.
    private SKShader ShaderOf(PageShaderPaint paint, XMatrix geometry, out bool known)
    {
        known = true;
        if (!geometry.HasInverse)
            return null;

        SKShader owned = null;
        if (!_sharedShaders.TryGetValue(paint.Shader, out var shader))
        {
            shader = Build(paint.Shader, out known);
            if (shader == null)
                return null;

            if (paint.Shader.IsShared)
                _sharedShaders.Add(paint.Shader, shader);
            else
                owned = shader;
        }

        using (owned)
        {
            geometry.Invert();
            return shader.WithLocalMatrix(Matrix(XMatrix.Multiply(paint.Transform, geometry)));
        }
    }

    private SKShader Build(PageShader shader, out bool known)
    {
        known = true;
        switch (shader)
        {
            case PageAxialShading axial:
                Stops(axial, false, out var colors, out var positions);
                return OverBackground(
                    SKShader.CreateLinearGradient(Point(axial.Start), Point(axial.End), colors, positions,
                        SKShaderTileMode.Clamp),
                    axial.Background);

            case PageRadialShading radial:
                return OverBackground(Radial(radial), radial.Background);

            case PageMeshShading mesh:
                return Mesh(mesh);

            case PageTiling tiling:
                return Tiling(tiling);

            default:
                known = false;
                return null;
        }
    }

    // Skia extends a gradient past both ends or past neither. An end that is not extended is
    // given a second stop where its first is, transparent, and that is the one carried on.
    // Reversed, the stops run from the end to the start.
    private static void Stops(PageGradient gradient, bool reversed, out SKColor[] colors, out float[] positions)
    {
        var stops = gradient.Stops;
        var extendFirst = reversed ? gradient.ExtendEnd : gradient.ExtendStart;
        var extendLast = reversed ? gradient.ExtendStart : gradient.ExtendEnd;
        var before = extendFirst ? 0 : 1;
        colors = new SKColor[stops.Count + before + (extendLast ? 0 : 1)];
        positions = new float[colors.Length];
        for (var index = 0; index < stops.Count; index++)
        {
            var stop = stops[reversed ? stops.Count - 1 - index : index];
            colors[index + before] = Color(stop.Color);
            positions[index + before] = (float)(reversed ? 1 - stop.Offset : stop.Offset);
        }

        if (!extendFirst)
            colors[0] = colors[1].WithAlpha(0);
        if (!extendLast)
            (colors[^1], positions[^1]) = (colors[^2].WithAlpha(0), 1);
    }

    // Two circles of the gradient may pass through one point. A PDF paints the later of them that
    // it paints at all, and Skia paints the later whether or not it lies past an end that is not
    // extended, which leaves a hole where the earlier one should show: the far side of the last
    // circle, where the circles are not one inside the other. So where an end is not extended the
    // gradient is laid over itself run backwards, which is the earlier circle at every point.
    private static SKShader Radial(PageRadialShading radial)
    {
        SKPoint start = Point(radial.StartCenter), end = Point(radial.EndCenter);
        float startRadius = (float)radial.StartRadius, endRadius = (float)radial.EndRadius;

        Stops(radial, false, out var colors, out var positions);
        var later = SKShader.CreateTwoPointConicalGradient(start, startRadius, end, endRadius, colors, positions,
            SKShaderTileMode.Clamp);
        if (later == null || radial.ExtendStart && radial.ExtendEnd)
            return later;

        Stops(radial, true, out colors, out positions);
        var earlier = SKShader.CreateTwoPointConicalGradient(end, endRadius, start, startRadius, colors, positions,
            SKShaderTileMode.Clamp);
        if (earlier == null)
            return later;

        using (later)
        using (earlier)
            return SKShader.CreateCompose(earlier, later, SKBlendMode.SrcOver);
    }

    // The shader over a colour, as one shader, so that a paint's alpha applies to the two once.
    private static SKShader OverBackground(SKShader shader, PagePaint? background)
    {
        if (shader == null || background == null)
            return shader;

        using (shader)
        using (var under = SKShader.CreateColor(Color(background.Value)))
            return SKShader.CreateCompose(under, shader, SKBlendMode.SrcOver);
    }

    private static SKShader Mesh(PageMeshShading mesh)
    {
        var points = mesh.Points;
        var bytes = mesh.Colors;
        if (points.Length < 3 || !(mesh.Bounds.Width > 0 && mesh.Bounds.Height > 0))
            return mesh.Background == null ? null : SKShader.CreateColor(Color(mesh.Background.Value));

        var vertices = new SKPoint[points.Length];
        var colors = new SKColor[points.Length];
        for (var index = 0; index < vertices.Length; index++)
        {
            vertices[index] = Point(points[index]);
            colors[index] = new SKColor(bytes[3 * index], bytes[3 * index + 1], bytes[3 * index + 2]);
        }

        var bounds = Rect(mesh.Bounds);
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(bounds);
        using (var paint = new SKPaint { Color = SKColors.White })
            canvas.DrawVertices(SKVertexMode.Triangles, vertices, colors, paint);
        using var picture = recorder.EndRecording();
        return OverBackground(PictureShader(picture, bounds, SKShaderTileMode.Decal, SKFilterMode.Linear), mesh.Background);
    }

    // The cell is drawn once, into a picture, by a device like this one, and Skia repeats it.
    private SKShader Tiling(PageTiling tiling)
    {
        var tile = Rect(tiling.Tile);
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(tile);
        using (var cell = new SkiaPageDevice(canvas, _antiAlias))
            tiling.Cell.Replay(cell, XMatrix.Identity);
        using var picture = recorder.EndRecording();
        return PictureShader(picture, tile, SKShaderTileMode.Repeat, _antiAlias ? SKFilterMode.Linear : SKFilterMode.Nearest);
    }

    // Skia lays a picture shader's tile with its corner at the origin, wherever the tile is in
    // the picture, so it is moved back to where the picture has it.
    private static SKShader PictureShader(SKPicture picture, SKRect tile, SKShaderTileMode mode, SKFilterMode filter) =>
        SKShader.CreatePicture(picture, mode, mode, filter, SKMatrix.CreateTranslation(tile.Left, tile.Top), tile);

    private static SKColor Color(PagePaint paint) => new(Byte(paint.Red), Byte(paint.Green), Byte(paint.Blue));

    private static SKRect Rect(XRect rect) =>
        new((float)rect.X, (float)rect.Y, (float)(rect.X + rect.Width), (float)(rect.Y + rect.Height));

    private void ResetPaint(SKPaintStyle style, PagePaint paint)
    {
        _paint.Style = style;
        _paint.Color = new SKColor(Byte(paint.Red), Byte(paint.Green), Byte(paint.Blue), Byte(paint.Alpha));
    }

    private static byte Byte(double unit) => (byte)Math.Round(unit * 255);

    private static SKPathEffect DashOf(PageStroke stroke)
    {
        var pattern = stroke.DashPattern;
        if (pattern.Count == 0)
            return null;

        var intervals = new float[pattern.Count];
        for (var i = 0; i < intervals.Length; i++)
            intervals[i] = (float)pattern[i];
        return SKPathEffect.CreateDash(intervals, (float)stroke.DashPhase);
    }

    // The path a call draws. A glyph outline is handed over again, so its path is kept until the
    // device is disposed; any other is made for this call, and handed back as owned to be freed.
    private SKPath PathOf(PagePath path, XFillMode fillMode, out SKPath owned)
    {
        ArgumentNullException.ThrowIfNull(path);
        owned = null;
        if (!_sharedPaths.TryGetValue(path, out var skPath))
        {
            skPath = Build(path);
            if (path.IsShared)
                _sharedPaths.Add(path, skPath);
            else
                owned = skPath;
        }

        skPath.FillType = fillMode == XFillMode.Alternate ? SKPathFillType.EvenOdd : SKPathFillType.Winding;
        return skPath;
    }

    private static SKPath Build(PagePath path)
    {
        using var builder = new SKPathBuilder();
        var points = path.Points;
        var next = 0;
        foreach (var verb in path.Verbs)
        {
            switch (verb)
            {
                case PagePathVerb.MoveTo:
                    builder.MoveTo(Point(points[next++]));
                    break;
                case PagePathVerb.LineTo:
                    builder.LineTo(Point(points[next++]));
                    break;
                case PagePathVerb.CubicTo:
                    builder.CubicTo(Point(points[next]), Point(points[next + 1]), Point(points[next + 2]));
                    next += 3;
                    break;
                case PagePathVerb.Close:
                    builder.Close();
                    break;
            }
        }

        return builder.Detach();
    }

    private static SKPoint Point(XPoint point) => new((float)point.X, (float)point.Y);
}
