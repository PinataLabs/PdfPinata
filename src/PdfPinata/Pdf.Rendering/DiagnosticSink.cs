using System.Collections.Generic;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// Gathers what one page render left out. This is the one way anything in page rendering reports
/// a problem.
/// </summary>
/// <remarks>
/// A problem met again is counted against the entry it already has, and no more than
/// <see cref="PageRenderResult.MaximumDiagnostics"/> entries are kept. So a page that repeats one
/// bad operator a million times costs one entry, and one that names a million missing resources
/// costs the maximum and a count.
/// </remarks>
internal sealed class DiagnosticSink
{
    private readonly Dictionary<(PageRenderProblem, string), PageRenderDiagnostic> _bySubject = new();
    private readonly List<PageRenderDiagnostic> _inOrder = [];
    private int _omitted;

    /// <summary>The content stream being drawn: <c>page</c>, or the forms drawn to reach it.</summary>
    internal string Stream { get; set; } = "page";

    /// <summary>The position of the operator being drawn, among those of <see cref="Stream"/>.</summary>
    internal int OperatorIndex { get; set; }

    /// <summary>
    /// The name of the operator being drawn, or null where none is: before the first of a stream
    /// and after the last.
    /// </summary>
    internal string Operator { get; set; }

    /// <summary>
    /// The problem and the reason last reported, whatever its subject and whether or not it was
    /// kept: what a reader that caches a failure says again for the next subject it fails.
    /// </summary>
    internal (PageRenderProblem Problem, string Message) Last { get; private set; }

    /// <summary>Records a problem met at the operator being drawn.</summary>
    /// <param name="problem">What sort of problem.</param>
    /// <param name="subject">What it is about. With <paramref name="problem"/>, what makes two reports one entry.</param>
    /// <param name="message">The reason, in plain words.</param>
    internal void Report(PageRenderProblem problem, string subject, string message)
    {
        subject ??= "";
        Last = (problem, message);
        if (_bySubject.TryGetValue((problem, subject), out var known))
        {
            if (known.Occurrences < int.MaxValue)
                known.Occurrences++;
            return;
        }

        if (_inOrder.Count >= PageRenderResult.MaximumDiagnostics)
        {
            if (_omitted < int.MaxValue)
                _omitted++;
            return;
        }

        var diagnostic = new PageRenderDiagnostic(problem, subject, message, Stream, OperatorIndex);
        _bySubject.Add((problem, subject), diagnostic);
        _inOrder.Add(diagnostic);
    }

    internal PageRenderResult ToResult() => new(_inOrder.ToArray(), _omitted);
}
