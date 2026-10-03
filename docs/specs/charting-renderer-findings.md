# Spec — What testing the charting renderers found, and what was done about it

`PdfPinata.Charting` had no tests. The whole assembly measured 0% of lines, and the ten
highest-CRAP methods in the fork were all in it — `YAxisRenderer.FineTuneYAxis` at 2,162, then the
axis, plot area and data label renderers behind it.

`PdfPinata.Charting.Tests` covers them. Every renderer in the package is `internal` and this
repository carries no `InternalsVisibleTo`, so the tests reach them the way a caller does: a `Chart`
handed to a `ChartFrame`, drawn onto a page, saved, reopened, and read back out of the content
stream. That has a consequence worth stating plainly — **everything below was reachable through
public API**. None of it needed reflection to find, and none of it needed reflection to hit.

Eight defects came out of it — seven from writing the tests, one more from review of the fixes.
A second round of tests, over legends, markers and the object model's copying, found three more,
and upstream reports three after that. All fourteen are now fixed, and the test that recorded each
has been turned round to assert the behaviour that replaced it.

| # | finding | severity | status |
|---|---|---|---|
| C1 | A chart with no X axis writes `NaN` coordinates to the page | **high** | **fixed** |
| C2 | `Series.AddBlank` throws when the chart is drawn | **high** | **fixed** |
| C3 | A blank category throws on a bar chart and is skipped on a column chart | medium | **fixed** |
| C4 | Fewer categories than values throws on a bar chart and is tolerated on a column chart | medium | **fixed** |
| C5 | A frame too small for its axes throws from inside `XRect` | medium | **fixed** |
| C6 | An axis title's alignment moves it nowhere, or only sometimes | low | **fixed** (see below) |
| C7 | `DataLabelPosition.InsideBase` stacks every pie label on one point | low | **fixed** |
| C8 | A chart with nothing plotted throws before it draws | medium | **fixed** |
| C9 | `Chart.Clone` shares its legend and font with the original | medium | **fixed** |
| C10 | Cloning a collection that holds a blank throws | medium | **fixed** |
| C11 | A legend reserves a line marker's size in its own unit rather than in points | low | **fixed** |
| C12 | A line format that says `Visible = false` is still stroked, as a hairline | medium | **fixed** |
| C13 | A second category series is drawn past the end of the axis (empira/PDFsharp#286) | medium | **fixed** |
| C14 | A legend too wide for its chart runs off both sides of it (empira/PDFsharp#306) | medium | **fixed** |

C3 and C4 were one shape seen twice: two renderers written as copies of each other, which had
drifted apart on which inputs they survive. C1 and C8 were another, seen four times over: a
collection created lazily by its property and then read through its field.

Fixing them took the assembly from 71.09% of lines to 71.73%, and branch coverage from 64.28% to
67.10% — the rise is dead branches becoming reachable rather than new tests. Eight of the ten
hotspot methods are now at 100% of statements and none is below 97%.

---

## C1. A chart with no X axis wrote `NaN` to the page — fixed

`Chart.XAxis` creates the axis the first time it is read, so a chart nothing configured has none:

```csharp
var chart = new Chart(ChartType.Column2D);
chart.XValues.AddXSeries().Add("A", "B");
chart.SeriesCollection.AddSeries().Add(1.0, 2.0);
// drawn into a ChartFrame, the content stream read:
//   NaN NaN NaN 200 re
//   f
```

`HorizontalXAxisRenderer.Init` put its scale calculation inside a null check, so with no axis
`MaximumScale` kept its default of zero. `ColumnLikePlotAreaRenderer.Format` then built the plot
area's matrix by dividing by it — `width / 0` is infinity, `0 * infinity` is `NaN`, and `NaN` is
what `XGraphicsPdfRenderer` wrote. The draw succeeded and the file was written; a reader got a page
whose content stream will not parse. PdfPinata's own content lexer answered it with
`KeyNotFoundException: The given key 'NaN' was not present in the dictionary`.

The value axis renderer had never had this problem, and its shape was the fix.
`VerticalYAxisRenderer.Init` calls `InitScale` **before** it asks whether the axis object exists,
leaving only the labelling inside the question. Both X axis renderers now do the same:

```csharp
xari.axis = chart.xAxis;
CalculateXAxisValues(chart, xari);     // outside the check, as InitScale always was
if (xari.axis != null)
{
  InitTickLabels(xari, cri.DefaultFont);
  ...
}
```

`CalculateXAxisValues` reached the series collection through `rendererInfo.axis.parent`, which is
the one thing not available when there is no axis, so it now takes the chart — which `Init` already
had in hand.

A chart with no category axis is therefore drawn correctly and merely goes unlabelled, which is
exactly what a chart with no value axis had always done.

Pinned by `ChartFrameTests.AChartWithNoXAxisIsStillDrawnAgainstItsData` and
`.AChartWithNoXAxisGoesUnlabelled`.

**Why nothing had noticed:** MigraDoc's chart mapper builds both axes unconditionally, so a chart
reached through a `Document` never took this path. It was only reachable by using
`PdfPinata.Charting` directly, which is what the package is for.

---

## C2. `Series.AddBlank` threw when the chart was drawn — fixed

```csharp
var series = chart.SeriesCollection.AddSeries();
series.Add(1.0);
series.AddBlank();
// NullReferenceException in VerticalYAxisRenderer.CalcYAxis
```

`AddBlank` is public, is documented as "Adds a blank to the series", and puts a `null` into the
element collection. The pass that found the smallest and largest value walked those elements
testing each for `NaN` without first testing it for `null`, so the one thing the method exists to
permit was the one thing the renderer could not survive.

Guarding that one walk was not enough — a blank has to survive the whole draw, and about fifteen
places read a point's value. Rather than fifteen null tests, `PointRendererInfo` grew the concept:

```csharp
/// <summary>The value this point plots, or NaN if there is nothing to plot.</summary>
internal double Value => this.point == null ? double.NaN : this.point.value;
```

NaN because a blank was already the same thing to a renderer as a point whose value is NaN — there
is nothing to draw and nothing to add to a total — and because every comparison against NaN is
false, so a blank falls out of a range test on its own and the `IsNaN` tests already written against
missing values now catch both kinds of missing. Reading through `Value` instead of through `point`
is what keeps a blank from being dereferenced, and it simplified four sites that were already
testing `column.point != null && !double.IsNaN(column.point.value)`.

Four places needed more than the substitution:

- The two base `CalcYAxis` implementations walk `series.Elements` rather than renderer infos, and
  took the plain `point != null` guard the stacked overrides already had.
- The column and bar data label renderers would have written the word `NaN` onto the plot area.
  They now leave a blank with no text at all, which their own `Draw` already passes over.
- `ColumnStackedPlotAreaRenderer.IsDataInside` returned `true` unconditionally — a stacked column is
  inside the scale by construction — and so drew a blank with a null brush. It now answers
  `!double.IsNaN(yValue)`: always inside, provided there is a value at all. That was only true of a
  scale worked out from the data; since #168 both stacked renderers test the stretch a segment
  covers on its pile against the scale, which a blank, having none, fails.
- `LinePlotAreaRenderer` and `AreaPlotAreaRenderer` read `sri.series.Elements[idx].Value` directly.
  Both already mapped a NaN value to zero, and a blank now joins it there.

That is `BlankType.NotPlotted`, which is what `Chart.DisplayBlanksAs` defaults to — see *What is
still open* below.

Pinned by `ValueAxisScaleTests.ABlankInASeriesIsLeftOutOfTheScale`, `.ABlankInASeriesIsNotDrawn`,
`.ASeriesOfNothingButBlanksIsGivenARangeToDrawAgainst` and `.EveryChartTypeSurvivesABlank`.

**Side effect:** this reached the first case of `FineTuneYAxis` —

```csharp
if (yMin == double.MaxValue && yMax == double.MinValue)
{
  // No series data given.
  yMin = 0.0f;
  yMax = 0.9f;
}
```

— which a series of nothing but blanks is the only way to produce, and which nothing could reach
while a series of nothing but blanks threw. `FineTuneYAxis` is now at 100% of its statements.

---

## C3, C4. The vertical category axis renderer was missing two guards — fixed

`HorizontalXAxisRenderer.Draw` and `VerticalXAxisRenderer.Draw` are the same method with the axes
swapped. The horizontal one read:

```csharp
for (int idx = 0; idx < countTickLabels && idx < xs.Count; ++idx)
{
  XValue xv = xs[idx];
  if (xv != null)
```

and the vertical one read:

```csharp
for (int idx = countTickLabels - 1; idx >= 0; --idx)
{
  XValue xv = xs[idx];
  string tickLabel = xv.Value;
```

Two guards short, and each was a throw rather than a wrong picture: a category added with
`XSeries.AddBlank` is `null` (`NullReferenceException`), and `countTickLabels` comes from the
longest *series* rather than from the category list, so a chart with three values and two categories
asked for a third that was not there (`ArgumentOutOfRangeException`).

Both conditions are now carried across, in `Draw` and in `Format`, which measures the same labels
and had the same gap. Reversed iteration makes the bound part of the same expression:

```csharp
XValue xv = idx < xs.Count ? xs[idx] : null;
if (xv != null)
```

A blank category keeps its place on the axis rather than closing up over the gap, which is what the
horizontal renderer has always done.

Pinned by `CategoryAxisTests.ABarChartSkipsACategoryWithNoValue`,
`.ABlankCategoryStillTakesItsPlaceOnTheAxis` and
`.ABarChartDrawsTheCategoriesItHasWhenThereAreFewerThanValues`.

---

## C5. A frame too small for its axes threw from inside `XRect` — fixed

Both plot area renderers open by returning if there is no room to draw in, which reads as a decision
already taken that a chart too small should draw nothing. It was unreachable.
`ColumnLikeChartRenderer.CalcLayout` subtracts the axes from the frame and assigns the remainder as
a width, and `XRect.Width` refuses a negative one:

```text
System.ArgumentException: WidthCannotBeNegative
  at PdfPinata.Drawing.XRect.set_Width
  at PdfPinata.Charting.Renderers.AreaRendererInfo.set_Width
  at PdfPinata.Charting.Renderers.ColumnLikeChartRenderer.CalcLayout
```

What a caller got was an exception three frames down naming a rectangle rather than the chart, at a
size that depends on how wide the tick labels measure in the resolved font — so not a threshold
anyone could be warned about.

Two changes, in one place each rather than at the four layout sites:

- `AreaRendererInfo.Width` and `.Height` take an extent below zero as no extent. Every layout in the
  package works by subtraction, so this is where a negative one can arise at all. `AxisRendererInfo`
  overrides both to size an inner rectangle as well, and clamps that too.
- The guard itself was `XRect.IsEmpty`, which means *the empty rectangle* — a width below zero —
  rather than *no room*. Nothing produces the empty rectangle now, so the eight sites that asked
  moved to `Renderer.HasNoRoom`, which asks what was meant:
  `area.Width <= 0 || area.Height <= 0`.

Pinned by `ColumnPlotAreaTests.AFrameTooSmallForItsAxesDrawsNothingInThePlotArea`, over six chart
types, and `.AFrameTooSmallForItsAxesStillWritesNoNaN`.

---

## C6. An axis title's alignment moved it nowhere, or only sometimes — fixed

`AxisTitle.Alignment` and `AxisTitle.VerticalAlignment` are public and settable, and what they did
depended on which axis the title was on and whether it was turned. Three separate causes, two of
them fixed outright and one of them a constraint rather than a bug.

**The category axis read neither setting.** `AxisTitleRenderer` was never constructed for it:
`HorizontalXAxisRenderer.Draw` and `VerticalXAxisRenderer.Draw` each ended by drawing the caption
themselves, so a caption on a category axis was written flat and in the middle whatever it was asked
for. Both now hand the caption to `AxisTitleRenderer`, as the value axis renderers always have,
after setting its rectangle to the strip the axis has to place it in — the full width of the axis
for a horizontal one, the full height for a vertical one. Alignment and orientation both work there
now, and the two axes no longer take different code paths to draw the same kind of object.

Both axes also now *measure* their title through `AxisTitleRenderer.Format`, which is the only place
that accounts for an orientation. Measuring the string by hand, as the category axis renderers did,
reserved the room a rotated caption would have taken lying flat.

The hand-written version carried a small arithmetic error too: the caption was centred on
`xari.Rect.Right / 2`, half of the axis's right edge, rather than on the middle of the axis. The two
are the same only when the axis starts at zero, which it never does — the value axis is to its left.

**A rotated caption's `Bottom` landed where `Center` did.** The two cases are written separately —
`y + height / 2` against `y + height - layout.Height / 2` — but `layout` was the strip rather than
the caption, so the height being halved was the same height on both sides of the subtraction and the
second expression reduced to the first. `AxisTitleRenderer.Format` now records the measured caption
in `AxisTitleRendererInfo.AxisTitleSize`, which existed for exactly that and which only the X axis
renderers were filling in, and `Draw` measures its offsets against the caption instead of against
the strip. The three vertical alignments are now three positions.

**Across the axis, a rotated value-axis caption still does not move, and cannot.** The strip the
axis sets aside for its title is exactly as wide as the title, because that is how much room the
axis took from the plot area for it. All three alignments put the caption in the middle of that
strip, which is the only place it fits. `Left` used to come out elsewhere by putting the caption's
centre on the strip's near edge, so half of it hung outside the reserved space; landing with the
other two is the correction rather than a loss. Giving that setting somewhere to move to would mean
reserving more width than the caption needs and taking it from the plot area, which is a layout
decision rather than a defect.

The same holds of an upright value-axis caption: it aligns vertically, within the height of the
axis, and not horizontally, within a strip its own width.

Pinned by `AxisTitleTests.TheCategoryAxisReadsItsCaptionsAlignmentAndOrientationToo`,
`.AligningACategoryAxisCaptionMovesItAlongTheAxis`,
`.ARotatedCategoryAxisCaptionReservesTheRoomItTakesTurned`,
`.EachVerticalAlignmentPutsARotatedCaptionSomewhereOfItsOwn`,
`.AligningARotatedCaptionAcrossTheAxisMovesItNowhere` and
`.AligningAnUprightCaptionAcrossMovesItNowhere`.

---

## C7. `DataLabelPosition.InsideBase` stacked every pie label on one point — fixed

```csharp
dleri.X = origin.X;
dleri.Y = origin.Y;
if (dleri.X < origin.X)          // cannot be: it was just set to origin.X
  dleri.X -= dleri.Width;
if (dleri.Y < origin.Y)          // likewise
  dleri.Y -= dleri.Height;
```

The two adjustments were copied from the `OutsideEnd` case above, where the comparison means
something because the position came from an angle. Here the point was being compared with itself, so
neither ran, and a four-wedge pie asked for `InsideBase` drew four labels at one point.

The tests are now on the direction the wedge runs in, which is what they were reaching for:

```csharp
if (Math.Cos(radMidAngle) < 0)
  dleri.X -= dleri.Width;
if (Math.Sin(radMidAngle) < 0)
  dleri.Y -= dleri.Height;
```

Each label is laid out away from the centre along its own wedge, so the corner of it nearest the
centre is the one that sits there and each quadrant gets a corner of its own. The position also now
keeps its labels nearer the middle than any of the other three, which is what its name says.

Pinned by `DataLabelTests.APieLabelledAtItsBaseGivesEachWedgeItsOwnCorner` and
`.APieLabelledAtItsBaseKeepsItsLabelsNearerTheMiddleThanAnyOtherPosition`.

---

## C8. A chart with nothing plotted threw before it drew — fixed

Found by review of the fixes above rather than by the tests. Copilot pointed at
`CalculateXAxisValues` and observed that `MaximumScale` is zero when a chart has no points, which
the plot area then divides its own width by — the C1 arithmetic exactly. It was right about the
arithmetic and wrong that it was reachable: an empty chart threw `NullReferenceException` long
before it got there, which is the more immediate defect and the one that had been hiding the other.

Three lazily-created collections, all the same shape as `Chart.XAxis` — created by a property on
first read, and then read through the field, which is null until someone has asked:

- `Chart.SeriesCollection`, read as `chart.seriesCollection` by `ChartFrame.GetChartRenderer`, so a
  chart nothing was added to threw before any renderer ran. The C1 fix had carried the same pattern
  into both `CalculateXAxisValues` methods.
- `Series.Elements`, read as `sri.series.seriesElements` at thirteen sites, so a chart holding a
  series that holds nothing threw in `InitSeries`.

All sixteen reads now go through the property. Behind them the arithmetic was reachable after all,
so both plot area renderers leave the matrix as the identity when there is nothing to plot against
it:

```csharp
if (xMax <= xMin || yMax <= yMin)
{
  cri.plotAreaRendererInfo.matrix = new XMatrix();
  return;
}
```

That guard tests the span, and review afterwards found that `ColumnLikePlotAreaRenderer` did not
divide by one: it scaled the width by `xMax` where the bar renderer scales by `xMax - xMin`, so
the guard and the division were asking different questions. The two agree only because the category
axis fixes its minimum at zero — `CalculateXAxisValues` assigns it, and unlike the value axis it
never takes one from the `Axis` object — which put the correctness of a guard here at the mercy of
a constant assigned in another file.

The division was the half that was wrong, so the division is what changed:
`plotAreaBox.Width / (xMax - xMin)`. The translate on the line above has already moved `xMin` to the
origin, so the span is the distance actually being fitted across the plot area, and the two
renderers now scale the same axis the same way. Nothing moves on any page today, `xMin` being zero.
The first draft guarded `xMax <= 0` instead and left the division alone; that stopped the infinity
but not the mis-scaling behind it, and it would have refused to draw a legitimate category range
lying entirely below zero.

One more sat behind that: `LinePlotAreaRenderer` handed a zero-length point array to `DrawLines`,
which answers `ArgumentException: The point array must contain 2 or more points`. A series with
fewer than two points is now skipped — a line through one point is not a line.

An empty chart of any of the eight types now draws its axes and nothing inside them.

Pinned by `ChartFrameTests.AChartWithNoSeriesAtAllIsDrawnEmpty` over eight chart types,
`.AChartWhoseSeriesHasNoPointsIsDrawnEmpty` over five, `.AChartWithCategoriesButNoSeriesIsDrawnEmpty`
and `.ALineChartWithASinglePointDrawsNoLine`.

---

## C9. `Chart.Clone` shared its legend and font with the original — fixed

```csharp
chart.Legend.Docking = DockingType.Left;
var copy = chart.Clone();
copy.Legend.Docking = DockingType.Top;
// chart.Legend.Docking is now Top as well
```

`Chart.DeepCopy` clones its children by hand, each behind a null check and each reparented to the
copy. It named seven — the three axes, the series, the categories, the plot area and the data
label — and left out `legend` and `font`, so `MemberwiseClone` carried those two across as shared
references still naming the original as their parent. Both are now cloned and reparented the same
way. Every other `DeepCopy` in the model already covers each `DocumentObject` field its class holds,
so nothing else had the gap.

Pinned by `ChartCloneAndLineFormatTests.ACopiedChartHasALegendAndAFontOfItsOwn` and
`.EveryChildOfACopiedChartNamesTheCopyAsItsParent`.

---

## C10. Cloning a collection that held a blank threw — fixed

`DocumentObjectCollection.DeepCopy` copied each element with `this[index].Clone()`, and a blank is
a null. So `new XSeriesElements()` with `Add("a")` and `AddBlank()` threw on `Clone()`, and so did
`Chart.Clone` on any chart whose series or categories held one — the C2 shape again, one layer
down: the thing `AddBlank` exists to permit was the thing the copy could not survive.

A blank is now copied as a null at the same index. Each copied element is also given the new
collection as its parent: `DocumentObject.DeepCopy` clears the parent, and nothing set it again, so
a copied element used to belong to nothing, where one added through `Add` belongs to its collection.

Pinned by `DocumentObjectCollectionTests.ACloneKeepsABlankWhereItWas`,
`.ACloneOfASeriesKeepsABlankBetweenItsValues`, `.AChartWhoseSeriesHoldsABlankCanStillBeCloned` and
`.ACloneIsTheParentOfEveryElementItCopied`.

---

## C11. A legend reserved a line marker's size in the wrong unit — fixed

Found by `LegendTests`, which now covers what the list below used to call untested: docking, entry
layout, borders, and the keys for line and pie charts.

`LegendEntryRenderer.Format` sized the key for a line series from
`markerRendererInfo.MarkerSize.Value` — the number in whatever unit the size was given in.
`MarkerRenderer` draws the marker at its size in points, and `ColumnLikeLegendRenderer` reads it
the same way, so the three agreed only for a size stated in points. A marker of
`XUnit.FromCentimeter(1)` was given a key of three units, held up to the 21-point minimum, and
then drawn 28.35 points wide across it, overhanging the room its entry had reserved. It now reads
`MarkerSize.Point`, and the key is 85 points long.

PinataLayout's chart mapper converts a DOM marker size to points before it hands it over, and the
demos state theirs in points, so nothing either of them draws moves.

Pinned by `LegendTests.ALineKeyIsMeasuredFromAMarkerSizeInPointsWhateverItsUnit`.

---

## C12. A line format that says `Visible = false` was still stroked, as a hairline — fixed

Reported upstream as empira/PDFsharp#287, against a line chart: a series whose line format was
hidden still drew its line.

`Converter.ToXPen` turns a hidden `LineFormat` into a pen of width 0, and the renderers' own
convention is that such a pen is no line — `ColumnPlotAreaRenderer`, `PlotAreaBorderRenderer` and
`YAxisRenderer` each test `Width > 0` before they stroke. PDF does not share the convention: a line
width of 0 is the thinnest line the device can draw. The line, area, bar and pie plot areas handed
the pen straight to `XGraphics`, so a hidden line came out one device pixel wide. It is the same
shape as C3 and C4: `BarPlotAreaRenderer` is a copy of `ColumnPlotAreaRenderer` and lacked the
guard its twin had.

The legend had it twice over. A line chart's key drew its stroke with a pen of width 1 made from
the marker's colour, never looking at the series' line at all; every other chart's swatch was
outlined with the series' pen, hairline included.

All five now test `Width > 0`. A hidden line series keeps its markers and its key keeps its marker;
a hidden area outline keeps its fill. What `Visible` does to a format that never set it is
unchanged: the property is a plain `bool` whose default is `false`, so a format that states a width
and not `Visible = true` is converted to width 0 as it always was — and is now not drawn at all,
where before it was a hairline. PinataLayout's chart mapper sets `Visible` on every format it
hands over, so nothing it draws is affected.

Pinned by `HiddenSeriesLineTests`, across line, area, column, bar and pie charts, with and without
a legend.

The axes were left out of that fix and disagreed among themselves (#173): a column chart's value
axis tested `Width > 0`, while the category axis and a bar chart's value axis stroked the width-0
pen, and no tick mark, gridline, zero baseline or legend border tested anything. The guard now lives
in `LineFormatRenderer(XGraphics, XPen)`, which every one of those is drawn through, so a pen of
width 0 is no pen wherever it is used. Pinned by `HiddenAxisLineTests`.

A data point's own line format had the same trouble in another shape (#171): the column, bar, area
and pie renderers each turned it into a pen their own way, and the pie chart made a pen of the
point's colour alone, 1 wide and stroked even when the point said `Visible = false`. Column, bar
and pie now take a line format the caller *set* on the point and resolve it through
`Converter.ToXPen` against the series' pen. "Set" is the word that matters: `Point.LineFormat`
creates a format the first time it is read, and one nobody assigned says `Visible = false`, so
taking every non-null format as the point's hid the border of any point whose format had merely
been looked at. `LineFormat` therefore records whether any of its setters has run (`isSet`,
internal, copied by `Clone`). An area chart does not read a point's line format at all: it is
outlined once, as one polygon, with the series' pen, so a point has no border to draw. Pinned by
`PointLineFormatTests`. The legend's border pen is dropped at the same width-0 test, so a hidden
border no longer doubles the legend's padding either (`LegendTests.AHiddenLegendBorderIsNotPaddedFor`).

The dash style needed the same distinction on its own (#192). `ToXPen` took a colour or a width the
point left unset from the series' pen, but not the dash style, whose default, `Solid`, is also a
value a caller can choose: a point that set only its width drew a solid border on a dashed series.
`LineFormat` now records whether `DashStyle` was assigned (`dashStyleSet`, beside `isSet`), and an
unassigned one is the default pen's. Every other caller passes `Solid` as that default, which is
what an unassigned dash style already was, so nothing else draws differently. PinataLayout's
`LineFormatMapper` wrote a dash style onto every format it carried across, `Solid` when the
document gave none, and now writes one only when the document did. Pinned by
`PointLineFormatTests` and `ChartMapperTests.APointThatSetsOnlyAWidthKeepsItsSeriesDashes`.

---

## C13. A second category series was drawn past the end of the axis — fixed

Reported upstream as empira/PDFsharp#286 against Line, Column2D, ColumnStacked2D, Area2D, Bar2D
and BarStacked2D, and reproduced here on all six. `Chart.XValues` is a collection, and
`AddXSeries` can be called more than once, but `XAxisRenderer.Draw` walked every series without
putting the pen back at the first category between them. With three values and two category
series `A B C` and `X Y Z`, a column chart 400 points wide drew `X`, `Y` and `Z` at 459, 584 and
710, and a bar chart drew them below its own foot, at -34, -127 and -220. `Format` disagreed with
itself as well: the horizontal axis measured only the first series, the vertical one measured
every series and reserved the width of the widest label in any of them.

**What several category series should mean is not written down anywhere**, so this is a choice.
The axis has one slot per category and one row of labels, and it is now labelled from **the first
series alone**, in both orientations, when measuring and when drawing. That is the series the
horizontal axis already measured and the pie legend already read (`PieLegendRenderer` takes
`xValues[0]`), and it is what Excel does when each series names categories of its own: the axis
takes the first series' categories and ignores the rest. The alternative, several rows of labels
in the manner of Excel's multi-level category axis, would be a new feature with a layout of its
own rather than a repair, and nothing in the object model says the series are levels.

The one place both orientations read the series from is `XAxisRenderer.CategoryLabels`. Pinned by
`SeveralCategorySeriesTests`, which draws all six chart types with a second, wider series and
asserts both that none of its labels is shown and that every run of text on the page is where it
would be with the first series alone.

## C14. A legend too wide for its chart ran off both sides of it — fixed

Reported upstream as [empira/PDFsharp#306](https://github.com/empira/PDFsharp/issues/306), "MigraDoc
Chart Legend does not Word Wrap": a legend in a chart's footer with many entries, or long ones,
"extends off both sides of the page".

`LegendRenderer.Format` added the entries of a legend docked above or below the chart side by side
into one row, with no idea how wide the chart was, and `ChartRenderer.LayoutLegend` centred the
legend with `Box.Width / 2 - legend.Width / 2` — negative, for a legend wider than the box. Twelve
categories named `Region 1` to `Region 12` on a 400-point chart drew from x = -182 to x = 580.
PinataLayout maps a legend in `FooterArea` or `BottomArea` to `DockingType.Bottom` and one in
`HeaderArea` or `TopArea` to `DockingType.Top`, so the DOM route reached exactly the same code.

`Format` now knows the room it has, `Box.Width` less the legend's padding either side, and
`LayoutRows` sets the entries out in rows: a new row whenever the next entry would not fit, the
rows stacked with the entry spacing between them, each one centred across the widest. Each entry's
place is kept in `LegendEntryRendererInfo.Offset`, and both `LegendRenderer.Draw` and its copy
`BarClusteredLegendRenderer.Draw` read it for a horizontal legend rather than advancing along the
row themselves — the two draw methods are near-copies, and the second is what a bar chart uses.

An entry wider than the room by itself is word wrapped by `LegendEntryRenderer.FitToWidth`, at
spaces, measuring with `XGraphics.MeasureString` as the entry always was measured. That applies to
a legend docked beside the chart too, where one long name used to be as wide as it liked. A single
word wider than the room is kept whole and still overhangs; breaking inside a word was left out.
The entry's marker keys the first line of the entry rather than its middle. Splitting the text into
lines also made a line break in a name work: the text used to go to `DrawString` whole, which draws
one line and drops a line feed.

A legend that fits in one row is laid out as it always was, and every earlier legend test passes
unchanged. Wrapping a vertical legend's entries into more *columns* when there are too many to fit
down the chart was left out: nothing reported it, and a legend beside the chart already takes its
room from the plot rather than from the page.

Pinned by `LegendTests.ALegendTooWideForTheChartWrapsItsEntriesOntoMoreRows` (pie, column and bar,
above and below), `TheRowsOfAWrappedLegendAreCentredAndSpacedAsEntriesAre`,
`AnEntryWiderThanTheChartIsWordWrapped`, `ALineBreakInASeriesNameStartsANewLineOfItsEntry`, and
through PinataLayout by `ChartAreaRenderingTests.AFooterLegendTooWideForTheChartStaysInsideIt`.

---

## What is still open

None of these is a defect. They are gaps the fixes above put in plain view, and each would be a
change to what a chart looks like rather than to whether it can be drawn.

- **`Chart.DisplayBlanksAs` is read by nothing.** The property is public, the `BlankType` enum
  offers `NotPlotted`, `Interpolated` and `Zero`, and no renderer consults it. C2 made the default
  work; the other two kinds are unimplemented. `LinePlotAreaRenderer` carries a TODO saying so.
- **A line or area chart plots a blank as zero**, which is `BlankType.Zero` rather than the default.
  Both renderers already did that for a NaN value, and an area is a closed shape that needs a point
  for every category to close over, so leaving them alone was the smaller claim. Implementing
  `DisplayBlanksAs` is where this belongs.
- **An axis title cannot be aligned across its own axis**, because the strip reserved for it is its
  own size. See C6.
- **Rasterization.** Nothing in the test project renders a chart to an image; it references no
  backend and needs neither Ghostscript nor ImageMagick, which is what lets it run anywhere. A chart
  is asserted through the operators it wrote.
- **Line, area and pie geometry.** Where the wedges and line segments themselves land is not
  asserted; those renderers were not among the ten and would want a path reader rather than a
  rectangle reader. `PaintedPaths` is that reader, and since #169 `PieExplodedPlotAreaTests` reads
  an exploded pie's wedge angles through it; the closed pie, the line and the area are still open.
