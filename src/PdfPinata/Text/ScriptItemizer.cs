using System;
using System.Collections.Generic;

namespace PdfPinata.Text;

/// <summary>
/// Splits mixed text into runs of a single script - "this much is Arabic, this much is Latin" -
/// which is one half of the unit a shaper can handle at a time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Internal, and asked of a window rather than of a paragraph.</b> Sweeping a Common character
/// into whatever it is beside only means something inside a stretch of text already resolved to one
/// direction: asked of the whole of "one <c>&#x0645;&#x0646;</c>", the space goes with the Latin
/// that precedes it, and <see cref="BidiAlgorithm"/> then puts that space in the middle of the
/// Arabic. A cut where there is no boundary, and the real boundary left uncut. So the only caller is
/// <see cref="TextItemizer"/>, which asks once per bidirectional run, and
/// <see cref="TextItemizer.Itemize"/> is the supported way to ask the question of a paragraph.
/// </para>
/// <para>
/// UAX #24. The whole of the difficulty is that most punctuation, all spaces and every digit have
/// script <see cref="UnicodeScript.Common"/>, and combining marks have
/// <see cref="UnicodeScript.Inherited"/>: neither says anything about which script the text is in,
/// and both have to be swept into whichever run they find themselves next to. A full stop after
/// Arabic belongs to the Arabic run; the same full stop after Latin belongs to the Latin one.
/// </para>
/// <para>
/// <b>What this does not do:</b> the Script_Extensions property. A character can belong to several
/// scripts at once - U+0640 Arabic tatweel is used by Syriac and Adlam too - and <c>scx</c> is the
/// property that says so. This reads <c>sc</c> alone, which puts such a character in the script it
/// is named for rather than in whichever neighbouring script also claims it. The visible cost is a
/// run boundary where there need not be one, never a wrong glyph, and adding <c>scx</c> later is a
/// third generated table and no change to any caller.
/// </para>
/// </remarks>
internal static class ScriptItemizer
{
    /// <summary>
    /// The runs of a whole string, in written order, with indices into the string.
    /// </summary>
    public static IReadOnlyList<ScriptRun> Itemize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Itemize(text, 0, text.Length);
    }

    /// <summary>
    /// The runs of one window of a string, in written order, with indices into the whole string
    /// rather than into the window.
    /// </summary>
    /// <remarks>
    /// The window is how <see cref="TextItemizer"/> asks this of one bidirectional run instead of
    /// the paragraph. No substring is taken - the walk reads <paramref name="text"/> between the
    /// bounds directly - because the caller is on the path every <c>DrawString</c> and
    /// <c>MeasureString</c> goes through, and a string plus a list per bidirectional run is a price
    /// that path does not have to pay.
    /// </remarks>
    /// <param name="text">The string the window is cut out of.</param>
    /// <param name="start">The index the window begins at.</param>
    /// <param name="length">How many characters of <paramref name="text"/> it covers.</param>
    public static IReadOnlyList<ScriptRun> Itemize(string text, int start, int length)
    {
        ValidateWindow(text, start, length);

        var runs = new List<ScriptRun>();
        if (length == 0)
            return runs;

        var end = start + length;
        var script = UnicodeScript.Common;
        var from = start;

        for (var idx = start; idx < end;)
        {
            var here = UnicodeProperties.ScriptOf(CodePointAt(text, idx, end, out var width));

            // Inherited and Common are carried by whatever they are next to. If the run has no
            // script yet, such a character does not give it one either - it waits for the first
            // character that does, which then takes the punctuation before it with it.
            if (here is not (UnicodeScript.Inherited or UnicodeScript.Common))
            {
                if (script == UnicodeScript.Common)
                {
                    // The run had nothing but Common and Inherited so far, so it becomes this
                    // script retroactively rather than starting a new one here.
                    script = here;
                }
                else if (here != script)
                {
                    runs.Add(new ScriptRun(from, idx - from, script));
                    from = idx;
                    script = here;
                }
            }

            idx += width;
        }

        runs.Add(new ScriptRun(from, end - from, script));
        return runs;
    }

    private static void ValidateWindow(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || start > text.Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0 || length > text.Length - start)
            throw new ArgumentOutOfRangeException(nameof(length));
    }

    /// <summary>
    /// The code point at <paramref name="idx"/>, and in <paramref name="width"/> how many chars it
    /// takes: two for a surrogate pair, one for anything else.
    /// </summary>
    private static int CodePointAt(string text, int idx, int end, out int width)
    {
        // The pair is looked for inside the window, so a window ending between the halves of a
        // surrogate pair reads what is left of it as itself rather than reaching past its own
        // end. char.ConvertToUtf32 throws on a lone surrogate, which is not an answer a layout
        // path can use.
        var isPair = char.IsHighSurrogate(text[idx]) && idx + 1 < end && char.IsLowSurrogate(text[idx + 1]);
        width = isPair ? 2 : 1;
        return isPair ? char.ConvertToUtf32(text[idx], text[idx + 1]) : text[idx];
    }
}

/// <summary>
/// One stretch of text in a single script.
/// </summary>
internal readonly struct ScriptRun
{
    internal ScriptRun(int start, int length, UnicodeScript script)
    {
        Start = start;
        Length = length;
        Script = script;
    }

    /// <summary>The index of the first character of the run.</summary>
    public int Start { get; }

    /// <summary>How many characters the run covers.</summary>
    public int Length { get; }

    /// <summary>
    /// The script. <see cref="UnicodeScript.Common"/> for a run that held nothing but punctuation,
    /// spaces and digits, which is what text with no letters in it comes to.
    /// </summary>
    public UnicodeScript Script { get; }

    /// <summary>
    /// The ISO 15924 code of <see cref="Script"/>, lowercased - the form
    /// <see cref="PdfPinata.Fonts.ITextShaper"/> is told a run's script in.
    /// </summary>
    public string ScriptCode => UnicodeProperties.ScriptCode(Script);

    /// <inheritdoc/>
    public override string ToString() => $"[{Start}..{Start + Length - 1}] {Script}";
}
