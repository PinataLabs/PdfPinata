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
using System.Diagnostics.CodeAnalysis;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Fields;
using PdfPinata.Drawing;

namespace PinataLayout.Rendering;

/// <summary>
/// Formats a series of document elements from top to bottom.
/// </summary>
internal class TopDownFormatter
{
    /// <summary>
    /// Returns the max of the given Margins, if both are positive or 0, the sum otherwise.
    /// </summary>
    /// <param name="prevBottomMargin">The bottom margin of the previous element.</param>
    /// <param name="nextTopMargin">The top margin of the next element.</param>
    /// <returns></returns>
    private static XUnit MarginMax(XUnit prevBottomMargin, XUnit nextTopMargin)
    {
        if (prevBottomMargin >= 0 && nextTopMargin >= 0)
            return Math.Max(prevBottomMargin, nextTopMargin);
        return prevBottomMargin + nextTopMargin;
    }

    internal TopDownFormatter(IAreaProvider areaProvider, DocumentRenderer documentRenderer,
        DocumentElements elements)
    {
        this.documentRenderer = documentRenderer;
        this.areaProvider = areaProvider;
        this.elements = elements;
    }

    private readonly IAreaProvider areaProvider;

    private readonly DocumentElements elements;

    /// <summary>
    /// Formats the elements on the areas provided by the area provider.
    /// </summary>
    /// <param name="graphics">The graphics object to render on.</param>
    /// <param name="topLevel">if set to <c>true</c> formats the object is on top level.</param>
    public void FormatOnAreas(XGraphics graphics, bool topLevel)
    {
        gfx = graphics;
        var state = new FormattingState { Ready = elements.Count == 0 };
        state.Area = areaProvider.GetNextArea();
        state.MaxHeight = state.Area.Height;
        if (state.Ready)
        {
            areaProvider.StoreRenderInfos(state.RenderInfos);
            return;
        }

        while (!state.Ready && state.Area != null)
        {
            FormatNextElement(state, topLevel);

            var allFormatted = state.Index == elements.Count && !state.Ready;
            if (!allFormatted)
                continue;

            areaProvider.StoreRenderInfos(state.RenderInfos);
            state.Ready = true;
        }
    }

    /// <summary>
    /// Where a run of <see cref="FormatOnAreas"/> has got to: the area being filled, the element
    /// being formatted, and what is carried over from the element before it.
    /// </summary>
    private sealed class FormattingState
    {
        internal Area Area;
        internal XUnit MaxHeight;
        internal int Index;
        internal bool Ready;
        internal bool IsFirstOnPage = true;
        internal XUnit PrevBottomMargin;
        internal RenderInfo PrevRenderInfo;
        internal FormatInfo PrevFormatInfo;
        internal ArrayList RenderInfos = new();
    }

    /// <summary>
    /// Formats the element the state has got to, on the area it has got to, and moves the state on:
    /// to the next element when this one is placed, or to the next area when it breaks.
    /// </summary>
    private void FormatNextElement(FormattingState state, bool topLevel)
    {
        var docObj = elements[state.Index];
        var renderer = Renderer.Create(gfx, documentRenderer, docObj, areaProvider.AreaFieldInfos);
        renderer?.MaxElementHeight = state.MaxHeight; // "Slightly hacked" for legends: see below

        if (topLevel)
            ReportProgress(state);

        // "Slightly hacked" for legends: they are rendered as part of the chart.
        // So they are skipped here.
        if (renderer == null)
        {
            SkipElementWithoutRenderer(state, docObj);
            return;
        }

        if (state.PrevFormatInfo == null)
            state.Area = state.Area.Lower(DistanceBefore(renderer.InitialLayoutInfo, state.PrevBottomMargin));

        // Room for whatever footnotes this element carries, taken off the bottom of the area
        // before the element is laid out in it - so the element sees the space that is really
        // left and breaks the page where it should. Nothing already placed above moves: the
        // notes go at the foot, and what is above the foot fits either way.
        //
        // Nothing here has to be undone when an element does not fit. The shrunken area is
        // discarded with the page, the element is formatted again on the next one, and the
        // notes are registered again against that page - which is what makes a single pass
        // enough for what would otherwise be a fixed point.
        state.Area = state.Area.Shorten(ReserveFootnotes(docObj, state.Area));

        renderer.Format(state.Area, state.PrevFormatInfo);
        areaProvider.PositionHorizontally(renderer.RenderInfo.LayoutInfo);
        var pagebreakBefore = BreaksAreaBefore(state, renderer);

        if (!pagebreakBefore && renderer.RenderInfo.FormatInfo.IsEnding)
            PlaceEndingElement(state, renderer, docObj);
        else
            BreakArea(state, renderer, docObj, pagebreakBefore);
    }

