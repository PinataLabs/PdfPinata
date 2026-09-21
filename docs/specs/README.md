# Spec index

Every design note in this directory, and whether the work it describes has landed.

Each spec carries its own status table, and **that table is the authority** — this file summarises
it. A spec here is one of three things: a *proposal* written before the work, a *retrospective*
written after it against what actually shipped, or a *findings* note recording what testing or an
investigation turned up. All three are tracked the same way, because what a reader wants to know
first is the same in each case: is this the code as it stands, or the code as somebody hoped it
would be?

**44 done · 4 in progress · 10 not started · 58 total.**

Read the relevant spec before extending that feature area.

---

## Done

The spec describes the code as it is. Items marked *deliberately not done* inside these are scope
decisions, not outstanding work.

- [x] [attachment-reachability-walk.md](attachment-reachability-walk.md) — one walk for every file a document carries (T19)
- [x] [axis-renderer-duplication.md](axis-renderer-duplication.md) — merging the three charting axis renderer pairs (T8)
- [x] [bookmarks-and-outlines.md](bookmarks-and-outlines.md) — a bookmark that reaches the page, ststeiger#321
- [x] [charting-renderer-findings.md](charting-renderer-findings.md) — eight defects testing the charting renderers found, all fixed
- [x] [compile-time-dom-value-model.md](compile-time-dom-value-model.md) — the DOM's reflection value model replaced by a source generator
- [x] [conformance-completeness.md](conformance-completeness.md) — the four resource rules enforced, the `A` levels claimable and validated; PDF/UA-2 claimable but one clause short of veraPDF (G4 tail)
- [x] [cross-reference-streams.md](cross-reference-streams.md) — writing cross-reference streams and object streams (G1)
- [x] [demonstration-app-coverage.md](demonstration-app-coverage.md) — closing `SampleApp`'s coverage gaps, 35 items
- [x] [digital-signatures.md](digital-signatures.md) — signing a document (G5): `IPdfSigner`, PAdES B-B, certifying signatures and verification; B-T, B-LT and `/DocMDP` enforcement followed in signature-lifetime.md. Trust stores and revocation checking are deliberately out.
- [x] [demonstration-app.md](demonstration-app.md) — what `SampleApp` is, and what it deliberately is not
- [x] [dom-thread-safety.md](dom-thread-safety.md) — the unsynchronised `static Meta` that failed two tests at random
- [x] [dom-value-model-findings.md](dom-value-model-findings.md) — what replacing the DOM value model turned up
- [x] [external-file-streams.md](external-file-streams.md) — a stream whose data is in another file, empira#389; `PdfStream.ExternalFile` says where, and nothing fetches it
- [x] [font-embedding-gaps.md](font-embedding-gaps.md) — `.otf`/`.ttc` discovery, CFF embedding, style simulation, G1–G6
- [x] [graphics-state-stack-unification.md](graphics-state-stack-unification.md) — the dead state stack removed, the live pair kept apart (T15)
- [x] [image-failure-reporting.md](image-failure-reporting.md) — an image decode failure that says which failure, empira#366
- [x] [image-pixel-seam.md](image-pixel-seam.md) — the image seam widened from an invented BMP to pixels (T14)
- [x] [import-size-and-annotations.md](import-size-and-annotations.md) — page-import defects found beyond issue #461
- [x] [incremental-update-save.md](incremental-update-save.md) — appending to a PDF instead of rewriting it (G6)
- [x] [migradoc-field-evaluation.md](migradoc-field-evaluation.md) — a field's value evaluable without a renderer (T18)
- [x] [migradoc-footnotes.md](migradoc-footnotes.md) — footnotes rendered, having been dropped in silence
- [x] [nested-lists.md](nested-lists.md) — a list inside a list, said through `ListInfo.NestingLevel` and nested in the tree (G2 tail)
- [x] [nice-to-have-inventory.md](nice-to-have-inventory.md) — the ten deferred features, each with the seam it would be tested at; re-verified 30 August 2026 and none picked up. An inventory, not a proposal.
- [x] [outline-destinations.md](outline-destinations.md) — outlines on a LaTeX document, empira#8
- [x] [page-resize.md](page-resize.md) — resizing a page that already has content on it
- [x] [parser-document-decoupling.md](parser-document-decoupling.md) — narrowing what the parser needs of a document (T13)
- [x] [pdf-a-conformance.md](pdf-a-conformance.md) — PDF/A, XMP metadata and hybrid e-invoicing (G4)
- [x] [pdfdocument-thin-forwarders.md](pdfdocument-thin-forwarders.md) — the page-tree guard moved to the page tree
- [x] [pdfpage-responsibility-split.md](pdfpage-responsibility-split.md) — the print sheet given a type of its own (T11)
- [x] [script-itemizer-contract.md](script-itemizer-contract.md) — `ScriptItemizer` put back where it is called from (T16)
- [x] [signature-lifetime.md](signature-lifetime.md) — PAdES B-T and B-LT, and a `/DocMDP` level that is enforced (G5 tail)
- [x] [simple-type-immutability.md](simple-type-immutability.md) — the simple-type rule enforced, not just stated (T12)
- [x] [soft-hyphen-in-justified-list.md](soft-hyphen-in-justified-list.md) — a hyphenated word in a justified list item, empira#339
- [x] [tabbed-bidirectional-lines.md](tabbed-bidirectional-lines.md) — a right-to-left line with a tab in it, reordered segment by segment (G3 tail)
- [x] [tagged-pdf-accessibility.md](tagged-pdf-accessibility.md) — tagged PDF and PDF/UA output, all three stages (G2)
- [x] [tagged-text-extraction.md](tagged-text-extraction.md) — extraction that reads the marked content the tagger writes; the writer now states `/ActualText` inline too (G7 tail)
- [x] [test-host-crash-investigation.md](test-host-crash-investigation.md) — why a full test run died at the end; found and fixed
- [x] [text-extraction.md](text-extraction.md) — reading the text back out of a page (G7)
- [x] [text-markup-annotations.md](text-markup-annotations.md) — highlight, underline and strikeout annotations, ststeiger#342
- [x] [text-normalization-seam.md](text-normalization-seam.md) — draw the characters `MeasureString` measured (T17)
- [x] [text-shaping-and-bidi.md](text-shaping-and-bidi.md) — complex-script shaping, bidirectional text and font fallback (G3)
- [x] [verapdf-validation.md](verapdf-validation.md) — the outside opinion on every conformance claim, and it gates
- [x] [wrong-stream-length.md](wrong-stream-length.md) — streams whose `/Length` is wrong, empira#29
- [x] [xmp-extension-schemas.md](xmp-extension-schemas.md) — declaring an XMP property PDF/A has never heard of

