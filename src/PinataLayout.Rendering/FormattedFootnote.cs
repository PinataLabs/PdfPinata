using System;
using System.Collections;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// One footnote's block content, laid out into a column of a given width.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="FormattedTextArea"/>: an <see cref="IAreaProvider"/> that hands out
/// exactly one area, runs a <see cref="TopDownFormatter"/> over the note's elements, and keeps the
/// <see cref="RenderInfo"/>s for the render pass. Two differences. The width is given rather than
/// inherent, because a note is as wide as the text it belongs to. And the first line is indented by
/// the width of the reference mark, which is drawn into that indent - see
/// <see cref="FootnoteRenderer"/>.
/// </para>
/// <para>
/// The height offered is unbounded. A note that will not fit on the page moves whole to the next
/// one rather than splitting, so nothing here needs to know how much room is left; deciding that is
/// <see cref="FormattedDocument"/>'s job, and it needs the note's full height to decide it with.
/// </para>
/// </remarks>
internal class FormattedFootnote : IAreaProvider
{
    internal FormattedFootnote(DocumentRenderer documentRenderer, Footnote footnote,
        FieldInfos fieldInfos, XUnit width)
    {
        _documentRenderer = documentRenderer;
        _footnote = footnote;
        _fieldInfos = fieldInfos;
        _width = width;
    }

    internal void Format(XGraphics gfx)
    {
        Indent = CalcIndent(gfx);
        _isFirstArea = true;
        _formatter = new TopDownFormatter(this, _documentRenderer, _footnote.Elements);
        _formatter.FormatOnAreas(gfx, false);
    }

    /// <summary>
    /// The gutter the note's text is held off the left margin by, which the mark is drawn in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A hanging indent, so the mark stands in the margin and every line of the note lines up
    /// under the first - which is how a footnote has looked since long before anyone typeset one
    /// with a computer, and, more to the point, the only arrangement in which a mark and the text
    /// beside it cannot collide.
    /// </para>
    /// <para>
    /// Measured from the mark rather than fixed, so a long one - <c>viii</c> under roman
    /// numbering, or a dagger the caller set as the <c>Reference</c> - is not written over the
    /// first word. Consecutive notes on a page almost always carry marks of the same width, so in
    /// practice they line up with each other as well.
    /// </para>
    /// </remarks>
    private XUnit CalcIndent(XGraphics gfx)
    {
        var mark = _documentRenderer.Footnotes.MarkFor(_footnote);
        var font = FontHandler.ToSubSuperFont(NoteFont(gfx));
        XUnit width = mark.Length > 0 ? gfx.MeasureString(mark, font).Width : 0;

        // Never less than the gap, so a note whose mark is empty still sits clear of the margin
        // and reads as a note rather than as another paragraph of the body.
        return XUnit.FromPoint(Math.Max(width.Point + MarkGap.Point, MarkGap.Point * 3));
    }

    /// <summary>The face the note is set in - its own style, or the predefined Footnote one.</summary>
    internal XFont NoteFont(XGraphics gfx)
    {
        var document = _footnote.Document;
        var style = document.Styles[
                        _footnote.Style.Length > 0 ? _footnote.Style : StyleNames.Footnote]
                    ?? document.Styles[StyleNames.Normal];

        return FontHandler.FontToXFont(style.Font, _documentRenderer.PrivateFonts, gfx.MUH);
    }

    /// <summary>How far the mark sits from the text it belongs to.</summary>
    private static readonly XUnit MarkGap = XUnit.FromPoint(2);

    /// <summary>The gutter the mark is drawn in, to the left of the note's own text.</summary>
    internal XUnit Indent { get; private set; }

    /// <summary>The note as laid out, top to bottom.</summary>
    internal RenderInfo[] GetRenderInfos()
    {
        if (_renderInfos == null)
            return [];

        // Not ToArray(Type): it builds the array type at run time, which carries
        // RequiresDynamicCode and an AOT compiler cannot always have code for.
        var result = new RenderInfo[_renderInfos.Count];
        _renderInfos.CopyTo(result);
        return result;
    }

    /// <summary>How tall the note came out.</summary>
    internal XUnit ContentHeight => RenderInfo.GetTotalHeight(GetRenderInfos());

    internal Footnote Footnote => _footnote;

    Area IAreaProvider.GetNextArea()
    {
        if (!_isFirstArea)
            return null;

        // Narrower than the column by the gutter the mark stands in. The whole note is laid out
        // inside that narrower column and shifted right when it is drawn, which is what makes the
        // indent a hanging one rather than an indent on the first line alone.
        _isFirstArea = false;
        return new Rectangle(0, 0, _width - Indent, double.MaxValue);
    }

    Area IAreaProvider.ProbeNextArea() => null;

    FieldInfos IAreaProvider.AreaFieldInfos => _fieldInfos;

    void IAreaProvider.StoreRenderInfos(ArrayList renderInfos) => _renderInfos = renderInfos;

    bool IAreaProvider.IsAreaBreakBefore(LayoutInfo layoutInfo) => false;

    bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo) => false;

    bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo) => false;

    private readonly DocumentRenderer _documentRenderer;
    private readonly Footnote _footnote;
    private readonly FieldInfos _fieldInfos;
    private readonly XUnit _width;

    private TopDownFormatter _formatter;
    private ArrayList _renderInfos;
    private bool _isFirstArea;
}