    private void ReportProgress(FormattingState state)
    {
        if (documentRenderer.HasPrepareDocumentProgress)
        {
            documentRenderer.OnPrepareDocumentProgress(documentRenderer.ProgressCompleted + state.Index + 1,
                documentRenderer.ProgressMaximum);
        }
    }

    /// <summary>
    /// Whether a formatted element has to start a new area: asked for one, or is forced into one
    /// by what follows it. The first element on a page never does.
    /// </summary>
    private bool BreaksAreaBefore(FormattingState state, Renderer renderer)
    {
        var breakRequested = areaProvider.IsAreaBreakBefore(renderer.RenderInfo.LayoutInfo);
        if (state.IsFirstOnPage)
            return false;
        return breakRequested || IsForcedAreaBreak(state.Index, renderer, state.Area);
    }

    /// <summary>
    /// Passes over an element that has no renderer, registering it first if it is a bookmark.
    /// </summary>
    private void SkipElementWithoutRenderer(FormattingState state, DocumentObject docObj)
    {
        // A bookmark draws nothing, so it has no renderer and would otherwise be skipped along
        // with the legends -- silently, which is what made a bookmark put on a section rather
        // than in a paragraph vanish without a word. Register it where it stands instead.
        if (docObj is BookmarkField bookmark)
            areaProvider.AreaFieldInfos.AddBookmark(bookmark.Name, state.Area.Y);

        state.Ready = state.Index == elements.Count - 1;
        if (state.Ready)
            areaProvider.StoreRenderInfos(state.RenderInfos);
        ++state.Index;
    }

    /// <summary>
    /// The space to leave above an element that starts afresh on the area: the previous
    /// element's bottom margin, or this one's top margin where it is larger and the element is
    /// placed after the previous one.
    /// </summary>
    private static XUnit DistanceBefore(LayoutInfo initialLayoutInfo, XUnit prevBottomMargin)
    {
        var distance = prevBottomMargin;
        if (IsPlacedAfterPreviousElement(initialLayoutInfo))
            distance = MarginMax(initialLayoutInfo.MarginTop, distance);

        return distance;
    }

    /// <summary>
    /// Whether an element is placed in the flow after the one before it, rather than positioned
    /// on the page independently of it.
    /// </summary>
    private static bool IsPlacedAfterPreviousElement(LayoutInfo layoutInfo) =>
        layoutInfo.VerticalReference == VerticalReference.PreviousElement &&
        layoutInfo.Floating != Floating.None; //Added KlPo 12.07.07

    /// <summary>
    /// Deals with an element whose ending fits on this area: shortens the previous element to make
    /// room for it, moves it whole to the next area to keep it with what follows, or places it.
    /// </summary>
    private void PlaceEndingElement(FormattingState state, Renderer renderer, DocumentObject docObj)
    {
        if (PreviousRendererNeedsRemoveEnding(state.PrevRenderInfo, renderer.RenderInfo))
        {
            state.PrevRenderInfo.RemoveEnding();
            renderer = Renderer.Create(gfx, documentRenderer, docObj, areaProvider.AreaFieldInfos);
            renderer.MaxElementHeight = state.MaxHeight;
            renderer.Format(state.Area, state.PrevRenderInfo.FormatInfo);
        }
        else if (NeedsEndingOnNextArea(state.Index, renderer, state.Area, state.IsFirstOnPage))
        {
            renderer.RenderInfo.RemoveEnding();
            // No break was forced before this element, or it would not have been placed at all.
            FinishArea(state, renderer.RenderInfo, pagebreakBefore: false);
            if (state.PrevRenderInfo == null)
                state.IsFirstOnPage = true;

            StartNextArea(state);
        }
        else
        {
            PlaceElement(state, renderer.RenderInfo);
        }
    }

