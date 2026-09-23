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
using PdfPinata;
using PdfPinata.Drawing;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Internals;

namespace PinataLayout.Rendering;

/// <summary>
/// Represents a formatted document.
/// </summary>
public class FormattedDocument : IAreaProvider, IFootnoteAreaProvider
{
    private enum PagePosition
    {
        First,
        Odd,
        Even
    }

    private struct HeaderFooterPosition : IEquatable<HeaderFooterPosition>
    {
        internal HeaderFooterPosition(int sectionNr, PagePosition pagePosition)
        {
            this.sectionNr = sectionNr;
            this.pagePosition = pagePosition;
        }

        public bool Equals(HeaderFooterPosition other)
        {
            return sectionNr == other.sectionNr && pagePosition == other.pagePosition;
        }

        public override bool Equals(object obj)
        {
            return obj is HeaderFooterPosition hfp && Equals(hfp);
        }

        public override int GetHashCode()
        {
            return sectionNr.GetHashCode() ^ pagePosition.GetHashCode();
        }
        internal int sectionNr;
        internal PagePosition pagePosition;
    }

    internal FormattedDocument(Document document, DocumentRenderer documentRenderer)
    {
        this.document = document;
        this.documentRenderer = documentRenderer;
    }

    /// <summary>
    /// Formats the document by performing line breaks and page breaks.
    /// </summary>
    internal void Format(XGraphics graphics)
    {
        bookmarks = new Dictionary<string, FieldInfos.BookmarkInfo>();
        pageRenderInfos = new Dictionary<int, ArrayList>();
        pageInfos = new Dictionary<int, PageInfo>();
        pageFieldInfos = new Dictionary<int, FieldInfos>();
        formattedHeaders = new Dictionary<HeaderFooterPosition, FormattedHeaderFooter>();
        formattedFooters = new Dictionary<HeaderFooterPosition, FormattedHeaderFooter>();
        gfx = graphics;
        currentPage = 0;
        sectionNumber = 0;
        pageCount = 0;
        shownPageNumber = 0;
        documentRenderer.ProgressCompleted = 0;
        documentRenderer.ProgressMaximum = 0;
        if (documentRenderer.HasPrepareDocumentProgress)
        {
            foreach (Section section in document.Sections)
                documentRenderer.ProgressMaximum += section.Elements.Count;
        }
        foreach (Section section in document.Sections)
        {
            isNewSection = true;
            currentSection = section;
            ++sectionNumber;
            if (NeedsEmptyPage())
                InsertEmptyPage();

            var formatter = new TopDownFormatter(this, documentRenderer, section.Elements);
            formatter.FormatOnAreas(gfx, true);
            FillSectionPagesInfo();
            documentRenderer.ProgressCompleted += section.Elements.Count;
        }
        pageCount = currentPage;
        FillNumPagesInfo();
    }

    private PagePosition CurrentPagePosition
    {
        get
        {
            if (isNewSection)
                return PagePosition.First;
            if (currentPage % 2 == 0)
                return PagePosition.Even;
            return PagePosition.Odd;
        }
    }

    private void FormatHeadersFooters()
    {
        var headers = (HeadersFooters)currentSection.GetValue("Headers", GV.ReadOnly);
        if (headers != null)
        {
            var pagePos = CurrentPagePosition;
            var hfp = new HeaderFooterPosition(sectionNumber, pagePos);
            if (!formattedHeaders.ContainsKey(hfp))
                FormatHeader(hfp, ChooseHeaderFooter(headers, pagePos));
        }

        var footers = (HeadersFooters)currentSection.GetValue("Footers", GV.ReadOnly);
        if (footers != null)
        {
            var pagePos = CurrentPagePosition;
            var hfp = new HeaderFooterPosition(sectionNumber, pagePos);
            if (!formattedFooters.ContainsKey(hfp))
                FormatFooter(hfp, ChooseHeaderFooter(footers, pagePos));
        }
    }


