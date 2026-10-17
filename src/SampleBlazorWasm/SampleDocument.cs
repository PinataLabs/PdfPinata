using System.IO;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Shapes;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;

namespace SampleBlazorWasm;

/// <summary>
/// The document the Generate button builds: one A4 page with text, a table, an image and a chart,
/// laid out by PinataLayout and saved to bytes.
/// </summary>
/// <remarks>
/// It registers nothing. The font resolver and the image backend are the app's to register, once,
/// at startup (see <c>Program.cs</c>); this class only names the family the app serves and hands
/// over the image bytes the app fetched. That is also what lets the tests run it against their own
/// backend.
/// </remarks>
public static class SampleDocument
{
    /// <summary>The family the app serves from <c>wwwroot/fonts</c>, and the one the document is set in.</summary>
    public const string FontFamily = "Liberation Sans";

    /// <summary>Where the app serves the image from, relative to its base address.</summary>
    public const string ImageUrl = "images/alpha-badge.png";

    /// <summary>How many pages the document has. Everything is sized to fit on one.</summary>
    public const int PageCount = 1;

    /// <summary>The name the download is saved under.</summary>
    public const string FileName = "pdfpinata-blazor.pdf";

    /// <summary>
    /// Builds the document and answers the bytes of the PDF.
    /// </summary>
    /// <param name="image">The encoded image to place on the page, a PNG or a JPEG.</param>
    public static byte[] Build(byte[] image)
    {
        var document = new Document { Info = { Title = "PdfPinata in the browser" } };
        document.Styles[StyleNames.Normal].Font.Name = FontFamily;
        document.Styles[StyleNames.Normal].Font.Size = 10;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);

        AddText(section);
        AddTable(section);
        AddImage(section, image);
        AddChart(section);

        var renderer = new PdfDocumentRenderer(unicode: true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private static void AddText(Section section)
    {
        var heading = section.AddParagraph("Built in your browser");
        heading.Format.Font.Size = 20;
        heading.Format.Font.Bold = true;
        heading.Format.SpaceAfter = Unit.FromPoint(8);

        var body = section.AddParagraph();
        body.Format.SpaceAfter = Unit.FromPoint(12);
        body.AddText("This PDF was laid out and written by PdfPinata running on .NET for WebAssembly. ");
        body.AddText("No server was asked for anything but static files: the fonts, the image and the app itself. ");
        body.AddFormattedText("Nothing left this page.", TextFormat.Italic);
    }

    private static void AddTable(Section section)
    {
        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.5);
        table.Borders.Color = Colors.Gray;
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(11));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Shading.Color = Colors.LightSteelBlue;
        header.Format.Font.Bold = true;
        header.Cells[0].AddParagraph("Piece");
        header.Cells[1].AddParagraph("What does it here");

        string[][] rows =
        [
            ["Layout", "PinataLayout.Rendering"],
            ["Images", "PdfPinata.ImageSharp4, managed code with nothing native to load"],
            ["Fonts", FontFamily + ", fetched at startup and served by InMemoryFontResolver"],
            ["Download", "JavaScript interop, from a stream the app holds in memory"],
        ];

        foreach (var cells in rows)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(cells[0]);
            row.Cells[1].AddParagraph(cells[1]);
        }

        section.AddParagraph().Format.SpaceAfter = Unit.FromPoint(6);
    }

    private static void AddImage(Section section, byte[] image)
    {
        var paragraph = section.AddParagraph();
        paragraph.Format.Alignment = ParagraphAlignment.Center;
        paragraph.Format.SpaceAfter = Unit.FromPoint(12);

        // From bytes rather than from a file: a browser app has no file system worth the name, and
        // the bytes were fetched over HTTP like the fonts.
        var picture = paragraph.AddImage(ImageSource.FromBinary("alpha-badge.png", () => image));
        picture.Height = Unit.FromCentimeter(3);
        picture.LockAspectRatio = true;
        picture.AlternativeText = "A translucent badge, drawn over the page through its alpha channel.";
    }

    private static void AddChart(Section section)
    {
        var chart = section.AddChart(ChartType.Column2D);
        chart.Width = Unit.FromCentimeter(16);
        chart.Height = Unit.FromCentimeter(8);
        chart.Format.Font.Name = FontFamily;
        chart.Format.Font.Size = 8;
        // PinataLayout tags what it draws, and a chart with no description is tagged as decoration.
        chart.AlternativeText = "Column chart of sales by region and quarter. North rises from 42 to 73; "
                                + "South stays between 29 and 44.";

        chart.XValues.AddXSeries().Add("Q1", "Q2", "Q3", "Q4");

        var north = chart.SeriesCollection.AddSeries();
        north.Name = "North";
        north.Add(42, 58, 51, 73);

        var south = chart.SeriesCollection.AddSeries();
        south.Name = "South";
        south.Add(31, 29, 44, 38);

        chart.HeaderArea.AddParagraph("Sales by region").Format.Font.Bold = true;
        chart.BottomArea.AddLegend();
        chart.YAxis.HasMajorGridlines = true;
        chart.XAxis.MajorTickMark = TickMarkType.Outside;
    }
}
