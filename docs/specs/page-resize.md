# Spec — resizing a page that already has content on it

What was built for the page-resize capability, and what was deliberately left out.

| item | what | status |
|---|---|---|
| 1 | `PdfPage.Resize` / `PdfDocument.ResizePages` — content, boxes, annotations | done |
| 2 | Destinations that point at a resized page are found and moved | done |
| 3 | `Size`, `Width`, `Height` setters refuse a page that has content | done, **breaking** |
| 4 | A second resize adjusts the wrapper rather than nesting another | done |
| 5 | Encrypted, signed and tagged documents are refused | done |

---

## The defect

`page.Size = PageSize.A5` wrote a new `/MediaBox` and nothing else. On a page that had been drawn
on or imported that crops the bottom-left A5's worth out of the A4 and discards the rest, with no
exception and no warning. `Width` and `Height` had the same hole.

Worse than merely wrong: the crop was anchored at the **origin**, and the origin of a PDF page is
its bottom-left corner. So the part that survived was the foot of the page and the part thrown away
was the heading.

```text
      A4 page              what page.Size = A5 kept
      ┌─────────────┐      ┌─────────────┐
      │ Heading     │      │░░░░░░░░░░░░░│  ← cropped away
      │ body text   │      │░░░░░░░░░░░░░│
      │ body text   │      ┌───────┐░░░░░│
      │ body text   │      │body   │░░░░░│  ← kept
      │ footer      │      │footer │░░░░░│
      └─────────────┘      └───────┴─────┘
```

The only working route was to build a second document and draw the source page into it as an
`XPdfForm`. That draws correctly and loses everything else — annotations, links, and the ability to
work in the document already in hand.

---

## Item 1 — the resize itself

### The mechanism

The content is not rewritten. It is moved whole into a form XObject and the page is given a single
content stream that draws that form under a transform.

```text
BEFORE                                    AFTER
┌─ page ───────────────┐                 ┌─ page ───────────────────────────────┐
│ /MediaBox [0 0 595 842]                │ /MediaBox [0 0 420 595]              │
│ /Contents ──► [bytes] │                │ /Contents ──► "q .7071 0 0 .7071 0 0 │
│ /Resources ──► {R} ◄──┼── shared with  │                cm /Fm0 Do Q"         │
│ /Annots   ──► [...]   │   other pages  │ /Resources ──► NEW {/XObject</Fm0 ►┐>}│
│ /Group    ──► (maybe) │                │ /Annots   ──► [... /Rect scaled]    ││
└───────────────────────┘                └─────────────────────────────────────┼┘
                                          ┌─ Form XObject ─────────────────────┘
                                          │ /BBox      [0 0 595 842]   ← source rect
                                          │ /Resources ──► {R}         ← same ref, untouched
                                          │ /Group     ──► copied from the page
                                          │ /Filter    ──► preserved
                                          │ stream     ──► [bytes] verbatim
                                          └────────────────────────────
```

Three reasons for a form rather than a `cm` in front of the content that is already there:

- **Unbalanced `q`/`Q`.** Real content streams have them. An extra open `q` swallows the trailing
  `Q` and the resize transform never unwinds; an extra `Q` tears it down mid-page and the rest
  draws at full size. `Do` on a form XObject saves and restores the graphics state implicitly
  (PDF 32000 §8.10.1), so nothing inside can escape. Pinned by two tests.
- **`/BBox` keeps the existing crop.** Content that fell outside the page stays outside once the
  page shrinks and slack appears around it.
- **It can be undone.** Nested `cm`s cannot be told apart from the content; a marked wrapper can.
  That is what item 4 rests on.

### What the source rectangle is

`CropBox ?? MediaBox` — a reader is shown the crop box, so on a page that has one that *is* the
page, and resizing relative to the media box gives a visibly wrong answer.

A crop box is not allowed to reach outside the media box, and where one does a reader takes the
part that lies within (ISO 32000 §14.11.2). So does this: without the intersection, a page whose
crop box is larger than its media box would be resized from a rectangle bigger than the page and
everything would come out too small. The same rule applies on the way out — `/CropBox`,
`/BleedBox`, `/TrimBox` and `/ArtBox` all travel with the content and are then clipped to the new
media box, because `Fill` and `None` both let the content overflow the page on purpose and a box
that overflowed with it would leave a page that is not well formed.

Two traps, both of which read as correct and are not:

