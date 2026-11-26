using System;
using System.Diagnostics.CodeAnalysis;
using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// What page rendering draws onto. Everything that is PDF has been resolved before a device is
/// called: operators, resources, colour spaces, fonts, glyph positions and image samples. A device
/// is handed geometry, paint and pixels, and nothing it has to look up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every call carries its own transform</b>, which maps the coordinates of the path or image it
/// comes with straight to device space: the origin at the top left, y downwards, one unit a pixel
/// at the scale the <see cref="PageView"/> was made with. A device keeps no transform and composes
/// none. It sets the matrix it was handed and draws. A stroke is drawn <i>under</i> that matrix
/// and not after it, so that a pen under a transform that scales x and y differently comes out
/// as the ellipse the content asked for.
/// </para>
/// <para>
/// <b>The only state a device keeps is its clip.</b> <see cref="Clip"/> narrows it, and
/// <see cref="Save"/> and <see cref="Restore"/> bracket it. Every <see cref="Save"/> is matched by
/// a <see cref="Restore"/> before <see cref="PageRendering.Render"/> returns, however unbalanced
/// the content's own <c>q</c> and <c>Q</c> are, so a device may map the two onto a canvas's own
/// save stack one for one.
/// </para>
/// <para>
/// <b>There is no member for a glyph.</b> A glyph is a <see cref="Fill"/>, a <see cref="Stroke"/>
/// or part of a <see cref="Clip"/> like any other path, already chosen, placed by the PDF's own
/// widths and split by text render mode. Its <see cref="PagePath"/> is the same instance each time
/// that glyph is drawn and says so through <see cref="PagePath.IsShared"/>, which is what a device
/// caches on. A font that has drawn more outline than page rendering keeps hands its other glyphs
/// over unshared, so a device that caches by <see cref="PagePath.IsShared"/> keeps no more of them
/// than page rendering does. A device that records rather than drawing pixels, such as one over a
/// Skia picture, keeps every path it is handed, unless it draws identical ones as one.
/// </para>
/// <para>
/// <b>An abstract class and not an interface</b>, so that it can grow. Shadings and patterns
/// arrived as <see cref="TryFill"/>, <see cref="TryStroke"/> and <see cref="TryDrawStencil"/>, and
/// blend modes and transparency groups arrive the same way: as new <c>virtual</c> members whose
/// base body says the device cannot draw them, as <see cref="DecodeImage"/> does, so that a
/// device written before them goes on compiling and drawing. An interface could only do that with
/// a default member, which the netstandard2.1 runtime this library keeps for Unity does not take.
/// </para>
/// <para>
/// A device is never called from two threads at once. An exception it throws is recorded as
/// <see cref="PageRenderProblem.OperatorFailed"/> against the operator being drawn and is not
/// passed on, like any other reason an operator could not be carried out. A <see cref="Save"/>
/// that throws is not matched by a <see cref="Restore"/>.
/// </para>
/// </remarks>
[Experimental("PPEXP001")]
public abstract class PageDevice
{
    /// <summary>Saves the clip. Matched by exactly one <see cref="Restore"/>.</summary>
    public abstract void Save();

    /// <summary>Puts the clip back to what it was at the matching <see cref="Save"/>.</summary>
    public abstract void Restore();

    /// <summary>
    /// Narrows the clip to the part of it inside a path. The clip is never widened.
    /// </summary>
    /// <param name="path">The path, in its own coordinates. An empty path clips everything away.</param>
    /// <param name="transform">Maps the path's coordinates to device space.</param>
    /// <param name="fillMode">How the inside of the path is decided.</param>
    public abstract void Clip(PagePath path, XMatrix transform, XFillMode fillMode);

    /// <summary>Fills a path.</summary>
    /// <param name="path">The path, in its own coordinates. Never empty.</param>
    /// <param name="transform">Maps the path's coordinates to device space.</param>
    /// <param name="fillMode">How the inside of the path is decided.</param>
    /// <param name="paint">The colour and alpha to fill with.</param>
    public abstract void Fill(PagePath path, XMatrix transform, XFillMode fillMode, PagePaint paint);

    /// <summary>Strokes a path.</summary>
    /// <param name="path">The path, in its own coordinates. Never empty.</param>
    /// <param name="transform">
    /// Maps the path's coordinates to device space, and the pen with them: the width and the
    /// dashes of <paramref name="stroke"/> are lengths in the path's coordinates.
    /// </param>
    /// <param name="stroke">The line state.</param>
    /// <param name="paint">The colour and alpha to stroke with.</param>
    public abstract void Stroke(PagePath path, XMatrix transform, PageStroke stroke, PagePaint paint);

