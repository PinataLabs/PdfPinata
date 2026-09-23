using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Charting;
using PdfPinata.Drawing;
using PdfPinata.Pdf;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Test.Charting;

/// <summary>
///   What the charting renderers actually write, read back out of the page they wrote it to.
/// </summary>
/// <remarks>
///   <para>
///     The two defects fixed here - a combination chart's legend printed on top of itself, and a pie
///     signing and scaling its percentages twice - were found by drawing the demonstration app's
///     Charts demo and looking at it. Neither threw, neither was reachable from the chart's own
///     properties afterwards, and the only symptom of either was a picture that was wrong.
///   </para>
///   <para>
///     The axis tests below record a third, C1 of <c>docs/specs/charting-renderer-findings.md</c>,
///     which was found independently and fixed there. They are kept because they came at it from the
///     other direction - a chart drawn as a caller draws one, asked only whether its data reached
///     the page - and because that is the invariant most worth not losing again.
///   </para>
///   <para>
///     The text cannot be read back as text: fonts are embedded as Identity-H by default, so a
///     show-text operator carries glyph identifiers rather than characters. Comparing sequences
///     sidesteps that - the identifiers are the face's own, so the same characters in the same font
///     always produce the same numbers, and a test says what a page should read by drawing that
///     text and comparing. Same technique as PinataLayout.Rendering.Tests' Glyphs helper.
///   </para>
/// </remarks>
public class ChartRenderingTests
{
    private const string Face = "Liberation Sans";
    private const double Size = 8;

    /// <summary>Draws one chart into a page of a known size and hands the page back.</summary>
    private static PdfPage Drawn(Chart chart, double width = 400, double height = 260)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = width + 40;
        page.Height = height + 40;

        using var gfx = XGraphics.FromPdfPage(page);
        var frame = new ChartFrame
        {
            Location = new XPoint(20, 20),
            Size = new XSize(width, height)
        };
        frame.Add(chart);
        frame.DrawChart(gfx);

