# Spec — remaining page-import defects after issue #461

What the investigation of issue #461 turned up beyond the defect the issue was raised about.

| item | what | status |
|---|---|---|
| 1 | Unused resources are copied with an imported page | done, `feat/prune-unused-resources` |
| 2 | `InsertRange` throws on a page carrying a resolvable link | done, `fix/insert-range-duplicate-annots` |
| 3 | `InsertRange` keeps only one shape of destination | done, with item 2 |
| 4 | A destination named rather than stated arrives standing for nothing | done, `fix/imported-named-destinations` |

The link destinations of #461 itself are fixed on `fix/imported-annotations-copy-linked-pages`,
which `fix/insert-range-duplicate-annots` builds on.

---

## Item 1 — Unused resources are copied with an imported page

**Done** on `feat/prune-unused-resources`, as `src/PdfPinata/Pdf.Advanced/PdfResourcePruner.cs`.
What follows is the design as built; the two notes marked *changed* are where it departs from what
was drafted.

### The defect

`PdfPages.ImportExternalPage` copies a page's `/Resources` wholesale. When pages share one resource
dictionary — either literally the same object, or one inherited from the `/Pages` node and pushed
down onto every page by `PdfPages.FlattenPageTree` — that dictionary names *every* font, image and
form in the document. Splitting therefore gives each page a copy of all of them.

Reproduced with a hand-crafted 3-page document, 3 × 20 KB images, one drawn per page, pages
inheriting one `/Resources` from the `/Pages` node: **every split file was 63,256 bytes**, against
~22,600 for the same document with per-page resources. Unchanged by the #461 fix.

This is very likely the residual cause in the reporter's file (`…\Slip\tesst12321.pdf` reads like a
generated payslip batch, and report generators habitually share a font resource dictionary).

Note this is *not* a violation of PDFsharp's stated import contract — "adding an external page
always makes a deep copy of their transitive closure" is exactly what it does. It is a missing
optimisation, not a broken invariant. That framing drives the API shape below.

### Proposed shape

A public, opt-in method on `PdfDocument`, following the `ConsolidateImages` precedent
(`PdfDocument.cs:815`) — an explicit post-processing pass the caller asks for:

```csharp
/// <summary>
/// Drops the entries of each page's resource dictionary that the page does not draw with.
/// </summary>
public void PruneUnusedResources()
```

Opt-in rather than automatic on import, because it has to decode and parse every content stream,
and because it can only ever be a best guess about what a content stream reaches. Callers splitting
a document call it before `Save`.

### Algorithm

Per page:

1. Resolve the page's effective `/Resources`.
2. Read the content and parse it with `ContentReader.ReadContent(byte[])`.
   *Changed*: the drafted `ContentReader.ReadContent(page)` goes through `page.Contents`, which
   rewrites the page's `/Contents` into an array as a side effect of being read. An analysis pass
   should not alter the document it is looking at, so the pruner concatenates the streams itself.
3. Walk the `CSequence` collecting (category, name) pairs from the operators that name a resource:

   | operator | category | operand |
   |---|---|---|
   | `Do` | `/XObject` | 0 |
   | `Tf` | `/Font` | 0 |
   | `gs` | `/ExtGState` | 0 |
   | `sh` | `/Shading` | 0 |
   | `cs`, `CS` | `/ColorSpace` | 0, unless a device space (`/DeviceGray`, `/DeviceRGB`, `/DeviceCMYK`, `/Pattern`) |
   | `scn`, `SCN` | `/Pattern` | last, only when it is a `CName` |
   | `BDC`, `DP` | `/Properties` | 1, only when it is a `CName` |

   `ri` names a standard rendering intent, not a resource. Text-showing operators name nothing
   beyond the font `Tf` already selected.

