# Spec — a conformance claim that refuses at the claim

What moving the PDF/A preconditions to the point of the claim covers, and what it deliberately
leaves out.
Follows on from `docs/specs/pdf-a-conformance.md`, which built the enforcement this reshapes.

| item | what | status |
|---|---|---|
| 1 | A claim that takes what it needs rather than discovering it at `Save` | proposed |
| 2 | The rules that live in the font writer brought behind the same module | proposed |
| 3 | Version arithmetic in one place instead of three | proposed |
| 4 | `CustomizeMetadata` chained rather than assigned, by construction | proposed |

## Problem Statement

`Options.Conformance = PdfAConformance.PdfA3B` is a plain enum assignment that always succeeds. What
it commits the caller to is discovered later, at `Save`, in `PdfConformanceWriter.Enforce`: the
document must have a title; it must not be encrypted; its colour mode must not be CMYK or
`Undefined` without a profile; it may carry attachments only under PDF/A-3; PDF/A-1 additionally
caps the version at 1.4 and refuses a cross-reference stream.

Six preconditions, none of them in any signature. The module is honest about this — its `<remarks>`
say plainly which rules it checks and which it does not, so that *"nobody reads a successful save as
a validator's verdict"* — and the failure messages are unusually good. The problem is not the
enforcement, it is where it happens relative to where the mistake is made.

The evidence that this is friction rather than good design is that **both test suites independently
grew the same undeclared arrangement**. `XmpMetadataTests` has `Conforming(…)`; `EInvoiceTests` has
`Prepared()`, whose comment explains it exists so that *"a test about invoicing fails for something
about invoicing"*. `src/SampleApp/Demos/FacturXDemo.cs` needs a comment to say that setting `Info.Title`
is load-bearing rather than decorative, because nothing in the type says so.

Two further rules are not in the module that advertises itself as their home. `PdfCIDFont` writes
`/CIDToGIDMap /Identity` because PDF/A and PDF/UA require what ISO 32000-1 leaves to a default, and
writes `/CIDSet` for PDF/A-1 alone. Both read `Options.Conformance` directly. That works only because
`_fontTable.PrepareForSave()` runs before `PdfConformanceWriter.PrepareForSave` in `PdfDocument`, an
ordering documented nowhere. `PdfConformanceWriter`'s own remarks enumerate what conformance does and
does not check and mention neither.

Version arithmetic is spread across three files: `PdfAttachments` raises the floor to 1.7,
`PdfConformanceWriter` refuses PDF/A-1 above 1.4 and raises the floor again, and `PdfDocument` raises
to 1.5 for a cross-reference stream. One of the six preconditions exists *solely* to pre-empt the
last of those, because it happens too late to be seen — the cross-reference check is asked separately
with a comment saying exactly that.

Finally, `PdfDocument.CustomizeMetadata` is a single assignable delegate. Every extension must chain
it. `FacturXInvoice` chains correctly and defends one ordering only: a caller who assigns the hook
*after* `AttachTo` silently drops the extension schema and the four `fx:` properties. The document
still claims PDF/A-3, still carries the attachment, still opens in every reader, and fails validation
for its metadata — the exact confusing failure the package exists to prevent. The rule appears in
prose in two places and in the type system nowhere, and the pinning test exercises only the safe
order.

## Solution

Make claiming a profile an operation that takes its preconditions, and pull the scattered rules
behind it.

A caller says which profile they claim and supplies what that profile requires. What cannot be
supplied — the colour mode, the encryption setting, the attachments — is checked then, against the
document as it stands, and refused with the same messages the writer already produces. What is
supplied late and legitimately, such as an attachment added afterwards, is still checked at `Save`,
because it must be.

The hook problem is solved by construction: an interface for adding to the metadata that appends
rather than replaces means there is no ordering to get wrong.

## User Stories

1. As a developer claiming PDF/A, I want to be told at the claim that I have not set a title, so
   that I fix it where I made the mistake.
2. As a developer, I want to be told at the claim that my document is encrypted, for the same
   reason.
3. As a developer, I want to be told at the claim that my colour mode cannot be described, for the
   same reason.
4. As a developer, I want the message to tell me what to do about it, as the current messages
   already do.
5. As a developer claiming PDF/A-1, I want to be told immediately that a cross-reference stream and
   that claim cannot both be asked for, rather than having the check exist to pre-empt an ordering.
6. As a developer, I want a rule that genuinely cannot be settled until save time to still be
   checked at save time, so that the guarantee is not weakened for the sake of tidiness.
7. As a developer, I want to add my own metadata without knowing whether anything else has, so that
   I cannot silently drop another component's contribution.
8. As a developer, I want to add metadata after attaching an invoice and have both survive, so that
   ordering is not a hidden rule.
9. As a developer of an extension package, I want a way to contribute to the packet that cannot
   replace anyone else's, so that my package is safe to combine.
10. As a maintainer, I want every PDF/A rule reachable from one module, so that the module's remarks
    can honestly enumerate them.
11. As a maintainer, I want `/CIDToGIDMap` and `/CIDSet` to stop depending on an undocumented save
    order, so that reordering `PrepareForSave` cannot silently break conformance.
12. As a maintainer, I want version arithmetic in one place, so that a floor and a ceiling cannot be
    set by three modules that do not know about each other.
13. As a maintainer, I want the two test helpers to become one, so that the arrangement a PDF/A test
    needs is stated once.
14. As a maintainer, I want veraPDF to keep gating, so that a passing unit test is still not a
    validator's verdict.
