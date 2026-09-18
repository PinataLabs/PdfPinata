#region MigraDoc - Creating Documents on the Fly

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
        else
            return prevBottomMargin + nextTopMargin;
    }

    internal TopDownFormatter(IAreaProvider areaProvider, DocumentRenderer documentRenderer,
        DocumentElements elements)
    {
        this.documentRenderer = documentRenderer;
        this.areaProvider = areaProvider;
        this.elements = elements;
    }

    IAreaProvider areaProvider;

    private DocumentElements elements;

    /// <summary>
    /// Formats the elements on the areas provided by the area provider.
    /// </summary>
    /// <param name="gfx">The graphics object to render on.</param>
    /// <param name="topLevel">if set to <c>true</c> formats the object is on top level.</param>
    public void FormatOnAreas(XGraphics gfx, bool topLevel)
    {
        this.gfx = gfx;
        XUnit prevBottomMargin = 0;
        RenderInfo prevRenderInfo = null;
        FormatInfo prevFormatInfo = null;
        var renderInfos = new ArrayList();
        var ready = elements.Count == 0;
        var isFirstOnPage = true;
        var area = areaProvider.GetNextArea();
        var maxHeight = area.Height;
        if (ready)
        {
            areaProvider.StoreRenderInfos(renderInfos);
            return;
        }

        var idx = 0;
        while (!ready && area != null)
        {
            var docObj = elements[idx];
            var renderer = Renderer.Create(gfx, documentRenderer, docObj, areaProvider.AreaFieldInfos);
            if (renderer != null) // "Slightly hacked" for legends: see below
                renderer.MaxElementHeight = maxHeight;

            if (topLevel && documentRenderer.HasPrepareDocumentProgress)
            {
                documentRenderer.OnPrepareDocumentProgress(documentRenderer.ProgressCompleted + idx + 1,
                    documentRenderer.ProgressMaximum);
            }

            // "Slightly hacked" for legends: they are rendered as part of the chart.
            // So they are skipped here.
            if (renderer == null)
            {
                // A bookmark draws nothing, so it has no renderer and would otherwise be skipped along
                // with the legends -- silently, which is what made a bookmark put on a section rather
                // than in a paragraph vanish without a word. Register it where it stands instead.
                var bookmark = docObj as BookmarkField;
                if (bookmark != null)
                    areaProvider.AreaFieldInfos.AddBookmark(bookmark.Name, area.Y);

                ready = idx == elements.Count - 1;
                if (ready)
                    areaProvider.StoreRenderInfos(renderInfos);
                ++idx;
                continue;
            }

            if (prevFormatInfo == null)
            {
                var initialLayoutInfo = renderer.InitialLayoutInfo;
                var distance = prevBottomMargin;
                if (initialLayoutInfo.VerticalReference == VerticalReference.PreviousElement &&
                    initialLayoutInfo.Floating != Floating.None) //Added KlPo 12.07.07
                    distance = MarginMax(initialLayoutInfo.MarginTop, distance);

                area = area.Lower(distance);
            }

            // Room for whatever footnotes this element carries, taken off the bottom of the area
            // before the element is laid out in it - so the element sees the space that is really
            // left and breaks the page where it should. Nothing already placed above moves: the
            // notes go at the foot, and what is above the foot fits either way.
            //
            // Nothing here has to be undone when an element does not fit. The shrunken area is
            // discarded with the page, the element is formatted again on the next one, and the
            // notes are registered again against that page - which is what makes a single pass
            // enough for what would otherwise be a fixed point.
            area = area.Shorten(ReserveFootnotes(docObj, area));

            renderer.Format(area, prevFormatInfo);
            areaProvider.PositionHorizontally(renderer.RenderInfo.LayoutInfo);
            var pagebreakBefore = areaProvider.IsAreaBreakBefore(renderer.RenderInfo.LayoutInfo) && !isFirstOnPage;
            pagebreakBefore = pagebreakBefore || !isFirstOnPage && IsForcedAreaBreak(idx, renderer, area);

            if (!pagebreakBefore && renderer.RenderInfo.FormatInfo.IsEnding)
            {
                if (PreviousRendererNeedsRemoveEnding(prevRenderInfo, renderer.RenderInfo))
                {
                    prevRenderInfo.RemoveEnding();
                    renderer = Renderer.Create(gfx, documentRenderer, docObj, areaProvider.AreaFieldInfos);
                    renderer.MaxElementHeight = maxHeight;
                    renderer.Format(area, prevRenderInfo.FormatInfo);
                }
                else if (NeedsEndingOnNextArea(idx, renderer, area, isFirstOnPage))
                {
                    renderer.RenderInfo.RemoveEnding();
                    prevRenderInfo = FinishPage(renderer.RenderInfo, pagebreakBefore, ref renderInfos);
                    if (prevRenderInfo != null)
                        prevFormatInfo = prevRenderInfo.FormatInfo;
                    else
                    {
                        prevFormatInfo = null;
                        isFirstOnPage = true;
                    }

                    prevBottomMargin = 0;
                    area = areaProvider.GetNextArea();
                    maxHeight = area.Height;
                }
                else
                {
                    renderInfos.Add(renderer.RenderInfo);
                    isFirstOnPage = false;
                    areaProvider.PositionVertically(renderer.RenderInfo.LayoutInfo);
                    if (renderer.RenderInfo.LayoutInfo.VerticalReference == VerticalReference.PreviousElement
                        && renderer.RenderInfo.LayoutInfo.Floating != Floating.None) //Added KlPo 12.07.07
                    {
                        // A shape the text runs beside does not push what follows it down the page;
                        // it stands in the area the following elements are laid out in. That is the
                        // whole difference between wrapping around a shape and being placed after
                        // one, and it is the only place the two part company.
                        Area beside = AreaBesideShape(area, renderer.RenderInfo.LayoutInfo);
                        if (beside != null)
                        {
                            area = beside;

                            // No bottom margin to carry: the next element is not placed after this
                            // shape, so there is nothing for a margin between them to separate.
                            // DistanceBottom has already grown the obstacle, and charging it again
                            // here would push the following text down the page as well as holding
                            // it off the shape - the same gap counted twice.
                            prevBottomMargin = 0;
                        }
                        else
                        {
                            prevBottomMargin = renderer.RenderInfo.LayoutInfo.MarginBottom;
                            area = area.Lower(renderer.RenderInfo.LayoutInfo.ContentArea.Height);
                        }
                    }
                    else
                        prevBottomMargin = 0;

                    prevFormatInfo = null;
                    prevRenderInfo = null;

                    ++idx;
                }
            }
            else
            {
                if (renderer.RenderInfo.FormatInfo.IsEmpty && isFirstOnPage)
                {
                    area = area.Unite(new Rectangle(area.X, area.Y, area.Width, double.MaxValue));

                    renderer = Renderer.Create(gfx, documentRenderer, docObj, areaProvider.AreaFieldInfos);
                    renderer.MaxElementHeight = maxHeight;
                    renderer.Format(area, prevFormatInfo);

                    areaProvider.PositionHorizontally(renderer.RenderInfo.LayoutInfo);
                    areaProvider.PositionVertically(renderer.RenderInfo.LayoutInfo);
                    ready = idx == elements.Count - 1;

                    ++idx;
                }

                prevRenderInfo = FinishPage(renderer.RenderInfo, pagebreakBefore, ref renderInfos);
                if (prevRenderInfo != null)
                    prevFormatInfo = prevRenderInfo.FormatInfo;
                else
                {
                    prevFormatInfo = null;
                }

                isFirstOnPage = true;
                prevBottomMargin = 0;
                if (!ready) //!!!newTHHO 19.01.2007: korrekt? oder GetNextArea immer ausf�hren???
                {
                    area = areaProvider.GetNextArea();
                    maxHeight = area.Height;
                }
            }

            if (idx == elements.Count && !ready)
            {
                areaProvider.StoreRenderInfos(renderInfos);
                ready = true;
            }
        }
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
    XUnit ReserveFootnotes(DocumentObject docObj, Area area)
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
    RenderInfo FinishPage(RenderInfo lastRenderInfo, bool pagebreakBefore, ref ArrayList renderInfos)
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
    static ObstructedArea AreaBesideShape(Area area, LayoutInfo layoutInfo)
    {
        if (layoutInfo.Floating != Floating.Left && layoutInfo.Floating != Floating.Right &&
            layoutInfo.Floating != Floating.BothSides)
            return null;

        Area shape = layoutInfo.ContentArea;
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

        if (area is ObstructedArea standing)
        {
            // A second shape beside the first, rather than one replacing the other.
            var all = new List<Rectangle>(standing.Obstacles) { obstacle };
            return new ObstructedArea(bounds, all);
        }

        return new ObstructedArea(bounds, new[] { obstacle });
    }

    /// <summary>
    /// Indicates that a break between areas has to be performed before the element with the given idx.
    /// </summary>
    /// <param name="idx">Index of the document element.</param>
    /// <param name="renderer">A formatted renderer for the document element.</param>
    /// <param name="remainingArea">The remaining area.</param>
    bool IsForcedAreaBreak(int idx, Renderer renderer, Area remainingArea)
    {
        var formatInfo = renderer.RenderInfo.FormatInfo;
        var layoutInfo = renderer.RenderInfo.LayoutInfo;

        if (formatInfo.IsStarting && !formatInfo.StartingIsComplete)
            return true;

        if (layoutInfo.KeepTogether && !formatInfo.IsComplete)
            return true;

        if (layoutInfo.KeepTogether && layoutInfo.KeepWithNext)
        {
            var area = remainingArea.Lower(layoutInfo.ContentArea.Height);
            return NextElementsDontFit(idx, area, layoutInfo.MarginBottom);
        }

        return false;
    }

    /// <summary>
    /// Indicates that the Ending of the element has to be removed.
    /// </summary>
    /// <param name="prevRenderInfo">The prev render info.</param>
    /// <param name="succedingRenderInfo">The succeding render info.</param>
    bool PreviousRendererNeedsRemoveEnding([NotNullWhen(true)] RenderInfo prevRenderInfo, RenderInfo succedingRenderInfo)
    {
        if (prevRenderInfo == null)
            return false;
        var layoutInfo = succedingRenderInfo.LayoutInfo;
        var formatInfo = succedingRenderInfo.FormatInfo;
        var prevLayoutInfo = prevRenderInfo.LayoutInfo;
        if (formatInfo.IsEnding && !formatInfo.EndingIsComplete)
        {
            var area = areaProvider.ProbeNextArea();
            if (area.Height > prevLayoutInfo.TrailingHeight + layoutInfo.TrailingHeight + Renderer.Tolerance)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The maximum number of elements that can be combined via keepwithnext and keeptogether
    /// </summary>
    private static readonly int MaxCombineElements = 10;

    bool NextElementsDontFit(int idx, Area remainingArea, XUnit previousMarginBottom)
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

            area = area.Lower(currLayoutInfo.ContentArea.Height);
            if (area.Height <= 0)
                return true;

            elementDistance = currLayoutInfo.MarginBottom;
        }

        return false;
    }

    bool NeedsEndingOnNextArea(int idx, Renderer renderer, Area remainingArea, bool isFirstOnPage)
    {
        var layoutInfo = renderer.RenderInfo.LayoutInfo;
        if (isFirstOnPage && layoutInfo.KeepTogether)
            return false;
        var formatInfo = renderer.RenderInfo.FormatInfo;

        if (!formatInfo.EndingIsComplete)
            return false;

        if (layoutInfo.KeepWithNext)
        {
            remainingArea = remainingArea.Lower(layoutInfo.ContentArea.Height);
            return NextElementsDontFit(idx, remainingArea, layoutInfo.MarginBottom);
        }

        return false;
    }

    DocumentRenderer documentRenderer;
    XGraphics gfx;
}