4. Recurse into every used `/XObject` of `/Subtype /Form`, and into tiling patterns and Type 3
   font `/CharProcs`:
   - a form **with** its own `/Resources` is a separate scope, scanned against that dictionary;
   - a form **without** `/Resources` inherits the page's, so its names join the page's used set.
     This is the rule most likely to be got wrong, and getting it wrong drops a resource that is
     genuinely drawn.
   - Cycle guard on `PdfObjectID`, plus a depth cap.

5. Build a **fresh** `/Resources` dictionary for the page holding only the used names per category.

### Traps

- **Never mutate a resource dictionary in place.** It is shared — that is the whole defect. Pruning
  page 1 in place would strip what page 2 draws with. Always write a new dictionary onto the page.
- **Nothing needs deleting.** Once a page stops naming an image, it is unreachable from the trailer
  and `PdfDocument.PrepareForSave` → `_irefTable.Compact()` (`PdfDocument.cs:423`) drops it.
- Preserve entries that are not name-keyed resource categories (`/ProcSet`, and anything the
  scanner does not model) untouched.
- **A page reads its resources but once.** `PdfPage.Resources` caches into `_resources`, so setting
  `Elements["/Resources"]` alone leaves a page that was asked for its resources beforehand still
  answering with the ones it started with. `PdfPage.ReplaceResources` sets both together.
- Give the page its own indirect `PdfResources` rather than a direct dictionary, which is how the
  dictionary it stands in for was held, and what the rest of the library expects to find.

### Failure mode: when in doubt, keep everything

A page that comes out too large is a nuisance. A page that comes out missing a font or an image is
corrupt. So any of the following leaves that page's `/Resources` **exactly as it was**:

- the content stream does not parse (`ContentReaderException`);
- **an inline image (`BI`) appears anywhere in the content.** `CLexer.ScanInlineImage`
  (`Pdf.Content/CLexer.cs:149`) is explicitly `NYI: Just scans over it`, and for non-ASCII85 data it
  finds the end by scanning for the literal bytes `E`,`I`, which can match inside binary image data.
  A false match desynchronises the parse, after which a real `/Name Do` can be missed entirely;
- a stream the content draws cannot be read, or the depth cap trips.

*Changed*: a **cycle does not bail**. A form drawing itself is read once and pruning goes ahead —
the visited set makes the reading terminate with the used names complete, so there is nothing to be
uncertain about. Likewise, content naming a resource the dictionary does not hold is ignored rather
than bailed on: a name that is not there cannot be dropped.

Categories the scanner does not model, and entries that are not written as a dictionary of names,
are carried over untouched rather than bailing the page — so uncertainty about one category still
leaves `/XObject` and `/Font` prunable, which is where nearly all the bytes are.

### Verification

`src/PdfPinata.Test/IO/PruneUnusedResourcesTests.cs`, over documents built by
`SharedResourceFixtures` on the raw-PDF assembler in `RawPdf.cs`:

- three pages sharing one dictionary that names all three images → each page keeps its own, which
  fails outright if pruning one page reaches into the dictionary the other two are holding;
- the same document split → each file under the weight of two images, against over three without
  pruning, which is the state of affairs the issue reports;
- a form without `/Resources` → the page font and image it draws are kept, the unused ones dropped;
- a form with `/Resources` of its own → the page's entry of the same name is not kept alive by it;
- a form drawing itself → read once, pruned all the same;
- an inline image, and content behind a filter that cannot be undone → the page left untouched;
- a page asked for its resources before pruning → answers with the pruned ones afterwards;
- pruning twice → the same as pruning once.

`src/PdfPinata.Test/IO/PruneUnusedResourcesRenderingTests.cs` renders `FamilyTree.pdf`, `test.pdf`
and `Pdf20.pdf` before and after pruning through the golden-image harness and compares page by page.
Not vacuous: `test.pdf` and `Pdf20.pdf` go from 14,187 to 13,048 bytes with the rendering identical.

Whole suite green on net8.0 and net10.0, 139 passed over four runs.

### Cost

525 lines, the largest of the three. The scanner and following what is drawn are the bulk; the
bail-outs are what keep it safe.

---

