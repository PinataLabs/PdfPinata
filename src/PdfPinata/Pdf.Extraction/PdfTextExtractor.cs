using System;
using System.Collections.Generic;
using System.Text;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Content;
using PdfPinata.Pdf.Content.Objects;
using PdfPinata.Pdf.Structure;

namespace PdfPinata.Pdf.Extraction;

/// <summary>
/// Reads the text back out of a page: what it says and where on the page it says it.
/// </summary>
/// <remarks>
/// <para>
/// The content-stream parser has always been here — <see cref="ContentReader"/> and the operator
/// table in <c>Pdf.Content.Objects</c> — and nothing turned what it produced into text. This walks
/// the operators keeping the text state the specification describes, decodes each show-text operand
/// through the font's <c>/ToUnicode</c> map, and reports where the run landed.
/// </para>
/// <para>
/// <b>One run per show-text operator, not one box per glyph.</b> A per-glyph box is exact only when
/// every glyph's own advance is known, and reporting an approximate one is worse than reporting
/// none: a caller cannot tell the two apart. A run's origin and total width are exact, and are what
/// most callers actually want.
/// </para>
/// <para>
/// <b>Not layout analysis.</b> Runs come back in the order they were drawn, which is the order the
/// producer chose and need not be reading order. Grouping runs into columns, paragraphs and a
/// reading sequence — recursive XY-cut and its relatives — is a separate piece of work and is not
/// here.
/// </para>
/// </remarks>
public static class PdfTextExtractor
{
    /// <summary>
    /// Every run of text on the page, in the order it was drawn.
    /// </summary>
    public static IReadOnlyList<PdfTextRun> ExtractRuns(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var walker = new Walker(page);
        walker.Walk(ContentReader.ReadContent(page));
        return walker.Runs;
    }

    /// <summary>
    /// The text of the page, with a line break wherever the baseline moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A convenience over <see cref="ExtractRuns"/> and no cleverer than it: runs sharing a
    /// baseline are joined with a space when there is a gap between them and directly when there is
    /// not, and a new baseline starts a new line. That is right for ordinary running text and wrong
    /// for a two-column page, which is what the note about layout analysis above is warning of.
    /// </para>
    /// <para>
    /// <b>Runs <see cref="PdfTextRun.IsArtifact"/> are left out</b> — a running head or a folio is
    /// on the page and not part of what it says, and <see cref="ExtractRuns"/> still returns them
    /// for a caller who wants them anyway. And <b>a run whose <see cref="PdfTextRun.ActualText"/> is
    /// declared contributes that text once</b>, at the position of the first run of the sequence
    /// that declared it, rather than its own glyph text at every run the sequence spans — which is
    /// what turns a hyphenated word drawn as two runs back into one word.
    /// </para>
    /// </remarks>
    public static string ExtractText(PdfPage page)
    {
        var text = new StringBuilder();
        double? baseline = null;
        var endOfPrevious = 0.0;
        var contributed = new HashSet<object>();

        foreach (var run in ExtractRuns(page))
        {
            if (run.IsArtifact)
                continue;

            string shown;
            if (run.ActualTextScope != null)
            {
                // The sequence that declared this text may span several runs, and it is only the
                // first of them that gets to say it — the rest were already shown once, by it.
                if (!contributed.Add(run.ActualTextScope))
                    continue;

                shown = run.ActualText;
            }
            else
            {
                shown = run.Text;
            }

            if (shown.Length == 0)
                continue;

            if (baseline == null || Math.Abs(run.Origin.Y - baseline.Value) > 0.5)
            {
                if (baseline != null)
                    text.Append('\n');
                baseline = run.Origin.Y;
            }
            else if (run.Origin.X - endOfPrevious > run.FontSize * 0.2)
            {
                // A gap wider than a fifth of the type size is a space somebody drew by moving the
                // pen rather than by showing one.
                text.Append(' ');
            }

            text.Append(shown);
            endOfPrevious = run.Origin.X + run.Width;
        }

        return text.ToString();
    }

