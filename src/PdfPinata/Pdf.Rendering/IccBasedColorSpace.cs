using System;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// <c>[/ICCBased profile]</c> (ISO 32000-1 8.6.5.5), drawn in the space the profile names as its
/// alternate or, failing that, the device space its component count says. The profile is not
/// applied.
/// </summary>
/// <remarks>
/// It is a space of its own, not the device space itself, for one thing: every component of an
/// ICCBased colour starts at zero, and the initial colour of DeviceCMYK is black, not white.
/// </remarks>
internal sealed class IccBasedColorSpace(RenderColorSpace stand) : RenderColorSpace
{
    internal override int ComponentCount => stand.ComponentCount;

    internal override PagePaint ToRgb(ReadOnlySpan<double> components) => stand.ToRgb(components);
}
