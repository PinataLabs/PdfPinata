using System.Collections.Generic;
using System.Linq;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Draws one string on a page of its own and reads back what the renderer wrote.
/// </summary>
/// <remarks>
///   Shared by the tests about shaping and the tests about itemisation, which ask different
///   questions of the same three lines of setup.
/// </remarks>
internal static class DrawnText
{
    /// <summary>A page with one string drawn on it.</summary>
    internal static PdfPage Page(string text, XFont font, XStringFormat format = null)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString(text, font, XBrushes.Black, new XPoint(20, 40),
                format ?? XStringFormats.Default);

        return page;
    }

    /// <summary>
    ///   The glyph identifiers of every run shown on a page, one array per run. The font is
    ///   embedded Identity-H, so the operand of a Tj is glyph numbers rather than characters - two
    ///   bytes each, most significant first, and the reader hands them back a byte at a time.
    /// </summary>
    internal static IReadOnlyList<int[]> GlyphRuns(PdfPage page) =>
        TextOperators.ShownStrings(page)
            .Select(run => Enumerable.Range(0, run.Length / 2)
                .Select(idx => (run[idx * 2] << 8) | run[idx * 2 + 1])
                .ToArray())
            .ToList();

    /// <summary>Every glyph shown on the page, in the order they are drawn.</summary>
    internal static int[] Glyphs(PdfPage page) =>
        GlyphRuns(page).SelectMany(run => run).ToArray();

    /// <summary>The whole content stream of a page, as the renderer wrote it.</summary>
    internal static string ContentOf(PdfPage page) =>
        Encoding.ASCII.GetString(PageContent.Of(page));

    /// <summary>How wide a string measures, which is what the layout engine asks.</summary>
    internal static double MeasuredWidth(string text, XFont font)
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        return gfx.MeasureString(text, font).Width;
    }
}
