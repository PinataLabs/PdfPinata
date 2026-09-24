using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Text;
using PinataLayout.DocumentObjectModel.Internals;
using PinataLayout.DocumentObjectModel.IO;
using PinataLayout.DocumentObjectModel.Shapes.Charts;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.DocumentObjectModel.Visitors;
using Xunit;

namespace PinataLayout.DocumentObjectModel.Tests;

/// <summary>
///   Pins what a handful of long, many-branched methods do today, captured before they were cut
///   into smaller ones so that the cutting could be checked to have changed nothing: the DDL a
///   document touching every attribute of a font, a paragraph format, a set of borders, a chart, a
///   style and a page setup is written as; the borders <see cref="MergedCellList"/> hands a
///   renderer for every cell of a table with merges in it; what flattening makes of a page setup
///   for each way its width, height and format can be left set or unset; how flattening cuts a
///   text into words; and which borders <see cref="Table.SetEdge(int, int, int, int, Edge, BorderStyle, Unit, Color)"/>
///   writes for each edge. Each is one string compared whole, so a change anywhere in it says so.
/// </summary>
public class SerializeAndFlattenCharacterizationTests
{
    private static Document EveryAttribute()
    {
        var document = new Document();

        // Built-in styles: Normal changed, a heading with its own base left alone but its font
        // changed, one given a different base, and one whose font repeats its base's.
        document.Styles.Normal.Font.Name = "Arial";
        document.Styles.Normal.ParagraphFormat.LeftIndent = "1cm";
        document.Styles[StyleNames.Heading1].Font.Bold = true;
        document.Styles[StyleNames.Heading1].Font.Underline = Underline.None;
        document.Styles[StyleNames.Heading2].Font.Bold = true;
        document.Styles[StyleNames.Heading3].Font.Name = "Arial";
        document.Styles[StyleNames.Heading4].BaseStyle = StyleNames.Normal;
        document.Styles[StyleNames.Heading4].Font.Italic = true;

        // A user-defined paragraph style setting everything a paragraph format can.
        var loud = document.Styles.AddStyle("Loud Style", StyleNames.Normal);
        loud.Font.Name = "Courier";
        loud.Font.Size = 13;
        loud.Font.Italic = true;
        loud.Font.Strikethrough = Strikethrough.Single;
        loud.Font.Superscript = true;
        loud.Font.Color = Colors.Red;
        var format = loud.ParagraphFormat;
        format.Alignment = ParagraphAlignment.Right;
        format.LeftIndent = "1cm";
        format.FirstLineIndent = "-5mm";
        format.RightIndent = "2cm";
        format.SpaceBefore = 3;
        format.SpaceAfter = 4;
        format.LineSpacingRule = LineSpacingRule.Exactly;
        format.LineSpacing = 14;
        format.KeepTogether = true;
        format.KeepWithNext = false;
        format.TextDirection = BidiParagraphDirection.RightToLeft;
        format.WidowControl = false;
        format.PageBreakBefore = true;
        format.OutlineLevel = OutlineLevel.Level2;
        format.ListInfo.ListType = ListType.NumberList2;
        format.TabStops.AddTabStop("3cm", TabAlignment.Center, TabLeader.Dots);
        format.Borders.Width = 2;
        format.Borders.Color = Colors.Blue;
        format.Shading.Color = Colors.Yellow;

        // A character style, whose paragraph format is not written.
        var mark = document.Styles.AddStyle("Mark", Style.DefaultParagraphFontName);
        mark.Font.Subscript = true;
        mark.Font.Color = Colors.Green;

        var section = document.AddSection();
        var setup = section.PageSetup;
        setup.PageHeight = "20cm";
        setup.PageWidth = "15cm";
        setup.Orientation = Orientation.Landscape;
        setup.LeftMargin = "1cm";
        setup.RightMargin = "1.5cm";
        setup.TopMargin = "2cm";
        setup.BottomMargin = "2.5cm";
        setup.FooterDistance = "0.5cm";
        setup.HeaderDistance = "0.7cm";
        setup.OddAndEvenPagesHeaderFooter = true;
        setup.DifferentFirstPageHeaderFooter = false;
        setup.SectionStart = BreakType.BreakOddPage;
        setup.PageFormat = PageFormat.A5;
        setup.MirrorMargins = true;
        setup.HorizontalPageBreak = false;
        setup.StartingNumber = 5;
        setup.Comment = "the setup";

        // A paragraph with every border attribute, then formatted text in each of the shapes the
        // font writer has a keyword for, and in the ones it has not.
        var paragraph = section.AddParagraph("plain");
        paragraph.Style = "Loud Style";
        var borders = paragraph.Format.Borders;
        borders.Visible = true;
        borders.Style = BorderStyle.DashDot;
        borders.Width = 1.5;
        borders.Color = Colors.Maroon;
        borders.DistanceFromTop = 1;
        borders.DistanceFromBottom = 2;
        borders.DistanceFromLeft = 3;
        borders.DistanceFromRight = 4;
        borders.Top.Width = 1;
        borders.Left.Color = Colors.Olive;
        borders.Bottom.Style = BorderStyle.Dot;
        borders.Right.Visible = false;
        borders.DiagonalDown.Width = 0.25;
        borders.DiagonalUp.Width = 0.5;
        paragraph.Format.Font.Bold = true;
        paragraph.Format.Font.Name = "Times";

        paragraph.AddFormattedText("sized").Font.Size = 9;
        paragraph.AddFormattedText("bold").Font.Bold = true;
        paragraph.AddFormattedText("notbold").Font.Bold = false;
        paragraph.AddFormattedText("italic").Font.Italic = true;
        paragraph.AddFormattedText("notitalic").Font.Italic = false;
        paragraph.AddFormattedText("coloured").Font.Color = Colors.Purple;
        paragraph.AddFormattedText("underlined").Font.Underline = Underline.Words;
        var several = paragraph.AddFormattedText("several");
        several.Font.Name = "Verdana";
        several.Font.Size = 7;
        several.Font.Subscript = true;
        var emptyName = paragraph.AddFormattedText("emptyname");
        emptyName.Font.Name = "";
        emptyName.Font.Italic = true;
        emptyName.Font.Bold = true;
        var styled = paragraph.AddFormattedText("styled");
        styled.Style = "Mark";
        styled.Font.Bold = true;
        var styledOnly = paragraph.AddFormattedText("styledonly");
        styledOnly.Style = "Mark";
        styledOnly.Font.Size = 8;

        var cleared = section.AddParagraph("cleared");
        cleared.Format.Borders.Width = 3;
        cleared.Format.Borders.ClearAll();

        var chart = section.AddChart(ChartType.Column2D);
        chart.DisplayBlanksAs = BlankType.Zero;
        chart.PivotChart = true;
        chart.HasDataLabel = true;
        chart.Style = "Loud Style";
        chart.Format.Alignment = ParagraphAlignment.Center;
        chart.DataLabel.Type = DataLabelType.Value;
        chart.PlotArea.LeftPadding = 3;
        chart.HeaderArea.AddParagraph("head");
        chart.FooterArea.AddParagraph("foot");
        chart.TopArea.AddParagraph("top");
        chart.BottomArea.AddParagraph("bottom");
        chart.LeftArea.AddParagraph("left");
        chart.RightArea.AddParagraph("right");
        chart.XAxis.HasMajorGridlines = true;
        chart.YAxis.MinimumScale = 1;
        chart.ZAxis.HasMajorGridlines = false;
        chart.SeriesCollection.AddSeries().Add(1.0, 2.0);
        chart.XValues.AddXSeries().Add("a", "b");

        return document;
    }

