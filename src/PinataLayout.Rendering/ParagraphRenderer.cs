#region Copyright
//
// Authors:
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//
// Copyright (c) 2001-2009 empira Software GmbH, Cologne (Germany)
//
// http://www.PdfPinata.com
// http://www.migradoc.com
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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using PinataLayout.DocumentObjectModel;
using PdfPinata.Pdf;
using PdfPinata.Drawing;
using PdfPinata.Drawing.Layout;
using PdfPinata.Text;
using PinataLayout.DocumentObjectModel.Fields;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.Rendering.Resources;
using PdfPinata.Fonts;
using PdfPinata.Pdf.Structure;

using PdfPinata;

namespace PinataLayout.Rendering;

internal struct TabOffset
{
    internal TabOffset(TabLeader leader, XUnit offset)
    {
        this.leader = leader;
        this.offset = offset;
    }
    internal TabLeader leader;
    internal XUnit offset;
}

/// <summary>
/// Summary description for ParagraphRenderer.
/// </summary>
internal class ParagraphRenderer : Renderer
{
    /// <summary>
    /// Process phases of the renderer.
    /// </summary>
    private enum Phase
    {
        Formatting,
        Rendering
    }

    /// <summary>
    /// Results that can occur when processing a paragraph element
    /// during formatting.
    /// </summary>
    private enum FormatResult
    {
        /// <summary>
        /// Ignore the current element during formatting.
        /// </summary>
        Ignore,

        /// <summary>
        /// Continue with the next element within the same line.
        /// </summary>
        Continue,

        /// <summary>
        /// Start a new line from the current object on.
        /// </summary>
        NewLine,

        /// <summary>
        /// Break formatting and continue in a new area (e.g. a new page).
        /// </summary>
        NewArea
    }
    private Phase phase;

    /// <summary>
    /// Initializes a ParagraphRenderer object for formatting.
    /// </summary>
    /// <param name="gfx">The XGraphics object to do measurements on.</param>
    /// <param name="paragraph">The paragraph to format.</param>
    /// <param name="fieldInfos">The field infos.</param>
    internal ParagraphRenderer(XGraphics gfx, Paragraph paragraph, FieldInfos fieldInfos)
        : base(gfx, paragraph, fieldInfos)
    {
        this.paragraph = paragraph;

        var parRenderInfo = new ParagraphRenderInfo { paragraph = this.paragraph };
        ((ParagraphFormatInfo)parRenderInfo.FormatInfo).widowControl = this.paragraph.Format.WidowControl;

        renderInfo = parRenderInfo;
    }

    /// <summary>
    /// Initializes a ParagraphRenderer object for rendering.
    /// </summary>
    /// <param name="gfx">The XGraphics object to render on.</param>
    /// <param name="renderInfo">The render info object containing information necessary for rendering.</param>
    /// <param name="fieldInfos">The field infos.</param>
    internal ParagraphRenderer(XGraphics gfx, RenderInfo renderInfo, FieldInfos fieldInfos)
        : base(gfx, renderInfo, fieldInfos)
    {
        paragraph = (Paragraph)renderInfo.DocumentObject;
    }

    /// <summary>
    /// Renders the paragraph.
    /// </summary>
    internal override void Render()
    {
        InitRendering();
        if ((int)paragraph.Format.OutlineLevel >= 1 && Gfx.PdfPage != null) // Don't call GetOutlineTitle() in vain
            DocumentRenderer.AddOutline((int)paragraph.Format.OutlineLevel, GetOutlineTitle(),
                Gfx.PdfPage, OutlineDestinationTop());

        // Shading and borders are decoration, and they are drawn before the paragraph's own scope
        // opens rather than inside it. Nesting an artifact inside the content it decorates is legal
        // and says the wrong thing: the shading is not part of the paragraph, it is behind it.
        using (Tagger.Artifact(Gfx))
        {
            RenderShading();
            RenderBorders();
        }

        using (BeginStructure())
        {
            var parFormatInfo = (ParagraphFormatInfo)renderInfo.FormatInfo;
            FindBrokenWords(parFormatInfo);

            for (var idx = 0; idx < parFormatInfo.LineCount; ++idx)
            {
                var lineInfo = parFormatInfo.GetLineInfo(idx);
                isLastLine = idx == parFormatInfo.LineCount - 1;

                lastTabPosition = 0;
                if (lineInfo.reMeasureLine)
                    ReMeasureLine(ref lineInfo);

                RenderLine(lineInfo);
            }
        }
    }

    /// <summary>
    /// Works out what this paragraph is — a heading, a list item, or prose — and makes the element
    /// holding its lines current for the scope.
    /// </summary>
    /// <remarks>
    /// A list item is the awkward one, because the bullet and the text are siblings rather than one
    /// inside the other: <c>/LI</c> holds a <c>/Lbl</c> for the symbol and an <c>/LBody</c> for
    /// everything else. So the label is not opened here — <see cref="RenderLine"/> opens it around
    /// the symbol on the first line, and what this makes current is the body.
    /// </remarks>
    private IDisposable BeginStructure()
    {
        labelElement = null;

        if (!IsListItem(out var listType, out var listLevel))
        {
            Tagger.EndList();
            return Tagger.Block(Gfx, paragraph, TagOfParagraph());
        }

        var item = Tagger.ListItem(Gfx, paragraph, listType, listLevel);
        if (item == null)
            return StructureTagger.Nothing;

        labelElement = Tagger.Element(paragraph, PdfTag.Lbl, item, LabelSlot);

        var body = Tagger.Element(paragraph, PdfTag.LBody, item, StructureTagger.ListBodySlot);
        return Tagger.Marks(Gfx, body);
    }

    /// <summary>
    /// Which of a list paragraph's elements is meant. Slot 0 is the <c>/LI</c> itself, and the body's
    /// slot is <see cref="StructureTagger.ListBodySlot"/>, shared with the tagger so that the element
    /// it creates while opening a nested list is the same one this asks for afterwards.
    /// </summary>
    private const int LabelSlot = 1;

    /// <summary>
    /// Whether this paragraph draws a bullet or a number, and of what kind, and how deep it is nested.
    /// </summary>
    /// <remarks>
    /// Asked of the format info rather than of the format, and only in the rendering phase, so it
    /// agrees with what <see cref="RenderListSymbol"/> will actually draw — a paragraph carrying a
    /// <c>ListInfo</c> whose type is none of the six draws nothing, and a continuation of a split
    /// paragraph draws nothing either.
    /// </remarks>
    private bool IsListItem(out ListType listType, out int listLevel)
    {
        listType = ListType.BulletList1;
        listLevel = 1;
        if (!GetListSymbol(out _, out _))
            return false;

        var format = paragraph.Format;
        if (format.IsNull("ListInfo"))
            return false;

        listType = format.ListInfo.ListType;
        listLevel = format.ListInfo.NestingLevel;
        return true;
    }

    /// <summary>
    /// The structure type of this paragraph: a heading at its outline level, or prose.
    /// </summary>
    /// <remarks>
    /// From the outline level rather than from the style name, because the level is what the style
    /// sets and what a caller overrides per paragraph — a heading styled by hand still says so there.
    /// PDF has six heading levels and PinataLayout has nine, so the last three land on <c>/H6</c>: a
    /// heading too deep to name exactly is still a heading, and calling it a paragraph would lose
    /// more.
    /// </remarks>
    private PdfTag TagOfParagraph()
    {
        return (int)paragraph.Format.OutlineLevel switch
        {
            1 => PdfTag.H1,
            2 => PdfTag.H2,
            3 => PdfTag.H3,
            4 => PdfTag.H4,
            5 => PdfTag.H5,
            6 or 7 or 8 or 9 => PdfTag.H6,
            _ => PdfTag.P
        };
    }

    /// <summary>
    /// The label element of a list item, opened around the bullet on the first line only.
    /// </summary>
    private PdfStructureElement labelElement;

    /// <summary>
    /// What the field reads as, or what to show in its place while the real answer is not in yet.
    /// The value itself is <see cref="FieldEvaluator"/>'s to work out; what belongs here is the
    /// choice of stand-in, because that depends on which phase is asking rather than on the field.
    /// </summary>
    private string GetFieldValue(DocumentObject field)
    {
        var value = FieldEvaluator.Evaluate(field, fieldInfos.ToEvaluationContext());
        if (value != null)
            return value;

        // A bookmark still unresolved while formatting may yet be placed further down the page, so
        // the line is measured against a two-digit guess and measured again once it is known. By
        // rendering time there is nothing left to wait for, and the document is told so.
        if (field is PageRefField pageRefField && phase == Phase.Rendering)
            return string.Format(AppResources.BookmarkNotDefined, pageRefField.Name);

        // The other two are counts that only the end of formatting can supply, and a document's is
        // the one that can run to three digits.
        return field is NumPagesField ? "XXX" : "XX";
    }

    private string GetOutlineTitle()
    {
        var iter = new ParagraphIterator(paragraph.Elements);
        iter = iter.GetFirstLeaf();

        var ignoreBlank = true;
        var title = "";
        while (iter != null)
        {
            var current = iter.Current;
            if (!ignoreBlank && (IsBlank(current) || IsTab(current) || IsLineBreak(current)))
            {
                title += " ";
                ignoreBlank = true;
            }
            else if (current is Text text)
            {
                title += text.Content;
                ignoreBlank = false;
            }
            else if (FieldEvaluator.IsField(current))
            {
                title += GetFieldValue(current);
                ignoreBlank = false;
            }
            else if (IsSymbol(current))
            {
                title += GetSymbol((Character)current);
                ignoreBlank = false;
            }

            if (title.Length > 64)
                break;
            iter = iter.GetNextLeaf();
        }
        return title;
    }

    /// <summary>
    /// Gets a layout info with only margin and break information set.
    /// It can be taken before the paragraph is formatted.
    /// </summary>
    /// <remarks>
    /// The following layout information is set properly:<br />
    /// MarginTop, MarginLeft, MarginRight, MarginBottom, KeepTogether, KeepWithNext, PagebreakBefore.
    /// </remarks>
    internal override LayoutInfo InitialLayoutInfo
    {
        get
        {
            var layoutInfo = new LayoutInfo
            {
                PageBreakBefore = paragraph.Format.PageBreakBefore,
                MarginTop = paragraph.Format.SpaceBefore.Point,
                MarginBottom = paragraph.Format.SpaceAfter.Point,
                //Don't confuse margins with left or right indent.
                //Indents are invisible for the layouter.
                MarginRight = 0,
                MarginLeft = 0,
                KeepTogether = paragraph.Format.KeepTogether,
                KeepWithNext = paragraph.Format.KeepWithNext
            };
            return layoutInfo;
        }
    }

    /// <summary>
    /// Adjusts the current x position to the given tab stop if possible.
    /// </summary>
    /// <returns>True, if the text doesn't fit the line any more and the tab causes a line break.</returns>
    private FormatResult FormatTab()
    {
        // For Tabs in Justified context
        if (paragraph.Format.Alignment == ParagraphAlignment.Justify)
            reMeasureLine = true;
        var nextTabStop = GetNextTabStop();
        savedWordWidth = 0;
        if (nextTabStop == null)
            return FormatResult.NewLine;

        var notFitting = false;
        var xPositionBeforeTab = currentXPosition;
        switch (nextTabStop.Alignment)
        {
            case TabAlignment.Left:
                currentXPosition = ProbeAfterLeftAlignedTab(nextTabStop.Position.Point, out notFitting);
                break;

            case TabAlignment.Right:
                currentXPosition = ProbeAfterRightAlignedTab(nextTabStop.Position.Point, out notFitting);
                break;

            case TabAlignment.Center:
                currentXPosition = ProbeAfterCenterAlignedTab(nextTabStop.Position.Point, out notFitting);
                break;

            case TabAlignment.Decimal:
                currentXPosition = ProbeAfterDecimalAlignedTab(nextTabStop.Position.Point, out notFitting);
                break;
        }
        if (notFitting)
            return FormatResult.NewLine;

        // For correct right paragraph alignment with tabs
        if (!IgnoreHorizontalGrowth)
            currentLineWidth += currentXPosition - xPositionBeforeTab;

        tabOffsets.Add(new TabOffset(nextTabStop.Leader, currentXPosition - xPositionBeforeTab));
        if (currentLeaf != null)
            lastTab = currentLeaf.Current;

        return FormatResult.Continue;
    }

    private static bool IsLineBreak(DocumentObject docObj)
    {
        if (docObj is not Character character)
            return false;

        if (character.SymbolName == SymbolName.LineBreak)
            return true;
        return false;
    }

    private static bool IsBlank(DocumentObject docObj)
    {
        if (docObj is not Text text)
            return false;

        if (text.Content == " ")
            return true;
        return false;
    }

    private static bool IsTab(DocumentObject docObj)
    {
        if (docObj is not Character character)
            return false;

        if (character.SymbolName == SymbolName.Tab)
            return true;
        return false;
    }

    private static bool IsSoftHyphen(DocumentObject docObj)
    {
        if (docObj is Text text)
            return text.Content == "­";

        return false;
    }

    /// <summary>
    /// Probes the paragraph elements after a left aligned tab stop and returns the vertical text position to start at.
    /// </summary>
    /// <param name="tabStopPosition">Position of the tab to probe.</param>
    /// <param name="notFitting">Out parameter determining whether the tab causes a line break.</param>
    /// <returns>The new x-position to restart behind the tab.</returns>
    private XUnit ProbeAfterLeftAlignedTab(XUnit tabStopPosition, out bool notFitting)
    {
        //--- Save ---------------------------------
        SaveBeforeProbing(out var iter, out var blankCount, out var wordsWidth, out var xPosition, out var lineWidth, out var blankWidth);
        //------------------------------------------

        var xPositionAfterTab = xPosition;
        currentXPosition = formattingArea.X + tabStopPosition.Point;

        notFitting = ProbeAfterTab();
        if (!notFitting)
            xPositionAfterTab = formattingArea.X + tabStopPosition;

        //--- Restore ---------------------------------
        RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
        //------------------------------------------
        return xPositionAfterTab;
    }

