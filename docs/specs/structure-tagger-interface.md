# Spec — naming the two questions the tagger answers

What separating "my parent" from "the element I just opened" covers, and what it deliberately leaves
out.
Follows on from `docs/specs/tagged-pdf-accessibility.md`, which built the tagger.

| item | what | status |
|---|---|---|
| 1 | `Current` renamed to what it means | proposed |
| 2 | The `out` parameter as the only route to a newly opened element | proposed |
| 3 | The two-argument overloads reserved for callers with nothing to write | proposed |

## Problem Statement

`StructureTagger` hands out scopes. Content goes in `Block` and `Container`, decoration goes in
`Artifact`, and **anything inside an artifact scope is not tagged at all** — the tagger counts depth
and refuses, because a running head drawn by the paragraph renderer would otherwise appear in the
structure tree as a paragraph.

That refusal has a consequence the interface does not carry. A refused scope returns `Nothing` and
pushes nothing, so after a refused `Block` the parent stack is unchanged and `Current` still names
the *enclosing* element. A renderer that opens a scope and then writes alternate text onto `Current`
writes it onto whatever was current before — the enclosing paragraph, the section, the document.

Both the tagger and `docs/specs/tagged-pdf-accessibility.md` document this, and clearly. The
`Block(…, out PdfStructureElement element)` overload exists precisely so a caller can ask correctly,
and its remarks explain the trap and name the case that reaches it: *"A figure in a running head
reaches exactly that case, because a header is drawn inside an artifact and an artifact does not
change what is current."*

The difficulty is that `Current` has a genuinely correct use, in the same files, spelled the same
way. As a *parent* for a new element it means exactly what its own doc comment says — *"the element
that anything tagged now becomes a child of"* — and `ParagraphRenderer` and `TableRenderer` both use
it that way correctly. The correct spelling of "the element I just opened" is the `out` parameter,
which `ImageRenderer` and `ChartRenderer` both use correctly.

So there are three legal reads of `Current` and one illegal one, identical in spelling and in type,
distinguished only by *when* the read happens. The compiler cannot tell them apart. And the
two-argument `Block` and `Container` overloads are the shorter, more inviting spelling, which is the
one a new renderer will reach for first.

No live defect was found. Every current caller is correct. This is a trap for the next renderer, not
a bug today.

## Solution

Give the two questions two names.

`Current` is renamed to say that it means the parent — the element a new child would attach to. The
`out` parameter stays the only way to obtain a newly opened element, and it is already nullable, with
null meaning "not tagged", which is the honest answer.

The two-argument overloads remain for callers that have nothing to write onto the element, which is
most of them.

## User Stories

1. As a renderer author, I want the property that gives me a parent to be named "parent", so that I
   do not mistake it for the element I just opened.
2. As a renderer author, I want the only way to reach a newly opened element to be the `out`
   parameter, so that I cannot ask the wrong way.
3. As a renderer author, I want that element to be nullable, so that "this was not tagged" is a value
   I have to handle rather than a fact I have to know.
4. As a renderer author drawing inside an artifact, I want my alternate text to be silently dropped
   rather than attached to an unrelated element, so that a running head cannot corrupt the structure
   tree.
5. As a renderer author, I want the short overloads to remain for the common case, so that a caller
   with nothing to write is not made to accept an `out` parameter.
6. As a document author relying on accessibility, I want alternate text to land on the element it
   describes, so that a screen reader reads the right thing.
7. As a document author, I want a figure in a running head to be an artifact rather than a mislabelled
   figure, so that decoration is not announced.
8. As a maintainer, I want the trap to be expressed in the interface rather than in a doc comment, so
   that it survives someone not reading the doc comment.
9. As a maintainer, I want the existing correct callers to keep being correct, so that the rename is
   mechanical.
10. As a maintainer, I want a test that fails if a scope refused inside an artifact leaks alternate
    text onto its enclosing element, so that the trap has a witness.
11. As a consumer of the library, I want no public type to change, since the tagger is internal.
12. As a consumer, I want every document to be byte-identical afterwards, so that this is provably a
    naming change.

## Implementation Decisions

**`StructureTagger` is `internal`.** Nothing here is public API, so the rename costs no consumer
anything and needs no deprecation. That is what makes an otherwise cosmetic change cheap enough to be
worth making.

**The rename is the whole of it.** `Current` becomes a name that says "parent". Its behaviour,
including the `_parents.Count > 0 ? Peek() : _root` fallback, is unchanged and correct.

