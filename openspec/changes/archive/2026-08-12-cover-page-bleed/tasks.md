Groups 1 and 2 pin and demonstrate what already works and do not depend on the crop-mark decision.
Group 3 is the only one that changes behaviour, and is gated on that decision being made.

## 1. Pin what is already there

- [x] 1.1 Write `PdfPinata.Test/Drawing/PageBleedTests.cs` against the **current** behaviour: the
      origin on the trim corner, a negative coordinate reaching the sheet edge, `Width`/`Height`
      still reporting the trimmed size, and the five boxes with their measured values. Write it
      before changing anything — the point is to find out what the code does, not to confirm what
      it ought to do.
- [x] 1.2 Assert the nesting rule — `TrimBox` within `BleedBox` within `MediaBox` — and record in
      the test what the current values make of it. `BleedBox == MediaBox` satisfies nesting; note
      it rather than fix it here.
- [x] 1.3 Add a rasterizing test in `[Collection(RasterizingCollection.Name)]` that a band bled off
      an edge puts ink on the sheet's outermost pixels. Reading the content stream proves the
      operators were written; only rasterizing proves nothing clipped them away.
- [x] 1.4 Test that a page with no trim margin gains no `/TrimBox`, `/BleedBox` or `/ArtBox`, so the
      whole feature stays invisible to every document that does not ask for it.
- [x] 1.5 Report anything found that contradicts `specs/page-bleed/spec.md`. Amend the spec to what
      is true and say so, rather than fixing the code inside this task — a behaviour change here
      needs its own decision. **Three departures found**, all sharing one cause — `PrepareForSave`
      derives the sheet from `Width`, and `Width` reads the media box it then overwrites. Recorded
      in the spec and pinned by the three `DEFECT_` tests.

## 2. Show it, and say where it applies

- [x] 2.1 Add `SampleApp/Demos/BleedDemo.cs`: one trimmed page, an image bled off three edges, and a
      thin rule marking the trim boundary so the bleed is visible on screen. Label the rule on the
      page as part of the demonstration rather than part of the artwork.
- [x] 2.2 Register it, give it `Shows` entries and a `PageCount`, and check the demo smoke tests
      cover it. Remember the two rules in `docs/specs/demonstration-app.md`: a demo never registers
      a backend, and its assets are embedded resources.
- [x] 2.3 Test the MigraDoc route — a caller-made trimmed page, an `XGraphics` on it, and
      `DocumentRenderer.RenderPage` — asserting the layout is measured from the trimmed page and the
      saved page carries the boxes.
- [x] 2.4 Correct `docs/specs/demonstration-app.md`: bleed comes out of the list of things the
      library cannot do, with a pointer to the demo. The doc no longer claimed bleed was impossible,
      but `Magazine` did describe an image running to the edge of an ordinary page as "bled off
      three edges" — so the correction is a section distinguishing the two, not a deletion.
- [x] 2.5 XML-document `PdfPage.TrimMargins` with what it moves, what it writes, and the point-units
      restriction. It is public API with no documentation at all today.

## 3. Crop marks

- [x] 3.1 **Settled: option 2, correct the boxes properly.** `TrimMargins` itself now produces the
      nesting the specification describes, `MarkMargins` gives the room outside the bleed, and the
      marks are drawn. This is a breaking change to the boxes of every trimmed page, chosen over
      the recommended option 3 deliberately — `MarkMargins.All = 0` is the escape hatch and
      reproduces the old boxes exactly.
- [x] 3.2 Grow `MediaBox` past `BleedBox` by the mark allowance and draw the eight standard marks in
      the space that opens up, on the sheet and clear of the bleed. Two meet at each corner, each
      running outward from the bleed to the sheet edge; drawn in a content stream of their own, in
      sheet coordinates, so no transformation the caller was under can move them.
- [x] 3.3 Test that a page with the allowance cleared is written as it was before the change, and
      that a page with no trim margin is untouched by any of it. That is what makes "nothing changes
      for a document that does not ask for it" a fact rather than an intention.
- [x] 3.4 Record the nesting rule in the spec — sheet, bleed, trim, and what each is the answer to —
      so the boxes read as a decision rather than as numbers copied from an InDesign file.
- [x] 3.5 Add a line to the release notes: an addition for `MarkMargins` and `DrawCropMarks`, and a
      **BREAKING** entry for the boxes.

## 3b. The three defects, fixed

Added after task group 1 found them. Their tests were written before the fix and now assert it.

- [x] 3b.1 Remember the size the page was asked for before the media box is grown into the sheet, so
      that a second save writes the same sheet rather than adding the margins again.
- [x] 3b.2 Have `Width` and `Height` go on reporting the page after it has been saved, rather than
      the sheet the media box has become.
- [x] 3b.3 Inset each edge of `/TrimBox` by its own margin. Y1 is the bottom edge in PDF space, so
      the bottom margins go there and the top margins come off Y2.
- [x] 3b.4 Add the release-note entries under Fixed.

## 4. Close out

- [x] 4.1 `./ci-build.ps1` clean and `dotnet test` green on both target frameworks. 1652 passed,
      1 skipped, on each of net8.0 and net10.0.
- [x] 4.2 Rasterize the `Bleed` demo and look at it. A bleed that is wrong by 3mm is invisible in any
      assertion that does not go to the pixel, and obvious to the eye against the trim rule. The
      photograph runs past the dashed rule and stops at the bleed; the eight marks are at the
      corners, clear of it.
- [x] 4.3 Open the demo's PDF in a reader that shows page boxes, or dump them, and check the sheet
      and trim are where a prepress operator would expect:

      /MediaBox [0 0 465.354 640.354]      the sheet
      /CropBox  [0 0 465.354 640.354]
      /BleedBox [14.173 14.173 451.181 626.181]    inset by 5mm
      /TrimBox  [22.677 22.677 442.677 617.677]    inset by 5mm + 3mm, and measuring A5 exactly
      /ArtBox   [22.677 22.677 442.677 617.677]
