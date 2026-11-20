using System;
using System.Collections.Generic;
using PdfPinata.Drawing;
using PdfPinata.Internal;
using PdfPinata.Pdf.Advanced;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;

namespace PdfPinata.Pdf.Rendering;

/// <summary>
/// One render of one page: reads its content, keeps the graphics state the content builds up, and
/// draws what it says onto a <see cref="PageDevice"/>.
/// </summary>
/// <remarks>
/// <para>
/// An instance is made for a render and not used again. A form XObject is drawn by the same
/// instance, so there is one state stack, one count of what the device has saved and one place
/// problems are reported.
/// </para>
/// <para>
/// <see cref="Execute"/> is the only place an operator is told from another. Colour spaces, glyph
/// outlines and images each have a class of their own that it calls
/// (<see cref="ColorSpaces"/>, <see cref="BuiltInGlyphSources"/>, <see cref="PageImages"/>), and a
/// new kind of any of them is added there: it changes what a call made from here answers, and
/// not which calls are made.
/// </para>
/// </remarks>
internal sealed partial class ContentInterpreter
{
    /// <summary>
    /// How many states content may save before a further <c>q</c> is only counted. Well past
    /// what a real page does, and there so that a page of nothing but <c>q</c> costs a counter
    /// and not a copy of the graphics state for each.
    /// </summary>
    internal const int MaximumSaveDepth = 256;

    /// <summary>
    /// How many faults are read past in one content stream before the rest of it is left unread.
    /// </summary>
    internal const int MaximumFaults = 64;

    private readonly PageDevice _device;
    private readonly PageView _view;
    private readonly RenderFontCache _fonts;
    private readonly Func<PdfDictionary, PageGlyphSource> _glyphSources;
    private readonly DiagnosticSink _sink = new();
    private readonly StateStack _states;
    private readonly FormVisits _forms = new();

    // What belongs to the content stream being drawn. A form XObject has its own of each, and
    // RunStream puts the caller's back when the form ends.
    private ResourceScope _resources;
    private PagePathBuilder _path = new();
    private XFillMode? _pendingClip;
    private TextObject _text;
    private int _compatibilityDepth;
    private int _floor;
    private int _uncountedSaves;

    internal ContentInterpreter(PageDevice device, PageView view, RenderFontCache fonts,
        Func<PdfDictionary, PageGlyphSource> glyphSources)
    {
        _device = new GuardedDevice(device, _sink);
        _view = view;
        _fonts = fonts;
        _glyphSources = glyphSources;
        _states = new StateStack(_device, new GraphicsState { Ctm = view.Transform });
    }

    private GraphicsState State => _states.Current;

    /// <summary>What the render left out.</summary>
    internal PageRenderResult Result => _sink.ToResult();

    /// <summary>
    /// Draws a page: clips to the box the view draws, then runs the page's content. Whatever the
    /// content does, and whatever is thrown, the device has been restored as often as it was
    /// saved when this returns.
    /// </summary>
    internal void RunPage(PdfPage page)
    {
        try
        {
            _states.Push();

            var box = new PagePathBuilder();
            box.AddRectangle(_view.Box.X, _view.Box.Y, _view.Box.Width, _view.Box.Height);
            _device.Clip(box.ToPath(), _view.Transform, XFillMode.Winding);

            if (_view.BoxIsAssumed)
            {
                _sink.Report(PageRenderProblem.InvalidValue, "page",
                    "The page states no box with any area. It is drawn as a US Letter page.");
            }

            if (!PdfContentStreams.TryGetPageContent(page, out var content))
            {
                _sink.Report(PageRenderProblem.UnreadableContent, "page",
                    "The page's content could not be decoded, and nothing of it is drawn.");
                return;
            }

            RunStream(content, ResourceScope.OfPage(page), "page");
        }
        finally
        {
            _states.PopTo(0);
        }
    }

