using System.Collections.Generic;
using System.Globalization;

namespace PdfPinata.Pdf.Rendering;

/// <summary>What page rendering left out of a page, and why.</summary>
/// <remarks>
/// Real files are often slightly broken, and a page that throws at the first bad operator cannot
/// be drawn at all. So whatever cannot be drawn is skipped, and the reason is kept here.
/// </remarks>
public sealed class PageRenderResult
{
    /// <summary>
    /// How many different problems <see cref="Diagnostics"/> holds at most, so that a hostile page
    /// costs a bounded amount of memory to describe.
    /// </summary>
    public const int MaximumDiagnostics = 256;

    internal PageRenderResult(IReadOnlyList<PageRenderDiagnostic> diagnostics, int omittedDiagnostics)
    {
        Diagnostics = diagnostics;
        OmittedDiagnostics = omittedDiagnostics;
    }

    /// <summary>
    /// One entry for each different problem, in the order each was first met. A problem met
    /// again raises <see cref="PageRenderDiagnostic.Occurrences"/> rather than adding an entry: a
    /// font that cannot be drawn is one entry, however many glyphs of it the page shows.
    /// </summary>
    public IReadOnlyList<PageRenderDiagnostic> Diagnostics { get; }

    /// <summary>
    /// How many problems were met after <see cref="Diagnostics"/> was full. They are counted and
    /// not described.
    /// </summary>
    public int OmittedDiagnostics { get; }

    /// <summary>
    /// Whether nothing was left out: the page was drawn as far as this library can draw one.
    /// </summary>
    public bool IsComplete => Diagnostics.Count == 0 && OmittedDiagnostics == 0;
}

/// <summary>One kind of thing page rendering left out of a page.</summary>
public sealed class PageRenderDiagnostic
{
    internal PageRenderDiagnostic(PageRenderProblem problem, string subject, string message, string stream,
        int operatorIndex)
    {
        Problem = problem;
        Subject = subject;
        Message = message;
        Stream = stream;
        OperatorIndex = operatorIndex;
        Occurrences = 1;
    }

    /// <summary>What sort of problem it is.</summary>
    public PageRenderProblem Problem { get; }

    /// <summary>
    /// What the problem is about: an operator (<c>cm</c>), a resource name (<c>/F1</c>) or a
    /// colour space family (<c>/Separation</c>). Two problems of one sort about one subject are
    /// one entry.
    /// </summary>
    public string Subject { get; }

    /// <summary>The reason, in plain words, as it was the first time.</summary>
    public string Message { get; }

    /// <summary>
    /// Which content stream the problem was first met in: <c>page</c>, or the form XObjects drawn
    /// to reach it, as in <c>page/Fm1/Fm2</c>.
    /// </summary>
    public string Stream { get; }

    /// <summary>
    /// The position, among the operators of <see cref="Stream"/>, of the operator the problem was
    /// first met at, counting from 0.
    /// </summary>
    public int OperatorIndex { get; }

    /// <summary>How many times the problem was met. At least 1.</summary>
    public int Occurrences { get; internal set; }

    /// <summary>Returns the problem, its subject, where it was first met and the reason.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture,
        "{0} {1} at {2} operator {3}{4}: {5}", Problem, Subject, Stream, OperatorIndex,
        Occurrences > 1 ? " (" + Occurrences.ToString(CultureInfo.InvariantCulture) + " times)" : "", Message);
}

/// <summary>The sorts of problem page rendering records.</summary>
/// <remarks>Members are added over time, so a <c>switch</c> over them needs a default.</remarks>
public enum PageRenderProblem
{
    /// <summary>
    /// A content stream could not be read: its filter failed and nothing of it was drawn, or part
    /// of it could not be parsed and was read past.
    /// </summary>
    UnreadableContent,

    /// <summary>An operator had too few operands, or operands of the wrong kind, and was skipped.</summary>
    WrongOperands,

    /// <summary>A name that is no operator, outside <c>BX</c> and <c>EX</c>.</summary>
    UnknownOperator,

    /// <summary>
    /// An operator where it does not belong: text shown outside a text object, or a <c>Q</c> with
    /// nothing saved.
    /// </summary>
    OperatorOutOfPlace,

    /// <summary>A resource the content names is missing, or is not what it is named as.</summary>
    MissingResource,

    /// <summary>
    /// A limit that keeps a malformed page from running away was reached: a form XObject drawing
    /// itself, forms nested too deep, too many states saved, or too many faults in one stream.
    /// </summary>
    LimitReached,

    /// <summary>A value out of range, such as a negative line width, was left as it was before.</summary>
    InvalidValue,

    /// <summary>A colour space this version cannot resolve. What it colours is drawn in a fallback colour.</summary>
    UnsupportedColorSpace,

    /// <summary>A font this version has no glyph outlines for. Its text is not drawn.</summary>
    UnsupportedFont,

    /// <summary>A font that could be read, but not a glyph asked of it.</summary>
    MissingGlyph,

    /// <summary>An image this version cannot decode, or the device could not.</summary>
    UnsupportedImage,

    /// <summary>
    /// Something recognised and not drawn yet: a shading, a pattern, a blend mode, a soft mask.
    /// </summary>
    UnsupportedFeature,

    /// <summary>
    /// An operator that could not be carried out for a reason none of the others names. The
    /// message is that of the exception it ended in.
    /// </summary>
    OperatorFailed
}
