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
///   The rows a table repeats onto its later pages are the marked rows at its top, running
///   unbroken from the first row. A row marked anywhere else used to be discarded without a
///   word, so a document that asked for a repeating heading and did not get one looked exactly
///   like a document that never asked. It now says so.
/// </summary>
public class TableHeadingRowTests
{
    [Fact]
    public void ATitleBandAboveTheColumnNamesIsRefused()
    {
        // The natural mistake: a title spanning the table, and the column names below it marked
        // as the heading. The heading is not at the top, so nothing repeats.
        var render = () => Render(table =>
        {
            AddRows(table, 3);
            table.Rows[1].HeadingFormat = true;
        });

        render.Should().Throw<InvalidOperationException>()
            .WithMessage("*row 1*")
            .WithMessage("*first row*");
    }

    [Fact]
    public void AGapInTheRunIsRefused()
    {
        var render = () => Render(table =>
        {
            AddRows(table, 4);
            table.Rows[0].HeadingFormat = true;
            table.Rows[2].HeadingFormat = true;
        });

        render.Should().Throw<InvalidOperationException>().WithMessage("*row 2*");
    }

    [Fact]
    public void AHeadingMarkedInTheBodyIsRefused()
    {
        var render = () => Render(table =>
        {
            AddRows(table, 12);
            table.Rows[7].HeadingFormat = true;
        });

        render.Should().Throw<InvalidOperationException>().WithMessage("*row 7*");
    }

    [Fact]
    public void NothingIsWrittenToTheStreamBeforeTheThrow()
    {
        var document = new Document();
        var table = NewTable(document);
        AddRows(table, 3);
        table.Rows[1].HeadingFormat = true;

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        using var stream = new MemoryStream();

        var render = () =>
        {
            renderer.RenderDocument();
            renderer.PdfDocument.Save(stream, false);
        };

        // Formatting comes first, so the caller is never handed half a document.
        render.Should().Throw<InvalidOperationException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public void AHeadingRunFromTheFirstRowIsAccepted()
    {
        var render = () => Render(table =>
        {
            AddRows(table, 4);
            table.Rows[0].HeadingFormat = true;
            table.Rows[1].HeadingFormat = true;
        });

        render.Should().NotThrow();
    }

    [Fact]
    public void OneHeadingRowIsRepeatedOnEachLaterPage()
    {
        var pages = Render(table =>
        {
            AddRows(table, 12);
            table.Rows[0].HeadingFormat = true;
        });

        pages.Count.Should().BeGreaterThan(1);
        RowsDrawnOn(pages).Should().Be(12 + (pages.Count - 1));
    }

    [Fact]
    public void TwoHeadingRowsAreRepeatedTogether()
    {
        var pages = Render(table =>
        {
            AddRows(table, 12);
            table.Rows[0].HeadingFormat = true;
            table.Rows[1].HeadingFormat = true;
        });

        pages.Count.Should().BeGreaterThan(1);
        RowsDrawnOn(pages).Should().Be(12 + 2 * (pages.Count - 1));
    }

    [Fact]
    public void ATableWithNoHeadingRowsRepeatsNothing()
    {
        var pages = Render(table => AddRows(table, 12));

        pages.Count.Should().BeGreaterThan(1);
        RowsDrawnOn(pages).Should().Be(12);
    }

    [Fact]
    public void ATableThatIsEntirelyHeadingRepeatsNothing()
    {
        // A heading that is the whole table has nothing to head, so the existing rule discards
        // it — and every row carrying the flag is inside the run, so nothing is refused either.
        var pages = Render(table =>
        {
            AddRows(table, 12);
            foreach (Row row in table.Rows)
                row.HeadingFormat = true;
        });

        pages.Count.Should().BeGreaterThan(1);
        RowsDrawnOn(pages).Should().Be(12);
    }

    /// <summary>How many rows the whole document shows, headings counted once per page drawn.</summary>
    static int RowsDrawnOn(IReadOnlyList<PdfPage> pages)
    {
        return pages.Sum(RowsOn);
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

    /// <summary>Rows tall enough that a dozen of them cannot fit on one page.</summary>
    static void AddRows(Table table, int count)
    {
        for (var index = 0; index < count; index++)
        {
            var row = table.AddRow();
            row.Height = Unit.FromCentimeter(3);
            row.HeightRule = RowHeightRule.Exactly;
            row[0].AddParagraph($"Row {row.Index}");
        }
    }

    static Table NewTable(Document document)
    {
        var table = document.AddSection().AddTable();
        table.Borders.Visible = true;
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(5));
        return table;
    }

    static IReadOnlyList<PdfPage> Render(Action<Table> build)
    {
        var document = new Document();
        build(NewTable(document));

        var renderer = new PdfDocumentRenderer(true) { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        stream.Position = 0;

        var rendered = Pdf.IO.PdfReader.Open(stream, PdfDocumentOpenMode.Modify);
        return Enumerable.Range(0, rendered.PageCount).Select(index => rendered.Pages[index]).ToList();
    }
}
