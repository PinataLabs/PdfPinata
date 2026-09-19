using System;

namespace PdfPinata.Drawing.Layout;

/// <summary>
/// An initial letter set into the opening lines of a block of text, with those lines shortened to
/// leave room for it.
/// </summary>
/// <remarks>
/// <para>
/// The depth is given in <b>lines</b>, not as a font size. Lines are what the surrounding text is
/// measured in, and they are what the eye reads the cap against: a cap is right when its foot rests
/// on a baseline. A size instead implies a depth that is almost never a whole number of lines, and
/// the rounding would happen somewhere the caller cannot see it.
/// </para>
/// <para>
/// So the font supplied here provides the family and the style, and the formatter scales it to
/// span the depth asked for. A caller who wants an exact size should choose the depth that produces
/// it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var formatter = new XTextFormatter(gfx)
/// {
///     DropCap = new XDropCap(new XFont("Times New Roman", 12), lines: 3),
/// };
/// formatter.DrawString(text, body, XBrushes.Black, area);
/// </code>
/// </example>
public sealed class XDropCap
{
    /// <summary>
    /// Initializes a new drop cap.
    /// </summary>
    /// <param name="font">
    /// The family and style the cap is set in. Its size is not used: the formatter scales the cap
    /// to the depth asked for.
    /// </param>
    /// <param name="lines">How many lines of body text the cap is set into. At least one.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lines"/> is less than one.</exception>
    public XDropCap(XFont font, int lines)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (lines < 1)
            throw new ArgumentOutOfRangeException(nameof(lines), lines, "A drop cap is at least one line deep.");

        Font = font;
        Lines = lines;
    }

    /// <summary>
    /// The family and style the cap is set in. The size is worked out from <see cref="Lines"/>.
    /// </summary>
    public XFont Font { get; }

    /// <summary>
    /// How many lines of body text the cap is set into. The cap's foot rests on the baseline of
    /// the last of them.
    /// </summary>
    public int Lines { get; }

    /// <summary>
    /// The space, in points, left between the cap and the text beside it. Null, the default,
    /// leaves the width of a space in the body font.
    /// </summary>
    /// <remarks>
    /// A space rather than a fraction of the cap, because what the gap has to look right against
    /// is the word spacing of the text it sits beside, and that does not change with the size of
    /// the cap.
    /// </remarks>
    public double? Gutter { get; set; }
}