    /// <summary>
    /// Walks a content stream keeping the graphics and text state, and records what was shown.
    /// </summary>
    private sealed class Walker
    {
        private readonly PdfPage _page;
        private readonly Dictionary<string, FontInfo> _fonts = new();
        private readonly Stack<XMatrix> _graphicsStack = new();
        private readonly Stack<MarkedContentScope> _markedContent = new();

        private XMatrix _ctm = XMatrix.Identity;
        private XMatrix _textMatrix = XMatrix.Identity;
        private XMatrix _lineMatrix = XMatrix.Identity;

        private FontInfo _font;
        private string _fontName;
        private double _fontSize;
        private double _charSpacing;
        private double _wordSpacing;
        private double _horizontalScale = 1;
        private double _leading;
        private double _rise;
        private int _renderMode;

        public Walker(PdfPage page) => _page = page;

        public List<PdfTextRun> Runs { get; } = [];

        /// <remarks>
        /// Indexed rather than iterated. <see cref="CSequence"/> implements
        /// <c>IEnumerable&lt;CObject&gt;</c> and its generic enumerator throws
        /// <see cref="NotImplementedException"/>, so a foreach over one compiles and then fails at
        /// run time. Indexing also lets <see cref="BeginTag"/> look one item back, which a
        /// <c>BDC</c> declaring its properties inline needs — see its remarks.
        /// </remarks>
        public void Walk(CSequence content)
        {
            for (var index = 0; index < content.Count; index++)
            {
                if (content[index] is COperator op)
                    Execute(content, index, op);
            }
        }

        private void Execute(CSequence content, int index, COperator op)
        {
            switch (op.OpCode.OpCodeName)
            {
                case OpCodeName.q:
                    _graphicsStack.Push(_ctm);
                    break;

                case OpCodeName.Q:
                    if (_graphicsStack.Count > 0)
                        _ctm = _graphicsStack.Pop();
                    break;

                case OpCodeName.cm:
                    _ctm = Multiply(MatrixOf(op, 0), _ctm);
                    break;

                case OpCodeName.BT:
                    _textMatrix = _lineMatrix = XMatrix.Identity;
                    break;

                case OpCodeName.Tf:
                    SelectFont(op);
                    break;

                case OpCodeName.Td:
                    Displace(Number(op, 0), Number(op, 1));
                    break;

                case OpCodeName.TD:
                    _leading = -Number(op, 1);
                    Displace(Number(op, 0), Number(op, 1));
                    break;

                case OpCodeName.Tm:
                    _textMatrix = _lineMatrix = MatrixOf(op, 0);
                    break;

                case OpCodeName.Tx:   // T*, next line
                    Displace(0, -_leading);
                    break;

                case OpCodeName.TL:
                    _leading = Number(op, 0);
                    break;

                case OpCodeName.Tc:
                    _charSpacing = Number(op, 0);
                    break;

                case OpCodeName.Tw:
                    _wordSpacing = Number(op, 0);
                    break;

                case OpCodeName.Tz:
                    _horizontalScale = Number(op, 0) / 100.0;
                    break;

                case OpCodeName.Ts:
                    _rise = Number(op, 0);
                    break;

                case OpCodeName.Tr:
                    _renderMode = (int)Number(op, 0);
                    break;

                case OpCodeName.Tj:
                    Show(op.Operands);
                    break;

                case OpCodeName.TJ:
                    Show(op.Operands.Count > 0 && op.Operands[0] is CArray array ? array : op.Operands);
                    break;

                case OpCodeName.QuoteSingle:
                    Displace(0, -_leading);
                    Show(op.Operands);
                    break;

                case OpCodeName.QuoteDbl:
                    // " sets word and character spacing before showing, and moves to the next line.
                    // Its operands are aw ac string, and the string starts at index 2 — showing from
                    // zero would read the two spacing numbers as though they were the kerning
                    // adjustments of a TJ array and displace every run after this one.
                    _wordSpacing = Number(op, 0);
                    _charSpacing = Number(op, 1);
                    Displace(0, -_leading);
                    Show(op.Operands, 2);
                    break;

                case OpCodeName.BDC:
                    BeginTag(content, index, op);
                    break;

                case OpCodeName.BMC:
                    // No properties are possible on this form, so no /MCID and no /ActualText —
                    // just the tag, straight off the operand the content parser gave it.
                    PushScope(new MarkedContentScope(NameOperand(op, 0), null, null));
                    break;

                case OpCodeName.EMC:
                    // A sequence opened past the depth cap was never pushed, so its end must not
                    // pop one that was — that would take back a scope some run still inside it is
                    // relying on. The uncapped count is what tells the two apart.
                    if (_uncappedMarkedContentDepth > 0)
                        _uncappedMarkedContentDepth--;
                    else if (_markedContent.Count > 0)
                        _markedContent.Pop();
                    break;

                // The content parser has no dictionary grammar, so a BDC that declares its
                // properties inline is split in two: this pseudo-operator carries the tag and the
                // raw dictionary text, and the BDC that follows carries neither. Handled by looking
                // back for it from BeginTag rather than here, because by itself it opens nothing.
                case OpCodeName.Dictionary:
                    break;
            }
        }

