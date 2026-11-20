using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

// The normal appearances of a page's annotations (ISO 32000-1 12.5.5).
internal sealed partial class ContentInterpreter
{
    private const int HiddenFlag = 1 << 1;
    private const int NoViewFlag = 1 << 5;

    /// <summary>
    /// Draws the normal appearance of each annotation of the page, in the order they are listed,
    /// each from the state the page began in and under the clip of its box.
    /// </summary>
    private void DrawAnnotations(PdfPage page)
    {
        if (PdfReference.Dereference(page.Elements["/Annots"]) is not PdfArray annotations)
            return;

        // A form that states no resources of its own is read against the page's.
        _resources = ResourceScope.OfPage(page);
        var depth = _states.Depth;
        var count = annotations.Elements.Count;
        for (var index = 0; index < count; index++)
        {
            if (PdfReference.Dereference(annotations.Elements[index]) is not PdfDictionary annotation)
                continue;

            var stream = _sink.Stream;
            var operatorIndex = _sink.OperatorIndex;
            _sink.Stream = $"/Annots[{index}]";
            _sink.OperatorIndex = 0;
            try
            {
                DrawAnnotation(annotation);
            }
            finally
            {
                _states.PopTo(depth);
                _sink.Stream = stream;
                _sink.OperatorIndex = operatorIndex;
            }
        }
    }

    private void DrawAnnotation(PdfDictionary annotation)
    {
        // A popup is the window a viewer opens beside a note, not something drawn on the page.
        if (annotation.Elements.GetNameOrEmpty("/Subtype") == "/Popup")
            return;

        if (PdfItemValues.TryGetInteger(annotation.Elements["/F"], out var flags)
            && (flags & (HiddenFlag | NoViewFlag)) != 0)
            return;

        var appearance = NormalAppearanceOf(annotation);
        if (appearance == null)
            return;

        ReportIfHidden(annotation.Elements.GetDictionary("/OC"));

        var rectangle = PdfPageResizer.RectangleOf(annotation, "/Rect");
        if (rectangle == null || !double.IsFinite(rectangle.X1 + rectangle.Y1 + rectangle.X2 + rectangle.Y2))
        {
            _sink.Report(PageRenderProblem.InvalidValue, "/Rect",
                "The annotation's /Rect is not four numbers. Its appearance is not drawn.");
            return;
        }

        if (!TryReadMatrix(appearance, out var matrix) || !TryReadBox(appearance, out var box))
        {
            _sink.Report(PageRenderProblem.InvalidValue, "/AP",
                "The appearance has no /BBox of four numbers or its /Matrix is not six numbers. It is not drawn.");
            return;
        }

        var left = Math.Min(rectangle.X1, rectangle.X2);
        var bottom = Math.Min(rectangle.Y1, rectangle.Y2);
        var width = Math.Abs(rectangle.X2 - rectangle.X1);
        var height = Math.Abs(rectangle.Y2 - rectangle.Y1);
        if (width <= 0 || height <= 0)
            return;

        // Step 1 of 12.5.5: the box as the matrix turns it.
        var corners = new[]
        {
            matrix.Transform(new XPoint(box.X, box.Y)),
            matrix.Transform(new XPoint(box.X + box.Width, box.Y)),
            matrix.Transform(new XPoint(box.X, box.Y + box.Height)),
            matrix.Transform(new XPoint(box.X + box.Width, box.Y + box.Height))
        };
        var x0 = Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
        var x1 = Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
        var y0 = Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
        var y1 = Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));
        var boxWidth = x1 - x0;
        var boxHeight = y1 - y0;
        if (!(boxWidth > 0) || !(boxHeight > 0) || !double.IsFinite(boxWidth + boxHeight))
        {
            _sink.Report(PageRenderProblem.InvalidValue, "/AP",
                "The appearance's /BBox has no area once its /Matrix is applied. It is not drawn.");
            return;
        }

        // Step 2: the scale and translation that put that box on the rectangle.
        var scaleX = width / boxWidth;
        var scaleY = height / boxHeight;
        var fit = new XMatrix(scaleX, 0, 0, scaleY, left - scaleX * x0, bottom - scaleY * y0);

        var transform = XMatrix.Multiply(fit, State.Ctm);
        if (!IsFinite(transform))
        {
            _sink.Report(PageRenderProblem.InvalidValue, "/Rect",
                "The annotation's /Rect is too large to draw under. Its appearance is not drawn.");
            return;
        }

        _states.Push();
        State.Ctm = transform;
        DrawForm("/AP", appearance);
    }

    /// <summary>
    /// The form <c>/AP /N</c> names: the stream itself, or the one of a dictionary of states that
    /// <c>/AS</c> picks. Null where there is none, which draws nothing.
    /// </summary>
    private static PdfDictionary NormalAppearanceOf(PdfDictionary annotation)
    {
        var normal = annotation.Elements.GetDictionary("/AP")?.Elements.GetDictionary("/N");
        if (normal == null)
            return null;

        if (normal.Stream != null)
            return normal;

        var state = annotation.Elements.GetNameOrEmpty("/AS");
        if (state.Length == 0)
            return null;

        var chosen = normal.Elements.GetDictionary(state);
        return chosen?.Stream != null ? chosen : null;
    }
}
