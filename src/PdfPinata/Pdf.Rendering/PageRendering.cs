using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Page rendering: interpreting the content of an existing page and drawing it onto a
/// <see cref="PageDevice"/>.
/// </summary>
[Experimental("PPEXP001")]
public static class PageRendering
{
    /// <summary>Draws a page onto a device.</summary>
    /// <param name="page">The page.</param>
    /// <param name="device">What to draw onto. It is drawn on and never cleared: a background is the caller's to paint.</param>
    /// <param name="view">Where the page lands on the device, or null for the whole crop box at 72 to the inch.</param>
    /// <param name="glyphSources">
    /// Asked for the glyph source of each font dictionary before this library's own sources are,
    /// or null. Answering null for a font leaves it to the library. This is how a caller supplies
    /// a kind of font the library does not read, and how a test sets text without a font file.
    /// Fonts read with it are kept for this call only.
    /// </param>
    /// <param name="annotations">
    /// Whether the normal appearance of each annotation is drawn after the page's content, as a
    /// viewer shows the page. True by default. Hidden and no-view annotations and popups are not
    /// drawn either way.
    /// </param>
    /// <param name="optionalContent">
    /// Which optional content (layers) is drawn: a configuration of the document and groups
    /// switched over it. Null draws the document's default configuration, <c>/D</c>. Content in a
    /// group that is off is not drawn. Hidden content still changes the graphics state, so what it
    /// does to the state is reported as visible content's is. What it would only paint, such as a
    /// shading, an image or a form, is not reported. Text is reported as visible text is.
    /// </param>
    /// <returns>What was left out of the page, and why. Never null.</returns>
    /// <remarks>
    /// <para>
    /// Nothing about the file throws. Content that cannot be drawn is skipped, the reason is in
    /// the result, and the rest of the page is drawn.
    /// </para>
    /// <para>
    /// <b>One page of a <see cref="PdfDocument"/> is rendered at a time.</b> The document reads
    /// its objects as they are asked for and keeps the fonts page rendering has read, and neither
    /// is safe to do from two threads. Pages of different documents render side by side.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> or <paramref name="device"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The page draws a font that has no font program and <c>GlobalFontSettings.FontResolver</c> is
    /// not set. This is the one thing about rendering a page that throws: it is a mistake in how
    /// the library was set up and not a defect in the file.
    /// </exception>
    public static PageRenderResult Render(PdfPage page, PageDevice device, PageView view = null,
        Func<PdfDictionary, PageGlyphSource> glyphSources = null, bool annotations = true,
        PageOptionalContent optionalContent = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(device);

        view ??= PageView.Of(page);

        // Fonts made with the caller's sources are not the document's: kept there, they would
        // be handed to a later render that asked for the library's own.
        var fonts = glyphSources == null && page.Owner != null ? page.Owner.RenderFontCache : new RenderFontCache();
        var interpreter = new ContentInterpreter(device, view, fonts, glyphSources, optionalContent);
        try
        {
            interpreter.RunPage(page, annotations);
        }
        catch (UnsetSeamException unset)
        {
            // Thrown as the seam threw it, so that the caller reads the seam's own message.
            ExceptionDispatchInfo.Capture(unset.Seam).Throw();
        }

        return interpreter.Result;
    }
}
