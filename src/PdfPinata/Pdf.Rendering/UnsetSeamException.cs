using System;
using PdfPinata.Fonts;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Carries the exception of a seam that is needed and unset out through page rendering, which
/// otherwise records every exception against the operator it came from and carries on.
/// </summary>
/// <remarks>
/// An unset seam is a mistake in how the library was set up and not a defect in the file, so it
/// is the one thing page rendering lets through. Being a type of its own is what makes that so:
/// the guard round each operator leaves this type alone by name, and
/// <see cref="PageRendering.Render"/> throws <see cref="Seam"/> in its place, so the caller sees
/// the seam's own exception and message.
/// </remarks>
internal sealed class UnsetSeamException : Exception
{
    internal UnsetSeamException(InvalidOperationException seam) : base(seam.Message, seam)
    {
        Seam = seam;
    }

    /// <summary>The exception the seam itself threw.</summary>
    internal InvalidOperationException Seam { get; }
}

/// <summary>
/// The seams page rendering may need, read so that finding one unset ends the render and nothing
/// else does.
/// </summary>
internal static class RequiredSeams
{
    /// <summary>
    /// <c>GlobalFontSettings.FontResolver</c>. A glyph source for a font with no font program
    /// reads the resolver here and nowhere else, and only when a glyph is drawn.
    /// </summary>
    /// <exception cref="UnsetSeamException">No resolver is set.</exception>
    internal static IFontResolver FontResolver
    {
        get
        {
            try
            {
                return GlobalFontSettings.FontResolver;
            }
            catch (InvalidOperationException ex) when (!GlobalFontSettings.IsFontResolverSet)
            {
                throw new UnsetSeamException(ex);
            }
        }
    }
}