        /// <summary>
        /// Opens a marked-content sequence with properties, resolving them whichever way the
        /// producer wrote them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Named properties are a real dictionary already</b> — reached through the page's own
        /// resources, in their <c>/Properties</c> category, the same way a font is reached through
        /// its <c>/Font</c> category. A name that resolves to nothing is a sequence with no
        /// properties, not an error.
        /// </para>
        /// <para>
        /// <b>Inline properties never became an object</b>, so the item just before this one in the
        /// sequence — the <c>Dictionary</c> pseudo-operator <see cref="CParser"/> emits for exactly
        /// this case — carries the tag and the raw text together, and this <c>BDC</c> itself carries
        /// neither operand. See <see cref="InlineMarkedContentProperties"/> for what is read out of
        /// it.
        /// </para>
        /// </remarks>
        private void BeginTag(CSequence content, int index, COperator op)
        {
            string tag;
            string actualText = null;
            int? mcid = null;

            if (op.Operands.Count >= 2 && op.Operands[0] is CName direct)
            {
                tag = direct.Name;
                var properties = PropertiesFor(NameOperand(op, 1));
                if (properties != null)
                {
                    actualText = properties.Elements.TryGetString("/ActualText", out var declared)
                        ? declared : null;
                    mcid = properties.Elements.ContainsKey("/MCID") ? properties.Elements.GetInteger("/MCID") : null;
                }
            }
            else if (index > 0 && content[index - 1] is COperator prior
                     && prior.OpCode.OpCodeName == OpCodeName.Dictionary
                     && prior.Operands.Count >= 2
                     && prior.Operands[0] is CName inlineTag
                     && prior.Operands[1] is CString inlineDictionary)
            {
                tag = inlineTag.Name;
                (actualText, mcid) = InlineMarkedContentProperties.Read(inlineDictionary.Value);
            }
            else
            {
                // Neither shape matched — a BDC with too few operands and nothing to look back to.
                // Malformed input degrades rather than aborts: whatever tag can be salvaged is kept
                // and the sequence carries no properties.
                tag = NameOperand(op, 0);
            }

            PushScope(new MarkedContentScope(tag, actualText, mcid));
        }

        /// <summary>
        /// Opens a sequence, tracking it on the stack unless doing so would take the nesting depth
        /// past anything sane — content that has gone wrong rather than a document that means it.
        /// </summary>
        /// <remarks>
        /// A sequence past the cap is still counted, in <see cref="_uncappedMarkedContentDepth"/>
        /// rather than on the stack itself, so that its own <c>EMC</c> is recognised as its own
        /// rather than mistaken for the deepest tracked sequence's — which would pop a scope some
        /// run still inside it is relying on, and desynchronise every tag, MCID and ActualText
        /// reported for the rest of the page. A run drawn inside an uncapped sequence still reports
        /// the deepest tracked one, which is the graceful degradation this cap exists for.
        /// </remarks>
        private void PushScope(MarkedContentScope scope)
        {
            if (_markedContent.Count < MaximumMarkedContentDepth)
                _markedContent.Push(scope);
            else
                _uncappedMarkedContentDepth++;
        }