    private void FormatHeader(HeaderFooterPosition hfp, HeaderFooter header)
    {
        if (header != null && !formattedHeaders.ContainsKey(hfp))
        {
            var formattedHeaderFooter = new FormattedHeaderFooter(header, documentRenderer, currentFieldInfos)
                {
                    ContentRect = GetHeaderArea(currentSection, currentPage)
                };
            formattedHeaderFooter.Format(gfx);
            formattedHeaders.Add(hfp, formattedHeaderFooter);
        }
    }


    private void FormatFooter(HeaderFooterPosition hfp, HeaderFooter footer)
    {
        if (footer != null && !formattedFooters.ContainsKey(hfp))
        {
            var formattedHeaderFooter = new FormattedHeaderFooter(footer, documentRenderer, currentFieldInfos)
                {
                    ContentRect = GetFooterArea(currentSection, currentPage)
                };
            formattedHeaderFooter.Format(gfx);
            formattedFooters.Add(hfp, formattedHeaderFooter);
        }
    }

    /// <summary>
    /// Fills the number pages information after formatting the document.
    /// </summary>
    private void FillNumPagesInfo()
    {
        for (var page = 1; page <= pageCount; ++page)
        {
            if (IsEmptyPage(page))
                continue;

            var fieldInfos = pageFieldInfos[page];
            fieldInfos.numPages = pageCount;
        }
    }

    /// <summary>
    /// Fills the section pages information after formatting a section.
    /// </summary>
    private void FillSectionPagesInfo()
    {
        for (var page = currentPage; page > 0; --page)
        {
            if (IsEmptyPage(page))
                continue;

            var fieldInfos = pageFieldInfos[page];
            if (fieldInfos.section != sectionNumber)
                break;

            fieldInfos.sectionPages = sectionPages;
        }
    }

    private Rectangle CalcContentRect(int page)
    {
        var pageSetup = currentSection.PageSetup;
        XUnit width;
        if (pageSetup.Orientation == Orientation.Portrait)
            width = pageSetup.PageWidth.Point;
        else
            width = pageSetup.PageHeight.Point;

        width -= pageSetup.RightMargin.Point;
        width -= pageSetup.LeftMargin.Point;

        XUnit height;
        if (pageSetup.Orientation == Orientation.Portrait)
            height = pageSetup.PageHeight.Point;
        else
            height = pageSetup.PageWidth.Point;

        height -= pageSetup.TopMargin.Point;
        height -= pageSetup.BottomMargin.Point;
        XUnit x;
        XUnit y;
        y = pageSetup.TopMargin.Point;
        if (pageSetup.MirrorMargins)
            x = page % 2 == 0 ? pageSetup.RightMargin.Point : pageSetup.LeftMargin.Point;
        else
            x = pageSetup.LeftMargin.Point;
        return new Rectangle(x, y, width, height);
    }

    /// <summary>
    /// Gets the rendering information for the page content.
    /// </summary>
    /// <param name="page">The page to render.</param>
    /// <returns>Rendering information for the page content.</returns>
    internal RenderInfo[] GetRenderInfos(int page)
    {
        if (pageRenderInfos.TryGetValue(page, out var infos))
        {
            // Not ToArray(Type): it builds the array type at run time, which carries
            // RequiresDynamicCode and an AOT compiler cannot always have code for.
            var result = new RenderInfo[infos.Count];
            infos.CopyTo(result);
            return result;
        }
        return null;
    }
    private Dictionary<int, ArrayList> pageRenderInfos;

    /// <summary>
    /// Gets a formatted headerfooter object for header of the given page.
    /// </summary>
    /// <param name="page">The page the header shall appear on.</param>
    /// <returns>The required header, null if none exists to render.</returns>
    internal FormattedHeaderFooter GetFormattedHeader(int page)
    {
        var pagePos = page % 2 == 0 ? PagePosition.Even : PagePosition.Odd;

        var fieldInfos = pageFieldInfos[page];

        if (page == 1)
            pagePos = PagePosition.First;
        else //page > 1
        {
            if (IsEmptyPage(page - 1)) // these empty pages only occur between sections.
                pagePos = PagePosition.First;
            else
            {
                var prevFieldInfos = pageFieldInfos[page - 1];
                if (fieldInfos.section != prevFieldInfos.section)
                    pagePos = PagePosition.First;
            }
        }
        var hfp = new HeaderFooterPosition(fieldInfos.section, pagePos);
        if (formattedHeaders.TryGetValue(hfp, out var header))
            return header;
        return null;
    }

