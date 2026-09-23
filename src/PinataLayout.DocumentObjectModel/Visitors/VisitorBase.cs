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
        (documentObject as IVisitable)?.AcceptVisitor(this, true);
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
        {
            FlattenFont(format.font, refFormat.font);
        }

        if (format.shading == null)
        {
            if (refFormat.shading != null)
            {
                format.shading = refFormat.shading.Clone();
                format.shading.parent = format;
            }
        }
        else if (refFormat.shading != null)
        {
            FlattenShading(format.shading, refFormat.shading);
        }

        // Copied rather than shared, as the font and the shading above are: a format is flattened
        // more than once, and the second pass would otherwise write what it inherits back into the
        // format it inherited the borders from.
        if (format.borders == null)
            format.borders = InheritedBorders(refFormat.borders, format);
        else if (refFormat.borders != null)
            FlattenBorders(format.borders, refFormat.borders);

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
        border ??= new Border(parentBorders);

        border.visible ??= parentBorders.visible;

        border.style ??= parentBorders.style;

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

        if (refBorders.bottom == null)
            return;

        FlattenBorder(borders.Bottom, refBorders.bottom);
        FlattenedBorderFromBorders(borders.bottom, borders);
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
            {
                PageSetup.GetPageSize(pageSetup.PageFormat, out pageSetup.pageWidth, out pageSetup.pageHeight);
            }
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

        pageSetup.sectionStart ??= refPageSetup.sectionStart;
        pageSetup.orientation ??= refPageSetup.orientation;
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
        pageSetup.oddAndEvenPagesHeaderFooter ??= refPageSetup.oddAndEvenPagesHeaderFooter;
        pageSetup.differentFirstPageHeaderFooter ??= refPageSetup.differentFirstPageHeaderFooter;
        pageSetup.mirrorMargins ??= refPageSetup.mirrorMargins;
        pageSetup.horizontalPageBreak ??= refPageSetup.horizontalPageBreak;
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
        if (refLineFormat == null)
            return;

        if (lineFormat.width.IsNull)
            lineFormat.width = refLineFormat.width;
    }