        private const int MaximumMarkedContentDepth = 1024;

        /// <summary>
        /// How many open sequences past <see cref="MaximumMarkedContentDepth"/> have not been
        /// closed yet — each one's own <c>EMC</c> to come, still owed before the stack's own
        /// entries may be popped again.
        /// </summary>
        private int _uncappedMarkedContentDepth;

        private PdfDictionary PropertiesFor(string name)
        {
            return name == null ? null : ResourceCategory("/Properties")?.Elements.GetDictionary(name);
        }

        private static string NameOperand(COperator op, int index) =>
            index < op.Operands.Count && op.Operands[index] is CName name ? name.Name : null;

        private void SelectFont(COperator op)
        {
            _fontSize = Number(op, 1);
            _fontName = op.Operands.Count > 0 && op.Operands[0] is CName name ? name.Name : null;
            _font = _fontName == null ? null : FontFor(_fontName);
        }

        private void Displace(double tx, double ty)
        {
            _lineMatrix = Multiply(new XMatrix(1, 0, 0, 1, tx, ty), _lineMatrix);
            _textMatrix = _lineMatrix;
        }

        /// <summary>
        /// Shows a string, or the mixture of strings and kerning adjustments a <c>TJ</c> array is.
        /// </summary>
        private void Show(CSequence operands, int from = 0)
        {
            // Text render mode 3 is invisible — the OCR layer under a scan is drawn that way, and a
            // caller asking what the page says usually does not want it twice. It is skipped rather
            // than reported, which is a decision this class makes and does not expose; the day
            // somebody needs it, it becomes an option.
            if (_font == null || _renderMode == 3)
                return;

            var text = new StringBuilder();
            var advance = 0.0;
            var origin = OriginOfCurrentPoint();

            for (var index = from; index < operands.Count; index++)
            {
                switch (operands[index])
                {
                    case CString shown:
                        advance += Append(text, shown.Value);
                        break;

                    case CInteger kerning:
                        advance += -kerning.Value / 1000.0 * _fontSize * _horizontalScale;
                        break;

                    case CReal kerning:
                        advance += -kerning.Value / 1000.0 * _fontSize * _horizontalScale;
                        break;
                }
            }

            if (text.Length > 0)
            {
                // Both the width and the size are measured through the same matrix, and they have to
                // be: the run is reported in user space, so leaving the current transformation out
                // of one of them makes a run under a scaled container report a width in text space
                // and a size in user space, which disagree with each other and with the documented
                // contract. A test that only translates cannot see it, because a translation scales
                // by one.
                var scale = ScaleOf(Multiply(_textMatrix, _ctm));
                var innermost = _markedContent.Count > 0 ? _markedContent.Peek() : null;
                var declaring = DeclaringScope();
                Runs.Add(new PdfTextRun(text.ToString(), origin, advance * scale,
                    _fontSize * scale, _fontName,
                    string.IsNullOrEmpty(innermost?.Tag) ? null : new PdfTag(innermost.Tag),
                    declaring?.ActualText, innermost?.Mcid, declaring, IsInsideArtifact()));
            }

            _textMatrix = Multiply(new XMatrix(1, 0, 0, 1, advance, 0), _textMatrix);
        }

        /// <summary>
        /// Decodes one shown string and answers how far it advances the pen.
        /// </summary>
        private double Append(StringBuilder text, string shown)
        {
            var advance = 0.0;
            var codeLength = _font.CodeLength;

            for (var at = 0; at + codeLength <= shown.Length; at += codeLength)
            {
                var code = 0;
                for (var b = 0; b < codeLength; b++)
                    code = (code << 8) | (shown[at + b] & 0xFF);

                text.Append(_font.TextFor(code));

                // The advance of a glyph, then the spacing the state adds after it. Word spacing
                // applies to the single byte 32 and to nothing else — not to a two-byte code whose
                // low byte happens to be 32, which is the trap in this arithmetic.
                var width = _font.WidthOf(code) * _fontSize + _charSpacing;
                if (code == 32 && codeLength == 1)
                    width += _wordSpacing;

                advance += width * _horizontalScale;
            }

            return advance;
        }