    /// <summary>
    /// Places a formatted element on the area and moves on to the next element, below it or -
    /// for a shape the text runs beside - beside it.
    /// </summary>
    private void PlaceElement(FormattingState state, RenderInfo renderInfo)
    {
        state.RenderInfos.Add(renderInfo);
        state.IsFirstOnPage = false;
        areaProvider.PositionVertically(renderInfo.LayoutInfo);

        state.PrevBottomMargin = 0;
        if (IsPlacedAfterPreviousElement(renderInfo.LayoutInfo))
        {
            // A shape the text runs beside does not push what follows it down the page;
            // it stands in the area the following elements are laid out in. That is the
            // whole difference between wrapping around a shape and being placed after
            // one, and it is the only place the two part company.
            Area beside = AreaBesideShape(state.Area, renderInfo.LayoutInfo);
            if (beside != null)
            {
                // No bottom margin to carry: the next element is not placed after this
                // shape, so there is nothing for a margin between them to separate.
                // DistanceBottom has already grown the obstacle, and charging it again
                // here would push the following text down the page as well as holding
                // it off the shape - the same gap counted twice.
                state.Area = beside;
            }
            else
            {
                state.PrevBottomMargin = renderInfo.LayoutInfo.MarginBottom;
                state.Area = state.Area.Lower(renderInfo.LayoutInfo.ContentArea.Height);
            }
        }

        state.PrevFormatInfo = null;
        state.PrevRenderInfo = null;

        ++state.Index;
    }

    /// <summary>
    /// Ends the area at this element, which either breaks across it or does not fit on it at all,
    /// and moves on to the next area. An element that does not fit even on an area of its own is
    /// placed regardless, on an area of unlimited height.
    /// </summary>
    private void BreakArea(FormattingState state, Renderer renderer, DocumentObject docObj, bool pagebreakBefore)
    {
        if (renderer.RenderInfo.FormatInfo.IsEmpty && state.IsFirstOnPage)
            renderer = FormatOnUnboundedArea(state, docObj);

        FinishArea(state, renderer.RenderInfo, pagebreakBefore);
        state.IsFirstOnPage = true;
        if (!state.Ready) //!!!newTHHO 19.01.2007: korrekt? oder GetNextArea immer ausf�hren???
            StartNextArea(state);
    }

    /// <summary>
    /// Formats an element that fits nowhere on an area as tall as it needs, places it there and
    /// moves on to the next element.
    /// </summary>
    private Renderer FormatOnUnboundedArea(FormattingState state, DocumentObject docObj)
    {
        state.Area = state.Area.Unite(new Rectangle(state.Area.X, state.Area.Y, state.Area.Width, double.MaxValue));

        var renderer = Renderer.Create(gfx, documentRenderer, docObj, areaProvider.AreaFieldInfos);
        renderer.MaxElementHeight = state.MaxHeight;
        renderer.Format(state.Area, state.PrevFormatInfo);

        areaProvider.PositionHorizontally(renderer.RenderInfo.LayoutInfo);
        areaProvider.PositionVertically(renderer.RenderInfo.LayoutInfo);
        state.Ready = state.Index == elements.Count - 1;

        ++state.Index;
        return renderer;
    }

    /// <summary>
    /// Hands the area's render infos to the area provider, and carries over to the next area what
    /// is left to format of the element it ended at, if anything.
    /// </summary>
    private void FinishArea(FormattingState state, RenderInfo lastRenderInfo, bool pagebreakBefore)
    {
        state.PrevRenderInfo = FinishPage(lastRenderInfo, pagebreakBefore, ref state.RenderInfos);
        state.PrevFormatInfo = state.PrevRenderInfo?.FormatInfo;
        state.PrevBottomMargin = 0;
    }

    private void StartNextArea(FormattingState state)
    {
        state.Area = areaProvider.GetNextArea();
        state.MaxHeight = state.Area.Height;
    }

    /// <summary>
    /// How much of the area to set aside for the footnotes this element carries.
    /// </summary>
    /// <remarks>
    /// Zero for the overwhelming majority of elements, and the scan that establishes that costs a
    /// walk of a paragraph's own children. Only <see cref="FormattedDocument"/> can answer for
    /// real: a footnote goes at the foot of a page, and a cell, a text frame or a header does not
    /// own one. Rather than drop the note - which is what this assembly did for twenty years -
    /// that case says so.
    /// </remarks>
    private XUnit ReserveFootnotes(DocumentObject docObj, Area area)
    {
        if (areaProvider is IFootnoteAreaProvider provider)
            return provider.ReserveFootnotes(docObj, area.Width, gfx);

        if (Footnotes.In(docObj).Count == 0)
            return 0;

        throw new NotSupportedException(
            "A footnote can only be attached to a paragraph that is laid out on a page. This one is "
            + "inside a table cell, a text frame, a header or footer, or another footnote, none of "
            + "which owns the page its note would have to appear at the foot of. Move the footnote "
            + "to a paragraph in the section itself, or put its text where it stands.");
    }