15. As a consumer, I want the existing enum property to keep working, so that this does not break
    documents I already produce.
16. As a consumer, I want a document that saves today to save afterwards, unless it was one the
    rules should always have refused.

## Implementation Decisions

**The enum property stays.** `Options.Conformance` is public API and removing it would break every
consumer. It keeps working and keeps being enforced at `Save`. What is added is a way to make the
claim that checks what it can immediately — so the late enforcement becomes the fallback rather than
the only path.

**Only rules that can be settled early move early.** Title, encryption and colour mode are properties
of the document at the moment of the claim and can be checked then. Attachments cannot: a caller may
legitimately claim PDF/A-3 and attach afterwards, which is exactly what `FacturXInvoice` does. That
rule stays at `Save` and this spec must not weaken it to make a nicer story.

**Re-checking at `Save` is kept, not replaced.** A document can be changed after the claim. Early
refusal is an improvement to when the caller learns, not a replacement for the guarantee.
`PdfConformanceWriter.Enforce` keeps its job.

**The messages are preserved.** They are the best part of the current implementation — each names the
setting to change and why the rule exists, and the CMYK message explains that the same four numbers
are a different colour on every press. They move; they are not rewritten.

**`/CIDToGIDMap` and `/CIDSet` move behind the conformance module or the coupling is documented.**
Two options, and the choice should be made deliberately. Either the font writer asks a conformance
module what to write, or the save-order dependency is stated in both files and asserted by a test.
The first is better; the second is acceptable and much smaller. What is not acceptable is leaving an
undocumented ordering that two conformance rules depend on.

**Version arithmetic gets one owner.** Floors and ceilings are the same kind of fact and three
modules setting them independently is why one precondition exists only to pre-empt another. The
cross-reference precondition should become unnecessary rather than remain a workaround.

**The metadata hook becomes additive.** A single assignable delegate is the wrong shape for something
several independent packages contribute to. An add-a-contributor interface removes the ordering
question entirely. `CustomizeMetadata` stays for compatibility and is expressed in terms of the new
one.

**This composes with `docs/specs/xmp-extension-schemas.md` and should follow it.** That spec deepens
what a contributor can say; this one fixes how contributors are registered. Doing them in that order
means the hook change lands with a caller that already exercises it.

## Testing Decisions

**A good test here asserts when the refusal happens and what it says.** The observable behaviour is
the exception, its timing and its message. Both matter: the whole point is that the failure moves to
the mistake.

**Modules under test.** `PdfConformanceWriter` through a document saved or claimed;
`PdfPinata.EInvoice` through `AttachTo`; the font writer through a saved document reopened and
read.

**Prior art to follow rather than reinvent.** `src/PdfPinata.Test/IO/XmpMetadataTests.cs` has
`Conforming(…)` and `Save(Action<PdfDocument>)`. `src/PdfPinata.Test/Pdfs/EInvoiceTests.cs` has
`Prepared()`, `Packet(…)` returning an `XDocument`, and `Latin1(…)`. These two helpers should
converge on one, which is itself a small deliverable of this work.
`src/PdfPinata.Test/Pdfs/CidFontConformanceTests.cs` and `StreamLengthTests` pin the writer-level
rules that veraPDF found and must keep passing.

**The behaviours worth pinning.** Each of the six preconditions refused at the claim where it can be,
with a message naming the setting. Each still refused at `Save` when introduced after the claim — the
attachment case in particular. A metadata contribution surviving regardless of the order in which
contributors are added, which is the test the current design cannot pass. `/CIDToGIDMap` present on a
Type 2 CIDFont and `/CIDSet` present under PDF/A-1 alone, asserted against a reopened document rather
than against the writer.

**A test for the ordering that currently works by accident.** Whatever the chosen answer to the font
coupling, there should be a test that fails if `PrepareForSave` is reordered. That test does not
exist today and the coupling it protects is real.

**veraPDF gates and has the last word.** `./verapdf-check.ps1` runs the same script CI does and all
six corpus documents conform. A failure is a regression. `pdfa-3b-facturx` must keep being built
through `PdfPinata.EInvoice`.

## Out of Scope

- **Declaring an XMP extension schema.** `docs/specs/xmp-extension-schemas.md` owns that, and should
  land first.
- **The rules `PdfConformanceWriter` deliberately does not check** — transparency for PDF/A-1,
  `JPXDecode` images. Its remarks explain why, and adding them is a separate proposal.
- **PDF/A-1a, A-2a, A-3a.** Gated on tagging, as `pdf-a-conformance.md` records.
- **PDF/A-4 and PDF/X.** Gated elsewhere.
- **Converting an existing PDF to PDF/A.** A different and much harder problem.
- **The CMYK refusal.** Deliberate and correct: the same four numbers are a different colour on
  every press. Not reopened.
- **`PdfOutputIntents` as a near-pass-through.** Two members over a resource read; a small cleanup
  question of its own.

## Further Notes

Two independent test suites growing the same undeclared arrangement is the clearest evidence a
codebase offers that an interface is missing a parameter. Neither author was doing anything unusual;
both needed the same two facts arranged before the thing under test would run, and both wrote it out
because there was nowhere to say it.

The hook is the sharper half of this and the cheaper one. `FacturXInvoice` gets the chaining right
and documents why, but a rule that only works when every caller remembers it is a rule that will be
broken by the caller who has not read the source — and the failure mode is a document that looks
perfect and fails validation for its metadata, which `pdf-a-conformance.md` has already recorded
happening once in this repository.
