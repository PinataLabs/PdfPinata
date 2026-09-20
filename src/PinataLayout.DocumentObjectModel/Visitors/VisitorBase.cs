#region Copyright

//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@PdfPinata.com)
//   Klaus Potzesny (mailto:Klaus.Potzesny@PdfPinata.com)
//   David Stephensen (mailto:David.Stephensen@PdfPinata.com)
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

using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;

namespace PinataLayout.DocumentObjectModel.Visitors;

/// <summary>
/// Walks a document and flattens the formatting it finds: every value a document object leaves
/// unset is filled in from the style or parent object it would have inherited it from, so that
/// what comes out the other side can be rendered without consulting anything above it.
/// </summary>
/// <remarks>
/// The <c>Flatten…</c> methods below all take the object being flattened and the reference it
/// inherits from, and copy across only the values that are still null. A few are empty: they mark
/// a place where a type has nothing to inherit today but is visited alongside those that do.
/// </remarks>
public abstract class VisitorBase : DocumentObjectVisitor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisitorBase"/> class.
    /// </summary>
    public VisitorBase()
    {
    }

    /// <summary>
    /// Visits a document object, and through it everything below it. Objects that cannot be
    /// visited are ignored.
    /// </summary>
    public override void Visit(DocumentObject documentObject)
    {
        var visitable = documentObject as IVisitable;
        if (visitable != null)
            visitable.AcceptVisitor(this, true);
    }

    /// <summary>
    /// Fills in every simple-valued [DV] member of <paramref name="target"/> left unset from
    /// <paramref name="reference"/> - the rule behind every Flatten* method in this class: if mine is
    /// null, take the reference's. Both must be the same DOM type, or a member the one declares and
    /// the other does not would silently fail to flatten.
    /// </summary>
    /// <remarks>
    /// Not used for a member whose copy needs more than that: a shared object needing its own clone
    /// and a reparented copy, a border needing per-side logic, and so on - those stay hand-written
    /// below, and are exactly the cases this rule does not cover. A member is read through
    /// <see cref="GV.ReadOnly"/> rather than <see cref="GV.GetNull"/>, because a Leaf member's boxed
    /// default is not null and would overwrite an unset target with it; skipping the copy whenever
    /// the reference is itself unset gets the same "left alone" result without ever reading that
    /// default at all.
    /// </remarks>
    protected static void FlattenSimpleValues(DocumentObject target, DocumentObject reference)
    {
        foreach (var vd in target.Meta.ValueDescriptors)
        {
            if (vd.IsSimpleValue && vd.IsNull(target) && !vd.IsNull(reference))
                vd.SetValue(target, vd.GetValue(reference, GV.ReadOnly));
        }
    }

    /// <summary>Fills in every paragraph format value left unset from <paramref name="refFormat"/>.</summary>
    protected void FlattenParagraphFormat(ParagraphFormat format, ParagraphFormat refFormat)
    {
        FlattenSimpleValues(format, refFormat);

        if (format.font == null)
        {
            if (refFormat.font != null)
            {
                //The font is cloned here to avoid parent problems
                format.font = refFormat.font.Clone();
                format.font.parent = format;
            }
        }
        else if (refFormat.font != null)
            FlattenFont(format.font, refFormat.font);

        if (format.shading == null)
        {
            if (refFormat.shading != null)
            {
                format.shading = refFormat.shading.Clone();
                format.shading.parent = format;
            }
            //        format.shading = refFormat.shading;
        }
        else if (refFormat.shading != null)
            FlattenShading(format.shading, refFormat.shading);

        // Copied rather than shared, as the font and the shading above are: a format is flattened
        // more than once, and the second pass would otherwise write what it inherits back into the
        // format it inherited the borders from.
        if (format.borders == null)
            format.borders = InheritedBorders(refFormat.borders, format);
        else if (refFormat.borders != null)
            FlattenBorders(format.borders, refFormat.borders);

        //      if (format.tabStops == null)
        //        format.tabStops = refFormat.tabStops;
        if (refFormat.tabStops != null)
            FlattenTabStops(format.TabStops, refFormat.tabStops);

        if (refFormat.listInfo != null)
            FlattenListInfo(format.ListInfo, refFormat.listInfo);
    }