    /// <summary>
    /// Gets a formatted headerfooter object for footer of the given page.
    /// </summary>
    /// <param name="page">The page the footer shall appear on.</param>
    /// <returns>The required footer, null if none exists to render.</returns>
    internal FormattedHeaderFooter GetFormattedFooter(int page)
    {
        var pagePos = page % 2 == 0 ? PagePosition.Even : PagePosition.Odd;

        var fieldInfos = pageFieldInfos[page];

        if (page == 1)
            pagePos = PagePosition.First;

        else //page > 1
        {
            if (IsEmptyPage(page - 1)) // these empty pages only occur between sections.
                pagePos = PagePosition.First;
            else
            {
                var prevFieldInfos = pageFieldInfos[page - 1];
                if (fieldInfos.section != prevFieldInfos.section)
                    pagePos = PagePosition.First;
            }
        }
        var hfp = new HeaderFooterPosition(fieldInfos.section, pagePos);
        if (formattedFooters.TryGetValue(hfp, out var footer))
            return footer;
        return null;
    }

    private static Rectangle GetHeaderArea(Section section, int page)
    {
        var pageSetup = section.PageSetup;
        XUnit xPos;
        if (pageSetup.MirrorMargins && page % 2 == 0)
            xPos = pageSetup.RightMargin.Point;
        else
            xPos = pageSetup.LeftMargin.Point;

        XUnit width = pageSetup.Orientation == Orientation.Portrait ? pageSetup.PageWidth.Point : pageSetup.PageHeight.Point;

        width -= pageSetup.LeftMargin + pageSetup.RightMargin;

        XUnit yPos = pageSetup.HeaderDistance.Point;
        XUnit height = pageSetup.TopMargin - pageSetup.HeaderDistance;
        return new Rectangle(xPos, yPos, width, height);
    }

    internal Rectangle GetHeaderArea(int page)
    {
        var fieldInfos = pageFieldInfos[page];
        var section = document.Sections[fieldInfos.section - 1];
        return GetHeaderArea(section, page);
    }

    internal Rectangle GetFooterArea(int page)
    {
        var fieldInfos = pageFieldInfos[page];
        var section = document.Sections[fieldInfos.section - 1];
        return GetFooterArea(section, page);
    }

    private static Rectangle GetFooterArea(Section section, int page)
    {
        var pageSetup = section.PageSetup;
        XUnit xPos;
        if (pageSetup.MirrorMargins && page % 2 == 0)
            xPos = pageSetup.RightMargin.Point;
        else
            xPos = pageSetup.LeftMargin.Point;

        XUnit width;
        if (pageSetup.Orientation == Orientation.Portrait)
            width = pageSetup.PageWidth.Point;
        else
            width = pageSetup.PageHeight.Point;
        width -= pageSetup.LeftMargin + pageSetup.RightMargin;
        XUnit yPos;
        if (pageSetup.Orientation == Orientation.Portrait)
            yPos = pageSetup.PageHeight.Point;
        else
            yPos = pageSetup.PageWidth.Point;

        yPos -= pageSetup.BottomMargin.Point;
        XUnit height = pageSetup.BottomMargin - pageSetup.FooterDistance;
        return new Rectangle(xPos, yPos, width, height);
    }


    private HeaderFooter ChooseHeaderFooter(HeadersFooters hfs, PagePosition pagePos)
    {
        if (hfs == null)
            return null;

        var pageSetup = currentSection.PageSetup;

        if (pagePos == PagePosition.First)
        {
            if (pageSetup.DifferentFirstPageHeaderFooter)
                return (HeaderFooter)hfs.GetValue("FirstPage", GV.ReadOnly);
        }
        if (pagePos == PagePosition.Even || currentPage % 2 == 0)
        {
            if (pageSetup.OddAndEvenPagesHeaderFooter)
                return (HeaderFooter)hfs.GetValue("EvenPage", GV.ReadOnly);
        }
        return (HeaderFooter)hfs.GetValue("Primary", GV.ReadOnly);
    }

