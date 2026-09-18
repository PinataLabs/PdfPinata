# Spec — the conformance rules nothing checks, and the levels nothing can claim

What is left of gap **G4** after PDF/A, XMP, the output intent, attachments and the e-invoice helper
shipped. `docs/specs/pdf-a-conformance.md` is the note for what was built and
`docs/specs/verapdf-validation.md` for how it is validated; this one is for the two things neither
covers.

| item | what | status |
|---|---|---|
| 1 | `PdfPageWalk` — the pruner's resource walk lifted into a base class; `PdfPageResourceUsage` is its second caller | done |
| 2 | Transparency and JPXDecode refused under PDF/A-1; `/Interpolate true` refused under every part | done |
| 3 | A colour space the output intent does not describe is refused, judged by component count over the whole document | done |
| 4 | `PdfAConformance.PdfA1A` / `PdfA2A` / `PdfA3A` — the `A` levels, requiring a tagged document that passes `PdfUaValidator` | done, and veraPDF passes all three |
| 5 | `PdfUAConformance.PdfUA2` — `pdfuaid:part 2`, `rev 2024`, PDF 2.0 namespace, `/FENote`, `/ListNumbering` | done, **but not validated** — see below |
| 6 | Every link annotation carries `/F` with Print set — the flags PDF/A and PDF/UA-2 require and the writer never wrote | done, found by veraPDF |
| 7 | A Factur-X invoice attaching to a prior PDF/A-3a claim keeps it rather than downgrading it | done, found on the way |
| 8 | A page whose content walk gives up is unchecked rather than refused; per-page colour-space judgement | not done, **deliberately** |

Covered by `PdfPinata.Test/IO/ResourceConformanceRulesTests.cs`,
`PdfPinata.Test/Annotations/LinkAnnotationConformanceTests.cs`, the `A`-level and PDF/UA-2 tests in
`PinataLayout.Rendering.Tests/PdfUaConformanceTests.cs`, and `EInvoiceTests`. The corpus grew from six
documents to nine gated ones — `pdfa-1a`, `pdfa-2a`, `pdfa-3a` — and all nine conform.

