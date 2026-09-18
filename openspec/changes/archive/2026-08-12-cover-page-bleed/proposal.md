## Why

`PdfPage.TrimMargins` works. Setting it moves the drawing origin to the trimmed page's top-left,
lets negative coordinates reach into the bleed, and writes all five page boxes on save. Proved by
drawing a band at `(-3mm, -3mm)` on an A5 page with a 3mm trim margin:

```text
MediaBox = [0 0 437.008 612.008]      TrimBox = [8.504 8.504 428.504 603.504]
BleedBox = [0 0 437.008 612.008]      ArtBox  = [8.504 8.504 428.504 603.504]

1 0 0 1 8.503937 -8.503937 cm         the origin, moved to the trim corner
-8.504 560.512 437.008 60 re f        the band, flush with the media box
```

Nothing tests it. `TrimMargins` has **zero** occurrences in `PdfPinata.Test`, so every part of
that — the translate, the added page height, the five boxes — is unguarded. The demonstration app
does not use it either, and `docs/specs/demonstration-app.md` still lists "nothing that bleeds a
photograph off the edge of a page" among the things the library cannot do, which is wrong.

This is the same class of problem the `fix-drawing-gaps` change existed to remove, arrived at from
the other side: there, features silently did nothing; here, a feature silently does the right thing
and no one knows. A working feature nobody can find and nothing protects is one refactor away from
being a broken feature nobody notices.

Two real gaps sit beside it. `BleedBox` is written equal to `MediaBox`, so there is no room outside
the bleed for crop marks and none are drawn. And a MigraDoc document cannot bleed at all through the
normal path: `PdfDocumentRenderer.RenderPages` creates each page itself and never sets trim margins,
and `PageSetup` has no bleed to set.

## What Changes

- **`page-bleed` is specified and tested.** What `TrimMargins` means for the drawing origin, for the
  page height, and for each of the five page boxes, with tests covering all of it. This pins
  behaviour that exists rather than adding any.
- **A `Bleed` demo** in the demonstration app: an image bled off three edges of a trimmed page, with
  the trim edge marked so the bleed is visible on screen. It is the demo `Magazine` should have been
  able to reach for.
- **Crop marks, decided one way or the other.** Either `PdfPage.CropMarks` draws the eight standard
  marks in the margin between the bleed and the media box — which requires `BleedBox` to stop being
  `MediaBox` — or the change records why the library does not draw them and leaves the caller to. The
  design settles it; the proposal does not pre-empt it.
- **A documented route from MigraDoc.** `DocumentRenderer.RenderPage` onto a caller-made page already
  works. Whether `PageSetup` gains a bleed of its own is a decision for the design; either way the
  route is written down and covered by a test.
- **`docs/specs/demonstration-app.md` corrected** — bleed moves out of the list of things the library
  cannot do.

Not in scope, and deliberately: printer's marks other than crop marks (registration targets, colour
bars, slug text), and any change to how `XGraphics` maps units. `TrimMargins` asserts point units
today and continues to.

## Capabilities

### New Capabilities

- `page-bleed`: what a trim margin does to the drawing origin, the page height and the five page
  boxes, and what a caller has to do to bleed content past the trim edge.

### Modified Capabilities

None. No existing spec's requirements change; this pins behaviour that has never been specified.

## Impact

**Code**

- `PdfPinata/Pdf/PdfPage.cs` — `PrepareForSave` writes the boxes. Changes only if crop marks are
  taken up, which needs `BleedBox` to sit inside `MediaBox`.
- `PdfPinata/Drawing.Pdf/XGraphicsPdfRenderer.cs` — `BeginPage` reads the trim offset. Expected to
  be read and tested, not changed.
- `PdfPinata.Test/` — a new test class; nothing exists to extend.
- `SampleApp/Demos/BleedDemo.cs` — new, plus its registry entry.

**Packages**: additive at most. `PdfPage.TrimMargins` is public API already.

**Behaviour**: a change to `BleedBox` would alter the boxes written for every page that sets trim
margins. That is a **BREAKING** output change for prepress consumers and is why the design has to
settle it rather than assume it.
