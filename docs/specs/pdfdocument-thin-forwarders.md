# Spec — PdfDocument forwards without absorbing

What moving a guard and two nested helpers to where their logic and their dependency actually
belong covers, and what stays exactly where it is because moving it would break every caller.

| item | what | status |
|---|---|---|
| 1 | The eight page-tree forwarders on `PdfDocument` stop duplicating the `CanModify` guard | done |
| 2 | `PdfPages` itself checks `CanModify`, closing the gap where `document.Pages` bypasses it | done |
| 3 | `ImageInfo` moves into a `Pdf.Advanced` consolidator that takes pages, not a document | done |
| 4 | `DocumentHandle` keeps its shape but drops the string round trip around a `Guid` it already has | done |

## Problem Statement

`PdfDocument` has eight methods whose entire body is a `CanModify` guard and one call into
`Catalog.Pages`: `AddPage()` (`PdfDocument.cs:1174-1179`), `AddPage(PdfPage, ...)` (`:1193-1198`),
`InsertPage(int)` (`:1206-1211`), `InsertPage(int, PdfPage, ...)` (`:1225-1230`), `PlacePage`
(`:1244-1249`), `ImportPage` (`:1264-1269`), `DuplicatePage` (`:1284-1289`) and `MovePage`
(`:1296-1301`). `PdfPages` already owns the real logic: `Insert` (`PdfPages.cs:115-177`) has the
three branches that matter — a page already owned by this document being re-inserted, a brand-new
page, and a page imported from a foreign document — and `Place`, `Import`, `Duplicate` and
`MovePage` are implemented there too, each with its own argument checks the forwarder does not
repeat. Deleting the eight forwarders would not change one line of `PdfPages`. That is the
deletion test, and they fail it as complexity: there is none in `PdfDocument` to remove.

What they are not free of is duplication. `CanModify` (`:199-203`) is currently hardcoded to
`true` — the real check is a commented-out line, which is exactly what
`docs/specs/open-mode-enforcement.md` (uncommitted, alongside this spec) proposes to fix. When it
does, the fix has to be applied and verified at all eight call sites, because each repeats
`if (!CanModify) throw new InvalidOperationException(PSSR.CannotModify);` on its own. And the
guard was never complete: `document.Pages` is `public` (`PdfDocument.cs:992`), every mutating
method on it is `public`, and none of them checks `CanModify` at all. A caller who writes
`document.Pages.Add()` instead of `document.AddPage()` was never guarded, forwarder or no
forwarder. `PdfPages.InsertRange` (`PdfPages.cs:349-416`) has no `PdfDocument` forwarder in the
first place — `src/PdfPinata.Test/IO/InsertRangeTests.cs` reaches it directly — so it is a ninth
page-tree mutation path that has never been guarded by anything.

Two helpers live nested inside `PdfDocument` and are reachable only through it. `ImageInfo`
(`:1397-1450`) walks every page's `/Resources/XObject`, hashes each image stream with `MD5Managed`
(`Pdf.Security/MD5Managed.cs`), and is used by exactly one caller, `ConsolidateImages`
(`:1375-1395`). Its `FindAll` takes a whole `PdfDocument` (`:1417`) to do work that only needs the
pages: `doc.Pages.Cast<PdfPage>()` is the only member of `PdfDocument` it touches. Its logic is
real — merging image XObjects by content hash is not trivial — so it passes the deletion test in
the other direction: delete it and `ConsolidateImages` has nothing to call.

`DocumentHandle` (`:1498-1543`) is a `WeakReference` plus a `Guid`-derived string ID, with
`Equals`/`GetHashCode`/`==`/`!=` all defined in terms of that string (`ID = document._guid
.ToString("B").ToUpper()`, `:1503`). It is genuinely used, not orphaned: `PdfDocument.Handle`
(`:917-926`) hands one out, `PdfImportedObjectTable` stores one per external document
(`Pdf.Advanced/PdfImportedObjectTable.cs:49,66`), `ThreadLocalStorage` keys its imported-document
cache by path and compares handles by value (`Pdf.Internal/ThreadLocalStorage.cs:93-122`), and
`PdfFormXObjectTable.DetachDocument` (`Pdf.Advanced/PdfFormXObjectTable.cs:135-149`) walks its
table comparing handles the same way. `XPdfForm.Dispose` (`Drawing/XPdfForm.cs:209`) is a live
caller: disposing a form built from an imported page detaches that document's handle right now.
What fails the deletion test is not the type — three collaborating classes need a comparable,
storable stand-in for "this document, weakly" — it is the string it compares by. `PdfDocument`
already exposes `Guid` publicly (`:911-914`); `DocumentHandle` reformats it into a string and
compares strings instead of comparing the `Guid` it started from.