        private XPoint OriginOfCurrentPoint() =>
            Multiply(_textMatrix, _ctm).Transform(new XPoint(0, _rise));

        /// <summary>
        /// The innermost open sequence that declares <c>/ActualText</c>, searching from the top of
        /// the marked-content stack outward — so a plain span inside a sequence that declares
        /// substitute text still reports that text, and one drawn inside two such sequences reports
        /// the nearer one.
        /// </summary>
        private MarkedContentScope DeclaringScope()
        {
            foreach (var scope in _markedContent)
            {
                if (scope.ActualText != null)
                    return scope;
            }

            return null;
        }

        /// <summary>
        /// Whether an artifact sequence is open anywhere on the stack, not only innermost. An
        /// artifact is not a container its contents can opt out of — this library's own writer never
        /// nests a structural sequence inside one, but a document from another producer is free to,
        /// and glyphs drawn there are still furniture whatever the nearer tag claims to be.
        /// </summary>
        private bool IsInsideArtifact()
        {
            foreach (var scope in _markedContent)
            {
                if (scope.Tag == ArtifactTagName)
                    return true;
            }

            return false;
        }

        private const string ArtifactTagName = "/Artifact";

        private FontInfo FontFor(string name)
        {
            if (_fonts.TryGetValue(name, out var known))
                return known;

            var dictionary = ResourceCategory("/Font")?.Elements.GetDictionary(name);

            var info = dictionary == null ? null : FontInfo.From(dictionary);
            _fonts[name] = info;
            return info;
        }

        /// <summary>
        /// A category of the page's resources — <c>/Font</c>, <c>/Properties</c> and so on — read
        /// the way every resource lookup in this class does: through the page's own
        /// <c>/Resources</c> entry first, and through the object model's own copy when that entry
        /// is missing or was never linked back to the page it belongs to.
        /// </summary>
        private PdfDictionary ResourceCategory(string category) =>
            _page.Elements.GetDictionary("/Resources")?.Elements.GetDictionary(category)
            ?? _page.Resources?.Elements.GetDictionary(category);

        private static XMatrix Multiply(XMatrix first, XMatrix second) => XMatrix.Multiply(first, second);

        /// <summary>
        /// How much a matrix scales along its own X axis, which is what turns a width in text space
        /// into one in user space.
        /// </summary>
        private static double ScaleOf(XMatrix matrix) =>
            Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);

        private static XMatrix MatrixOf(COperator op, int from) => new(
            Number(op, from), Number(op, from + 1), Number(op, from + 2),
            Number(op, from + 3), Number(op, from + 4), Number(op, from + 5));

        private static double Number(COperator op, int index) =>
            index < op.Operands.Count && op.Operands[index] is CNumber number
                ? (number is CInteger integer ? integer.Value : ((CReal)number).Value)
                : 0;
    }

    /// <summary>
    /// One open <c>BDC</c> or <c>BMC</c> sequence, as far as extraction cares what it says: its tag,
    /// the text it declares its glyphs stand for if it declares any, and its <c>/MCID</c> if it
    /// carries one. The identity of the instance is what <see cref="PdfTextRun.ActualTextScope"/>
    /// hands back — a run inside this sequence, not a copy of what it says.
    /// </summary>
    private sealed class MarkedContentScope
    {
        internal MarkedContentScope(string tag, string actualText, int? mcid)
        {
            Tag = tag;
            ActualText = actualText;
            Mcid = mcid;
        }

        internal string Tag { get; }
        internal string ActualText { get; }
        internal int? Mcid { get; }
    }
}
