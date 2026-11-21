using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Turns a paint of the graphics state into the device call that draws it: the solid call for a
/// colour, the shader call for a shading or a pattern, and nothing for the pattern that paints
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// This is the only code that knows a device can be called two ways. The interpreter's paint
/// sites pass <see cref="GraphicsState.FillPaint"/> or <see cref="GraphicsState.StrokePaint"/> and
/// cannot get the choice wrong, because they cannot see it.
/// </para>
/// <para>
/// A shading's <c>/BBox</c> is lowered here to a save, a clip and a restore round the call, so
/// a device never hears of it. A device that answers false to a shader call is handed the solid
/// call in the shader's fallback colour, and the render reports
/// <see cref="PageRenderProblem.UnsupportedFeature"/>.
/// </para>
/// </remarks>
internal sealed class PaintRouter
{
    private readonly DiagnosticSink _sink;

    internal PaintRouter(GuardedDevice device, DiagnosticSink sink)
    {
        Device = device;
        _sink = sink;
    }

    /// <summary>The device, for what is no paint: saves, restores and decoding.</summary>
    internal GuardedDevice Device { get; }

    /// <summary>
    /// Whether the content being drawn is optional content that is switched off. Nothing is
    /// painted, no shader is handed over and no shader the device declines is reported, but a
    /// clip still reaches the device.
    /// </summary>
    internal bool Hidden
    {
        get => Device.Hidden;
        set => Device.Hidden = value;
    }

    internal void Clip(PagePath path, XMatrix transform, XFillMode fillMode) => Device.Clip(path, transform, fillMode);

    internal void Fill(PagePath path, XMatrix transform, XFillMode fillMode, RenderPaint paint)
    {
        if (paint.IsNothing || Hidden)
            return;

        if (paint.Shaded == null)
        {
            Device.Fill(path, transform, fillMode, paint.Color);
            return;
        }

        var boxed = Begin(paint.Shaded);
        if (!Device.TryFill(path, transform, fillMode, ShaderOf(paint)) && Declined(paint, out var fallback))
            Device.Fill(path, transform, fillMode, fallback);
        End(boxed);
    }

    internal void Stroke(PagePath path, XMatrix transform, PageStroke stroke, RenderPaint paint)
    {
        if (paint.IsNothing || Hidden)
            return;

        if (paint.Shaded == null)
        {
            Device.Stroke(path, transform, stroke, paint.Color);
            return;
        }

        var boxed = Begin(paint.Shaded);
        if (!Device.TryStroke(path, transform, stroke, ShaderOf(paint)) && Declined(paint, out var fallback))
            Device.Stroke(path, transform, stroke, fallback);
        End(boxed);
    }

    internal void DrawImage(PageImage image, XMatrix transform, RenderPaint paint)
    {
        if (Hidden)
            return;

        // Only a stencil mask is painted with the fill colour. Any other image takes its alpha.
        if (image.Format != PageImageFormat.Alpha8)
        {
            Device.DrawImage(image, transform, new PagePaint(0, 0, 0, paint.Color.Alpha));
            return;
        }

        if (paint.IsNothing)
            return;

        if (paint.Shaded == null)
        {
            Device.DrawImage(image, transform, paint.Color);
            return;
        }

        var boxed = Begin(paint.Shaded);
        if (!Device.TryDrawStencil(image, transform, ShaderOf(paint)) && Declined(paint, out var fallback))
            Device.DrawImage(image, transform, fallback);
        End(boxed);
    }

    private static PageShaderPaint ShaderOf(RenderPaint paint) =>
        new(paint.Shaded.Shader, paint.Shaded.ToDevice, paint.Color.Alpha);

    private bool Begin(ShadedPaint shaded)
    {
        if (shaded.Box == null)
            return false;

        Device.Save();
        Device.Clip(shaded.Box, shaded.ToDevice, XFillMode.Winding);
        return true;
    }

    private void End(bool boxed)
    {
        if (boxed)
            Device.Restore();
    }

    /// <summary>
    /// Reports a shader the device would not draw, and answers whether there is a colour to draw
    /// in its place.
    /// </summary>
    private bool Declined(RenderPaint paint, out PagePaint fallback)
    {
        var shader = paint.Shaded.Shader;
        _sink.Report(PageRenderProblem.UnsupportedFeature, _sink.Operator == "sh" ? "sh" : "/Pattern",
            "The device does not draw shadings or patterns. What one paints is drawn in a single colour that stands for it.");

        fallback = shader.Fallback.GetValueOrDefault().WithAlpha(paint.Color.Alpha);
        return shader.Fallback != null;
    }
}
