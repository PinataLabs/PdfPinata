Group 1 fixes a defect that is reachable today and must land before anything can produce the case it
mishandles. Group 2 is a pure lift, shared with MigraDoc. Group 3 is the abstraction, group 4 wires
it into the formatter, group 5 makes it visible and closes the change out.

`FormatterLayoutPinTests` — seventeen arrangements — is re-run at the end of **every** group. A
layout regression is silent, and the whole compatibility claim of this change is that a block with
nothing reserved lays out byte for byte as it did.

## 1. The "no room" path, before anything can produce one

Landed on its own as PR #96, ahead of the rest of the change.

- [x] 1.1 Probe the loop before touching it: does a band with no room already occur? **It does.** A
      drop cap is scaled to its own depth and nothing holds its width to the measure, so a 30pt-wide
      area with a five-line cap draws five lines sixteen points past its own right edge, one word to
      a line, nothing thrown. The proposal had this down as latent; it is shipped. Corrected there
      rather than left to mislead.
- [x] 1.2 Give the measure a way to say the band has no room, and how far down the thing standing in
      it reaches. A start at or past its own limit is a different answer from a narrow line and
      cannot be expressed as one — the loop places the first block of a line whether it fits or not,
      which is right for an over-wide word and is what put the text outside the column.
- [x] 1.3 Write the termination guard **before** the skip, not after. iText7's shift-under-floats
      rule relies on a bounded box; `AllowVerticalOverflow` leaves none here, so nothing outside the
      skip can stop it. The floor of one line is what ends it, and makes the worst case equal to the
      line-at-a-time advance it replaces.
- [x] 1.4 Skip to the foot of the obstruction rather than a line at a time, so the number of tries is
      a fact about the obstruction and not about the line height. Where the skip runs out of layout,
      drop the text exactly as text past the last column is dropped.
- [x] 1.5 Test against the drop cap: text kept inside the column, text beginning below the cap,
      nothing lost on the way past, the move across to the next column, and both termination cases.
      **Six tests.**
- [x] 1.6 Re-run the pins. **Byte-identical**, all seventeen.
- [x] 1.7 Check whether `XTextSegmentFormatter` inherits any of this. **It does not** — it carries
      its own `CreateLayout` with no per-line measure in it, so it has neither the defect nor the
      fix. The proposal and design said it shared the loop; both corrected.

## 2. The one piece of arithmetic the two engines share

- [x] 2.1 Extract the tolerance-aware widest-free-span scan into `PdfPinata/Drawing.Layout/` as a
      pure static helper taking and returning **plain doubles**. No `Rectangle`, `XRect` or `XUnit`
      in the signature — that is what sidesteps the type mismatch between the engines. Public,
      because this repository deliberately carries no `InternalsVisibleTo` and MigraDoc must call it.
      `LineSpans.TryWidestFree`.
- [x] 2.2 A **pure lift**: same comparison operators, same tie-breaking, tolerance threaded as a
      parameter rather than hardcoded. Any tidying belongs in a later commit — tidying here is what
      would break the pin and make the cause hard to find. The spans are still sorted **in place**
      rather than copied, for the same reason: it is what the code did, and a per-line call in a
      layout loop should not allocate a second list. Said plainly on the parameter and pinned.
- [x] 2.3 Point `ObstructedArea.GetFittingRect` at it, wrapping the result back into a `Rectangle`.
      `BlockedSpansIn` stays: obstacle collection and band filtering are per-engine, because
      MigraDoc's obstacles are page-absolute and the formatter's are block-relative.
      **The "nothing is standing here" early-out stays in the area too**, which the first pass had
      folded into the scan: the scan judges a run against the tolerance and answers null for an area
      no wider than that, where the area answers with itself. They differ only for an area of no
      width, which is exactly the sort of case a pinned layout is made of. Pinned by a test of its
      own so a later tidy cannot quietly undo it.
- [x] 2.4 Verify against `ObstructedAreaTests` (now 21, which exercise the tolerance boundaries)
      **and** `MigraDocLayoutPinTests` (10 documents, 14 pages). **Byte-identical.** Plus 20 tests on
      the helper directly, since it is public API and an area cannot produce some of the inputs it
      has to accept: an obstacle hanging outside the line, obstacles in any order, one swallowed by
      another, and a caller passing nothing at all.

## 3. The abstraction

- [x] 3.1 A band type: a top and a bottom, the **line box** rather than the baseline. A line whose
      baseline clears an obstacle can still have ascenders inside it. `FlowBand`, which owns the
      overlap test as well, so every obstacle answers by one rule rather than each choosing.
- [x] 3.2 An interval set with subtraction, and the free-span query answering with it. More general
      than either reference implementation — iText7 coalesces to one span, QuestPDF has no per-line
      seam at all — and chosen anyway so that taking the widest span is a policy in the loop rather
      than a property of the type. `XInterval` and `IntervalSet`, immutable, with `TryWidest` as the
      place the one-run-per-line decision is taken.
- [x] 3.3 An obstacle interface returning excluded intervals for a band, with a rectangle
      implementation. This is where an ellipse, a polygon and an `XGraphicsPath` become new
      implementations rather than a redesign. `IFlowObstacle` and `RectangleObstacle`, composed by
      `TextFlowRegion`.
- [x] 3.4 Padding **on the obstacle** — `Inflate` for a rectangle. A margin is a fact about the thing
      being avoided, and two obstacles in one block can want different distances. One distance
      rather than four; growing it to four sides later changes `RectangleObstacle` and nothing else.
      It holds text off **vertically as well as horizontally**, so a line that would otherwise clear
      the obstacle by a hair is pushed past it, which is what MigraDoc's `DistanceTop` does.
