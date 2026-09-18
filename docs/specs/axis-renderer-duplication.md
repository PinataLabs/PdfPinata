# Spec — merging the axis renderer pairs (T8)

`PdfPinata.Charting` had three renderer pairs that were near-copies of one another: horizontal
and vertical category axis (`HorizontalXAxisRenderer` / `VerticalXAxisRenderer`), horizontal and
vertical value axis (`HorizontalYAxisRenderer` / `VerticalYAxisRenderer`), and their two stacked
variants. `docs/specs/charting-renderer-findings.md` C3/C4 already found the category axis pair
diverged on which guards it carried — one had a null check and a bounds check the other lacked —
and that class of defect, a change reaching one orientation and not its twin, is what this merge
exists to close off.

`XAxisRenderer` and `YAxisRenderer` now hold the whole of each pair's geometry, with a readonly
`AxisOrientation orientation` field deciding the handful of things that actually differ. The six
original classes remain as thin subclasses so every call site — `AxisRendererFactory`, the plot
area renderers, the stacked renderers — is unchanged. The two stacked Y-axis renderers fold in
behind the same `YAxisRenderer`, sharing one `CalcStackedYAxis` rather than carrying the same
override twice.

## The one behaviour change: tick-mark pens

Before this merge, only the value axis of a column or line chart stroked its tick marks from
`MinorTickMarkLineFormat` / `MajorTickMarkLineFormat` — the pens `AxisRenderer` computes for every
axis and which are never null. The other three axis kinds stroked theirs from `Axis.LineFormat`
instead, which stays null until a caller sets one, so a category axis or a horizontal value axis
drew **no tick marks at all** by default. Both `XAxisRenderer.Draw` and `YAxisRenderer.Draw` now
read the base-class pens for every orientation, which is the repair `AxisOrientationParityTests`
pins with `EveryAxisDrawsTickMarksByDefaultWithNoLineFormatSet`.

Fixing that reached a second, previously unreachable defect: the vertical category axis divides its
major-tick step by a tick count computed from `(int)(xMax / xMajorTick)`, with no guard against
that count being zero — which a chart with no series at all produces, `xMax` being `MaximumScale`
at its default of zero. Before the pens fix this orientation's ticks were drawn with a pen that was
always null, so the division never ran; it does now, and `XAxisRenderer.Draw` carries the same
`if (countMajorTickMarks != 0)` guard the horizontal orientation already had. See the comment at the
vertical branch of the major-tick loop.

## What was deliberately left alone

Every other divergence between the two renderers of a pair is preserved exactly as found, branched
on `orientation` rather than unified, because the tick-mark pens are the one behaviour change this
merge is for:

- **Locale for default category labels.** `XAxisRenderer.InitXValues` formats a horizontal axis's
  synthesized labels with `TickLabelsFormat`; a vertical axis formats them with
  `CultureInfo.InvariantCulture`. The two were never the same and nothing here makes them so.
- **Call order in `Init`.** The horizontal category axis calls `InitTickLabels` before
  `InitXValues`, because `InitXValues` formats default labels with `TickLabelsFormat`, which
  `InitTickLabels` sets; the vertical one calls them in the other order and never depended on this,
  since it formats with the invariant culture instead.
- **Which series are measured for tick-label width**, in `XAxisRenderer.Format` — the horizontal
  axis measures only its first series, since categories are shared across series and one is
  enough; the vertical axis measures every series it is given.
- **The vertical value axis's half-line `InnerRect` offset**, in `YAxisRenderer.Format` —
  compensates for the vertical axis centring its tick labels on their tick, which the horizontal
  axis does not need to. Local to this orientation rather than promoted to a shared property.

## Repair item 2: the tick-mark pens, tracked

The commit that made this merge (`715c1dc`) is repair item 2 against the class of defect
`charting-renderer-findings.md` describes: a behaviour that only one renderer of a pair had, found
by writing a test that compares both. `AxisOrientationParityTests` is where that class of defect is
now caught for these two renderers as a group, rather than pair by pair.

## What changed after the merge landed: `GetTickMarkPos`

The merge itself carried `GetTickMarkPos` over unchanged into each renderer — two copies, one per
file, each still an `if (orientation == Horizontal) { switch ... } else { switch ... }` with the
same four `TickMarkType` arms repeated per orientation. That was the duplication relocated rather
than removed: a fifth `TickMarkType` would still have wanted the same edit made four times over,
two per file.

`AxisRenderer.GetTickMarkEndpoints` is the shared switch now, taking an edge coordinate and a
`direction` of `+1` or `-1` — which way "away from the plot area" points for that axis's edge — and
answering the same two endpoints either orientation's `GetTickMarkPos` used to compute by hand. Both
files reduce to one call each for the major ticks and one for the minor ticks. A tick mark's `start`
and `end` are only ever used as the two ends of a drawn line, so which one comes out as which is
arbitrary; that is why `Cross`'s two orientations can differ in which endpoint the width is added to
and the drawn line is still the same line. Both `GetTickMarkPos` methods, and the shared switch
itself, are covered by the same `AxisOrientationParityTests` that pin tick-mark visibility, stroke
counts and stroke widths across both orientations.

`isHorizontal`, a field cached once in each renderer's constructor from
`orientation == AxisOrientation.Horizontal`, replaces the repeated comparison that both `Draw` and
`Format` re-evaluated at every one of the several places they branch on orientation — including once
per label inside `YAxisRenderer.Format`'s tick-label measurement loop. The comparison itself was
never expensive; what the cached field buys is one fewer thing to keep in sync if a fifth branch is
ever added, and it reads as what it is: a property of the axis fixed for its lifetime, not a
question asked freshly each time.