#pragma warning disable CA1822 // Protected on an unsealed public visitor: making it static would change the public API.
    /// <summary>Fills in every list value left unset from <paramref name="refListInfo"/>.</summary>
    protected void FlattenListInfo(ListInfo listInfo, ListInfo refListInfo) =>
        FlattenSimpleValues(listInfo, refListInfo);

    /// <summary>Fills in every font value left unset from <paramref name="refFont"/>.</summary>
    protected void FlattenFont(Font font, Font refFont) =>
        FlattenSimpleValues(font, refFont);

    /// <summary>Fills in every shading value left unset from <paramref name="refShading"/>.</summary>
    protected void FlattenShading(Shading shading, Shading refShading) =>
        FlattenSimpleValues(shading, refShading);
#pragma warning restore CA1822

#pragma warning disable CA1822 // Protected on an unsealed public visitor: making it static would change the public API.
    /// <summary>
    /// Returns a border with every value left unset filled in from the <see cref="Borders"/> collection
    /// holding it, creating the border first if there is none.
    /// </summary>
    protected Border FlattenedBorderFromBorders(Border border, Borders parentBorders)
    {
        if (border == null)
            border = new Border(parentBorders);

        if (border.visible == null)
            border.visible = parentBorders.visible;

        if (border.style == null)
            border.style = parentBorders.style;

        if (border.width.IsNull)
            border.width = parentBorders.width;

        if (border.color.IsNull)
            border.color = parentBorders.color;

        return border;
    }

    /// <summary>
    /// Gives an object its own copy of the borders it inherits from the object that contains it.
    /// Handing out the container's own instance instead would leave every cell of a row, and every
    /// row of a table, holding one Borders object between them, so the next thing flattened onto
    /// any one of them would appear on all of them.
    /// </summary>
    protected Borders InheritedBorders(Borders refBorders, DocumentObject owner)
    {
        if (refBorders == null)
            return null;

        var borders = refBorders.Clone();
        borders.parent = owner;
        return borders;
    }

    /// <summary>
    /// Gives an object its own copy of the shading it inherits from the object that contains it,
    /// for the same reason <see cref="InheritedBorders"/> does so for borders.
    /// </summary>
    protected Shading InheritedShading(Shading refShading, DocumentObject owner)
    {
        if (refShading == null)
            return null;

        var shading = refShading.Clone();
        shading.parent = owner;
        return shading;
    }
#pragma warning restore CA1822

    /// <summary>Fills in every border value left unset from <paramref name="refBorders"/>.</summary>
    protected void FlattenBorders(Borders borders, Borders refBorders)
    {
        FlattenSimpleValues(borders, refBorders);

        if (refBorders.left != null)
        {
            FlattenBorder(borders.Left, refBorders.left);
            FlattenedBorderFromBorders(borders.left, borders);
        }

        if (refBorders.right != null)
        {
            FlattenBorder(borders.Right, refBorders.right);
            FlattenedBorderFromBorders(borders.right, borders);
        }

        if (refBorders.top != null)
        {
            FlattenBorder(borders.Top, refBorders.top);
            FlattenedBorderFromBorders(borders.top, borders);
        }

        if (refBorders.bottom != null)
        {
            FlattenBorder(borders.Bottom, refBorders.bottom);
            FlattenedBorderFromBorders(borders.bottom, borders);
        }
    }

