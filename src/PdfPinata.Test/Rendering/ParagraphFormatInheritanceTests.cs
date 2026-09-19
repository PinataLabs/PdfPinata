using System.Collections.Generic;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   Flattening a paragraph format handed on the borders of the format it inherited from rather
///   than a copy of them, unlike the font and the shading beside them. A cell's format is
///   flattened twice over, once against its row and once against its column, so the second pass
///   wrote the column's paragraph borders into the row's format and every cell of the row read
///   them back out.
/// </summary>
public class ParagraphFormatInheritanceTests
{
    const int Columns = 3;

    [Fact]
    public void AParagraphBorderSetOnOneColumnIsDrawnInThatColumnAlone()
    {
        var page = Render(ARuleDownTheFirstColumnAndABandAcrossTheRow);

        // The rule belongs to the first column, so it is drawn once and no further right than
        // the column it was set on.
        var verticals = Distinct(StrokedLines.Of(page).Where(line => line.IsVertical));
        var bands = Distinct(StrokedLines.Of(page).Where(line => line.IsHorizontal));

        verticals.Should().ContainSingle();
        verticals[0].Should().BeLessThan(bands.Skip(1).First());
    }

    [Fact]
    public void AParagraphBorderSetOnTheRowIsStillDrawnInEveryCell()
    {
        var page = Render(ARuleDownTheFirstColumnAndABandAcrossTheRow);

        // The band is set on the row, so each of the three cells draws its own stretch of it.
        var bands = StrokedLines.Of(page).Where(line => line.IsHorizontal).ToList();
        bands.Should().HaveCount(Columns);
    }

    [Fact]
    public void ACellDoesNotShareItsParagraphBordersWithItsRow()
    {
        var document = new Document();
        var table = ARuleDownTheFirstColumnAndABandAcrossTheRow(document);
        Render(document);

        var row = table.Rows[0];
        for (var column = 0; column < Columns; column++)
            table[0, column].Format.Borders.Should().NotBeSameAs(row.Format.Borders);

        var borders = Enumerable.Range(0, Columns).Select(c => table[0, c].Format.Borders);
        borders.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    ///   A red rule down the right of the paragraphs of the first column, and a green band over
    ///   the paragraphs of every cell of the row.
    /// </summary>
    static Table ARuleDownTheFirstColumnAndABandAcrossTheRow(Document document)
    {
        var table = document.AddSection().AddTable();

        for (var column = 0; column < Columns; column++)
            table.AddColumn(Unit.FromCentimeter(4));

        table.Columns[0].Format.Borders.Right.Color = Colors.Red;
        table.Columns[0].Format.Borders.Right.Width = 1;

        var row = table.AddRow();
        row.Format.Borders.Top.Color = Colors.Green;
        row.Format.Borders.Top.Width = 1;

        for (var column = 0; column < Columns; column++)
            row.Cells[column].AddParagraph($"Cell {column}");

        return table;
    }

    static IReadOnlyList<double> Distinct(IEnumerable<StrokedLines.Line> lines)
    {
        return lines.Select(line => System.Math.Round(System.Math.Min(line.X1, line.X2), 2))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    static PdfPage Render(System.Func<Document, Table> build)
    {
        var document = new Document();
        build(document);
        return Render(document);
    }

    static PdfPage Render(Document document)
    {
        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        return Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify).Pages[0];
    }
}
