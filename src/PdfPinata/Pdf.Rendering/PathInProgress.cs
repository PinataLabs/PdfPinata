using PdfPinata.Drawing;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// The path content is building, from the first construction operator to the painting operator
/// that ends it.
/// </summary>
/// <remarks>
/// <para>
/// A point is placed by the transform in force when it is added, and a transform changed later
/// moves the pen and not the points before it. A transform changed in the middle of a path is
/// not allowed and is still found in files.
/// </para>
/// <para>
/// The points are kept in the user space of one transform, the base: the one the path began
/// under. A point added under another transform is taken to the base as it is added, and the
/// whole path is taken to the user space of the painting operator once, when it is painted. So
/// each point is mapped at most three times however often the transform changes. Rewriting
/// the points already in the path at every change made a page of <c>q cm l Q</c> inside one path
/// take time by the square of its length.
/// </para>
/// <para>
/// A transform with no inverse has no user space to take a point back to. While the base is one,
/// points are kept as they are written, which is right for as long as the transform stays. When
/// a point is added under another, the points are re-based once: onto the new transform where it
/// has an inverse, and otherwise onto device space. Either base has an inverse, so it is never
/// re-based again.
/// </para>
/// <para>
/// A path painted under a transform with no inverse is handed over in the base's user space with
/// the base, rather than flattened through a transform that placed none of its points.
/// </para>
/// </remarks>
internal sealed class PathInProgress
{
    private readonly PagePathBuilder _builder = new();

    // The transform the points are kept in the user space of, and its inverse where it has one.
    private XMatrix _base;
    private XMatrix _baseInverse;
    private bool _hasBegun;
    private bool _baseHasInverse;

    // The transform points were last added under, and what takes a point from there to the
    // base. Kept so that a run of points under one transform works it out once.
    private XMatrix _placedUnder;
    private XMatrix _place;
    private bool _places;

    /// <summary>Whether anything has been drawn that leaves a point to draw on from.</summary>
    internal bool HasCurrentPoint => _builder.HasCurrentPoint;

    /// <summary><c>m</c>.</summary>
    internal void MoveTo(XMatrix ctm, double x, double y)
    {
        var point = Placed(ctm, x, y);
        _builder.MoveTo(point.X, point.Y);
    }

    /// <summary><c>l</c>.</summary>
    internal void LineTo(XMatrix ctm, double x, double y)
    {
        var point = Placed(ctm, x, y);
        _builder.LineTo(point.X, point.Y);
    }

    /// <summary><c>c</c>.</summary>
    internal void CubicTo(XMatrix ctm, double x1, double y1, double x2, double y2, double x3, double y3)
    {
        var control1 = Placed(ctm, x1, y1);
        var control2 = Placed(ctm, x2, y2);
        var end = Placed(ctm, x3, y3);
        _builder.CubicTo(control1.X, control1.Y, control2.X, control2.Y, end.X, end.Y);
    }

    /// <summary><c>v</c>: the current point is the first control point.</summary>
    internal void CubicFromCurrentPoint(XMatrix ctm, double x2, double y2, double x3, double y3)
    {
        // Placed first, because placing may re-base the current point along with the rest.
        var control2 = Placed(ctm, x2, y2);
        var end = Placed(ctm, x3, y3);
        var control1 = _builder.CurrentPoint;
        _builder.CubicTo(control1.X, control1.Y, control2.X, control2.Y, end.X, end.Y);
    }

    /// <summary><c>re</c>.</summary>
    internal void AddRectangle(XMatrix ctm, double x, double y, double width, double height)
    {
        MoveTo(ctm, x, y);
        LineTo(ctm, x + width, y);
        LineTo(ctm, x + width, y + height);
        LineTo(ctm, x, y + height);
        _builder.Close();
    }

    /// <summary><c>h</c>.</summary>
    internal void Close() => _builder.Close();

    /// <summary>
    /// The path, in the user space of the transform it is painted under: the one a stroke's pen
    /// is measured in. It changes nothing, and the path can go on being built.
    /// </summary>
    /// <returns>
    /// The path and the transform to paint it under: the one in force where it has an inverse,
    /// and otherwise the base, which puts every point where it was placed. A transform with no
    /// inverse at the painting operator would flatten points it did not place.
    /// </returns>
    internal (PagePath Path, XMatrix Transform) ToPath(XMatrix ctm)
    {
        var path = _builder.ToPath();
        if (path.IsEmpty || ctm == _base)
            return (path, ctm);

        if (ctm.HasInverse)
        {
            var inverse = ctm;
            inverse.Invert();
            var toPaintedSpace = XMatrix.Multiply(_base, inverse);
            if (IsFinite(toPaintedSpace))
                return (path.Transformed(toPaintedSpace), ctm);
        }

        return (path, _base);
    }

    /// <summary>Where a point written under a transform belongs in the base's user space.</summary>
    private XPoint Placed(XMatrix ctm, double x, double y)
    {
        if (!_hasBegun)
        {
            _hasBegun = true;
            _placedUnder = ctm;
            SetBase(ctm);
        }
        else if (ctm != _placedUnder)
        {
            if (!_baseHasInverse)
                Rebase(ctm);

            _placedUnder = ctm;
            _place = XMatrix.Multiply(ctm, _baseInverse);
            _places = ctm != _base && IsFinite(_place);
        }

        return _places ? _place.Transform(new XPoint(x, y)) : new XPoint(x, y);
    }

    /// <summary>
    /// Takes the points kept under a base with no inverse to a base that has one: the transform
    /// now in force where it has an inverse, and device space where it does not either.
    /// </summary>
    private void Rebase(XMatrix ctm)
    {
        if (ctm.HasInverse)
        {
            var inverse = ctm;
            inverse.Invert();
            var toCtm = XMatrix.Multiply(_base, inverse);
            if (IsFinite(toCtm))
            {
                _builder.TransformInPlace(toCtm);
                SetBase(ctm);
                return;
            }
        }

        _builder.TransformInPlace(_base);
        SetBase(XMatrix.Identity);
    }

    private void SetBase(XMatrix matrix)
    {
        _base = matrix;
        _baseHasInverse = matrix.HasInverse;
        _places = false;
        if (_baseHasInverse)
        {
            _baseInverse = matrix;
            _baseInverse.Invert();
        }
    }

    private static bool IsFinite(XMatrix matrix) =>
        double.IsFinite(matrix.M11) && double.IsFinite(matrix.M12) && double.IsFinite(matrix.M21)
        && double.IsFinite(matrix.M22) && double.IsFinite(matrix.OffsetX) && double.IsFinite(matrix.OffsetY);
}
