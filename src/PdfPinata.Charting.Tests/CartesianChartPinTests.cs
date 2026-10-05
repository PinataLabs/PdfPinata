using System;
using System.IO;
using System.Text;
using AwesomeAssertions;
using PdfPinata.Charting.Tests.Helpers;
using PdfPinata.Test.Helpers;
using Xunit;

namespace PdfPinata.Charting.Tests;

/// <summary>
///   Every chart drawn on two axes - column, bar, line, area and combination, stacked and not -
///   pinned to the content stream it wrote before the five renderers that draw them were folded
///   into one pipeline.
/// </summary>
/// <remarks>
///   Each of those renderers used to run its own copy of the same Init, Format and Draw order: the
///   legend, the axes, the layout, the plot area and the data labels, then the wall, the
///   gridlines, the border and the axes again. A slip in folding them together moves nothing a
///   reader would call broken - a legend formatted after the axes rather than before, or a data
///   label renderer that is no longer run - and the page still shows a chart. So "every chart is
///   drawn exactly as it was" has to be an observation, made on charts carrying everything those
///   renderers draw: two named series, a legend, axis titles, gridlines and data labels.
///   <para>
///   To re-capture after a deliberate change, run these with the environment variable
///   <c>CHART_PIN_CAPTURE_DIRECTORY</c> naming a directory, copy what they write there over
///   <c>Assets/ChartPins</c>, and read the diff before believing it.
///   </para>
/// </remarks>
public class CartesianChartPinTests
{
    [Theory]
    [InlineData("column")]
    [InlineData("column-stacked")]
    [InlineData("bar")]
    [InlineData("bar-stacked")]
    [InlineData("line")]
    [InlineData("area")]
    [InlineData("combination")]
    [InlineData("combination-stacked")]
    public void EveryCartesianChartIsDrawnExactlyAsItWas(string arrangement)
    {
        var written = Normalized(Encoding.Latin1.GetString(PageContent.Of(Drawn.Page(Arranged(arrangement)))));

        Capture(arrangement, written);

        written.Should().Be(Normalized(Pinned(arrangement)),
            "the '" + arrangement + "' chart must be drawn as it was before");
    }

    /// <summary>The chart each arrangement names, carrying everything its renderer draws.</summary>
    private static Chart Arranged(string arrangement)
    {
        return arrangement switch
        {
            "column" => Decorated(ChartType.Column2D),
            "column-stacked" => Decorated(ChartType.ColumnStacked2D),
            "bar" => Decorated(ChartType.Bar2D),
            "bar-stacked" => Decorated(ChartType.BarStacked2D),
            "line" => Decorated(ChartType.Line),
            "area" => Decorated(ChartType.Area2D),
            "combination" => Combined(ChartType.Area2D, ChartType.Column2D),
            "combination-stacked" => Combined(ChartType.ColumnStacked2D, ChartType.ColumnStacked2D),
            _ => throw new ArgumentOutOfRangeException(nameof(arrangement), arrangement, null)
        };
    }

    /// <summary>
    ///   A chart whose series differ in type, which is what draws it as a combination: the last
    ///   series stays a line, the first two take the types given.
    /// </summary>
    private static Chart Combined(ChartType first, ChartType second)
    {
        var chart = Decorated(ChartType.Line);
        chart.SeriesCollection[0].ChartType = first;
        chart.SeriesCollection[1].ChartType = second;
        return chart;
    }

    private static Chart Decorated(ChartType type)
    {
        var chart = Charts.OfSeries(type, [1.0, 3.5, 2.0], [2.5, 1.0, 4.0], [0.5, 2.0, 3.0]);
        chart.SeriesCollection[0].Name = "First";
        chart.SeriesCollection[1].Name = "Second";
        chart.SeriesCollection[2].Name = "Third";

        chart.Legend.Docking = DockingType.Right;
        chart.HasDataLabel = true;
        chart.XAxis.Title.Caption = "Across";
        chart.YAxis.Title.Caption = "Up";
        chart.YAxis.HasMajorGridlines = true;
        return chart;
    }

    private static string Pinned(string arrangement)
    {
        var name = "PdfPinata.Charting.Tests.Assets.ChartPins." + arrangement + ".txt";
        using var stream = typeof(CartesianChartPinTests).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException("No pinned content stream named " + name + ".");
        using var reader = new StreamReader(stream, Encoding.Latin1);
        return reader.ReadToEnd();
    }

    /// <summary>
    ///   Writes what was drawn where the capture variable says, when it says anywhere at all.
    /// </summary>
    private static void Capture(string arrangement, string written)
    {
        var directory = Environment.GetEnvironmentVariable("CHART_PIN_CAPTURE_DIRECTORY");
        if (string.IsNullOrEmpty(directory))
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, arrangement + ".txt"), written, Encoding.Latin1);
    }

    /// <summary>
    ///   The same text with every line ending reduced to a line feed, so that what is compared is
    ///   the drawing and not what the checkout did to the asset file.
    /// </summary>
    private static string Normalized(string text) => text.Replace("\r\n", "\n");
}
