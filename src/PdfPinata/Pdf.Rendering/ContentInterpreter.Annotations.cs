using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;

namespace PdfPinata.Pdf.Rendering;

// The normal appearances of a page's annotations (ISO 32000-1 12.5.5).
internal sealed partial class ContentInterpreter
{
    private const int NoZoomFlag = 1 << 3;
    private const int NoRotateFlag = 1 << 4;
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

        if (annotations.Elements.Count == 0)
            return;

        // What the content set or clipped to outside a q is not what annotations start from, so the
        // page is begun again, in the state it was first in.
        _states.PopTo(0);
        BeginPage();

        // A form that states no resources of its own is read against the page's.
        _resources = ResourceScope.OfPage(page);
        var depth = _states.Depth;

        // The page's content is done with its budget of forms, which it may have spent whole, and
        // an annotation is not left undrawn for that. The annotations have one of their own.
        _context.Forms = new FormVisits();
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

    // Drawn at 1 to 1 and not turned: what NoZoom and NoRotate ask for is already what is drawn.
    private bool IsUpright => Math.Abs(_view.Scale - 1) < 1e-9
        && Math.Abs(_view.Transform.M11 - 1) < 1e-9 && Math.Abs(_view.Transform.M22 + 1) < 1e-9
        && Math.Abs(_view.Transform.M12) < 1e-9 && Math.Abs(_view.Transform.M21) < 1e-9;

    /// <summary>
    /// <c>/F</c>, which is an integer. A real that is a whole number is read as one, as a viewer
    /// reads it, so that <c>2.0</c> still hides.
    /// </summary>
    private static int? FlagsOf(PdfDictionary annotation)
    {
        var item = annotation.Elements["/F"];
        if (PdfItemValues.TryGetInteger(item, out var stated))
            return stated;

#pragma warning disable S1244 // Exact on purpose: /F is accepted as a real only when it is a whole number.
        if (PdfItemValues.TryGetNumber(item, out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue)
#pragma warning restore S1244
            return (int)real;

        return null;
    }

    private bool NeedsAppearances()
    {
        if (_needAppearancesReported
            || _context.Document?.Internals.Catalog.Elements.GetDictionary("/AcroForm") is not { } form
            || !PdfItemValues.TryGetBoolean(form.Elements["/NeedAppearances"], out var needed) || !needed)
            return false;

        _needAppearancesReported = true;
        return true;
    }

    private void DrawAnnotation(PdfDictionary annotation)
    {
        // A popup is the window a viewer opens beside a note, not something drawn on the page.
        if (annotation.Elements.GetNameOrEmpty("/Subtype") == "/Popup")
            return;

        int? flags = FlagsOf(annotation);
        if (flags is { } shown && (shown & (HiddenFlag | NoViewFlag)) != 0)
            return;

        var appearance = NormalAppearanceOf(annotation);
        if (appearance == null)
            return;

        if (flags is { } kept && (kept & (NoZoomFlag | NoRotateFlag)) != 0 && !IsUpright)
        {
            _sink.Report(PageRenderProblem.UnsupportedFeature, "/F",
                "The no-zoom and no-rotate flags are not applied yet. The appearance is drawn scaled and turned with the page.");
        }

        ReportIfHidden(annotation.Elements.GetDictionary("/OC"));
        ReportIfHidden(appearance.Elements.GetDictionary("/OC"));

        if (PdfItemValues.TryGetNumber(annotation.Elements["/CA"], out var opacity) && opacity < 1)
        {
            _sink.Report(PageRenderProblem.UnsupportedFeature, "/CA",
                "The annotation's opacity is not applied yet. Its appearance is drawn opaque.");
        }

        if (annotation.Elements.GetNameOrEmpty("/Subtype") == "/Widget" && NeedsAppearances())
        {
            _sink.Report(PageRenderProblem.UnsupportedFeature, "/NeedAppearances",
                "The form asks for its appearances to be made again, which this does not do. The widget is drawn from the appearance it has.");
        }

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
    /// <c>/AS</c> picks. Null where there is none, which draws nothing, and where there is one that
    /// is not a stream, which is also reported.
    /// </summary>
    private PdfDictionary NormalAppearanceOf(PdfDictionary annotation)
    {
        var appearances = PdfReference.Dereference(annotation.Elements["/AP"]);
        if (PdfItemValues.IsNull(appearances))
            return null;

        if (appearances is not PdfDictionary dictionary)
            return NotAStream("/AP");

        var normal = PdfReference.Dereference(dictionary.Elements["/N"]);
        if (PdfItemValues.IsNull(normal))
            return null;

        if (normal is not PdfDictionary form)
            return NotAStream("/N");

        if (form.Stream != null)
            return form;

        var state = annotation.Elements.GetNameOrEmpty("/AS");
        if (state.Length == 0)
            return null;

        var chosen = PdfReference.Dereference(form.Elements[state]);
        if (PdfItemValues.IsNull(chosen))
            return null;

        return chosen is PdfDictionary { Stream: not null } stream ? stream : NotAStream("/AS");
    }

    private PdfDictionary NotAStream(string subject)
    {
        _sink.Report(PageRenderProblem.InvalidValue, subject,
            "The annotation's appearance is not a form XObject. It is not drawn.");
        return null;
    }
}
