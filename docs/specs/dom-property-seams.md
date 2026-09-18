# Spec — a DOM property as one fact

What driving the DOM's remaining property seams off the value descriptor covers, and what it
deliberately leaves out.
Follows on from `docs/specs/compile-time-dom-value-model.md`, `docs/specs/dom-value-model-findings.md`
and `docs/specs/generated-serialization.md`, whose §5 already sequences part of this.

| item | what | status |
|---|---|---|
| 1 | `Font.Strikethrough` inherited from a style, as the other eight members are | proposed |
| 2 | Style flattening driven by the descriptor rather than by hand | proposed |
| 3 | Deep copy driven by the descriptor rather than by hand | proposed |
| 4 | MDG007 — a diagnostic for a `[DV]` member no seam covers | proposed |
| 5 | A byte-comparison harness for DDL round trips | proposed, sequenced by `generated-serialization.md` |

## Problem Statement

`Meta` and `ValueDescriptor` are a genuinely deep module. Five methods — get, set, is-null, set-null,
create — sit over dotted-name resolution, on-demand child construction, per-kind null semantics and
ref-only cycle breaking, and nothing outside `Internals` needs to know a `ValueKind` exists. Adding a
`[DV]` member gets all of that for free, and the MDDDL *reader* for free with it: `DdlParser` never
names a property, it resolves through `Meta` and dispatches on the descriptor's value type.

That depth covers reading and writing by name, and stops. The other four things the DOM knows about a
property are hand-written, once per property, per seam:

| fact | where | how |
|---|---|---|
| it exists, and its null-ness | the `[DV]` field | generated |
| how to read it from DDL | `DdlParser` | descriptor-driven |
| how to write it to DDL | `Serialize` | by hand — 198 call sites across 75 methods |
| how to inherit it from a style | `VisitorBase.Flatten*` | by hand |
| how to clone it | `DeepCopy` | by hand — 32 overrides |

So a new property is invisibly correct through the descriptor and silently incomplete everywhere
else. There is a live instance in the tree right now. `Font` has nine `[DV]` members;
`VisitorBase.FlattenFont` copies eight. `strikethrough` is declared, guarded in `ApplyFont`, reported
in `CheckWhatIsNotNull`, serialised in two branches, defaulted in a built-in style and rendered by
`ParagraphRenderer` — and absent from the one place that pushes a style's font down onto a
paragraph's. **A style that sets `Strikethrough` does not pass it on.**

`docs/specs/dom-value-model-findings.md` records the same member escaping the *reader* seam for the
same reason. That one was found and fixed. The flatten seam was not swept, so the same member is
still missing from it, which is about as direct a demonstration as this design could offer: the fault
is not the property, it is that there are five places to remember and only two of them remember for
you.

Counted against the real history of `Strikethrough`, adding one `Font` member is nine edit sites
across five files in the DOM assembly alone, plus the renderer, the chart mapper and the charting
package's hand-mirrored `Font`. A plain `ParagraphFormat` leaf is five sites across four files.

## Solution

Drive the remaining seams off the descriptor that already drives the reader.

Flattening a style onto a format is "for every member of this object, if mine is null take the
reference's" — which is precisely what `ValueDescriptor` can answer without being told which members
exist. Deep copy is the same shape. The DDL writer is the mirror of the reader and the reader is
already free.

Where a seam genuinely cannot be driven — and `generated-serialization.md` argues at length that
`Serialize` is one of those, because only 15 of 74 methods are pure value-model output — the answer
is not to generate it but to make the omission a **compile error**: a diagnostic that fails the build
when a `[DV]` member is not covered by the seam it needs.

The fix for `strikethrough` itself is one line. The point of this spec is the reason it was possible.

## User Stories

1. As a document author, I want a style that sets strikethrough to apply it to paragraphs using that
   style, so that the style system means what it says.
2. As a document author, I want every font property to inherit from a style the same way, so that I
   do not have to learn which eight of nine work.
3. As a document author, I want a property I set on a style to survive a deep copy of the document,
   for the same reason.
4. As a document author, I want a property I set to survive an MDDDL write-and-read round trip, so
   that saving and reloading does not lose my formatting.
5. As a developer using the DOM, I want the null-means-inherit semantics to be uniform, so that
   `IsNull` means one thing everywhere.
6. As a maintainer, I want adding a `[DV]` member to be one edit, so that I cannot make a partial
   one.
7. As a maintainer, I want the build to fail when I add a member that a seam does not cover, so that
   the omission is caught at compile time rather than by a user.
8. As a maintainer, I want that diagnostic to name the member and the seam it is missing from, so
   that fixing it is obvious.
9. As a maintainer, I want the flattening visitors to stop listing members by hand, so that
   `FlattenFont` cannot fall behind `Font` again.
10. As a maintainer, I want the same for `DeepCopy`, so that 32 overrides stop being 32 chances.
11. As a maintainer, I want a byte-comparison harness over a corpus of MDDDL, so that a change to
    the writer is provably a no-op when it should be.
12. As a maintainer, I want the existing hand-written `Serialize` methods left alone where they do
    more than emit values, so that this does not become a rewrite of the DDL writer.
13. As a maintainer, I want the value model to keep its current interface, so that the depth it
    already has is not disturbed.
14. As a maintainer, I want `PdfPinata.Test/Dom` and `PinataLayout.DocumentObjectModel.Tests` to
    keep covering the model from their two different sides, so that neither loses its purpose.
15. As a consumer of the DOM, I want no public type to change, so that this costs me nothing.
16. As a consumer, I want documents I already produce to be byte-identical afterwards, apart from
    the strikethrough repair.
