## 1. Options and geometry

- [x] 1.1 Add `PageFitMode` (`Fit`, `Fill`, `Stretch`, `None`) in `PdfPinata/root/enums/`,
      file-scoped namespace, XML docs on every member.
      Also added `PageAlignment` (nine-way) — no such enum existed to reuse.
- [x] 1.2 Add `PageResizeOptions` in `PdfPinata/root/` — `Fit`, `Alignment`, `Margin`,
      `AutoRotate`, `ScaleAnnotations`, `ScaleDestinations`. Remember `LangVersion=latest` unlocks
      syntax only: `init` accessors need the `IsExternalInit` polyfill already in
      `PdfPinata/!internal/`, so check it is reachable before using them, or use plain setters.
      **Checked: there is no `IsExternalInit` polyfill in `!internal/`** — it holds only
      `Configuration.cs` and the two `DynamicallyAccessedMembers` files. Used plain settable
      properties rather than adding a polyfill for one options bag.
- [x] 1.2a Add the two presets: `PageResizeOptions.Default` (`Fit`, centred) and
      `PageResizeOptions.Crop` (`None`, **top-left**). `Crop` deliberately does not reproduce the
      old setter, which anchored at the bottom-left origin and so cropped the heading off the top.
      Say so in its XML doc — someone will read `Crop` as bug-compatibility otherwise.
      Both are properties returning a fresh instance, not static fields: the class is mutable, so
      a shared singleton would let one caller's `Margin` leak into everybody else's resize.
- [x] 1.3 Write the fit calculation as a pure static function: source rect + target rect + options
      → `XMatrix`. No PDF types in the signature, so it is testable on its own.
      `PdfPinata/root/PageFit.cs`. **Public, not internal** — the repo has no
      `InternalsVisibleTo` anywhere, so 1.4 could not test it directly otherwise. Defensible on its
      own merits: no PDF types in the signature and useful to anyone placing an `XPdfForm` by hand.
- [x] 1.4 Unit-test 1.3 directly across all four fit modes, all nine alignments, a margin, and
      auto-rotate on matching and opposing aspects. This is where the arithmetic gets pinned; the
      integration tests should not have to re-derive it.
      `PdfPinata.Test/Drawing/PageFitTests.cs`, 31 tests, asserting on where corners land rather
      than on matrix components. Includes the `XRect.Top`-is-not-the-top trap as its own test.
- [x] 1.5 Resolve the source rect: `CropBox ?? MediaBox`, normalised through `/Rotate`, in unrotated
      media-box coordinates. Cover a page with a non-zero media-box origin — `PdfPage.MediaBox`
      warns that `XGraphics` cannot handle one, and real files have them.
      `PdfPageResizer.SourceRectOf`. Two traps found: `page.CropBox` passes `create: true`, so
      *reading* it gives the page a crop box — the element is read raw instead. And
      `MediaBoxIsTurnedWhenWritten` is a write-time hack (`PdfPage.WriteObject`), so for a
      landscape-authored page the in-memory `MediaBox` is **not** the space the content is drawn
      in; the rect is turned to match what actually gets written.

## 2. Same-document form wrap

- [x] 2.1 Split `PdfFormXObject` so the page-to-form work is reachable without a
      `PdfImportedObjectTable`. Today the only constructor doing it (`PdfFormXObject.cs:60-150`) is
      hardwired to an external document. Keep the existing constructor's behaviour byte-identical —
      the import path is well covered and must not shift.
      Added a same-document constructor **alongside** the import one rather than refactoring a
      shared core out of it: the overlap turned out to be small (the import path also imports a
      closure and bakes `/Rotate` into a `/Matrix`, neither of which the resize wants), so sharing
      would have meant reshaping the import path for no gain. It is untouched.
- [x] 2.2 Build the wrapper: `/BBox` = source rect, `/Resources` = the page's existing resources
      reference (moved, not copied — nothing shared is mutated), `/Group` copied from the page if it
      has one, `/Filter` preserved, stream moved verbatim without recompressing.
      One content stream (nearly every page) hands its bytes over with its `/Filter` and
      `/DecodeParms` untouched — nothing is decoded. Several streams have to be run together, which
      cannot be done without decoding, so that case re-compresses only if the document is set to.
      Note the import path's "preserve filter" is a no-op — `CreateSingleContent` returns unfiltered
      bytes and never sets `/Filter` — so this is the first path that really does preserve it.