    /// <summary>
    /// Probes the paragraph elements after a right aligned tab stop and returns the vertical text position to start at.
    /// </summary>
    /// <param name="tabStopPosition">Position of the tab to probe.</param>
    /// <param name="notFitting">Out parameter determining whether the tab causes a line break.</param>
    /// <returns>The new x-position to restart behind the tab.</returns>
    private XUnit ProbeAfterRightAlignedTab(XUnit tabStopPosition, out bool notFitting)
    {
        //--- Save ---------------------------------
        SaveBeforeProbing(out var iter, out var blankCount, out var wordsWidth, out var xPosition, out var lineWidth, out var blankWidth);
        //------------------------------------------

        var xPositionAfterTab = xPosition;

        notFitting = ProbeAfterTab();
        if (!notFitting && xPosition + currentLineWidth <= formattingArea.X + tabStopPosition)
            xPositionAfterTab = formattingArea.X + tabStopPosition - currentLineWidth;

        //--- Restore ------------------------------
        RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
        //------------------------------------------
        return xPositionAfterTab;
    }

    /// <summary>
    /// The hyperlink the leaf about to be drawn sits inside, or null when it sits in none.
    /// </summary>
    /// <remarks>
    /// One level at a time, and stopping at the top as well as at the paragraph, because a leaf is
    /// not always a word. An element holding no text - <c>AddFormattedText("")</c>, or a hyperlink
    /// with nothing in it - has nothing to descend to, so <see cref="ParagraphIterator"/> hands back
    /// its own empty collection as the leaf, one level above where a word would be. A walk taking
    /// two levels at a time from there lands on the collections rather than on the objects, never
    /// meets the paragraph, and runs off the top of the document.
    /// </remarks>
    private Hyperlink GetHyperlink()
    {
        var current = currentLeaf.Current;
        while (current != null && current is not Paragraph)
        {
            if (current is Hyperlink hyperlink)
                return hyperlink;

            current = DocumentRelations.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// Probes the paragraph elements after a right aligned tab stop and returns the vertical text position to start at.
    /// </summary>
    /// <param name="tabStopPosition">Position of the tab to probe.</param>
    /// <param name="notFitting">Out parameter determining whether the tab causes a line break.</param>
    /// <returns>The new x-position to restart behind the tab.</returns>
    private XUnit ProbeAfterCenterAlignedTab(XUnit tabStopPosition, out bool notFitting)
    {
        //--- Save ---------------------------------
        SaveBeforeProbing(out var iter, out var blankCount, out var wordsWidth, out var xPosition, out var lineWidth, out var blankWidth);
        //------------------------------------------

        var xPositionAfterTab = xPosition;
        notFitting = ProbeAfterTab();

        if (!notFitting)
        {
            if (xPosition + currentLineWidth / 2.0 <= formattingArea.X + tabStopPosition)
            {
                var rect = FittingRectOrBounds(formattingArea, currentYPosition, currentVerticalInfo.height);
                if (formattingArea.X + tabStopPosition + currentLineWidth / 2.0 > rect.X + rect.Width - RightIndent)
                {
                    //the text is too long on the right hand side of the tabstop => align to right indent.
                    xPositionAfterTab = rect.X +
                                        rect.Width -
                                        RightIndent -
                                        currentLineWidth;
                }
                else
                    xPositionAfterTab = formattingArea.X + tabStopPosition - currentLineWidth / 2;
            }
        }

        //--- Restore ------------------------------
        RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
        //------------------------------------------
        return xPositionAfterTab;
    }

    /// <summary>
    /// The characters a decimal aligned tab stop lines a number up on.
    /// </summary>
    private static readonly char[] DecimalSeparators = [',', '.'];

    /// <summary>
    /// Probes the paragraph elements after a right aligned tab stop and returns the vertical text position to start at.
    /// </summary>
    /// <param name="tabStopPosition">Position of the tab to probe.</param>
    /// <param name="notFitting">Out parameter determining whether the tab causes a line break.</param>
    /// <returns>The new x-position to restart behind the tab.</returns>
    private XUnit ProbeAfterDecimalAlignedTab(XUnit tabStopPosition, out bool notFitting)
    {
        notFitting = false;
        var savedLeaf = currentLeaf;

        //Extra for auto tab after list symbol
        if (IsTab(currentLeaf.Current))
            currentLeaf = currentLeaf.GetNextLeaf();
        if (currentLeaf == null)
        {
            currentLeaf = savedLeaf;
            return currentXPosition + tabStopPosition;
        }
        var newVerticalInfo = CalcCurrentVerticalInfo();
        var fittingRect = formattingArea.GetFittingRect(currentYPosition, newVerticalInfo.height);
        if (fittingRect == null)
        {
            notFitting = true;
            currentLeaf = savedLeaf;
            return currentXPosition;
        }

        if (IsPlainText(currentLeaf.Current))
        {
            var text = (Text)currentLeaf.Current;
            var word = text.Content;
            var lastIndex = text.Content.LastIndexOfAny(DecimalSeparators);
            if (lastIndex > 0)
                word = word[..lastIndex];

            var wordLength = MeasureString(word);
            notFitting = currentXPosition + wordLength >= formattingArea.X + formattingArea.Width + Tolerance;
            if (!notFitting)
                return formattingArea.X + tabStopPosition - wordLength;

            return currentXPosition;
        }
        currentLeaf = savedLeaf;
        return ProbeAfterRightAlignedTab(tabStopPosition, out notFitting);
    }

    private void SaveBeforeProbing(out ParagraphIterator paragraphIter, out int blankCount, out XUnit wordsWidth, out XUnit xPosition, out XUnit lineWidth, out XUnit blankWidth)
    {
        paragraphIter = currentLeaf;
        blankCount = currentBlankCount;
        xPosition = currentXPosition;
        lineWidth = currentLineWidth;
        wordsWidth = currentWordsWidth;
        blankWidth = savedBlankWidth;
    }

    private void RestoreAfterProbing(ParagraphIterator paragraphIter, int blankCount, XUnit wordsWidth, XUnit xPosition, XUnit lineWidth, XUnit blankWidth)
    {
        currentLeaf = paragraphIter;
        currentBlankCount = blankCount;
        currentXPosition = xPosition;
        currentLineWidth = lineWidth;
        currentWordsWidth = wordsWidth;
        savedBlankWidth = blankWidth;
    }

    /// <summary>
    /// Probes the paragraph after a tab.
    /// Caution: This Function resets the word count and line width before doing its work.
    /// </summary>
    /// <returns>True if the tab causes a linebreak.</returns>
    private bool ProbeAfterTab()
    {
        currentLineWidth = 0;
        currentBlankCount = 0;

        // FormatTab is reached through FormatElement with the iterator still on the tab, and the
        // loop below stops at the first tab it meets - so without this step it would measure
        // nothing, a right or center aligned stop would set the text after it from the stop
        // rather than up to it, and a left aligned one could never break the line. The one caller
        // that is not on a tab is the automatic tab after a list symbol, which starts on the
        // paragraph's first leaf and is left where it is unless that leaf is itself a tab.
        if (currentLeaf != null && IsTab(currentLeaf.Current))
            currentLeaf = currentLeaf.GetNextLeaf();

        var wordAppeared = false;
        while (currentLeaf != null && !IsLineBreak(currentLeaf.Current) && !IsTab(currentLeaf.Current))
        {
            var result = FormatElement(currentLeaf.Current);
            if (result != FormatResult.Continue)
                break;

            wordAppeared = wordAppeared || IsWordLikeElement(currentLeaf.Current);
            currentLeaf = currentLeaf.GetNextLeaf();
        }
        return currentLeaf != null && !IsLineBreak(currentLeaf.Current) &&
               !IsTab(currentLeaf.Current) && !wordAppeared;
    }

    /// <summary>
    /// Gets the next tab stop following the current x position.
    /// </summary>
    /// <returns>The searched tab stop.</returns>
    private TabStop GetNextTabStop()
    {
        var format = paragraph.Format;
        var tabStops = format.TabStops;
        XUnit lastPosition = 0;

        foreach (TabStop tabStop in tabStops)
        {
            if (tabStop.Position.Point > formattingArea.Width - RightIndent + Tolerance)
                break;

            if (tabStop.Position.Point + formattingArea.X > currentXPosition + Tolerance) // With Tolerance ...
                return tabStop;

            lastPosition = tabStop.Position.Point;
        }
        //Automatic tab stop: FirstLineIndent < 0 => automatic tab stop at LeftIndent.

        if (format.FirstLineIndent < 0 || (!format.IsNull("ListInfo") && format.ListInfo.NumberPosition < format.LeftIndent))
        {
            XUnit leftIndent = format.LeftIndent.Point;
            if (isFirstLine && currentXPosition < leftIndent + formattingArea.X)
                return new TabStop(leftIndent.Point);
        }
        XUnit defaultTabStop = "1.25cm";
        if (!paragraph.Document.IsNull("DefaultTabstop"))
            defaultTabStop = paragraph.Document.DefaultTabStop.Point;

        var currTabPos = defaultTabStop;
        while (currTabPos + formattingArea.X <= formattingArea.Width - RightIndent)
        {
            if (currTabPos > lastPosition && currTabPos + formattingArea.X > currentXPosition + Tolerance)
                return new TabStop(currTabPos.Point);

            currTabPos += defaultTabStop;
        }
        return null;
    }

    /// <summary>
    /// Gets the horizontal position to start a new line.
    /// </summary>
    /// <returns>The position to start the line.</returns>
    private XUnit StartXPosition
    {
        get
        {
            XUnit xPos = 0;

            if (phase == Phase.Formatting)
            {
                xPos = FittingRectOrBounds(formattingArea, currentYPosition, currentVerticalInfo.height).X;
                xPos += LeftIndent;
            }
            else //if (phase == Phase.Rendering)
            {
                var contentArea = renderInfo.LayoutInfo.ContentArea;
                //next lines for non fitting lines that produce an empty fitting rect:
                var rectX = contentArea.X;
                var rectWidth = contentArea.Width;

                // The measure the formatting phase broke this line to, rather than the same
                // question asked again of an area that has since forgotten the answer.
                var fittingRect = currentLineFittingRect
                                  ?? contentArea.GetFittingRect(currentYPosition, currentVerticalInfo.height);
                if (fittingRect != null)
                {
                    rectX = fittingRect.X;
                    rectWidth = fittingRect.Width;
                }
                switch (paragraph.Format.Alignment)
                {
                    case ParagraphAlignment.Left:
                    case ParagraphAlignment.Justify:
                        xPos = rectX;
                        xPos += LeftIndent;
                        break;

                    case ParagraphAlignment.Right:
                        xPos = rectX + rectWidth - RightIndent;
                        xPos -= currentLineWidth;
                        break;

                    case ParagraphAlignment.Center:
                        xPos = rectX + (rectWidth + LeftIndent - RightIndent - currentLineWidth) / 2.0;
                        break;
                }
            }
            return xPos;
        }
    }

    /// <summary>
    /// Renders a single line.
    /// </summary>
    /// <param name="lineInfo"></param>
    private void RenderLine(LineInfo lineInfo)
    {
        currentLineFittingRect = lineInfo.fittingRect;
        currentVerticalInfo = lineInfo.vertical;
        currentLeaf = lineInfo.startIter;
        startLeaf = lineInfo.startIter;
        endLeaf = lineInfo.endIter;
        currentBlankCount = lineInfo.blankCount;
        currentLineWidth = lineInfo.lineWidth;
        currentWordsWidth = lineInfo.wordsWidth;
        currentXPosition = StartXPosition;
        tabOffsets = lineInfo.tabOffsets;
        lastTabPassed = lineInfo.lastTab == null;
        lastTab = lineInfo.lastTab;

        tabIdx = 0;

        var ready = currentLeaf == null;
        if (isFirstLine)
        {
            // The bullet is the /Lbl and the text is the /LBody, and they are siblings — so the
            // label's own scope is opened here, inside the body's, and closed again before the words
            // start. A label drawn inside the body would be read as part of the sentence.
            using (Tagger.Marks(Gfx, labelElement))
                RenderListSymbol();
        }

        // Where each leaf goes, when that is not where it was written. Worked out before anything
        // is drawn, because the answer depends on how wide every part of the line is and the parts
        // are only measured by walking them.
        var placed = PlacedInVisualOrder(lineInfo);
        reordering = placed != null;

        try
        {
            var at = 0;
            while (!ready)
            {
                if (currentLeaf.Current == lineInfo.endIter.Current)
                    ready = true;

                if (currentLeaf.Current == lineInfo.lastTab)
                    lastTabPassed = true;

                // The leaves are still walked in the order they were written - only where they land
                // changes. That keeps the marked content in reading order, which is what a
                // structure tree is for, and keeps every scope nesting the way it did.
                if (placed != null)
                    currentXPosition = placed[at];

                OpenInlineScopes();
                RenderElement(currentLeaf.Current);
                currentLeaf = currentLeaf.GetNextLeaf();
                at++;
            }
        }
        finally
        {
            reordering = false;
            // Never allowed to straddle a line. The annotation is made per line anyway, and a scope
            // left open by a line that ends inside a hyperlink would swallow everything after it.
            // The broken word does straddle one, but as two runs of marks on the same element rather
            // than as one sequence — which is also what carries it over a page boundary, where one
            // sequence is not even possible.
            CloseInlineScopes();
        }

        currentYPosition += lineInfo.vertical.height;
        isFirstLine = false;
    }

    // ----------------------------------------------------------------------------------------
    // Laying a line out in the order it is read
    //
    // XGraphics.DrawString turns a right-to-left string round on its own, so every word of a
    // Hebrew or Arabic paragraph has always come out correctly. The words themselves did not: this
    // renderer draws one show-text operator per leaf and advances the pen by its width, so the
    // words stayed in the order they were written and the sentence read inside out.
    //
    // Reordering them needs every leaf's width before any of them is placed, and the only thing
    // that knows a leaf's width is the code that draws it. So the line is walked twice: once with
    // "probing" set, which advances the pen and puts nothing on the page, and then again for real
    // with each leaf placed where the first walk and the bidirectional algorithm say it belongs.
    //
    // The second walk is still in the order the leaves were written. Only the x changes. That is
    // what keeps the marked content in reading order - which is what a structure tree is for - and
    // what keeps the hyperlink and broken-word scopes nesting as they did.
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// True while the line is being walked to find out how wide its parts are. Everything that puts
    /// marks on the page is skipped; everything that moves the pen still runs.
    /// </summary>
    private bool probing;

    /// <summary>
    /// True while a line is being drawn whose leaves are not in the order they were written.
    /// </summary>
    /// <remarks>
    /// Read by the underline, strikethrough and hyperlink rules, which otherwise run from the first
    /// leaf of a stretch to the last and would draw one rule across the whole line - backwards, and
    /// over the words in between. Each leaf gets a rule of its own instead. For a stretch that is
    /// still contiguous the pieces abut and the result is the same line.
    /// </remarks>
    private bool reordering;

    /// <summary>The line's text, as the leaves contribute it during a probing walk.</summary>
    private StringBuilder probedText;

    /// <summary>
    /// Where each leaf of the line should be drawn, or null when the line reads the way it was
    /// written and nothing needs moving.
    /// </summary>
    /// <remarks>
    /// A tab is a boundary, not a character with a direction: it divides the line into segments and
    /// each segment is reordered within itself, exactly as a whole line with no tabs in it would be.
    /// The tabs themselves never move - a leaf's default position is where it was written, and only
    /// a segment that turns out to hold something right to left has that default overwritten.
    /// </remarks>
    private XUnit[] PlacedInVisualOrder(LineInfo lineInfo)
    {
        if (!MayNeedReordering(lineInfo))
            return null;

        var widths = new List<XUnit>();
        var spans = new List<(int Start, int Length)>();
        var isTab = new List<bool>();
        var text = Probe(lineInfo, widths, spans, isTab);

        var placed = new XUnit[widths.Count];
        var cursor = StartXPosition;
        for (var idx = 0; idx < widths.Count; idx++)
        {
            placed[idx] = cursor;
            cursor += widths[idx];
        }

        var anyReordered = false;

        var segmentStart = 0;
        for (var idx = 0; idx <= isTab.Count; idx++)
        {
            if (idx != isTab.Count && !isTab[idx])
                continue;

            if (idx > segmentStart)
                anyReordered |= ReorderSegment(segmentStart, idx, text, widths, spans, placed);

            segmentStart = idx + 1;
        }

        return anyReordered ? placed : null;
    }

    /// <summary>
    /// Reorders one segment of a line - the whole line, when it holds no tab - in place. Answers
    /// whether the segment held anything right to left, which is what decides whether the line is
    /// drawn from <paramref name="placed"/> at all.
    /// </summary>
    /// <remarks>
    /// <paramref name="placed"/> already holds this segment's written-order positions on entry -
    /// segments are handled left to right, and nothing at or after <paramref name="start"/> has
    /// been touched by an earlier one - so <c>placed[start]</c> doubles as that starting position
    /// with no second array to keep it in. A whole-line segment (the only kind a tab-free RTL line
    /// ever has) is passed <paramref name="text"/> and <paramref name="spans"/> as they stand,
    /// unsliced, which is what keeps the common right-to-left line - still the majority of what
    /// this reorders - to the one copy <see cref="Probe"/> already made.
    /// </remarks>
    private bool ReorderSegment(int start, int end, string text, List<XUnit> widths,
        List<(int Start, int Length)> spans, XUnit[] placed)
    {
        var wholeLine = start == 0 && end == spans.Count;

        var segmentText = text;
        IReadOnlyList<(int Start, int Length)> segmentSpans = spans;

        if (!wholeLine)
        {
            var textStart = spans[start].Start;
            var lastSpan = spans[end - 1];
            var textEnd = lastSpan.Start + lastSpan.Length;
            segmentText = text.Substring(textStart, textEnd - textStart);

            var localSpans = new List<(int Start, int Length)>(end - start);
            for (var idx = start; idx < end; idx++)
                localSpans.Add((spans[idx].Start - textStart, spans[idx].Length));
            segmentSpans = localSpans;
        }

        var bidi = BidiAlgorithm.Resolve(segmentText, ParagraphDirection);
        var anyRightToLeft = false;
        foreach (var run in bidi.Runs())
            anyRightToLeft |= run.Direction == XTextDirection.RightToLeft;

        if (!anyRightToLeft)
            return false;

        var order = VisualOrder.Of(bidi, segmentSpans);
        var x = placed[start];
        foreach (var local in order)
        {
            var leaf = start + local;
            placed[leaf] = x;
            x += widths[leaf];
        }

        return true;
    }

    /// <summary>
    /// Walks the line without drawing it, collecting what each leaf says, how wide it is, and
    /// whether it is a tab - the boundary a reordered segment must not cross.
    /// </summary>
    /// <remarks>
    /// A tab's width comes from <see cref="tabOffsets"/>, read in order and advanced by
    /// <see cref="NextTabOffset"/> as the walk passes each one. That read position is state the
    /// probing walk disturbs exactly as it disturbs <see cref="currentLeaf"/> and
    /// <see cref="currentXPosition"/>, so it is saved and restored alongside them - a walk that
    /// left it consumed would hand the real walk behind it either the wrong tab's width or none at
    /// all.
    /// </remarks>
    private string Probe(LineInfo lineInfo, List<XUnit> widths, List<(int Start, int Length)> spans, List<bool> isTab)
    {
        var savedLeaf = currentLeaf;
        var savedPosition = currentXPosition;
        var savedTabIdx = tabIdx;

        probedText = new StringBuilder();
        probing = true;
        try
        {
            var ready = currentLeaf == null;
            while (!ready)
            {
                if (currentLeaf.Current == lineInfo.endIter.Current)
                    ready = true;

                var start = probedText.Length;
                var before = currentXPosition;

                isTab.Add(IsTab(currentLeaf.Current));
                RenderElement(currentLeaf.Current);

                widths.Add(currentXPosition - before);
                spans.Add((start, probedText.Length - start));
                currentLeaf = currentLeaf.GetNextLeaf();
            }

            return probedText.ToString();
        }
        finally
        {
            probing = false;
            probedText = null;
            currentLeaf = savedLeaf;
            currentXPosition = savedPosition;
            tabIdx = savedTabIdx;
        }
    }

    /// <summary>
    /// Whether the line could possibly want reordering, asked before anything is measured.
    /// </summary>
    /// <remarks>
    /// A line with nothing right to left in it and no direction declared cannot need moving, and
    /// this is what keeps every left-to-right document paying one cheap scan rather than an extra
    /// walk of every line - tab or no tab.
    /// </remarks>
    private bool MayNeedReordering(LineInfo lineInfo)
    {
        var declared = ParagraphDirection == BidiParagraphDirection.RightToLeft;
        var found = declared;

        var leaf = lineInfo.startIter;
        while (leaf != null)
        {
            if (!found && leaf.Current is Text { Content: not null } text)
            {
                foreach (var ch in text.Content)
                {
                    // Nothing below the Hebrew block is written right to left, so a string made
                    // only of characters below it can only be read the way it was written.
                    if (ch >= '\u0590')
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (leaf.Current == lineInfo.endIter.Current)
                break;

            leaf = leaf.GetNextLeaf();
        }

        return found;
    }

    /// <summary>
    /// Opens and closes the scopes that live inside a line — a <c>/Link</c> around the text of a
    /// hyperlink, a <c>/Span</c> around a word broken at a hyphen — as the leaf about to be drawn
    /// moves into and out of them.
    /// </summary>
    /// <remarks>
    /// Before the element is drawn rather than after it, which is what stops this being a line in
    /// <see cref="RealizeHyperlink"/>. That runs once the word is already on the page — it measures
    /// what was drawn in order to grow the annotation's rectangle — so a scope opened there would
    /// leave the first word of every link outside the link.
    /// </remarks>
    private void OpenInlineScopes()
    {
        var hyperlink = GetHyperlink();
        if (!ReferenceEquals(hyperlink, scopedHyperlink))
        {
            // The span first: it is the inner scope, and closing scopes out of order would cross a
            // pair of BDC/EMC rather than nest them.
            CloseSpanScope();
            CloseLinkScope();

            if (hyperlink != null)
            {
                scopedHyperlink = hyperlink;
                linkScope = Tagger.Marks(Gfx, LinkElementOf(hyperlink));
            }
        }

        var word = BrokenWordOf(currentLeaf.Current);
        if (ReferenceEquals(word, scopedWord))
            return;

        CloseSpanScope();

        if (word == null)
            return;

        scopedWord = word;
        spanScope = Tagger.Marks(Gfx, SpanElementOf(word));
    }

    private void CloseInlineScopes()
    {
        CloseSpanScope();
        CloseLinkScope();
    }

    private void CloseLinkScope()
    {
        linkScope?.Dispose();
        linkScope = null;
        scopedHyperlink = null;
    }

    private void CloseSpanScope()
    {
        spanScope?.Dispose();
        spanScope = null;
        scopedWord = null;
    }

    /// <summary>
    /// The element standing for a hyperlink, one per hyperlink however many lines and pages its text
    /// runs over, and however many annotations that costs.
    /// </summary>
    private PdfStructureElement LinkElementOf(Hyperlink hyperlink) =>
        Tagger.Element(hyperlink, PdfTag.Link, Tagger.Parent);

    private IDisposable linkScope;
    private Hyperlink scopedHyperlink;

    // ----------------------------------------------------------------------------------------
    // Words broken at a soft hyphen
    //
    // A word broken across a line is on the page as "some-" and "thing" and is neither of those.
    // Anything reading the marks gets the hyphen the typesetter added and the break the line
    // introduced, and has no way to know that the word was "something" — so a screen reader says
    // "some" and "thing", a search for the word fails, and copying the paragraph out pastes the
    // hyphen. /ActualText is what says otherwise: an exact replacement for an element and its
    // children, which the two fragments and the hyphen between them are.
    //
    // The replacement goes on a /Span element covering both fragments rather than on either of the
    // two marked-content sequences that draw them. It has to: the fragments are separated by a line
    // break and sometimes by a page break, so there is no one sequence to put it on, and putting the
    // word on each of two sequences would say it twice.
    // ----------------------------------------------------------------------------------------

    /// <summary>
    /// A word this paragraph breaks at a soft hyphen: the leaves it is drawn from, and what it says.
    /// </summary>
    private sealed class BrokenWord
    {
        internal BrokenWord(DocumentObject hyphen, string text)
        {
            Hyphen = hyphen;
            Text = text;
        }

        /// <summary>
        /// The soft hyphen the break happens at, which is also the key the element is filed under —
        /// stable across the two renderers that draw a paragraph split over a page boundary, where
        /// nothing belonging to a renderer would be.
        /// </summary>
        internal DocumentObject Hyphen { get; }

        /// <summary>The whole word, without the hyphen that was never part of it.</summary>
        internal string Text { get; }
    }

    /// <summary>
    /// Finds the words this part of the paragraph breaks, so that the lines below can wrap each one
    /// in a scope as they come to it.
    /// </summary>
    /// <remarks>
    /// Two places a break can be. A soft hyphen that ends a line is one — that is the test
    /// <see cref="RenderSoftHyphen"/> itself uses to decide whether to draw a hyphen at all, so the
    /// two cannot disagree about which hyphens are real. The other is a break inherited from the
    /// previous part: a paragraph split over a page boundary at a hyphen has the hyphen on one page
    /// and the rest of the word on the next, drawn by a different renderer that would otherwise
    /// never learn that its first leaves finish a word begun elsewhere.
    /// </remarks>
    private void FindBrokenWords(ParagraphFormatInfo formatInfo)
    {
        brokenWords = null;

        if (!Tagger.Enabled || Gfx.PdfPage == null)
            return;

        for (var idx = 0; idx < formatInfo.LineCount; ++idx)
            RecordBrokenWord(formatInfo.GetLineInfo(idx).endIter);

        if (formatInfo.LineCount > 0)
            RecordBrokenWord(formatInfo.GetLineInfo(0).startIter?.GetPreviousLeaf());
    }

    /// <summary>
    /// Records the word broken at the given leaf, if that leaf is a soft hyphen with a word on each
    /// side of it.
    /// </summary>
    private void RecordBrokenWord(ParagraphIterator hyphen)
    {
        if (hyphen == null || !IsSoftHyphen(hyphen.Current))
            return;

        var alreadyRecorded = brokenWords != null && brokenWords.ContainsKey(hyphen.Current);
        if (alreadyRecorded)
            return;

        // The same guard FormatSoftHyphen uses. A hyphen with nothing on one side of it did not
        // break a word, so there is no word to put back together.
        var previous = hyphen.GetPreviousLeaf();
        var next = hyphen.GetNextLeaf();
        if (!IsTextOnBothSides(previous, next))
            return;

        var leaves = WordLeaves(previous, hyphen.Current, next);
        var word = new BrokenWord(hyphen.Current, Spell(leaves));
        brokenWords ??= new Dictionary<DocumentObject, BrokenWord>(ReferenceComparer.Instance);
        foreach (var leaf in leaves)
            brokenWords[leaf] = word;
    }

    /// <summary>
    /// Whether there is plain text immediately before a soft hyphen and immediately after it.
    /// </summary>
    private static bool IsTextOnBothSides(ParagraphIterator previous, ParagraphIterator next) =>
        previous != null && next != null && IsPlainText(previous.Current) && IsPlainText(next.Current);

    /// <summary>
    /// The leaves of the word broken at a soft hyphen, in reading order, the hyphen among them.
    /// </summary>
    /// <remarks>
    /// Backwards to the front of the word, then forwards to the end of it. The walk stops at
    /// anything that is not plain text or another soft hyphen — a blank, a tab, a field, a symbol
    /// — which is what makes the run a word. It also stops the replacement from claiming more
    /// than the scope covers: whatever the walk collects is exactly what the scope will wrap and
    /// exactly what the replacement will spell.
    /// </remarks>
    private static List<DocumentObject> WordLeaves(ParagraphIterator previous, DocumentObject hyphen, ParagraphIterator next)
    {
        var leaves = new List<DocumentObject>();
        for (var iter = previous; iter != null && IsWordFragment(iter.Current); iter = iter.GetPreviousLeaf())
            leaves.Add(iter.Current);
        leaves.Reverse();

        leaves.Add(hyphen);
        for (var iter = next; iter != null && IsWordFragment(iter.Current); iter = iter.GetNextLeaf())
            leaves.Add(iter.Current);

        return leaves;
    }

    /// <summary>
    /// The word a run of leaves spells, without its soft hyphens.
    /// </summary>
    /// <remarks>
    /// The hyphens are what is being taken out. A word may carry several and break at one of
    /// them; the others draw nothing and must not spell anything either.
    /// </remarks>
    private static string Spell(List<DocumentObject> leaves)
    {
        var text = new StringBuilder();
        foreach (var leaf in leaves)
        {
            if (!IsSoftHyphen(leaf))
                text.Append(((Text)leaf).Content);
        }
        return text.ToString();
    }

    /// <summary>
    /// Whether a leaf is part of a word rather than something between words.
    /// </summary>
    private static bool IsWordFragment(DocumentObject docObj) => IsPlainText(docObj) || IsSoftHyphen(docObj);

    private BrokenWord BrokenWordOf(DocumentObject leaf) =>
        brokenWords != null && leaf != null && brokenWords.TryGetValue(leaf, out var word)
            ? word
            : null;

    /// <summary>
    /// The element standing for a broken word, one however many lines and pages its fragments are
    /// spread over, carrying the word it really spells.
    /// </summary>
    private PdfStructureElement SpanElementOf(BrokenWord word)
    {
        var element = Tagger.Element(word.Hyphen, PdfTag.Span, Tagger.Parent);
        element?.ActualText = word.Text;

        return element;
    }

    private Dictionary<DocumentObject, BrokenWord> brokenWords;
    private IDisposable spanScope;
    private BrokenWord scopedWord;

    /// <summary>
    /// Keys leaves by identity. A <c>Text</c> compares by value, and the two halves of "in-ter-in"
    /// are equal without being the same leaf.
    /// </summary>
    private sealed class ReferenceComparer : IEqualityComparer<DocumentObject>
    {
        internal static readonly ReferenceComparer Instance = new();

        public bool Equals(DocumentObject x, DocumentObject y) => ReferenceEquals(x, y);

        public int GetHashCode(DocumentObject obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private void ReMeasureLine(ref LineInfo lineInfo)
    {
        //--- Save ---------------------------------
        SaveBeforeProbing(out var iter, out var blankCount, out var wordsWidth, out var xPosition, out var lineWidth, out var blankWidth);
        var origLastTabPassed = lastTabPassed;
        //------------------------------------------
        currentLeaf = lineInfo.startIter;
        // The line being measured has to say where it starts as well as where it ends. Formatting
        // a soft hyphen asks whether it is the first thing on the line, and whether the word
        // before it is, and answers both by comparing against startLeaf. This runs on a renderer
        // that has only ever rendered, so startLeaf is null until the first line is drawn and
        // belongs to the line before this one after that: the first question threw and the rest
        // were answered about the wrong line.
        startLeaf = lineInfo.startIter;
        endLeaf = lineInfo.endIter;
        formattingArea = renderInfo.LayoutInfo.ContentArea;
        tabOffsets = new ArrayList();
        currentLineWidth = 0;
        currentWordsWidth = 0;

        // No room for this line here - a band off the bottom of the area, or one with something
        // standing across the whole of it. Either way there is nothing to lay out against, so the
        // line is left alone and the caller moves on to a new area.
        //
        // This used to read "if (fittingRect == null) GetType();", which is a breakpoint someone
        // left behind rather than a decision. The decision was always the one below: skip.
        var fittingRect = formattingArea.GetFittingRect(currentYPosition, currentVerticalInfo.height);
        if (fittingRect != null)
        {
            currentXPosition = fittingRect.X + LeftIndent;
            FormatListSymbol();
            var goOn = true;
            while (goOn && currentLeaf != null)
            {
                if (currentLeaf.Current == lineInfo.lastTab)
                    lastTabPassed = true;

                var result = FormatElement(currentLeaf.Current);

                // Where this line breaks was settled while the paragraph was formatted; this pass
                // only measures it again, and must not break it a second time. An element that
                // says it no longer fits moves currentLeaf back to the break it wants, and the
                // step forward below moved it straight back to where it had just been, so a line
                // ending in a soft hyphen was measured for ever and no document ever came out.
                if (result != FormatResult.Continue && result != FormatResult.Ignore)
                    break;

                goOn = currentLeaf != null && currentLeaf.Current != endLeaf.Current;
                if (goOn)
                    currentLeaf = currentLeaf.GetNextLeaf();
            }
            lineInfo.lineWidth = currentLineWidth;
            lineInfo.wordsWidth = currentWordsWidth;
            lineInfo.blankCount = currentBlankCount;
            lineInfo.tabOffsets = tabOffsets;
            lineInfo.reMeasureLine = false;
            lastTabPassed = origLastTabPassed;
        }
        RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
    }

    private XUnit CurrentWordDistance
    {
        get
        {
            if (phase != Phase.Rendering ||
                paragraph.Format.Alignment != ParagraphAlignment.Justify || !lastTabPassed)
                return MeasureString(" ");

            if (currentBlankCount < 1 || (isLastLine && renderInfo.FormatInfo.IsEnding))
                return MeasureString(" ");

            var contentArea = renderInfo.LayoutInfo.ContentArea;
            // Justification stretches blanks to fill the line's own measure. Reading the
            // content area's width here would stretch a line beside a shape to the full
            // measure, which is ragged rather than obviously broken.
            var width = (currentLineFittingRect
                         ?? FittingRectOrBounds(contentArea, currentYPosition, currentVerticalInfo.height)).Width;
            if (lastTabPosition > 0)
            {
                width -= lastTabPosition -
                          contentArea.X;
            }
            else
                width -= LeftIndent;

            width -= RightIndent;
            return (width - currentWordsWidth) / currentBlankCount;
        }
    }

    private void RenderElement(DocumentObject docObj)
    {
        var typeName = docObj.GetType().Name;
        switch (typeName)
        {
            case "Text":
                if (IsBlank(docObj))
                    RenderBlank();
                else if (IsSoftHyphen(docObj))
                    RenderSoftHyphen();
                else
                    RenderText((Text)docObj);
                break;

            case "Character":
                RenderCharacter((Character)docObj);
                break;

            case "DateField":
                RenderDateField((DateField)docObj);
                break;

            case "InfoField":
                RenderInfoField((InfoField)docObj);
                break;

            case "NumPagesField":
                RenderNumPagesField((NumPagesField)docObj);
                break;

            case "PageField":
                RenderPageField((PageField)docObj);
                break;

            case "SectionField":
                RenderSectionField((SectionField)docObj);
                break;

            case "SectionPagesField":
                RenderSectionPagesField((SectionPagesField)docObj);
                break;

            case "BookmarkField":
                RenderBookmarkField();
                break;

            case "PageRefField":
                RenderPageRefField((PageRefField)docObj);
                break;

            case "Image":
                RenderImage();
                break;

            case "Footnote":
                RenderFootnote((Footnote)docObj);
                break;
        }
    }

    private void RenderImage()
    {
        var imageRenderInfo = CurrentImageRenderInfo;
        var top = CurrentBaselinePosition;
        var contentArea = imageRenderInfo.LayoutInfo.ContentArea;
        top -= contentArea.Height;

        if (probing)
        {
            // An object replacement character: neutral, so it takes the direction of whatever
            // it sits between, which is the right answer for a picture in a line of text.
            probedText.Append('￼');
            currentXPosition += contentArea.Width;
            return;
        }

        RenderByInfos(currentXPosition, top, [imageRenderInfo]);

        RenderUnderline(contentArea.Width, true);
        RenderStrikethrough(contentArea.Width, true);
        RealizeHyperlink(contentArea.Width);

        currentXPosition += contentArea.Width;
    }

    private void RenderDateField(DateField dateField)
    {
        RenderWord(fieldInfos.date.ToString(dateField.Format));
    }

    private void RenderInfoField(InfoField infoField)
    {
        RenderWord(GetFieldValue(infoField));
    }

    private void RenderNumPagesField(NumPagesField numPagesField)
    {
        RenderWord(GetFieldValue(numPagesField));
    }

    private void RenderPageField(PageField pageField)
    {
        RenderWord(GetFieldValue(pageField));
    }

    private void RenderSectionField(SectionField sectionField)
    {
        RenderWord(GetFieldValue(sectionField));
    }

    private void RenderSectionPagesField(SectionPagesField sectionPagesField)
    {
        RenderWord(GetFieldValue(sectionPagesField));
    }

    private void RenderBookmarkField()
    {
        if (probing)
            return;

        RenderUnderline(0, false);
        RenderStrikethrough(0, false);
    }

    private void RenderPageRefField(PageRefField pageRefField)
    {
        RenderWord(GetFieldValue(pageRefField));
    }

    private void RenderCharacter(Character character)
    {
        switch (character.SymbolName)
        {
            case SymbolName.Blank:
            case SymbolName.Em:
            case SymbolName.Em4:
            case SymbolName.En:
                RenderSpace(character);
                break;
            case SymbolName.LineBreak:
                RenderLinebreak();
                break;

            case SymbolName.Tab:
                RenderTab();
                break;

            default:
                RenderSymbol(character);
                break;
        }
    }

    private void RenderSpace(Character character)
    {
        if (probing)
            probedText.Append(' ', character.Count);

        currentXPosition += GetSpaceWidth(character);
    }

    private void RenderLinebreak()
    {
        if (probing)
            return;

        RenderUnderline(0, false);
        RenderStrikethrough(0, false);
        RealizeHyperlink(0);
    }

    private void RenderSymbol(Character character)
    {
        // GetSymbol already answers the character as many times as it repeats, and that is what
        // FormatSymbol measures. Repeating it a second time here drew Count squared of them -
        // four for a count of two, nine for three - into the width reserved for Count.
        RenderWord(GetSymbol(character));
    }

    private void RenderTab()
    {
        var tabOffset = NextTabOffset();

        // Every other leaf checks this and returns before touching the page - a tab's own segment
        // could never need reordering until now, so this was never called during a probing walk.
        // A leader, an underline or strikethrough under the tab, or a hyperlink around it would
        // otherwise be drawn once here and again for real.
        if (probing)
        {
            currentXPosition += tabOffset.offset;
            return;
        }

        RenderUnderline(tabOffset.offset, false);
        RenderStrikethrough(tabOffset.offset, false);
        RenderTabLeader(tabOffset);
        RealizeHyperlink(tabOffset.offset);
        currentXPosition += tabOffset.offset;
        if (currentLeaf.Current == lastTab)
            lastTabPosition = currentXPosition;
    }

    private void RenderTabLeader(TabOffset tabOffset)
    {
        string leaderString;
        switch (tabOffset.leader)
        {
            case TabLeader.Dashes:
                leaderString = "-";
                break;

            case TabLeader.Dots:
                leaderString = ".";
                break;

            case TabLeader.Heavy:
            case TabLeader.Lines:
                leaderString = "_";
                break;

            case TabLeader.MiddleDot:
                leaderString = "·";
                break;

            default:
                return;
        }
        var leaderWidth = MeasureString(leaderString);
        var xPosition = currentXPosition;
        var drawString = "";

        while (xPosition + leaderWidth <= currentXPosition + tabOffset.offset)
        {
            drawString += leaderString;
            xPosition += leaderWidth;
        }
        var font = CurrentDomFont;
        var xFont = CurrentFont;
        if (font.Subscript || font.Superscript)
            xFont = FontHandler.ToSubSuperFont(xFont);

        Gfx.DrawString(drawString, xFont, CurrentBrush, currentXPosition, CurrentBaselinePosition);
    }

    private TabOffset NextTabOffset()
    {

        var offset = tabOffsets.Count > tabIdx ?
            // ReSharper disable once PossibleNullReferenceException
            (TabOffset)tabOffsets[tabIdx] :
            new TabOffset(0, 0);
        ++tabIdx;
        return offset;
    }
    private int tabIdx;

    private bool IgnoreBlank()
    {
        if (currentLeaf == startLeaf)
            return true;

        if (endLeaf != null && currentLeaf.Current == endLeaf.Current)
            return true;

        var nextIter = currentLeaf.GetNextLeaf();
        while (nextIter != null && (IsBlank(nextIter.Current) || nextIter.Current is BookmarkField))
        {
            nextIter = nextIter.GetNextLeaf();
        }
        if (nextIter == null)
            return true;

        if (IsTab(nextIter.Current))
            return true;

        var prevIter = currentLeaf.GetPreviousLeaf();
        // Can be null if currentLeaf is the first leaf
        var obj = prevIter != null ? prevIter.Current : null;
        while (obj is BookmarkField)
        {
            prevIter = prevIter.GetPreviousLeaf();
            if (prevIter != null)
                obj = prevIter.Current;
            else
                obj = null;
        }
        if (obj == null)
            return true;

        return IsBlank(obj) || IsTab(obj);
    }

    private void RenderBlank()
    {
        if (probing)
        {
            if (IgnoreBlank())
                return;

            probedText.Append(' ');
            currentXPosition += CurrentWordDistance;
            return;
        }

        if (!IgnoreBlank())
        {
            var wordDistance = CurrentWordDistance;
            RenderUnderline(wordDistance, false);
            RenderStrikethrough(wordDistance, false);
            RealizeHyperlink(wordDistance);
            currentXPosition += wordDistance;
        }
        else
        {
            RenderUnderline(0, false);
            RenderStrikethrough(0, false);
            RealizeHyperlink(0);
        }
    }

    private void RenderSoftHyphen()
    {
        if (currentLeaf.Current == endLeaf.Current)
            RenderWord("-");
    }

    private void RenderText(Text text)
    {
        RenderWord(text.Content);
    }

    private void RenderWord(string word)
    {
        var wordWidth = MeasureString(word);

        if (probing)
        {
            probedText.Append(word);
            currentXPosition += wordWidth;
            return;
        }

        var font = CurrentDomFont;
        var xFont = CurrentFont;
        if (font.Subscript || font.Superscript)
            xFont = FontHandler.ToSubSuperFont(xFont);

        Gfx.DrawString(word, xFont, CurrentBrush, currentXPosition, CurrentBaselinePosition);
        RenderUnderline(wordWidth, true);
        RenderStrikethrough(wordWidth, true);
        RealizeHyperlink(wordWidth);
        currentXPosition += wordWidth;
    }

    private void StartHyperlink(XUnit left, XUnit top)
    {
        hyperlinkRect = new XRect(left, top, 0, 0);
    }

    private void EndHyperlink(Hyperlink hyperlink, XUnit right, XUnit bottom)
    {
        hyperlinkRect.Width = right - hyperlinkRect.X;
        hyperlinkRect.Height = bottom - hyperlinkRect.Y;
        var page = Gfx.PdfPage;
        if (page == null)
            return;

        var rect = Gfx.Transformer.WorldToDefaultPage(hyperlinkRect);
        PdfPinata.Pdf.Annotations.PdfLinkAnnotation annotation = null;

        switch (hyperlink.Type)
        {
            case HyperlinkType.Local:
                var pageRef = fieldInfos.GetPhysicalPageNumber(hyperlink.Name);
                if (pageRef > 0)
                    annotation = page.AddDocumentLink(new PdfRectangle(rect), pageRef,
                        fieldInfos.GetBookmarkTop(hyperlink.Name));
                break;

            case HyperlinkType.Web:
                annotation = page.AddWebLink(new PdfRectangle(rect), hyperlink.Name);
                break;

            case HyperlinkType.File:
                annotation = page.AddFileLink(new PdfRectangle(rect), hyperlink.Name);
                break;
        }

        TagLink(hyperlink, annotation);
        hyperlinkRect = new XRect();
    }

    /// <summary>
    /// Joins a link annotation to the structure and gives it something to be announced as.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A link that exists only as a rectangle is found by a reader hit-testing the page and by
    /// nothing else, so the annotation goes into the tree beside the text it covers. One hyperlink
    /// makes as many annotations as it has lines and pages, and all of them join the same element —
    /// which is what makes a link broken over a line break one link.
    /// </para>
    /// <para>
    /// The description is the destination, because it is the only thing here that is certainly true.
    /// The link's own text is what a reader would rather hear, and by the time the annotation is
    /// made the text has been drawn and not kept.
    /// </para>
    /// </remarks>
    private void TagLink(Hyperlink hyperlink, PdfPinata.Pdf.Annotations.PdfLinkAnnotation annotation)
    {
        if (annotation == null)
            return;

        if (string.IsNullOrEmpty(annotation.Elements.GetString("/Contents")))
            annotation.Elements.SetString("/Contents", hyperlink.Name ?? "");

        Tagger.AddAnnotation(Gfx, LinkElementOf(hyperlink), annotation);
    }

    private void RealizeHyperlink(XUnit width)
    {
        var top = currentYPosition;
        var left = currentXPosition;
        XUnit bottom = top + currentVerticalInfo.height;
        XUnit right = left + width;
        var hyperlink = GetHyperlink();

        var hyperlinkChanged = currentHyperlink != hyperlink;

        if (hyperlinkChanged)
        {
            if (currentHyperlink != null)
                EndHyperlink(currentHyperlink, left, bottom);

            if (hyperlink != null)
                StartHyperlink(left, top);

            currentHyperlink = hyperlink;
        }

        if (!reordering && currentLeaf.Current != endLeaf.Current)
            return;

        if (currentHyperlink != null)
            EndHyperlink(currentHyperlink, right, bottom);

        currentHyperlink = null;
    }
    private Hyperlink currentHyperlink;
    private XRect hyperlinkRect;

    private XUnit CurrentBaselinePosition
    {
        get
        {
            var verticalInfo = currentVerticalInfo;
            var position = currentYPosition;

            var font = CurrentDomFont;
            var xFont = CurrentFont;
            if (font.Subscript)
            {
                position += verticalInfo.inherentlineSpace;
                position -= FontHandler.GetSubSuperScaling(CurrentFont) * FontHandler.GetDescent(xFont);
            }
            else if (font.Superscript)
            {
                position += FontHandler.GetSubSuperScaling(CurrentFont) * (xFont.GetHeight() - FontHandler.GetDescent(xFont));
            }
            else
            {
                position += verticalInfo.inherentlineSpace - verticalInfo.descent;
            }

            return position;
        }
    }

    private XBrush CurrentBrush
    {
        get
        {
            return currentLeaf != null ? FontHandler.FontColorToXBrush(CurrentDomFont) : null;
        }
    }

    /// <summary>
    /// Where on the page an outline entry for this paragraph should land, in the coordinates a
    /// PDF page is measured in.
    /// </summary>
    /// <remarks>
    /// Without this the entry points at the page and says nothing about where on it, and a
    /// reader following it is left wherever the page happens to be scrolled to rather than at
    /// the heading. The paragraph is being rendered onto the very page the entry points at, so
    /// its own transformer is the one that turns the distance down the page into a distance up it.
    /// </remarks>
    private double OutlineDestinationTop()
    {
        var contentArea = renderInfo.LayoutInfo.ContentArea;
        if (contentArea == null)
            return double.NaN;

        var onPage = Gfx.Transformer.WorldToDefaultPage(
            new XRect(contentArea.X, contentArea.Y, 0, 0));
        return onPage.Y;
    }

    private void InitRendering()
    {
        phase = Phase.Rendering;

        var parFormatInfo = (ParagraphFormatInfo)renderInfo.FormatInfo;
        if (parFormatInfo.LineCount == 0)
            return;
        isFirstLine = parFormatInfo.IsStarting;

        var lineInfo = parFormatInfo.GetFirstLineInfo();
        var contentArea = renderInfo.LayoutInfo.ContentArea;
        currentYPosition = contentArea.Y + TopBorderOffset;
        // StL: GetFittingRect liefert manchmal null
        var rect = lineInfo.fittingRect
                   ?? contentArea.GetFittingRect(currentYPosition, lineInfo.vertical.height);
        if (rect != null)
            currentXPosition = rect.X;
        currentLineWidth = 0;
    }

    /// <summary>
    /// Initializes this instance for formatting.
    /// </summary>
    /// <param name="area">The area for formatting</param>
    /// <param name="previousFormatInfo">A previous format info.</param>
    /// <returns>False, if nothing of the paragraph will fit the area any more.</returns>
    private bool InitFormat(Area area, FormatInfo previousFormatInfo)
    {
        phase = Phase.Formatting;

        tabOffsets = new ArrayList();

        var prevParaFormatInfo = (ParagraphFormatInfo)previousFormatInfo;
        if (previousFormatInfo == null || prevParaFormatInfo.LineCount == 0)
        {
            ((ParagraphFormatInfo)renderInfo.FormatInfo).isStarting = true;
            var parIt = new ParagraphIterator(paragraph.Elements);
            currentLeaf = parIt.GetFirstLeaf();
            isFirstLine = true;
        }
        else
        {
            currentLeaf = prevParaFormatInfo.GetLastLineInfo().endIter.GetNextLeaf();
            isFirstLine = false;
            ((ParagraphFormatInfo)renderInfo.FormatInfo).isStarting = false;
        }

        startLeaf = currentLeaf;
        currentVerticalInfo = CalcCurrentVerticalInfo();
        currentYPosition = area.Y + TopBorderOffset;
        formattingArea = area;
        var rect = formattingArea.GetFittingRect(currentYPosition, currentVerticalInfo.height);
        if (rect == null)
            return false;

        currentXPosition = rect.X + LeftIndent;
        if (isFirstLine)
            FormatListSymbol();

        return true;
    }

    /// <summary>
    /// Gets information necessary to render or measure the list symbol.
    /// </summary>
    /// <param name="symbol">The text to list symbol to render or measure</param>
    /// <param name="font">The font to use for rendering or measuring.</param>
    /// <returns>True, if a symbol needs to be rendered.</returns>
    private bool GetListSymbol(out string symbol, out XFont font)
    {
        font = null;
        symbol = null;
        var formatInfo = (ParagraphFormatInfo)renderInfo.FormatInfo;
        if (phase == Phase.Formatting)
        {
            var format = paragraph.Format;
            if (format.IsNull("ListInfo"))
                return false;

            var listInfo = format.ListInfo;
            double size = format.Font.Size;
            var style = FontHandler.GetXStyle(format.Font);

            switch (listInfo.ListType)
            {
                case ListType.BulletList1:
                    symbol = "·";
                    font = new XFont(GlobalFontSettings.FontResolver.DefaultFontName, size, style);
                    break;

                case ListType.BulletList2:
                    symbol = "o";
                    font = new XFont(GlobalFontSettings.FontResolver.DefaultFontName, size, style);
                    break;

                case ListType.BulletList3:
                    symbol = "§";
                    font = new XFont(GlobalFontSettings.FontResolver.DefaultFontName, size, style);
                    break;

                case ListType.NumberList1:
                    symbol = DocumentRenderer.NextListNumber(listInfo) + ".";
                    font = FontHandler.FontToXFont(format.Font, DocumentRenderer.PrivateFonts, Gfx.MUH);
                    break;

                case ListType.NumberList2:
                    symbol = DocumentRenderer.NextListNumber(listInfo) + ")";
                    font = FontHandler.FontToXFont(format.Font, DocumentRenderer.PrivateFonts, Gfx.MUH);
                    break;

                case ListType.NumberList3:
                    symbol = NumberFormatter.Format(DocumentRenderer.NextListNumber(listInfo), "alphabetic") + ")";
                    font = FontHandler.FontToXFont(format.Font, DocumentRenderer.PrivateFonts, Gfx.MUH);
                    break;
            }
            formatInfo.listFont = font;
            formatInfo.listSymbol = symbol;
            return true;
        }
        else
        {
            if (formatInfo.listFont == null || formatInfo.listSymbol == null)
                return false;

            font = formatInfo.listFont;
            symbol = formatInfo.listSymbol;
            return true;
        }
    }

    private XUnit LeftIndent
    {
        get
        {
            var format = paragraph.Format;
            XUnit leftIndent = format.LeftIndent.Point;
            if (isFirstLine)
            {
                if (!format.IsNull("ListInfo"))
                {
                    if (!format.ListInfo.IsNull("NumberPosition"))
                        return format.ListInfo.NumberPosition.Point;
                    if (format.IsNull("FirstLineIndent"))
                        return 0;
                }
                return leftIndent + paragraph.Format.FirstLineIndent.Point;
            }
            return leftIndent;
        }
    }

    private XUnit RightIndent => paragraph.Format.RightIndent.Point;

    /// <summary>
    /// Formats the paragraph by performing line breaks etc.
    /// </summary>
    /// <param name="area">The area in which to render.</param>
    /// <param name="previousFormatInfo">The format info that was obtained on formatting the same paragraph on a previous area.</param>
    internal override void Format(Area area, FormatInfo previousFormatInfo)
    {
        var formatInfo = (ParagraphFormatInfo)renderInfo.FormatInfo;
        if (!InitFormat(area, previousFormatInfo))
        {
            formatInfo.isStarting = false;
            return;
        }
        formatInfo.isEnding = true;

        var lastResult = FormatResult.Continue;
        while (currentLeaf != null)
        {
            var result = FormatElement(currentLeaf.Current);
            switch (result)
            {
                case FormatResult.Ignore:
                    currentLeaf = currentLeaf.GetNextLeaf();
                    break;

                case FormatResult.Continue:
                    lastResult = result;
                    currentLeaf = currentLeaf.GetNextLeaf();
                    break;

                case FormatResult.NewLine:
                    lastResult = result;
                    StoreLineInformation();
                    if (!StartNewLine())
                    {
                        result = FormatResult.NewArea;
                        formatInfo.isEnding = false;
                    }
                    break;
            }
            if (result == FormatResult.NewArea)
            {
                lastResult = result;
                formatInfo.isEnding = false;
                break;
            }
        }
        if (formatInfo.IsEnding && lastResult != FormatResult.NewLine)
            StoreLineInformation();

        formatInfo.imageRenderInfos = imageRenderInfos;
        FinishLayoutInfo();
    }

    /// <summary>
    /// Finishes the layout info by calculating starting and trailing heights.
    /// </summary>
    private void FinishLayoutInfo()
    {
        var layoutInfo = renderInfo.LayoutInfo;
        var format = paragraph.Format;
        var parInfo = (ParagraphFormatInfo)renderInfo.FormatInfo;
        layoutInfo.MinWidth = minWidth;
        layoutInfo.KeepTogether = format.KeepTogether;

        if (parInfo.IsComplete)
        {
            var limitOfLines = 1;
            if (parInfo.widowControl)
                limitOfLines = 3;

            if (parInfo.LineCount <= limitOfLines)
                layoutInfo.KeepTogether = true;
        }
        if (parInfo.IsStarting)
        {
            layoutInfo.MarginTop = format.SpaceBefore.Point;
            layoutInfo.PageBreakBefore = format.PageBreakBefore;
        }
        else
        {
            layoutInfo.MarginTop = 0;
            layoutInfo.PageBreakBefore = false;
        }

        if (parInfo.IsEnding)
        {
            layoutInfo.MarginBottom = paragraph.Format.SpaceAfter.Point;
            layoutInfo.KeepWithNext = paragraph.Format.KeepWithNext;
        }
        else
        {
            layoutInfo.MarginBottom = 0;
            layoutInfo.KeepWithNext = false;
        }
        if (parInfo.LineCount <= 0)
            return;

        var startingHeight = parInfo.GetFirstLineInfo().vertical.height;
        if (parInfo.isStarting && paragraph.Format.WidowControl && parInfo.LineCount >= 2)
            startingHeight += parInfo.GetLineInfo(1).vertical.height;

        layoutInfo.StartingHeight = startingHeight;

        var trailingHeight = parInfo.GetLastLineInfo().vertical.height;

        if (parInfo.IsEnding && paragraph.Format.WidowControl && parInfo.LineCount >= 2)
            trailingHeight += parInfo.GetLineInfo(parInfo.LineCount - 2).vertical.height;

        layoutInfo.TrailingHeight = trailingHeight;
    }


    private XUnit PopSavedBlankWidth()
    {
        var width = savedBlankWidth;
        savedBlankWidth = 0;
        return width;
    }

    private void SaveBlankWidth(XUnit blankWidth)
    {
        savedBlankWidth = blankWidth;
    }
    private XUnit savedBlankWidth = 0;

    /// <summary>
    /// Processes the elements when formatting.
    /// </summary>
    /// <param name="docObj"></param>
    /// <returns></returns>
    private FormatResult FormatElement(DocumentObject docObj)
    {
        return JoinedRunBreaksBeforeCurrentLeaf() ? FormatResult.NewLine : FormatLeaf(docObj);
    }

    private static bool IsNonBreakableBlank(DocumentObject docObj) =>
        docObj is Character { SymbolName: SymbolName.NonBreakableBlank };

    /// <summary>
    /// Whether no line may be broken between two adjacent leaves: one of them is a non-breakable
    /// blank and the other is something a word is made of.
    /// </summary>
    private static bool IsJoinedTo(DocumentObject left, DocumentObject right) =>
        (IsNonBreakableBlank(left) || IsNonBreakableBlank(right))
        && IsWordLikeElement(left) && IsWordLikeElement(right);

    private bool probingJoinedRun;

    /// <summary>
    /// Whether the line has to be broken before the current leaf because it begins a run of leaves
    /// joined by non-breakable blanks and the run does not fit on what is left of the line.
    /// </summary>
    /// <remarks>
    /// A line is broken before whichever leaf does not fit, so without this a non-breakable blank
    /// would be broken at like any other leaf boundary - before it, or before the word after it.
    /// The whole run is measured instead, as the soft hyphen measures the word after it, and moved
    /// down together. A run that starts a line is let through and broken where it has to be, as a
    /// word longer than the measure is. Only while formatting: the rendering pass measures lines
    /// already broken and must not break them again.
    /// </remarks>
    private bool JoinedRunBreaksBeforeCurrentLeaf()
    {
        if (probingJoinedRun || phase != Phase.Formatting || currentLeaf == null || startLeaf == null
            || currentLeaf.Current == startLeaf.Current)
            return false;

        var first = currentLeaf;
        var previous = first.GetPreviousLeaf();
        if (previous != null && IsJoinedTo(previous.Current, first.Current))
            return false;
        var next = first.GetNextLeaf();
        if (next == null || !IsJoinedTo(first.Current, next.Current))
            return false;

        SaveBeforeProbing(out var iter, out var blankCount, out var wordsWidth, out var xPosition, out var lineWidth, out var blankWidth);
        var wordWidth = savedWordWidth;
        var verticalInfo = currentVerticalInfo;
        FormatResult result;
        probingJoinedRun = true;
        try
        {
            for (var leaf = first; ; leaf = next)
            {
                currentLeaf = leaf;
                result = FormatLeaf(leaf.Current);
                if (result != FormatResult.Continue && result != FormatResult.Ignore)
                    break;

                next = leaf.GetNextLeaf();
                if (next == null || !IsJoinedTo(leaf.Current, next.Current))
                    break;
            }
        }
        finally
        {
            probingJoinedRun = false;
            RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
            savedWordWidth = wordWidth;
            currentVerticalInfo = verticalInfo;
        }
        return result == FormatResult.NewLine;
    }

    private FormatResult FormatLeaf(DocumentObject docObj)
    {
        switch (docObj.GetType().Name)
        {
            case "Text":
                if (IsBlank(docObj))
                    return FormatBlank();
                if (IsSoftHyphen(docObj))
                    return FormatSoftHyphen();
                return FormatText((Text)docObj);

            case "Character":
                return FormatCharacter((Character)docObj);

            case "DateField":
                return FormatDateField((DateField)docObj);

            case "InfoField":
                return FormatInfoField((InfoField)docObj);

            case "NumPagesField":
                return FormatNumPagesField((NumPagesField)docObj);

            case "PageField":
                return FormatPageField((PageField)docObj);

            case "SectionField":
                return FormatSectionField((SectionField)docObj);

            case "SectionPagesField":
                return FormatSectionPagesField((SectionPagesField)docObj);

            case "BookmarkField":
                return FormatBookmarkField((BookmarkField)docObj);

            case "PageRefField":
                return FormatPageRefField((PageRefField)docObj);

            case "Image":
                return FormatImage();

            // Only the reference mark: the note's own content is block content, laid out on its
            // own and drawn at the foot of the page. See FormatFootnote.
            case "Footnote":
                return FormatFootnote((Footnote)docObj);

            default:
                return FormatResult.Continue;
        }
    }

    /// <summary>
    /// Measures a footnote's reference mark, which occupies the running text exactly as a short
    /// superscript word does.
    /// </summary>
    /// <remarks>
    /// The note's own content is not measured here and takes no room in the paragraph. It is laid
    /// out separately by <see cref="FormattedFootnote"/> and the space for it is taken off the foot
    /// of the page before this paragraph is formatted - see <see cref="TopDownFormatter"/>.
    /// </remarks>
    private FormatResult FormatFootnote(Footnote footnote)
    {
        // The mark's text depends on where every note on the page ended up, so like a page
        // reference it can change between being measured and being drawn. Same answer as
        // FormatPageRefField: ask for the line to be measured again.
        reMeasureLine = true;
        return FormatAsWord(MeasureFootnoteMark(MarkOf(footnote)));
    }

    private void RenderFootnote(Footnote footnote)
    {
        var mark = MarkOf(footnote);
        if (mark.Length == 0)
            return;

        var width = MeasureFootnoteMark(mark);

        if (probing)
        {
            // The mark is text on the line like any other. Left out of the probed string, every
            // character index after it would be short by its length and the bidirectional algorithm
            // would place the rest of the line against the wrong positions.
            probedText.Append(mark);
            currentXPosition += width;
            return;
        }

        var xFont = FontHandler.ToSubSuperFont(CurrentFont);

        // Opened here rather than around the whole of RenderFootnote, so that the probing walk above
        // neither marks anything nor builds an element: the line is walked twice and only the second
        // walk draws. The scope also builds the note's own element, at the point the note is cited
        // rather than at the foot of the page where it is drawn - see StructureTagger.
        using (Tagger.FootnoteReference(Gfx, footnote))
        {
            Gfx.DrawString(mark, xFont, CurrentBrush, currentXPosition, FootnoteMarkBaseline);
        }

        RealizeHyperlink(width);
        currentXPosition += width;
    }

    private string MarkOf(Footnote footnote) => DocumentRenderer.Footnotes.MarkFor(footnote);

    /// <summary>
    /// The mark's width, which is the string measured at the reduced size a superscript is set in.
    /// </summary>
    /// <remarks>
    /// Not <see cref="MeasureString"/>, which scales by the same factor but only when the run's own
    /// font says it is a superscript. A reference mark is raised whatever the text around it is set
    /// in, so the scaling is applied here rather than asked for.
    /// </remarks>
    private XUnit MeasureFootnoteMark(string mark)
    {
        var xFont = CurrentFont;
        return Gfx.MeasureString(mark, xFont, StringFormat).Width
            * FontHandler.GetSubSuperScaling(xFont);
    }

    /// <summary>
    /// Where the mark sits: raised off the line's baseline by the same amount a superscript run is.
    /// </summary>
    private XUnit FootnoteMarkBaseline
    {
        get
        {
            var xFont = CurrentFont;
            return currentYPosition
                + FontHandler.GetSubSuperScaling(xFont)
                * (xFont.GetHeight() - FontHandler.GetDescent(xFont));
        }
    }

    private FormatResult FormatImage()
    {
        var width = CurrentImageRenderInfo.LayoutInfo.ContentArea.Width;
        return FormatAsWord(width);
    }

    private RenderInfo CalcImageRenderInfo(Image image)
    {
        var renderer = Create(Gfx, DocumentRenderer, image, fieldInfos);
        renderer.Format(new Rectangle(0, 0, double.MaxValue, double.MaxValue), null);

        return renderer.RenderInfo;
    }

    private static bool IsPlainText(DocumentObject docObj)
    {
        if (docObj is Text)
            return !IsSoftHyphen(docObj) && !IsBlank(docObj);

        return false;
    }

    private static bool IsSymbol(DocumentObject docObj)
    {
        if (docObj is Character)
        {
            return !IsSpaceCharacter(docObj) && !IsTab(docObj) && !IsLineBreak(docObj);
        }
        return false;
    }

    private static bool IsSpaceCharacter(DocumentObject docObj)
    {
        if (docObj is not Character character)
            return false;

        switch (character.SymbolName)
        {
            case SymbolName.Blank:
            case SymbolName.Em:
            case SymbolName.Em4:
            case SymbolName.En:
                return true;
        }
        return false;
    }

    private static bool IsWordLikeElement(DocumentObject docObj)
    {
        if (IsPlainText(docObj))
            return true;

        if (FieldEvaluator.IsField(docObj))
            return true;

        return IsSymbol(docObj);
    }

    private FormatResult FormatBookmarkField(BookmarkField bookmarkField)
    {
        // The position is taken while formatting rather than while rendering because a link to
        // the bookmark may well be drawn before it -- a table of contents is the whole point --
        // and by then the answer has to be known already.
        fieldInfos.AddBookmark(bookmarkField.Name, currentYPosition);
        return FormatResult.Ignore;
    }

    private FormatResult FormatPageRefField(PageRefField pageRefField)
    {
        reMeasureLine = true;
        var fieldValue = GetFieldValue(pageRefField);
        return FormatWord(fieldValue);
    }

    private FormatResult FormatNumPagesField(NumPagesField numPagesField)
    {
        reMeasureLine = true;
        var fieldValue = GetFieldValue(numPagesField);
        return FormatWord(fieldValue);
    }

    private FormatResult FormatPageField(PageField pageField)
    {
        reMeasureLine = true;
        var fieldValue = GetFieldValue(pageField);
        return FormatWord(fieldValue);
    }

    private FormatResult FormatSectionField(SectionField sectionField)
    {
        reMeasureLine = true;
        var fieldValue = GetFieldValue(sectionField);
        return FormatWord(fieldValue);
    }

    private FormatResult FormatSectionPagesField(SectionPagesField sectionPagesField)
    {
        reMeasureLine = true;
        var fieldValue = GetFieldValue(sectionPagesField);
        return FormatWord(fieldValue);
    }

    /// <summary>
    /// Helper function for formatting word-like elements like text and fields.
    /// </summary>
    private FormatResult FormatWord(string word)
    {
        var width = MeasureString(word);
        return FormatAsWord(width);
    }

    private XUnit savedWordWidth = 0;

    /// <summary>
    /// When rendering a justified paragraph, only the part after the last tab stop needs remeasuring.
    /// </summary>
    private bool IgnoreHorizontalGrowth => phase == Phase.Rendering && paragraph.Format.Alignment == ParagraphAlignment.Justify &&
                                           !lastTabPassed;

    private FormatResult FormatAsWord(XUnit width)
    {
        var newVertInfo = CalcCurrentVerticalInfo();

        var rect = formattingArea.GetFittingRect(currentYPosition, newVertInfo.height + BottomBorderOffset);
        if (rect == null)
            return FormatResult.NewArea;

        if (currentXPosition + width <= rect.X + rect.Width - RightIndent + Tolerance)
        {
            savedWordWidth = width;
            currentXPosition += width;
            // For Tabs in justified context
            if (!IgnoreHorizontalGrowth)
                currentWordsWidth += width;
            if (savedBlankWidth > 0)
            {
                // For Tabs in justified context
                if (!IgnoreHorizontalGrowth)
                    ++currentBlankCount;
            }
            // For Tabs in justified context
            if (!IgnoreHorizontalGrowth)
                currentLineWidth += width + PopSavedBlankWidth();
            currentVerticalInfo = newVertInfo;
            minWidth = Math.Max(minWidth, width);
            return FormatResult.Continue;
        }

        savedWordWidth = width;
        return FormatResult.NewLine;
    }

    private FormatResult FormatDateField(DateField dateField)
    {
        reMeasureLine = true;
        var estimatedFieldValue = GlobalTimeSettings.Now.ToString(dateField.Format);
        return FormatWord(estimatedFieldValue);
    }

    private FormatResult FormatInfoField(InfoField infoField)
    {
        var fieldValue = GetFieldValue(infoField);
        return fieldValue != "" ? FormatWord(fieldValue) : FormatResult.Continue;
    }

    private Rectangle GetShadingArea()
    {
        var contentArea = renderInfo.LayoutInfo.ContentArea;
        var format = paragraph.Format;
        var left = contentArea.X;
        left += format.LeftIndent;
        if (format.FirstLineIndent < 0)
            left += format.FirstLineIndent;

        var top = contentArea.Y;
        XUnit bottom = contentArea.Y + contentArea.Height;
        XUnit right = contentArea.X + contentArea.Width;
        right -= format.RightIndent;

        if (paragraph.Format.IsNull("Borders"))
            return new Rectangle(left, top, right - left, bottom - top);

        var borders = format.Borders;
        var bordersRenderer = new BordersRenderer(borders, Gfx);

        if (renderInfo.FormatInfo.IsStarting)
            top += bordersRenderer.GetWidth(BorderType.Top);
        if (renderInfo.FormatInfo.IsEnding)
            bottom -= bordersRenderer.GetWidth(BorderType.Bottom);

        left -= borders.DistanceFromLeft;
        right += borders.DistanceFromRight;
        return new Rectangle(left, top, right - left, bottom - top);
    }

    private void RenderShading()
    {
        if (paragraph.Format.IsNull("Shading"))
            return;

        var shadingRenderer = new ShadingRenderer(Gfx, paragraph.Format.Shading);
        var area = GetShadingArea();

        shadingRenderer.Render(area.X, area.Y, area.Width, area.Height);
    }


    private void RenderBorders()
    {
        if (paragraph.Format.IsNull("Borders"))
            return;

        var shadingArea = GetShadingArea();
        var left = shadingArea.X;
        var top = shadingArea.Y;
        XUnit bottom = shadingArea.Y + shadingArea.Height;
        XUnit right = shadingArea.X + shadingArea.Width;

        var borders = paragraph.Format.Borders;
        var bordersRenderer = new BordersRenderer(borders, Gfx);
        var borderWidth = bordersRenderer.GetWidth(BorderType.Left);
        if (borderWidth > 0)
        {
            left -= borderWidth;
            bordersRenderer.RenderVertically(BorderType.Left, left, top, bottom - top);
        }

        borderWidth = bordersRenderer.GetWidth(BorderType.Right);
        if (borderWidth > 0)
        {
            bordersRenderer.RenderVertically(BorderType.Right, right, top, bottom - top);
            right += borderWidth;
        }

        borderWidth = bordersRenderer.GetWidth(BorderType.Top);
        if (renderInfo.FormatInfo.IsStarting && borderWidth > 0)
        {
            top -= borderWidth;
            bordersRenderer.RenderHorizontally(BorderType.Top, left, top, right - left);
        }

        borderWidth = bordersRenderer.GetWidth(BorderType.Bottom);
        if (renderInfo.FormatInfo.IsEnding && borderWidth > 0)
        {
            bordersRenderer.RenderHorizontally(BorderType.Bottom, left, bottom, right - left);
        }
    }

    private XUnit MeasureString(string word)
    {
        var xFont = CurrentFont;
        XUnit width = Gfx.MeasureString(word, xFont, StringFormat).Width;
        var font = CurrentDomFont;

        if (font.Subscript || font.Superscript)
            width *= FontHandler.GetSubSuperScaling(xFont);

        return width;
    }

    private XUnit GetSpaceWidth(Character character)
    {
        XUnit width = character.SymbolName switch
        {
            SymbolName.Blank => MeasureString(" "),
            SymbolName.Em => MeasureString("m"),
            SymbolName.Em4 => 0.25 * MeasureString("m"),
            SymbolName.En => MeasureString("n"),
            _ => 0
        };
        return width * character.Count;
    }

    private void RenderListSymbol()
    {
        if (!GetListSymbol(out var symbol, out var font))
            return;

        var brush = FontHandler.FontColorToXBrush(paragraph.Format.Font);
        Gfx.DrawString(symbol, font, brush, currentXPosition, CurrentBaselinePosition);
        currentXPosition += Gfx.MeasureString(symbol, font, StringFormat).Width;
        var tabOffset = NextTabOffset();
        currentXPosition += tabOffset.offset;
        lastTabPosition = currentXPosition;
    }

    private void FormatListSymbol()
    {
        if (!GetListSymbol(out var symbol, out var font))
            return;

        currentVerticalInfo = CalcVerticalInfo(font);
        currentXPosition += Gfx.MeasureString(symbol, font, StringFormat).Width;
        FormatTab();
    }

    private FormatResult FormatSpace(Character character)
    {
        var width = GetSpaceWidth(character);
        return FormatAsWord(width);
    }

    private static string GetSymbol(Character character)
    {
        var ch = character.SymbolName switch
        {
            SymbolName.Euro => "€",
            SymbolName.Copyright => "©",
            SymbolName.Trademark => "™",
            SymbolName.RegisteredTrademark => "®",
            SymbolName.Bullet => "•",
            SymbolName.Not => "¬",
            // HardBlank is the same value. That no line breaks at it is FormatElement's business.
            SymbolName.NonBreakableBlank => "\u00A0",
            SymbolName.EmDash => "—",
            SymbolName.EnDash => "–",
            // A character is its own code. SymbolName rather than Char, which keeps 16 bits of it.
            _ => CharacterText((uint)character.SymbolName)
        };
        var returnString = ch;
        var count = character.Count;
        while (--count > 0)
            returnString += ch;
        return returnString;
    }

    /// <summary>
    /// The text a character given by its code stands for. A code past U+FFFF is a surrogate pair,
    /// and one past the last code point is no character at all and draws as the replacement
    /// character. A named symbol with no text of its own - the top nibble set - is a NUL, which
    /// text normalization drops, as it always was.
    /// </summary>
    private static string CharacterText(uint code) => code switch
    {
        <= 0xFFFF => ((char)code).ToString(),
        <= 0x10FFFF => char.ConvertFromUtf32((int)code),
        >= 0x10000000 => "\0",
        _ => "\uFFFD"
    };

    private FormatResult FormatSymbol(Character character)
    {
        return FormatWord(GetSymbol(character));
    }

    /// <summary>
    /// Processes (measures) a special character within text.
    /// </summary>
    /// <param name="character">The character to process.</param>
    /// <returns>True if the character should start at a new line.</returns>
    private FormatResult FormatCharacter(Character character)
    {
        return character.SymbolName switch
        {
            SymbolName.Blank or SymbolName.Em or SymbolName.Em4 or SymbolName.En => FormatSpace(character),
            SymbolName.LineBreak => FormatLineBreak(),
            SymbolName.Tab => FormatTab(),
            _ => FormatSymbol(character)
        };
    }

    /// <summary>
    /// Processes (measures) a blank.
    /// </summary>
    /// <returns>True if the blank causes a line break.</returns>
    private FormatResult FormatBlank()
    {
        if (IgnoreBlank())
            return FormatResult.Ignore;

        savedWordWidth = 0;
        var width = MeasureString(" ");
        var newVertInfo = CalcCurrentVerticalInfo();
        var rect = formattingArea.GetFittingRect(currentYPosition, newVertInfo.height + BottomBorderOffset);
        if (rect == null)
            return FormatResult.NewArea;

        if (width + currentXPosition > rect.X + rect.Width + Tolerance)
            return FormatResult.NewLine;

        currentXPosition += width;
        currentVerticalInfo = newVertInfo;
        SaveBlankWidth(width);
        return FormatResult.Continue;
    }

    private FormatResult FormatLineBreak()
    {
        if (phase != Phase.Rendering)
            currentLeaf = currentLeaf.GetNextLeaf();

        savedWordWidth = 0;
        return FormatResult.NewLine;
    }

    /// <summary>
    /// Processes a text element during formatting.
    /// </summary>
    /// <param name="text">The text element to measure.</param>
    private FormatResult FormatText(Text text)
    {
        return FormatWord(text.Content);
    }

    private FormatResult FormatSoftHyphen()
    {
        if (currentLeaf.Current == startLeaf.Current)
            return FormatResult.Continue;

        var nextIter = currentLeaf.GetNextLeaf();
        var prevIter = currentLeaf.GetPreviousLeaf();
        if (!IsWordLikeElement(prevIter.Current) || !IsWordLikeElement(nextIter.Current))
            return FormatResult.Continue;

        //--- Save ---------------------------------
        SaveBeforeProbing(out var iter, out var blankCount, out var wordsWidth, out var xPosition, out var lineWidth, out var blankWidth);
        //------------------------------------------
        currentLeaf = nextIter;
        var result = FormatElement(nextIter.Current);

        //--- Restore ------------------------------
        RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
        //------------------------------------------
        if (result == FormatResult.Continue)
            return FormatResult.Continue;

        RestoreAfterProbing(iter, blankCount, wordsWidth, xPosition, lineWidth, blankWidth);
        var fittingRect = FittingRectOrBounds(formattingArea, currentYPosition, currentVerticalInfo.height);

        var hyphenWidth = MeasureString("-");
        if (xPosition + hyphenWidth <= fittingRect.X + fittingRect.Width + Tolerance
            // If one word fits, but not the hyphen, the formatting must continue with the next leaf
            || prevIter.Current == startLeaf.Current)
        {
            // For Tabs in justified context
            if (!IgnoreHorizontalGrowth)
            {
                currentWordsWidth += hyphenWidth;
                currentLineWidth += hyphenWidth;
            }
            currentLeaf = nextIter;
            return FormatResult.NewLine;
        }

        currentWordsWidth -= savedWordWidth;
        currentLineWidth -= savedWordWidth;
        currentLineWidth -= GetPreviousBlankWidth(prevIter);
        currentLeaf = prevIter;
        return FormatResult.NewLine;
    }

    private XUnit GetPreviousBlankWidth(ParagraphIterator beforeIter)
    {
        XUnit width = 0;
        var savedIter = currentLeaf;
        currentLeaf = beforeIter.GetPreviousLeaf();
        while (currentLeaf != null)
        {
            if (currentLeaf.Current is BookmarkField)
            {
                currentLeaf = currentLeaf.GetPreviousLeaf();
            }
            else if (IsBlank(currentLeaf.Current))
            {
                if (!IgnoreBlank())
                    width = CurrentWordDistance;

                break;
            }
            else
            {
                break;
            }
        }
        currentLeaf = savedIter;
        return width;
    }

    private void HandleNonFittingLine()
    {
        if (currentLeaf == null)
            return;

        if (savedWordWidth > 0)
        {
            currentWordsWidth = savedWordWidth;
            currentLineWidth = savedWordWidth;
        }
        currentLeaf = currentLeaf.GetNextLeaf();
        currentYPosition += currentVerticalInfo.height;
        currentVerticalInfo = new VerticalLineInfo();
    }

    /// <summary>
    /// Starts a new line by resetting measuring values.
    /// Do not call before the first first line is formatted!
    /// </summary>
    /// <returns>True, if the new line may fit the formatting area.</returns>
    private bool StartNewLine()
    {
        tabOffsets = new ArrayList();
        lastTab = null;
        lastTabPosition = 0;
        currentYPosition += currentVerticalInfo.height;
        var rect = formattingArea.GetFittingRect(currentYPosition, currentVerticalInfo.height + BottomBorderOffset);
        if (rect == null)
            return false;

        isFirstLine = false;
        currentXPosition = StartXPosition; // depends on "currentVerticalInfo"
        currentVerticalInfo = new VerticalLineInfo();
        currentVerticalInfo = CalcCurrentVerticalInfo();
        startLeaf = currentLeaf;
        currentBlankCount = 0;
        currentWordsWidth = 0;
        currentLineWidth = 0;
        return true;
    }
    /// <summary>
    /// Stores all line information.
    /// </summary>
    private void StoreLineInformation()
    {
        PopSavedBlankWidth();

        var topBorderOffset = TopBorderOffset;
        var contentArea = renderInfo.LayoutInfo.ContentArea;
        if (topBorderOffset > 0)//May only occure for the first line.
            contentArea = formattingArea.GetFittingRect(formattingArea.Y, topBorderOffset);

        // The measure this line was broken to. Kept, because uniting it into the content area below
        // loses it - see LineInfo.fittingRect.
        var lineFittingRect = formattingArea.GetFittingRect(currentYPosition, currentVerticalInfo.height);

        if (contentArea == null)
        {
            contentArea = lineFittingRect;
        }
        else
            contentArea = contentArea.Unite(lineFittingRect);

        var bottomBorderOffset = BottomBorderOffset;
        if (bottomBorderOffset > 0)
            contentArea = contentArea.Unite(formattingArea.GetFittingRect(currentYPosition + currentVerticalInfo.height, bottomBorderOffset));

        var lineInfo = new LineInfo { vertical = currentVerticalInfo };

        if (startLeaf != null && startLeaf == currentLeaf)
            HandleNonFittingLine();

        lineInfo.lastTab = lastTab;
        // Carried only for an area with something standing in it. Elsewhere the content area
        // answers the same question just as well, and it answers it later: a table formats its
        // cells in one place and renders them in another, so a rect kept from formatting would be
        // stale by the time the cell is drawn. See LineInfo.fittingRect.
        lineInfo.fittingRect = formattingArea is ObstructedArea ? lineFittingRect : null;
        renderInfo.LayoutInfo.ContentArea = contentArea;

        lineInfo.startIter = startLeaf;

        if (currentLeaf == null)
            lineInfo.endIter = new ParagraphIterator(paragraph.Elements).GetLastLeaf();
        else
            lineInfo.endIter = currentLeaf.GetPreviousLeaf();

        lineInfo.blankCount = currentBlankCount;

        lineInfo.wordsWidth = currentWordsWidth;

        lineInfo.lineWidth = currentLineWidth;
        lineInfo.tabOffsets = tabOffsets;
        lineInfo.reMeasureLine = reMeasureLine;

        savedWordWidth = 0;
        reMeasureLine = false;
        ((ParagraphFormatInfo)renderInfo.FormatInfo).AddLineInfo(lineInfo);
    }

    /// <summary>
    /// Gets the top border offset for the first line, else 0.
    /// </summary>
    private XUnit TopBorderOffset
    {
        get
        {
            XUnit offset = 0;
            if (!isFirstLine || paragraph.Format.IsNull("Borders"))
                return offset;

            offset += paragraph.Format.Borders.DistanceFromTop;
            if (paragraph.Format.IsNull("Borders"))
                return offset;

            var bordersRenderer = new BordersRenderer(paragraph.Format.Borders, Gfx);
            offset += bordersRenderer.GetWidth(BorderType.Top);
            return offset;
        }
    }

    private bool IsLastVisibleLeaf
    {
        get
        {
            // REM: Code is missing here for blanks, bookmarks etc. which might be invisible.
            return currentLeaf.IsLastLeaf;
        }
    }
    /// <summary>
    /// Gets the bottom border offset for the last line, else 0.
    /// </summary>
    private XUnit BottomBorderOffset
    {
        get
        {
            XUnit offset = 0;
            //while formatting, it is impossible to determine whether we are in the last line until the last visible leaf is reached.
            if (!((phase == Phase.Formatting && (currentLeaf == null || IsLastVisibleLeaf))
                  || (phase == Phase.Rendering && isLastLine)))
                return offset;

            if (paragraph.Format.IsNull("Borders"))
                return offset;

            offset += paragraph.Format.Borders.DistanceFromBottom;
            var bordersRenderer = new BordersRenderer(paragraph.Format.Borders, Gfx);
            offset += bordersRenderer.GetWidth(BorderType.Bottom);
            return offset;
        }
    }

    private VerticalLineInfo CalcCurrentVerticalInfo()
    {
        return CalcVerticalInfo(CurrentFont);
    }

    private VerticalLineInfo CalcVerticalInfo(XFont font)
    {
        var paragraphFormat = paragraph.Format;
        var spacingRule = paragraphFormat.LineSpacingRule;
        XUnit lineHeight = 0;

        var descent = FontHandler.GetDescent(font);
        descent = Math.Max(currentVerticalInfo.descent, descent);

        XUnit singleLineSpace = font.GetHeight();
        var imageRenderInfo = CurrentImageRenderInfo;
        if (imageRenderInfo != null)
            singleLineSpace = singleLineSpace - FontHandler.GetAscent(font) + imageRenderInfo.LayoutInfo.ContentArea.Height;

        XUnit inherentLineSpace = Math.Max(currentVerticalInfo.inherentlineSpace, singleLineSpace);
        switch (spacingRule)
        {
            case LineSpacingRule.Single:
                lineHeight = singleLineSpace;
                break;

            case LineSpacingRule.OnePtFive:
                lineHeight = 1.5 * singleLineSpace;
                break;

            case LineSpacingRule.Double:
                lineHeight = 2.0 * singleLineSpace;
                break;

            case LineSpacingRule.Multiple:
                lineHeight = paragraph.Format.LineSpacing * singleLineSpace;
                break;

            case LineSpacingRule.AtLeast:
                lineHeight = Math.Max(singleLineSpace, paragraph.Format.LineSpacing);
                break;

            case LineSpacingRule.Exactly:
                lineHeight = new XUnit(paragraph.Format.LineSpacing);
                inherentLineSpace = paragraph.Format.LineSpacing.Point;
                break;
        }
        lineHeight = Math.Max(currentVerticalInfo.height, lineHeight);
        if (MaxElementHeight > 0)
            lineHeight = Math.Min(MaxElementHeight - Tolerance, lineHeight);

        return new VerticalLineInfo(lineHeight, descent, inherentLineSpace);
    }

    /// <summary>
    /// The font used for the current paragraph element.
    /// </summary>
    private XFont CurrentFont => FontHandler.FontToXFont(CurrentDomFont, DocumentRenderer.PrivateFonts, Gfx.MUH);

    private Font CurrentDomFont
    {
        get
        {
            if (currentLeaf == null)
                return paragraph.Format.Font;

            var parent = DocumentRelations.GetParent(currentLeaf.Current);
            parent = DocumentRelations.GetParent(parent);
            if (parent is FormattedText formattedText)
                return formattedText.Font;
            if (parent is Hyperlink hyperlink)
                return hyperlink.Font;
            return paragraph.Format.Font;
        }
    }

    /// <summary>
    /// Help function to receive a line height on empty paragraphs.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="gfx">The GFX.</param>
    /// <param name="renderer">The renderer.</param>
    internal static XUnit GetLineHeight(ParagraphFormat format, XGraphics gfx, DocumentRenderer renderer)
    {
        var font = FontHandler.FontToXFont(format.Font, renderer.PrivateFonts, gfx.MUH);
        XUnit singleLineSpace = font.GetHeight();
        switch (format.LineSpacingRule)
        {
            case LineSpacingRule.Exactly:
                return format.LineSpacing.Point;

            case LineSpacingRule.AtLeast:
                return Math.Max(format.LineSpacing.Point, font.GetHeight());

            case LineSpacingRule.Multiple:
                return format.LineSpacing * format.Font.Size;

            case LineSpacingRule.OnePtFive:
                return 1.5 * singleLineSpace;

            case LineSpacingRule.Double:
                return 2.0 * singleLineSpace;

            case LineSpacingRule.Single:
            default:
                return singleLineSpace;
        }
    }

    private void RenderUnderline(XUnit width, bool isWord)
    {
        var pen = GetUnderlinePen(isWord);

        var penChanged = UnderlinePenChanged(pen);
        if (penChanged)
        {
            if (currentUnderlinePen != null)
                EndUnderline(currentUnderlinePen, currentXPosition);

            if (pen != null)
                StartUnderline(currentXPosition);

            currentUnderlinePen = pen;
        }

        if (!reordering && currentLeaf.Current != endLeaf.Current)
            return;

        if (currentUnderlinePen != null)
            EndUnderline(currentUnderlinePen, currentXPosition + width);

        currentUnderlinePen = null;
    }

    private void StartUnderline(XUnit xPosition)
    {
        underlineStartPos = xPosition;
    }

    private void EndUnderline(XPen pen, XUnit xPosition)
    {
        var yPosition = CurrentBaselinePosition;
        yPosition += 0.33 * currentVerticalInfo.descent;
        Gfx.DrawLine(pen, underlineStartPos, yPosition, xPosition, yPosition);
    }

    private XPen currentUnderlinePen;
    private XUnit underlineStartPos;

    private bool UnderlinePenChanged(XPen pen)
    {
        if (pen == null && currentUnderlinePen == null)
            return false;

        if (pen == null && currentUnderlinePen != null)
            return true;

        if (pen != null && currentUnderlinePen == null)
            return true;

        // ReSharper disable once PossibleNullReferenceException
        if (pen.Color != currentUnderlinePen.Color)
            return true;

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return pen.Width != currentUnderlinePen.Width;
        #pragma warning restore S1244
    }


    private void RenderStrikethrough(XUnit width, bool isWord)
    {
        var pen = GetStrikethroughPen(isWord);

        var penChanged = StrikethroughPenChanged(pen);
        if (penChanged)
        {
            if (currentStrikethroughPen != null)
                EndStrikethrough(currentStrikethroughPen, currentXPosition);

            if (pen != null)
                StartStrikethrough(currentXPosition);

            currentStrikethroughPen = pen;
        }

        if (!reordering && currentLeaf.Current != endLeaf.Current)
            return;

        if (currentStrikethroughPen != null)
            EndStrikethrough(currentStrikethroughPen, currentXPosition + width);

        currentStrikethroughPen = null;
    }

    private void StartStrikethrough(XUnit xPosition)
    {
        strikethroughStartPos = xPosition;
    }

    private void EndStrikethrough(XPen pen, XUnit xPosition)
    {
        var yPosition = CurrentBaselinePosition;
        yPosition -= pen.Width / 2;
        yPosition -= currentVerticalInfo.descent;

        Gfx.DrawLine(pen, strikethroughStartPos, yPosition, xPosition, yPosition);
    }

    private XPen currentStrikethroughPen;
    private XUnit strikethroughStartPos;

    private bool StrikethroughPenChanged(XPen pen)
    {
        if (pen == null && currentStrikethroughPen == null)
            return false;

        if (pen == null && currentStrikethroughPen != null)
            return true;

        if (pen != null && currentStrikethroughPen == null)
            return true;

        // ReSharper disable once PossibleNullReferenceException
        if (pen.Color != currentStrikethroughPen.Color)
            return true;

        #pragma warning disable S1244 // Exact on purpose: compared with the value last written, so any change at all is a change.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return pen.Width != currentStrikethroughPen.Width;
        #pragma warning restore S1244
    }

    private RenderInfo CurrentImageRenderInfo
    {
        get
        {
            if (currentLeaf is not { Current: Image image })
                return null;

            if (imageRenderInfos != null && imageRenderInfos.ContainsKey(image))
            {
                return (RenderInfo)imageRenderInfos[image];
            }

            imageRenderInfos ??= new Hashtable();

            var imageRenderInfo = CalcImageRenderInfo(image);
            imageRenderInfos.Add(image, imageRenderInfo);
            return imageRenderInfo;
        }
    }
    private XPen GetUnderlinePen(bool isWord)
    {
        var font = CurrentDomFont;
        var underlineType = font.Underline;
        if (underlineType == Underline.None)
            return null;

        if (underlineType == Underline.Words && !isWord)
            return null;

        var pen = new XPen(ColorHelper.ToXColor(font.Color, paragraph.Document.UseCmykColor), font.Size / 16);
        switch (font.Underline)
        {
            case Underline.DotDash:
                pen.DashStyle = XDashStyle.DashDot;
                break;

            case Underline.DotDotDash:
                pen.DashStyle = XDashStyle.DashDotDot;
                break;

            case Underline.Dash:
                pen.DashStyle = XDashStyle.Dash;
                break;

            case Underline.Dotted:
                pen.DashStyle = XDashStyle.Dot;
                break;

            case Underline.Single:
            default:
                pen.DashStyle = XDashStyle.Solid;
                break;
        }
        return pen;
    }

    private XPen GetStrikethroughPen(bool isWord)
    {
        var font = CurrentDomFont;
        var strikethroughType = font.Strikethrough;
        if (strikethroughType == Strikethrough.None)
            return null;

        if (strikethroughType == Strikethrough.Words && !isWord)
            return null;

        var pen = new XPen(ColorHelper.ToXColor(font.Color, paragraph.Document.UseCmykColor), font.Size / 16);
        switch (font.Strikethrough)
        {
            case Strikethrough.DotDash:
                pen.DashStyle = XDashStyle.DashDot;
                break;

            case Strikethrough.DotDotDash:
                pen.DashStyle = XDashStyle.DashDotDot;
                break;

            case Strikethrough.Dash:
                pen.DashStyle = XDashStyle.Dash;
                break;

            case Strikethrough.Dotted:
                pen.DashStyle = XDashStyle.Dot;
                break;

            case Strikethrough.Single:
            default:
                pen.DashStyle = XDashStyle.Solid;
                break;
        }
        return pen;
    }

    /// <summary>
    /// The format every string of this paragraph is measured and drawn with.
    /// </summary>
    /// <remarks>
    /// One instance per direction rather than the single shared one there used to be, because the
    /// direction is a property of the paragraph and the shared instance is reached by every
    /// paragraph in the process at once. They are built once and never written to afterwards, so
    /// sharing them is safe in the way sharing one mutable format would not have been.
    /// </remarks>
    private XStringFormat StringFormat => FormatFor(ParagraphDirection);

    /// <summary>Which way the paragraph says it runs.</summary>
    private BidiParagraphDirection ParagraphDirection => paragraph.Format.TextDirection;

    private static XStringFormat FormatFor(BidiParagraphDirection direction)
    {
        return direction switch
        {
            BidiParagraphDirection.LeftToRight => leftToRightFormat,
            BidiParagraphDirection.RightToLeft => rightToLeftFormat,
            _ => automaticFormat
        };
    }

    private static XStringFormat Built(BidiParagraphDirection direction)
    {
        var format = XStringFormats.Default;
        format.TextDirection = direction;
        return format;
    }

    private static readonly XStringFormat automaticFormat = Built(BidiParagraphDirection.Automatic);
    private static readonly XStringFormat leftToRightFormat = Built(BidiParagraphDirection.LeftToRight);
    private static readonly XStringFormat rightToLeftFormat = Built(BidiParagraphDirection.RightToLeft);

    /// <summary>
    /// The paragraph to format or render.
    /// </summary>
    private readonly Paragraph paragraph;
    /// <summary>
    /// The rect a line of this height would occupy at this position, or the area's own bounds
    /// where the area has no room for one.
    /// </summary>
    /// <remarks>
    /// For the places that need a left edge and a width and have no way to decline. A line being
    /// measured or drawn has to be somewhere, and where the area cannot say, the whole of it is a
    /// better answer than a null reference - which is what several of these call sites would have
    /// produced. The formatting phase declines properly instead, by asking for a new area.
    /// <para>
    /// It is the fallback <c>StartXPosition</c> already reached for in the rendering phase, under
    /// the comment "next lines for non fitting lines that produce an empty fitting rect". This
    /// gives the same answer one name.
    /// </para>
    /// </remarks>
    private static Rectangle FittingRectOrBounds(Area area, XUnit yPosition, XUnit height)
    {
        return area.GetFittingRect(yPosition, height)
               ?? new Rectangle(area.X, yPosition, area.Width, height);
    }

    /// <summary>
    /// While rendering, the measure the line being rendered was broken to. Null while formatting.
    /// </summary>
    private Rectangle currentLineFittingRect;

    private XUnit currentWordsWidth;
    private int currentBlankCount;
    private XUnit currentLineWidth;
    private bool isFirstLine;
    private bool isLastLine;
    private VerticalLineInfo currentVerticalInfo;
    private Area formattingArea;
    private XUnit currentYPosition;
    private XUnit currentXPosition;
    private ParagraphIterator currentLeaf;
    private ParagraphIterator startLeaf;
    private ParagraphIterator endLeaf;
    private bool reMeasureLine;
    private XUnit minWidth = 0;
    private Hashtable imageRenderInfos;
    private ArrayList tabOffsets;
    private DocumentObject lastTab;
    private bool lastTabPassed;
    private XUnit lastTabPosition;
}