    /// <summary>
    /// Gets the number of pages of the document.
    /// </summary>
    public int PageCount => pageCount;

    private int pageCount;


    /// <summary>
    /// Gets information about the specified page.
    /// </summary>
    /// <param name="page">The page the information is asked for.</param>
    /// <returns>The page information.</returns>
    public PageInfo GetPageInfo(int page)
    {
        if (page < 1 || page > pageCount)
            throw new ArgumentOutOfRangeException(nameof(page));

        return pageInfos[page];
    }


    /// <summary>
    /// Returns the absolute Y position of the last item on the last page in points.
    /// Must be called after rendering the document.
    /// </summary>
    /// <returns>The current Y position at the end of the last page.</returns>
    public double GetCurrentPinataLayoutPosition()
    {
        var RenderInfos = GetRenderInfos(PageCount);
        var r = RenderInfos[^1];
        return r.LayoutInfo.ContentArea.Y + r.LayoutInfo.ContentArea.Height;
    }

    #region IAreaProvider Members

    Area IAreaProvider.GetNextArea()
    {
        if (isNewSection)
            sectionPages = 0;

        ++currentPage;
        ++shownPageNumber;
        ++sectionPages;

        // A fresh page has nothing set aside on it yet. Pages are filled in order and never
        // revisited, so this is the whole of the bookkeeping the reservation needs.
        reservedForFootnotes = 0;

        InitFieldInfos();
        FormatHeadersFooters();
        isNewSection = false;

        // Kept, because the render pass needs it and cannot work it out: CalcContentRect reads
        // currentSection, which means something only while the document is being formatted.
        var contentRect = CalcContentRect(currentPage);
        pageContentRects[currentPage] = contentRect;
        return contentRect;
    }
    private int currentPage;

    private readonly Dictionary<int, Rectangle> pageContentRects = new Dictionary<int, Rectangle>();

    /// <summary>
    /// The rectangle the page's body text was laid out in - the page less its margins.
    /// </summary>
    internal Rectangle ContentRectOf(int page) =>
        pageContentRects.TryGetValue(page, out var rect) ? rect : CalcContentRect(page);

    /// <summary>
    /// How far down the page the last thing laid out on it reaches, or zero for a page with
    /// nothing on it.
    /// </summary>
    /// <remarks>
    /// This is what <see cref="FootnoteLocation.BeneathText"/> means by "the text": not the bottom
    /// of the area the text could have used, but the bottom of what it actually used.
    /// </remarks>
    internal XUnit BottomOfContentOn(int page)
    {
        if (!pageRenderInfos.TryGetValue(page, out var renderInfos))
            return 0;

        XUnit bottom = 0;
        foreach (RenderInfo renderInfo in renderInfos)
        {
            var contentArea = renderInfo.LayoutInfo.ContentArea;
            XUnit candidate = contentArea.Y + contentArea.Height;
            if (candidate > bottom)
                bottom = candidate;
        }

        return bottom;
    }

    /// <summary>
    /// Lays out the footnotes an element carries and sets room aside for them at the foot of the
    /// page being filled, returning how much more room that took than was already set aside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called for every element, and answers zero for almost all of them. The height is worked out
    /// from every note on the page rather than from the ones just found, so the separator band is
    /// counted once however many notes there are, and so re-registering a note the formatter is
    /// trying for a second time does not charge for it twice.
    /// </para>
    /// <para>
    /// The delta is never negative. Notes are only ever added to the page being filled - one that
    /// moves goes to the next page and is registered against that - so the reservation only grows,
    /// and a negative would mean giving back room an element has already been laid out in.
    /// </para>
    /// </remarks>
    XUnit IFootnoteAreaProvider.ReserveFootnotes(DocumentObject element, XUnit width, XGraphics graphics)
    {
        var notes = Footnotes.In(element);
        if (notes.Count == 0)
            return 0;

        var registry = documentRenderer.Footnotes;
        foreach (var note in notes)
        {
            var formatted =
                new FormattedFootnote(documentRenderer, note, currentFieldInfos, width);

            // Registered before it is formatted, not after. Formatting measures the mark to size
            // the gutter it hangs in, and a generated mark does not exist until the note has an
            // entry to take its ordinal from - so formatting first measured an empty string, and
            // every note fell back to the bare minimum gutter. A mark wider than that, which "10"
            // and "viii" both are, was then written over the note's first word. Place overwrites,
            // so registering first is safe on the second and later attempts at the same note.
            registry.Place(note, sectionNumber, currentPage, formatted);
            formatted.Format(graphics);
        }

        var required = FootnoteBlockHeight(currentPage);
        XUnit delta = required - reservedForFootnotes;
        if (delta <= 0)
            return 0;

        reservedForFootnotes = required;
        return delta;
    }

