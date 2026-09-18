using System;
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
///   A row asks to be kept with the rows below it, and a table built a row at a time cannot know
///   while it is being built how many rows are still to come. The last rows of such a table are
///   left asking to be kept with rows that never arrive, and the renderer used to follow that
///   past the end of the table and fail looking for the row after the last one.
///   See https://github.com/empira/PDFsharp/issues/250.
/// </summary>
public class TableKeepWithTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public void ARowMayAskToBeKeptWithMoreRowsThanFollowIt(int keepWith)
    {
        // The reported document: two rows, each asking to be kept with one more than there is.
        var pages = Render(table =>
        {
            for (var index = 0; index < 2; index++)
            {
                var row = table.AddRow();
                row.KeepWith = keepWith;
                row[0].AddParagraph($"Row {row.Index}");
            }
        });

        pages.Should().ContainSingle();
        RowsOn(pages[0]).Should().Be(2);
    }

    [Fact]
    public void ARowKeptWithTheRowsThatDoFollowItStillHoldsThemTogether()
    {
        // Three rows, only two of which fit on a page, with the second asking to be kept with
        // the third. Breaking after the first is the only way to honour that.
        var pages = Render(table =>
        {
            for (var index = 0; index < 3; index++)
            {
                var row = table.AddRow();
                row.Height = Unit.FromCentimeter(9);
                row.HeightRule = RowHeightRule.Exactly;
                row[0].AddParagraph($"Row {row.Index}");
            }
            table.Rows[1].KeepWith = 1;
        });

        pages.Should().HaveCount(2);
        RowsOn(pages[0]).Should().Be(1);
        RowsOn(pages[1]).Should().Be(2);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void ARowKeptWithMoreRowsThanFollowItStillHoldsOnToTheOnesThatDo(int keepWith)
    {
        // The same three rows, but the second asks for more than follow it. Clamping the request
        // to the rows that exist must not lose the one that does follow it, however large the
        // request — a row asking for int.MaxValue more is asking for all of them, not for none.
        var pages = Render(table =>
        {
            for (var index = 0; index < 3; index++)
            {
                var row = table.AddRow();
                row.Height = Unit.FromCentimeter(9);
                row.HeightRule = RowHeightRule.Exactly;
                row[0].AddParagraph($"Row {row.Index}");
            }
            table.Rows[1].KeepWith = keepWith;
        });

        pages.Should().HaveCount(2);
        RowsOn(pages[0]).Should().Be(1);
        RowsOn(pages[1]).Should().Be(2);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    public void AColumnMayAskToBeKeptWithMoreColumnsThanStandToTheRightOfIt(int keepWith)
    {
        var pages = Render(table =>
        {
            table.Columns[0].KeepWith = keepWith;
            table.Columns[1].KeepWith = keepWith;
            table.AddRow()[0].AddParagraph("Only row");
        });

        pages.Should().ContainSingle();
        RowsOn(pages[0]).Should().Be(1);
    }

    [Fact]
    public void AHeadingRowMayAskToBeKeptWithMoreRowsThanFollowIt()
    {
        // A heading is grown to the last row connected to it, which is the other route to the
        // same reading past the end of the table.
        var pages = Render(table =>
        {
            var heading = table.AddRow();
            heading.HeadingFormat = true;
            heading.KeepWith = 4;
            heading[0].AddParagraph("Heading");
            table.AddRow()[0].AddParagraph("Body");
        });

        pages.Should().ContainSingle();
        RowsOn(pages[0]).Should().Be(2);
    }

    /// <summary>
    ///   How many rows of the table the page shows, counted from the rules between them: a table
    ///   of n rows is ruled n + 1 times across.
    /// </summary>
    static int RowsOn(PdfPage page)
    {
        var rules = StrokedLines.Of(page)
            .Where(line => line.IsHorizontal)
            .Select(line => Math.Round(line.Y1, 2))
            .Distinct()
            .Count();

        return rules - 1;
    }

    static IReadOnlyList<PdfPage> Render(Action<Table> build)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(5));
        build(table);

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        var rendered = Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        return Enumerable.Range(0, rendered.PageCount).Select(index => rendered.Pages[index]).ToList();
    }
}
