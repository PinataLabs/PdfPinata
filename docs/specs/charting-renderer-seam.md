# Spec — asking a renderer what it computed

What giving the charting renderers a seam for their geometry covers, and what it deliberately leaves
out.
Follows `docs/specs/axis-renderer-duplication.md`, which should land first.

| item | what | status |
|---|---|---|
| 1 | An internal seam taking renderer parameters and answering computed geometry | proposed |
| 2 | Geometry tests that need no page, no save and no parser | proposed |
| 3 | Rotated captions brought within reach of an assertion | proposed |
| 4 | The round trip kept for what it is actually for | proposed |

## Problem Statement

Every renderer in `PdfPinata.Charting` is `internal`, and this repository carries no
`InternalsVisibleTo`. So a test reaches a renderer the only way a caller can: a `Chart` handed to a
`ChartFrame`, drawn onto a page, saved, reopened with `PdfReader`, and read back out of the content
stream.

`PdfPinata.Charting.Tests` has 110 tests and 112 calls into the `Drawn` helper, because there is
no other route in. Every question about axis arithmetic — where does this tick sit, how many labels
are there, what does this one say — travels through `ChartFrame`'s renderer selection, the whole
`Save` pipeline, `PdfReader`, and a content-stream tokenizer before it can be asked.

The helpers this requires are substantial and good: `PaintedRectangles` is 241 lines recovering the
`re` operators the columns and bars are drawn with, and `ShownText` is 274 lines turning Identity-H
glyph runs back into text through the font's own `/ToUnicode` map so that a test can assert `"0.0"`
rather than a glyph number. That is 515 lines of decoder existing so that a test can ask what a
338-line renderer computed.

They also have a limit, and `ShownText` records it: what it follows is the text matrix, not the
transformation matrix, so *"a rotated axis title is drawn under a rotate of its own and is not
comparable with anything"*. `AxisTitleTests.RotatedCaption` compares content streams instead. One
geometry case is simply out of reach of the assertion style everything else uses.

The round trip is not wrong. It is the right test for *does this chart actually draw* and for golden
images. It is a deep test path for a shallow question, used for every question because it is the only
one available.

## Solution

Give the renderers an internal seam that takes parameters and answers computed geometry, before
anything is drawn.

The renderers already have this shape internally — `Init`, `Format`, `Draw`, with the geometry
settled into a `RendererInfo` by the time `Draw` runs. What is missing is a way for a test to stop
after `Format` and look. That is an **internal seam**: private to the implementation, used by its own
tests, and not exposed through the package's interface.

The round trip stays for what it is for.

## User Stories

1. As a maintainer, I want to assert where a tick mark is without saving a PDF, so that a question
   about arithmetic is answered by arithmetic.
2. As a maintainer, I want to assert what a tick label says without decoding a glyph run, so that a
   test failure names the value rather than a glyph number.
3. As a maintainer, I want a rotated axis title assertable, so that the one case currently out of
   reach stops being out of reach.
4. As a maintainer, I want geometry tests to run without Ghostscript, so that they run on every
   machine and every CI leg.
5. As a maintainer, I want geometry tests to run without a rasterizing collection, so that they are
   not serialised behind the in-process Ghostscript.
6. As a maintainer, I want a failing geometry test to point at the renderer, so that I am not
   debugging a parser to find out about a plot area.
7. As a maintainer, I want the round trip kept for whether a chart draws at all, so that the
   end-to-end guarantee is not lost.
8. As a maintainer, I want golden images kept, so that what a chart looks like is still pinned.
9. As a maintainer, I want the seam internal, so that the package's public surface does not grow.
10. As a maintainer, I want the existing 110 tests to keep passing, so that the seam is added rather
    than swapped in.
11. As a maintainer, I want the tests that genuinely describe drawing to stay on the round trip, so
    that the split is by question rather than by convenience.
12. As a consumer of `PdfPinata.Charting`, I want no public type to change, so that this costs me
    nothing.
13. As a consumer, I want charts to render identically, so that this is provably a test-only change.

## Implementation Decisions

**This follows `axis-renderer-duplication.md` and does not precede it.** Adding a seam to four
near-copies means adding it four times and then removing three of them. Deepen the renderer first,
then put the seam on the one module.

**The seam is internal and stays internal.** Nothing here is a capability a consumer of the package
should have. `docs/specs/charting-renderer-findings.md` records that the renderers being internal is
the reason the tests are shaped as they are; the answer is a seam the tests can reach, not a public
API.