    /// <summary>
    /// How tall the whole footnote block on a page is - the separator band and every note under it
    /// - or zero where the page carries none.
    /// </summary>
    internal XUnit FootnoteBlockHeight(int page)
    {
        var registry = documentRenderer.Footnotes;
        var notes = registry.On(page);
        if (notes.Count == 0)
            return 0;

        var height = FootnoteSeparatorBand;
        foreach (var note in notes)
        {
            var formatted = registry.FormattedOf(note);
            if (formatted != null)
                height += formatted.ContentHeight;
        }

        return height;
    }

    /// <summary>
    /// The space above the notes: a gap, the separator rule, and a gap under it.
    /// </summary>
    /// <remarks>
    /// Fixed rather than settable. The document object model has no property for the separator and
    /// this work deliberately adds none - see docs/specs/PinataLayout-footnotes.md - because a rule at
    /// hairline weight over a third of the column is what Word and LaTeX both draw and what a
    /// reader expects to see.
    /// </remarks>
    internal static readonly XUnit FootnoteSeparatorBand = XUnit.FromPoint(11);

    /// <summary>How far down the separator band the rule itself sits.</summary>
    internal static readonly XUnit FootnoteSeparatorOffset = XUnit.FromPoint(6);

    private XUnit reservedForFootnotes;

    Area IAreaProvider.ProbeNextArea()
    {
        return CalcContentRect(currentPage + 1);
    }

    private void InitFieldInfos()
    {
        currentFieldInfos = new FieldInfos(bookmarks);
        currentFieldInfos.pyhsicalPageNr = currentPage;
        currentFieldInfos.section = sectionNumber;
        // A landscape page is the page setup turned on its side, the same way CalcContentRect
        // reads it. This is what lets a bookmark record where up the page it sits.
        var pageSetup = currentSection.PageSetup;
        currentFieldInfos.pageHeight = pageSetup.Orientation == Orientation.Portrait
            ? pageSetup.PageHeight.Point
            : pageSetup.PageWidth.Point;

        if (isNewSection && !currentSection.PageSetup.IsNull("StartingNumber"))
            shownPageNumber = currentSection.PageSetup.StartingNumber;

        currentFieldInfos.displayPageNr = shownPageNumber;
    }

    void IAreaProvider.StoreRenderInfos(ArrayList renderInfos)
    {
        pageRenderInfos.Add(currentPage, renderInfos);
        var pageSize = CalcPageSize(currentSection.PageSetup);
        var pageOrientation = CalcPageOrientation();
        var pageInfo = new PageInfo(pageSize.Width, pageSize.Height, pageOrientation);
        pageInfos.Add(currentPage, pageInfo);
        pageFieldInfos.Add(currentPage, currentFieldInfos);
    }

    private PageOrientation CalcPageOrientation()
    {
        var pageOrientation = PageOrientation.Portrait;
        if (currentSection.PageSetup.Orientation == Orientation.Landscape)
            pageOrientation = PageOrientation.Landscape;

        return pageOrientation;
    }

    private static XSize CalcPageSize(PageSetup pageSetup)
    {
        return new XSize(pageSetup.PageWidth.Point, pageSetup.PageHeight.Point);
    }