    /// <summary>
    /// Runs one content stream, the page's or a form's, to its end. What the stream saves and
    /// leaves unrestored is restored when it ends, so a stream cannot leave anything on the stack
    /// of whatever drew it, and a <c>Q</c> in it cannot reach past where it began.
    /// </summary>
    private void RunStream(byte[] content, ResourceScope resources, string streamName)
    {
        var faults = new List<ContentFault>();
        var sequence = ContentReader.ReadPastFaults(content, faults, MaximumFaults, out var leftUnread);

        var outer = (_resources, _path, _pendingClip, _text, _compatibilityDepth, _floor, _uncountedSaves,
            _sink.Stream, _sink.OperatorIndex, _sink.Operator);
        _resources = resources;
        _path = new PagePathBuilder();
        _pendingClip = null;
        _text = null;
        _compatibilityDepth = 0;
        _floor = _states.Depth;
        _uncountedSaves = 0;
        _sink.Stream = streamName;
        _sink.Operator = null;

        try
        {
            var nextFault = 0;

            // Indexed and not enumerated: the generic enumerator of a CSequence throws.
            for (var index = 0; index < sequence.Count; index++)
            {
                _sink.OperatorIndex = index;
                ReportFaults(faults, ref nextFault, index);
                if (sequence[index] is not COperator op)
                    continue;

                _sink.Operator = op.Name;
                ExecuteGuarded(op);
            }

            _sink.OperatorIndex = sequence.Count;
            _sink.Operator = null;
            ReportFaults(faults, ref nextFault, int.MaxValue);
            if (leftUnread)
            {
                _sink.Report(PageRenderProblem.LimitReached, streamName,
                    "Too much of the content stream could not be read. What follows the last fault is not drawn.");
            }

            // A text object the content never ended still clips, as it would have at its ET.
            if (_text != null)
                EndText();
        }
        finally
        {
            _states.PopTo(_floor);
            (_resources, _path, _pendingClip, _text, _compatibilityDepth, _floor, _uncountedSaves,
                _sink.Stream, _sink.OperatorIndex, _sink.Operator) = outer;
        }
    }

    /// <summary>
    /// Reports the faults the parser met before the operator about to be drawn, so that they
    /// come in the order of the content and each is told at the operator that follows it.
    /// </summary>
    private void ReportFaults(List<ContentFault> faults, ref int next, int operatorIndex)
    {
        for (; next < faults.Count && faults[next].OperatorIndex <= operatorIndex; next++)
        {
            var fault = faults[next];
            if (fault.UnknownOperator == null)
            {
                _sink.Report(PageRenderProblem.UnreadableContent, _sink.Stream, fault.Message);
            }
            else if (_compatibilityDepth == 0)
            {
                // Between BX and EX a name that is no operator is what the content expects of a
                // reader that does not know it.
                _sink.Report(PageRenderProblem.UnknownOperator, fault.UnknownOperator, fault.Message);
            }
        }
    }

    /// <summary>
    /// Draws one operator, and records whatever it throws against it rather than passing it on:
    /// a malformed file may make an operator fail in a way none of its own checks foresaw, and
    /// the page is still drawn but for that operator. Two things are passed on: being out of
    /// memory, which says nothing about the file, and <see cref="UnsetSeamException"/>. What the
    /// device throws never gets this far: <see cref="GuardedDevice"/> has recorded it.
    /// </summary>
    private void ExecuteGuarded(COperator op)
    {
        try
        {
            Execute(op);
        }
        catch (Exception ex) when (!Unrecoverable.Is(ex) && ex is not UnsetSeamException)
        {
            _sink.Report(PageRenderProblem.OperatorFailed, op.Name, ex.Message);
        }
    }

