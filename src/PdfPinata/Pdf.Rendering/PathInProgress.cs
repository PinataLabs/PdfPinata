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
/// The points are kept in the user space the path began in. A point added under another
/// transform is taken to that space as it is added, and the whole path is taken to the user
/// space of the painting operator once, when it is painted. So each point is mapped at most
/// twice however often the transform changes. Rewriting the points already in the path at every
/// change made a page of <c>q cm l Q</c> inside one path take time by the square of its length.
/// </para>
/// </remarks>
internal sealed class PathInProgress
{
    private readonly PagePathBuilder _builder = new();

    // The transform the path began under, and its inverse where it has one.
    private XMatrix _began;
    private XMatrix _beganInverse;
    private bool _hasBegun;
    private bool _beganHasInverse;

    // The transform points were last added under, and what takes a point from there to where
    // the path began. Kept so that a run of points under one transform works it out once.
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

    /// <summary><c>v</c>: the current point, which is already where it belongs, is the first control point.</summary>
    internal void CubicFromCurrentPoint(XMatrix ctm, double x2, double y2, double x3, double y3)
    {
        var control1 = _builder.CurrentPoint;
        var control2 = Placed(ctm, x2, y2);
        var end = Placed(ctm, x3, y3);
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
    /// is measured in.
    /// </summary>
    internal PagePath ToPath(XMatrix ctm)
    {
        var path = _builder.ToPath();
        if (path.IsEmpty || ctm == _began || !ctm.HasInverse)
            return path;

        // Under a transform with no inverse everything lands on one line or one point wherever
        // it is, so the path is handed over as it stands.
        ctm.Invert();
        var toPaintedSpace = XMatrix.Multiply(_began, ctm);
        return IsFinite(toPaintedSpace) ? path.Transformed(toPaintedSpace) : path;
    }

    /// <summary>Where a point written under a transform belongs in the space the path began in.</summary>
    private XPoint Placed(XMatrix ctm, double x, double y)
    {
        if (!_hasBegun)
        {
            _hasBegun = true;
            _began = _placedUnder = ctm;
            _beganHasInverse = ctm.HasInverse;
            if (_beganHasInverse)
            {
                _beganInverse = ctm;
                _beganInverse.Invert();
            }
        }
        else if (ctm != _placedUnder)
        {
            _placedUnder = ctm;
            _place = _beganHasInverse ? XMatrix.Multiply(ctm, _beganInverse) : default;

            // A path begun under a transform with no inverse has no space to be taken back to,
            // and its points are kept as they are written.
            _places = _beganHasInverse && ctm != _began && IsFinite(_place);
        }

        return _places ? _place.Transform(new XPoint(x, y)) : new XPoint(x, y);
    }

    private static bool IsFinite(XMatrix matrix) =>
        double.IsFinite(matrix.M11) && double.IsFinite(matrix.M12) && double.IsFinite(matrix.M21)
        && double.IsFinite(matrix.M22) && double.IsFinite(matrix.OffsetX) && double.IsFinite(matrix.OffsetY);
}
