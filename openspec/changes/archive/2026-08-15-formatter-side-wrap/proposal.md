## Why

`XTextFormatter` already flows text beside a rectangle. It does it for exactly one rectangle, which
it computes itself, anchored to the left edge of column zero:

```csharp
if (_dropCap != null && column == 0)
{
    bool overlaps = yTop < _dropCap.Reserved.Bottom && yTop + _lineHeight > _dropCap.Reserved.Top;
    if (overlaps)
        return new LineMeasure(start + _dropCap.Reserved.Width, columnWidth);
}
```

What is missing is not the machinery. It is a way for a caller to say where a rectangle is — and an
abstraction that will still be right when the rectangle turns out to be a circle.

`docs/specs/demonstration-app.md` currently states that the formatter "does not flow text beside a
shape, and is not going to", and that `MagazineDemo`'s hand-split pull quote therefore stays. That
was written believing the gap was structural. It is not, and the sentence has to go.

**Why now.** `shape-side-wrap` has just landed the same idea in MigraDoc and `drop-cap-layout`
landed the per-line measure this rests on. Both are fresh and both were pinned byte for byte, so the
decisions they settled are still legible enough to reuse or to contradict deliberately.

There is also a defect to fix on the way: the layout loop has **no way to express "no room on this
line"**, and its behaviour when there is none is silently wrong. See Impact.

## What Changes

### The core abstraction is an interval set, not a pair of numbers

For every prospective line, one question:

> At this vertical band, which horizontal spans are free for text?

```csharp
IntervalSet GetAvailableIntervals(FlowBand band);
```

with `FlowBand` a top and a bottom — the **line box**, not the baseline, because a line whose
baseline clears an obstacle can still have ascenders inside it.

This is the decision the rest follows from, and it is a correction. An earlier draft of this
proposal extended the existing `LineMeasure(start, limit)` — a single span — and justified that by
saying the layout loop cannot continue a line across a gap. The loop indeed cannot, today. But that
is a fact about the loop, and building it into the type that expresses *geometry* forecloses
irregular obstacles, multi-span lines and everything else at the one layer that cannot be refactored
cheaply later. The geometry answers honestly; **taking the widest interval is a policy in the loop**,
and the loop is free to grow.

```text
plain rectangle          one exclusion              a circle
[0 ──────────── 500]     [0 ──────────── 500]       [0 ─────────────── 500]
[0 ──────────── 500]     [0 ─ 180] [320 ─ 500]      [0 ─ 210] [290 ─ 500]
[0 ──────────── 500]     [0 ─ 180] [320 ─ 500]      [0 ─ 180] [320 ─ 500]
[0 ──────────── 500]     [0 ──────────── 500]       [0 ─────────────── 500]
```

The formatter never learns what a circle is. It subtracts intervals.

### Obstacles are an interface

```csharp
IReadOnlyList<XInterval> GetExcludedIntervals(FlowBand band);
```

A rectangle implementation ships here. An ellipse, a polygon and an `XGraphicsPath` become new
*implementations* rather than a redesign — flatten, scanline-intersect, pair the crossings. That is
the whole return on getting the abstraction right, and the reason contour flow is listed below as
"not yet built" rather than "out of scope".

**Padding belongs on the obstacle**, not on the formatter: a margin is a fact about the thing being
avoided. For a rectangle it is `Inflate`; for a path it is an offset-outline problem that can wait.
An earlier draft omitted padding altogether, which would have shipped a wrap with no way to hold
text off the thing it wraps — MigraDoc has four distances for this and they earn their keep.

### What else changes

- `XTextFormatter` gains a flow region: bounds plus a list of obstacles. The caller supplies them,
  because the caller drew whatever is there.
- The drop cap becomes an obstacle the formatter creates — only the formatter can size it, since it
  scales the glyph to the line depth. `MeasureOfLineAt` loses its `_dropCap` branch **and** its
  `column == 0` test and gets shorter.
