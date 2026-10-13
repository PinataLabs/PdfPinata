using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using ImageMagick;
using PinataLayout.DocumentObjectModel;
using PinataLayout.DocumentObjectModel.Tables;
using PinataLayout.Rendering;
using PdfPinata.Test.Helpers;
using TUnit.Core;

namespace PdfPinata.Test.Rendering;

/// <summary>
///   That the rule two cells share is no heavier on the page than the rule round the outside of
///   the table, which only one cell draws.
/// </summary>
/// <remarks>
///   Each cell used to stroke all four of its edges, so the rule between two cells was stroked
///   twice, once by each, in the same place with the same pen. That reads as harmless in the
///   content stream and is not on the page: a viewer blends the partly covered pixels along each
///   side of a stroke, and blending them twice darkens them, so every rule inside a table came out
///   heavier than the rules round it. See empira/PDFsharp#408.
///
///   The grid below is laid out so that every rule falls at the same fraction of a pixel - the
///   columns are 60 points apart and the rows 30, both whole numbers of pixels at 300 dpi - which
///   leaves nothing but the number of strokes to make one rule weigh more than another. The cells
///   are shaded, as in the report, because the shading of the second cell is painted between the
///   two strokes.
/// </remarks>
[Rasterizing]
public sealed class TableSharedBorderRenderingTests : IDisposable
{
    private const string OutDir = "Out/TableSharedBorders";

    private const int Size = 3;
    private const double ColumnWidth = 60;
    private const double RowHeight = 29;
    private const double BorderWidth = 1;

    private readonly Rasterizations _rasterized = new(OutDir);

    public void Dispose() => _rasterized.Dispose();

    [Test, GoldenImage]
    public void ARuleBetweenTwoRowsIsAsHeavyAsTheRulesAboveAndBelowTheTable()
    {
        var page = Render("rows");

        // Down the middle of the second column, well clear of the rules running across it.
        var rules = RulesAlong(page, x: TableLeft(page) + ColumnWidth * 1.5, vertical: true);

        rules.Should().HaveCount(Size + 1);
        rules.Should().AllSatisfy(rule => rule.Should().Be(rules[0],
            "a rule between two rows is drawn once, like the rules at the top and the foot"));
    }

    [Test, GoldenImage]
    public void ARuleBetweenTwoColumnsIsAsHeavyAsTheRulesEitherSideOfTheTable()
    {
        var page = Render("columns");

        // Across the middle of the second row.
        var rules = RulesAlong(page, y: TableTop(page) + RowHeight * 1.5, vertical: false);

        rules.Should().HaveCount(Size + 1);
        rules.Should().AllSatisfy(rule => rule.Should().Be(rules[0],
            "a rule between two columns is drawn once, like the rules at either side"));
    }

    private IMagickImage<byte> Render(string name)
    {
        var document = new Document();
        var table = document.AddSection().AddTable();
        for (var column = 0; column < Size; column++)
            table.AddColumn(Unit.FromPoint(ColumnWidth));

        for (var r = 0; r < Size; r++)
        {
            var row = table.AddRow();
            row.Height = Unit.FromPoint(RowHeight);
            row.HeightRule = RowHeightRule.Exactly;
            foreach (Cell cell in row.Cells)
            {
                cell.Shading.Color = Colors.White;
                cell.Borders.Color = Colors.Red;
                cell.Borders.Width = BorderWidth;
            }
        }

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        return _rasterized.FirstPageOf(renderer.PdfDocument, name);
    }

    /// <summary>
    ///   Each rule crossed walking down the page at <paramref name="x"/>, or across it at
    ///   <paramref name="y"/>, as the ink of every pixel it covers: how far each one is from white
    ///   in the green channel, which a red rule on white takes away and leaves the red alone.
    /// </summary>
    private static List<string> RulesAlong(IMagickImage<byte> image, double x = 0, double y = 0, bool vertical = true)
    {
        var scale = PageInk.ScaleOf(image);
        var fixedAt = (int)((vertical ? x : y) * scale);
        var length = vertical ? image.Height : image.Width;

        var rules = new List<string>();
        var rule = new List<int>();
        using var pixels = image.GetPixels();
        for (var i = 0; i < length; i++)
        {
            var colour = (vertical ? pixels.GetPixel(fixedAt, i) : pixels.GetPixel(i, fixedAt)).ToColor()!;
            var ink = 255 - colour.G;
            if (ink > 0)
            {
                rule.Add(ink);
                continue;
            }

            if (rule.Count == 0)
                continue;

            rules.Add(string.Join(",", rule));
            rule.Clear();
        }

        return rules;
    }

    /// <summary>Where the table starts across the page, in points: the first column of red.</summary>
    private static double TableLeft(IMagickImage<byte> image) => FirstRed(image).X / PageInk.ScaleOf(image);

    /// <summary>Where the table starts down the page, in points: the first row of red.</summary>
    private static double TableTop(IMagickImage<byte> image) => FirstRed(image).Y / PageInk.ScaleOf(image);

    private static (int X, int Y) FirstRed(IMagickImage<byte> image)
    {
        using var pixels = image.GetPixels();
        var red = pixels
            .Where(pixel => pixel.ToColor() is { R: > 200, G: < 100, B: < 100 })
            .Select(pixel => (pixel.X, pixel.Y))
            .ToList();

        red.Should().NotBeEmpty();
        return (red.Min(p => p.X), red.Min(p => p.Y));
    }
}