    /// <summary>The one place an operator is told from another.</summary>
    private void Execute(COperator op)
    {
        switch (op.OpCode.OpCodeName)
        {
            // The graphics state.
            case OpCodeName.q: Save(); break;
            case OpCodeName.Q: Restore(); break;
            case OpCodeName.cm: Concatenate(op); break;
            case OpCodeName.w: SetLineWidth(op); break;
            case OpCodeName.J: SetLineCap(op); break;
            case OpCodeName.j: SetLineJoin(op); break;
            case OpCodeName.M: SetMiterLimit(op); break;
            case OpCodeName.d: SetDash(op); break;
            case OpCodeName.gs: SetExtGState(op); break;

            // Colour.
            case OpCodeName.g: SetColor(op, false, ColorSpaces.DeviceGray); break;
            case OpCodeName.G: SetColor(op, true, ColorSpaces.DeviceGray); break;
            case OpCodeName.rg: SetColor(op, false, ColorSpaces.DeviceRgb); break;
            case OpCodeName.RG: SetColor(op, true, ColorSpaces.DeviceRgb); break;
            case OpCodeName.k: SetColor(op, false, ColorSpaces.DeviceCmyk); break;
            case OpCodeName.K: SetColor(op, true, ColorSpaces.DeviceCmyk); break;
            case OpCodeName.cs: SetColorSpace(op, false); break;
            case OpCodeName.CS: SetColorSpace(op, true); break;
            case OpCodeName.sc:
            case OpCodeName.scn: SetColorInSpace(op, false); break;
            case OpCodeName.SC:
            case OpCodeName.SCN: SetColorInSpace(op, true); break;

            // Paths.
            case OpCodeName.m: MoveTo(op); break;
            case OpCodeName.l: LineTo(op); break;
            case OpCodeName.c: CurveTo(op); break;
            case OpCodeName.v: CurveFromCurrentPoint(op); break;
            case OpCodeName.y: CurveToEndPoint(op); break;
            case OpCodeName.h: _path.Close(); break;
            case OpCodeName.re: Rectangle(op); break;
            case OpCodeName.f:
            case OpCodeName.F: Paint(fill: XFillMode.Winding, stroke: false, close: false); break;
            case OpCodeName.fx: Paint(fill: XFillMode.Alternate, stroke: false, close: false); break;
            case OpCodeName.B: Paint(fill: XFillMode.Winding, stroke: true, close: false); break;
            case OpCodeName.Bx: Paint(fill: XFillMode.Alternate, stroke: true, close: false); break;
            case OpCodeName.b: Paint(fill: XFillMode.Winding, stroke: true, close: true); break;
            case OpCodeName.bx: Paint(fill: XFillMode.Alternate, stroke: true, close: true); break;
            case OpCodeName.S: Paint(fill: null, stroke: true, close: false); break;
            case OpCodeName.s: Paint(fill: null, stroke: true, close: true); break;
            case OpCodeName.n: Paint(fill: null, stroke: false, close: false); break;
            case OpCodeName.W: _pendingClip = XFillMode.Winding; break;
            case OpCodeName.Wx: _pendingClip = XFillMode.Alternate; break;

            // Text.
            case OpCodeName.BT: BeginText(); break;
            case OpCodeName.ET: EndTextObject(); break;
            case OpCodeName.Tf: SetFont(op); break;
            case OpCodeName.Tc:
            case OpCodeName.Tw:
            case OpCodeName.Tz:
            case OpCodeName.TL:
            case OpCodeName.Ts: SetTextState(op); break;
            case OpCodeName.Tr: SetRenderMode(op); break;
            case OpCodeName.Td: MoveText(op, setLeading: false); break;
            case OpCodeName.TD: MoveText(op, setLeading: true); break;
            case OpCodeName.Tm: SetTextMatrix(op); break;
            case OpCodeName.Tx: NextLine(op); break;
            case OpCodeName.Tj:
            case OpCodeName.TJ: ShowText(op); break;
            case OpCodeName.QuoteSingle: NextLine(op); ShowText(op); break;
            case OpCodeName.QuoteDbl: SetSpacingAndShowText(op); break;

            // XObjects and inline images.
            case OpCodeName.Do: DrawXObject(op); break;
            case OpCodeName.BI: DrawInlineImage(op); break;

            case OpCodeName.sh:
                _sink.Report(PageRenderProblem.UnsupportedFeature, "sh", "Shadings are not drawn yet.");
                break;

            case OpCodeName.BX: _compatibilityDepth++; break;
            case OpCodeName.EX: _compatibilityDepth = Math.Max(0, _compatibilityDepth - 1); break;

            // Everything else says nothing about what is drawn: marked content, the rendering
            // intent and the flatness, the glyph metrics of a Type 3 font, and the pseudo-operator
            // the parser makes of a dictionary written in the content.
        }
    }

