using System;
using System.Collections.Generic;
using PdfPinata.Fonts;

namespace PdfPinata.Text;

/// <summary>
/// Cuts a paragraph into the runs a shaper can be handed one at a time: each one a single
/// direction and a single script, in the order they are drawn.
/// </summary>
/// <remarks>
/// This is where the bidirectional algorithm and script itemisation meet. Neither is enough on its
/// own - <see cref="BidiAlgorithm"/> says which way each stretch runs but not what script it is,
/// <see cref="ScriptItemizer"/> the other way about - and
/// <see cref="PdfPinata.Fonts.ITextShaper.Shape"/> wants both at once, for a stretch of text
/// that has exactly one of each.
/// </remarks>
public static class TextItemizer
{
    /// <summary>
    /// The runs of a paragraph, in visual order - leftmost first - so that a renderer can draw
    /// them one after another without doing any reordering of its own.
    /// </summary>
    /// <param name="text">The paragraph.</param>
    /// <param name="direction">Which way it runs, or Automatic to read it off the text.</param>
    public static IReadOnlyList<TextRun> Itemize(
        string text, BidiParagraphDirection direction = BidiParagraphDirection.Automatic)
    {
        ArgumentNullException.ThrowIfNull(text);

        var runs = new List<TextRun>();
        if (text.Length == 0)
            return runs;

        var bidi = BidiAlgorithm.Resolve(text, direction);

        foreach (var level in bidi.Runs())
        {
            var first = runs.Count;

            // Script itemisation of this run and nothing else. Asking it of the paragraph and then
            // cutting the answer at these boundaries is a different question with a different
            // answer: UAX #24 sweeps a Common character into the run beside it, and "beside" is not
            // a property the paragraph can settle. The space in "one <U+0645><U+0646>" goes with the
            // Latin when the paragraph is read as one piece, and the bidirectional algorithm then
            // puts that space in the middle of the Arabic - a cut where there is no boundary, and
            // the real boundary left uncut. A run is one direction before it is one script.
            foreach (var script in ScriptItemizer.Itemize(text, level.Start, level.Length))
                runs.Add(new TextRun(script.Start, script.Length, level.Level, script.Script));

            // The pieces of a right-to-left run are drawn right to left: the piece written first is
            // the rightmost. This is the same reordering the algorithm did to the runs themselves,
            // one level further down and for a reason it knows nothing about - a run does not stop
            // being one direction because the script changed half way along it.
            if (level.Direction == XTextDirection.RightToLeft)
                runs.Reverse(first, runs.Count - first);
        }

        return runs;
    }
}

/// <summary>
/// One stretch of a paragraph in a single direction and a single script - a unit of shaping.
/// </summary>
public readonly struct TextRun
{
    internal TextRun(int start, int length, byte level, UnicodeScript script)
    {
        Start = start;
        Length = length;
        Level = level;
        Script = script;
    }

    /// <summary>The index of the first character, in the order the text was written.</summary>
    public int Start { get; }

    /// <summary>How many characters the run covers.</summary>
    public int Length { get; }

    /// <summary>The bidirectional embedding level: even runs left to right, odd right to left.</summary>
    public byte Level { get; }

    /// <summary>The script of the run.</summary>
    public UnicodeScript Script { get; }

    /// <summary>Which way the run is written, which is the parity of its level.</summary>
    public XTextDirection Direction
        => (Level & 1) == 0 ? XTextDirection.LeftToRight : XTextDirection.RightToLeft;

    /// <summary>
    /// The ISO 15924 code of <see cref="Script"/>, lowercased - what
    /// <see cref="PdfPinata.Fonts.ITextShaper.Shape"/> takes.
    /// </summary>
    public string ScriptCode => UnicodeProperties.ScriptCode(Script);

    /// <inheritdoc/>
    public override string ToString()
        => $"[{Start}..{Start + Length - 1}] {Script} {Direction}";
}
