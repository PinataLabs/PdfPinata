using System;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// A colour space, resolved once when the content selects it and then asked to turn each colour
/// set in it into RGB.
/// </summary>
/// <remarks>
/// A new kind of colour space is a new class derived from this one and a branch in
/// <see cref="ColorSpaces"/>. The operators that set colour ask a space only for
/// <see cref="ComponentCount"/> and <see cref="ToRgb"/>, so they do not change.
/// </remarks>
internal abstract class RenderColorSpace
{
    /// <summary>
    /// How many numbers a colour in this space is, or -1 where that is not known, in which case
    /// however many are given are passed on.
    /// </summary>
    internal abstract int ComponentCount { get; }

    /// <summary>
    /// The colour selecting the space sets (ISO 32000-1 8.6.3): every component 0 unless the
    /// space says otherwise.
    /// </summary>
    internal virtual PagePaint InitialColor => ToRgb(new double[Math.Max(ComponentCount, 0)]);

    /// <summary>
    /// The colour as RGB. Never throws: the components are whatever the content gave, as many as
    /// <see cref="ComponentCount"/> where that is known, and may be out of range.
    /// </summary>
    internal abstract PagePaint ToRgb(ReadOnlySpan<double> components);
}

internal sealed class DeviceGrayColorSpace : RenderColorSpace
{
    internal override int ComponentCount => 1;

    internal override PagePaint ToRgb(ReadOnlySpan<double> components) =>
        new(components[0], components[0], components[0]);
}

internal sealed class DeviceRgbColorSpace : RenderColorSpace
{
    internal override int ComponentCount => 3;

    internal override PagePaint ToRgb(ReadOnlySpan<double> components) =>
        new(components[0], components[1], components[2]);
}

/// <summary>
/// DeviceCMYK, turned to RGB by complement and multiply. No profile is applied: the same four
/// numbers are a different colour on every press, and this does not try to choose one.
/// </summary>
internal sealed class DeviceCmykColorSpace : RenderColorSpace
{
    internal override int ComponentCount => 4;

    /// <summary>Black is no ink but the black, where every other space starts at zero.</summary>
    internal override PagePaint InitialColor => new(0, 0, 0);

    internal override PagePaint ToRgb(ReadOnlySpan<double> components)
    {
        var white = 1 - Unit(components[3]);
        return new PagePaint((1 - Unit(components[0])) * white, (1 - Unit(components[1])) * white,
            (1 - Unit(components[2])) * white);
    }

    private static double Unit(double value) => value > 0 ? Math.Min(value, 1) : 0;
}

/// <summary>
/// A space this version cannot resolve. It takes as many components as its family has, so that
/// the colours set in it are read as what they are, and answers one fallback colour for all of
/// them.
/// </summary>
internal sealed class UnsupportedColorSpace : RenderColorSpace
{
    private readonly PagePaint _fallback;

    internal UnsupportedColorSpace(int componentCount, PagePaint fallback)
    {
        ComponentCount = componentCount;
        _fallback = fallback;
    }

    internal override int ComponentCount { get; }

    internal override PagePaint InitialColor => _fallback;

    internal override PagePaint ToRgb(ReadOnlySpan<double> components) => _fallback;
}
