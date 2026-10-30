# Inventory — the nice-to-have tier, and where each of it would be tested

The second tier of the competitive gap analysis. Ten items, none of them built, re-verified against
the tree on 30 August 2026 and every one still missing exactly as first recorded.

This is an inventory rather than a proposal: it does not argue for building any of these. It records
what is missing, what each would cost, **what has since made two of them cheap**, and — the part that
does not exist anywhere else — the seam each would be tested at. That last is what makes an item
pickable up: the research artefact says what is missing and what it is worth, and says nothing about
where the work would land.

## Problem Statement

Ten deferred features live in a table in a research artefact, ranked by a competitive argument. That
is the right place for the argument and the wrong place for the work.

A maintainer with a spare fortnight cannot tell from it which of the ten are now cheaper than they
were, which are blocked and on what, or where a test for any of them would go. Two of the ten have had
their preconditions met by work that shipped since — one by tagged output, one by text extraction —
and nothing anywhere says so; the fact is buried in a status paragraph. The effort figures are the
original estimates and none accounts for machinery the library has grown since.

And each re-check costs the same search of the tree, run again, to confirm what was true a fortnight
ago is still true.

## Solution

One note holding all ten, each with the same four things: what is genuinely absent, the seam it would
be built and tested at, what it depends on, and what it is worth. Preferring an existing seam over a
new one for every item, because for six of the ten there is already an obvious one.

When any item is picked up it gets a spec of its own and its row here becomes a pointer to it.

## The ten

**Form filling, appearance generation and flattening** — *3–5 wk.* **Flattening is done**
(`PdfAcroForm.Flatten()`, [form-flattening.md](form-flattening.md)); the rest below was written
before it. The field model reads and models
eight field types and nothing sets a value, generates an appearance or flattens a widget away. The
sample application's forms demonstration draws its own appearance streams by hand, which is the
clearest possible evidence of the gap. **Seam:** the typed field model that already exists — a value
setter per field type, appearance generation behind it, and a flatten call on the form. **Verified
by** reopening the written document and reading the appearance stream, and by extracting the text of a
flattened form to prove the value is now page content. **Depends on** nothing. The existing inventory
of interactive-layer gaps argues the first half of this in detail.

**Redaction** — *4–6 wk.* Nothing. **Its precondition is now met**: it needed extraction in-process
and extraction shipped. **Seam:** a call taking a page and a region. **Verified by extraction itself**,
which is the right oracle and an unusually strong one — the test asserts the text is *gone* rather
than covered, which is exactly the property a black rectangle drawn over the top would fail. Wants the
tagged-extraction work first, so that redacting a hyphenated word removes both halves and so that
furniture is not missed. This is the item a competitor charges for.

**Pattern-based hyphenation** — *2–3 wk.* Only the soft hyphen is honoured; there is no pattern
dictionary and no algorithm. **Seam:** the line breaker, in both layout engines. **Verified by** where
lines break in rendered output, and — importantly — by extraction, which must return the whole word:
the break has to write substitute text or hyphenation trades a layout improvement for an extraction
defect. **Depends on** a licence decision per pattern file, which is the open question that has kept it
deferred and is not a technical one.

**SVG import as vector** — *3–5 wk.* No trace anywhere. **Seam:** the existing graphics path type. The
path grammar alone is about a week and covers icon sets, which is the cheapest useful slice of it.
**Verified by** the path geometry helper the drawing tests already use, which reads a path back as
segments, so an assertion is about the shape rather than about the parser. Text and filters are the
part to refuse.

**Document assembly helpers** — *1–2 wk.* Merging works only by hand, page by page, from an
import-mode document. No split, no N-up, no overlay, no booklet. **Seam:** document-level composition
over the page import, resizer and pruner that already exist. **Verified by** page counts, and by the
things that actually break on a merge: outlines, named destinations and colliding form field names.
That last is the real work and the rest is API surface.

**Image optimisation** — *1–2 wk.* Images are de-duplicated across a document and nothing else — no
downsampling, no quality control, no encoder choice, no pass-through of an already-compressed JPEG.
**Seam:** the image placement options. **Verified by** the size and the filter of the written image
stream, which is exact and needs no rasterizer.

**Optional content groups** — *1–2 wk.* A catalog key name and nothing behind it. **Its precondition
is now met**: it needed the marked-content machinery and tagged output built it. **Seam:** the catalog
for the group definitions, the drawing surface for the marked content, and the existing places that
already attach things to XObjects and annotations. **Verified by** reopening the document and reading
the catalog's configuration and the content stream's marked content. The cheapest item in the tier and
the one whose cost has actually fallen.

**CFF subsetting** — *4–6 wk.* PostScript-outlined faces embed whole, and the library says so out loud
to the caller. Megabytes per document for CJK. **Seam:** font embedding, where TrueType subsetting
already lives. **Verified by** the size of the embedded font stream and by the subset naming rule
already pinned — a face that was cut down is tagged and one that was not is not. Fiddly, self-contained
and with no .NET prior art to lean on.

**Linearization** — *3 wk.* Nothing but a note in the trailer saying where it would be needed. **Seam:**
the writer, which already has the two-pass shape cross-reference streams needed. **Verified by** object
order and the parameter dictionary in the written bytes. Low value for a server-side generation
audience; in the tier for completeness.

