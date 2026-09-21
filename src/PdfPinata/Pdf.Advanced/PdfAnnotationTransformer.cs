#region Copyright
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Annotations;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Moves the annotations of a page along with the content when the page is resized.
/// <para>
/// The appearance stream of an annotation is deliberately left alone. A viewer maps an
/// appearance onto the /Rect through the appearance's own /BBox and /Matrix, so moving the
/// rectangle moves what is drawn in it. Moving both would apply the resize twice.
/// </para>
/// <para>
/// What does have to be moved is the geometry an annotation keeps beside its rectangle - the
/// points of an ink stroke, the corners of a highlight, the ends of a line. Those are in page
/// coordinates of their own and a viewer draws from them, so a rectangle that moved without them
/// would leave the annotation drawn away from the thing it annotates.
/// </para>
/// </summary>
static class PdfAnnotationTransformer
{
    /// <summary>
    /// Moves every annotation of the page under the transform the page's content moved under.
    /// </summary>
    internal static void Transform(PdfPage page, XMatrix matrix)
    {
        // Read the element and not the Annotations property, whose getter gives a page without
        // annotations an empty array to hold.
        var item = page.Elements[PdfPage.Keys.Annots];
        if (item is PdfReference reference)
            item = reference.Value;

        if (item is not PdfArray annotations)
            return;

        foreach (var element in annotations.Elements)
        {
            var annotationItem = element;
            if (annotationItem is PdfReference annotationReference)
                annotationItem = annotationReference.Value;

            if (annotationItem is PdfDictionary annotation)
                TransformOne(annotation, matrix);
        }
    }

    static void TransformOne(PdfDictionary annotation, XMatrix matrix)
    {
        TransformRectangle(annotation, "/Rect", matrix);

        // Only the entries that hold coordinates, and only for the subtypes that have them. An
        // annotation of a subtype not listed here keeps its rectangle moved and everything else
        // as it was, which is right for anything drawn from an appearance stream - and for
        // anything else leaves a decoration out of place rather than a broken file.
        switch (annotation.Elements.GetName(PdfAnnotation.Keys.Subtype))
        {
            case "/Line":
                TransformPoints(annotation, "/L", matrix);
                TransformPoints(annotation, "/CL", matrix);
                break;

            case "/Polygon":
            case "/PolyLine":
                TransformPoints(annotation, "/Vertices", matrix);
                break;

            case "/Ink":
                TransformPointsOfEach(annotation, "/InkList", matrix);
                break;

            case "/Highlight":
            case "/Underline":
            case "/StrikeOut":
            case "/Squiggly":
            case "/Link":
            case "/Redact":
                TransformPoints(annotation, "/QuadPoints", matrix);
                break;

            case "/Square":
            case "/Circle":
            case "/Caret":
                TransformDifferences(annotation, "/RD", matrix);
                break;
        }
    }

    /// <summary>
    /// Moves a rectangle, taking the four transformed corners rather than two: a quarter turn
    /// sends the bottom left corner somewhere other than the bottom left.
    /// </summary>
    static void TransformRectangle(PdfDictionary dictionary, string key, XMatrix matrix)
    {
        var item = Resolve(dictionary.Elements[key]);
        var numbers = NumbersOf(item);
        if (numbers == null || numbers.Length != 4)
            return;

        var rect = new XRect(
            Math.Min(numbers[0], numbers[2]),
            Math.Min(numbers[1], numbers[3]),
            Math.Abs(numbers[2] - numbers[0]),
            Math.Abs(numbers[3] - numbers[1]));

        var moved = PdfPageResizer.Transformed(rect, matrix);

        dictionary.Elements.SetRectangle(key,
            new PdfRectangle(moved.X, moved.Y, moved.X + moved.Width, moved.Y + moved.Height));
    }

    /// <summary>
    /// Moves a flat array of x y pairs - a line, a run of vertices, the corners of a highlight.
    /// </summary>
    static void TransformPoints(PdfDictionary dictionary, string key, XMatrix matrix)
    {
        var item = Resolve(dictionary.Elements[key]);
        if (item is not PdfArray array)
            return;

        WritePoints(array, matrix);
    }