17. As a reviewer, I want the strikethrough fix landed separately from the machinery, so that a
    user-visible repair is not buried in a refactor.

## Implementation Decisions

**Land the one-line repair first, on its own.** `FlattenFont` gains its ninth member, with a test.
That is a user-visible bug fix and belongs in its own commit ahead of any machinery, so that it can
be released, described and reverted independently.

**Flattening is driven by the descriptor.** The rule — if mine is null, take the reference's — is
uniform across leaves and nullable values, and `ValueDescriptor` already knows which a member is.
The hand-written `Flatten*` methods reduce to the cases that are not that rule, and those cases
should be visible rather than buried in a list of eight identical lines.

**Deep copy is the same move and should follow it, not accompany it.** Two mechanical changes in one
diff is one too many to review.

**`Serialize` is not generated.** `generated-serialization.md` measured this: only 15 of 74 methods
are pure value-model output, and three of them — `Font`, `Style`, `Character` — are a second DDL
writer in miniature. Generating the easy 15 and leaving 59 hand-written creates two ways to write
DDL, which is worse than one. The writer is addressed by the diagnostic and the harness instead.

**MDG007 is the load-bearing piece.** A diagnostic that fails the build when a `[DV]` member is not
reached by the seams that must reach it. `Diagnostics.cs` currently stops at MDG006, and
`generated-serialization.md` §5 already names MDG007 as the first step. The generator is the only
party that knows a member's kind statically, so it is the right party to ask the question.

**The four copies of the `ValueKind` classification are the reason the diagnostic is worth having.**
The taxonomy is written out in `Generators/Parser.cs` over `ITypeSymbol`, three more times at run
time in `ValueDescriptor`, again in `DdlParser` as `typeof` comparisons, and a fifth time in the
generator test harness's `Preamble`, which declares stand-in copies of `DVAttribute`, `INullableValue`,
`ValueKind`, `Meta`, `ValueDescriptor`, `DocumentObject` and `Unit`. Reducing that count is desirable
and is **not** part of this spec — the harness fork in particular exists because
`DocumentObject.Meta` is `internal abstract` and no test compilation can declare a `DocumentObject`
at all.

**Nothing gains `InternalsVisibleTo`.** The repository has none and the tests reach the model through
real DOM types. That constraint stays.

## Testing Decisions

**A good test here asserts on the document, not on the descriptor.** The observable behaviour is what
a style does to a paragraph, what a copy contains, and what an MDDDL round trip preserves. A test
that constructs a `ValueDescriptor` is testing past the interface — and cannot be written anyway,
because both constructors are `internal`.

**Modules under test.** The DOM itself, from both sides it is already tested from:
`PinataLayout.DocumentObjectModel.Tests` for `Unit`, MDDDL and the flattening visitors, and
`PdfPinata.Test/Dom` for the value model, colours, styles and the generated property machinery.
The generator through `CSharpGeneratorDriver` in
`PinataLayout.DocumentObjectModel.Generators.Tests`.

**Prior art to follow rather than reinvent.** `PdfPinata.Test/Dom/ValueModelKnownDefectsTests.cs`
is where assertions about the model go when they can only be made through a real DOM type.
`GeneratorHarness` is how a diagnostic is asserted — MDG007 gets a test that compiles a type with an
uncovered member and expects the diagnostic, beside the existing MDG001–MDG006 tests.

**The behaviours worth pinning.** That a style setting each of the nine font members passes each one
down to a paragraph — a test per member, or a theory over the nine, so that the tenth member added
is the tenth case. That a deep copy preserves every member. That an MDDDL write-then-read preserves
every member. All three are the same shape and all three are the tests that would have caught
`strikethrough`.

**The byte-comparison harness.** `generated-serialization.md` §5 specifies it and it does not exist.
It is what makes the writer safe to touch: serialise a corpus of documents before and after, and
require the bytes to be equal. Until it exists, no change to `Serialize` should be made at all.

**Tests belong where the project boundaries already put them.**
`PinataLayout.DocumentObjectModel.Tests` references the DOM and nothing else — no renderer, no
backend, no font files — and its `NamedFontsOnly` resolver throws if asked to resolve a face. A test
needing a real font belongs in `PinataLayout.Rendering.Tests` instead.

## Out of Scope

- **Generating `Serialize`.** Argued against at length in `generated-serialization.md` and not
  reopened here.
- **Reducing the four copies of the `ValueKind` classification.** Desirable, constrained by
  `internal abstract Meta`, and its own question.
- **The generator's incremental caching cost.** `dom-value-model-findings.md` records that the
  `Collect()` barrier in `GroupByTypeClosingInheritance` is unmeasured. Unrelated to this.
- **`DdlScanner` as a third lexer.** See `docs/specs/shared-character-scanner.md`.
- **`PdfSharp.Charting.Font`, the hand-mirrored second `Font` type.** Real duplication, different
  problem.
- **Thread safety.** `docs/specs/dom-thread-safety.md` owns that.
- **Testing `Emitter` in isolation.** It was written free of Roslyn types precisely so it could be,
  and `dom-value-model-findings.md` records that it never was. Worth doing; not this.

## Further Notes

The value model is the counter-example that makes the argument. It is deep, and where it reaches, a
new property is correct without anyone thinking about it. The seams it does not reach are exactly
where properties go wrong, and the same member has now gone wrong at two of them. That is not an
argument for less generation; it is an argument for the generator being asked the one question it is
uniquely able to answer — *is this member covered?* — and refusing to compile when the answer is no.

Land the strikethrough line first. Everything after it is machinery, and machinery should not be the
reason a user-visible repair waits.