    bool IAreaProvider.PositionHorizontally(LayoutInfo layoutInfo)
    {
        switch (layoutInfo.HorizontalReference)
        {
            case HorizontalReference.PageMargin:
            case HorizontalReference.AreaBoundary:
                return PositionHorizontallyToMargin(layoutInfo);

            case HorizontalReference.Page:
                return PositionHorizontallyToPage(layoutInfo);
        }
        return false;
    }

    /// <summary>
    /// Gets the alignment depending on the currentPage for the alignments "Outside" and "Inside".
    /// </summary>
    /// <param name="alignment">The original alignment</param>
    /// <returns>the alignment depending on the currentPage for the alignments "Outside" and "Inside"</returns>
    private ElementAlignment GetCurrentAlignment(ElementAlignment alignment)
    {
        var align = alignment;

        if (align == ElementAlignment.Inside)
        {
            if (currentPage % 2 == 0)
                align = ElementAlignment.Far;
            else
                align = ElementAlignment.Near;
        }
        else if (align == ElementAlignment.Outside)
        {
            if (currentPage % 2 == 0)
                align = ElementAlignment.Near;
            else
                align = ElementAlignment.Far;
        }
        return align;
    }

    private bool PositionHorizontallyToMargin(LayoutInfo layoutInfo)
    {
        var rect = CalcContentRect(currentPage);
        var align = GetCurrentAlignment(layoutInfo.HorizontalAlignment);


        switch (align)
        {
            case ElementAlignment.Near:
                if (layoutInfo.Left != 0)
                {
                    layoutInfo.ContentArea.X += layoutInfo.Left;
                    return true;
                }
                if (layoutInfo.MarginLeft != 0)
                {
                    layoutInfo.ContentArea.X += layoutInfo.MarginLeft;
                    return true;
                }
                return false;

            case ElementAlignment.Far:
                XUnit xPos = rect.X + rect.Width;
                xPos -= layoutInfo.ContentArea.Width;
                xPos -= layoutInfo.MarginRight;
                layoutInfo.ContentArea.X = xPos;
                return true;

            case ElementAlignment.Center:
                xPos = rect.Width;
                xPos -= layoutInfo.ContentArea.Width;
                xPos = rect.X + xPos / 2;
                layoutInfo.ContentArea.X = xPos;
                return true;
        }
        return false;
    }

    private bool PositionHorizontallyToPage(LayoutInfo layoutInfo)
    {
        XUnit xPos;
        var align = GetCurrentAlignment(layoutInfo.HorizontalAlignment);
        switch (align)
        {
            case ElementAlignment.Near:
                // Allow negative offsets (supporting "Anschnitt" for images)
                if (layoutInfo.HorizontalReference == HorizontalReference.Page ||
                    layoutInfo.HorizontalReference == HorizontalReference.PageMargin)
                    xPos = layoutInfo.MarginLeft; // Ignore layoutInfo.Left if absolute position is specified
                else
                    xPos = Math.Max(layoutInfo.MarginLeft, layoutInfo.Left);

                layoutInfo.ContentArea.X = xPos;
                break;

            case ElementAlignment.Far:
                xPos = currentSection.PageSetup.PageWidth.Point;
                xPos -= layoutInfo.ContentArea.Width;
                xPos -= layoutInfo.MarginRight;
                layoutInfo.ContentArea.X = xPos;
                break;

            case ElementAlignment.Center:
                xPos = currentSection.PageSetup.PageWidth.Point;
                xPos -= layoutInfo.ContentArea.Width;
                xPos /= 2;
                layoutInfo.ContentArea.X = xPos;
                break;
        }
        return true;
    }

