using System.Collections.Generic;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;

namespace PinataLayout.Rendering.Tests.Helpers;

/// <summary>
///   What a page actually shows, as the sequence of glyphs it draws.
/// </summary>
/// <remarks>
///   The text on a page cannot be read back as text. PinataLayout embeds its fonts as Identity-H, so a
///   show-text operator carries glyph identifiers rather than characters - two bytes each - and
///   turning them back into characters would mean reading the embedded font's own tables.
///
///   Comparing sequences sidesteps that. The identifiers are the face's own, so the same
///   characters in the same font always produce the same numbers, and a test can say what a page
///   should read by rendering that text and comparing. It is why the assertions below say
///   <c>Of(page).Should().Equal(For("Page: I"))</c> rather than naming the string outright.
/// </remarks>
internal static class Glyphs
{
    /// <summary>The glyphs the page draws, in the order it draws them.</summary>
    internal static IReadOnlyList<int> On(PdfPage page)
    {
        var glyphs = new List<int>();

        foreach (var run in RunsOn(page))
            glyphs.AddRange(run);

        return glyphs;
    }

    /// <summary>
    ///   The same glyphs, kept in the runs the page draws them in rather than run together.
    /// </summary>
    /// <remarks>
    ///   Needed wherever one piece of text can appear inside another. Searching the flattened
    ///   sequence for the glyphs of "I" finds them inside "III" as readily as on their own, so a
    ///   caller asking where a roman numeral was drawn has to compare whole runs to get an answer
    ///   that is about the numeral rather than about its first letter.
    /// </remarks>
    internal static IReadOnlyList<IReadOnlyList<int>> RunsOn(PdfPage page)
    {
        var runs = new List<IReadOnlyList<int>>();

        foreach (var run in TextOperators.ShownStrings(page))
            runs.Add(GlyphsOf(run));

        return runs;
    }

    /// <summary>
    ///   The glyphs the page draws, in the order a reader sees them rather than the order they
    ///   were written.
    /// </summary>
    /// <remarks>
    ///   The two differ for a right-to-left paragraph, whose words are drawn in the order they
    ///   are written and placed in the order they are read. Which of the two a test wants depends
    ///   on what it is asking: <see cref="On"/> for the reading order a structure tree records,
    ///   this for what the page looks like.
    /// </remarks>
    internal static IReadOnlyList<int> AcrossThePage(PdfPage page)
    {
        var glyphs = new List<int>();

        foreach (var run in TextOperators.ShownAcrossThePage(page))
            glyphs.AddRange(GlyphsOf(run));

        return glyphs;
    }

    /// <summary>
    ///   The glyph runs the page draws with the position each was drawn at, for a test that has to
    ///   say where something landed and not only in what order. A bigger Y is further up the page.
    /// </summary>
    internal static IReadOnlyList<(double X, double Y, IReadOnlyList<int> Run)> PlacedOn(PdfPage page)
    {
        var placed = new List<(double, double, IReadOnlyList<int>)>();

        foreach (var (x, y, run) in TextOperators.ShownWithPositions(page))
            placed.Add((x, y, GlyphsOf(run)));

        return placed;
    }

    /// <summary>
    ///   One show-text operand read as glyph identifiers. Two bytes each - reading it a byte at a
    ///   time shifts everything by half a glyph and produces a sequence that differs everywhere.
    /// </summary>
    static IReadOnlyList<int> GlyphsOf(string run)
    {
        var glyphs = new List<int>();
        for (var idx = 0; idx + 1 < run.Length; idx += 2)
            glyphs.Add((run[idx] << 8) | run[idx + 1]);

        return glyphs;
    }

    /// <summary>
    ///   The glyphs that text draws when it is laid out as plain paragraphs, one per line given.
    ///   Nothing separates the lines in the result: PinataLayout draws one run per word and puts the
    ///   spaces between them in the positioning, so no whitespace glyph is ever shown.
    /// </summary>
    internal static IReadOnlyList<int> For(params string[] lines)
    {
        var document = new PinataLayout.DocumentObjectModel.Document();
        var paragraph = document.AddSection().AddParagraph();

        for (var idx = 0; idx < lines.Length; idx++)
        {
            if (idx > 0)
                paragraph.AddLineBreak();
            paragraph.AddText(lines[idx]);
        }

        return On(Rendered.FirstPageOf(document));
    }
}