    /// <summary><c>q</c>.</summary>
    private void Save()
    {
        if (_states.Depth - _floor >= MaximumSaveDepth)
        {
            _uncountedSaves++;
            _sink.Report(PageRenderProblem.LimitReached, "q",
                "More graphics states are saved than are kept. The states saved past the limit are not restored.");
            return;
        }

        _states.Push();
    }

    /// <summary><c>Q</c>.</summary>
    private void Restore()
    {
        // A q that was only counted is undone by the Q that pairs with it, which is the next.
        if (_uncountedSaves > 0)
        {
            _uncountedSaves--;
            return;
        }

        if (_states.Depth <= _floor)
        {
            _sink.Report(PageRenderProblem.OperatorOutOfPlace, "Q", "A graphics state is restored where none is saved.");
            return;
        }

        _states.PopTo(_states.Depth - 1);
    }

    /// <summary><c>cm</c>.</summary>
    private void Concatenate(COperator op)
    {
        Span<double> m = stackalloc double[6];
        if (!Read(op, m))
            return;

        var ctm = XMatrix.Multiply(new XMatrix(m[0], m[1], m[2], m[3], m[4], m[5]), State.Ctm);
        if (!IsFinite(ctm))
        {
            _sink.Report(PageRenderProblem.InvalidValue, "cm", "The transform is too large to draw under, and is left as it was.");
            return;
        }

        State.Ctm = ctm;
    }

    /// <summary><c>w</c>.</summary>
    private void SetLineWidth(COperator op)
    {
        if (ReadNumber(op, out var width))
            SetLineWidth(width, "w");
    }

    private void SetLineWidth(double width, string subject)
    {
        if (width < 0)
            Invalid(subject, "A line width is not negative.");
        else
            State.Stroke = State.Stroke.WithWidth(width);
    }

    /// <summary><c>J</c>.</summary>
    private void SetLineCap(COperator op)
    {
        if (ReadNumber(op, out var cap))
            SetLineCap(cap, "J");
    }

    private void SetLineCap(double cap, string subject)
    {
        if (cap is 0 or 1 or 2)
            State.Stroke = State.Stroke.WithLineCap((XLineCap)(int)cap);
        else
            Invalid(subject, "A line cap is 0, 1 or 2.");
    }

    /// <summary><c>j</c>.</summary>
    private void SetLineJoin(COperator op)
    {
        if (ReadNumber(op, out var join))
            SetLineJoin(join, "j");
    }

    private void SetLineJoin(double join, string subject)
    {
        if (join is 0 or 1 or 2)
            State.Stroke = State.Stroke.WithLineJoin((XLineJoin)(int)join);
        else
            Invalid(subject, "A line join is 0, 1 or 2.");
    }

    /// <summary><c>M</c>.</summary>
    private void SetMiterLimit(COperator op)
    {
        if (ReadNumber(op, out var limit))
            SetMiterLimit(limit, "M");
    }

    private void SetMiterLimit(double limit, string subject)
    {
        if (limit < 1)
            Invalid(subject, "A miter limit is at least 1.");
        else
            State.Stroke = State.Stroke.WithMiterLimit(limit);
    }

