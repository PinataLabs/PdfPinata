using System.Collections.Generic;
using System.IO;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.Rendering;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using SampleApp.Infrastructure;
using Charting = PdfPinata.Charting;

namespace SampleApp.Demos;

/// <summary>
///   The charting engine, reached both ways: drawn straight onto a page, and laid out by PinataLayout.
/// </summary>
internal sealed class ChartsDemo : PdfDemo
{
    public ChartsDemo() : base() { }

    public override string Name => "Charts";

    public override string Summary =>
        "All eight chart types, combination series, and the two routes into the engine.";

    public override IReadOnlyList<string> Shows => new[]
    {
        "Column2D, ColumnStacked2D, Bar2D and BarStacked2D, sharing one set of figures",
        "Line and Area2D, with markers, gridlines and a fixed axis scale",
        "A combination chart: one series drawn as a line on a chart of columns",
        "Pie2D and PieExploded2D, with percentage data labels and the legend docked four ways",
        "ChartFrame.DrawChart against ChartFrame.Draw - the second brings its own frame",
        "The same figures again through PinataLayout, laid out in the flow between paragraphs"
    };

    public override int PageCount => 4;

    protected override PdfDocument Build(DemoContext context)
    {
        #region example
        var document = new PdfDocument();
        document.Info.Title = "Charts";

        // One set of figures for the whole demo, so that what changes from chart to chart is the
        // chart type rather than the data.
        string[] quarters = { "Q1", "Q2", "Q3", "Q4" };
        double[] north = { 42, 58, 51, 73 };
        double[] south = { 31, 29, 44, 38 };
        double[] west = { 18, 26, 33, 47 };

        var heading = new XFont("Liberation Sans", 16, XFontStyle.Bold);
        var caption = new XFont("Liberation Sans", 8);

        // docs:begin chart-frame
        // A chart carries no size of its own. ChartFrame is what gives it one: set the frame's
        // Location and Size, add the chart, and DrawChart lays it out inside that rectangle.
        // Draw - the other method - decorates the rectangle with a rounded border, a gradient and
        // a drop shadow first, which page 3 shows.
        void Place(XGraphics gfx, Charting.Chart chart, XRect rect, string label)
        {
            var frame = new Charting.ChartFrame
            {
                Location = new XPoint(rect.X, rect.Y),
                Size = new XSize(rect.Width, rect.Height)
            };
            frame.Add(chart);
            frame.DrawChart(gfx);

            gfx.DrawString(label, caption, XBrushes.DimGray,
                new XRect(rect.X, rect.Bottom + 2, rect.Width, 12), XStringFormats.TopCenter);
        }
        // docs:end chart-frame

        // docs:begin build-chart
        // Every chart on pages 1 and 2 is built the same way, so the shape of the API is visible
        // once rather than four times: a chart of some type, an X series of labels, and a value
        // series per region.
        Charting.Chart Regional(Charting.ChartType type, params string[] series)
        {
            var chart = new Charting.Chart(type)
            {
                Font =
                {
                    Name = "Liberation Sans",
                    Size = 7
                }
            };

            var labels = chart.XValues.AddXSeries();
            labels.Add(quarters);

            foreach (var name in series)
            {
                var values = chart.SeriesCollection.AddSeries();
                values.Name = name;
                values.Add(name switch { "North" => north, "South" => south, _ => west });
            }

            chart.Legend.Docking = Charting.DockingType.Bottom;
            chart.XAxis.MajorTickMark = Charting.TickMarkType.Outside;
            chart.YAxis.MajorTickMark = Charting.TickMarkType.Outside;
            chart.YAxis.HasMajorGridlines = true;
            return chart;
        }
        // docs:end build-chart

        // ----- page 1: the column and bar family -----

        var page1 = document.AddPage();
        var gfx1 = XGraphics.FromPdfPage(page1);
        gfx1.DrawString("Columns and bars", heading, XBrushes.Black, new XPoint(50, 60));

        // Clustered puts the regions side by side and compares them; stacked puts them on top of
        // one another and compares the totals. Same numbers, different question.
        Place(gfx1, Regional(Charting.ChartType.Column2D, "North", "South", "West"),
            new XRect(50, 90, 235, 210), "Column2D - clustered");
        Place(gfx1, Regional(Charting.ChartType.ColumnStacked2D, "North", "South", "West"),
            new XRect(310, 90, 235, 210), "ColumnStacked2D - one bar per quarter");
        Place(gfx1, Regional(Charting.ChartType.Bar2D, "North", "South", "West"),
            new XRect(50, 350, 235, 210), "Bar2D - the same chart on its side");
        Place(gfx1, Regional(Charting.ChartType.BarStacked2D, "North", "South", "West"),
            new XRect(310, 350, 235, 210), "BarStacked2D");

        // ----- page 2: lines, areas, and one chart of two kinds -----

        var page2 = document.AddPage();
        var gfx2 = XGraphics.FromPdfPage(page2);
        gfx2.DrawString("Lines, areas and combinations", heading, XBrushes.Black, new XPoint(50, 60));

        // docs:begin fixed-scale
        var line = Regional(Charting.ChartType.Line, "North", "South", "West");
        foreach (var index in new[] { 0, 1, 2 })
        {
            line.SeriesCollection[index].MarkerStyle = Charting.MarkerStyle.Circle;
            line.SeriesCollection[index].MarkerSize = 4;
        }

        // An axis left alone scales itself to the data. Fixing the scale is how two charts drawn
        // from different figures become comparable - and how a chart stops rescaling itself every
        // time the numbers move.
        line.YAxis.MinimumScale = 0;
        line.YAxis.MaximumScale = 80;
        line.YAxis.MajorTick = 20;
        Place(gfx2, line, new XRect(50, 90, 235, 210), "Line - markers and a fixed scale");
        // docs:end fixed-scale

        Place(gfx2, Regional(Charting.ChartType.Area2D, "North", "South"),
            new XRect(310, 90, 235, 210), "Area2D - two series, the later in front");

        // docs:begin combination
        // A series carries its own ChartType, and a chart whose series disagree with it is drawn by
        // CombinationChartRenderer instead. That is the whole of the combination API: set the
        // property on the series that should be different.
        var combination = Regional(Charting.ChartType.Column2D, "North", "South", "West");
        combination.SeriesCollection[2].ChartType = Charting.ChartType.Line;
        combination.SeriesCollection[2].MarkerStyle = Charting.MarkerStyle.Diamond;
        combination.SeriesCollection[2].MarkerSize = 5;
        Place(gfx2, combination, new XRect(50, 350, 495, 210),
            "Column2D with one series set to Line - a combination chart");
        // docs:end combination

        // ----- page 3: pies, labels and the framed drawing -----

        var page3 = document.AddPage();
        var gfx3 = XGraphics.FromPdfPage(page3);
        gfx3.DrawString("Pies, labels and frames", heading, XBrushes.Black, new XPoint(50, 60));

        // docs:begin pie
        // A pie shows one series, so the X series labels the slices rather than an axis.
        Charting.Chart Pie(Charting.ChartType type, Charting.DockingType docking)
        {
            var chart = new Charting.Chart(type)
            {
                Font =
                {
                    Name = "Liberation Sans",
                    Size = 7
                }
            };
            chart.XValues.AddXSeries().Add(quarters);
            chart.SeriesCollection.AddSeries().Add(north);
            chart.Legend.Docking = docking;

            // A pie's natural label is the share rather than the number, which is what Percent
            // means here; Value would print 42, 58, 51, 73 again.
            chart.HasDataLabel = true;
            chart.DataLabel.Type = Charting.DataLabelType.Percent;
            chart.DataLabel.Position = Charting.DataLabelPosition.InsideEnd;
            chart.DataLabel.Format = "0%";
            return chart;
        }
        // docs:end pie

        Place(gfx3, Pie(Charting.ChartType.Pie2D, Charting.DockingType.Right),
            new XRect(50, 90, 235, 200), "Pie2D - legend docked Right");
        Place(gfx3, Pie(Charting.ChartType.PieExploded2D, Charting.DockingType.Left),
            new XRect(310, 90, 235, 200), "PieExploded2D - legend docked Left");

        // docs:begin framed
        // The other draw method. Draw() paints a rounded border, a vertical gradient and a drop
        // shadow of its own before laying the chart out inside them, and it draws every chart the
        // frame holds rather than only the first. Worth knowing which one you called: a chart that
        // arrives with a border nobody asked for arrived through here.
        var framed = new Charting.ChartFrame
        {
            Location = new XPoint(50, 350),
            Size = new XSize(495, 230)
        };
        framed.Add(Regional(Charting.ChartType.Column2D, "North", "South"));
        framed.Draw(gfx3);
        // docs:end framed
        gfx3.DrawString("ChartFrame.Draw - the frame is the frame's, not the chart's",
            caption, XBrushes.DimGray, new XRect(50, 584, 495, 12), XStringFormats.TopCenter);

        // ----- page 4: the same engine, reached through PinataLayout -----

        // PinataLayout holds a chart of its own in the document object model and maps it onto the
        // charting engine above at render time - PinataLayout.Rendering.ChartMapper does the copying.
        // The difference is not the picture, it is who decides where the chart goes: here the
        // renderer places it in the flow, where above the caller passed a rectangle.
        var report = new Document();
        report.Styles[StyleNames.Normal].Font.Name = "Liberation Sans";
        var section = report.AddSection();
        section.PageSetup.LeftMargin = Unit.FromPoint(50);
        section.PageSetup.RightMargin = Unit.FromPoint(50);
        section.PageSetup.TopMargin = Unit.FromPoint(50);

        var title = section.AddParagraph("The same figures through PinataLayout");
        title.Format.Font.Size = 16;
        title.Format.Font.Bold = true;
        title.Format.SpaceAfter = Unit.FromPoint(12);

        section.AddParagraph(
            "A chart added to a section is an element of the document like a paragraph or a table. "
            + "It is laid out in the flow, it moves when the text above it moves, and it breaks to "
            + "the next page if it does not fit - none of which the drawn route does for you.")
            .Format.SpaceAfter = Unit.FromPoint(10);

        // docs:begin dom-chart
        var flowed = section.AddChart(ChartType.Column2D);
        flowed.Width = Unit.FromPoint(440);
        flowed.Height = Unit.FromPoint(220);
        // The DOM's chart takes its type from a paragraph format rather than from a Font of its
        // own, because everything in a PinataLayout document is formatted the same way.
        flowed.Format.Font.Name = "Liberation Sans";
        flowed.Format.Font.Size = 8;

        flowed.XValues.AddXSeries().Add(quarters);
        var domNorth = flowed.SeriesCollection.AddSeries();
        domNorth.Name = "North";
        domNorth.Add(north);
        var domSouth = flowed.SeriesCollection.AddSeries();
        domSouth.Name = "South";
        domSouth.Add(south);

        // The DOM's chart has six text areas around the plot - header, footer, left, right, top,
        // bottom - and the legend is added to one of them rather than docked to a side.
        flowed.HeaderArea.AddParagraph("Sales by region").Format.Font.Bold = true;
        flowed.BottomArea.AddLegend();
        flowed.YAxis.HasMajorGridlines = true;
        flowed.XAxis.MajorTickMark = TickMarkType.Outside;
        // docs:end dom-chart

        section.AddParagraph(
            "The two routes reach the same renderers. Reach for this one when the chart belongs to "
            + "a document, and for the drawn one when it belongs to a page you are laying out "
            + "yourself.").Format.SpaceBefore = Unit.FromPoint(10);

        var renderer = new PdfDocumentRenderer(unicode: true) { Document = report };
        renderer.RenderDocument();

        // Saved and reopened rather than imported from the live document: a document being written
        // and a document being read are different things to PdfPinata, and Import is the mode that
        // permits taking pages out of one.
        using (var buffer = new MemoryStream())
        {
            renderer.PdfDocument.Save(buffer, false);
            buffer.Position = 0;

            using var laidOut = PdfReader.Open(buffer, PdfDocumentOpenMode.Import);
            foreach (var rendered in laidOut.Pages)
                _ = document.AddPage(rendered);
        }
        #endregion

        return document;
    }
}