One more thing surfaced while tracing `DocumentHandle`'s callers, worth recording even though nothing
here fixes it: `PdfDocument.OnExternalDocumentFinalized` (`:1472-1482`), the method that calls both
`ThreadLocalStorage.DetachDocument` and `PdfFormXObjectTable.DetachDocument`, has no live caller.
Its only call site is `PdfDocument`'s finalizer, and the finalizer is entirely commented out
(`:139-142`, `~PdfDocument() { Dispose(false); }` never runs). `docs/specs/crap-coverage-backlog.md`
already describes this method as reachable only by reflection for the same reason. `XPdfForm.Dispose`
reaches the same two `DetachDocument` methods a different way, by calling
`PdfDocument.Tls.DetachDocument` directly, so the detach machinery itself is live — the finalizer path
into it is not.

## Solution

Relocate the guard to the class that owns the branches it protects, relocate the image-consolidation
logic to a dependency it actually needs, and simplify the handle's comparison to the identity it
already carries. Change no public method's name, parameters, or return type.

## User Stories

1. As a developer calling `document.AddPage()`, I want it to keep working exactly as it does today,
   so that upgrading this library does not touch application code that uses the library's most basic
   method.
2. As a developer calling `document.Pages.Add()` directly, I want the same guard `document.AddPage()`
   gets, so that going through the page tree's own API is not a way to skip the open-mode check.
3. As a developer calling `document.Pages.InsertRange`, which has no `PdfDocument` counterpart at all,
   I want it guarded the same way, so that the one page-tree mutation with no forwarder is not the one
   exception to the rule.
4. As a maintainer landing the `CanModify` repair from `open-mode-enforcement.md`, I want to write and
   verify the guard once, in `PdfPages`, rather than at eight near-identical call sites in
   `PdfDocument` plus `PdfPages` itself.
5. As a maintainer reading `PdfDocument.AddPage()`, I want its body to be the one line it actually is,
   so that the guard is not read twice in two different files with no way to tell which one is real.
6. As a maintainer calling `ConsolidateImages()`, I want it to keep working and keep its current
   public signature, so that `src/PdfPinata.Test/Merge.cs` and `src/SampleApp/Demos/AssembleDemo.cs` do not
   change.
7. As a maintainer reading `ImageInfo`, I want its dependency to be the pages it walks, not the whole
   document, so that its signature says what it actually needs.
8. As a maintainer reading `DocumentHandle`, I want it to compare the `Guid` `PdfDocument` already
   exposes, not a reformatted string built from a private copy of the same value.
9. As a maintainer of `ThreadLocalStorage`, `PdfImportedObjectTable`, and `PdfFormXObjectTable`, I
   want `DocumentHandle`'s public shape — `IsAlive`, `Target`, `==` — to stay exactly as it is, so
   that none of the three has to change.
10. As a maintainer, I want the fact that `OnExternalDocumentFinalized` currently has no live caller
    recorded, so the next person tracing `DocumentHandle`'s use does not have to rediscover it.
11. As a consumer of the public API, I want zero behavioural change from this spec on its own — no
    method that succeeds today starts refusing, because `CanModify` is still hardcoded `true` until
    `open-mode-enforcement.md` lands.

## Implementation Decisions

**None of the eight forwarders are deleted.** `AddPage()` alone is called from
`src/PinataLayout.Rendering/PdfDocumentRenderer.cs`, every one of the thirty-odd `SampleApp` demos,
`src/ConformanceCorpus/Corpus.cs`, and dozens of tests. It is public API a fork cannot remove without
cause, and there is no cause here: the method is already exactly as thin as it should be. The fix
for "PdfDocument forwards without absorbing" is not fewer forwarders, it is forwarders that no
longer duplicate logic that belongs one level down.

**The guard moves into `PdfPages`, and is written once per branch it protects rather than once per
forwarder.** `PdfPages.Insert` gains the check `AddPage()`, `AddPage(PdfPage, ...)`,
`InsertPage(int)` and `InsertPage(int, PdfPage, ...)` all currently repeat — one check covers all
four, since all four already call `Insert`. `Place`, `Import`, `Duplicate` and `MovePage` each gain
their own, matching what their `PdfDocument` counterpart checks today. `InsertRange` gains a check
it has never had. The eight `PdfDocument` methods drop their `if (!CanModify) throw` line entirely
and become the one-line forwarders they read as being.