**Reaching it without `InternalsVisibleTo` is the open question, and it has three answers.** They
should be weighed explicitly rather than defaulted into:

- Add `InternalsVisibleTo` for the test assembly. Simplest, and would be the repository's first —
  eight source comments currently note its absence as a constraint, so introducing one is a decision
  about the whole codebase, not about charting.
- Link the renderer sources into the test project, as the four content readers are already linked.
  Consistent with existing practice and adds no attribute, but compiles the implementation twice.
- Make the geometry types public with doc comments saying what they are for. Honest, and grows the
  public surface for a testing reason, which is usually the wrong trade.

The first is recommended if the repository is willing to have one; the second if it is not.

**Nothing is deleted at the start.** The seam is added, geometry tests are written against it, and
only then are the round-trip tests that were asking geometry questions considered for removal — and
only where the round trip adds nothing to what the new test asserts. `DEEPENING.md`'s "replace, don't
layer" applies, but replacement follows the replacement existing.

**The split is by question, not by count.** A test asking *where is this* moves. A test asking *does
this draw* stays. A test asking *what does this look like* is a golden image and stays. There is no
target for how many move.

**`PaintedRectangles` and `ShownText` are not deleted.** They serve the round-trip tests that remain,
and `ShownText`'s `/ToUnicode` decoding is the only way to assert on drawn text end to end. They stop
being the only route in; they do not stop being useful.

**Rotation is the acceptance test.** If the seam is right, `AxisTitleTests.RotatedCaption` can assert
on a position and an angle instead of comparing content streams. If it still cannot, the seam is in
the wrong place.

## Testing Decisions

**A good test at the new seam asserts on computed geometry, not on drawing calls.** Renderer
parameters in, positions and sizes out. A test that asserts on the order in which `XGraphics` methods
were called is testing the implementation and will need changing the next time it is tidied.

**A good test at the old seam asserts on the content stream.** That is what it is for, and the
existing helpers do it well.

**Modules under test.** The axis renderers first, since they are the ones with the arithmetic and the
ones `axis-renderer-duplication.md` will have just merged. The plot-area, data-label and axis-title
renderers after, if the seam proves out.

**Prior art to follow rather than reinvent.** `PdfPinata.Charting.Tests/Helpers/Charts.cs` builds
the chart fixtures and is reusable unchanged — the arrangement half of every test does not change,
only the assertion half. `Drawn.cs` stays for the round trip. `PaintedRectangles`, `ShownText` and
the linked `StrokedLines`, `PageContent` and `TextOperators` stay for the same reason.

**The behaviours worth pinning at the new seam.** Tick positions for a given axis range and size.
Tick label text for a given format. Label count when the category list is shorter than the longest
series. A blank category read through `PointRendererInfo.Value`, which answers `NaN`, rather than
through `point.value`, which throws. Caption placement, including rotated.

**Golden images and the rasterizing collection are unchanged.** Anything that rasterizes stays in
`[Collection(RasterizingCollection.Name)]` and carries `[GoldenImageFact]`, which self-skips when
Ghostscript cannot rasterize. The new geometry tests belong to neither, which is much of the point.

**Judge the run by its exit code, not by the word `Passed`.** Unchanged, and the reason the new tests
are worth having: a geometry test that does not rasterize cannot take the host down with it.

## Out of Scope

- **Making the renderers public.** Not proposed, and the seam is designed to avoid it.
- **The rest of the renderer family's duplication.** Column, bar, line, pie and area renderers have
  their own; `axis-renderer-duplication.md` covers the axes only.
- **`PdfSharp.Charting.Font`, the hand-mirrored second `Font` type.** Different problem.
- **New charting features.** Nothing here changes what a chart can do.
- **Sharing the test helpers as a package rather than by link.** The link mechanism is established
  and works; changing it is `docs/specs/font-seam-contracts.md`'s neighbourhood, not this.
- **Deleting round-trip tests wholesale.** Only where a new test genuinely replaces one.

## Further Notes

The ratio is the argument: 515 lines of decoder so that a test can ask what a 338-line renderer
computed, and one geometry case still unreachable at the end of it. That is not a criticism of the
decoder, which is careful work and is the reason charting has 110 tests at all. It is an observation
that the only available seam is a long way from the question.

`docs/specs/charting-renderer-findings.md` records seven defects found by writing those tests, all
reachable through public API and all since fixed. That is the case for the tests existing. This
proposal is about making the next seven cheaper to find, and about the eighth — the one in a rotated
caption, which nothing can currently assert.

Do not start here. Start with `axis-renderer-duplication.md`, or this seam gets built four times.