**Hot-reload previewer and layout debugger** — *4–6 wk.* Nothing, and it is not a PDF feature at all.
Repeatedly cited as the reason teams choose the competitor it belongs to. **Seam:** none in this
library — a separate tool watching an assembly and serving a preview. **Blocked on having a rasterizer**:
this repository rasterizes only through Ghostscript in tests. Outside the stated scope of core library
work, and recorded because losing for a non-feature reason is still losing.

## User Stories

1. As a maintainer with a spare fortnight, I want to see which deferred items are cheapest now, so
   that I can pick one without re-deriving the whole tier.
2. As a maintainer, I want to know which items had a precondition that has since been met, so that a
   cost that has fallen does not go unnoticed.
3. As a maintainer, I want each item to name the seam it would be tested at, so that picking it up
   starts with a test rather than with an investigation.
4. As a maintainer, I want each item to prefer an existing seam, so that the tier cannot add ten new
   ways to ask the library something.
5. As a maintainer, I want to know which items are blocked on a decision rather than on code, so that
   I do not start one that will stall.
6. As a maintainer, I want the re-verification date recorded, so that the next person knows how stale
   this is without running the searches again.
7. As a developer filling a form, I want to set a field's value and get an appearance, so that the
   document shows what I filled in without asking the viewer to build it.
8. As a developer publishing a completed form, I want to flatten it, so that the values cannot be
   changed and every viewer shows the same thing.
9. As a developer redacting a document, I want the text actually removed, so that copying from the
   file cannot recover what was covered.
10. As a developer typesetting narrow columns, I want hyphenation, so that justified text does not
    have rivers through it.
11. As a developer, I want a hyphenated word to still extract whole, so that hyphenation does not
    break search.
12. As a developer placing icons, I want to import an SVG path as vector art, so that logos scale.
13. As a developer merging documents, I want outlines and links to survive, so that the merged file is
    navigable.
14. As a developer merging forms, I want colliding field names handled, so that filling one field does
    not fill another.
15. As a developer embedding photographs, I want to choose a target resolution and quality, so that a
    report is emailable.
16. As a developer producing drawings, I want optional content groups, so that a reader can turn
    layers on and off.
17. As a developer producing CJK documents, I want subsetted PostScript fonts, so that files are not
    megabytes larger than they need to be.
18. As a developer serving PDFs over the web, I want linearized output, so that the first page shows
    before the file has finished downloading.
19. As a developer building a complex layout, I want to see it as I write it, so that I am not
    compiling and reopening a viewer to check a margin.
20. As a maintainer, I want an item that is picked up to get its own spec, so that this note stays an
    index and does not turn into ten specs in a trench coat.

## Implementation Decisions

**This note makes no implementation decisions**, deliberately. Each item's decisions belong in its own
spec, written when it is picked up. What is decided here is the shape of the record:

**Every item names a seam, and prefers an existing one.** Six of the ten have an obvious existing seam
and are recorded against it. Of the rest, redaction and assembly want one new entry point each, image
optimisation extends an existing options object, and the previewer is not a seam in this library at
all.

**Two items' preconditions are recorded as met.** Optional content groups needed marked content and
redaction needed extraction; both shipped. This is the fact most likely to be lost, because it lives
in the difference between two documents written months apart.

**An item that is picked up gets a spec and this row becomes a pointer**, so that the tier stays a
single page and the detail lives where detail belongs.

**The effort figures are the original estimates and are not re-derived here.** Two of them are known to
be high for the reason above; the rest are as good as they were.

## Testing Decisions

**What makes a good test for anything in this tier.** Write a document, read it back, and assert on
what a consumer of that document would see: the text extracted, the size of a stream, the entries in
the catalog, the shape of a path, the number of pages. None of these features needs a rasterizer to
be proved correct, and only one of them — the previewer — needs anything outside the existing test
projects.

**Where each would live.** The broad test project for everything touching the document model, fonts,
images, forms, the writer and extraction; the rendering test project for hyphenation, because it is a
layout change in both engines and that project covers MigraDoc's layout without rasterizing.

**Prior art worth copying.** The extraction tests, for anything whose correctness is "what does this
document say afterwards" — redaction and hyphenation both. The path geometry helper, for SVG. The font
embedding tests and the subset naming rule, for CFF subsetting. The conformance corpus, for anything
that must not break an existing claim: optional content and flattened forms both change what a
document contains and both are cheap to add a corpus document for.

**The oracle worth naming.** Redaction is the one item in this tier whose test is genuinely strong,
because the library's own extractor is an independent reader of the result. A redaction that merely
covers text passes a visual check and fails extraction, which is exactly the defect that makes
redaction worth paying for elsewhere.

## Out of Scope

- **Building any of it.** This note records; it does not propose.
- **Re-ranking the tier.** The competitive argument for the order is in the gap analysis and is not
  repeated or revised here.
- **The moonshot.** Converting HTML and CSS to PDF was assessed and refused, for reasons that have not
  changed: the libraries that win at it embed a browser.
- **Anything in the must-have tier.** Seven of those are built and the eighth is a decision; they have
  their own specs.

## Further Notes

Two of the ten were not named in the paragraph that prompted this note — hyphenation and the
previewer — and both are still missing, so the tier is empty at ten out of ten rather than eight out
of eight.

The pattern worth noticing across the tier is that **the cheap items got cheaper by accident**. Nobody
built the marked-content machinery to enable layers, or extraction to enable redaction; both fell out
of work done for regulatory reasons. That is an argument for keeping this inventory current rather
than for planning around it: the tier's costs move when other work lands, and they move downwards.