- **`page.CropBox` creates one.** Its getter is `GetRectangle(key, create: true)`, so asking
  whether a page has a crop box gives it an empty one. The element is read raw. The same is true of
  `page.Contents` (rewrites `/Contents` into an array) and of
  `SecuritySettings.SecurityHandler` (resolves `/Encrypt` with `VCF.CreateIndirect`, so asking
  whether a document is encrypted encrypts it).
- **`MediaBoxIsTurnedWhenWritten` is a write-time hack.** `PdfPage.WriteObject` swaps the media box
  of a page marked `PageOrientation.Landscape` while it serializes and swaps it back afterwards.
  So for such a page the in-memory `MediaBox` is *not* the space the content is drawn in — the
  swapped one is. The source rect is turned to match, and `PdfPage.ApplyResizedBox` clears the
  authoring orientation afterwards, or the write-time swap would turn the finished box again and
  undo the work.

### /Rotate

Kept exactly as it was, and all the arithmetic done in unrotated media-box coordinates. Annotation
rectangles and destination coordinates already live in that space, so one matrix covers the content
and the metadata alike. The media box written for a page turned by a quarter is the target with its
sides swapped, so that `page.Width` and `page.Height` go on reporting the size the reader sees.

### The arithmetic

`PageFit.Calculate(source, target, options)` — a pure function of two rectangles and the options,
with no PDF types in the signature, so it is pinned on its own by 31 tests rather than re-derived
inside every integration test. It is **public**: the repository has no `InternalsVisibleTo`, and it
is a reasonable thing to expose in any case.

`XRect` is the trap there. It comes from a world where y runs down the page, so its `Top` is the
side with the *smaller* y — the bottom, in PDF. Only `X`, `Y`, `Width` and `Height` are used.

---

## Item 2 — the destination sweep

The expensive half, and the reason a resize is a document-wide operation wearing a page-scope name.
A page carries no list of what points at it, so all of this has to be looked at:

```text
every page's /Annots  ──► /Dest, or /A << /S /GoTo /D >>
/Outlines tree        ──► /Dest, or /A
catalog /Names /Dests name tree, and the legacy /Dests dictionary
catalog /OpenAction
```

Gated on `/S` being `/GoTo`, as the import path was taught to for #461 — a `/GoToR` names a page in
another file.

One thing the import path did not need: **a destination array can be indirect and shared by several
links.** Moving it once for each link that finds it would move it several times over. Each array is
moved once, tracked by object identity.

| form | what moves |
|---|---|
| `[p /XYZ l t z]` | `l` and `t`. **`z` is never touched** |
| `[p /FitR l b r t]` | all four |
| `[p /FitH t]`, `/FitBH` | `t` — becomes a `/FitV` when the page is turned |
| `[p /FitV l]`, `/FitBV` | `l` — becomes a `/FitH` when the page is turned |
| `[p /Fit]`, `[p /FitB]` | nothing |

### Why the zoom is not scaled

An earlier draft scaled it inversely, so that a destination would show text at the size it showed
before. That is wrong, and enlargement is what shows it:

| | shrink A4 → A5 | enlarge A5 → A4 |
|---|---|---|
| leave `z` | shows at 100%, like the rest of the shrunk document | shows bigger — the reason it was enlarged |
| invert `z` | jumps to 141%, a magnification nothing else in the document uses | shrinks back to the original apparent size, undoing the enlargement at the moment the reader arrives |

A destination is a view into the document, not a promise about physical text size. Leaving `z`
alone is also the simpler implementation and makes `z` of `0` or null — what Word, hyperref and
Acrobat all emit, and therefore most real destinations — need no special case.

A quarter turn converts `/FitH` to `/FitV` rather than approximating: the line `y = t` becomes
`x = M21·t + OffsetX`, which no longer depends on `y` at all, so the conversion is exact.

---

## Item 3 — the breaking change

`Size`, `Width` and `Height` throw `InvalidOperationException` naming `Resize` when the page has
content. On a page with none — the overwhelmingly common `AddPage(); page.Size = A4;` — nothing
changes.

Not "silently do the resize instead", for two reasons. A resize has irreducible parameters that a
property setter cannot take, so doing it silently buries a policy choice in an assignment. And it
turns an O(1) assignment into a document-wide mutation touching every other page's links.

Blast radius in this repository was one line, and it turned out not even to need changing:
`XTextFormatterTest` sets `Size` before drawing. MigraDoc sets `pdfPage.Width`/`Height` on blank
pages and is unaffected.