    /// <summary>Draws an image.</summary>
    /// <param name="image">The decoded samples, every mask already applied to them.</param>
    /// <param name="transform">
    /// Maps the image's sample coordinates to device space: (0, 0) is the top left of the first
    /// row, and (<see cref="PageImage.Width"/>, <see cref="PageImage.Height"/>) the bottom right of
    /// the last. The unit square a PDF draws an image in, and its upward y, are already in it, so
    /// a device draws the samples at the origin under this matrix and turns nothing over.
    /// </param>
    /// <param name="paint">
    /// For <see cref="PageImageFormat.Rgba32"/> only the alpha is read, and it multiplies the
    /// image's own. For <see cref="PageImageFormat.Alpha8"/>, which is a stencil mask, it is the
    /// colour the covered samples are painted in.
    /// </param>
    public abstract void DrawImage(PageImage image, XMatrix transform, PagePaint paint);

    /// <summary>
    /// Decodes image data this library has no decoder for, because it carries no imaging
    /// dependency. What it answers is colour already: a codec turns a CMYK JPEG to RGB itself. The
    /// library applies the image's masks to that, so what <see cref="DrawImage"/> is handed is
    /// always finished samples, whichever way they were decoded.
    /// </summary>
    /// <remarks>
    /// <b>A JPEG's colours are answered with any Adobe inversion already undone.</b> A JPEG of four
    /// components with an Adobe <c>APP14</c> marker stores its inks inverted, as Photoshop writes
    /// them, and a codec such as SkiaSharp's or libjpeg's reads them back the right way round. A
    /// PDF that embeds such a JPEG says <c>/Decode [1 0 1 0 1 0 1 0]</c> to do the same, so that
    /// <c>/Decode</c> is taken as already applied. Where the marker and that <c>/Decode</c> do not
    /// come together, or the <c>/Decode</c> is anything else, the device's RGB cannot be what the
    /// file says, and the image is drawn as the device answers it and reported as
    /// <see cref="PageRenderProblem.UnsupportedFeature"/>. A device must therefore not leave the
    /// inversion in.
    /// </remarks>
    /// <param name="codec">What <paramref name="data"/> is encoded as.</param>
    /// <param name="data">The encoded bytes, a complete file of that format.</param>
    /// <returns>
    /// The samples as <see cref="PageImageFormat.Rgba32"/>, or null when the device cannot decode
    /// them. Null is what this base implementation answers, and the image is then skipped and
    /// reported as <see cref="PageRenderProblem.UnsupportedImage"/>.
    /// </returns>
    public virtual PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data) => null;

    /// <summary>Fills a path with a shader: a shading or a pattern in place of one colour.</summary>
    /// <param name="path">The path, in its own coordinates. Never empty.</param>
    /// <param name="transform">Maps the path's coordinates to device space.</param>
    /// <param name="fillMode">How the inside of the path is decided.</param>
    /// <param name="paint">The shader, with its own transform to device space, and the alpha.</param>
    /// <returns>
    /// True when the fill was drawn. False, which is what this base implementation answers, says
    /// the device cannot draw this shader and has drawn nothing. The path is then filled through
    /// <see cref="Fill"/> in one colour that stands for the shader, and the render reports
    /// <see cref="PageRenderProblem.UnsupportedFeature"/>.
    /// </returns>
    public virtual bool TryFill(PagePath path, XMatrix transform, XFillMode fillMode, PageShaderPaint paint) => false;

    /// <summary>Strokes a path with a shader.</summary>
    /// <param name="path">The path, in its own coordinates. Never empty.</param>
    /// <param name="transform">
    /// Maps the path's coordinates to device space, and the pen with them, as in <see cref="Stroke"/>.
    /// The shader is under its own transform and not this one.
    /// </param>
    /// <param name="stroke">The line state.</param>
    /// <param name="paint">The shader, with its own transform to device space, and the alpha.</param>
    /// <returns>True when the stroke was drawn, as <see cref="TryFill"/> answers.</returns>
    public virtual bool TryStroke(PagePath path, XMatrix transform, PageStroke stroke, PageShaderPaint paint) => false;

    /// <summary>Paints a stencil mask with a shader where the mask is covered.</summary>
    /// <param name="mask">The mask. Always <see cref="PageImageFormat.Alpha8"/>.</param>
    /// <param name="transform">Maps the mask's sample coordinates to device space, as in <see cref="DrawImage"/>.</param>
    /// <param name="paint">The shader, with its own transform to device space, and the alpha.</param>
    /// <returns>True when the mask was drawn, as <see cref="TryFill"/> answers.</returns>
    public virtual bool TryDrawStencil(PageImage mask, XMatrix transform, PageShaderPaint paint) => false;
}
