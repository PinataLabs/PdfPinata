using System;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// What a text object keeps between <c>BT</c> and <c>ET</c>: the two matrices, which are not
/// part of the graphics state and so are not saved by <c>q</c>, and the glyphs shown in a clip
/// mode.
/// </summary>
internal sealed class TextObject
{
    internal XMatrix TextMatrix = XMatrix.Identity;
    internal XMatrix LineMatrix = XMatrix.Identity;

    /// <summary>
    /// The outlines of the glyphs shown in a render mode that clips, in device space, or null
    /// until one is. The clip is narrowed to them all together when the text object ends.
    /// </summary>
    internal PagePathBuilder Clip;
}

// Text state, text positioning and text showing.
internal sealed partial class ContentInterpreter
{
    // The line state last taken to a glyph's space, so that a run of glyphs set at one size
    // shares one.
    private PageStroke _glyphStrokeOf;
    private PageStroke _glyphStroke;
    private double _glyphStrokeScale;

    /// <summary><c>BT</c>.</summary>
    private void BeginText()
    {
        if (_text != null)
        {
            _sink.Report(PageRenderProblem.OperatorOutOfPlace, "BT", "A text object is begun inside another.");
            EndText();
        }

        _text = new TextObject();
    }

    /// <summary><c>ET</c>.</summary>
    private void EndTextObject()
    {
        if (_text == null)
            _sink.Report(PageRenderProblem.OperatorOutOfPlace, "ET", "A text object is ended where none is begun.");
        else
            EndText();
    }

    /// <summary>
    /// Ends the text object, narrowing the clip to the glyphs it showed in a render mode that
    /// clips.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clip is narrowed only where at least one glyph had an outline. A text object that set
    /// a clip mode and showed nothing, or whose font has no glyphs this version can draw, would
    /// otherwise clip away everything that follows it, and a font that cannot be drawn must not
    /// cost the rest of the page.
    /// </para>
    /// <para>
    /// The glyphs are one path, clipped by nonzero winding, which is the union of them where
    /// their contours wind the same way, as the outlines of one font do.
    /// </para>
    /// </remarks>
    private void EndText()
    {
        var clip = _text.Clip?.ToPath();
        _text = null;
        if (clip is { IsEmpty: false })
            _device.Clip(clip, XMatrix.Identity, XFillMode.Winding);
    }

    /// <summary>
    /// The text object in force. Text positioned or shown outside one is reported and set in one
    /// begun for it, which is what viewers do with it.
    /// </summary>
    private TextObject TextFor(COperator op)
    {
        if (_text == null)
        {
            _sink.Report(PageRenderProblem.OperatorOutOfPlace, op.Name, "Text is positioned or shown outside a text object.");
            _text = new TextObject();
        }

        return _text;
    }

    /// <summary><c>Tf</c>.</summary>
    private void SetFont(COperator op)
    {
        var name = Operands.NameFromEnd(op, 1);
        if (name == null || !Operands.TryNumber(op, out var size))
        {
            WrongOperands(op);
            return;
        }

        State.FontName = name;
        State.FontSize = size;
        var font = _resources.Lookup("/Font", name);
        if (font == null)
        {
            State.Font = null;
            _sink.Report(PageRenderProblem.MissingResource, name, "The resources hold no font of that name. Text set in it is not drawn.");
            return;
        }

        State.Font = _fonts.For(font, _glyphSources);
    }

    /// <summary><c>Tc</c>, <c>Tw</c>, <c>Tz</c>, <c>TL</c> and <c>Ts</c>: one number each.</summary>
    private void SetTextState(COperator op)
    {
        if (!ReadNumber(op, out var value))
            return;

        switch (op.OpCode.OpCodeName)
        {
            case OpCodeName.Tc: State.CharacterSpacing = value; break;
            case OpCodeName.Tw: State.WordSpacing = value; break;
            case OpCodeName.Tz: State.HorizontalScaling = value / 100; break;
            case OpCodeName.TL: State.Leading = value; break;
            default: State.Rise = value; break;
        }
    }

    /// <summary><c>Tr</c>.</summary>
    private void SetRenderMode(COperator op)
    {
        if (!ReadNumber(op, out var mode))
            return;

        // A mode is one of eight whole numbers, so a real that is not exactly one of them is none.
        if (mode is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 7)
            State.RenderMode = (TextRenderMode)(int)mode;
        else
            Invalid("Tr", "A text render mode is a whole number from 0 to 7.");
    }

