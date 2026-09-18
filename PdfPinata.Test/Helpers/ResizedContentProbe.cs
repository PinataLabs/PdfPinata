using System;
using System.Collections.Generic;
using System.Linq;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Test.Helpers;

/// <summary>
///   Finds where the drawing of a page really ends up, in the coordinates of the media box, by
///   walking the content and applying every transform on the way.
///   <para>
///   The same idea as the walker in <c>RotatedPageTests</c>, with the one thing a resize needs
///   that rotation did not: after a page has been resized its content is no longer in the page's
///   own content stream. It has been moved whole into a form XObject and the page draws that. So
///   the walk has to follow a Do into the form and carry the transform down with it, or it would
///   find nothing at all on every resized page and every assertion would pass vacuously.
///   </para>
/// </summary>
internal static class ResizedContentProbe
{
    /// <summary>
    ///   The smallest rectangle holding every rectangle the page draws, in media box coordinates.
    ///   Drawing one rectangle over the whole of a page and asking for this afterwards says
    ///   exactly where that page's content has been put.
    /// </summary>
    internal static XRect DrawnBounds(PdfPage page)
    {
        List<XRect> rectangles = DrawnRectangles(page);
        if (rectangles.Count == 0)
            throw new InvalidOperationException("The page draws no rectangles for the probe to find.");

        double minX = rectangles.Min(rect => rect.X);
        double minY = rectangles.Min(rect => rect.Y);
        double maxX = rectangles.Max(rect => rect.X + rect.Width);
        double maxY = rectangles.Max(rect => rect.Y + rect.Height);

        return new XRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    ///   Every rectangle the page draws, transformed into media box coordinates.
    /// </summary>
    internal static List<XRect> DrawnRectangles(PdfPage page)
    {
        List<XRect> found = new List<XRect>();
        PdfDictionary resources = ResourcesOf(page.Elements["/Resources"]);

        Walk(ContentReader.ReadContent(page), resources, XMatrix.Identity, found, 0);
        return found;
    }

    /// <summary>
    ///   How many form XObjects the page draws, counted through the whole tree of them. One after
    ///   a resize; more than one says a resize wrapped a page that was already wrapped.
    /// </summary>
    internal static int FormCount(PdfPage page)
    {
        PdfDictionary resources = ResourcesOf(page.Elements["/Resources"]);
        return CountForms(ContentReader.ReadContent(page), resources, 0);
    }

    static void Walk(CSequence content, PdfDictionary resources, XMatrix ctm, List<XRect> found, int depth)
    {
        if (depth > 16)
            return;

        Stack<XMatrix> saved = new Stack<XMatrix>();

        foreach (COperator op in content.OfType<COperator>())
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push(ctm);
                    break;

                case OpCodeName.Q:
                    if (saved.Count > 0)
                        ctm = saved.Pop();
                    break;

                case OpCodeName.cm:
                    // The new matrix applies before whatever is already in force.
                    ctm = Matrix(op) * ctm;
                    break;

                case OpCodeName.re:
                    found.Add(TransformedRectangle(op, ctm));
                    break;

                case OpCodeName.Do:
                    WalkForm(op, resources, ctm, found, depth);
                    break;
            }
        }
    }

    static void WalkForm(COperator op, PdfDictionary resources, XMatrix ctm, List<XRect> found, int depth)
    {
        PdfDictionary form = FormNamedBy(op, resources);
        if (form?.Stream == null)
            return;

        // A form may carry a transform of its own, applied before the one in force.
        XMatrix inner = ctm;
        PdfArray matrix = form.Elements.GetArray("/Matrix");
        if (matrix != null && matrix.Elements.Count == 6)
        {
            inner = new XMatrix(
                matrix.Elements.GetReal(0), matrix.Elements.GetReal(1),
                matrix.Elements.GetReal(2), matrix.Elements.GetReal(3),
                matrix.Elements.GetReal(4), matrix.Elements.GetReal(5)) * ctm;
        }

        // A form with resources of its own is a scope of its own; one without inherits.
        PdfDictionary formResources = ResourcesOf(form.Elements["/Resources"]) ?? resources;

        Walk(ContentReader.ReadContent(form.Stream.UnfilteredValue), formResources, inner, found, depth + 1);
    }

    static int CountForms(CSequence content, PdfDictionary resources, int depth)
    {
        if (depth > 16)
            return 0;

        int count = 0;
        foreach (COperator op in content.OfType<COperator>())
        {
            if (op.OpCode.OpCodeName != OpCodeName.Do)
                continue;

            PdfDictionary form = FormNamedBy(op, resources);
            if (form?.Stream == null)
                continue;

            count++;
            PdfDictionary formResources = ResourcesOf(form.Elements["/Resources"]) ?? resources;
            count += CountForms(ContentReader.ReadContent(form.Stream.UnfilteredValue), formResources, depth + 1);
        }

        return count;
    }

    static PdfDictionary FormNamedBy(COperator op, PdfDictionary resources)
    {
        if (resources == null || op.Operands.Count == 0 || op.Operands[0] is not CName name)
            return null;

        PdfDictionary xObjects = ResourcesOf(resources.Elements["/XObject"]);
        if (xObjects == null)
            return null;

        PdfDictionary form = Resolve(xObjects.Elements[name.Name]) as PdfDictionary;
        return form?.Elements.GetName("/Subtype") == "/Form" ? form : null;
    }

    static XRect TransformedRectangle(COperator op, XMatrix ctm)
    {
        double x = Number(op.Operands[0]);
        double y = Number(op.Operands[1]);
        double width = Number(op.Operands[2]);
        double height = Number(op.Operands[3]);

        XPoint[] corners =
        {
            ctm.Transform(new XPoint(x, y)),
            ctm.Transform(new XPoint(x + width, y)),
            ctm.Transform(new XPoint(x + width, y + height)),
            ctm.Transform(new XPoint(x, y + height)),
        };

        double minX = corners.Min(point => point.X);
        double minY = corners.Min(point => point.Y);
        double maxX = corners.Max(point => point.X);
        double maxY = corners.Max(point => point.Y);

        return new XRect(minX, minY, maxX - minX, maxY - minY);
    }

    static XMatrix Matrix(COperator op)
    {
        return new XMatrix(
            Number(op.Operands[0]), Number(op.Operands[1]), Number(op.Operands[2]),
            Number(op.Operands[3]), Number(op.Operands[4]), Number(op.Operands[5]));
    }

    static double Number(CObject operand)
    {
        return operand is CReal real ? real.Value : ((CInteger)operand).Value;
    }

    static PdfDictionary ResourcesOf(PdfItem item)
    {
        return Resolve(item) as PdfDictionary;
    }

    static PdfItem Resolve(PdfItem item)
    {
        return item is PdfReference reference ? reference.Value : item;
    }
}
