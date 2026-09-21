# Spec — reading a page box without writing one

A retrospective. Upstream PDFsharp 7.0.0 added `HasCropBox`, `EffectiveCropBoxReadOnly` and their
siblings (its change log, "MediaBox, CropBox, BleedBox, ArtBox, TrimBox changes"), because its
getters create the entry they are asked about. This fork, descended from PDFsharp 1.5 through
PdfSharpCore, had the same getters with the same fault, and the idea is ported here rather than the
names.

| item | what | status |
|---|---|---|
| 1 | Reading `MediaBox`, `CropBox`, `BleedBox`, `TrimBox` or `ArtBox` leaves the page as it was | done |
| 2 | `HasMediaBox`, `HasCropBox`, `HasBleedBox`, `HasTrimBox`, `HasArtBox` | done |
| 3 | `EffectiveCropBox`, `EffectiveBleedBox`, `EffectiveTrimBox`, `EffectiveArtBox` (ISO 32000-1 14.11.2) | done |
| 4 | A landscape page with no media box is not given an empty one when saved | done |
| 5 | Changing what an absent box's getter answers | deliberately not done |
| 6 | An `EffectiveMediaBox` | deliberately not done |
| 7 | Resolving inheritance again at read time | deliberately not done |

`src/PdfPinata.Test/Pdfs/PageBoxTests.cs` pins all of it.

---

## What was wrong

Every box getter called `Elements.GetRectangle(key, create: true)`, which stores a new empty
`PdfRectangle` under the key when the page has none. So asking a page whether it had a trim box gave
it `/TrimBox [0 0 0 0]`, and the page was saved with it. For the trim, bleed and art boxes that is a
page going to a press that says it is cut to nothing. For the crop box it is worse: a crop box of no
size is the region a reader is told to show and print, and it got there because somebody read a
property.

The library knew. `PdfPageResizer` read the dictionary directly with a comment saying why, and
`PdfPage.Initialize` read `MediaBox` for the sole purpose of creating it (`_ = MediaBox;`), which
was redundant — `Size` has already set it by then.

## What was built

The getters read the entry and write nothing. Each one answers what it always answered for an
absent box, the empty rectangle, so a caller that tested `IsEmpty` sees no change. Two things are
different: the entry no longer appears in the file, and an entry that is not a rectangle at all -
`/CropBox 42` - is read as no box rather than throwing `InvalidCastException`.

`Has…Box` says whether the page states the box. An entry whose value is null, directly or by
reference, or is not an array of four numbers, counts as no box — the same reading
`PdfPageResizer.RectangleOf` already made, which the page now shares rather than keeping a copy.

`Effective…Box` is the box a reader uses. ISO 32000-1 14.11.2: the crop box defaults to the media
box; the bleed, trim and art boxes default to the crop box; and any of them reaching beyond the
media box is "effectively reduced to" its intersection with it. Only the media box clips. A stated
trim box larger than the crop box is left larger, because the standard says nothing more. The
result is always lower-left corner first, and the empty rectangle when the box lies wholly outside
the media box.

## Two choices worth knowing

**The media box is taken as written.** A page built here as `PageOrientation.Landscape` holds its
media box upright while it is drawn on and turns it over in `WriteObject`, while the other four
boxes are written as they are. The effective boxes are meant to agree with the file, so they are
measured against the turned media box. For such a page `EffectiveCropBox` is therefore not equal to
`MediaBox` even when there is no crop box. `TurnedOver` is the one place both read the turn from.

**A page with no media box is not clipped.** The media box is required, but a file can leave it
out. Clipping against the empty rectangle would make every effective box empty, which says less
than the box the page states, so the stated box is answered as it is.

## Deliberately not done

- **An absent box still answers the empty rectangle** rather than its effective value. Changing it
  would silently move every caller that reads `CropBox` today. The effective value is a property
  of its own.
- **No `EffectiveMediaBox`.** There is nothing for the media box to default to and nothing to clip
  it against.
- **Inheritance is not resolved again.** `PdfPage.InheritValues` copies a media box and a crop box
  stated on a node of the page tree onto each page as the document is read, so by the time a getter
  runs the inherited box is the page's own.
- **`PdfDictionary.GetRectangle(key, create)` is unchanged.** It is public and its contract is
  documented; the page simply stops asking it to create.