## In progress

Part of the spec has landed. Each entry says what is left.

- [ ] [crap-coverage-backlog.md](crap-coverage-backlog.md) — the CRAP backlog, worked in batches. Batches 0–18 are done: 237 methods over the threshold down to 172, 109 never-executed down to 71. Batches 15–18 leave a handful of named items for later, and the list wants re-measuring.
- [ ] [generated-serialization.md](generated-serialization.md) — generating the flat attribute writes inside the DOM's `Serialize`. **Step 1 of 6 shipped** — the `MDG007` diagnostic exists and fires. Steps 2–6 have not started: no `SerializeValues` is emitted anywhere.
- [ ] [interactive-layer-gaps.md](interactive-layer-gaps.md) — gaps found writing the Forms, Annotations and Outline demos. Five fixed, one partly. **Open:** authoring a form through the typed AcroForm API, `PdfAcroFieldFlags.Comb`, `PdfInternals.CreateIndirectObject<T>()` returning null, bookmarks and links not surviving page import.
- [ ] [pdfkit-text-parity.md](pdfkit-text-parity.md) — text feature parity against PDFKit's 25 `doc.text()` options. **Sections A–E are built.** Section F is not, and is deliberately left: it is a shaping engine, and bigger than everything above it put together.

## Not started

Written, argued, and not yet acted on.

- [ ] [charting-renderer-seam.md](charting-renderer-seam.md) — asking a charting renderer what it computed. Sequenced after `axis-renderer-duplication.md`, which has landed.
- [ ] [conformance-preconditions.md](conformance-preconditions.md) — a conformance claim that refuses at the point of the claim rather than at save.
- [ ] [dom-property-seams.md](dom-property-seams.md) — the DOM's remaining property seams driven off the value descriptor. Sequenced behind `generated-serialization.md`.
- [ ] [font-seam-contracts.md](font-seam-contracts.md) — stating the lifecycle each of the five global font seams promises.
- [ ] [layout-api-decision.md](layout-api-decision.md) — **a decision note, not a proposal** (G8). Build a constraint-solver layout model, or make the flow model pleasanter? The largest strategic call in the gap analysis, and unmade. Nothing should be built here until it is answered.
- [ ] [legacy-collections-migration.md](legacy-collections-migration.md) — `ArrayList` and `Hashtable` replaced by generic collections. Still 19 files using one and 7 the other; `ParagraphFormatInfo.lineInfos` still boxes a struct per line of every paragraph.
- [ ] [open-mode-enforcement.md](open-mode-enforcement.md) — `PdfDocumentOpenMode` enforced where it is named. `CanModify` is still hardcoded `true`; twelve guarded operations at seven places are waiting on it.
- [ ] [shared-character-scanner.md](shared-character-scanner.md) — one character scanner shared between the document lexer, the content lexer and the DDL scanner.
- [ ] [shared-visual-order.md](shared-visual-order.md) — the leftmost-position rule written once instead of in both layout engines.
- [ ] [structure-tagger-interface.md](structure-tagger-interface.md) — separating "my parent" from "the element I just opened" in the tagger.

---

## The competitive gap analysis

Eight gaps were named. Seven are closed or substantially closed; the eighth is a decision nobody has
made.

| gap | spec | status |
|---|---|---|
| G1 | [cross-reference-streams.md](cross-reference-streams.md) | done |
| G2 | [tagged-pdf-accessibility.md](tagged-pdf-accessibility.md) | done |
| G3 | [text-shaping-and-bidi.md](text-shaping-and-bidi.md) | done |
| G4 | [pdf-a-conformance.md](pdf-a-conformance.md) | done |
| G5 | [digital-signatures.md](digital-signatures.md) | done, with [signature-lifetime.md](signature-lifetime.md) |
| G6 | [incremental-update-save.md](incremental-update-save.md) | done |
| G7 | [text-extraction.md](text-extraction.md) | done |
| G8 | [layout-api-decision.md](layout-api-decision.md) | **undecided** |

Five of the seven closed gaps had a tail specified after they closed — G2 nested lists, G3 tabbed
right-to-left lines, G4 the unchecked rules and the `A` levels, G5 signature lifetime, G7 tagged
extraction — and all five are now built, each under **Done** above. The second tier of that analysis,
ten features none of which is built, is inventoried in [nice-to-have-inventory.md](nice-to-have-inventory.md).

## Keeping this file honest

- A spec's own status table is what decides its entry here. Change that table first.
- Move a spec between sections in the same commit that lands the work, not afterwards.
- A new spec starts under **Not started** with a one-line summary of what it proposes.
- "Done" means every item is either shipped or recorded as a deliberate exclusion. An item that is
  merely deferred keeps the spec under **In progress**, with the remainder named in its entry above.