- [x] 3.5 Unit-test the geometry with no formatter in sight: obstacle left, right, in the middle
      (two spans), spanning the full width, above the band, below the band, two at once, and
      touching an edge exactly. Correctness is cheap to establish here and expensive to debug
      through a rendered page. **61 tests** across `FlowGeometryTests` and `TextFlowRegionTests`.
- [x] 3.6 Re-run the pins. **Byte-identical.**
- [x] 3.7 Not planned, and not optional once 3.2 existed: `LineSpans` had hand-rolled the same
      sweep, written before there was an interval type. Two implementations of one idea in one
      folder is the thing group 2 was for, so `LineSpans` now delegates to `IntervalSet` and keeps
      only the doubles-in-doubles-out signature MigraDoc calls. It stops sorting the caller's list
      in place — a wart flagged when it was written and now gone, since the set orders its own copy.
      Costs a few allocations per line, which is cheap against a page of glyphs and is recorded on
      the class along with what to do if it ever stops being.

## 4. Wire it into the formatter

- [x] 4.1 Give `XTextFormatter` a flow region: bounds plus obstacles, supplied by the caller. The
      bounds come from the layout rectangle the formatter already has, so `Obstacles` is the whole
      of the new surface and there is nothing for a caller to keep in step. The region is built only
      where something stands in the block, so the ordinary case reaches the same code it always did.
- [x] 4.2 Refuse an obstacle where `Rotation != 0`, with an error naming the frame obstacles are
      given in. Layout runs unrotated and rotation is a draw-time transform, so a layout-local
      obstacle costs nothing while a page-space one would inverse-rotate a rectangle into a quad —
      dragging the polygon implementation onto the critical path to support rectangles. **Only
      caller-supplied obstacles are refused**: the drop cap is one the formatter makes for itself in
      the frame it lays out in, and caps under rotation have always worked.
- [x] 4.3 Make the drop cap an obstacle the formatter creates. Only the formatter can size it, since
      it scales the glyph to the line depth. `MeasureOfLineAt` loses its `_dropCap` branch **and**
      its `column == 0` test: with the reservation in layout coordinates, "first column only" falls
      out geometrically. The cap is **clipped to the first column** when the obstacle is made — it
      is scaled by its depth and can come out wider than the column it is set in, and unclipped it
      would reach across the gutter and narrow the next column too.
- [x] 4.4 Clip each obstacle to the column of the line being measured, so an obstacle straddling the
      gutter becomes two ordinary reductions with nothing to do about the gutter itself.
      `IntervalSet.Intersect`, which group 3 did not need and this does.
- [x] 4.5 Measure `ApplyEllipsis` against the line it lands on rather than the column it sits in.
      **Already so** — `drop-cap-layout` did it when it introduced `Block.LineWidth`, and the
      comment there says why. Covered now by a test with a caller's obstacle rather than a cap.
- [x] 4.6 Test: both sides narrowed, an obstacle spanning two columns, ellipsis on a narrowed last
      line, a cap and an obstacle narrowing one line together, and the drop cap unchanged in a
      two-column layout. **15 tests**, including the two rotation cases and the one that pins the
      nearest-foot rule below.
- [x] 4.7 Re-run the pins, and `DropCapTests` unchanged and green — the cap's behaviour must survive
      the change of mechanism untouched. **Both**: 49 cap tests and the seventeen pinned
      arrangements, none of them edited.
- [x] 4.8 Not planned: the skip needs a foot to move to, and with more than one obstacle there is a
      choice of feet. It has to be the **nearest** one below, not the furthest. Two obstacles can
      cover a line between them while neither covers it alone, and the deeper one may go on long
      after the shallower ends — so moving to the deepest steps over bands that do have room.
      `IFlowObstacle` gains `Bottom` and `TextFlowRegion` gains `NextClearanceBelow` for it. My own
      first test of this asserted the opposite and was wrong, which is how the rule got pinned.

## 5. Make it visible, and close out

- [x] 5.1 `MagazineDemo` drops its hand-split pull quote and becomes a caller. One block and one
      obstacle in place of two blocks with a gap arithmetic'd between them. The quote was widened
      out to nearly the full measure before; it is narrower and centred now, so it straddles the
      gutter and leaves a usable run down the outside of each column — which is the arrangement that
      actually shows something.
- [x] 5.2 `docs/specs/demonstration-app.md` says the formatter does not flow text beside a shape and
      is not going to, and that the hand-split quote is therefore not a gap. Both statements go. The
      `SideWrap`-versus-`Magazine` argument stays but changes its point: what differs between the
      engines is no longer *whether* they wrap but what you hand them — a shape in a tree, or a
      rectangle a caller drew and therefore knows the position of. Two new entries take the old
      bullet's place on the "thinner than it looks" list: rectangles only, and one run per line.
- [x] 5.3 Release notes.
- [x] 5.4 `./ci-build.ps1` clean; `dotnet test -f net8.0` and `-f net10.0` **separately** — run
      together they report green and the host then crashes, which is the documented memory-pressure
      abort rather than a failure. **1952 passing on each.** Worth adding to the note: the abort can
      also truncate a *single* leg, and it wears a green label when it does — three runs of the same
      suite reported 1950, 1952 and 1953 tests, all of them saying "Passed!", two of them also
      exiting non-zero. The count and the duration are what give it away, not the word.
- [x] 5.5 Rasterize a page per arrangement and **look at it**. Caught two things no assertion did:
      the second column was empty, because there was not enough copy to reach it and a test that
      asks "is the second column untouched" passes perfectly on a column with nothing in it; and the
      quote's own text overflowed its tint, because the box got narrower and the 16pt lines set into
      it did not. Page one re-checked too — the drop cap is drawn by different code now and had to
      look identical, and does.
