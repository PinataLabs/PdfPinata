using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Rendering;

// XObjects, inline images and the parameters of a graphics state dictionary.
internal sealed partial class ContentInterpreter
{
    /// <summary><c>Do</c>.</summary>
    private void DrawXObject(COperator op)
    {
        var name = Operands.NameFromEnd(op, 0);
        if (name == null)
        {
            WrongOperands(op);
            return;
        }

        // Nothing it draws is painted, and a form's own state is its own.
        if (_device.Hidden)
            return;

        var xObject = _resources.Lookup("/XObject", name);
        if (xObject == null)
        {
            _sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no XObject of that name.");
            return;
        }

        if (IsHidden(xObject.Elements.GetDictionary("/OC")))
            return;

        switch (xObject.Elements.GetNameOrEmpty("/Subtype"))
        {
            case "/Form":
                DrawForm(name, xObject);
                break;

            case "/Image":
                DrawImage(ImageOf(xObject, name));
                break;

            default:
                _sink.Report(PageRenderProblem.UnsupportedFeature, name, "The XObject is neither a form nor an image.");
                break;
        }
    }

    /// <summary>
    /// Draws a form XObject: saves, applies its <c>/Matrix</c>, clips to its <c>/BBox</c>, runs
    /// its content with its own resources, and restores.
    /// </summary>
    private void DrawForm(string name, PdfDictionary form)
    {
        if (!TryReadMatrix(form, out var matrix))
        {
            _sink.Report(PageRenderProblem.InvalidValue, name, "The form's /Matrix is not six numbers. The form is not drawn.");
            return;
        }

        var transform = XMatrix.Multiply(matrix, State.Ctm);
        if (!IsFinite(transform))
        {
            _sink.Report(PageRenderProblem.InvalidValue, name, "The form's /Matrix is too large to draw under. The form is not drawn.");
            return;
        }

        // Asked before the content is decoded, so that a form drawn past the limit costs nothing.
        if (!_forms.TryEnter(form))
        {
            _sink.Report(PageRenderProblem.LimitReached, name,
                "The form draws itself, or forms are nested too deep or drawn too often. It is not drawn again.");
            return;
        }

        var depth = _states.Depth;
        try
        {
            var content = ContentOf(form);
            if (content == null)
            {
                _sink.Report(PageRenderProblem.UnreadableContent, name, "The form's content could not be decoded, and nothing of it is drawn.");
                return;
            }

            ReportGroupThatIsNotComposed(form);

            _states.Push();
            State.Ctm = transform;

            if (TryReadBox(form, out var box))
            {
                var clip = new PagePathBuilder();
                clip.AddRectangle(box.X, box.Y, box.Width, box.Height);
                _device.Clip(clip.ToPath(), State.Ctm, XFillMode.Winding);
            }
            else
            {
                _sink.Report(PageRenderProblem.InvalidValue, name, "The form has no /BBox of four numbers. It is drawn without being clipped to one.");
            }

            RunStream(content, _resources.ForForm(form), _sink.Stream + name);
        }
        finally
        {
            _states.PopTo(depth);
            _forms.Leave(form);
        }
    }

    /// <summary>
    /// A form's content, decoded and parsed the first time the form is drawn in this render and
    /// kept for the rest of it. Null where it cannot be decoded.
    /// </summary>
    /// <remarks>
    /// Forms that each draw the next twice reach the last of them thousands of times, well
    /// inside the limits of <see cref="FormVisits"/>, and reading its stream again each time is
    /// what made such a page slow.
    /// </remarks>
    private ParsedContent ContentOf(PdfDictionary form)
    {
        var key = ReferenceIdentity.KeyOf(form);
        if (!_formContent.TryGetValue(key, out var content))
        {
            content = PdfContentStreams.TryGetContent(form, out var bytes) ? ParsedContent.Of(bytes) : null;
            _formContent[key] = content;
        }

        return content;
    }

    /// <summary>
    /// An image XObject's samples, decoded the first time it is drawn in this render and kept for
    /// the rest of it. Null where it cannot be decoded, which is reported the first time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page that draws one image many times, as a tiled background does, decodes it once. A few
    /// kilobytes of Flate can stand for an image of 2^28 samples, which costs seconds and a
    /// gigabyte to decode, so decoding it again for each <c>Do</c> is a cost the file chooses. A
    /// stencil mask is kept too: what is decoded is its coverage, and the colour it is painted in
    /// is the fill colour at each draw.
    /// </para>
    /// <para>
    /// The samples kept are bounded by <see cref="MaximumImageBytesKept"/>, so that a page of many
    /// large images drawn once each does not hold all of them to the end of the render. One that
    /// does not fit is decoded at each draw, as every image was before. An image of at most
    /// <see cref="SmallImageBytes"/> is kept against a budget of its own,
    /// <see cref="MaximumSmallImageBytesKept"/>, so that large images that have filled the first
    /// do not make a tile of one sample decode at each of its thousand draws.
    /// </para>
    /// <para>
    /// An image whose <c>/ColorSpace</c> names a space of the resources is not kept: two forms can
    /// draw it with different resources, and so in different colours.
    /// </para>
    /// </remarks>
    private PageImage ImageOf(PdfDictionary image, string name)
    {
        var key = ReferenceIdentity.KeyOf(image);
        if (_images.TryGetValue(key, out var samples))
            return samples;

        samples = PageImages.ReadXObject(image, name, _resources, _device, _sink);
        var bytes = samples?.Data.Length ?? 0;
        if (!PageImages.NamesAResourceSpace(image))
        {
            if (bytes <= SmallImageBytes)
            {
                if (_smallImageBytesKept + bytes <= MaximumSmallImageBytesKept)
                {
                    _images[key] = samples;
                    _smallImageBytesKept += bytes;
                }
            }
            else if (_imageBytesKept + bytes <= MaximumImageBytesKept)
            {
                _images[key] = samples;
                _imageBytesKept += bytes;
            }
        }

        return samples;
    }