    /// <summary><c>Td</c> and <c>TD</c>.</summary>
    private void MoveText(COperator op, bool setLeading)
    {
        Span<double> t = stackalloc double[2];
        if (!Read(op, t))
            return;

        if (setLeading)
            State.Leading = -t[1];
        MoveLine(TextFor(op), t[0], t[1]);
    }

    /// <summary><c>T*</c>, and the move to the next line that <c>'</c> and <c>"</c> begin with.</summary>
    private void NextLine(COperator op) => MoveLine(TextFor(op), 0, -State.Leading);

    private static void MoveLine(TextObject text, double x, double y) =>
        text.TextMatrix = text.LineMatrix = XMatrix.Multiply(new XMatrix(1, 0, 0, 1, x, y), text.LineMatrix);

    /// <summary><c>Tm</c>.</summary>
    private void SetTextMatrix(COperator op)
    {
        Span<double> m = stackalloc double[6];
        if (!Read(op, m))
            return;

        var text = TextFor(op);
        text.TextMatrix = text.LineMatrix = new XMatrix(m[0], m[1], m[2], m[3], m[4], m[5]);
    }

    /// <summary><c>"</c>: word spacing, character spacing, then as <c>'</c>.</summary>
    private void SetSpacingAndShowText(COperator op)
    {
        Span<double> spacing = stackalloc double[2];
        if (Operands.FromEnd(op, 0) is not CString || !Operands.TryNumbers(op, spacing, 1))
        {
            WrongOperands(op);
            return;
        }

        State.WordSpacing = spacing[0];
        State.CharacterSpacing = spacing[1];
        NextLine(op);
        ShowText(op);
    }