## Item 2 — `PdfPages.InsertRange` throws on a page that carries a resolvable link

**Done** on `fix/insert-range-duplicate-annots`.

### The defect

Confirmed by direct probe:

```
System.ArgumentException : An item with the same key has already been added. Key: /Annots
```

`InsertRange` calls `ImportExternalPage(importPage, annotationCopying)` in its first loop
(`PdfPages.cs:234`), which with the default `AnnotationCopyingType.ShallowCopy` already puts
`/Annots` on the new page. Its second loop then builds its own annotation array and does
`page.Elements.Add(PdfPage.Keys.Annots, annotations)` (`PdfPages.cs:350`).
`DictionaryElements.Add` is backed by `Dictionary<string, PdfItem>.Add`
(`PdfDictionary.cs:1328`), which throws on a duplicate key.

It only escapes notice because the `Add` sits behind `if (annotations.Count > 0)`, and the second
loop only ever adds an annotation that is a `/Link` **and** whose `/Dest` is a 5-element array
**and** whose target page is in the inserted range. Miss any of those and the array stays empty and
the crash does not happen. Hit all three — an ordinary `[page /XYZ l t z]` link between two pages
being inserted together — and it throws.

### The fix

The second loop is gone and the import path handles annotations, with the calls the first loop was
missing added to it:

```csharp
PdfAnnotations.FixImportedAnnotation(page);
DetachImportedDestinations(page, importPage, importedObjectTable);
```

The deferred-destination mechanism added for #461 does what the second loop was reaching for, and
does it properly: destinations are resolved against the pages that actually made it into the
document, at save time, whichever direction the link points. The bespoke loop then has no job left.

This also stops `InsertRange` silently discarding every annotation that is not a `/Link` — its
key handling is a whitelist (`/BS`, `/F`, `/Rect`, `/StructParent`, `/Subtype`, `/Dest`) that drops
everything else on the floor.

Net effect on the library: 125 lines gone, 6 added. `RemapReference` had no other caller and went
with the loop.

### Verification

`src/PdfPinata.Test/IO/InsertRangeTests.cs`, all of which failed before the change:

- a 5-element `/Dest` link no longer throws;
- a link to a page of the range points at the inserted page rather than at a second copy of it,
  over `/XYZ`, `/Fit`, `/FitH`, `/FitR` and a `/A` go-to action;
- a link to a page left out of the range loses its destination, and that page is not copied in;
- an annotation that is not a link survives, as does every annotation of a page with more than one.

The fixtures moved to `src/PdfPinata.Test/IO/ImportedPageFixtures.cs` so the split tests and these
share them. Whole suite green on net8.0 and net10.0, 126 passed.

---

## Item 3 — `InsertRange` drops every destination that is not `[page /XYZ l t z]`

**Done**, with item 2.

The gate was `destArray.Elements.Count == 5`, so `[page /Fit]`, `[page /FitH t]`, `[page /FitB]`,
`[page /FitR l b r t]` and friends were all discarded, along with the whole annotation.
`/A << /S /GoTo /D … >>` actions were not looked at at all, and neither were named destinations.

Fixed for free by item 2: the shared path keys off "is the first element of the destination array a
reference to a page", which is true of every explicit destination form, and it handles `/A` go-to
actions as well.

---

## Item 4 — A destination named rather than stated arrives standing for nothing

**Done** on `fix/imported-named-destinations`, which this document had put out of scope. What follows
is why that was wrong.

### The defect

A link can name where it goes instead of saying it outright, leaving the document catalog to hold
what the name stands for — a name tree under `/Names`, or the `/Dests` dictionary that PDF 1.1 used.
`ImportExternalPage` copies no catalog, so the name arrived standing for nothing. The annotation
survived intact, `/Dest (section.1)` and all, naming something that was nowhere in the file.

Worse than the explicit case rather than merely different: `ResolveImportedDestinations` **removes**
an explicit destination it cannot resolve, leaving a well-formed inert link. A named one was left
dangling, which is the one path out of the import that wrote a broken reference.

