using System;
using System.Buffers;

namespace PdfPinata.Fonts;

/// <summary>
/// The characters a line of text loses on its way to a glyph, in one place, so that measuring and
/// drawing lose the same ones.
/// </summary>
/// <remarks>
/// <para>
/// The rule is the one <c>FontHelper.MeasureString</c> has always applied and
/// <c>XGraphicsPdfRenderer.DrawString</c> never did: a tab becomes a single space, and every other
/// character below 32 is dropped. Nothing at or above 32 is touched. Drawing used to hand all of
/// them to the face's <c>cmap</c> as ordinary code points, which almost always answers
/// <c>.notdef</c> - so a string that measured as one width was drawn at another, with a box glyph
/// where a tab was meant to be.
/// </para>
/// <para>
/// This knows nothing about a font, a direction or a script, which is why it is not part of
/// <see cref="TextShaping"/>. It also has to run <em>before</em> shaping rather than inside it:
/// both callers keep using the original string afterwards - for the text a run stands for, for
/// what <c>/ToUnicode</c> says and for the WinAnsi bytes - so a shaper that quietly filtered
/// underneath them would hand back cluster indices into a string nobody else has.
/// </para>
/// <para>
/// There is no line splitting here. <c>\n</c> falls into "everything else below 32" and is
/// dropped like the rest; a caller that wants it to mean a line break - which
/// <c>FontHelper.MeasureString</c> does, and <c>XGraphicsPdfRenderer.DrawString</c> deliberately
/// does not - intercepts it before offering the character here. Multi-line text is
/// <c>XTextFormatter</c>'s job and PinataLayout's, and both split before a string reaches either of
/// these two methods.
/// </para>
/// </remarks>
static class TextNormalization
{
    /// <summary>
    /// Says whether a character survives on its way to a glyph, and what it survives as.
    /// </summary>
    /// <param name="ch">The character as written.</param>
    /// <param name="normalized">
    /// What to draw and measure in its place: a space for a tab, the character itself for anything
    /// at or above 32, and undefined when this answers false.
    /// </param>
    /// <returns>False for a character that draws nothing at all.</returns>
    internal static bool TryNormalize(char ch, out char normalized)
    {
        // A tab is the one control character with a width. Nothing here knows about tab stops -
        // that is a layout engine's job - so it is worth exactly the space it has always been
        // measured as.
        if (ch == '\t')
        {
            normalized = ' ';
            return true;
        }

        // Everything else below 32 - \n and \r among them - draws nothing. A face has no glyph for
        // a control character, so the alternative is not a line break but whatever box the face
        // keeps at .notdef.
        if (ch < 32)
        {
            normalized = '\0';
            return false;
        }

        normalized = ch;
        return true;
    }

    /// <summary>
    /// A whole line filtered one character at a time through
    /// <see cref="TryNormalize(char, out char)"/>. Has no split points of its own.
    /// </summary>
    /// <remarks>
    /// Answers <paramref name="text"/> itself - the same reference, and no allocation - when
    /// nothing in it needs filtering. That is every string this library already draws through
    /// <c>XTextFormatter</c>, PinataLayout or the charting renderers, which is what makes this change
    /// provably a no-op for them.
    /// </remarks>
    internal static string NormalizeLine(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var first = -1;
        for (var idx = 0; idx < text.Length; idx++)
        {
            if (text[idx] < 32)
            {
                first = idx;
                break;
            }
        }

        // The common case, and the whole reason for looking before copying.
        if (first < 0)
            return text;

        var buffer = ArrayPool<char>.Shared.Rent(text.Length);
        try
        {
            // Everything before the first control character is already normalized by definition.
            text.CopyTo(0, buffer, 0, first);

            var length = first;
            for (var idx = first; idx < text.Length; idx++)
            {
                if (TryNormalize(text[idx], out var normalized))
                    buffer[length++] = normalized;
            }

            return new string(buffer, 0, length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }
}