    /// <summary>
    /// <c>Tj</c>, <c>TJ</c> and the showing half of <c>'</c> and <c>"</c>: sets the last operand,
    /// which is a string, or an array of strings and of the adjustments between them.
    /// </summary>
    private void ShowText(COperator op)
    {
        var shown = Operands.FromEnd(op, 0);
        if (shown is not CString && shown is not CArray)
        {
            WrongOperands(op);
            return;
        }

        var text = TextFor(op);
        var font = State.Font;
        if (font == null)
        {
            // A font the resources do not hold was reported when it was selected. Without its
            // widths there is nothing to place the text by, nor what follows it on the line.
            if (State.FontName == null)
                _sink.Report(PageRenderProblem.OperatorOutOfPlace, op.Name, "Text is shown before any font is selected.");
            return;
        }

        if (font.EncodingNote != null)
            _sink.Report(PageRenderProblem.UnsupportedFeature, State.FontName, font.EncodingNote);

        // Invisible text asks for no outlines, so a font that has none is no loss to it: the
        // text under a scanned page is drawn this way, and is no problem to report.
        if (font.Source == null && State.RenderMode != TextRenderMode.Invisible)
            _sink.Report(PageRenderProblem.UnsupportedFont, State.FontName, font.UnsupportedReason);

        if (shown is CString single)
        {
            ShowString(text, font, single.Value);
            return;
        }

        var array = (CArray)shown;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is CString part)
            {
                ShowString(text, font, part.Value);
            }
            else if (CNumber.TryGetValue(array[index], out var adjustment))
            {
                // Thousandths of text space, taken off the position: a positive number closes
                // the text up.
                Advance(text, -adjustment / 1000 * State.FontSize * State.HorizontalScaling);
            }
        }
    }

    /// <summary>
    /// Sets one string: splits it into codes, draws each glyph where the text matrix has it, and
    /// advances by the PDF's own width for the code, the spacing the text state adds and the
    /// horizontal scaling.
    /// </summary>
    /// <param name="text">The text object.</param>
    /// <param name="font">The font selected.</param>
    /// <param name="shown">The string, one character for each byte of it.</param>
    private void ShowString(TextObject text, RenderFont font, string shown)
    {
        var state = State;
        var bytesPerCode = font.BytesPerCode;
        var draws = state.RenderMode != TextRenderMode.Invisible && font.Source != null;

        // A byte left over at the end of a string of two-byte codes is no code, and is dropped.
        for (var at = 0; at + bytesPerCode <= shown.Length; at += bytesPerCode)
        {
            var code = shown[at] & 0xFF;
            if (bytesPerCode == 2)
                code = code << 8 | shown[at + 1] & 0xFF;

            if (draws)
                DrawGlyph(text, font, code);

            // Word spacing is added after the single byte 32 and after nothing else: not after
            // a two-byte code that happens to be 32 (ISO 32000-1 9.3.3).
            var spacing = state.CharacterSpacing + (bytesPerCode == 1 && code == 32 ? state.WordSpacing : 0);
            Advance(text, (font.WidthOf(code) * state.FontSize + spacing) * state.HorizontalScaling);
        }
    }

    private static void Advance(TextObject text, double x) =>
        text.TextMatrix = XMatrix.Multiply(new XMatrix(1, 0, 0, 1, x, 0), text.TextMatrix);

    /// <summary>
    /// Draws one glyph by the text render mode: filled, stroked, added to the clip, or any of
    /// those together.
    /// </summary>
    private void DrawGlyph(TextObject text, RenderFont font, int code)
    {
        var state = State;
        var outline = font.OutlineOf(code, out var why);
        if (outline == null)
        {
            _sink.Report(PageRenderProblem.MissingGlyph, state.FontName, why);
            return;
        }

        // The outline is in text space before the type size: this takes it to user space.
        var glyphToUser = XMatrix.Multiply(
            new XMatrix(state.FontSize * state.HorizontalScaling, 0, 0, state.FontSize, 0, state.Rise),
            text.TextMatrix);
        var glyphToDevice = XMatrix.Multiply(glyphToUser, state.Ctm);
        if (outline.IsEmpty || !IsFinite(glyphToDevice))
            return;

        var mode = state.RenderMode;
        if (mode is TextRenderMode.Fill or TextRenderMode.FillStroke or TextRenderMode.FillClip
            or TextRenderMode.FillStrokeClip)
        {
            // The outline is handed over as it is, the instance this glyph always has, so that
            // a device can keep what it builds from it.
            _device.Fill(outline, glyphToDevice, XFillMode.Winding, state.FillPaint);
        }

        if (mode is TextRenderMode.Stroke or TextRenderMode.FillStroke or TextRenderMode.StrokeClip
            or TextRenderMode.FillStrokeClip)
        {
            StrokeGlyph(outline, glyphToUser, glyphToDevice);
        }

        if (mode >= TextRenderMode.FillClip)
            (text.Clip ??= new PagePathBuilder()).Append(outline, glyphToDevice);
    }

    /// <summary>
    /// Strokes a glyph's outline. The line width is a length in user space and the outline is in
    /// its own, so one of the two has to be taken to the other.
    /// </summary>
    /// <remarks>
    /// Where the outline's space is user space scaled the same in every direction, which is all
    /// text not stretched by <c>Tz</c> or by a text matrix that scales x and y differently, the
    /// line state is taken to the outline's space: a pen that is a circle in one is a circle
    /// in the other. The outline is then handed over as it is, the instance this glyph always
    /// has, as a filled glyph is. Elsewhere the pen is an ellipse in the outline's space, which
    /// a <see cref="PageStroke"/> cannot say, so the outline is copied into user space and is
    /// not an instance a device has seen before.
    /// </remarks>
    private void StrokeGlyph(PagePath outline, XMatrix glyphToUser, XMatrix glyphToDevice)
    {
        var state = State;
        if (TryGetUniformScale(glyphToUser, out var scale))
        {
            if (!ReferenceEquals(_glyphStrokeOf, state.Stroke) || _glyphStrokeScale != scale)
            {
                _glyphStrokeOf = state.Stroke;
                _glyphStrokeScale = scale;
                _glyphStroke = state.Stroke.InUnitsOf(scale);
            }

            if (_glyphStroke != null)
            {
                _device.Stroke(outline, glyphToDevice, _glyphStroke, state.StrokePaint);
                return;
            }
        }

        _device.Stroke(outline.Transformed(glyphToUser), state.Ctm, state.Stroke, state.StrokePaint);
    }

    /// <summary>
    /// Whether a matrix scales every direction by the same amount, turned or mirrored or not,
    /// and by how much.
    /// </summary>
    private static bool TryGetUniformScale(XMatrix matrix, out double scale)
    {
        // The images of the two axes: the same length, and at right angles.
        var x = matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12;
        var y = matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22;
        var across = matrix.M11 * matrix.M21 + matrix.M12 * matrix.M22;
        var tolerance = Math.Max(x, y) * 1e-9;

        scale = Math.Sqrt(x);
        return scale > 0 && double.IsFinite(scale) && Math.Abs(x - y) <= tolerance && Math.Abs(across) <= tolerance;
    }
}