**PDF/UA-2 is the one claim veraPDF does not yet pass.** A document claiming it fails a single clause,
8.8: every destination inside the document must be a *structure destination*, a PDF 2.0 `/SD` whose
contents ISO 32000-2 never actually defines (pdf-association/pdf-issues#162 is the open erratum). Rather
than guess at a syntax and ship links that may not navigate, no `pdfua-2` document is in the corpus,
`PdfUA2` says so in its own remarks, and the four rules that *were* fixed for it stay fixed. It is a
claim the library can make and cannot yet stand behind, and the enum member says which.

## Problem Statement

**Four archival rules are real, and none of them is checked.** A document may claim PDF/A-1 and paint
with transparency; it may claim PDF/A-1 and carry a JPEG 2000 image; it may claim any profile at all
and set an image to interpolate; and it may hold RGB and CMYK content together while naming an output
intent that describes only one of them. Each is a genuine requirement of the standard, each is
something this library can produce, and each passes a save without a word.

The conformance writer says so in its own comments rather than implying it by silence, which is
honest, and the reason is the same in all four cases: answering the question means **walking every
page's resources**, and nothing does that walk. Transparency can be answered for one form XObject and
not for a page. JPEG 2000 means looking at every image. Colour space means looking at every colour
space in use, not the one the document was configured with.

So a caller learns from veraPDF, or from their customer, what a successful save did not tell them.

**And three profiles cannot be claimed at all.** The archival enum offers the `B` levels only —
conformance to the *look* of the document. The `A` levels, which additionally require the document to
be tagged, are absent, and so is PDF/UA-2. This was the right scope when nothing produced a structure
tree. Tagged output shipped months ago and is on by default for everything MigraDoc renders, so the
library now routinely produces documents that would satisfy PDF/A-2a and has no way to say so.

The two halves are one spec because they share the missing piece and because they are validated the
same way.

## Solution

**One walk, built once, that four rules ask questions of.** The traversal already exists: the resource
pruner reads a page's content stream, follows every resource it names, descends into nested form
XObjects and into the soft masks hanging off graphics states, and stops at a sane depth. It walks a
page's resources to decide what to throw away. The same walk can say what a page *uses*, and the four
unenforced rules are all questions about what a page uses.

So the walk is lifted out of the pruner into something both callers use, and the pruner keeps
behaving exactly as it does today. Then each rule is a question asked of the result, refused at save
with the message style the conformance writer already uses — naming the rule, naming the profile, and
naming what to do about it.

**And the enums grow.** An `A` level claim is the archival rules of its part plus the tagging rules
the accessibility validator already enforces. Both axes exist and both already refuse what they can
check, so claiming PDF/A-2a is claiming both at once and being held to both.

Every new claim gets a document in the conformance corpus, so it is validated by veraPDF in CI rather
than asserted by its author.

## User Stories

1. As a developer claiming PDF/A-1, I want a save to refuse when the document paints with
   transparency, so that I find out from my own build rather than from a validator weeks later.
2. As a developer claiming PDF/A-1, I want a save to refuse a JPEG 2000 image, so that a picture
   chosen for its compression does not quietly void the claim.
3. As a developer claiming any archival profile, I want a save to refuse an image set to interpolate,
   so that a display hint does not cost me conformance.
4. As a developer whose document mixes RGB and CMYK, I want a save to refuse when the output intent
   describes only one of them, so that the colours in my archive mean something in fifty years.
5. As a developer, I want each of those refusals to name the rule and the profile, so that I can
   decide whether to fix the document or to claim a later profile that permits it.
6. As a developer, I want the refusal to happen at save rather than at the claim for these four rules,
   so that I can claim a profile before I have drawn the pages.
7. As a developer, I want the rules that *can* be settled at the moment of the claim to keep being
   settled there, so that the recent improvement in when I find out is not undone.
8. As a developer of an archive, I want to claim PDF/A-2a, so that my documents are archival and
   accessible under one claim rather than two.
9. As a developer, I want to claim PDF/A-1a, so that the oldest and strictest archival profile is
   available to a tagged document.
10. As a developer sending hybrid e-invoices, I want to claim PDF/A-3a, so that an invoice can carry
    its XML and be accessible at the same time.
11. As a developer, I want to claim PDF/UA-2, so that I can meet the current accessibility standard
    rather than only its predecessor.
12. As a developer claiming an `A` level on an untagged document, I want to be refused with a message
    that tells me tagging is what is missing, so that the fix is obvious.
13. As a developer, I want an `A` level claim to be held to every rule its `B` sibling is held to, so
    that the stronger claim is genuinely stronger.
14. As a procurement officer, I want the library's claims to have been validated by an independent
    tool, so that a claim in a tender response is evidence rather than assertion.
15. As a maintainer, I want each new claim to add a document to the conformance corpus, so that a
    regression in it fails the build rather than a customer's ingestion.
16. As a maintainer, I want one walk of a page's resources rather than two, so that the pruner and the
    conformance rules cannot disagree about what a page uses.
17. As a maintainer, I want the pruner's behaviour to be unchanged by the extraction of that walk, so
    that a refactoring does not become a feature change.
18. As a maintainer, I want the walk to be depth-limited as it already is, so that a malformed
    document cannot make the writer run away.
19. As a maintainer, I want new enum members appended and never inserted, so that an assembly compiled
    against the old enum does not silently land in a different profile.
20. As a developer with a document that legitimately uses transparency, I want the refusal to point at
    PDF/A-2, so that I learn the cheapest way out.
21. As a developer, I want a document that claims nothing to be unaffected by all of this, so that the
    common path costs nothing.
22. As a maintainer, I want the four rules to be listed somewhere as enforced once they are, so that
    the comments admitting they are not stop being true and stop being there.

## Implementation Decisions

**The walk is lifted, not duplicated.** The resource pruner's traversal becomes a shared walk over a
page that reports what the page uses; the pruner is then one caller of it and the conformance rules
are another. It already handles the things a second implementation would get wrong: resources named
by a content stream rather than merely present in the dictionary, nested form XObjects with resource
dictionaries of their own, soft masks reached through graphics state parameter dictionaries, and a
depth limit for documents that nest without end.

**The walk reports; it does not judge.** It answers what images, XObjects, colour spaces and graphics
state entries a page uses. Every conformance rule is then a question asked of that answer, written
with the other conformance rules rather than inside the walk. A walk that knew about PDF/A would have
to be changed for every future rule.

**These four are save-time rules, not claim-time rules.** The recent move of what can be settled early
to the moment of the claim stands and is not disturbed: a claim is refused immediately when the
document as it stands already breaks it. What a page will contain is not knowable when the claim is
made, so resource rules are enforced where they always could be — at save, before the bytes go out.

**Which rule applies to which profile is part of the rule, not part of the walk.** Transparency and
JPEG 2000 are refused for PDF/A-1 and permitted by later parts. Interpolation is refused by all of
them. The colour space question applies wherever an output intent is required.

**Both conformance enums grow by appending.** The archival enum gains its `A` levels and the
accessibility enum gains its second part. Members are added **after** the existing ones and no
existing value moves — the same lesson the open-mode enum recorded, for the same reason: the compiler
inlines an enum constant at the call site, so renumbering silently redirects callers compiled against
the old assembly.

**An `A` level claim is the conjunction of two sets of rules that already exist.** The archival rules
of that part, and the tagging rules the accessibility validator already holds a document to. The
implementation is a claim that requires both rather than a third set of rules written out again.

**An `A` level claim on an untagged document is refused at the claim**, because that is settleable
immediately: a document with no structure tree cannot become tagged by being saved.

**Every new claim gets a corpus document.** The corpus exists so that a claim is validated by an
outside tool rather than by the author of the claim, and a new claim without one is a claim nobody has
checked. The corpus documents each make a claim on purpose, because flavour detection is automatic and
a file claiming nothing is held to the fallback flavour.

**The admissions in the conformance writer's comments are deleted as each rule lands.** They are
accurate today and become a lie the moment the rule is enforced.

## Testing Decisions

**What makes a good test here.** Build a document that breaks the rule, claim the profile, save, and
assert that the save was refused and that the message names the rule. Build the same document without
the offending feature and assert the save succeeds. Both are entirely external: the caller sets an
option, calls save, and either gets bytes or gets told why not. A test that reaches into the walk and
asserts what it found is testing the mechanism, and the mechanism is expected to move when the pruner
is next touched.

**Modules tested.** `PdfPinata.Test` for the rules and the refusals, using the existing helper that
builds a document claiming a profile. `ConformanceCorpus` for the new claims, which is not a test
project but is the gate: it writes one document per claim, and the validation script runs the same way
locally and in CI and fails the build on a regression.

**Prior art.** The existing conformance tests for refusal-shaped assertions — claim, save, expect a
throw naming the rule. The stream-length and CID font tests for what a corpus finding turns into once
it is pinned. The pruner's own tests for the walk: extracting it must leave them passing untouched,
and if they need changing, the extraction changed behaviour and has gone wrong.

**Cases that must exist.**

- A PDF/A-1 claim over a page drawn with transparency is refused; the same page under PDF/A-2 is not.
- A PDF/A-1 claim over a JPEG 2000 image is refused; under PDF/A-2 it is not.
- An interpolated image is refused under every archival profile.
- A document mixing device colour spaces against its output intent is refused.
- Transparency reached only through a nested form XObject is still found — the case a shallower walk
  would miss and the reason the walk is the pruner's rather than a new one.
- Transparency reached only through a soft mask on a graphics state is still found.
- Each new `A` level and PDF/UA-2 can be claimed, saved and reopened.
- An `A` level claimed on an untagged document is refused at the claim, naming tagging.
- A document claiming nothing saves unchanged in every one of these cases.
- The pruner's existing behaviour is unchanged.

**Validation.** Six corpus documents conform today; each new claim adds one and all of them must
conform. That is the only test in this repository that is not written by the same people who wrote the
feature, which is precisely why the claims go through it.

## Out of Scope

- **Validating a document this library did not produce.** The conformance machinery refuses to write
  what breaks a rule; it is not a validator, and turning it into one is a different product.
- **Fixing a document that breaks a rule.** Refusing is the contract. Silently flattening transparency
  or re-encoding an image would produce a document the caller did not ask for.
- **Every remaining rule of every profile.** The four here are the ones the code admits to skipping.
  Others exist, and veraPDF remains the authority.
- **PDF/A-4.** A later part with its own rules and a different relationship to PDF 2.0. Nothing here
  is aimed at it.
- **Changing what the corpus documents draw.** They exist to make claims, not to be pretty.

## Further Notes

The four unenforced rules were listed as one item — "the page-resource walk" — because they are one
piece of work wearing four hats. The fourth of them, the colour space question, is easy to miss: it is
recorded in a different comment from the other three and it is the one a mixed-mode document hits in
practice.

There is a pleasing economy in where the walk comes from. The pruner exists to make files smaller, was
written for a completely different reason, and turns out to have solved the hard part of a conformance
problem years before the conformance problem was stated. Using it is cheaper than writing a second
walk, and it also means the answer to "what does this page use" cannot differ between the code that
throws things away and the code that judges what is left.