`PageResizeOptions.Crop` is the one-line replacement for callers who really did want a crop — but
it anchors **top-left**, not bottom-left. The old anchoring was an artefact of the coordinate
system rather than anybody's intent and is not reproduced. A caller who wants it can ask for
`PageAlignment.BottomLeft`.

---

## Item 4 — resizing twice

The wrapper carries a private key, `/PdfPinataResizeWrapper`. A page whose `/Contents` is
*exactly* the eleven tokens this writes — `q`, six numbers, `cm`, a name, `Do`, `Q` — over a form
carrying that key is resized by rewriting the transform rather than by wrapping again.

The new transform is worked out afresh from the form's `/BBox`, which is the rectangle the content
occupied to begin with. Not composed onto the transform already there: composing would accumulate
rounding, and A4 → A5 → A4 has to come back exactly where it started. The rectangle is not recorded
separately from `/BBox`, because a second copy could only drift from it.

The boxes, the annotations and the destinations are all in the coordinates of the page *as it is
now*, not of the content inside the wrapper, so they move by the difference between the old
transform and the new one. The two are the same thing the first time a page is resized.

Anything unexpected — a second content stream, an extra operator, a missing marker, a transform
that cannot be inverted — falls back to wrapping again. Nesting costs a few bytes; rewriting the
transform of something that is not a wrapper loses the page.

---

## Item 5 — what is refused

Checked once, up front, before anything is mutated — `ResizePages` must not leave a document half
resized.

- **Encrypted** — meaning a document that came *out of* an encrypted file, whose streams were
  decrypted on the way in and have to be encrypted again on the way out. Rewriting content streams
  needs the security handler in the loop.
  A document built in memory with a password set on it is deliberately **not** refused: nothing is
  encrypted yet and the password is an instruction for the save. The two cannot be told apart from
  the trailer — setting a password reaches `SecurityHandler`, whose getter resolves `/Encrypt` with
  `VCF.CreateIndirect` and creates the entry there and then — so what distinguishes them is
  `PdfDocument.IsImported`.
- **Signed** (`/Sig` field in `/AcroForm`, following `/Kids`, depth-capped). The signature would no
  longer verify.
- **Tagged** (`/StructTreeRoot` on the catalog). This one is refused rather than merely documented
  because the damage does not show: moving content into a form breaks the `/StructParents` mapping
  into the structure tree, and the page goes on rendering perfectly, at an unremarkable size,
  passing any golden image, while nothing describes it any more. The caller would find out from a
  screen-reader user. Keyed off the catalog rather than off a page's `/StructParents`, so a stray
  key on one page does not lock the feature out.
- **Not open for modification**, and a page with a live `XGraphics` on it.

---

## Verification

- `src/PdfPinata.Test/Drawing/PageFitTests.cs` — 31 tests over the arithmetic alone: four fit modes,
  nine alignments, margins, auto-rotate on matching and opposing aspects, sources away from the
  origin, and the guards. Asserts where corners land, not what the matrix components are.
- `src/PdfPinata.Test/IO/PageResizeTests.cs` — the content really moves, via a walker that follows
  the `Do` into the wrapper (`Helpers/ResizedContentProbe.cs`); unbalanced `q` and `Q`; `/Group`;
  compressed content not recompressed; two pages sharing a resource dictionary; the caches on
  `Resources` and `Contents`; resizing twice and three times; every refusal; save and read back.
- `src/PdfPinata.Test/IO/PageResizeAnnotationTests.cs` — every geometry entry, the appearance stream
  left byte-identical, an unknown subtype, the pass turned off, and a turned page.
- `src/PdfPinata.Test/IO/PageResizeDestinationTests.cs` — every destination form and every place one
  can hide, the zoom unchanged on both a shrink and an enlargement, `/GoToR` left alone, a shared
  array moved once.
- `src/PdfPinata.Test/IO/PageResizeRenderingTests.cs` — `FamilyTree.pdf`, `test.pdf` and `Pdf20.pdf`
  rasterized before and after. Resized to the size they already are, and down to half and back
  again: both have to render identically, and do. This is what says a font, a shading or a clipping
  path survived the move, which no content walk can.

---

## Not in scope

- **Tagged PDF.** Refused rather than supported — see item 5. Carrying `/StructParents` into the
  form and rewriting the parent tree is a feature of its own, and the refusal is what keeps that
  option open rather than shipping a quiet corruption first.
