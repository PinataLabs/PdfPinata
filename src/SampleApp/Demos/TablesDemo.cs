using System.Collections.Generic;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using SampleApp.Infrastructure;

namespace SampleApp.Demos;

/// <summary>
///   A PinataLayout table long enough to cross a page, so the heading row has something to repeat onto.
/// </summary>
/// <remarks>
///   The first demo that goes through PinataLayout rather than drawing on the page directly. Content
///   flows and breaks by itself, which is the whole reason to reach for it - and the reason a
///   table is where the difference shows.
/// </remarks>
internal sealed class TablesDemo : PdfDemo
{
    public TablesDemo() : base() { }

    public override string Name => "Tables";

    public override string Summary => "A PinataLayout table that breaks across pages.";

    public override IReadOnlyList<string> Shows =>
    [
        "A heading row repeated on every page the table reaches",
        "Merged cells, across and down",
        "Alternate row shading and per-column alignment",
        "SetEdge ruling a block, and a footer counting the pages"
    ];

    public override int PageCount => 2;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var document = new Document
        {
            Info =
            {
                Title = "Tables"
            }
        };

        // Styles are named and inherited, so setting Normal here sets the font of
        // everything that does not override it - including the table.
        document.Styles["Normal"].Font.Name = "Liberation Sans";
        document.Styles["Normal"].Font.Size = 9;