- The layout loop learns what to do with a band that has no free interval: advance and try lower,
  rather than placing a block anyway.
- `ApplyEllipsis` is measured against the line it lands on rather than the column it sits in.

**The formatter is not given a shape model.** It takes obstacles, not shapes — no wrap style, no
floating, no element. `WrapFormat.Style` describes a shape in a document tree and there is no
document tree here.

Columns are **not** excluded. Clipping each obstacle to the column of the line being measured turns
a rectangle spanning two columns into two ordinary reductions, one per column, with nothing to do
about the gutter because no text was ever drawn there. Excluding columns would mean writing a
special case to reject something that costs nothing.

### Deliberately not built here

- **Multi-span lines.** The geometry will return two intervals for an obstacle standing in the
  middle of the measure; the loop uses the widest and leaves the other empty. Filling both means one
  logical line spanning several spans, which justification, alignment and ellipsis all currently
  assume never happens. The abstraction does not foreclose it — that is the point — but the loop
  work is a change of its own. Recorded as a design decision rather than a limitation, because
  `shape-side-wrap` chose widest-span for MigraDoc and the two engines should differ knowingly.
- **Irregular obstacles.** Ellipse, polygon and path implementations, as above.
- **Extracting a layout engine.** The seam lives inside `XTextFormatter` for now. Whether
  `TextLayoutEngine` becomes a type of its own — with the formatter as a façade over it — is a real
  question and a real improvement, but introducing a third layout layer beside `XTextFormatter`,
  `XTextSegmentFormatter` and MigraDoc's renderer deserves its own change rather than arriving as a
  side effect of adding wrapping. The abstraction is what makes that extraction cheap later.
- **Reserving space the text moves *down* for.** An obstacle narrows lines; it never pushes the
  block's top down or grows the layout rectangle.

## Capabilities

### New Capabilities

- `text-flow-regions`: text laid out by `XTextFormatter` flows around caller-supplied obstacles,
  queried per line band as a set of free horizontal intervals, in any column, with a band that has
  no free interval advancing past the obstruction rather than being drawn across it.

### Modified Capabilities

- `variable-line-measure`: gains the requirement that a line with no room is moved past the
  obstruction rather than filled — which the capability says nothing about today, though a drop cap
  can already produce one. The existing requirement that an unobstructed block lays out exactly as
  before is unchanged and still pinned.
- `drop-cap`: no behaviour changes and every existing requirement still holds. Listed because the
  cap becomes an ordinary obstacle rather than a branch, and a requirement that says the cap reserves
  room should say it in terms of the mechanism that now does it.

## Impact

**The defect this fixes.** `XTextFormatter.CreateLayout` places a block that starts a line whether
it fits or not:

```csharp
if (!LineBreak || x + width <= measure.Width || x == lineStart)
```

That rule is right for a word wider than its measure, which is what it was written for. In a band
with no room at all it fires on every line: the fit test fails, `x == lineStart` succeeds, and the
block is placed at the column's right edge — past the column, on top of whatever stands there. The
line breaks, `y` advances, the measure is re-asked, and it repeats for as many lines as the
obstruction is deep. Nothing is thrown.

**It was drafted here as latent. It is not.** A drop cap is scaled to its own depth and nothing
holds its width to the measure, so a cap deep enough in a column narrow enough leaves the lines
beside it no room at all — and then the text goes outside the column, one word to a line, for as
many lines as the cap is deep. Measured, not reasoned: a 30pt-wide area with a five-line cap draws
five lines sixteen points past its own right edge.

So this is a shipped defect with a reproduction, not a trap set for a feature that has not arrived.
It is fixed **first** and on its own — the same sequencing `shape-side-wrap` used for
`GetFittingRect` returning null, and for the same reason: a later failure in the obstacle logic is
then attributable to the obstacle logic.