- [x] 2.3 Give the page a fresh `/Resources` holding only the wrapper, through
      `PdfPage.ReplaceResources` (`PdfPage.cs:562`) — writing `Elements["/Resources"]` alone leaves
      a page that was asked for its resources earlier still answering with the old ones.
      `_contents` is cached in exactly the same way, so a matching `PdfPage.ReplaceContents` was
      needed too — without it the page goes on handing out the streams it no longer holds.
- [x] 2.4 Write the page's new `/Contents`: `q <matrix> cm /Fm0 Do Q`.
- [x] 2.5 Mark the wrapper with a private key naming it as a resize wrapper and recording the source
      rect, for task 5.
      `PdfFormXObject.ResizeWrapperKey`. The source rect is not stored twice: it *is* the `/BBox`
      by construction, and a second copy could only drift from it.
- [x] 2.6 Set `/MediaBox` to the target and transform `/CropBox`, `/BleedBox`, `/TrimBox`,
      `/ArtBox`. Leave `/Rotate` as it was.
      The media box also has to be written swapped when the page is turned by a quarter, so that
      `page.Width`/`Height` report the target as the reader sees it. `PdfPage.ApplyResizedBox`
      clears the authoring orientation at the same time, or `WriteObject` would turn the box again
      and undo the work.

## 3. Annotations

- [x] 3.1 Transform `/Rect` on every annotation of the page.
      `PdfAnnotationTransformer`. Reads `page.Elements[Keys.Annots]` raw — `page.Annotations` would
      give a page without any an empty array to hold.
- [x] 3.2 Transform per-subtype geometry: `/QuadPoints`, `/InkList` (array of arrays), `/Vertices`,
      `/L`, `/CL`, `/RD`.
      `/RD` needed care: it holds four *distances* (insets), not points, so it scales rather than
      transforms — and under auto-rotate the four go round the page with everything else, so which
      inset is which changes.
- [x] 3.3 Leave `/AP` appearance streams alone, and add a test asserting they are byte-identical —
      the viewer maps an appearance into `/Rect`, so touching both would double the transform.
      The test compares the written form of `/Matrix` rather than reading it back: `GetMatrix`
      cannot parse what `SetMatrix` writes (it stores a literal and throws
      `NotImplementedException("Parsing matrix from literal")`). Pre-existing library gap, left
      alone — nothing in the resize path needs it — but noted in `docs/specs/page-resize.md`.
- [x] 3.4 An unrecognised subtype gets `/Rect` and nothing else. Test that such an annotation
      survives intact.

## 4. Destination sweep

- [x] 4.1 Write `PdfDestinationWalker` — enumerate every destination in the document: each page's
      `/Annots` (`/Dest` and `/A`), the `/Outlines` tree, the catalog `/Names` `/Dests` name tree,
      the legacy `/Dests` dictionary, and `/OpenAction`. Reuse what `PdfNamedDestinations` and
      `DetachImportedDestinations` already know about where these hide.
      Built as `PdfDestinationScaler` (it scales rather than merely walking). One thing the import
      path did not need: a destination array can be **indirect and shared by several links**, so
      moving it once per link found would move it several times over. Each array is moved once,
      tracked by object identity.
- [x] 4.2 Gate on `/S` being `/GoTo`. A `/GoToR` names a page in another file — `#461` learned this
      the hard way; see `docs/specs/import-size-and-annotations.md`.
- [x] 4.3 Transform coordinates per form: `/XYZ` (`l` and `t` only — **the zoom `z` is never
      touched**, see design decision 7), `/FitR` (all four), `/FitH`, `/FitBH` (`t`), `/FitV`,
      `/FitBV` (`l`), `/Fit`, `/FitB` (nothing).
      Under auto-rotate a `/FitH` names a line that is no longer horizontal, so it converts to
      `/FitV` (and `/FitBH` to `/FitBV`), which is exact rather than approximate. A null coordinate
      — allowed, and meaning "leave this one alone" — is left alone.
- [x] 4.4 Honour `ScaleDestinations = false`.
- [x] 4.5 Consider whether the walker is worth exposing beyond `internal`. Default to `internal`
      until something outside needs it. Kept internal.

## 5. Idempotency

- [x] 5.1 Detect a page whose `/Contents` is exactly one wrapper invocation over a form carrying the
      marker from 2.5.
      Tokenised and matched strictly: exactly eleven tokens, `q`, six numbers, `cm`, a name, `Do`,
      `Q`, and the name has to lead to a form carrying the marker.
- [x] 5.2 On a match, rewrite the `cm` against the recorded source rect rather than wrapping again.
      Recomputed from the form's `/BBox` every time rather than composed onto the previous
      transform, so repeated resizes cannot accumulate rounding error either.