    /// <summary>
    /// Says when a form that is a transparency group is drawn as something a group is not.
    /// </summary>
    /// <remarks>
    /// A group is drawn on its own with every alpha at 1 and then laid on the page once, at the
    /// alpha in force where it is drawn (ISO 32000-1 11.6.6). Here its content is drawn straight
    /// onto the page, each piece at that alpha, so two pieces that overlap show through one
    /// another where the group would not. With both alphas at 1 the two come to the same thing
    /// and nothing is reported, unless the group is a knockout group, which never does.
    /// </remarks>
    private void ReportGroupThatIsNotComposed(PdfDictionary form)
    {
        var group = form.Elements.GetDictionary("/Group");
        if (group == null || group.Elements.GetNameOrEmpty("/S") != "/Transparency")
            return;

        var knocksOut = PdfItemValues.TryGetBoolean(group.Elements["/K"], out var knockout) && knockout;
        if (State.FillAlpha < 1 || State.StrokeAlpha < 1 || knocksOut)
        {
            _sink.Report(PageRenderProblem.UnsupportedFeature, "/Group",
                "Transparency groups are not composed yet. What the form draws is drawn piece by piece, each at the alpha the group is drawn at.");
        }
    }

    /// <summary><c>BMC</c> and <c>BDC</c>: opens a sequence, hidden where it is in optional content that is switched off.</summary>
    private void BeginMarkedContent(COperator op)
    {
        var hidden = Operands.NameFromEnd(op, 1) == "/OC" && Operands.NameFromEnd(op, 0) is { } name
                     && IsHidden(_resources.Lookup("/Properties", name));
        PushMarkedContent(hidden);
    }

    private void PushMarkedContent(bool hidden)
    {
        _marked.Push(hidden);
        if (hidden)
            _hiddenMarks++;
        _device.Hidden = _hiddenMarks > 0;
    }

    /// <summary><c>EMC</c>. One with no sequence open in the stream is reported and does nothing.</summary>
    private void EndMarkedContent(bool report)
    {
        if (_marked.Count == 0)
        {
            if (report)
                _sink.Report(PageRenderProblem.OperatorOutOfPlace, "EMC", "A marked-content sequence is ended where none is open.");
            return;
        }

        if (_marked.Pop())
            _hiddenMarks--;
        _device.Hidden = _hiddenMarks > 0;
    }

    /// <summary>
    /// Whether content belongs to optional content the render switches off.
    /// </summary>
    /// <param name="membership">What an <c>/OC</c> names, or null where there is none.</param>
    private bool IsHidden(PdfDictionary membership)
    {
        if (membership == null)
            return false;

        if (_optionalContent == null)
        {
            _optionalContent = OptionalContent.Of(_document, _optionalContentSelection, out var problem);
            if (problem != null)
                _sink.Report(PageRenderProblem.InvalidValue, "/OC", problem);
        }

        return _optionalContent.Hides(membership);
    }