    /// <summary>
    /// Finishes rendering for the page.
    /// </summary>
    /// <param name="lastRenderInfo">The last render info.</param>
    /// <param name="pagebreakBefore">set to <c>true</c> if there is a pagebreak before this page.</param>
    /// <param name="renderInfos">The render infos.</param>
    /// <returns>
    /// The RenderInfo to set as previous RenderInfo.
    /// </returns>
    private RenderInfo FinishPage(RenderInfo lastRenderInfo, bool pagebreakBefore, ref ArrayList renderInfos)
    {
        RenderInfo prevRenderInfo;
        if (lastRenderInfo.FormatInfo.IsEmpty || pagebreakBefore)
        {
            prevRenderInfo = null;
        }
        else
        {
            prevRenderInfo = lastRenderInfo;
            renderInfos.Add(lastRenderInfo);
            if (lastRenderInfo.FormatInfo.IsEnding)
                prevRenderInfo = null;
        }

        areaProvider.StoreRenderInfos(renderInfos);
        renderInfos = new ArrayList();
        return prevRenderInfo;
    }

    /// <summary>
    /// The area the elements after a side-wrapped shape are laid out in: the same area, with the
    /// shape standing in it. Null where the shape is not one the text runs beside, or where it
    /// cannot be made to stand in the area.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The obstacle is the shape's rectangle grown by all four wrap distances, which is what makes
    /// every one of them mean something. <c>DistanceLeft</c> and <c>DistanceRight</c> hold the text
    /// off horizontally, as they always claimed to; <c>DistanceTop</c> and <c>DistanceBottom</c>
    /// grow it vertically, so a line whose box would otherwise clear the shape by a hair is pushed
    /// past it instead.
    /// </para>
    /// <para>
    /// <b>The side is expressed in the obstacle, not in the area.</b> A shape the text runs down
    /// the left of blocks everything from its own left edge to the right edge of the area, so the
    /// only clear span left is the one the caller asked for. That keeps
    /// <see cref="ObstructedArea"/> free of any notion of sides: it subtracts what it is given and
    /// the text goes where it can.
    /// </para>
    /// </remarks>
    private static ObstructedArea AreaBesideShape(Area area, LayoutInfo layoutInfo)
    {
        if (layoutInfo.Floating != Floating.Left && layoutInfo.Floating != Floating.Right &&
            layoutInfo.Floating != Floating.BothSides)
            return null;

        var shape = layoutInfo.ContentArea;
        if (shape == null)
            return null;

        XUnit top = shape.Y - layoutInfo.MarginTop;
        XUnit bottom = shape.Y + shape.Height + layoutInfo.MarginBottom;
        XUnit left = shape.X - layoutInfo.MarginLeft;
        XUnit right = shape.X + shape.Width + layoutInfo.MarginRight;

        // A shape taller than the area left to it cannot be an obstacle in that area: the obstacle
        // would outlive the area holding it, and the text after the page break would be laid out
        // around something that is no longer there. Fall back to being placed between neighbours,
        // which is a predictable degradation rather than a wrong page.
        if (bottom > area.Y + area.Height + Renderer.Tolerance)
            return null;

        switch (layoutInfo.Floating)
        {
            case Floating.Left:
                // Text on the left, so everything from the shape rightwards is taken.
                right = area.X + area.Width;
                break;

            case Floating.Right:
                left = area.X;
                break;
        }

        if (right - left <= Renderer.Tolerance || bottom - top <= Renderer.Tolerance)
            return null;

        var obstacle = new Rectangle(left, top, right - left, bottom - top);
        var bounds = new Rectangle(area.X, area.Y, area.Width, area.Height);

        if (area is not ObstructedArea standing)
            return new ObstructedArea(bounds, [obstacle]);

        // A second shape beside the first, rather than one replacing the other.
        var all = new List<Rectangle>(standing.Obstacles) { obstacle };
        return new ObstructedArea(bounds, all);
    }