    private bool PositionVerticallyToMargin(LayoutInfo layoutInfo)
    {
        var rect = CalcContentRect(currentPage);
        XUnit yPos;
        switch (layoutInfo.VerticalAlignment)
        {
            case ElementAlignment.Near:
                yPos = rect.Y;
                if (layoutInfo.Top == 0)
                    yPos += layoutInfo.MarginTop;
                else
                    yPos += layoutInfo.Top;

                layoutInfo.ContentArea.Y = yPos;
                break;

            case ElementAlignment.Far:
                yPos = rect.Y + rect.Height;
                yPos -= layoutInfo.ContentArea.Height;
                yPos -= layoutInfo.MarginBottom;
                layoutInfo.ContentArea.Y = yPos;
                break;

            case ElementAlignment.Center:
                yPos = rect.Height;
                yPos -= layoutInfo.ContentArea.Height;
                yPos = rect.Y + yPos / 2;
                layoutInfo.ContentArea.Y = yPos;
                break;
        }
        return true;
    }

    private bool NeedsEmptyPage()
    {
        var nextPage = currentPage + 1;
        var pageSetup = currentSection.PageSetup;
        var startOnEvenPage = pageSetup.SectionStart == BreakType.BreakEvenPage;
        var startOnOddPage = pageSetup.SectionStart == BreakType.BreakOddPage;

        if (startOnOddPage)
            return nextPage % 2 == 0;
        if (startOnEvenPage)
            return nextPage % 2 == 1;

        return false;
    }

    private void InsertEmptyPage()
    {
        ++currentPage;
        ++shownPageNumber;
        emptyPages.Add(currentPage, null);

        var pageSize = CalcPageSize(currentSection.PageSetup);
        var pageOrientation = CalcPageOrientation();
        var pageInfo = new PageInfo(pageSize.Width, pageSize.Height, pageOrientation);
        pageInfos.Add(currentPage, pageInfo);
    }

    private bool PositionVerticallyToPage(LayoutInfo layoutInfo)
    {
        XUnit yPos;
        switch (layoutInfo.VerticalAlignment)
        {
            case ElementAlignment.Near:
                yPos = Math.Max(layoutInfo.MarginTop, layoutInfo.Top);
                layoutInfo.ContentArea.Y = yPos;
                break;

            case ElementAlignment.Far:
                yPos = currentSection.PageSetup.PageHeight.Point;
                yPos -= layoutInfo.ContentArea.Height;
                yPos -= layoutInfo.MarginBottom;
                layoutInfo.ContentArea.Y = yPos;
                break;

            case ElementAlignment.Center:
                yPos = currentSection.PageSetup.PageHeight.Point;
                yPos -= layoutInfo.ContentArea.Height;
                yPos /= 2;
                layoutInfo.ContentArea.Y = yPos;
                break;
        }
        return true;
    }

    bool IAreaProvider.PositionVertically(LayoutInfo layoutInfo)
    {
        switch (layoutInfo.VerticalReference)
        {
            case VerticalReference.PreviousElement:
                return false;

            case VerticalReference.AreaBoundary:
            case VerticalReference.PageMargin:
                return PositionVerticallyToMargin(layoutInfo);

            case VerticalReference.Page:
                return PositionVerticallyToPage(layoutInfo);
        }
        return false;
    }

    internal FieldInfos GetFieldInfos(int page)
    {
        return pageFieldInfos[page];
    }

    FieldInfos IAreaProvider.AreaFieldInfos => currentFieldInfos;

    bool IAreaProvider.IsAreaBreakBefore(LayoutInfo layoutInfo)
    {
        return layoutInfo.PageBreakBefore;
    }

    internal bool IsEmptyPage(int page)
    {
        return emptyPages.ContainsKey(page);
    }
    #endregion

    private Dictionary<string, FieldInfos.BookmarkInfo> bookmarks;
    private int sectionPages;
    private int shownPageNumber;
    private int sectionNumber;
    private Section currentSection;
    private bool isNewSection;
    private FieldInfos currentFieldInfos;
    private Dictionary<int, FieldInfos> pageFieldInfos;
    private Dictionary<HeaderFooterPosition, FormattedHeaderFooter> formattedHeaders;
    private Dictionary<HeaderFooterPosition, FormattedHeaderFooter> formattedFooters;
    private DocumentRenderer documentRenderer;
    private XGraphics gfx;
    private Dictionary<int, PageInfo> pageInfos;
    private Dictionary<int, object> emptyPages = new Dictionary<int, object>();
    private Document document;
}
