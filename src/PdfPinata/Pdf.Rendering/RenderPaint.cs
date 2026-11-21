using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// What the graphics state paints with: one colour, a shader laid on the device, or nothing.
/// </summary>
/// <remarks>
/// The interpreter's paint sites hand this to <see cref="PaintRouter"/> and do not look inside
/// it. Which device call a paint becomes is the router's to say.
/// </remarks>
internal readonly struct RenderPaint
{
    private RenderPaint(PagePaint color, ShadedPaint shaded, bool isNothing)
    {
        Color = color;
        Shaded = shaded;
        IsNothing = isNothing;
    }

    internal RenderPaint(ShadedPaint shaded) : this(new PagePaint(0, 0, 0), shaded, false)
    {
    }

    /// <summary>
    /// The pattern that paints nothing: what a pattern colour space starts at, and what a pattern
    /// that cannot be read comes to.
    /// </summary>
    internal static RenderPaint Nothing { get; } = new(default, null, true);

    /// <summary>The colour where <see cref="Shaded"/> is null. Its alpha is the paint's either way.</summary>
    internal PagePaint Color { get; }

    /// <summary>The shader, or null where <see cref="Color"/> is all there is.</summary>
    internal ShadedPaint Shaded { get; }

    internal bool IsNothing { get; }

    internal RenderPaint WithAlpha(double alpha) => new(Color.WithAlpha(alpha), Shaded, IsNothing);

    public static implicit operator RenderPaint(PagePaint color) => new(color, null, false);
}

/// <summary>A shader where the content put it. Immutable, and made once, at <c>scn</c> or <c>sh</c>.</summary>
internal sealed class ShadedPaint
{
    internal ShadedPaint(PageShader shader, XMatrix toDevice, PagePath box)
    {
        Shader = shader;
        ToDevice = toDevice;
        Box = box;
    }

    internal PageShader Shader { get; }

    /// <summary>
    /// Maps the shader's coordinates to device space: a pattern's <c>/Matrix</c> over the default
    /// space of the content stream that names it, or the transform in force at an <c>sh</c>.
    /// Finite and invertible.
    /// </summary>
    internal XMatrix ToDevice { get; }

    /// <summary>The shading's <c>/BBox</c> in the shader's coordinates, or null where it has none.</summary>
    internal PagePath Box { get; }
}
