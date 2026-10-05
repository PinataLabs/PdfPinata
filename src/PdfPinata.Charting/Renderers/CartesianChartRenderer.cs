namespace PdfPinata.Charting.Renderers;

/// <summary>
/// The pipeline every chart drawn against a category axis and a value axis runs through: column,
/// bar, line, area and combination charts alike.
/// </summary>
/// <remarks>
/// Each of those renderers used to carry its own copy of the same order. Init makes the series'
/// renderer infos, then the legend's, the two axes', the plot area's and the data labels'. Format
/// formats the legend and the axes, lays the chart out, then formats the plot area and the labels
/// in the room that is left. Draw draws the legend, the wall, the gridlines, the plot area's border,
/// the plot area, the labels and finally the axes over the top. That order is here once; what a
/// chart type decides is which renderer fills each place in it, through the <c>Create…</c> hooks,
/// and how its series are given their pens and brushes, through <see cref="InitSeries"/>.
/// <para>
/// A combination chart overrides more than the rest, because its series are of several types and
/// each is formatted and drawn by its own type's plot area over its own series. It keeps its own
/// <see cref="Draw"/> for the same reason.
/// </para>
/// </remarks>
internal abstract class CartesianChartRenderer : ChartRenderer
{
  /// <summary>
  /// Initializes a new instance of the CartesianChartRenderer class with the specified renderer
  /// parameters.
  /// </summary>
  internal CartesianChartRenderer(RendererParameters parms)
    : base(parms)
  {
  }

  /// <summary>
  /// Returns an initialized and renderer specific rendererInfo.
  /// </summary>
  internal override RendererInfo Init()
  {
    var cri = CreateRendererInfo();
    cri.Chart = (Chart)rendererParms.DrawingItem;
    rendererParms.RendererInfo = cri;

    InitSeriesRendererInfo();

    cri.LegendRendererInfo = (LegendRendererInfo)CreateLegendRenderer().Init();
    cri.XAxisRendererInfo = (AxisRendererInfo)CreateXAxisRenderer().Init();
    cri.YAxisRendererInfo = (AxisRendererInfo)CreateYAxisRenderer().Init();
    cri.PlotAreaRendererInfo = (PlotAreaRendererInfo)CreatePlotAreaRenderer().Init();

    InitDataLabels();

    return cri;
  }

  /// <summary>
  /// Lays the chart out: the legend and the axes take their room first, and the plot area and its
  /// labels are formatted in what is left.
  /// </summary>
  internal override void Format()
  {
    CreateLegendRenderer().Format();

    CreateXAxisRenderer().Format();
    CreateYAxisRenderer().Format();

    // Calculate rects and positions.
    LayOut();

    // Calculated remaining plot area, now it's safe to format.
    FormatPlotArea();
  }

  /// <summary>
  /// Draws the chart, the axes last so that nothing in the plot area is drawn over them.
  /// </summary>
  internal override void Draw()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    CreateLegendRenderer().Draw();

    new WallRenderer(rendererParms).Draw();
    new ColumnLikeGridlinesRenderer(rendererParms, CategoryAxis).Draw();
    new PlotAreaBorderRenderer(rendererParms).Draw();

    CreatePlotAreaRenderer().Draw();
    CreateDataLabelRenderer()?.Draw();

    if (cri.XAxisRendererInfo.Axis != null)
      CreateXAxisRenderer().Draw();

    if (cri.YAxisRendererInfo.Axis != null)
      CreateYAxisRenderer().Draw();
  }

  /// <summary>
  /// Which way the category axis runs: across for a column, line or area chart, up for a bar chart.
  /// </summary>
  protected abstract AxisOrientation CategoryAxis { get; }

  /// <summary>
  /// The renderer info the chart is laid out in. A combination chart needs one that also holds its
  /// series sorted by type.
  /// </summary>
  protected virtual ChartRendererInfo CreateRendererInfo() => new();

  /// <summary>The renderer for the legend.</summary>
  protected virtual LegendRenderer CreateLegendRenderer() => new ColumnLikeLegendRenderer(rendererParms);

  /// <summary>The renderer for the category axis.</summary>
  protected abstract AxisRenderer CreateXAxisRenderer();

  /// <summary>The renderer for the value axis.</summary>
  protected abstract AxisRenderer CreateYAxisRenderer();

  /// <summary>The renderer for the plot area.</summary>
  protected abstract PlotAreaRenderer CreatePlotAreaRenderer();

  /// <summary>
  /// The renderer for the data labels, or null for a chart type that draws none.
  /// </summary>
  protected virtual DataLabelRenderer CreateDataLabelRenderer() => null;

  /// <summary>
  /// Gives the series' renderer infos their pens, brushes and points.
  /// </summary>
  /// <remarks>
  /// Internal rather than protected because a combination chart initializes each of its kinds of
  /// series with the chart type's own renderer.
  /// </remarks>
  internal abstract void InitSeries();

  /// <summary>
  /// Places the axes and the plot area in the room the legend leaves.
  /// </summary>
  protected abstract void LayOut();

  /// <summary>Initializes the data labels, for a chart type that draws them.</summary>
  protected virtual void InitDataLabels() => CreateDataLabelRenderer()?.Init();

  /// <summary>
  /// Formats the plot area and the data labels, once the layout has said how much room they have.
  /// </summary>
  protected virtual void FormatPlotArea()
  {
    CreatePlotAreaRenderer().Format();
    CreateDataLabelRenderer()?.Format();
  }

  /// <summary>
  /// Makes one renderer info per series of the chart, in the order the chart holds them, and
  /// initializes them.
  /// </summary>
  private void InitSeriesRendererInfo()
  {
    var cri = (ChartRendererInfo)rendererParms.RendererInfo;

    var seriesColl = cri.Chart.SeriesCollection;
    cri.SeriesRendererInfos = new SeriesRendererInfo[seriesColl.Count];
    for (var idx = 0; idx < seriesColl.Count; ++idx)
      cri.SeriesRendererInfos[idx] = new SeriesRendererInfo { Series = seriesColl[idx] };

    InitSeries();
  }
}