        var heading = document.Styles.AddStyle("TableHeading", "Normal");
        heading.Font.Bold = true;
        heading.Font.Color = Colors.White;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        // The footer is written once and appears on every page. The two fields are
        // resolved at render time, when how many pages there are is finally known.
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.AddText("Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        var title = section.AddParagraph("Quarterly returns by region");
        title.Format.Font.Size = 16;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(12);

        // docs:begin columns
        var table = section.AddTable();
        table.Borders.Width = 0.4;
        table.Borders.Color = Colors.LightGray;
        table.Rows.LeftIndent = 0;

        // Columns are added with their widths, and each carries the alignment of the
        // cells in it - set once here rather than on every cell below.
        var region = table.AddColumn(Unit.FromCentimeter(3.4));
        region.Format.Alignment = ParagraphAlignment.Left;

        var quarter = table.AddColumn(Unit.FromCentimeter(2.2));
        quarter.Format.Alignment = ParagraphAlignment.Left;

        foreach (var _ in new[] { "units", "revenue", "margin" })
            table.AddColumn(Unit.FromCentimeter(3)).Format.Alignment = ParagraphAlignment.Right;
        // docs:end columns

        // docs:begin heading-rows
        // A title band across the whole width. MergeRight is a count of the cells to
        // swallow to the right, so the other four cells of this row are never filled in.
        var band = table.AddRow();
        band.Shading.Color = Colors.DarkSlateGray;
        band.Cells[0].MergeRight = 4;
        band.Cells[0].Format.Alignment = ParagraphAlignment.Center;
        band.Cells[0].AddParagraph("Financial year to date").Style = "TableHeading";

        // HeadingFormat is what makes a row repeat onto every page the table reaches.
        //
        // It has to be set on this band as well as on the row of column names below it,
        // and that is not decoration. The renderer walks the rows from the first one and
        // stops at the first row that does not carry the flag, so the heading is whatever
        // unbroken run of rows starts the table. Marking only the second row would leave
        // the run empty and nothing would repeat at all.
        band.HeadingFormat = true;

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Colors.SlateGray;
        header.VerticalAlignment = VerticalAlignment.Center;
        header.Height = Unit.FromPoint(20);

        string[] headings = { "Region", "Quarter", "Units", "Revenue", "Margin" };
        for (var column = 0; column < headings.Length; column++)
            header.Cells[column].AddParagraph(headings[column]).Style = "TableHeading";
        // docs:end heading-rows

        // Twenty regions of four quarters each is eighty rows, which is comfortably more
        // than an A4 page holds. That is the point: a table that fits on one page has
        // nothing to say about what happens to the heading when it does not.
        string[] regions =
        {
            "North", "South", "East", "West", "Central", "Highlands", "Islands",
            "Coastal", "Riverside", "Uplands", "Lowlands", "Borders", "Midlands",
            "Fenland", "Weald", "Downs", "Moors", "Dales", "Marches", "Cinque Ports"
        };
        string[] quarters = { "Q1", "Q2", "Q3", "Q4" };

        // Accumulated as the rows are built rather than worked out again afterwards, so the
        // totals row cannot disagree with the column above it. A reader who adds the column
        // up is exactly the reader a table demo has to survive.
        var rowIndex = 0;
        long totalUnits = 0;
        double totalRevenue = 0;
        double marginTimesUnits = 0;

        foreach (var name in regions)
        {
            for (var q = 0; q < quarters.Length; q++)
            {
                // docs:begin banding-and-merge-down
                var row = table.AddRow();
                row.VerticalAlignment = VerticalAlignment.Center;

                // Banding by row rather than by border, which stays readable when the
                // table is wide and the eye has to track across it.
                if (rowIndex % 2 == 1)
                    row.Shading.Color = Colors.WhiteSmoke;

                // The region is named once and its cell swallows the three rows below it,
                // so the four quarters read as one block. MergeDown counts the rows taken.
                if (q == 0)
                {
                    row.Cells[0].MergeDown = quarters.Length - 1;
                    row.Cells[0].VerticalAlignment = VerticalAlignment.Center;
                    row.Cells[0].AddParagraph(name);
                }
                // docs:end banding-and-merge-down

                var units = 400 + rowIndex * 37 % 900;
                var revenue = units * 12.5;
                var margin = (units % 17 + 8) / 100.0;

                totalUnits += units;
                totalRevenue += revenue;
                marginTimesUnits += margin * units;

                row.Cells[1].AddParagraph(quarters[q]);
                row.Cells[2].AddParagraph($"{units:N0}");
                row.Cells[3].AddParagraph($"{revenue:N2}");
                row.Cells[4].AddParagraph($"{margin:P1}");

                rowIndex++;
            }

            // docs:begin set-edge
            // A rule under each region's block, so the merged cell has a visible extent.
            // SetEdge takes a column, a row, how many of each, and which edges to draw.
            table.SetEdge(0, table.Rows.Count - 1, 5, 1, Edge.Bottom, BorderStyle.Single, 0.8,
                Colors.Gainsboro);
            // docs:end set-edge
        }

        // docs:begin total-row
        var total = table.AddRow();
        total.Shading.Color = Colors.Gainsboro;
        total.Format.Font.Bold = true;
        total.Cells[0].MergeRight = 1;
        total.Cells[0].AddParagraph("All regions");
        total.Cells[2].AddParagraph($"{totalUnits:N0}");
        total.Cells[3].AddParagraph($"{totalRevenue:N2}");
        // Weighted by units rather than a plain mean of the column, which is what a total
        // row of percentages has to be if it is to mean anything.
        total.Cells[4].AddParagraph($"{marginTimesUnits / totalUnits:P1}");

        table.SetEdge(0, table.Rows.Count - 1, 5, 1, Edge.Box, BorderStyle.Single, 1, Colors.Black);
        // docs:end total-row

        // Lists live here rather than in the Layout demo, because ListInfo is PinataLayout's
        // and there is nothing like it on the PdfSharp side.
        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(10);
        section.AddParagraph("Notes").Format.Font.Bold = true;

        string[] notes =
        {
            "Margin is gross and excludes carriage.",
            "The heading row above repeats on every page this table reaches, which is what "
                + "HeadingFormat is for.",
            "This list is a PinataLayout ListInfo - the marker, the indent and the hanging "
                + "alignment all come from the style rather than being drawn."
        };

        // docs:begin bullet-list
        foreach (var note in notes)
        {
            var item = section.AddParagraph(note);
            item.Format.ListInfo.ListType = ListType.BulletList1;
            item.Format.LeftIndent = Unit.FromCentimeter(0.6);
        }
        // docs:end bullet-list

        // PinataLayout owns the PdfDocument: the renderer builds it, and it is handed back for
        // the base class to save exactly as the hand-drawn demos hand theirs back.
        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();
        #endregion

        return renderer.PdfDocument;
    }
}