        return page;
    }

    private static Chart Quarterly(ChartType type, params double[][] series)
    {
        var chart = new Chart(type)
        {
            Font = { Name = Face, Size = Size }
        };
        chart.XValues.AddXSeries().Add("Q1", "Q2", "Q3", "Q4");

        var names = new[] { "North", "South", "West" };
        for (var index = 0; index < series.Length; index++)
        {
            var values = chart.SeriesCollection.AddSeries();
            values.Name = names[index];
            values.Add(series[index]);
        }

        chart.Legend.Docking = DockingType.Bottom;
        return chart;
    }

    // ----- reading glyphs back -----

    /// <summary>Two bytes per glyph, one list per run of text, in the order they were drawn.</summary>
    private static IReadOnlyList<IReadOnlyList<int>> RunsOn(PdfPage page)
    {
        return [..TextOperators.ShownStrings(page)
            .Select(run => (IReadOnlyList<int>)Enumerable
                .Range(0, run.Length / 2)
                .Select(index => (run[index * 2] << 8) | run[index * 2 + 1])
                .ToList())];
    }

    /// <summary>The glyphs the given text draws in the font the charts above are set in.</summary>
    private static IReadOnlyList<int> GlyphsFor(string text)
    {
        var document = new PdfDocument();
        var page = document.AddPage();

        using (var gfx = XGraphics.FromPdfPage(page))
            gfx.DrawString(text, new XFont(Face, Size), XBrushes.Black, new XPoint(20, 20));

        return [..RunsOn(page).SelectMany(run => run)];
    }

    private static bool Shows(PdfPage page, string text)
    {
        var wanted = GlyphsFor(text);
        return RunsOn(page).Any(run => run.SequenceEqual(wanted));
    }

    /// <summary>Where the run reading as the given text starts, in points from the page's left.</summary>
    private static double XOf(PdfPage page, string text)
    {
        var wanted = GlyphsFor(text);
        var runs = RunsOn(page);
        var positions = TextBaselines.PositionsOf(page);

        for (var index = 0; index < Math.Min(runs.Count, positions.Count); index++)
        {
            if (runs[index].SequenceEqual(wanted))
                return positions[index].X;
        }

        throw new InvalidOperationException($"The page does not show '{text}'.");
    }

    /// <summary>The page's content stream, as written, before anything tries to parse it.</summary>
    private static string ContentOf(PdfPage page)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < page.Contents.Elements.Count; index++)
        {
            builder.Append(Encoding.ASCII.GetString(
                page.Contents.Elements.GetDictionary(index).Stream.UnfilteredValue));
        }

        return builder.ToString();
    }

    // ----- an axis nobody asked for -----

    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.ColumnStacked2D)]
    [InlineData(ChartType.Bar2D)]
    [InlineData(ChartType.BarStacked2D)]
    [InlineData(ChartType.Area2D)]
    public void AChartWhoseAxesWereNeverTouchedStillPlotsItsData(ChartType type)
    {
        // Chart.XAxis creates the axis the first time it is read, so a chart nothing configured has
        // none, and the category axis renderer used to calculate its scale inside the null check
        // that asks. The scale stayed at the zero it was constructed with, the plot area divided
        // its own width by that, and the whole area was written as NaN from end to end.
        //
        // Nothing complained. The document saved, opened and rasterized, because a reader handed an
        // operand it cannot parse abandons the path and paints nothing. Recorded as C1 of
        // docs/specs/charting-renderer-findings.md, which fixed it by calculating the scale outside
        // the check rather than by creating an axis nobody asked for - see the test below.
        var chart = Quarterly(type, [42.0, 58, 51, 73], [31.0, 29, 44, 38]);

        ContentOf(Drawn(chart)).Should().NotContain("NaN",
            "a coordinate that is not a number is not a coordinate");
    }

    [Theory]
    [InlineData(ChartType.Line)]
    [InlineData(ChartType.Column2D)]
    [InlineData(ChartType.Bar2D)]
    public void ReadingTheAxisAddsItsLabellingAndNothingElse(ChartType type)
    {
        // The other half of it. Merely *reading* chart.XAxis used to be the difference between a
        // chart that drew its data and one that did not, which is not something any caller could be
        // expected to discover.
        //
        // The two pages are still not identical, and should not be: reading the property creates an
        // axis, and an axis that exists is labelled. What must not differ is whether the data was
        // plotted at all. So the untouched chart draws the same picture with less writing on it -
        // more content in the touched one, and no NaN in either.
        var untouched = Quarterly(type, [42.0, 58, 51, 73], [31.0, 29, 44, 38]);

        var touched = Quarterly(type, [42.0, 58, 51, 73], [31.0, 29, 44, 38]);
        _ = touched.XAxis;
        _ = touched.YAxis;

        var bare = ContentOf(Drawn(untouched));
        var labelled = ContentOf(Drawn(touched));

        bare.Should().NotContain("NaN");
        labelled.Should().NotContain("NaN");
        labelled.Length.Should().BeGreaterThan(bare.Length,
            "an axis that exists carries tick labels, and one that was never asked for does not");
    }

    // ----- the legend of a combination chart -----

    [Fact]
    public void ACombinationChartSpacesItsLegendLikeAnyOther()
    {
        // A legend entry for a line series is given three times the marker of one for a column, and
        // every entry is then widened to the widest before being drawn. The widening used to happen
        // after the widths had been totalled, so the columns were measured narrow and drawn wide,
        // and each entry was painted over the one before it.
        //
        // Equalized markers mean equal markers, so the step from one label to the next depends only
        // on the text before it - which makes a chart of nothing but lines the right thing to
        // compare against, and needs no marker width written down here to compare with.
        var combination = Quarterly(ChartType.Column2D,
            [42.0, 58, 51, 73], [31.0, 29, 44, 38], [18.0, 26, 33, 47]);
        combination.SeriesCollection[2].ChartType = ChartType.Line;

        var allLines = Quarterly(ChartType.Line,
            [42.0, 58, 51, 73], [31.0, 29, 44, 38], [18.0, 26, 33, 47]);

        var mixed = Drawn(combination);
        var lines = Drawn(allLines);

        (XOf(mixed, "South") - XOf(mixed, "North")).Should().BeApproximately(
            XOf(lines, "South") - XOf(lines, "North"), 0.5,
            "an entry drawn with the widest marker has to have been measured with it");
        (XOf(mixed, "West") - XOf(mixed, "South")).Should().BeApproximately(
            XOf(lines, "West") - XOf(lines, "South"), 0.5);
    }

    [Fact]
    public void ALegendEntryDoesNotStartBeforeTheOneBeforeItHasFinished()
    {
        // The same defect stated without a second chart to compare against: whatever the markers
        // are, a label cannot begin to the left of where the previous label's text ends.
        var chart = Quarterly(ChartType.Column2D,
            [42.0, 58, 51, 73], [31.0, 29, 44, 38], [18.0, 26, 33, 47]);
        chart.SeriesCollection[1].ChartType = ChartType.Line;

        var page = Drawn(chart);

        XOf(page, "South").Should().BeGreaterThan(XOf(page, "North") + WidthOf("North"));
        XOf(page, "West").Should().BeGreaterThan(XOf(page, "South") + WidthOf("South"));
    }

    /// <summary>
    ///   How wide the text is in the font the charts are set in, measured on a page of its own.
    /// </summary>
    /// <remarks>
    ///   Not measured on the page under test: opening a second <see cref="XGraphics"/> over a page
    ///   that has already been drawn on appends another content stream to it, and what the
    ///   assertions then read back is no longer the page the chart wrote.
    /// </remarks>
    private static double WidthOf(string text)
    {
        var document = new PdfDocument();
        using var gfx = XGraphics.FromPdfPage(document.AddPage());
        return gfx.MeasureString(text, new XFont(Face, Size)).Width;
    }

    // ----- the percentage labels of a pie -----

    private static Chart Pie(string format, params double[] values)
    {
        var chart = new Chart(ChartType.Pie2D)
        {
            Font = { Name = Face, Size = Size }
        };
        chart.XValues.AddXSeries().Add([..values.Select((_, index) => $"S{index}")]);
        chart.SeriesCollection.AddSeries().Add(values);
        chart.HasDataLabel = true;
        chart.DataLabel.Type = DataLabelType.Percent;
        chart.DataLabel.Position = DataLabelPosition.InsideEnd;
        if (format != null)
            chart.DataLabel.Format = format;

        return chart;
    }

    [Theory]
    // A format carrying '%' is a .NET percent format: it scales and signs the value itself.
    [InlineData("0%", "19%")]
    [InlineData("0.0%", "18.8%")]
    // Anything else is a plain numeric format over a number out of a hundred, and the renderer
    // appends the sign. This is what the property always meant, and still does.
    [InlineData("0", "19%")]
    [InlineData("0.0", "18.8%")]
    // Leaving Format alone is not the same as leaving it empty: DataLabelRenderer substitutes "0"
    // for an unset format, so a share defaults to whole percents rather than to everything it has.
    [InlineData(null, "19%")]
    public void APieLabelsItsSharesTheWayTheFormatAsks(string format, string expected)
    {
        // The renderer formats with the ambient culture, as every ToString(format) in the charting
        // package does, so the expectations above are only true under one. Pinned rather than
        // localised because a percent format varies in more than the decimal separator - the
        // symbol moves, and some cultures put a space in front of it - and an expectation rebuilt
        // from the same rules the renderer uses would assert nothing. CurrentCulture is per thread
        // and xUnit gives a test method a thread to itself, so this does not reach a test running
        // beside it.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            // 42 of 42 + 58 + 51 + 73 = 224 is 18.75%, worth using because it rounds differently at
            // each of the precisions above rather than being a round number twice over.
            Shows(Drawn(Pie(format, 42, 58, 51, 73)), expected).Should().BeTrue();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void APieNeverSignsItsSharesTwice()
    {
        // The whole of the defect in one assertion. "0%" produced "1875%%": the share was scaled by
        // a hundred to make a percentage, scaled again by the format, and signed by both.
        var page = Drawn(Pie("0%", 1, 3));

        Shows(page, "25%").Should().BeTrue();
        Shows(page, "75%").Should().BeTrue();
        Shows(page, "1875%%").Should().BeFalse();
        Shows(page, "2500%%").Should().BeFalse();
    }

    [Fact]
    public void APieShareIsStillAShareOfTheWhole()
    {
        // The guard on the arithmetic that was rewritten to fix the format handling: the shares
        // have to add up, whichever way round the division is written.
        var page = Drawn(Pie("0", 10, 20, 30, 40));

        foreach (var share in new[] { "10%", "20%", "30%", "40%" })
            Shows(page, share).Should().BeTrue($"a tenth of the whole reads as {share}");
    }
}