#pragma warning disable CA1822 // Protected on an unsealed public visitor: making it static would change the public API.
    /// <summary>Fills in every value of a single border left unset from <paramref name="refBorder"/>.</summary>
    protected void FlattenBorder(Border border, Border refBorder) =>
        FlattenSimpleValues(border, refBorder);

    /// <summary>
    /// Takes on the inherited tab stops when none have been set here, and drops any marked for removal.
    /// </summary>
    protected void FlattenTabStops(TabStops tabStops, TabStops refTabStops)
    {
        if (!tabStops.fClearAll)
        {
            foreach (TabStop refTabStop in refTabStops)
            {
                if (tabStops.GetTabStopAt(refTabStop.Position) == null && refTabStop.AddTab)
                    tabStops.AddTabStop(refTabStop.Position, refTabStop.Alignment, refTabStop.Leader);
            }
        }

        for (var i = 0; i < tabStops.Count; i++)
        {
            var tabStop = tabStops[i];
            if (!tabStop.AddTab)
                tabStops.RemoveObjectAt(i);
        }

        // The TabStopCollection is complete as it is now.
        // Therefore, it must not inherit anything, i.e.:
        tabStops.fClearAll = true;
    }

    /// <summary>Fills in every page setup value left unset from <paramref name="refPageSetup"/>.</summary>
    protected void FlattenPageSetup(PageSetup pageSetup, PageSetup refPageSetup)
    {
        if (pageSetup.pageWidth.IsNull && pageSetup.pageHeight.IsNull)
        {
            if (pageSetup.pageFormat == null)
            {
                pageSetup.pageWidth = refPageSetup.pageWidth;
                pageSetup.pageHeight = refPageSetup.pageHeight;
                pageSetup.pageFormat = refPageSetup.pageFormat;
            }
            else
                PageSetup.GetPageSize(pageSetup.PageFormat, out pageSetup.pageWidth, out pageSetup.pageHeight);
        }
        else
        {
            // Fill in the one that is missing. The two arms used to fill in the other one: a
            // section given a height and no width had its height overwritten and its width left
            // unset, so the page came out no width at all - and the length the caller did set was
            // the one that was thrown away.
            if (pageSetup.pageWidth.IsNull)
            {
                if (pageSetup.pageFormat == null)
                    pageSetup.pageWidth = refPageSetup.pageWidth;
                else
                    PageSetup.GetPageSize(pageSetup.PageFormat, out pageSetup.pageWidth, out _);
            }
            else if (pageSetup.pageHeight.IsNull)
            {
                if (pageSetup.pageFormat == null)
                    pageSetup.pageHeight = refPageSetup.pageHeight;
                else
                    PageSetup.GetPageSize(pageSetup.PageFormat, out _, out pageSetup.pageHeight);
            }
        }

        //      if (pageSetup.pageWidth.IsNull)
        //        pageSetup.pageWidth = refPageSetup.pageWidth;
        //      if (pageSetup.pageHeight.IsNull)
        //        pageSetup.pageHeight = refPageSetup.pageHeight;
        //      if (pageSetup.pageFormat == null)
        //        pageSetup.pageFormat = refPageSetup.pageFormat;
        if (pageSetup.sectionStart == null)
            pageSetup.sectionStart = refPageSetup.sectionStart;
        if (pageSetup.orientation == null)
            pageSetup.orientation = refPageSetup.orientation;
        if (pageSetup.topMargin.IsNull)
            pageSetup.topMargin = refPageSetup.topMargin;
        if (pageSetup.bottomMargin.IsNull)
            pageSetup.bottomMargin = refPageSetup.bottomMargin;
        if (pageSetup.leftMargin.IsNull)
            pageSetup.leftMargin = refPageSetup.leftMargin;
        if (pageSetup.rightMargin.IsNull)
            pageSetup.rightMargin = refPageSetup.rightMargin;
        if (pageSetup.headerDistance.IsNull)
            pageSetup.headerDistance = refPageSetup.headerDistance;
        if (pageSetup.footerDistance.IsNull)
            pageSetup.footerDistance = refPageSetup.footerDistance;
        if (pageSetup.oddAndEvenPagesHeaderFooter == null)
            pageSetup.oddAndEvenPagesHeaderFooter = refPageSetup.oddAndEvenPagesHeaderFooter;
        if (pageSetup.differentFirstPageHeaderFooter == null)
            pageSetup.differentFirstPageHeaderFooter = refPageSetup.differentFirstPageHeaderFooter;
        if (pageSetup.mirrorMargins == null)
            pageSetup.mirrorMargins = refPageSetup.mirrorMargins;
        if (pageSetup.horizontalPageBreak == null)
            pageSetup.horizontalPageBreak = refPageSetup.horizontalPageBreak;
    }

    /// <summary>
    /// Flattens a header or footer. Empty: a header or footer inherits nothing of its own, its
    /// paragraphs being flattened against their styles like any others.
    /// </summary>
    protected void FlattenHeaderFooter(HeaderFooter headerFooter, bool isHeader)
    {
    }

    /// <summary>Flattens a fill format. Empty: a fill format has nothing to inherit.</summary>
    protected void FlattenFillFormat(FillFormat fillFormat)
    {
    }

    /// <summary>Fills in the line width when it is unset and <paramref name="refLineFormat"/> has one.</summary>
    protected void FlattenLineFormat(LineFormat lineFormat, LineFormat refLineFormat)
    {
        if (refLineFormat != null)
        {
            if (lineFormat.width.IsNull)
                lineFormat.width = refLineFormat.width;
        }
    }