    /// <summary><c>d</c>.</summary>
    private void SetDash(COperator op)
    {
        if (Operands.FromEnd(op, 1) is not CArray array || !Operands.TryNumber(op, out var phase))
        {
            WrongOperands(op);
            return;
        }

        var pattern = new double[array.Count];
        for (var index = 0; index < pattern.Length; index++)
        {
            if (!CNumber.TryGetValue(array[index], out pattern[index]))
            {
                WrongOperands(op);
                return;
            }
        }

        SetDash(pattern, phase, "d");
    }

    private void SetDash(double[] pattern, double phase, string subject)
    {
        if (!Array.TrueForAll(pattern, length => length >= 0 && double.IsFinite(length)))
            Invalid(subject, "The lengths of a dash pattern are not negative.");
        else
            State.Stroke = State.Stroke.WithDash(pattern, phase);
    }

    /// <summary><c>g</c>, <c>G</c>, <c>rg</c>, <c>RG</c>, <c>k</c> and <c>K</c>: a space and a colour in it at once.</summary>
    private void SetColor(COperator op, bool stroking, RenderColorSpace space)
    {
        Span<double> components = stackalloc double[space.ComponentCount];
        if (Read(op, components))
            SetColor(stroking, space, space.ToRgb(components));
    }

    private void SetColor(bool stroking, RenderColorSpace space, PagePaint color)
    {
        if (stroking)
        {
            State.StrokeSpace = space;
            State.StrokeColor = color;
        }
        else
        {
            State.FillSpace = space;
            State.FillColor = color;
        }
    }

    /// <summary><c>cs</c> and <c>CS</c>.</summary>
    private void SetColorSpace(COperator op, bool stroking)
    {
        var name = Operands.NameFromEnd(op, 0);
        if (name == null)
        {
            WrongOperands(op);
            return;
        }

        // A name the resources do not hold has been reported, and leaves the colour as it was.
        var space = ColorSpaces.ResolveName(name, _resources, _sink);
        if (space != null)
            SetColor(stroking, space, space.InitialColor);
    }

    /// <summary><c>sc</c>, <c>SC</c>, <c>scn</c> and <c>SCN</c>.</summary>
    private void SetColorInSpace(COperator op, bool stroking)
    {
        var space = stroking ? State.StrokeSpace : State.FillSpace;

        // scn may end in the name of a pattern, which is no component.
        var skip = Operands.FromEnd(op, 0) is CName ? 1 : 0;
        var count = space.ComponentCount < 0 ? op.Operands.Count - skip : space.ComponentCount;
        var components = new double[Math.Max(count, 0)];
        if (!Operands.TryNumbers(op, components, skip))
        {
            WrongOperands(op);
            return;
        }

        SetColor(stroking, space, space.ToRgb(components));
    }

    /// <summary>Reads the last operands as numbers, or reports the operator and answers false.</summary>
    private bool Read(COperator op, Span<double> into)
    {
        if (Operands.TryNumbers(op, into))
            return true;

        WrongOperands(op);
        return false;
    }

    private bool ReadNumber(COperator op, out double value)
    {
        if (Operands.TryNumber(op, out value))
            return true;

        WrongOperands(op);
        return false;
    }

    private void WrongOperands(COperator op) =>
        _sink.Report(PageRenderProblem.WrongOperands, op.Name,
            "The operator was given too few operands, or operands of the wrong kind, and is skipped.");

    private void Invalid(string subject, string message) =>
        _sink.Report(PageRenderProblem.InvalidValue, subject, message + " The value is left as it was.");

    private static bool IsFinite(XMatrix matrix) =>
        double.IsFinite(matrix.M11) && double.IsFinite(matrix.M12) && double.IsFinite(matrix.M21)
        && double.IsFinite(matrix.M22) && double.IsFinite(matrix.OffsetX) && double.IsFinite(matrix.OffsetY);
}
