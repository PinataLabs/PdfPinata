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
/// Content that is drawn apart, which a tiling pattern's cell is, is run by an instance of its
/// own onto a <see cref="PictureRecorder"/>: <see cref="Record"/>. That instance has its own
/// device, state stack and surface, and shares the <see cref="RenderContext"/> of the render it
/// is part of.
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

    /// <summary>
    /// How many bytes of decoded images one render keeps to draw again: 1 GiB, which is one image
    /// of <see cref="PageImages.MaximumSamples"/>.
    /// </summary>
    internal const long MaximumImageBytesKept = PageImages.MaximumSamples * 4;

    /// <summary>
    /// The size, in bytes of samples, up to which an image is kept against
    /// <see cref="MaximumSmallImageBytesKept"/> and not <see cref="MaximumImageBytesKept"/>: 1 MiB,
    /// an image of 512 by 512.
    /// </summary>
    internal const long SmallImageBytes = 1L << 20;

    /// <summary>
    /// How many bytes of decoded images of at most <see cref="SmallImageBytes"/> one render keeps
    /// to draw again: 64 MiB, sixty-four of the largest.
    /// </summary>
    internal const long MaximumSmallImageBytesKept = 64L << 20;

    private readonly RenderContext _context;
    private readonly PaintRouter _device;
    private readonly PageView _view;
    private readonly RenderFontCache _fonts;
    private readonly Func<PdfDictionary, PageGlyphSource> _glyphSources;
    private readonly DiagnosticSink _sink;
    private readonly StateStack _states;
    private readonly Dictionary<object, ParsedContent> _formContent;
    private bool _needAppearancesReported;

    // Everything this instance draws into, with its transform to the device: the page's box, or
    // the box of content that is recorded. It is what sh paints, under whatever the clip leaves.
    private readonly PagePath _surface;
    private readonly XMatrix _surfaceTransform;

    // Set for the cell of an uncoloured tiling pattern, whose colour is the one it is painted
    // in: every operator that sets a colour is passed over (ISO 32000-1 8.7.3.3).
    private readonly bool _colorsLocked;

    // Whether this instance records. What it draws is then placed on a device later, under a
    // transform not known here, so nothing is judged by its size in pixels.
    private readonly bool _isRecording;

    // The marked-content sequences open in the stream being drawn, innermost last, each whether it
    // is optional content that is switched off, and how many of them are.
    private Stack<bool> _marked = new();
    private int _hiddenMarks;

    // What belongs to the content stream being drawn. A form XObject has its own of each, and
    // RunStream puts the caller's back when the form ends.
    private ResourceScope _resources;

    // The default coordinate space of the stream being drawn, as a transform to the device: the
    // space a pattern's /Matrix is over, whatever cm has done since the stream began.
    private XMatrix _streamSpace;
    private PathInProgress _path = new();
    private XFillMode? _pendingClip;
    private TextObject _text;
    private int _compatibilityDepth;
    private int _floor;
    private int _uncountedSaves;

    internal ContentInterpreter(PageDevice device, PageView view, RenderFontCache fonts,
        Func<PdfDictionary, PageGlyphSource> glyphSources, PageOptionalContent optionalContent = null)
        : this(new RenderContext(fonts, glyphSources) { OptionalContentSelection = optionalContent }, device,
            new GraphicsState { Ctm = view.Transform }, view.Box, colorsLocked: false, isRecording: false)
    {
        _view = view;
    }

    private ContentInterpreter(RenderContext context, PageDevice device, GraphicsState initial, XRect surface,
        bool colorsLocked, bool isRecording)
    {
        _context = context;
        _sink = context.Sink;
        _fonts = context.Fonts;
        _glyphSources = context.GlyphSources;
        _formContent = context.FormContent;
        _colorsLocked = colorsLocked;
        _isRecording = isRecording;

        var guarded = new GuardedDevice(device, _sink);
        context.Decoder ??= guarded;
        _device = new PaintRouter(guarded, _sink);
        _states = new StateStack(guarded, initial);

        var box = new PagePathBuilder();
        box.AddRectangle(surface.X, surface.Y, surface.Width, surface.Height);
        _surface = box.ToPath();
        _surfaceTransform = initial.Ctm;
    }

    private GraphicsState State => _states.Current;

    /// <summary>What the render left out.</summary>
    internal PageRenderResult Result => _sink.ToResult();

    /// <summary>
    /// Draws a page: clips to the box the view draws, then runs the page's content. Whatever the
    /// content does, and whatever is thrown, the device has been restored as often as it was
    /// saved when this returns.
    /// </summary>
    internal void RunPage(PdfPage page, bool includeAnnotations)
    {
        try
        {
            BeginPage();

            if (_view.BoxIsAssumed)
            {
                _sink.Report(PageRenderProblem.InvalidValue, "page",
                    "The page states no box with any area. It is drawn as a US Letter page.");
            }

            _context.Document = page.Owner;
            if (!PdfContentStreams.TryGetReadablePageContent(page, out var content, out var unreadable))
            {
                _sink.Report(PageRenderProblem.UnreadableContent, "page",
                    "The page's content could not be decoded, and nothing of it is drawn.");
            }
            else
            {
                if (unreadable > 0)
                {
                    _sink.Report(PageRenderProblem.UnreadableContent, "page",
                        $"{unreadable} of the streams the page's content is written in could not be decoded. What the others say is drawn.");
                }

                RunStream(ParsedContent.Of(content), ResourceScope.OfPage(page), "page", _surfaceTransform);
            }

            if (includeAnnotations)
                DrawAnnotations(page);
        }
        finally
        {
            _states.PopTo(0);
        }
    }

    /// <summary>Saves the device's state and clips to the box the view draws.</summary>
    private void BeginPage()
    {
        _states.Push();
        _device.Clip(_surface, _surfaceTransform, XFillMode.Winding);
    }

    /// <summary>
    /// Runs a content stream apart from the device, from a graphics state of its own, and
    /// answers what it drew as a picture. A tiling pattern's cell is recorded by this, and a
    /// transparency group is content of the same kind.
    /// </summary>
    /// <param name="context">The render the content is part of.</param>
    /// <param name="content">The content.</param>
    /// <param name="resources">What its names are looked up in.</param>
    /// <param name="streamName">What its problems are reported against.</param>
    /// <param name="initialState">
    /// The graphics state it starts from. Its transform is the content's default space, and the
    /// space the picture's coordinates are in where that is the identity.
    /// </param>
    /// <param name="box">What the content is clipped to, in its default space.</param>
    /// <param name="colorsLocked">Whether the colours of <paramref name="initialState"/> are the only ones it paints in.</param>
    internal static PagePicture Record(RenderContext context, ParsedContent content, ResourceScope resources,
        string streamName, GraphicsState initialState, XRect box, bool colorsLocked)
    {
        var recorder = new PictureRecorder(context.Decoder);
        var nested = new ContentInterpreter(context, recorder, initialState, box, colorsLocked, isRecording: true);
        try
        {
            nested.BeginPage();
            nested.RunStream(content, resources, streamName, nested._surfaceTransform);
        }
        finally
        {
            nested._states.PopTo(0);
        }

        if (recorder.Overflowed)
        {
            context.Sink.Report(PageRenderProblem.LimitReached, streamName,
                "The content draws more than one picture keeps. What comes after the limit is not drawn.");
        }

        return recorder.ToPicture();
    }

    /// <summary>
    /// Runs one content stream, the page's or a form's, to its end. What the stream saves and
    /// leaves unrestored is restored when it ends, so a stream cannot leave anything on the stack
    /// of whatever drew it, and a <c>Q</c> in it cannot reach past where it began.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="resources">What its names are looked up in.</param>
    /// <param name="streamName">What its problems are reported against.</param>
    /// <param name="space">The stream's default coordinate space, as a transform to the device.</param>
    private void RunStream(ParsedContent content, ResourceScope resources, string streamName, XMatrix space)
    {
        var faults = content.Faults;
        var sequence = content.Sequence;

        var outer = (_resources, _streamSpace, _path, _pendingClip, _text, _compatibilityDepth, _floor,
            _uncountedSaves, _sink.Stream, _sink.OperatorIndex, _sink.Operator);
        _resources = resources;
        _streamSpace = space;
        _path = new PathInProgress();
        _pendingClip = null;
        _text = null;
        _compatibilityDepth = 0;
        _floor = _states.Depth;
        _uncountedSaves = 0;
        var outerMarked = _marked;
        _marked = new Stack<bool>();

        // A stream of its own is not part of the marked content it is drawn from: a form is
        // not entered while that is hidden, and a pattern cell or soft mask is its own content.
        // So it starts with no sequence open and nothing hidden, whatever drew it.
        var outerHiddenMarks = _hiddenMarks;
        _hiddenMarks = 0;
        _device.Hidden = false;
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
            if (content.LeftUnread)
            {
                _sink.Report(PageRenderProblem.LimitReached, streamName,
                    "Too much of the content stream could not be read. What follows the last fault is not drawn.");
            }

            // A text object the content never ended still clips, as it would have at its ET.
            if (_text != null)
                EndText();

            if (_marked.Count > 0)
            {
                _sink.Report(PageRenderProblem.OperatorOutOfPlace, "BDC",
                    "A marked-content sequence is never ended. It is ended where the content stream ends.");
            }
        }
        finally
        {
            while (_marked.Count > 0)
                EndMarkedContent(report: false);
            _marked = outerMarked;
            _hiddenMarks = outerHiddenMarks;
            _device.Hidden = _hiddenMarks > 0;
            _states.PopTo(_floor);
            (_resources, _streamSpace, _path, _pendingClip, _text, _compatibilityDepth, _floor,
                _uncountedSaves, _sink.Stream, _sink.OperatorIndex, _sink.Operator) = outer;
        }
    }

    /// <summary>
    /// A content stream as the parser read it: its operators, and the faults read past on the way.
    /// Nothing that draws it changes it, so one reading serves every time a form is drawn.
    /// </summary>
    internal sealed class ParsedContent
    {
        private ParsedContent(CSequence sequence, List<ContentFault> faults, bool leftUnread)
        {
            Sequence = sequence;
            Faults = faults;
            LeftUnread = leftUnread;
        }

        internal CSequence Sequence { get; }

        internal List<ContentFault> Faults { get; }

        /// <summary>Whether the reading stopped at <see cref="MaximumFaults"/>, short of the end.</summary>
        internal bool LeftUnread { get; }

        internal static ParsedContent Of(byte[] content)
        {
            var faults = new List<ContentFault>();
            var sequence = ContentReader.ReadPastFaults(content, faults, MaximumFaults, out var leftUnread);
            return new ParsedContent(sequence, faults, leftUnread);
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
                // A shading in hidden content paints nothing, so it is not read and nothing is reported.
                if (!_device.Hidden)
                    PaintShading(op);
                break;

            case OpCodeName.BMC: PushMarkedContent(hidden: false); break;
            case OpCodeName.BDC: BeginMarkedContent(op); break;
            case OpCodeName.EMC: EndMarkedContent(report: true); break;

            case OpCodeName.BX: _compatibilityDepth++; break;
            case OpCodeName.EX: _compatibilityDepth = Math.Max(0, _compatibilityDepth - 1); break;

            // Everything else says nothing about what is drawn: the rest of marked content, the rendering
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

    /// <summary>Where every operator that sets a colour ends.</summary>
    private void SetColor(bool stroking, RenderColorSpace space, RenderPaint color)
    {
        if (_colorsLocked)
            return;

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
            SetColor(stroking, space, space is PatternColorSpace ? RenderPaint.Nothing : space.InitialColor);
    }

    /// <summary><c>sc</c>, <c>SC</c>, <c>scn</c> and <c>SCN</c>.</summary>
    private void SetColorInSpace(COperator op, bool stroking)
    {
        var space = stroking ? State.StrokeSpace : State.FillSpace;
        if (space is PatternColorSpace patternSpace)
        {
            SetPatternColor(op, stroking, patternSpace);
            return;
        }

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
