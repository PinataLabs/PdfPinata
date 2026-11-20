using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Rendering;

// Path construction, painting and clipping.
internal sealed partial class ContentInterpreter
{
    /// <summary><c>m</c>.</summary>
    private void MoveTo(COperator op)
    {
        Span<double> p = stackalloc double[2];
        if (Read(op, p))
            _path.MoveTo(State.Ctm, p[0], p[1]);
    }

    /// <summary><c>l</c>.</summary>
    private void LineTo(COperator op)
    {
        Span<double> p = stackalloc double[2];
        if (Read(op, p) && HasCurrentPoint(op))
            _path.LineTo(State.Ctm, p[0], p[1]);
    }

    /// <summary><c>c</c>.</summary>
    private void CurveTo(COperator op)
    {
        Span<double> p = stackalloc double[6];
        if (Read(op, p) && HasCurrentPoint(op))
            _path.CubicTo(State.Ctm, p[0], p[1], p[2], p[3], p[4], p[5]);
    }

    /// <summary><c>v</c>: the current point is the first control point.</summary>
    private void CurveFromCurrentPoint(COperator op)
    {
        Span<double> p = stackalloc double[4];
        if (Read(op, p) && HasCurrentPoint(op))
            _path.CubicFromCurrentPoint(State.Ctm, p[0], p[1], p[2], p[3]);
    }

    /// <summary><c>y</c>: the point the curve ends at is the second control point.</summary>
    private void CurveToEndPoint(COperator op)
    {
        Span<double> p = stackalloc double[4];
        if (Read(op, p) && HasCurrentPoint(op))
            _path.CubicTo(State.Ctm, p[0], p[1], p[2], p[3], p[2], p[3]);
    }

    /// <summary><c>re</c>.</summary>
    private void Rectangle(COperator op)
    {
        Span<double> p = stackalloc double[4];
        if (Read(op, p))
            _path.AddRectangle(State.Ctm, p[0], p[1], p[2], p[3]);
    }

    /// <summary>
    /// Whether there is a point for a line or curve to start from. Without one the operator is
    /// reported and skipped, where drawing it from nowhere would put a line on the page that the
    /// content never placed.
    /// </summary>
    private bool HasCurrentPoint(COperator op)
    {
        if (_path.HasCurrentPoint)
            return true;

        _sink.Report(PageRenderProblem.OperatorOutOfPlace, op.Name, "A line or curve is drawn before any point to draw it from.");
        return false;
    }

    /// <summary>
    /// Paints the path built so far and then, where <c>W</c> or <c>W*</c> came before, narrows
    /// the clip to it: the clip takes effect after the painting operator that follows it, and
    /// not at the <c>W</c> (ISO 32000-1 8.5.4). Both are then forgotten.
    /// </summary>
    /// <remarks>
    /// The path is painted in the user space of the transform in force at the painting operator,
    /// so that the pen a stroke is drawn with is measured in the same space as the path. Where
    /// that transform has no inverse, it is painted in the space its points were placed in.
    /// <see cref="PathInProgress"/> says how it gets there.
    /// </remarks>
    /// <param name="fill">The rule to fill by, or null not to fill.</param>
    /// <param name="stroke">Whether to stroke.</param>
    /// <param name="close">Whether to close the open figure first.</param>
    private void Paint(XFillMode? fill, bool stroke, bool close)
    {
        if (close)
            _path.Close();

        var (path, transform) = _path.ToPath(State.Ctm);
        var clip = _pendingClip;
        _path = new PathInProgress();
        _pendingClip = null;

        if (!path.IsEmpty)
        {
            if (fill != null)
                _device.Fill(path, transform, fill.Value, State.FillPaint);
            if (stroke)
                _device.Stroke(path, transform, State.Stroke, State.StrokePaint);
        }

        // A clip to a path with nothing in it leaves nothing to draw in, which is what it says.
        if (clip != null)
            _device.Clip(path, transform, clip.Value);
    }
}
