namespace PdfPinata.Charting.Tests.Helpers;

/// <summary>
///   Charts arranged the way the tests need them, so that each test says only what it varies.
/// </summary>
internal static class Charts
{
    /// <summary>
    ///   A chart of the given type with one category per value, plotting <paramref name="values"/>
    ///   as its only series.
    /// </summary>
    internal static Chart Of(ChartType type, params double[] values)
    {
        var chart = Empty(type);

        var categories = chart.XValues.AddXSeries();
        for (var idx = 0; idx < values.Length; idx++)
            categories.Add(CategoryNames[idx % CategoryNames.Length]);

        chart.SeriesCollection.AddSeries().Add(values);
        return chart;
    }

    /// <summary>
    ///   A chart of the given type with one category per value of the first series, plotting each
    ///   array given as a series of its own.
    /// </summary>
    internal static Chart OfSeries(ChartType type, params double[][] series)
    {
        var chart = Empty(type);

        var categories = chart.XValues.AddXSeries();
        for (var idx = 0; idx < series[0].Length; idx++)
            categories.Add(CategoryNames[idx % CategoryNames.Length]);

        foreach (var values in series)
            chart.SeriesCollection.AddSeries().Add(values);

        return chart;
    }

    /// <summary>
    ///   A chart of the given type with both axes present and nothing plotted.
    /// </summary>
    /// <remarks>
    ///   Both axes are read, which is not idle: <see cref="Chart.XAxis"/> creates the axis the
    ///   first time it is asked for, and a chart that was never asked has none. A chart drawn by
    ///   anything else - MigraDoc's chart mapper, or a caller following the package's own samples
    ///   - has them, so creating them here is what makes these tests the ordinary case rather
    ///   than a corner of it.
    ///
    ///   What an absent axis costs is its labelling and nothing else. It used to cost the whole
    ///   chart, which was drawn at NaN;
    ///   <c>ChartFrameTests.AChartWithNoXAxisIsStillDrawnAgainstItsData</c> is where that is
    ///   covered now.
    /// </remarks>
    internal static Chart Empty(ChartType type)
    {
        var chart = new Chart(type);
        _ = chart.XAxis;
        _ = chart.YAxis;
        return chart;
    }

    /// <summary>
    ///   One-character category names, so that a tick label measures the same width whichever
    ///   category it belongs to and a test comparing two of them is comparing their positions.
    /// </summary>
    private static readonly string[] CategoryNames = { "A", "B", "C", "D", "E", "F", "G", "H" };
}