- **`/DA` font sizes in form fields.** A widget with a fixed `/Helv 12 Tf` does not scale, so field
  text comes out proportionally large. Auto-size (`0 Tf`) is fine. The same goes for `/BS /W`
  border widths.
- **Reflowing text.** A resize scales what is drawn; it does not re-wrap paragraphs. That is
  MigraDoc's job, at authoring time, through `PageSetup`.
- **`/UserUnit`.** Considered seriously and rejected. It scales the interpretation of default user
  space, so content, annotation rectangles, destination coordinates and appearance streams would
  all scale together for one dictionary entry and no risk at all — the key is already declared at
  `PdfPage.cs` and unused. But it is PDF 1.6 where `PdfDocument._version` is `14`, support outside
  Acrobat and Ghostscript is patchy, it cannot change an aspect ratio or an orientation, and
  anything reading `/MediaBox` to choose paper still sees A4. A mechanism that is perfect sometimes
  and silently degrades otherwise is worse than one that always works. Nothing was built for it and
  there is no way to ask for it today; it is recorded here as a route that stays open should the
  pure uniform case ever want a fast path.

---

## Turned up on the way

`PdfArray.WriteObject` writes no brackets around an **indirect** array — it emits the object header
and then the elements, so an indirect array of one reference is written as a bare `8 0 R` where an
object should be, and the file will not open again. This is why `PdfPage.ReplaceContents` keeps the
`PdfContents` array direct, which is also what the `Contents` property does (it asserts
`Reference == null`). Cost an hour and an unreadable PDF to find; worth knowing before making any
other array indirect.

**The test harness leaked every bitmap it ever rasterized, and it was killing the test host.**
Found while adding the rendering tests above, and fixed; the fix is not part of the resize feature
but it is what makes the suite finish.

`PdfHelper.Rasterize` handed back a `MagickImageCollection` that no caller disposed, and
`PdfHelper.Diff` leaked all three of the images it opened. A page rasterized at 300 dpi is tens of
megabytes of bitmap held in **unmanaged** memory, and the garbage collector sees only the small
managed wrapper around it — so nothing about holding a dozen of them makes a collection happen any
sooner. The process simply runs out of memory and is killed.

The symptom is what makes this worth writing down: `Test host process crashed` and
`Test Run Aborted`, with **zero failing tests** and a passing count quietly short of the total.
That reads like flakiness, and it is not. Two further things led the diagnosis astray:

- the whole-solution `dotnet test` had been crashing this way for some time, so a per-project crash
  looked like more of the same pre-existing fault. It was the same fault, but it was not
  pre-existing in the per-project run — adding three rendering tests is what tipped that one over;
- it looked like a *concurrency* problem, because the whole-solution run crashes soonest and that
  run has two test hosts going at once. Two hosts leaking simply reach the ceiling faster.

With the leaks fixed, both crashes are gone: the whole solution runs green, and the rendering tests
here were restored to the fuller coverage they had been cut back to fit under the old ceiling.
`GhostscriptSetup.Probe` and `PostscriptOutlineEmbeddingTest` already disposed what they
rasterized, so the pattern was known — it just was not applied anywhere else.

**`GetReal` does not follow a reference — it throws on one.** Any object in a PDF may be indirect,
a coordinate in an array included, and `PdfArray.ArrayElements.GetReal` handles `PdfReal`,
`PdfRealObject`, `PdfInteger` and `PdfIntegerObject` but not a `PdfReference` to any of them: it
falls through to an `InvalidCastException`. So reading coordinates with it turns a legal if unusual
file into a failed resize — and, because the transform writes as it reads, one that fails *after*
the content has been wrapped and the boxes moved, leaving the page half done with no way back.

Everything here reads coordinates through `PdfPageResizer.TryNumber`, which follows the reference
and answers false rather than throwing. The readers also take the whole array before writing any of
it, so an array that cannot be read entirely is left exactly as it was found. Worth knowing beyond
this feature: the same `GetReal` sits under a good deal of the library.

`PdfDictionary.GetMatrix` cannot read back what `SetMatrix` writes. `SetMatrix` stores a
`PdfLiteral` and `GetMatrix` throws `NotImplementedException("Parsing matrix from literal")` on it.
Not fixed here — nothing in the resize path needs it — but a test that reads an annotation's
`/Matrix` back has to compare the written form instead.