- [x] 5.3 Fall back to wrapping on anything unexpected — extra operators, more than one content
      stream, a missing marker. Nesting is wasteful; a wrong in-place rewrite is not recoverable.
- [x] 5.4 Test A4 → A5 → A4 returns the original geometry with exactly one wrapper, and that three
      resizes leave one wrapper.
      This is what caught the real bug in 5.2: the second resize was working its transform out
      from the page's *current* media box rather than from the wrapper's `/BBox`, and the boxes and
      annotations were being moved by the whole new transform when they are in the coordinates of
      the page as it stands and only need the difference. Both fixed.

## 6. Refusals and the breaking change

- [x] 6.1 Refuse a signed document (`/Sig` field in `/AcroForm`), an encrypted one, or a tagged one
      (catalog holds `/StructTreeRoot`) — up front, before anything is mutated, with a message
      naming which of the three was found. `ResizePages` must make the check once before touching
      any page, not per page, or a refusal leaves the document half resized.
      A third create-on-read trap here: `SecuritySettings.SecurityHandler` resolves `/Encrypt` with
      `VCF.CreateIndirect`, so asking whether the document is encrypted would encrypt it. The
      trailer element is read raw. Signature fields nest, so `/Kids` is followed, with a depth cap.
- [x] 6.2 Refuse a document not open in `PdfDocumentOpenMode.Modify`.
      `Modify` is the first enum member, so a newly created `PdfDocument` already satisfies this;
      `IsReadOnly` covers both cases.
- [x] 6.3 Guard the `Size`, `Width` and `Height` setters: throw `InvalidOperationException` naming
      `Resize` when `/Contents` is present and non-empty. **Read `Elements[Keys.Contents]` raw** —
      the `Contents` property getter rewrites `/Contents` into array form as a side effect, which is
      the hazard `PdfResourcePruner` had to route around.
      `PdfPage.HasContent`. "Non-empty" means an empty array and a zero-length stream both count as
      no content, so a page that was drawn on and produced nothing still takes the old path.
- [x] 6.4 Fix `PdfPinata.Test/Drawing/Layout/XTextFormatterTest.cs:43` if the guard catches it.
      It sets `Size` before drawing so it should pass untouched — confirm rather than assume.
      Confirmed by reading it and by the suite: `Size` is set before `XGraphics.FromPdfPage`, so
      the page has no content yet. No change needed, and nothing else in the repo assigns these.
- [x] 6.5 Covered by 6.1: a tagged document is refused. Keep the refusal narrow — key off
      `/StructTreeRoot` on the catalog, not off a page's `/StructParents`, so a stray key on one
      page does not lock the whole feature out.

## 7. Public API

- [x] 7.1 `PdfPage.Resize(PageSize, PageOrientation, PageResizeOptions?)` and
      `PdfPage.Resize(XSize, PageResizeOptions?)`.
- [x] 7.2 `PdfDocument.ResizePages(PageSize, PageOrientation, PageResizeOptions?)`, making one
      destination sweep for the whole document rather than one per page.
      Plus an `XSize` overload to match `PdfPage`.
- [x] 7.3 XML docs saying plainly that a single-page `Resize` walks the whole document to fix
      destinations, and that `ResizePages` amortises that.
- [x] 7.4 Check the API compiles on all three legs — `netstandard2.1`, `net8.0`, `net10.0`. The
      netstandard2.1 leg exists for Unity and is the one that will object to a missing BCL type.
      Whole solution builds clean, netstandard2.1 included. Avoiding `init` (1.2) is what kept it
      that way.

## 8. Verification

- [x] 8.1 Content-walking tests in the `RotatedPageTests` idiom — walk the content applying every
      transform and assert where a mark actually lands. Cover: A4 → A5, A5 → A4, each fit mode,
      each alignment, a margin, a cropped page, a page with `/Rotate 90`, a page with a non-zero
      media-box origin.
      `PageResizeTests.cs`, over `Helpers/ResizedContentProbe.cs`. The walker needed one thing
      rotation did not: a resized page's drawing is no longer in the page's own content stream, so
      the walk has to follow the `Do` into the wrapper and carry the transform with it — otherwise
      it finds nothing and every assertion passes vacuously. Alignments are covered exhaustively in
      1.4 rather than re-derived here.
- [x] 8.2 Graphics-state tests: unbalanced `q`, unbalanced `Q`, a `/Group`, flate-encoded content
      not recompressed, two pages sharing one resource dictionary where only one is resized, and a
      page whose `Resources` was read before the resize.
      Plus a page whose `Contents` was read before it, which is the same trap.