**This is a relocation, not a redesign of what is guarded.** `CanModify` is still hardcoded `true`
(`:202`), so moving `if (!CanModify) throw` from `PdfDocument` to `PdfPages` changes where dead code
sits, not what happens when a document is modified. Whether each of these operations should require
`Modify`, or something looser, is `open-mode-enforcement.md`'s call to make — "each of the sixteen
sites is a decision" is that spec's phrase for exactly this judgment. This spec does not relitigate
it; it makes sure there is one site per operation to make the decision about, not two.

**`ImageInfo` becomes `PdfImageConsolidator`, a new `internal sealed` class in `Pdf.Advanced`, on the
model of `PdfResourcePruner` in the same namespace.** `PdfResourcePruner.Prune(PdfPage)`
(`Pdf.Advanced/PdfResourcePruner.cs:49`) is exactly this shape already: a static entry point in
`Pdf.Advanced` that `PdfDocument.PruneUnusedResources()` (`:1327-1331`) forwards a single line into,
per page. `PdfImageConsolidator.Consolidate(IEnumerable<PdfPage> pages)` takes the same shape for the
whole set at once, because merging by content hash needs to see every page's images together, not
one page at a time. `ConsolidateImages()` becomes `PdfImageConsolidator.Consolidate(Pages)` — one
line, the same shape `PruneUnusedResources` already uses for its own helper. The `MD5Managed`
dependency moves with the logic; `Pdf.Advanced/PdfPageResizer.cs` already references
`Pdf.Security`, so this is not a new layering crossing.

**`ImageInfo` itself is kept as a private nested type of the new class**, not made a standalone
public or internal type — nothing outside `ConsolidateImages`'s old body ever constructed one, and
there is no reason to widen it now.

**`DocumentHandle` keeps its name, its nesting inside `PdfDocument`, and its public members —
`IsAlive`, `Target`, `==`, `!=`.** Three other classes hold or compare it by these names, and none
of the three needs to change. What changes is inside: a private `Guid Id` — set from the document's
already-public `Guid` rather than reformatted through `ToString("B").ToUpper()` — replaces the
`string ID`, and a `WeakReference<PdfDocument>` replaces the untyped `WeakReference`, dropping the
cast from `object` on every read of `Target`. Equality compares two `Guid` values instead of two
strings holding the same information less directly.

**`OnExternalDocumentFinalized` is left exactly as it is.** It has no live caller today because the
finalizer that would call it is commented out, and deciding whether `PdfDocument` should have a
working finalizer is a different, larger question — resurrecting a finalizer changes when `Dispose`
semantics run, which is not what this spec is about. It is recorded here because tracing
`DocumentHandle`'s callers is what found it, and leaving it unrecorded would mean re-finding it later.

## Testing Decisions

**`src/PdfPinata.Test/IO/PagePlacementTests.cs` and `src/PdfPinata.Test/Merge.cs` already cover the
eight forwarders' observable behaviour through `PdfDocument`**, and none of their assertions change:
`PlacePage`, `ImportPage`, `DuplicatePage` and `MovePage` are exercised directly
(`PagePlacementTests.cs:77-249`), and `AddPage` is exercised implicitly by every test in the suite
that builds a document. These are the regression net for "the public method still does what it did."
`InsertPage(int)` and `InsertPage(int, PdfPage, ...)` have no direct happy-path test today — only
the rejection of an already-placed page, reached through `InsertPage` at `PagePlacementTests.cs:46`
— which is a pre-existing gap, not one this change opens, but worth closing alongside it since the
method's body is moving.

**New tests belong on `PdfPages` directly, because that is now where the decision is made.** No test
file targets `PdfPages` on its own today — `Insert`'s three branches, `Place`'s rejection of a
foreign or already-placed page, `Import`'s rejection of a page this document already owns, and
`Duplicate`'s and `MovePage`'s argument checks are all currently reached only by going through
`PdfDocument` first. A new `src/PdfPinata.Test/Pdfs/PdfPagesTests.cs` should call `document.Pages.X(...)`
directly for each of the three `Insert` branches and for `InsertRange`, so that the guard's new home
has its own tests independent of whichever `PdfDocument` method happens to forward into it.

