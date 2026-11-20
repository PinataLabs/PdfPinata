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
/// over unshared, so a device keeps no more of them than page rendering does.
/// </para>
/// <para>
/// <b>An abstract class and not an interface</b>, so that it can grow. Shadings, patterns, blend
/// modes and transparency groups arrive as new <c>virtual</c> members whose base body says the
/// device cannot draw them, as <see cref="DecodeImage"/> does today, and a device written before
/// them goes on compiling and drawing. An interface could only do that with a default member,
/// which the netstandard2.1 runtime this library keeps for Unity does not take.
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
    /// dependency. What it answers is colour already: a codec turns a CMYK or an Adobe-inverted
    /// JPEG to RGB itself. The library applies the image's masks to that, so what
    /// <see cref="DrawImage"/> is handed is always finished samples, whichever way they were
    /// decoded.
    /// </summary>
    /// <param name="codec">What <paramref name="data"/> is encoded as.</param>
    /// <param name="data">The encoded bytes, a complete file of that format.</param>
    /// <returns>
    /// The samples as <see cref="PageImageFormat.Rgba32"/>, or null when the device cannot decode
    /// them. Null is what this base implementation answers, and the image is then skipped and
    /// reported as <see cref="PageRenderProblem.UnsupportedImage"/>.
    /// </returns>
    public virtual PageImage DecodeImage(PageImageCodec codec, ReadOnlyMemory<byte> data) => null;
}