**A naming collision to settle before any enum is written.** MigraDoc ships
`WrapStyle { TopBottom, None, Through, Left, Right, Largest, Both }`. A parallel `TextWrapSide` or
`TextWrapMode` in the PdfPinata namespace would put two near-identical enumerations in one
solution — the `PageSize`/`PageFormat` situation this repository already has and already recorded a
lesson about: *"The names now agree, which is what made them confusable."* Since the side a line
takes is a consequence of which intervals are free, this change may need no such enum at all. If it
does, that needs deciding rather than defaulting.

**Code affected** — all in `PdfPinata`; no new dependency, no backend seam:

- `Drawing.Layout/XTextFormatter.cs` — `MeasureOfLineAt`, `CreateLayout`, `ApplyEllipsis`,
  `MeasureDropCap`.
- `Drawing.Layout/XDropCap.cs` — unchanged as an API; its reservation is expressed differently.
- `XTextSegmentFormatter` — **checked, and it turns out not to share the loop.** It carries its own
  `CreateLayout` with no per-line measure in it, so it inherits neither the defect nor the fix.
  Giving it obstacles would mean giving it the per-line measure first, which is not this change.

**Not affected.** MigraDoc's `ObstructedArea` and `WrapFormat` are a separate engine and are not
touched. The two remain deliberately separate implementations of one idea, which
`openspec/specs/variable-line-measure/spec.md` and `openspec/specs/shape-side-wrap/spec.md` already
cross-refer about. Worth noting the asymmetry this creates: `ObstructedArea` returns one rectangle
and cannot express two spans, where this returns intervals and can. If multi-span is ever built
here, MigraDoc will be the engine that cannot do it.

**Compatibility.** Additive. A formatter with nothing reserved must lay out byte for byte as it does
now — `FormatterLayoutPinTests` pins that across seventeen arrangements and it is re-checked after
every task group. No public member changes meaning, and no serialised form is involved, so unlike
`shape-side-wrap` there is no older-reader problem.

**Documentation that becomes false.** `docs/specs/demonstration-app.md` says the formatter will never
do this and that `MagazineDemo`'s pull quote is not a gap waiting to be filled. Both statements go,
and the demo becomes a caller.

**Two reference implementations were read before settling this** — QuestPDF and iText7.

*iText7 validates the approach and supplies a better algorithm for the hard case.* `FloatingHelper`
threads a flat `List<Rectangle> floatRendererAreas` through every `LayoutContext` and re-derives
left/right **geometrically** rather than storing a side — which is the same conclusion
`shape-side-wrap` reached independently. More usefully, when floats leave no room,
`calculateLineShiftUnderFloats` finds the lowest bottom edge among the blocking floats and pushes the
cursor **straight past the obstruction** rather than advancing one line height at a time. That is the
rule this change should adopt for its blocked-band path. Note iText7's version relies on a bounded
box; `AllowVerticalOverflow` may leave none here, so the termination guard is ours to write.

*Neither library does interval sets.* iText7 coalesces all left floats into one boundary and all right
into one and clips each line to `[left, right]` — always one contiguous run, never two. QuestPDF
cannot wrap around anything at all: it hands a single scalar width to Skia's native shaper
(`PlanLayout(float availableWidth)`), so there is no per-line seam to exploit.

So `IntervalSet` is more general than either reference. It is chosen anyway, deliberately: the
widest-span rule becomes a **policy in the loop** rather than a property of the type, which is what
makes contour obstacles a new implementation instead of a redesign. Recorded here because the
cheaper, proven alternative was available and was not taken.

**Rotation — settled in the design, decision 3.** Layout runs in unrotated local coordinates and
rotation is a draw-time transform, so obstacles are given in the formatter's own layout frame and
cost nothing. Page-space obstacles under arbitrary rotation would inverse-rotate a rectangle into a
quad and drag the polygon implementation onto the critical path to support rectangles, so a caller
supplying an obstacle while `Rotation != 0` is refused rather than silently reinterpreted.