#pragma warning restore CA1822

    /// <summary>Flattens a chart axis. Empty: an axis takes its formatting from the chart it is drawn in.</summary>
    protected void FlattenAxis(Axis axis)
    {
        if (axis == null)
            return;

        var refLineFormat = new LineFormat();
        refLineFormat.width = 0.15;
        if ((axis.hasMajorGridlines ?? false) && axis.majorGridlines != null)
            FlattenLineFormat(axis.majorGridlines.lineFormat, refLineFormat);
        if ((axis.hasMinorGridlines ?? false) && axis.minorGridlines != null)
            FlattenLineFormat(axis.minorGridlines.lineFormat, refLineFormat);

        refLineFormat.width = 0.4;
        if (axis.lineFormat != null)
            FlattenLineFormat(axis.lineFormat, refLineFormat);

        //      axis.majorTick;
        //      axis.majorTickMark;
        //      axis.minorTick;
        //      axis.minorTickMark;

        //      axis.maximumScale;
        //      axis.minimumScale;

        //      axis.tickLabels;
        //      axis.title;
    }

#pragma warning disable CA1822 // Protected on an unsealed public visitor: making it static would change the public API.
    /// <summary>Flattens a plot area. Empty: a plot area has nothing to inherit.</summary>
    protected void FlattenPlotArea(PlotArea plotArea)
    {
    }

    /// <summary>Flattens a data label. Empty: a data label has nothing to inherit.</summary>
    protected void FlattenDataLabel(DataLabel dataLabel)
    {
    }