And it did not need the target page to be missing. Confirmed by direct probe on a 15-page LaTeX
paper, every page imported:

```
name tree YES, named links 95, explicit links 0
  merged: resolved 0, still named 95
```

**Every internal link of that document was named, and none explicit.** Merging a document whose
cross references worked gave one whose cross references did not. hyperref, Word and InDesign all
write destinations this way, so the form already handled is the rarer one in generated documents.

### The fix

Resolve the name against the document the page came from, while that is still at hand, and write the
destination it stands for in its place. `DetachDestination` already bailed at exactly the right spot:

```csharp
// Named destinations are strings or names and hold on to nothing.
PdfReference externalPage = externalDestination.Elements[0] as PdfReference;
if (externalPage == null)
    return;
```

Everything past that point was already built for #461 and needed no change — the deferred
resolution, the retargeting at the imported page, the dropping of a destination whose page was left
behind. Only the lookup is new, as `Pdf.Advanced/PdfNamedDestinations.cs`: the name tree walk
(`/Kids`, `/Limits`, leaf `/Names`), the `/Dests` dictionary, and the `/D` dictionary a destination
can be held in.

**Inlined rather than kept as a name.** Rebuilding a name tree in the output would mean deciding
what happens when two merged documents both define `section.1`, and that collision is the only part
of this that is genuinely hard. Inlining sidesteps it: nothing that survives the import refers to a
destination by name, because `/Outlines` is not imported either.

### Traps

- **`/GoToR` is not `/GoTo`.** `DetachImportedDestinations` looked at the `/D` of any action at all.
  Explicit destinations got away with it — a remote one names its page by number, so the
  `as PdfReference` above already bailed — but resolving a *name* locally would point a link meant
  for another file at whatever this document happens to call that. Now gated on `/S`, with an action
  that does not say what it is still taken to be a go-to, which is what it was taken to be before.
- **Fit specifications are copied, not shared.** `[page /XYZ l t z]` past the page is numbers and
  names, which clone. Anything else there would be an object of the other document, and rather than
  write a destination that reaches into it the conversion is abandoned and the name left alone.
- A name the catalog does not hold is left as written. It cannot be resolved and cannot be shown to
  be wrong, and leaving it is what happened before.

### Verification

`src/PdfPinata.Test/IO/NamedDestinationTests.cs`, over `NamedDestinationFixtures.cs`: the name tree,
a nested tree with `/Limits` where the destination is in the second leaf, a destination held under
`/D`, the `/Dests` dictionary, a `/GoTo` action, every annotation of a page carrying three, and that
where on the page to go survives. Plus the three that say what does *not* change: a `/GoToR` keeps
its name, an unheld name is left alone, an explicit destination behaves as it did.

Nine of the twelve fail with the wiring reverted; the three that pass are exactly those three.

On the LaTeX paper above: **95 of 95 resolved, none left named**, for 1,191 bytes more in a 3 MB
file. Whole suite green on net8.0 and net10.0, 260 passed.

---

---

## Turned up on the way

`ImageMagick` drives Ghostscript in process, and one process holds one Ghostscript. Adding a second
test class that rasterizes made the two run at once, and the one that lost fell back to running
Ghostscript as a command, which is not there to run on a machine without an installation of its own.
Every test that rasterizes now sits in one collection that does not run alongside the others —
`src/PdfPinata.Test/Helpers/RasterizingCollection.cs`, committed separately.

---

## Not in scope

- ~~Named destinations resolved through the catalog `/Names` tree or `/Dests`. Real, but a different
  feature.~~ Done as item 4. It was not a different feature: reading it as one rested on assuming
  the name tree had to be *imported*, where resolving the name at import time and writing what it
  stands for needs no tree in the output at all.
- Importing `/Outlines`, `/StructTreeRoot` or `/AcroForm` alongside a page. Note this bounds item 4:
  a merged document keeps the cross references written into its pages, and still has no bookmarks.