- [x] 8.3 Annotation tests over fixtures in the style of
      `PdfPinata.Test/IO/ImportedPageFixtures.cs`: a link, an ink annotation, a highlight, an
      annotation with an `/AP`, and an unknown subtype.
      `PageResizeAnnotationTests.cs`, 11 tests. Every resize there halves the page exactly, so the
      expected numbers can be read off rather than computed.
- [x] 8.4 Destination tests: a link from another page, `/XYZ` with the zoom unchanged on both a
      shrink and an enlargement, a zero zoom, `/Fit`, `/FitR`, an outline entry, a named destination
      through the name tree, a `/GoToR`, a link to a page that was not resized, and the sweep
      disabled.
      `PageResizeDestinationTests.cs`, 19 tests. Added: a null coordinate, the legacy `/Dests`
      dictionary, `/OpenAction`, `/FitH` becoming `/FitV` on a turned page, a destination array
      shared by two links moved once, and `ResizePages` moving each destination exactly once.
- [x] 8.5 Golden-image tests under `[GoldenImageFact]` and
      `[Collection(RasterizingCollection.Name)]` — one in-process Ghostscript means a second
      concurrent rasterization silently falls back to an executable that may not be installed.
      Render `FamilyTree.pdf`, `test.pdf` and `Pdf20.pdf` resized, comparing against references.
      `PageResizeRenderingTests.cs`. No new reference images: the two cases where the drawing must
      be *unchanged* are the ones worth rasterizing — resized to the size it already is (which
      still moves the content into a form), and down to half and back again. Both render identical
      on all three documents. This is what says a font, a shading or a clipping path survived the
      move, which no content walk can.
      Adding these tests made the test host crash part way through a run — no failing test, just a
      passing count quietly short of the total. Chased down after the change was archived and found
      to be a **leak in the test harness, not a budget**: `PdfHelper.Rasterize` returned a
      `MagickImageCollection` no caller disposed and `PdfHelper.Diff` leaked all three images it
      opened, all of them unmanaged bitmaps the collector cannot see the size of. Fixed in
      `PdfHelper` and its four leaking call sites; the same fix also cured the whole-solution crash
      that had been written off as pre-existing. See `docs/specs/page-resize.md`.
      With that fixed these tests carry the fuller coverage they had briefly been cut back to fit
      under the apparent ceiling: both the wrapped and the there-and-back case, on all three
      documents.
- [x] 8.6 Idempotency tests from 5.4.
- [x] 8.7 Refusal tests: signed, encrypted, tagged, read-only, import-mode — and that a refused
      `ResizePages` leaves every page untouched rather than half the document resized.
      Plus a page with a live `XGraphics` on it, which would otherwise go on drawing to the size
      the page had when it was opened.
- [x] 8.8 Whole suite green on `net8.0` and `net10.0`.
      **1027 passed, 1 skipped, 0 failed on each**, repeated to confirm it is not flaky. Baseline
      before this change was 928 passed / 1 skipped, so it adds 99 tests.
      `dotnet test` on the whole **solution** is green too, now that the harness leak found under
      8.5 is fixed. It had been crashing before this change as well as after, which is why it was
      first written off as pre-existing and unrelated; it was the same leak, reached sooner because
      that run has two test hosts going at once.

## 9. Documentation

- [x] 9.1 Write `docs/specs/page-resize.md` in the house style — what was built, and what was
      deliberately left out: tagged PDF and why it is refused rather than documented, `/DA` font
      sizes in form fields, text reflow, and `/UserUnit` with the reasons it was not chosen.
      Carries a "Turned up on the way" section, as the #461 spec does, for the two library faults
      this ran into: indirect `PdfArray` writes no brackets, and `GetMatrix` cannot read back what
      `SetMatrix` writes.
- [x] 9.2 Note the breaking change in `CHANGELOG.md`. The migration is `page.Size = X` →
      `page.Resize(X)`. Say explicitly that the replaced behaviour cropped from the **bottom-left**
      and so lost the head of the page, that `PageResizeOptions.Crop` anchors top-left instead, and
      that anyone who genuinely wants the old anchoring must ask for bottom-left alignment. Do not
      present `Crop` as "the old behaviour".
- [x] 9.3 Add a short example to `examples/` or `SampleApp` showing A4 → A5 on a document with links.
      `SampleApp/Program.cs`. `examples/Examples` turned out to be stale `bin`/`obj` with no source
      and is not in the solution, so `SampleApp` is the only live one. Runs; output not committed.