**`PdfImageConsolidator` gets tests that do not depend on `Save`/reopen round-tripping.**
`Merge.cs:36-60`'s `CanConsolidateImageDataInDocument` is the only existing coverage, and it asserts
an aggregate file-size drop after saving, reopening, and merging fifty pages — real, but it exercises
the whole pipeline, not the consolidator's own decisions. Add tests against an in-memory document
built directly: two pages sharing one image XObject already consolidated correctly stay a no-op, two
byte-identical images across pages become one shared reference, two images that merely look alike but
differ by one byte stay separate, and a document with no images does nothing. `Merge.cs` stays as the
end-to-end proof that the moved code still produces smaller files.

**`DocumentHandle`'s existing coverage is `src/PdfPinata.Test/Pdfs/DocumentPlumbingTests.cs`'s
`FormTableProbe`**, which reaches `PdfFormXObjectTable.DetachDocument` and the `Handle` property by
reflection because both are internal with no public route (`DocumentPlumbingTests.cs:99-132`). Its
five tests (`:134-206`) reflect on the property named `Handle` and the method named `DetachDocument`
by name and by parameter type — neither name nor either type's shape changes here, so these tests
need no edit and are the check that the simplified equality still behaves the same: remembering the
same document twice still remembers it once, detaching one document still leaves the others alone.

**No new test asserts a `CanModify` refusal.** `CanModify` is unchanged by this spec — still
`true` unconditionally — so a test asserting that `document.Pages.Add()` throws when the document is
read-only would fail today regardless of where the guard line sits, and belongs to
`open-mode-enforcement.md`'s matrix once that lands, not here.

**`./verapdf-check.ps1` and the demos are still the cheapest end-to-end evidence.** Nothing in this
spec should change any conformance corpus document or any demo's page count, since every path through
`PdfDocument` produces the same result it does today. If either moves, that is a sign the relocation
was not behaviour-preserving.

## Out of Scope

- **`CanModify` actually enforcing.** Still hardcoded `true`. `open-mode-enforcement.md`'s job, and
  this spec is designed so that repair lands in one place — `PdfPages` — once it does.
- **Guarding `PdfPages.Remove` and `RemoveAt`.** Neither has a `PdfDocument` forwarder, neither was
  among the sixteen guarded-but-dead sites `open-mode-enforcement.md` inventories, and neither is
  part of the eight forwarders this spec is about. Whether they should be guarded at all is a fresh
  design decision — a new guard, not a relocated one — and belongs with that spec's "each site is a
  decision," not smuggled into an architectural move.
- **A `PdfDocument.InsertRange` forwarder.** `PdfPages.InsertRange` gains the same `CanModify` check
  as its siblings, but adding a `PdfDocument`-level wrapper for it would grow the public surface this
  spec is trying to stop duplicating, not shrink it.
- **`OnExternalDocumentFinalized` and `PdfDocument`'s commented-out finalizer.** Recorded as a
  finding in the Problem Statement; deciding whether to delete the dead method or revive the
  finalizer is a separate change with its own consequences for `Dispose` semantics.
- **The `WeakReference`/`Guid` TOCTOU gap between `IsAlive` and `Target`.** Pre-existing in the
  current `DocumentHandle` and not introduced or fixed by narrowing `string ID` to `Guid Id` — a
  correctness question about the pattern, not about this spec's change to it.
- **`PdfPage`'s six responsibilities.** Named alongside this candidate in
  `open-mode-enforcement.md`'s own Out of Scope. Real, and a separate spec.

## Further Notes

This spec exists because of its sibling: `open-mode-enforcement.md`'s own Out of Scope section
already names "eight page methods that are a guard plus one call into `PdfPages`, and two nested
helpers ... that fail the deletion test in opposite directions" as real but not its job. Landing
this spec before or alongside that one means the `CanModify` repair is written and verified in one
place — `PdfPages`'s five mutating methods plus `InsertRange` — instead of at the eight forwarders
in `PdfDocument` and then again wherever `PdfPages` itself would need the same check to close the
`document.Pages.Add()` gap. Landing them in the other order would mean writing the guard eight times
in `PdfDocument`, watching this spec delete seven of those eight rewrites a moment later, and writing
a ninth for `InsertRange` regardless. There is no version of both specs landing where writing the
guard at each of the eight forwarders individually survives; the only question is whether that
wasted pass happens once.

The two nested helpers point in opposite directions on purpose, and that is worth keeping visible
rather than resolved into a single rule. `ImageInfo` had real logic and a dependency wider than it
needed — narrow the dependency, keep the logic, move both together. `DocumentHandle` had real
callers and a comparison narrower than what it was already holding — keep the callers' contract,
narrow the comparison to the `Guid` underneath the string. Neither is "helpers nested in a big class
are bad"; each is its own specific mismatch between what a piece of code needs and what it was
handed.
