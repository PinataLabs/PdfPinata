---
title: Charts
description: Draw column, bar, line, area and pie charts, either straight onto a page or as an element in a PinataLayout document.
demos: [Charts]
---

The charting engine draws eight kinds of business chart: columns, bars, lines, areas and pies, with
axes, gridlines, legends and data labels. You can reach it two ways, and both end in the same
renderers:

- **Draw a chart onto a page.** Build a `PdfPinata.Charting.Chart`, put it in a `ChartFrame`, and
  draw the frame with an `XGraphics`. You choose the rectangle. This needs the
  [PdfPinata.Charting](https://www.nuget.org/packages/PdfPinata.Charting) package.
- **Add a chart to a document.** Call `Section.AddChart` on a PinataLayout document. The renderer
  places the chart in the flow, between paragraphs, and moves it to the next page if it does not
  fit. This needs [PinataLayout.Rendering](https://www.nuget.org/packages/PinataLayout.Rendering),
  which brings the charting package with it.

Charts draw text, so either route needs a font resolver. See [Installation](../installation.md).

## The eight chart types

| `ChartType` | What it draws |
| --- | --- |
| `Column2D` | Vertical bars, one group per category, the series side by side. |
| `ColumnStacked2D` | Vertical bars with the series stacked into one bar per category. |
| `Bar2D` | The same as `Column2D`, turned on its side. |
| `BarStacked2D` | The same as `ColumnStacked2D`, turned on its side. |
| `Line` | A line per series, with optional markers at each point. |
| `Area2D` | A filled area per series. A later series is drawn in front of an earlier one. |
| `Pie2D` | One series as slices of a circle. |
| `PieExploded2D` | A pie with the slices pulled apart. |

Both routes use the same names. The drawn route's types are in the `PdfPinata.Charting` namespace;
the document route's are in `PinataLayout.DocumentObjectModel.Shapes.Charts`. The two namespaces
have classes with the same names (`Chart`, `Series`, `ChartType`), so if you use both in one file,
give one of them an alias. The demo uses `using Charting = PdfPinata.Charting;`.

## Draw a chart on a page

A chart has no size of its own. A `ChartFrame` gives it one: set the frame's `Location` and `Size`,
add the chart, and call `DrawChart`.

```csharp demo=Charts snippet=chart-frame
```

## Build a chart from series

A chart holds two kinds of data:

- **An X series** holds the category labels. `chart.XValues.AddXSeries()` makes one, and `Add` takes
  the labels.
- **A series** holds the values for one thing being measured. `chart.SeriesCollection.AddSeries()`
  makes one, `Add` takes the numbers, and `Name` is what the legend shows.

```csharp demo=Charts snippet=build-chart
```

`chart.Font` sets the family and size for every piece of text in the chart. `chart.Legend.Docking`
puts the legend at the `Top`, `Bottom`, `Left` or `Right` of the chart.

## Axes and scales

An axis you leave alone scales itself to the data. Set `MinimumScale`, `MaximumScale` and `MajorTick`
to fix the scale. A fixed scale lets you compare two charts drawn from different figures, and it stops
a chart from changing its scale each time the numbers change.

```csharp demo=Charts snippet=fixed-scale
```

Each axis also has `HasMajorGridlines` and `HasMinorGridlines`, `MajorTickMark` and `MinorTickMark`,
`TickLabels.Format` for the format of its numbers, and `Title` for a caption beside it
(`chart.YAxis.Title.Caption`). For a line chart, `MarkerStyle` and `MarkerSize` on a series put a
marker at each point.

## Pies and data labels

A pie shows one series. The X series labels the slices instead of an axis.

```csharp demo=Charts snippet=pie
```

Set `HasDataLabel` to `true` to write a label on each point. `DataLabel.Type` chooses what the label
says: `Value` for the number, `Percent` for its share of the total, or `None`. `DataLabel.Position`
takes `Center`, `InsideBase`, `InsideEnd` or `OutsideEnd`, and `DataLabel.Format` is a .NET number
format. You can set the same properties on one series instead of on the whole chart.

## Combination charts

Each series has its own `ChartType`. When a series has a different type from its chart, the chart is
drawn as a combination. That is the whole API: set `ChartType` on the series that must be different.

```csharp demo=Charts snippet=combination
```

## Leave a gap in a series

`Series.AddBlank()` adds a point with no value. Use it when a figure is missing, rather than
inventing a zero. `XSeries.AddBlank()` does the same for a category label.

```csharp
Charting.Series sales = chart.SeriesCollection.AddSeries();
sales.Add(42);
sales.AddBlank();   // no figure for Q2
sales.Add(51);
```

A column, bar or pie chart draws nothing for a blank point, and the blank does not count towards the
axis scale. A line or area chart draws a blank point as zero.

## Draw a chart with a frame

`ChartFrame` has a second method, `Draw`. It paints a rounded border, a vertical gradient and a drop
shadow, then lays the chart out inside them. It also draws every chart the frame holds, where
`DrawChart` draws only the first.

```csharp demo=Charts snippet=framed
```

If a chart arrives with a border you did not ask for, check which of the two methods you called.

## Charts in a PinataLayout document

`Section.AddChart` adds a chart to the document like a paragraph or a table. Give it a `Width` and
`Height`; the renderer decides where it goes.

```csharp demo=Charts snippet=dom-chart
```

The document chart differs from the drawn one in two ways:

- **Text is formatted through `Format`**, like everything else in a PinataLayout document, not
  through a `Font` of its own.
- **The legend goes in a text area.** The chart has six areas around the plot: `HeaderArea`,
  `FooterArea`, `TopArea`, `BottomArea`, `LeftArea` and `RightArea`. You add paragraphs to them for
  titles and notes, and `AddLegend()` puts the legend in one.

A document chart is a shape, so it can also stand beside the text, with the text running down one
side of it. See [Columns, drop caps and wrapping](./advanced-layout.md).

Choose the document route when the chart belongs to a report and must move with the text around it.
Choose the drawn route when you are placing everything on the page yourself.

## Things to know

- **An unnamed font is Arial.** A drawn chart whose `Font.Name` you do not set asks the font
  resolver for Arial. If your resolver cannot supply it, set `chart.Font.Name` to a family it can.
- **A frame too small for its axes draws no plot.** When the axes and labels take up all the room in
  the frame, the plot area is left empty. Nothing is thrown. Make the frame bigger or the font
  smaller.
- **An empty chart draws its axes.** A chart with no series, or with series that hold no points,
  draws its axes and nothing inside them. A line series with only one point draws no line.
- **`Chart.DisplayBlanksAs` has no effect.** You can set it, but no renderer reads it. Blank points
  behave as described above whatever it says.
- **An axis title cannot move across its axis.** `AxisTitle.Alignment` and `VerticalAlignment` move a
  title along its axis. The strip kept for the title is only as big as the title, so it has nowhere
  to move across it.
- **Only the document route breaks across pages.** A drawn chart goes exactly where you put it,
  whether or not there is room.

## See it in action

The [Charts demo](../demos.mdx#charts) draws all eight types from one set of figures, a combination
chart, two pies with percentage labels and a framed chart. Its last page draws the same figures
again through PinataLayout.

<details>
<summary>The full Charts demo</summary>

```csharp demo=Charts
```

</details>