    /// <summary>
    /// Moves an array of arrays of x y pairs, which is how an ink annotation keeps its strokes.
    /// </summary>
    static void TransformPointsOfEach(PdfDictionary dictionary, string key, XMatrix matrix)
    {
        var item = Resolve(dictionary.Elements[key]);
        if (item is not PdfArray outer)
            return;

        foreach (var element in outer.Elements)
        {
            if (Resolve(element) is PdfArray inner)
                WritePoints(inner, matrix);
        }
    }

    static void WritePoints(PdfArray array, XMatrix matrix)
    {
        var count = array.Elements.Count;

        // Pairs. An odd count is a malformed array and there is no sensible half a point to
        // move, so it is left as it stands.
        if (count == 0 || count % 2 != 0)
            return;

        // Read the whole array before writing any of it. Writing as it goes would leave a
        // malformed array half moved and half not - worse than either - and would do it after
        // the content had already been wrapped and the boxes set, so there would be no going
        // back. Anything that is not a number leaves the array exactly as it was found.
        var numbers = new double[count];
        for (var index = 0; index < count; index++)
        {
            if (!PdfPageResizer.TryNumber(array.Elements[index], out numbers[index]))
                return;
        }

        for (var index = 0; index < count; index += 2)
        {
            var moved = matrix.Transform(new XPoint(numbers[index], numbers[index + 1]));

            array.Elements[index] = new PdfReal(moved.X);
            array.Elements[index + 1] = new PdfReal(moved.Y);
        }
    }

    /// <summary>
    /// Scales the four insets of an /RD entry, which are distances rather than points: how far
    /// inside /Rect the annotation really is, in the order left, top, right, bottom.
    /// <para>
    /// A quarter turn sends them round the page with everything else, so which inset is which
    /// changes: the content that was against the left of the page is against the top of it
    /// afterwards.
    /// </para>
    /// </summary>
    static void TransformDifferences(PdfDictionary dictionary, string key, XMatrix matrix)
    {
        var item = Resolve(dictionary.Elements[key]);
        var numbers = NumbersOf(item);
        if (numbers == null || numbers.Length != 4)
            return;

        // What a unit step along each axis measures after the transform. For a plain scale these
        // are the two scale factors; for a turned one they come out swapped, which is what makes
        // the arithmetic below work out in the right units either way.
        var alongX = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var alongY = Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);

        double left = numbers[0], top = numbers[1], right = numbers[2], bottom = numbers[3];

        var moved = IsTurned(matrix)
            // Turned a quarter clockwise: what was at the left is at the top, what was at the
            // top is at the right, and so on round.
            ? new[] { bottom * alongY, left * alongX, top * alongY, right * alongX }
            : new[] { left * alongX, top * alongY, right * alongX, bottom * alongY };

        if (Resolve(dictionary.Elements[key]) is PdfArray array && array.Elements.Count == 4)
        {
            for (var index = 0; index < 4; index++)
                array.Elements[index] = new PdfReal(moved[index]);
        }
    }

    /// <summary>
    /// Whether the transform turns the page a quarter, which it does exactly when it sends the
    /// x axis onto the y axis.
    /// </summary>
    static bool IsTurned(XMatrix matrix)
    {
        return Math.Abs(matrix.M11) < 1e-9 && Math.Abs(matrix.M22) < 1e-9;
    }

    static PdfItem Resolve(PdfItem item)
    {
        return item is PdfReference reference ? reference.Value : item;
    }

    /// <summary>
    /// The numbers of an array, or null where the item is not an array of numbers. A number held
    /// indirectly is followed; anything that is not a number at all makes the whole array
    /// unreadable rather than throwing, so the caller leaves it alone.
    /// </summary>
    static double[] NumbersOf(PdfItem item)
    {
        if (item is PdfRectangle rectangle)
            return new[] { rectangle.X1, rectangle.Y1, rectangle.X2, rectangle.Y2 };

        if (item is not PdfArray array)
            return null;

        var numbers = new double[array.Elements.Count];
        for (var index = 0; index < numbers.Length; index++)
        {
            if (!PdfPageResizer.TryNumber(array.Elements[index], out numbers[index]))
                return null;
        }

        return numbers;
    }
}
