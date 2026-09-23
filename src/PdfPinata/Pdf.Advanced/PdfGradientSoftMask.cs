using PdfPinata.Drawing;
using PdfPinata.Drawing.Pdf;
using PdfPinata.Pdf.Internal;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Builds the luminosity soft mask that makes a gradient between translucent colours fade out as
/// well as across.
/// </summary>
/// <remarks>
/// A shading pattern paints colour and nothing else - there is no alpha anywhere in a shading
/// dictionary - so the alpha has to arrive as a mask over the paint. The mechanism is the one the
/// PDF specification provides for it and the one every other producer uses:
/// <code>
///   ExtGState                          the gradient is painted with /Gs1 gs applied
///     /SMask &lt;&lt; /Type /Mask
///               /S /Luminosity         luminosity, not /Alpha
///               /G  &lt;form&gt;          ┐
///            &gt;&gt;                     │
///   Form XObject  ◄──────────────────┘
///     /Group &lt;&lt; /S /Transparency
///               /CS /DeviceGray &gt;&gt;     grey: luminance IS the alpha
///     /BBox  [ the page ]
///     stream: /Pattern cs /P0 scn      a second shading, same geometry,
///             &lt;box&gt; re f               whose C0/C1 are the alpha values
/// </code>
/// Nothing here is new machinery: <see cref="PdfSoftMask"/>, <see cref="PdfFormXObject"/>,
/// <see cref="PdfTransparencyGroupAttributes"/> and <see cref="PdfExtGState.SoftMask"/> were all
/// present and unreached from the gradient path.
/// </remarks>
internal static class PdfGradientSoftMask
{
    /// <summary>
    /// The extended graphics state that masks a gradient by its own alpha, or null where both of
    /// the brush's colours are opaque and there is nothing to mask.
    /// </summary>
    /// <param name="brush">The gradient being painted.</param>
    /// <param name="patternMatrix">
    /// The matrix of the colour shading pattern. The alpha shading is given the same one, so the
    /// two ramps run along the same axis.
    /// </param>
    /// <param name="renderer">The renderer painting the gradient.</param>
    internal static PdfExtGState ForBrush(XBaseGradientBrush brush, XMatrix patternMatrix,
        XGraphicsPdfRenderer renderer)
    {
        if (!NeedsMask(brush))
            return null;

        var document = renderer.Owner;

        var alphaPattern = new PdfShadingPattern(document);
        alphaPattern.SetupFromBrush(brush, patternMatrix, renderer, PdfShadingChannel.Alpha);

        var form = MaskForm(document, alphaPattern, patternMatrix, renderer);
        document._irefTable.Add(form);

        var mask = new PdfSoftMask(document);
        mask.Elements.SetName(PdfSoftMask.Keys.S, "/Luminosity");
        mask.Elements.SetReference(PdfSoftMask.Keys.G, form);
        document._irefTable.Add(mask);

        var extGState = new PdfExtGState(document) { SoftMask = mask };
        return extGState;
    }

    /// <summary>
    /// Whether either of the brush's colours is short of fully opaque. Where neither is, not one
    /// byte of the document differs from what the library wrote before soft masks existed.
    /// </summary>
    internal static bool NeedsMask(XBaseGradientBrush brush)
    {
        return brush.Color1.A < 1 || brush.Color2.A < 1;
    }

    /// <summary>
    /// The transparency group whose luminance is read as the alpha: a grey form painting the
    /// alpha shading across the whole page.
    /// </summary>
    /// <remarks>
    /// The bounding box is the page rather than the shape being filled. A box larger than it
    /// needs to be costs a reader time and costs correctness nothing, and the shape is not always
    /// known where the brush is realized; narrowing it is an optimisation for another day.
    /// <para>
    /// The form's matrix undoes the transform in force where the mask is applied, because the
    /// group is evaluated under that transform while a pattern is anchored to the page. Without
    /// it a gradient drawn through a rotation or a scale would be masked by a ramp that had been
    /// rotated or scaled twice.
    /// </para>
    /// </remarks>
    private static PdfFormXObject MaskForm(PdfDocument document, PdfShadingPattern alphaPattern,
        XMatrix patternMatrix, XGraphicsPdfRenderer renderer)
    {
        var box = renderer.StoredPageSize;

        var form = new PdfFormXObject(document);
        form.Elements.SetInteger("/FormType", 1);
        form.Elements.SetRectangle("/BBox", new PdfRectangle(new XPoint(0, 0), new XPoint(box.Width, box.Height)));

        var undoRealizedTransform = renderer.RealizedTransformOf(patternMatrix);
        undoRealizedTransform.Invert();
        form.Elements.SetMatrix("/Matrix", undoRealizedTransform);

        var group = new PdfTransparencyGroupAttributes(document);
        group.Elements.SetName(PdfTransparencyGroupAttributes.Keys.CS, "/DeviceGray");
        form.Elements["/Group"] = group;

        var name = form.Resources.AddPattern(alphaPattern);
        form.CreateStream(PdfEncoders.RawEncoding.GetBytes(
            PdfEncoders.Format("/Pattern cs\n{0} scn\n0 0 {1:0.###} {2:0.###} re\nf\n", name, box.Width, box.Height)));

        return form;
    }
}