    /// <summary>
    /// Indicates that a break between areas has to be performed before the element with the given idx.
    /// </summary>
    /// <param name="idx">Index of the document element.</param>
    /// <param name="renderer">A formatted renderer for the document element.</param>
    /// <param name="remainingArea">The remaining area.</param>
    private bool IsForcedAreaBreak(int idx, Renderer renderer, Area remainingArea)
    {
        var formatInfo = renderer.RenderInfo.FormatInfo;
        var layoutInfo = renderer.RenderInfo.LayoutInfo;

        if (formatInfo.IsStarting && !formatInfo.StartingIsComplete)
            return true;

        if (layoutInfo.KeepTogether && !formatInfo.IsComplete)
            return true;

        if (!layoutInfo.KeepTogether || !layoutInfo.KeepWithNext)
            return false;

        var area = remainingArea.Lower(layoutInfo.ContentArea.Height);
        return NextElementsDontFit(idx, area, layoutInfo.MarginBottom);
    }

    /// <summary>
    /// Indicates that the Ending of the element has to be removed.
    /// </summary>
    /// <param name="prevRenderInfo">The prev render info.</param>
    /// <param name="succedingRenderInfo">The succeding render info.</param>
    private bool PreviousRendererNeedsRemoveEnding([NotNullWhen(true)] RenderInfo prevRenderInfo, RenderInfo succedingRenderInfo)
    {
        if (prevRenderInfo == null)
            return false;
        var layoutInfo = succedingRenderInfo.LayoutInfo;
        var formatInfo = succedingRenderInfo.FormatInfo;
        var prevLayoutInfo = prevRenderInfo.LayoutInfo;
        if (!formatInfo.IsEnding || formatInfo.EndingIsComplete)
            return false;

        var area = areaProvider.ProbeNextArea();
        if (area.Height > prevLayoutInfo.TrailingHeight + layoutInfo.TrailingHeight + Renderer.Tolerance)
            return true;

        return false;
    }

    /// <summary>
    /// The maximum number of elements that can be combined via keepwithnext and keeptogether
    /// </summary>
    private static readonly int MaxCombineElements = 10;

    private bool NextElementsDontFit(int idx, Area remainingArea, XUnit previousMarginBottom)
    {
        var elementDistance = previousMarginBottom;
        var area = remainingArea;
        for (var index = idx + 1; index < elements.Count; ++index)
        {
            // Never combine more than MaxCombineElements elements
            if (index - idx > MaxCombineElements)
                return false;

            var obj = elements[index];
            var currRenderer = Renderer.Create(gfx, documentRenderer, obj, areaProvider.AreaFieldInfos);
            elementDistance = MarginMax(elementDistance, currRenderer.InitialLayoutInfo.MarginTop);
            area = area.Lower(elementDistance);

            if (area.Height <= 0)
                return true;

            currRenderer.Format(area, null);
            var verdict = KeptElementVerdict(currRenderer);
            if (verdict.HasValue)
                return verdict.Value;

            var currLayoutInfo = currRenderer.RenderInfo.LayoutInfo;
            area = area.Lower(currLayoutInfo.ContentArea.Height);
            if (area.Height <= 0)
                return true;

            elementDistance = currLayoutInfo.MarginBottom;
        }

        return false;
    }

    /// <summary>
    /// What a formatted following element settles about whether the chain fits: true when it does
    /// not, false when it does, and null when the element is kept together with the next one too
    /// and the chain has to be followed further.
    /// </summary>
    private static bool? KeptElementVerdict(Renderer currRenderer)
    {
        var currFormatInfo = currRenderer.RenderInfo.FormatInfo;
        var currLayoutInfo = currRenderer.RenderInfo.LayoutInfo;

        if (currLayoutInfo.VerticalReference != VerticalReference.PreviousElement)
            return false;

        if (!currFormatInfo.StartingIsComplete)
            return true;

        if (currLayoutInfo.KeepTogether && !currFormatInfo.IsComplete)
            return true;

        if (!(currLayoutInfo.KeepTogether && currLayoutInfo.KeepWithNext))
            return false;

        return null;
    }

    private bool NeedsEndingOnNextArea(int idx, Renderer renderer, Area remainingArea, bool isFirstOnPage)
    {
        var layoutInfo = renderer.RenderInfo.LayoutInfo;
        if (isFirstOnPage && layoutInfo.KeepTogether)
            return false;
        var formatInfo = renderer.RenderInfo.FormatInfo;

        if (!formatInfo.EndingIsComplete)
            return false;

        if (!layoutInfo.KeepWithNext)
            return false;

        remainingArea = remainingArea.Lower(layoutInfo.ContentArea.Height);
        return NextElementsDontFit(idx, remainingArea, layoutInfo.MarginBottom);
    }

    private readonly DocumentRenderer documentRenderer;
    private XGraphics gfx;
}
