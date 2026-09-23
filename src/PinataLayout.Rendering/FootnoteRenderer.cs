using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Drawing;
using PdfPinata.Pdf.Structure;

namespace PinataLayout.Rendering;

/// <summary>
/// Draws a page's footnotes: the separator rule, and each note under it with its mark in the
/// margin the note's first line was indented by.
/// </summary>
/// <remarks>
/// Not a <see cref="Renderer"/>. A renderer draws one document object from a
/// <see cref="RenderInfo"/> the formatter produced for it, and this draws a band the formatter
/// reserved rather than an object it laid out. What it does share is the shifting trick:
/// a note is formatted at the origin and moved into place on the way out, which is how
/// <see cref="Renderer.RenderByInfos(XUnit, XUnit, RenderInfo[])"/> puts a nested layout anywhere.
/// </remarks>
internal class FootnoteRenderer
{
    internal FootnoteRenderer(XGraphics gfx, DocumentRenderer documentRenderer, FieldInfos fieldInfos)
    {
        _gfx = gfx;
        _documentRenderer = documentRenderer;
        _fieldInfos = fieldInfos;
    }

    /// <summary>
    /// Draws the block. <paramref name="top"/> is the top of the separator band, and the notes
    /// follow underneath it.
    /// </summary>
    internal void Render(IReadOnlyList<Footnote> notes, XUnit left, XUnit top, XUnit width)
    {
        if (notes.Count == 0)
            return;

        DrawSeparator(left, top, width);

        XUnit y = top + FormattedDocument.FootnoteSeparatorBand;
        foreach (var note in notes)
        {
            var formatted = _documentRenderer.Footnotes.FormattedOf(note);
            if (formatted == null)
                continue;

            var renderInfos = formatted.GetRenderInfos();

            // The element the paragraph renderer built where this note was cited. Entered rather than
            // marked: the note draws its content through renderers of its own, which mark what they
            // draw, and a /Note holding marks directly as well would claim that some of the page
            // belongs to the note rather than to the paragraphs inside it.
            var element = Tagger.FootnoteFor(_gfx, note);
            using (Tagger.Enter(element))
            {
                // The note's text sits to the right of the gutter it was laid out to leave; the mark
                // goes in the gutter, so the two cannot run into one another however wide the mark is.
                RenderByInfos(left + formatted.Indent, y, renderInfos);
                DrawMark(note, formatted, left, y, element);
            }

            y += formatted.ContentHeight;
        }
    }

    /// <summary>
    /// The rule between the body text and the notes. A third of the column at hairline weight,
    /// which is what a reader expects and what Word and LaTeX both draw.
    /// </summary>
    /// <remarks>
    /// Drawn as an artifact, because that is what it is: a rule saying where the body text stops and
    /// the notes start, carrying nothing a reader needs read to them. Everything on a page is either
    /// content or furniture, and a line that is neither is the first thing a validator objects to.
    /// </remarks>
    private void DrawSeparator(XUnit left, XUnit top, XUnit width)
    {
        using (Tagger.Artifact(_gfx))
        {
            XUnit y = top + FormattedDocument.FootnoteSeparatorOffset;
            _gfx.DrawLine(new XPen(XColors.Black, 0.5), left, y, left + width / 3, y);
        }
    }

    private StructureTagger Tagger => _documentRenderer.Tagger;

    /// <summary>
    /// The note's own mark, drawn into the indent its first paragraph was formatted with.
    /// </summary>
    /// <remarks>
    /// Drawn here rather than made part of the note's content, because the note's content is the
    /// caller's and putting a generated number into it would change what a caller reads back out
    /// of their own document object model.
    /// </remarks>
    private void DrawMark(Footnote note, FormattedFootnote formatted, XUnit left, XUnit top,
        PdfStructureElement element)
    {
        var mark = _documentRenderer.Footnotes.MarkFor(note);
        if (mark.Length == 0)
            return;

        var font = formatted.NoteFont(_gfx);
        var raised = FontHandler.ToSubSuperFont(font);

        // Raised off the note's first line exactly as the reference mark is raised off the line it
        // sits in, and by the same arithmetic - see ParagraphRenderer.FootnoteMarkBaseline. Setting
        // it on the note's own baseline instead puts a small numeral level with the text, which
        // reads as part of the sentence rather than as the mark that names it.
        XUnit baseline = top + FontHandler.GetSubSuperScaling(font)
            * (font.GetHeight() - FontHandler.GetDescent(font));

        using (Tagger.FootnoteLabel(_gfx, note, element))
        {
            _gfx.DrawString(mark, raised, new XSolidBrush(XColors.Black), left, baseline);
        }
    }

    private void RenderByInfos(XUnit xShift, XUnit yShift, RenderInfo[] renderInfos)
    {
        if (renderInfos == null)
            return;

        foreach (var renderInfo in renderInfos)
        {
            var savedX = renderInfo.LayoutInfo.ContentArea.X;
            var savedY = renderInfo.LayoutInfo.ContentArea.Y;
            renderInfo.LayoutInfo.ContentArea.X += xShift;
            renderInfo.LayoutInfo.ContentArea.Y += yShift;
            var renderer = Renderer.Create(_gfx, _documentRenderer, renderInfo, _fieldInfos);
            renderer.Render();
            renderInfo.LayoutInfo.ContentArea.X = savedX;
            renderInfo.LayoutInfo.ContentArea.Y = savedY;
        }
    }

    private readonly XGraphics _gfx;
    private readonly DocumentRenderer _documentRenderer;
    private readonly FieldInfos _fieldInfos;
}