    [Fact]
    public void ADocumentSettingEveryAttributeSerializesToTheDdlItAlwaysHas() =>
        DdlWriter.WriteToString(EveryAttribute()).Should().Be(string.Join(Environment.NewLine, ExpectedEveryAttribute));

    /// <summary>
    ///   A four-by-four table with a merge in each direction, and a border of a different width on
    ///   most sides of most cells, so that each neighbour comparison has something to decide.
    /// </summary>
    private static Table MergedTable()
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        for (var c = 0; c < 4; c++)
            table.AddColumn("2cm");
        for (var r = 0; r < 4; r++)
            table.AddRow();

        table[0, 1].MergeRight = 1;
        table[1, 0].MergeDown = 1;
        table[1, 2].MergeRight = 1;
        table[1, 2].MergeDown = 1;

        for (var r = 0; r < 4; r++)
        {
            // Every cell's borders are asked for, so every cell has a Borders object, set or not.
            for (var c = 0; c < 4; c++)
                SetMergedTableBorders(table[r, c].Borders, r * 4 + c);
        }
        table[3, 3].Borders.Width = 4;
        return table;
    }

    /// <summary>
    ///   Sets the borders of the cell numbered <paramref name="n"/>, counting across the rows: every
    ///   fifth cell is left alone, and the rest get widths, and a hidden right border, by their number.
    /// </summary>
    private static void SetMergedTableBorders(Borders cellBorders, int n)
    {
        if (n % 5 == 0)
            return;
        cellBorders.Top.Width = 0.5 + n % 3;
        cellBorders.Left.Width = 0.5 + n % 4;
        if (n % 2 == 0)
            cellBorders.Bottom.Width = 1 + n % 5;
        if (n % 3 == 0)
            cellBorders.Right.Visible = false;
        else
            cellBorders.Right.Width = 2 + n % 2;
    }

    private static string Side(Border border) =>
        border == null
            ? "-"
            : (border.IsNull("Visible") ? "" : border.Visible ? "v" : "h")
              + (border.IsNull("Width") ? "?" : border.Width.Point.ToString("0.##", CultureInfo.InvariantCulture));

    private static string MergedBorders()
    {
        var merged = new MergedCellList(MergedTable());

        return string.Join("|", merged.Select(cell =>
        {
            var b = merged.GetEffectiveBorders(cell);
            return $"{cell.Row.Index},{cell.Column.Index}: T{Side(b.GetValue("Top", GV.ReadOnly) as Border)}"
                   + $" L{Side(b.GetValue("Left", GV.ReadOnly) as Border)}"
                   + $" B{Side(b.GetValue("Bottom", GV.ReadOnly) as Border)}"
                   + $" R{Side(b.GetValue("Right", GV.ReadOnly) as Border)}"
                   + $" W{(b.IsNull("Width") ? "?" : b.Width.Point.ToString(CultureInfo.InvariantCulture))}";
        }));
    }

    [Fact]
    public void EveryCellOfAMergedTableGetsTheBordersItAlwaysHas() =>
        MergedBorders().Should().Be(ExpectedMergedBorders);

    private static string FlattenedPageSetups()
    {
        var results = new List<string>();
        foreach (var width in new[] { false, true })
        foreach (var height in new[] { false, true })
        foreach (var format in new[] { false, true })
            results.Add(FlattenedPageSetup(width, height, format));

        return string.Join("|", results);
    }

    private static string FlattenedPageSetup(bool width, bool height, bool format)
    {
        var document = new Document();
        var first = document.AddSection();
        first.PageSetup.PageFormat = PageFormat.A6;
        first.PageSetup.PageWidth = "11cm";
        first.PageSetup.PageHeight = "17cm";
        first.PageSetup.TopMargin = "1cm";
        first.PageSetup.MirrorMargins = true;
        first.PageSetup.SectionStart = BreakType.BreakEvenPage;

        var second = document.AddSection();
        if (width)
            second.PageSetup.PageWidth = "5cm";
        if (height)
            second.PageSetup.PageHeight = "6cm";
        if (format)
            second.PageSetup.PageFormat = PageFormat.Letter;
        second.PageSetup.LeftMargin = "3cm";

        new PdfFlattenVisitor().Visit(document);

        var s = second.PageSetup;
        return $"{Flag(width, "w")}{Flag(height, "h")}{Flag(format, "f")}: "
               + $"{s.PageWidth} x {s.PageHeight} {(s.IsNull("PageFormat") ? "?" : s.PageFormat.ToString())}"
               + $" {s.SectionStart} {s.Orientation} {s.TopMargin} {s.BottomMargin} {s.LeftMargin} {s.RightMargin}"
               + $" {s.HeaderDistance} {s.FooterDistance} {s.OddAndEvenPagesHeaderFooter} {s.DifferentFirstPageHeaderFooter}"
               + $" {s.MirrorMargins} {s.HorizontalPageBreak}";
    }

    private static string Flag(bool set, string letter) => set ? letter : "-";

    [Fact]
    public void FlatteningFillsInAPageSetupTheWayItAlwaysHas() =>
        FlattenedPageSetups().Should().Be(ExpectedPageSetups);

    private static string FlattenedWords()
    {
        var document = new Document();
        var paragraph = document.AddSection().AddParagraph();
        paragraph.AddText("one two  three\r\nfour\tfive");
        paragraph.AddFormattedText("bold");
        paragraph.AddText("self-made\u200Bzero\u00ADsoft\u00AD");
        paragraph.AddText(" -lead trail- ");
        paragraph.AddText("");

        new PdfFlattenVisitor().Visit(document);

        return string.Concat(paragraph.Elements.Cast<DocumentObject>()
            .Select(element => element is Text text ? "[" + text.Content + "]" : "<" + element.GetType().Name + ">"));
    }

    [Fact]
    public void FlatteningCutsTextIntoTheWordsItAlwaysHas() =>
        FlattenedWords().Should().Be(ExpectedWords);

    private static readonly string[] EdgeBorderNames = ["Top", "Left", "Bottom", "Right", "DiagonalDown", "DiagonalUp"];

    private static string EdgesSet(Edge edge)
    {
        var both = new List<string>();
        foreach (var colour in new[] { Color.Empty, Colors.Red })
            both.Add(EdgesSet(edge, colour));

        return string.Join("|", both);
    }

    private static string EdgesSet(Edge edge, Color colour)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        for (var c = 0; c < 4; c++)
            table.AddColumn("2cm");
        for (var r = 0; r < 4; r++)
            table.AddRow();

        table.SetEdge(1, 1, 2, 2, edge, BorderStyle.DashLargeGap, 2.5, colour);

        var set = new StringBuilder();
        for (var r = 0; r < 4; r++)
        for (var c = 0; c < 4; c++)
            AppendBordersSet(set, table[r, c], r, c);

        return set.ToString();
    }

    private static void AppendBordersSet(StringBuilder set, Cell cell, int r, int c)
    {
        if (cell.IsNull("Borders"))
            return;
        foreach (var name in EdgeBorderNames)
        {
            if (cell.Borders.IsNull(name))
                continue;
            var border = (Border)cell.Borders.GetValue(name, GV.ReadOnly);
            set.Append($"{r}{c}{name}:{border.Style}/{border.Width}/{(border.IsNull("Color") ? "-" : border.Color.RGB.ToString("X"))} ");
        }
    }

    [Theory]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Right)]
    [InlineData(Edge.Horizontal)]
    [InlineData(Edge.Vertical)]
    [InlineData(Edge.DiagonalDown)]
    [InlineData(Edge.DiagonalUp)]
    [InlineData(Edge.Box)]
    [InlineData(Edge.Interior)]
    [InlineData(Edge.Box | Edge.Interior | Edge.DiagonalDown | Edge.DiagonalUp)]
    public void SetEdgeWritesTheBordersItAlwaysHas(Edge edge) =>
        EdgesSet(edge).Should().Be(ExpectedEdges[edge]);

    private static readonly Dictionary<Edge, string> ExpectedEdges = new()
    {
        [Edge.Top] =
            "11Top:DashLargeGap/2.5/- 12Top:DashLargeGap/2.5/- |" +
            "11Top:DashLargeGap/2.5/FFFF0000 12Top:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Left] =
            "11Left:DashLargeGap/2.5/- 21Left:DashLargeGap/2.5/- |" +
            "11Left:DashLargeGap/2.5/FFFF0000 21Left:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Bottom] =
            "21Bottom:DashLargeGap/2.5/- 22Bottom:DashLargeGap/2.5/- |" +
            "21Bottom:DashLargeGap/2.5/FFFF0000 22Bottom:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Right] =
            "12Right:DashLargeGap/2.5/- 22Right:DashLargeGap/2.5/- |" +
            "12Right:DashLargeGap/2.5/FFFF0000 22Right:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Horizontal] =
            "11Bottom:DashLargeGap/2.5/- 12Bottom:DashLargeGap/2.5/- 21Top:DashLargeGap/2.5/- 22Top:DashLargeGap/2.5/- |" +
            "11Bottom:DashLargeGap/2.5/FFFF0000 12Bottom:DashLargeGap/2.5/FFFF0000 21Top:DashLargeGap/2.5/FFFF0000 22Top:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Vertical] =
            "11Right:DashLargeGap/2.5/- 12Left:DashLargeGap/2.5/- 21Right:DashLargeGap/2.5/- 22Left:DashLargeGap/2.5/- |" +
            "11Right:DashLargeGap/2.5/FFFF0000 12Left:DashLargeGap/2.5/FFFF0000 21Right:DashLargeGap/2.5/FFFF0000 22Left:DashLargeGap/2.5/FFFF0000 ",
        [Edge.DiagonalDown] =
            "11DiagonalDown:DashLargeGap/2.5/- 12DiagonalDown:DashLargeGap/2.5/- 21DiagonalDown:DashLargeGap/2.5/- 22DiagonalDown:DashLargeGap/2.5/- |" +
            "11DiagonalDown:DashLargeGap/2.5/FFFF0000 12DiagonalDown:DashLargeGap/2.5/FFFF0000 21DiagonalDown:DashLargeGap/2.5/FFFF0000 22DiagonalDown:DashLargeGap/2.5/FFFF0000 ",
        [Edge.DiagonalUp] =
            "11DiagonalUp:DashLargeGap/2.5/- 12DiagonalUp:DashLargeGap/2.5/- 21DiagonalUp:DashLargeGap/2.5/- 22DiagonalUp:DashLargeGap/2.5/- |" +
            "11DiagonalUp:DashLargeGap/2.5/FFFF0000 12DiagonalUp:DashLargeGap/2.5/FFFF0000 21DiagonalUp:DashLargeGap/2.5/FFFF0000 22DiagonalUp:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Box] =
            "11Top:DashLargeGap/2.5/- 11Left:DashLargeGap/2.5/- 12Top:DashLargeGap/2.5/- 12Right:DashLargeGap/2.5/- 21Left:DashLargeGap/2.5/- 21Bottom:DashLargeGap/2.5/- 22Bottom:DashLargeGap/2.5/- 22Right:DashLargeGap/2.5/- |" +
            "11Top:DashLargeGap/2.5/FFFF0000 11Left:DashLargeGap/2.5/FFFF0000 12Top:DashLargeGap/2.5/FFFF0000 12Right:DashLargeGap/2.5/FFFF0000 21Left:DashLargeGap/2.5/FFFF0000 21Bottom:DashLargeGap/2.5/FFFF0000 22Bottom:DashLargeGap/2.5/FFFF0000 22Right:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Interior] =
            "11Bottom:DashLargeGap/2.5/- 11Right:DashLargeGap/2.5/- 12Left:DashLargeGap/2.5/- 12Bottom:DashLargeGap/2.5/- 21Top:DashLargeGap/2.5/- 21Right:DashLargeGap/2.5/- 22Top:DashLargeGap/2.5/- 22Left:DashLargeGap/2.5/- |" +
            "11Bottom:DashLargeGap/2.5/FFFF0000 11Right:DashLargeGap/2.5/FFFF0000 12Left:DashLargeGap/2.5/FFFF0000 12Bottom:DashLargeGap/2.5/FFFF0000 21Top:DashLargeGap/2.5/FFFF0000 21Right:DashLargeGap/2.5/FFFF0000 22Top:DashLargeGap/2.5/FFFF0000 22Left:DashLargeGap/2.5/FFFF0000 ",
        [Edge.Box | Edge.Interior | Edge.DiagonalDown | Edge.DiagonalUp] =
            "11Top:DashLargeGap/2.5/- 11Left:DashLargeGap/2.5/- 11Bottom:DashLargeGap/2.5/- 11Right:DashLargeGap/2.5/- 11DiagonalDown:DashLargeGap/2.5/- 11DiagonalUp:DashLargeGap/2.5/- 12Top:DashLargeGap/2.5/- 12Left:DashLargeGap/2.5/- 12Bottom:DashLargeGap/2.5/- 12Right:DashLargeGap/2.5/- 12DiagonalDown:DashLargeGap/2.5/- 12DiagonalUp:DashLargeGap/2.5/- 21Top:DashLargeGap/2.5/- 21Left:DashLargeGap/2.5/- 21Bottom:DashLargeGap/2.5/- 21Right:DashLargeGap/2.5/- 21DiagonalDown:DashLargeGap/2.5/- 21DiagonalUp:DashLargeGap/2.5/- 22Top:DashLargeGap/2.5/- 22Left:DashLargeGap/2.5/- 22Bottom:DashLargeGap/2.5/- 22Right:DashLargeGap/2.5/- 22DiagonalDown:DashLargeGap/2.5/- 22DiagonalUp:DashLargeGap/2.5/- |" +
            "11Top:DashLargeGap/2.5/FFFF0000 11Left:DashLargeGap/2.5/FFFF0000 11Bottom:DashLargeGap/2.5/FFFF0000 11Right:DashLargeGap/2.5/FFFF0000 11DiagonalDown:DashLargeGap/2.5/FFFF0000 11DiagonalUp:DashLargeGap/2.5/FFFF0000 12Top:DashLargeGap/2.5/FFFF0000 12Left:DashLargeGap/2.5/FFFF0000 12Bottom:DashLargeGap/2.5/FFFF0000 12Right:DashLargeGap/2.5/FFFF0000 12DiagonalDown:DashLargeGap/2.5/FFFF0000 12DiagonalUp:DashLargeGap/2.5/FFFF0000 21Top:DashLargeGap/2.5/FFFF0000 21Left:DashLargeGap/2.5/FFFF0000 21Bottom:DashLargeGap/2.5/FFFF0000 21Right:DashLargeGap/2.5/FFFF0000 21DiagonalDown:DashLargeGap/2.5/FFFF0000 21DiagonalUp:DashLargeGap/2.5/FFFF0000 22Top:DashLargeGap/2.5/FFFF0000 22Left:DashLargeGap/2.5/FFFF0000 22Bottom:DashLargeGap/2.5/FFFF0000 22Right:DashLargeGap/2.5/FFFF0000 22DiagonalDown:DashLargeGap/2.5/FFFF0000 22DiagonalUp:DashLargeGap/2.5/FFFF0000 ",
    };

    private static readonly string[] ExpectedEveryAttribute =
    [
        "\\document",
        "{",
        "  \\styles",
        "  {",
        "    Normal",
        "    {",
        "      Font",
        "      {",
        "        Name = \"Arial\"",
        "      }",
        "      ParagraphFormat",
        "      {",
        "        LeftIndent = \"1cm\"",
        "      }",
        "    }",
        "",
        "    Heading1",
        "    {",
        "      Font",
        "      {",
        "        Bold = true",
        "      }",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level1",
        "      }",
        "    }",
        "",
        "    Heading2",
        "    {",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level2",
        "      }",
        "    }",
        "",
        "    Heading3",
        "    {",
        "      Font",
        "      {",
        "        Name = \"Arial\"",
        "      }",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level3",
        "      }",
        "    }",
        "",
        "    Heading4 : Normal",
        "    {",
        "      Font",
        "      {",
        "        Italic = true",
        "      }",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level4",
        "      }",
        "    }",
        "",
        "    Heading5",
        "    {",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level5",
        "      }",
        "    }",
        "",
        "    Heading6",
        "    {",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level6",
        "      }",
        "    }",
        "",
        "    Heading7",
        "    {",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level7",
        "      }",
        "    }",
        "",
        "    Heading8",
        "    {",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level8",
        "      }",
        "    }",
        "",
        "    Heading9",
        "    {",
        "      ParagraphFormat",
        "      {",
        "        OutlineLevel = Level9",
        "      }",
        "    }",
        "",
        "    InvalidStyleName",
        "    {",
        "      Font",
        "      {",
        "        Bold = true",
        "        Underline = Dash",
        "        Color = Lime",
        "      }",
        "    }",
        "",
        "    \"Loud Style\" : Normal",
        "    {",
        "      Font",
        "      {",
        "        Name = \"Courier\"",
        "        Size = 13",
        "        Italic = true",
        "        Strikethrough = Single",
        "        Superscript = true",
        "        Color = Red",
        "      }",
        "      ParagraphFormat",
        "      {",
        "        Alignment = Right",
        "        FirstLineIndent = \"-5mm\"",
        "        RightIndent = \"2cm\"",
        "        SpaceBefore = 3",
        "        SpaceAfter = 4",
        "        LineSpacingRule = Exactly",
        "        LineSpacing = 14",
        "        KeepTogether = true",
        "        TextDirection = RightToLeft",
        "        WidowControl = false",
        "        PageBreakBefore = true",
        "        OutlineLevel = Level2",
        "        ListInfo.ListType = NumberList2",
        "        TabStops +=",
        "        {",
        "          Position = \"3cm\"",
        "          Alignment = Center",
        "          Leader = Dots",
        "        }",
        "        Borders",
        "        {",
        "          Width = 2",
        "          Color = Blue",
        "        }",
        "        Shading",
        "        {",
        "          Color = Yellow",
        "        }",
        "      }",
        "    }",
        "",
        "    Mark : DefaultParagraphFont",
        "    {",
        "      Font",
        "      {",
        "        Subscript = true",
        "        Color = Green",
        "      }",
        "    }",
        "  }",
        "  \\section",
        "  [",
        "    // the setup",
        "    PageSetup",
        "    {",
        "      PageHeight = \"20cm\"",
        "      PageWidth = \"15cm\"",
        "      Orientation = Landscape",
        "      LeftMargin = \"1cm\"",
        "      RightMargin = \"1.5cm\"",
        "      TopMargin = \"2cm\"",
        "      BottomMargin = \"2.5cm\"",
        "      FooterDistance = \"0.5cm\"",
        "      HeaderDistance = \"0.7cm\"",
        "      OddAndEvenPagesHeaderFooter = true",
        "      DifferentFirstPageHeaderFooter = false",
        "      SectionStart = BreakOddPage",
        "      PageFormat = A5",
        "      MirrorMargins = true",
        "      HorizontalPageBreak = false",
        "      StartingNumber = 5",
        "    }",
        "  ]",
        "  {",
        "    \\paragraph",
        "    [",
        "      Style = \"Loud Style\"",
        "      Format",
        "      {",
        "        Font",
        "        {",
        "          Name = \"Times\"",
        "          Bold = true",
        "        }",
        "        Borders",
        "        {",
        "          Visible = true",
        "          Style = DashDot",
        "          Width = 1.5",
        "          Color = Maroon",
        "          DistanceFromTop = 1",
        "          DistanceFromBottom = 2",
        "          DistanceFromLeft = 3",
        "          DistanceFromRight = 4",
        "          Top",
        "          {",
        "            Width = 1",
        "          }",
        "          Left",
        "          {",
        "            Color = Olive",
        "          }",
        "          Bottom",
        "          {",
        "            Style = Dot",
        "          }",
        "          Right",
        "          {",
        "            Visible = false",
        "          }",
        "          DiagonalDown",
        "          {",
        "            Width = 0.25",
        "          }",
        "          DiagonalUp",
        "          {",
        "            Width = 0.5",
        "          }",
        "        }",
        "      }",
        "    ]",
        "    {",
        "      plain\\fontsize(9){sized}\\bold{bold}\\font[Bold = false",
        "      ]{notbold}\\italic{italic}\\font[Italic = false",
        "      ]{notitalic}\\fontcolor(Purple){coloured}\\font[Underline = Words",
        "      ]{underlined}\\font[Name = \"Verdana\"",
        "      Size = 7",
        "      Subscript = true",
        "      ]{several}\\font[Bold = true",
        "      Italic = true",
        "      ]{emptyname}\\font(\"Mark\")[Bold = true",
        "      ]{styled}\\font(\"Mark\")[Size = 8",
        "      ]{styledonly}",
        "    }",
        "    \\paragraph",
        "    [",
        "      Format",
        "      {",
        "        Borders = null",
        "        Borders",
        "        {",
        "          Width = 3",
        "        }",
        "      }",
        "    ]",
        "    {",
        "      cleared",
        "    }",
        "    \\chart(Column2D)",
        "    [",
        "      DisplayBlanksAs = Zero",
        "      PivotChart = true",
        "      HasDataLabel = true",
        "      Style = \"Loud Style\"",
        "      Format",
        "      {",
        "        Alignment = Center",
        "      }",
        "      DataLabel",
        "      {",
        "        Type = Value",
        "      }",
        "    ]",
        "    {",
        "      \\plotarea",
        "      [",
        "        LeftPadding = 3",
        "      ]",
        "      {",
        "      }",
        "      \\headerarea",
        "      {",
        "        head",
        "      }",
        "      \\footerarea",
        "      {",
        "        foot",
        "      }",
        "      \\toparea",
        "      {",
        "        top",
        "      }",
        "      \\bottomarea",
        "      {",
        "        bottom",
        "      }",
        "      \\leftarea",
        "      {",
        "        left",
        "      }",
        "      \\rightarea",
        "      {",
        "        right",
        "      }",
        "      \\xaxis",
        "      [",
        "        HasMajorGridLines = true",
        "      ]",
        "      \\yaxis",
        "      [",
        "        MinimumScale = 1",
        "      ]",
        "      \\zaxis",
        "      [",
        "        HasMajorGridLines = false",
        "      ]",
        "      \\series",
        "      {",
        "        1, 2, ",
        "      }",
        "      \\xvalues",
        "      {",
        "        \"a\", \"b\", ",
        "      }",
        "    }",
        "  }",
        "}",
        "",
    ];

    private const string ExpectedMergedBorders =
        "0,0: T- L- B1.5 R1.5 W?|" +
        "0,1: T1.5 L1.5 B- R3.5 W?|" +
        "0,3: T0.5 L3.5 B0.5 Rh? W?|" +
        "1,0: T1.5 L0.5 B4 R2 W?|" +
        "1,1: T? L2 B0.5 R2.5 W?|" +
        "1,2: T0.5 L2.5 B2.5 R3 W?|" +
        "2,1: T0.5 L2 B1.5 R2.5 W?|" +
        "3,0: T5 L0.5 B3 R1.5 W?|" +
        "3,1: T1.5 L1.5 B- R3 W?|" +
        "3,2: T2.5 L3 B5 R4 W?|" +
        "3,3: T- L- B- R- W4";

    private const string ExpectedPageSetups =
        "---: 11cm x 17cm A6 BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "--f: 612 x 792 Letter BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "-h-: 11cm x 6cm ? BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "-hf: 612 x 6cm Letter BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "w--: 5cm x 17cm ? BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "w-f: 5cm x 792 Letter BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "wh-: 5cm x 6cm ? BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False|" +
        "whf: 5cm x 6cm Letter BreakEvenPage Portrait 1cm 2cm 3cm 2.5cm 1.25cm 1.25cm False False True False";

    private const string ExpectedWords =
        "[one][ ][two][ ][ ][three][ ]" +
        "<Character>[four]" +
        "<Character>[five]" +
        "<FormattedText>[self-][made\u200B][zero][\u00AD][soft][\u00AD][ ][-][lead][ ][trail-][ ]";
}