    /// <summary>
    /// A form's <c>/Matrix</c>: the identity where it has none, and false where it is not six
    /// numbers. It is an array in a file and a literal in a form this library has just made.
    /// </summary>
    private static bool TryReadMatrix(PdfDictionary form, out XMatrix matrix)
    {
        matrix = XMatrix.Identity;
        if (PdfItemValues.IsNull(form.Elements["/Matrix"]))
            return true;

        try
        {
            matrix = form.Elements.GetMatrix("/Matrix");
            return IsFinite(matrix);
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private static bool TryReadBox(PdfDictionary form, out XRect box)
    {
        var stated = PdfPageResizer.RectangleOf(form, "/BBox");
        if (stated == null || !double.IsFinite(stated.X1 + stated.Y1 + stated.X2 + stated.Y2))
        {
            box = default;
            return false;
        }

        box = new XRect(Math.Min(stated.X1, stated.X2), Math.Min(stated.Y1, stated.Y2),
            Math.Abs(stated.X2 - stated.X1), Math.Abs(stated.Y2 - stated.Y1));
        return true;
    }

    /// <summary><c>BI</c>, which the parser reads with its data up to <c>EI</c> as one operator.</summary>
    private void DrawInlineImage(COperator op)
    {
        if (op is CInlineImage image && !_device.Hidden)
            DrawImage(PageImages.ReadInline(image, _resources, _device, _sink));
    }

    /// <summary>
    /// Draws an image in the unit square a PDF draws every image in. Null is an image that could
    /// not be decoded and has been reported.
    /// </summary>
    private void DrawImage(PageImage image)
    {
        if (image == null)
            return;

        // Sample coordinates, rows from the top, to the unit square with y upwards.
        var samplesToUser = new XMatrix(1.0 / image.Width, 0, 0, -1.0 / image.Height, 0, 1);
        var transform = XMatrix.Multiply(samplesToUser, State.Ctm);
        if (IsFinite(transform))
            _device.DrawImage(image, transform, State.FillPaint);
    }

    /// <summary><c>gs</c>.</summary>
    private void SetExtGState(COperator op)
    {
        var name = Operands.NameFromEnd(op, 0);
        if (name == null)
        {
            WrongOperands(op);
            return;
        }

        var parameters = _resources.Lookup("/ExtGState", name);
        if (parameters == null)
        {
            _sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no graphics state dictionary of that name.");
            return;
        }

        var elements = parameters.Elements;
        if (PdfItemValues.TryGetNumber(elements["/LW"], out var width))
            SetLineWidth(width, "/LW");
        if (PdfItemValues.TryGetNumber(elements["/LC"], out var cap))
            SetLineCap(cap, "/LC");
        if (PdfItemValues.TryGetNumber(elements["/LJ"], out var join))
            SetLineJoin(join, "/LJ");
        if (PdfItemValues.TryGetNumber(elements["/ML"], out var limit))
            SetMiterLimit(limit, "/ML");
        if (elements.GetArray("/D") is { } dash)
            SetDashOf(dash);
        if (PdfItemValues.TryGetNumber(elements["/CA"], out var strokeAlpha))
            State.StrokeAlpha = new PagePaint(0, 0, 0, strokeAlpha).Alpha;
        if (PdfItemValues.TryGetNumber(elements["/ca"], out var fillAlpha))
            State.FillAlpha = new PagePaint(0, 0, 0, fillAlpha).Alpha;
        if (elements.GetArray("/Font") is { } font)
            SetFontOf(font, name);

        ReportWhatIsNotDrawn(elements);
    }

    /// <summary><c>/D</c>: an array of the dash pattern and the phase.</summary>
    private void SetDashOf(PdfArray dash)
    {
        if (dash.Elements.Count != 2 || PdfReference.Dereference(dash.Elements[0]) is not PdfArray lengths
                                     || !PdfItemValues.TryGetNumber(dash.Elements[1], out var phase))
        {
            Invalid("/D", "A dash is an array of lengths and a phase.");
            return;
        }

        var pattern = new double[lengths.Elements.Count];
        for (var index = 0; index < pattern.Length; index++)
        {
            if (!PdfItemValues.TryGetNumber(lengths.Elements[index], out pattern[index]))
            {
                Invalid("/D", "A dash is an array of lengths and a phase.");
                return;
            }
        }

        SetDash(pattern, phase, "/D");
    }

    /// <summary><c>/Font</c>: an array of a font dictionary and a size, as <c>Tf</c> names them.</summary>
    private void SetFontOf(PdfArray font, string name)
    {
        if (font.Elements.Count != 2 || PdfReference.Dereference(font.Elements[0]) is not PdfDictionary dictionary
                                     || !PdfItemValues.TryGetNumber(font.Elements[1], out var size))
        {
            Invalid("/Font", "A font in a graphics state dictionary is an array of a font and a size.");
            return;
        }

        State.FontName = name;
        State.FontSize = size;
        State.Font = _fonts.For(dictionary, _glyphSources);
    }

    /// <summary>
    /// Says which parameters of a graphics state dictionary change what is drawn and are not
    /// applied yet, so that a page that needs one is not taken for a page drawn in full.
    /// </summary>
    private void ReportWhatIsNotDrawn(PdfDictionary.DictionaryElements elements)
    {
        var blendMode = PdfReference.Dereference(elements["/BM"]);
        if (blendMode is PdfArray modes && modes.Elements.Count > 0)
            blendMode = modes.Elements[0];
        if (PdfItemValues.TryGetName(blendMode, out var mode) && mode != "/Normal" && mode != "/Compatible")
            _sink.Report(PageRenderProblem.UnsupportedFeature, "/BM", "Blend modes are not applied yet. What is drawn is drawn as Normal.");

        if (PdfReference.Dereference(elements["/SMask"]) is PdfDictionary)
            _sink.Report(PageRenderProblem.UnsupportedFeature, "/SMask", "Soft masks are not applied yet. What is drawn is drawn unmasked.");
    }
}
