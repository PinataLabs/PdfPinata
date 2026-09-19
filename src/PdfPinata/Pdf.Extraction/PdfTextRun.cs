using PdfPinata.Drawing;
using PdfPinata.Pdf.Structure;

namespace PdfPinata.Pdf.Extraction;

/// <summary>
/// A run of text as it was drawn: what it says, where its baseline starts, and how large it is.
/// </summary>
/// <remarks>
/// One run per show-text operator, not one per glyph. A glyph-level box needs the advance of every
/// individual glyph, and a run needs only the advance of the whole — so this is what can be given
/// exactly rather than approximately. See the remarks on <see cref="PdfTextExtractor"/>.
/// </remarks>
public sealed class PdfTextRun
{
    internal PdfTextRun(string text, XPoint origin, double width, double fontSize, string fontName,
        PdfTag? tag, string actualText, int? markedContentId, object actualTextScope, bool isArtifact)
    {
        Text = text;
        Origin = origin;
        Width = width;
        FontSize = fontSize;
        FontName = fontName;
        Tag = tag;
        ActualText = actualText;
        MarkedContentId = markedContentId;
        ActualTextScope = actualTextScope;
        IsArtifact = isArtifact;
    }

    /// <summary>
    /// What the run says, decoded through the font's <c>/ToUnicode</c> map where it has one.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Where the baseline of the run starts, in PDF user space — so the origin is the bottom-left
    /// of the page and Y grows upwards, which is the opposite of how a page is usually drawn on.
    /// </summary>
    public XPoint Origin { get; }

    /// <summary>
    /// How wide the run is along its baseline, in user space units.
    /// </summary>
    public double Width { get; }

    /// <summary>
    /// The size the text was drawn at, after the text matrix has had its say — so a run scaled by
    /// its matrix reports the size it appears at rather than the number passed to <c>Tf</c>.
    /// </summary>
    public double FontSize { get; }

    /// <summary>
    /// The resource name of the font, such as <c>/F0</c>. Not the typeface name: a content stream
    /// names fonts by the key they have in the page's resources and by nothing else.
    /// </summary>
    public string FontName { get; }

    /// <summary>
    /// The structure type of the innermost marked-content sequence this run was drawn inside, or
    /// null when it was drawn inside none.
    /// </summary>
    /// <remarks>
    /// Innermost and nothing else: a run inside a heading inside a table cell reports the heading,
    /// not the cell. <see cref="PdfTag.Artifact"/> is a tag like any other here — content that is on
    /// the page but is not part of what it says, which is what <see cref="PdfTextExtractor.ExtractText"/>
    /// skips and this method does not.
    /// </remarks>
    public PdfTag? Tag { get; }

    /// <summary>
    /// Whether this run is nested inside an artifact sequence at any depth — not only when
    /// <see cref="PdfTag.Artifact"/> is the innermost one.
    /// </summary>
    /// <remarks>
    /// A run tagged something else while an ancestor sequence is an artifact is still furniture: an
    /// artifact is not a container its contents can opt out of, so a structural sequence nested
    /// inside one — malformed for this library's own writer, which never nests one there, but not for
    /// PDF in general — does not make the glyphs inside it content. This is what
    /// <see cref="PdfTextExtractor.ExtractText"/> actually excludes on; <see cref="Tag"/> stays the
    /// innermost tag regardless, for a caller who wants to know what that inner sequence claims to be
    /// as well as whether it counts.
    /// </remarks>
    public bool IsArtifact { get; }

    /// <summary>
    /// What the innermost marked-content sequence declaring any <c>/ActualText</c> says this run's
    /// glyphs stand for, or null when none of the sequences this run is nested inside declare one.
    /// </summary>
    /// <remarks>
    /// Not necessarily <see cref="Tag"/>'s own sequence: a plain span inside a sequence that
    /// declares substitute text still reports that text, because the declaration belongs to
    /// whichever sequence made it, however deep the run drawing inside it sits.
    /// </remarks>
    public string ActualText { get; }

    /// <summary>
    /// The <c>/MCID</c> of the innermost marked-content sequence this run was drawn inside, or null
    /// when that sequence carries none or there is no such sequence. The join key between this run
    /// and the structure element it belongs to, for a caller reading the structure tree by hand.
    /// </summary>
    public int? MarkedContentId { get; }

    /// <summary>
    /// The marked-content sequence <see cref="ActualText"/> was read from, or null when it is null.
    /// Not the same object as any sequence <see cref="Tag"/> names unless that one declared the
    /// text itself. Used only to tell two runs that share a declaration from two that do not, so
    /// nothing outside this assembly needs to know what it actually is.
    /// </summary>
    internal object ActualTextScope { get; }

    /// <inheritdoc/>
    public override string ToString() => $"\"{Text}\" at {Origin.X:0.##},{Origin.Y:0.##}";
}