**The `out` parameter stays as it is.** It is already the right design: null means not tagged, and
the four overloads already exist. Nothing about `Block` or `Container` changes except that the
alternative spelling no longer invites the wrong reading.

**The two-argument overloads are kept.** Most callers open a scope and write nothing onto the
element; forcing them to accept a discard would be worse. `Container(gfx, key, tag)` already forwards
to the four-argument form with `out _`, which is the right arrangement.

**Whether to keep a member meaning "the element I just opened" at all: no.** There is no correct way
to answer that question from the stack, because a refused scope pushed nothing. The `out` parameter
is the only place where the answer is known, and that is where it should stay.

**The doc comments stay.** Making the name honest does not make the explanation redundant — the
artifact rule is genuinely subtle and `tagged-pdf-accessibility.md` explains why an element is keyed
by its DOM object rather than built per render pass. The prose stops being the *only* protection.

**Every call site is checked, not just renamed.** There are four reads of `Current` across
`ParagraphRenderer`, `TableRenderer`, `ImageRenderer` and `ChartRenderer`, and all are believed
correct. A mechanical rename that does not confirm each one would miss the very defect the rename is
meant to prevent.

**`PdfDocumentRenderer.TagContent` shadowing `DocumentRenderer.TagContent` in a private field, re-synced
in two places, is noted and not fixed here.** One piece of state with two homes is a real smell in the
same file family; it is not this change.

## Testing Decisions

**A good test here asserts on the structure tree of a saved document.** The observable behaviour is
which elements exist, what they contain, and what alternate text they carry. A test that asserts on
the tagger's parent stack is testing past the interface.

**Modules under test.** The MigraDoc renderers through a rendered document, which is how the tagged
output is already tested.

**Prior art to follow rather than reinvent.** `PinataLayout.Rendering.Tests` covers MigraDoc's own
layout and its tagged output and deliberately rasterizes nothing, so it needs neither Ghostscript nor
ImageMagick — which makes it the right home. `TheMarksStayInTheOrderTheTextIsRead` is the model for
asserting on two properties of the tree at once. The four content-stream readers linked from
`PdfPinata.Test/Helpers` are available there.

**The test worth writing, which does not exist today.** Draw a figure inside a running head — a
header, which is drawn inside an artifact — and give it alternate text. Assert that the alternate
text appears nowhere in the structure tree, and in particular not on the enclosing element. That test
passes today, because every caller is currently correct; its value is that it fails the day one is
not, which is the entire justification for this spec.

**A second, cheaper test.** Assert that a `Block` opened inside an artifact scope yields a null
element through the `out` parameter. That pins the contract the rename is protecting.

**Byte-identical output is the acceptance criterion.** This is a naming change. If any document's
bytes move, something else changed and the diff is wrong. The demo smoke tests and the golden images
are the check.

**`PdfPage.Resize` refuses a tagged document.** Unrelated to this change but worth remembering when
arranging a test document: `PdfDocumentRenderer.TagContent` is `true` by default, so code that renders
through MigraDoc and then resizes has to turn it off.

## Out of Scope

- **The artifact rule itself.** Counting depth and refusing to tag inside an artifact is correct and
  is not reopened.
- **How elements are keyed.** `tagged-pdf-accessibility.md` explains why an element is keyed by its
  DOM object rather than built per render pass. Unchanged.
- **`PdfDocumentRenderer.TagContent` shadowing `DocumentRenderer.TagContent`.** Real; separate.
- **The duplicated "figure if described, artifact if not" decision** in `ImageRenderer` and
  `ChartRenderer` — the same eight lines in two files in two brace styles. Related, small, and
  arguably worth doing in the same pass if the reviewer prefers; not specified here.
- **PDF/A-1a, A-2a, A-3a.** The accessible conformance levels are gated on tagging generally, not on
  this.
- **Making the tagger public.** Not proposed.

## Further Notes

This is the weakest candidate of the ten and is written up for completeness. There is no defect: the
tagger documents the trap, provides the correct overload, and every existing caller uses it
correctly. What it has is a shape that will eventually be got wrong — a short inviting overload beside
a longer correct one, and a property whose two meanings are indistinguishable at the point of use.

The case for doing it anyway is that it is genuinely cheap. The type is internal, the rename is
mechanical, the output cannot move, and the artifact-scope test is worth having regardless of whether
the rename happens. The case against is that the prose already works and renaming a member that four
callers use correctly is churn.

If it is done, it should ride along with other work in `PinataLayout.Rendering` rather than be a
change of its own. If it is not, the artifact-scope test should still be written.