#pragma warning restore CA1822

    /// <summary>
    /// Flattens a chart axis: the widths of its gridlines and its line, and the fonts of its title
    /// and its tick labels.
    /// </summary>
    /// <remarks>
    /// Its tick marks, tick spacing and scales are not flattened, because nothing above an axis has
    /// a value for them to inherit: left unset, <c>AxisMapper</c> leaves them unset too and the chart
    /// renderers choose - an outside tick mark, and a scale worked out from the data.
    /// </remarks>
    protected void FlattenAxis(Axis axis)
    {
        if (axis == null)
            return;

        var refLineFormat = new LineFormat { width = 0.15 };
        if ((axis.hasMajorGridlines ?? false) && axis.majorGridlines != null)
            FlattenLineFormat(axis.majorGridlines.lineFormat, refLineFormat);
        if ((axis.hasMinorGridlines ?? false) && axis.minorGridlines != null)
            FlattenLineFormat(axis.minorGridlines.lineFormat, refLineFormat);

        refLineFormat.width = 0.4;
        if (axis.lineFormat != null)
            FlattenLineFormat(axis.lineFormat, refLineFormat);

        var chart = axis.Parent as Chart;
        if (axis.title?.font != null)
            FlattenChartFont(axis.title.font, axis.title.style, chart);
        if (axis.tickLabels?.font != null)
            FlattenChartFont(axis.tickLabels.font, axis.tickLabels.style, chart);
    }

    /// <summary>
    /// Fills in a chart element's own font from the style it names or, naming none, from the
    /// chart's. Without it, a title given both a style and a font of its own reached the renderer
    /// as two fonts, and the second, mapped over the first, answered <c>false</c> for every bold
    /// and italic it had not set.
    /// </summary>
    private void FlattenChartFont(Font font, string styleName, Chart chart)
    {
        var refFont = !string.IsNullOrEmpty(styleName) && font.Document?.Styles[styleName] is { } style
            ? style.Font
            : chart?.format?.font;
        if (refFont != null)
            FlattenFont(font, refFont);
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
        chart.style ??= Style.DefaultParagraphName;
        var style = document.Styles[chart.style ?? ""];
        if (chart.format == null)
        {
            chart.format = style.paragraphFormat.Clone();
            chart.format.parent = chart;
        }
        else
        {
            FlattenParagraphFormat(chart.format, style.paragraphFormat);
        }


        FlattenLineFormat(chart.lineFormat, null);
        FlattenFillFormat(chart.fillFormat);

        FlattenAxis(chart.xAxis);
        FlattenAxis(chart.yAxis);
        FlattenAxis(chart.zAxis);

        FlattenPlotArea(chart.plotArea);

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
        if (baseStyle is not { paragraphFormat: not null })
            return;

        if (style.paragraphFormat == null)
            style.paragraphFormat = baseStyle.paragraphFormat;
        else
            FlattenParagraphFormat(style.paragraphFormat, baseStyle.paragraphFormat);
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

        var style = document.styles[footnote.style ?? ""];
        if (style != null)
        {
            format = ParagraphFormatFromStyle(style);
        }
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
        {
            FlattenParagraphFormat(footnote.format, format);
        }
    }

    internal override void VisitParagraph(Paragraph paragraph)
    {
        var document = paragraph.Document;

        ParagraphFormat format;

        var currentElementHolder = GetDocumentElementHolder(paragraph);
        var style = document.styles[paragraph.style ?? ""];
        if (style != null)
        {
            format = ParagraphFormatFromStyle(style);
        }

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
        {
            FlattenParagraphFormat(paragraph.format, format);
        }
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
        var style = document.styles[headerFooter.style ?? ""];
        if (style != null)
        {
            format = ParagraphFormatFromStyle(style);
        }
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
        {
            FlattenParagraphFormat(headerFooter.format, format);
        }
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
    private ParagraphFormat ParagraphFormatFromStyle(Style style)
    {
        if (style.Type == StyleType.Character)
        {
            var doc = style.Document;
            var format = style.paragraphFormat.Clone();
            FlattenParagraphFormat(format, doc.Styles.Normal.ParagraphFormat);
            return format;
        }
        return style.paragraphFormat;
    }

    internal override void VisitTable(Table table)
    {
        var document = table.Document;

        if (table.leftPadding.IsNull)
            table.leftPadding = Unit.FromMillimeter(1.2);
        if (table.rightPadding.IsNull)
            table.rightPadding = Unit.FromMillimeter(1.2);

        if (!TryStyledFormat(document, table.style, out var format))
        {
            table.Style = "Normal";
            format = document.styles.Normal.paragraphFormat;
        }

        table.format = OwnFormat(table.format, format, table, null);

        var columns = table.Columns.Count;
        for (var idxclm = 0; idxclm < columns; idxclm++)
            FlattenColumn(table, table.Columns[idxclm]);

        var rows = table.Rows.Count;
        for (var idxrow = 0; idxrow < rows; idxrow++)
            FlattenRow(table, table.Rows[idxrow]);
    }

    private void FlattenColumn(Table table, Column column)
    {
        if (!TryStyledFormat(table.Document, column.style, out var colFormat))
        {
            column.style = table.style;
            colFormat = table.Format;
        }

        column.format = OwnFormat(column.format, colFormat, column, table.format.shading);

        if (column.leftPadding.IsNull)
            column.leftPadding = table.leftPadding;
        if (column.rightPadding.IsNull)
            column.rightPadding = table.rightPadding;

        column.shading = MergedShading(column.shading, table.shading, column);
        column.borders = MergedBorders(column.borders, table.borders, column);
    }

    private void FlattenRow(Table table, Row row)
    {
        var rowHasStyle = TryStyledFormat(table.Document, row.style, out var rowFormat);
        if (!rowHasStyle)
        {
            row.style = table.style;
            rowFormat = table.Format;
        }

        // The cells go first: they take the row's shading and borders as the row itself states them,
        // before the row has inherited the table's.
        var columns = table.Columns.Count;
        for (var idxclm = 0; idxclm < columns; idxclm++)
            FlattenCell(table, row, rowHasStyle, rowFormat, table.Columns[idxclm], row[idxclm]);

        row.format = OwnFormat(row.format, rowFormat, row, table.format.shading);

        if (row.topPadding.IsNull)
            row.topPadding = table.topPadding;
        if (row.bottomPadding.IsNull)
            row.bottomPadding = table.bottomPadding;

        row.shading = MergedShading(row.shading, table.shading, row);
        row.borders = MergedBorders(row.borders, table.borders, row);
    }

    private void FlattenCell(Table table, Row row, bool rowHasStyle, ParagraphFormat rowFormat, Column column, Cell cell)
    {
        if (TryStyledFormat(table.Document, cell.style, out var cellFormat))
        {
            if (cell.format == null)
                cell.format = cellFormat;
            else
                FlattenParagraphFormat(cell.format, cellFormat);
        }
        else
        {
            if (row.format != null)
                FlattenParagraphFormat(cell.Format, row.format);

            if (rowHasStyle)
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

        cell.format.shading ??= table.format.shading;

        // Each cell takes a copy of what it inherits rather than the row's or the column's own
        // object: the column is flattened onto the cell straight after the row is, and writing
        // that into the row's borders would give it to every other cell of the row as well.
        cell.shading = MergedShading(cell.shading, row.shading, cell);
        cell.shading = MergedShading(cell.shading, column.shading, cell);
        cell.borders = MergedBorders(cell.borders, row.borders, cell);
        cell.borders = MergedBorders(cell.borders, column.borders, cell);
    }

    /// <summary>
    /// Gives the paragraph format of the named style, when there is a style of that name.
    /// </summary>
    /// <remarks>
    /// Answers whether the style exists rather than whether the format does: a style's format is
    /// made on first use, so a style that exists can still have none yet.
    /// </remarks>
    private bool TryStyledFormat(Document document, string styleName, out ParagraphFormat format)
    {
        var style = document.styles[styleName ?? ""];
        format = style != null ? ParagraphFormatFromStyle(style) : null;
        return style != null;
    }

    /// <summary>
    /// An object's own paragraph format with every value left unset filled in from
    /// <paramref name="inherited"/>, or a copy of that format when the object has none of its own.
    /// A newly made copy with no shading takes <paramref name="fallbackShading"/> - the table's, for
    /// its rows and columns.
    /// </summary>
    private ParagraphFormat OwnFormat(ParagraphFormat own, ParagraphFormat inherited, DocumentObject owner, Shading fallbackShading)
    {
        if (own != null)
        {
            FlattenParagraphFormat(own, inherited);
            return own;
        }

        var format = inherited.Clone();
        format.parent = owner;
        format.shading ??= fallbackShading;
        return format;
    }

    /// <summary>
    /// An object's own shading filled in from what it inherits, or its own copy of that when it has none.
    /// </summary>
    private Shading MergedShading(Shading own, Shading inherited, DocumentObject owner)
    {
        if (own == null)
            return InheritedShading(inherited, owner);

        if (inherited != null)
            FlattenShading(own, inherited);
        return own;
    }

    /// <summary>
    /// An object's own borders filled in from what it inherits, or its own copy of them when it has none.
    /// </summary>
    private Borders MergedBorders(Borders own, Borders inherited, DocumentObject owner)
    {
        if (own == null)
            return InheritedBorders(inherited, owner);

        if (inherited != null)
            FlattenBorders(own, inherited);
        return own;
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
