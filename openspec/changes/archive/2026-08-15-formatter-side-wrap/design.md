## Context

`XTextFormatter` lays out in unrotated local coordinates and applies rotation as a graphics
transform at draw time:

```csharp
if (Rotation != 0)
    _gfx.RotateAtTransform(-Rotation, new XPoint(layoutRectangle.X, layoutRectangle.Y));
```

`CreateLayout` never learns that rotation exists. That single fact decides the rotation question, and
it decides it cheaply — but only in one direction.

Two decisions were settled by comparison against QuestPDF and iText7 (see the proposal), and two more
are settled here.

## Goals / Non-Goals

**Goals**

- A caller can reserve regions and have text flow around them.
- The abstraction survives contour obstacles being added later without redesign.
- A formatter with nothing reserved lays out byte for byte as it does today.

**Non-Goals**

- Multi-span lines. The geometry may return several free intervals; the loop takes the widest.
- Obstacles expressed in page coordinates under arbitrary rotation — see decision 3.
- Extracting a layout engine type.

## Decisions

### 1. The geometry answers with an interval set; the widest-span rule is a policy in the loop

`IntervalSet GetAvailableIntervals(FlowBand band)`, where `FlowBand` is a line **box** rather than a
baseline — a line whose baseline clears an obstacle can still have ascenders inside it.

**More general than either reference implementation, and chosen knowing that.** iText7 coalesces all
left floats into one boundary and all right into one and clips each line to `[left, right]`; QuestPDF
cannot wrap at all, because it passes a single scalar width to Skia's shaper and has no per-line seam.
The extra generality buys one thing: taking the widest span becomes a decision the *loop* makes, so a
contour obstacle is a new `IFlowObstacle` implementation rather than a change to the type everything
else is expressed in. Behaviour on day one is identical to iText7's.

### 2. Only the widest-free-span scan is shared between the engines

Extracted into `PdfPinata/Drawing.Layout/` as a pure static helper taking and returning **plain
doubles** — no `Rectangle`, `XRect` or `XUnit` in the signature, which sidesteps the type mismatch
between the two engines entirely. Public, because this repository deliberately carries no
`InternalsVisibleTo` and MigraDoc must call it.

Obstacle collection and band filtering stay per-engine: MigraDoc's obstacles are page-absolute and
the formatter's are block-relative, and that difference is real rather than incidental.

A **pure lift** — same comparison operators, same tie-breaking, tolerance threaded as a parameter.
Any tidying in the same commit is what would break `MigraDocLayoutPinTests`.

### 3. Obstacles are given in the formatter's own layout coordinates

**This is the rotation answer, and it follows from where rotation is applied rather than from
preference.**

Layout runs in unrotated local space, so an obstacle expressed in that space needs no transform at
all: it is subtracted in the frame subtraction was always going to happen in. The obstacle then
rotates *with* the text and stays where it was put relative to it, which is what a reserved region
inside a block means.

**Page coordinates were considered and rejected for now, because they are not the cheap option they
look like.** An axis-aligned page rectangle inverse-rotated into layout space is a quad, not a
rectangle:

```text
   page space            layout space, inverse-rotated
   ┌────────┐                      ◇
   │  rect  │        →           ◇   ◇        no longer axis-aligned
   └────────┘                      ◇
```

So supporting page-space obstacles at arbitrary rotation would drag the polygon/scanline
implementation out of "a later obstacle type" and onto the critical path — to support *rectangles*.
That is a large cost for a case no caller has asked for yet.

The restriction is only visible when `Rotation != 0`. At `Rotation == 0` the two frames coincide, so
for every caller who does not rotate, this decision is invisible and costs nothing.

**A caller who supplies an obstacle while `Rotation != 0` is refused, not silently reinterpreted.**
The two readings put text in visibly different places, and there is no way for the formatter to tell
which was meant. This repository has spent this release closing four defects that failed silently —
a wrong page that looks deliberate is the failure mode being avoided, and an exception naming both
frames is cheap by comparison.

**The escape hatch, if page space is wanted later:** at multiples of 90° an axis-aligned page
rectangle inverse-rotates to an axis-aligned layout rectangle and the quad problem vanishes. There is
precedent for exactly that restriction in the solution — MigraDoc's `TextFrame.Orientation` is 90°
steps only. Worth knowing the door is open; not worth opening it here.

### 4. No wrap-side enumeration

MigraDoc already ships `WrapStyle { TopBottom, None, Through, Left, Right, Largest, Both }`. A
parallel `TextWrapSide` or `TextWrapMode` in the PdfPinata namespace would put two near-identical
enumerations in one solution, which is the `PageSize`/`PageFormat` situation this repository has
already recorded a lesson about: *"The names now agree, which is what made them confusable."*

None is needed. **The side a line takes is a consequence of which intervals are free**, not an
instruction — iText7 stores no side at all and re-derives it geometrically, and `shape-side-wrap`
reached the same conclusion independently by expressing the side in the obstacle. An obstacle
touching the column's left edge leaves free space only on the right, and that is the whole rule.

## Risks / Trade-offs

- **The blocked-band path is not new and is already reached by ordinary content.** This was drafted
  the other way round — the path was thought to be unreachable until a caller could supply an
  obstacle — and probing the loop before touching it showed a drop cap deep enough in a column
  narrow enough already produces one, and puts the text outside the column when it does → its own
  task group, landed first and on its own, tested against the drop cap that produces it.
- **iText7's shift-under-floats rule relies on a bounded box.** `AllowVerticalOverflow` may leave
  none here, so an unbounded advance could spin → the termination guard is written before the skip,
  not after.
- **Extracting the scan touches byte-pinned MigraDoc output** → a pure lift, verified against
  `ObstructedAreaTests` and the pinned corpus, with no other change in the commit.
- **Refusing obstacles under rotation may be the wrong call** if page-space turns out to be what
  callers want → it is the reversible direction. Loosening a refusal later breaks nobody; changing
  which frame an accepted obstacle means would move text on existing documents.

## Open Questions

- ~~Does `XTextSegmentFormatter`, which shares the layout loop, want obstacles of its own or merely to
  inherit the blocked-band fix?~~ **Checked, and the premise was wrong.** It does not share the loop:
  it carries its own `CreateLayout` with no per-line measure at all, so it inherits nothing and
  cannot reach the blocked-band defect either. Giving it obstacles would mean giving it the per-line
  measure first, which is a change of its own and not this one.
