# Spec — one walk for every file a document carries (T19)

`PdfAttachments.Specifications()` and `PdfConformanceWriter.EmbeddedFiles()` were two answers to
one question — which files does this document carry — each reading the catalog's `/AF` array and
then the `/EmbeddedFiles` name tree, each keeping its own found-list, each deduping by reference.
Only `PdfAttachments.Resolve` was shared between them. `PdfConformanceWriter`'s copy had one thing
the public walk did not: a third source, every page's `/Annots` looking for a `/FS`, needed because
PDF/A-3's association rule is about a file being in the document rather than about how it got
there, and a file hung off a `PdfFileAttachmentAnnotation` was the one path onto a document the
name-tree-only check could not see.

The shape landed exactly as the plan called it. `PdfAttachments.Reachable(bool includeAnnotations)`
is the one walk, `src/PdfPinata/Pdf.Advanced/PdfAttachments.cs:259-291`. `Specifications()` is a
one-line call to it with `includeAnnotations: false` (`PdfAttachments.cs:235`), and
`PdfConformanceWriter.EmbeddedFiles` calls it with `includeAnnotations: true` and keeps the one
filter that was never about reachability (`src/PdfPinata/Pdf.Metadata/PdfConformanceWriter.cs:373-384`).
`Reachable` stayed `internal`. `IsListedIn` was not touched. `AttachmentTests.cs` was not touched
either — it does not appear in the commit's diff at all.

## What shrank, and by how much

The plan predicted `EmbeddedFiles()`/`Collect()`/`Dictionary()` would shrink to "a five-line method"
plus a deleted `Dictionary()`. What actually happened is close to that but not identical.
`EmbeddedFiles` is twelve lines including its opening brace and closing brace — a `foreach` over
`Reachable(includeAnnotations: true)` keeping only specifications with a non-null `EmbeddedFile`
(`PdfConformanceWriter.cs:373-384`) — not five, but the same shape: one call plus one filter,
nothing else. `Collect()` is gone entirely, exactly as planned. `Dictionary()` is gone from
`PdfConformanceWriter.cs` too, but not deleted — it moved, verbatim in behaviour, into
`PdfAttachments.cs:332-337`, where it is called from inside `Reachable`'s annotation branch
(`PdfAttachments.cs:283`). The plan called this move explicitly ("move into `PdfAttachments.cs`
verbatim"), so this is the plan followed, not a divergence from it — worth stating only because the
diff stat's "59 lines changed, mostly removed" on `PdfConformanceWriter.cs` could be misread as pure
deletion when a third of what left that file is code that still exists, just in the other one.

## The one thing the plan explicitly ruled out that still happened

The plan's Out of Scope section says "Touching `PdfNameTree`" is out — correctly; `PdfNameTree.cs`
does not appear in the commit at all. But it also implied, through Testing Decisions ("no new tests
are required... running the existing suite unmodified is the test") and the closing framing
("behaviour-preserving... no test changed"), that the diff would touch only the two `.cs` files
under discussion. It touched three: `docs/specs/pdf-a-conformance.md` gained an eight-line addition
(`git show a8905d0` shows the third hunk) recording that the association rule "looks through one
walk, not a second copy of one," pointing at `PdfAttachments.Reachable` and restating which side gets
`includeAnnotations: true` and why the `/EF` filter stayed in `PdfConformanceWriter`. That is
documentation catching up to a code change already described in that same file, not a scope
expansion — the plan's own Implementation Decisions section already promised the essay above would
need updating, it just did not list the file by name.

## The signature, delivered as designed

`Reachable(bool includeAnnotations)` rather than a `[Flags]` enum, exactly per the Implementation
Decision: there are two configurations any caller needs, and a bool says as much as is true today.
It returns `List<PdfFileSpecification>`, not a richer provenance-carrying type — `EnforceAssociation`
still asks `IsListedIn(associated, attachment)` as a separate question against the `/AF` array
specifically (`PdfConformanceWriter.cs:233`), because "is this one associated" and "is this one
reachable at all" stayed two different questions with two different answers, as planned.

The XML doc comment on `Reachable` (`PdfAttachments.cs:237-258`) states plainly what the parameter
means for each caller and why the shared walk answers "what is reachable" and nothing about which of
those specifications carry bytes — the same division of labour the plan's Implementation Decisions
argued for and the same one `PdfConformanceWriter.EmbeddedFiles`'s remaining doc comment
(`PdfConformanceWriter.cs:357-372`) now cross-references by name.

## What was never true and stayed that way

The plan's own "Further Notes" section already worked out that no visibility problem existed —
`Pdf.Advanced` and `Pdf.Metadata` are namespaces inside one assembly, `PdfConformanceWriter.cs` was
already `using PdfPinata.Pdf.Advanced;` and already called the internal `PdfAttachments.Resolve`.
Nothing in the shipped code needed to test that claim; there was no project reference to add and none
was added. `PdfPinata.EInvoice` still never touches `Reachable` — it calls the public
`Attachments.Add`, as before.

## Testing

No test file changed. The plan named five existing tests in
`src/PdfPinata.Test/Pdfs/AttachmentTests.cs` as adequate coverage for both configurations —
`TheSpecificationIsOneObjectRatherThanOneCopyPerPlaceItIsMentioned` and
`AnAttachmentListedOnlyInTheNameTreeIsStillFound` for the narrow walk through `document.Attachments`,
`AnAttachmentHangingOffAnAnnotationIsSeenByTheCheckToo` and
`PdfA3RefusesAFileThatIsInTheDocumentButNotOfIt` for the wide walk through `Save`'s refusal, and
`ANameTreeThatLeadsBackIntoItselfIsGivenUpOnRatherThanWalkedForever` for the cycle guard neither walk
needed its own copy of. All five are still present in the file, unmodified, at the same names. The
commit message records the full suite passing on both target frameworks and all six conformance-corpus
documents still passing veraPDF — the gate the plan's Testing Decisions called out by name
(`./verapdf-check.ps1`) rather than `dotnet test` alone, since that script is what would have caught a
silent difference between the old two walks and the new one.

No reflection probe was written for `Reachable`, matching the plan: it is internal, this repository
adds no `InternalsVisibleTo`, and both configurations it can produce are already observable through
the public paths listed above.

## What this confirms about the plan's own framing

The plan drew an explicit contrast with `docs/specs/open-mode-enforcement.md`, calling itself the
opposite kind of change — a refactor released as one, not a breaking change — and staked that framing
on "no test's assertion... should need to change." That held. Nothing in `AttachmentTests.cs` moved,
and the commit message's closing line states the same thing in the same words the plan used:
"Behaviour-preserving, so no test changed."
