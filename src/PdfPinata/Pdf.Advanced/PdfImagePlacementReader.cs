using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Internal;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Advanced;

/// <summary>
/// Reads the content of a page to find the images it draws and the transform it draws each of
/// them under.
/// <para>
/// The transform is what says which way up an image is stored, and it is not written beside the
/// image: it is built up along the way by the content, from the matrices concatenated before
/// the image is drawn and from those of any forms the drawing happens inside. So it can only be
/// had by reading the content through, keeping the graphics state as the content does.
/// </para>
/// </summary>
internal sealed class PdfImagePlacementReader
{
    /// <summary>
    /// How deep forms may be drawn within one another before reading stops. Well past anything
    /// a real document does, and there to stop a malformed one running away.
    /// </summary>
    private const int MaximumDepth = 32;

    /// <summary>
    /// The images the page draws, in the order the content draws them.
    /// </summary>
    internal static IList<PdfImagePlacement> Read(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var reader = new PdfImagePlacementReader();

        if (!PdfContentStreams.TryGetPageContent(page, out var content))
            return reader._placements;

        reader.Read(content, page.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources),
            XMatrix.Identity, 0);

        return reader._placements;
    }

    private readonly List<PdfImagePlacement> _placements = [];

    /// <summary>The forms being drawn through, so that one drawing itself does not go round forever.</summary>
    private readonly Dictionary<string, object> _open = new();

    private void Read(byte[] content, PdfDictionary scope, XMatrix ctm, int depth)
    {
        if (depth > MaximumDepth)
            return;

        CSequence sequence;
        try
        {
            sequence = ContentReader.ReadContent(content);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex))
        {
            // Content that cannot be read says nothing about what it draws.
            return;
        }

        ReadSequence(sequence, scope, ctm, depth);
    }

    private void ReadSequence(CSequence sequence, PdfDictionary scope, XMatrix ctm, int depth)
    {
        // The state a stream saves and restores is its own: a form leaving the stack unbalanced
        // cannot reach past its own content into the state of the page that drew it.
        var saved = new Stack<XMatrix>();

        foreach (var item in sequence)
        {
            var op = item as COperator;
            if (op == null)
                continue;

            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    saved.Push(ctm);
                    break;

                case OpCodeName.Q:
                    // Content restoring a state it never saved is malformed. Keeping the state
                    // as it stands carries on with the reading rather than throwing it away.
                    if (saved.Count > 0)
                        ctm = saved.Pop();
                    break;

                case OpCodeName.cm:
                    ctm = Concatenate(op, ctm);
                    break;

                case OpCodeName.Do:
                    Draw(NameAt(op, 0), scope, ctm, depth);
                    break;

                case OpCodeName.BI:
                    // The lexer finds the end of an inline image by looking for the bytes of EI,
                    // which the image data itself may hold. A wrong guess carries the reading
                    // off, and an image reported under a transform picked up from the middle of
                    // some image data would be reported the wrong way up. So the rest of this
                    // stream is left unread, and the inline image itself is not reported.
                    return;
            }
        }
    }

    /// <summary>
    /// Applies the matrix of a cm operator to the transform in force, which is what the content
    /// asks for: the matrix maps into the space the transform already describes.
    /// </summary>
    private static XMatrix Concatenate(COperator op, XMatrix ctm)
    {
        if (op.Operands.Count < 6)
            return ctm;

        var m = new double[6];
        for (var idx = 0; idx < 6; idx++)
        {
            if (!TryGetNumber(op.Operands[idx], out m[idx]))
                return ctm;
        }

        var matrix = new XMatrix(m[0], m[1], m[2], m[3], m[4], m[5]);
        matrix.Multiply(ctm, XMatrixOrder.Append);
        return matrix;
    }

    private void Draw(string name, PdfDictionary scope, XMatrix ctm, int depth)
    {
        if (name == null || scope == null)
            return;

        var xObjects = scope.Elements.GetDictionary("/XObject");
        var xObject = xObjects == null ? null : xObjects.Elements.GetDictionary(name);
        if (xObject == null)
        {
            // The content names something the resources do not hold.
            return;
        }

        switch (xObject.Elements.GetName(PdfImage.Keys.Subtype))
        {
            case "/Image":
                _placements.Add(new PdfImagePlacement(name, xObject, ctm));
                break;

            case "/Form":
                DrawForm(xObject, scope, ctm, depth);
                break;
        }
    }

    private void DrawForm(PdfDictionary form, PdfDictionary scope, XMatrix ctm, int depth)
    {
        var id = Identify(form);
        if (id != null)
        {
            if (_open.ContainsKey(id))
                return;

            _open[id] = null;
        }

        try
        {
            if (!PdfContentStreams.TryGetContent(form, out var content))
                return;

            // A form draws in a space of its own, which its matrix maps into the space it is
            // drawn in. Names in it resolve against its own resources where it has them, and
            // against those of whatever drew it where it has not.
            var inner = MatrixOf(form);
            inner.Multiply(ctm, XMatrixOrder.Append);

            var formScope = form.Elements.GetDictionary(PdfPage.InheritablePageKeys.Resources) ?? scope;

            Read(content, formScope, inner, depth + 1);
        }
        finally
        {
            if (id != null)
                _open.Remove(id);
        }
    }

    /// <summary>
    /// The /Matrix of a form, which is the identity where it has none.
    /// </summary>
    private static XMatrix MatrixOf(PdfDictionary form)
    {
        var matrix = form.Elements.GetArray("/Matrix");
        if (matrix == null || matrix.Elements.Count < 6)
            return XMatrix.Identity;

        var m = new double[6];
        for (var idx = 0; idx < 6; idx++)
        {
            if (!TryGetNumber(matrix.Elements[idx], out m[idx]))
                return XMatrix.Identity;
        }

        return new XMatrix(m[0], m[1], m[2], m[3], m[4], m[5]);
    }

    /// <summary>
    /// What tells one form from another while it is being drawn. A form written out in place
    /// cannot be shared and so cannot be drawn within itself.
    /// </summary>
    private static string Identify(PdfDictionary form)
    {
        return form.IsIndirect ? form.ObjectID.ToString() : null;
    }

    private static string NameAt(COperator op, int index)
    {
        if (index < 0 || index >= op.Operands.Count)
            return null;

        var name = op.Operands[index] as CName;
        return name == null ? null : name.Name;
    }

    private static bool TryGetNumber(CObject operand, out double value)
    {
        var real = operand as CReal;
        if (real != null)
        {
            value = real.Value;
            return true;
        }

        var integer = operand as CInteger;
        if (integer != null)
        {
            value = integer.Value;
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetNumber(PdfItem item, out double value)
    {
        if (item is PdfReference)
            item = ((PdfReference)item).Value;

        var real = item as PdfReal;
        if (real != null)
        {
            value = real.Value;
            return true;
        }

        var integer = item as PdfInteger;
        if (integer != null)
        {
            value = integer.Value;
            return true;
        }

        value = 0;
        return false;
    }
}