#pragma warning restore CA1822


    #region Chart

    internal override void VisitChart(Chart chart)
    {
        var document = chart.Document;
        if (chart.style == null)
            chart.style = Style.DefaultParagraphName;
        var style = document.Styles[(chart.style ?? "")];
        if (chart.format == null)
        {
            chart.format = style.paragraphFormat.Clone();
            chart.format.parent = chart;
        }
        else
            FlattenParagraphFormat(chart.format, style.paragraphFormat);


        FlattenLineFormat(chart.lineFormat, null);
        FlattenFillFormat(chart.fillFormat);

        FlattenAxis(chart.xAxis);
        FlattenAxis(chart.yAxis);
        FlattenAxis(chart.zAxis);

        FlattenPlotArea(chart.plotArea);

        //      if ((this.hasDataLabel ?? false))
        FlattenDataLabel(chart.dataLabel);
    }

    #endregion

    #region Document

    internal override void VisitDocument(Document document)
    {
    }

    internal override void VisitDocumentElements(DocumentElements elements)
    {
    }

    #endregion

    #region Format

    internal override void VisitStyle(Style style)
    {
        var baseStyle = style.GetBaseStyle();
        if (baseStyle != null && baseStyle.paragraphFormat != null)
        {
            if (style.paragraphFormat == null)
                style.paragraphFormat = baseStyle.paragraphFormat;
            else
                FlattenParagraphFormat(style.paragraphFormat, baseStyle.paragraphFormat);
        }
    }

    internal override void VisitStyles(Styles styles)
    {
    }

    #endregion

    #region Paragraph

    internal override void VisitFootnote(Footnote footnote)
    {
        var document = footnote.Document;

        ParagraphFormat format;

        var style = document.styles[(footnote.style ?? "")];
        if (style != null)
            format = ParagraphFormatFromStyle(style);
        else
        {
            footnote.Style = "Footnote";
            format = document.styles[footnote.Style].paragraphFormat;
        }

        if (footnote.format == null)
        {
            footnote.format = format.Clone();
            footnote.format.parent = footnote;
        }
        else
            FlattenParagraphFormat(footnote.format, format);
    }

    internal override void VisitParagraph(Paragraph paragraph)
    {
        var document = paragraph.Document;

        ParagraphFormat format;

        var currentElementHolder = GetDocumentElementHolder(paragraph);
        var style = document.styles[(paragraph.style ?? "")];
        if (style != null)
            format = ParagraphFormatFromStyle(style);

        else if (currentElementHolder is Cell cell)
        {
            paragraph.style = cell.style;
            format = cell.format;
        }
        else if (currentElementHolder is HeaderFooter currHeaderFooter)
        {
            if (currHeaderFooter.IsHeader)
            {
                paragraph.Style = "Header";
                format = document.styles["Header"].paragraphFormat;
            }
            else
            {
                paragraph.Style = "Footer";
                format = document.styles["Footer"].paragraphFormat;
            }

            if (currHeaderFooter.format != null)
                FlattenParagraphFormat(paragraph.Format, currHeaderFooter.format);
        }
        else if (currentElementHolder is Footnote)
        {
            paragraph.Style = "Footnote";
            format = document.styles["Footnote"].paragraphFormat;
        }
        else if (currentElementHolder is TextArea)
        {
            paragraph.style = ((TextArea)currentElementHolder).style;
            format = ((TextArea)currentElementHolder).format;
        }
        else
        {
            if ((paragraph.style ?? "") != "")
                paragraph.Style = "InvalidStyleName";
            else
                paragraph.Style = "Normal";
            format = document.styles[paragraph.Style].paragraphFormat;
        }

        if (paragraph.format == null)
        {
            paragraph.format = format.Clone();
            paragraph.format.parent = paragraph;
        }
        else
            FlattenParagraphFormat(paragraph.format, format);
    }

    #endregion

    #region Section

    internal override void VisitHeaderFooter(HeaderFooter headerFooter)
    {
        var document = headerFooter.Document;
        string styleString;
        if (headerFooter.IsHeader)
            styleString = "Header";
        else
            styleString = "Footer";

        ParagraphFormat format;
        var style = document.styles[(headerFooter.style ?? "")];
        if (style != null)
            format = ParagraphFormatFromStyle(style);
        else
        {
            format = document.styles[styleString].paragraphFormat;
            headerFooter.Style = styleString;
        }

        if (headerFooter.format == null)
        {
            headerFooter.format = format.Clone();
            headerFooter.format.parent = headerFooter;
        }
        else
            FlattenParagraphFormat(headerFooter.format, format);
    }

    internal override void VisitHeadersFooters(HeadersFooters headersFooters)
    {
    }

    internal override void VisitSection(Section section)
    {
        var prevSec = section.PreviousSection();
        var prevPageSetup = PageSetup.DefaultPageSetup;
        if (prevSec != null)
        {
            prevPageSetup = prevSec.pageSetup;

            if (!section.Headers.HasHeaderFooter(HeaderFooterIndex.Primary))
                section.Headers.primary = prevSec.Headers.primary;
            if (!section.Headers.HasHeaderFooter(HeaderFooterIndex.EvenPage))
                section.Headers.evenPage = prevSec.Headers.evenPage;
            if (!section.Headers.HasHeaderFooter(HeaderFooterIndex.FirstPage))
                section.Headers.firstPage = prevSec.Headers.firstPage;

            if (!section.Footers.HasHeaderFooter(HeaderFooterIndex.Primary))
                section.Footers.primary = prevSec.Footers.primary;
            if (!section.Footers.HasHeaderFooter(HeaderFooterIndex.EvenPage))
                section.Footers.evenPage = prevSec.Footers.evenPage;
            if (!section.Footers.HasHeaderFooter(HeaderFooterIndex.FirstPage))
                section.Footers.firstPage = prevSec.Footers.firstPage;
        }

        if (section.pageSetup == null)
            section.pageSetup = prevPageSetup;
        else
            FlattenPageSetup(section.pageSetup, prevPageSetup);
    }

    internal override void VisitSections(Sections sections)
    {
    }

    #endregion

    #region Shape

    internal override void VisitTextFrame(TextFrame textFrame)
    {
        if (textFrame.height.IsNull)
            textFrame.height = Unit.FromInch(1);
        if (textFrame.width.IsNull)
            textFrame.width = Unit.FromInch(1);
    }

    #endregion

    #region Table

    internal override void VisitCell(Cell cell)
    {
        // format, shading and borders are already processed.
    }

    internal override void VisitColumns(Columns columns)
    {
        foreach (Column col in columns)
        {
            if (col.width.IsNull)
                col.width = columns.width;

            if (col.width.IsNull)
                col.width = "2.5cm";
        }
    }

    internal override void VisitRow(Row row)
    {
        foreach (Cell cell in row.Cells)
        {
            cell.verticalAlignment ??= row.verticalAlignment;
        }
    }

    internal override void VisitRows(Rows rows)
    {
        foreach (Row row in rows)
        {
            if (row.height.IsNull)
                row.height = rows.height;
            row.heightRule ??= rows.heightRule;
            row.verticalAlignment ??= rows.verticalAlignment;
        }
    }

    /// <summary>
    /// Returns a paragraph format object initialized by the given style.
    /// It differs from style.ParagraphFormat if style is a character style.
    /// </summary>
    ParagraphFormat ParagraphFormatFromStyle(Style style)
    {
        if (style.Type == StyleType.Character)
        {
            var doc = style.Document;
            var format = style.paragraphFormat.Clone();
            FlattenParagraphFormat(format, doc.Styles.Normal.ParagraphFormat);
            return format;
        }
        else
            return style.paragraphFormat;
    }

    internal override void VisitTable(Table table)
    {
        var document = table.Document;

        if (table.leftPadding.IsNull)
            table.leftPadding = Unit.FromMillimeter(1.2);
        if (table.rightPadding.IsNull)
            table.rightPadding = Unit.FromMillimeter(1.2);

        ParagraphFormat format;
        var style = document.styles[(table.style ?? "")];
        if (style != null)
            format = ParagraphFormatFromStyle(style);
        else
        {
            table.Style = "Normal";
            format = document.styles.Normal.paragraphFormat;
        }

        if (table.format == null)
        {
            table.format = format.Clone();
            table.format.parent = table;
        }
        else
            FlattenParagraphFormat(table.format, format);

        var rows = table.Rows.Count;
        var clms = table.Columns.Count;

        for (var idxclm = 0; idxclm < clms; idxclm++)
        {
            var column = table.Columns[idxclm];
            ParagraphFormat colFormat;
            style = document.styles[(column.style ?? "")];
            if (style != null)
                colFormat = ParagraphFormatFromStyle(style);
            else
            {
                column.style = table.style;
                colFormat = table.Format;
            }

            if (column.format == null)
            {
                column.format = colFormat.Clone();
                column.format.parent = column;
                if (column.format.shading == null && table.format.shading != null)
                    column.format.shading = table.format.shading;
            }
            else
                FlattenParagraphFormat(column.format, colFormat);

            if (column.leftPadding.IsNull)
                column.leftPadding = table.leftPadding;
            if (column.rightPadding.IsNull)
                column.rightPadding = table.rightPadding;

            if (column.shading == null)
                column.shading = InheritedShading(table.shading, column);

            else if (table.shading != null)
                FlattenShading(column.shading, table.shading);

            if (column.borders == null)
                column.borders = InheritedBorders(table.borders, column);
            else if (table.borders != null)
                FlattenBorders(column.borders, table.borders);
        }

        for (var idxrow = 0; idxrow < rows; idxrow++)
        {
            var row = table.Rows[idxrow];

            ParagraphFormat rowFormat;
            style = document.styles[(row.style ?? "")];
            if (style != null)
            {
                rowFormat = ParagraphFormatFromStyle(style);
            }
            else
            {
                row.style = table.style;
                rowFormat = table.Format;
            }

            for (var idxclm = 0; idxclm < clms; idxclm++)
            {
                var column = table.Columns[idxclm];
                var cell = row[idxclm];

                ParagraphFormat cellFormat;
                var cellStyle = document.styles[(cell.style ?? "")];
                if (cellStyle != null)
                {
                    cellFormat = ParagraphFormatFromStyle(cellStyle);

                    if (cell.format == null)
                        cell.format = cellFormat;
                    else
                        FlattenParagraphFormat(cell.format, cellFormat);
                }
                else
                {
                    if (row.format != null)
                        FlattenParagraphFormat(cell.Format, row.format);

                    if (style != null)
                    {
                        cell.style = row.style;
                        FlattenParagraphFormat(cell.Format, rowFormat);
                    }
                    else
                    {
                        cell.style = column.style;
                        FlattenParagraphFormat(cell.Format, column.format);
                    }
                }

                if (cell.format.shading == null && table.format.shading != null)
                    cell.format.shading = table.format.shading;

                // Each cell takes a copy of what it inherits rather than the row's or the column's own
                // object: the column is flattened onto the cell straight after the row is, and writing
                // that into the row's borders would give it to every other cell of the row as well.
                if (cell.shading == null)
                    cell.shading = InheritedShading(row.shading, cell);
                else if (row.shading != null)
                    FlattenShading(cell.shading, row.shading);
                if (cell.shading == null)
                    cell.shading = InheritedShading(column.shading, cell);
                else if (column.shading != null)
                    FlattenShading(cell.shading, column.shading);
                if (cell.borders == null)
                    cell.borders = InheritedBorders(row.borders, cell);
                else if (row.borders != null)
                    FlattenBorders(cell.borders, row.borders);
                if (cell.borders == null)
                    cell.borders = InheritedBorders(column.borders, cell);
                else if (column.borders != null)
                    FlattenBorders(cell.borders, column.borders);
            }

            if (row.format == null)
            {
                row.format = rowFormat.Clone();
                row.format.parent = row;
                if (row.format.shading == null && table.format.shading != null)
                    row.format.shading = table.format.shading;
            }
            else
                FlattenParagraphFormat(row.format, rowFormat);

            if (row.topPadding.IsNull)
                row.topPadding = table.topPadding;
            if (row.bottomPadding.IsNull)
                row.bottomPadding = table.bottomPadding;

            if (row.shading == null)
                row.shading = InheritedShading(table.shading, row);
            else if (table.shading != null)
                FlattenShading(row.shading, table.shading);

            if (row.borders == null)
                row.borders = InheritedBorders(table.borders, row);
            else if (table.borders != null)
                FlattenBorders(row.borders, table.borders);
        }
    }

    #endregion


    internal override void VisitLegend(Legend legend)
    {
        ParagraphFormat parentFormat;
        if (legend.style != null)
        {
            var style = legend.Document.Styles[legend.Style];
            if (style == null)
                style = legend.Document.Styles["InvalidStyleName"];

            parentFormat = style.paragraphFormat;
        }
        else
        {
            var textArea = (TextArea)GetDocumentElementHolder(legend);
            legend.style = textArea.style;
            parentFormat = textArea.format;
        }

        if (legend.format == null)
            legend.Format = parentFormat.Clone();
        else
            FlattenParagraphFormat(legend.format, parentFormat);
    }

    internal override void VisitTextArea(TextArea textArea)
    {
        if (textArea?.elements == null)
            return;

        ParagraphFormat parentFormat;

        if (textArea.style != null)
        {
            var style = textArea.Document.Styles[textArea.Style];
            if (style == null)
                style = textArea.Document.Styles["InvalidStyleName"];

            parentFormat = style.paragraphFormat;
        }
        else
        {
            var chart = (Chart)textArea.parent;
            parentFormat = chart.format;
            textArea.style = chart.style;
        }

        if (textArea.format == null)
            textArea.Format = parentFormat.Clone();
        else
            FlattenParagraphFormat(textArea.format, parentFormat);

        FlattenFillFormat(textArea.fillFormat);
        FlattenLineFormat(textArea.lineFormat, null);
    }


    private static DocumentObject GetDocumentElementHolder(DocumentObject docObj)
    {
        var docEls = (DocumentElements)docObj.parent;
        return docEls.parent;
    }
}
