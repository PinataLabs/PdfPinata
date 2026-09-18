# Spec — a list inside a list, said out loud

The last open item of gap **G2**. `docs/specs/tagged-pdf-accessibility.md` is the note for tagged
output as a whole and records this one as blocked on the document object model rather than on the
tagger, which is where it still is.

| item | what | status |
|---|---|---|
| 1 | `ListInfo.NestingLevel` on the DOM — one-based, default 1, set directly or by a style, round-trips through MDDDL | done |
| 2 | The tagger nests a deeper item's list inside the previous item's `/LBody`; shallower closes it | done |
| 3 | A skipped level opens one list, not several; levels compare only as deeper/shallower | done |
| 4 | Every list item has an `/LBody`, nested or not | done, **was already true** |
| 5 | A document that never sets a level produces exactly the tree it produced before | done, pinned |
| 6 | Nesting survives a page break; an outer list resumes its count after an inner one | done |
| 7 | A list object in the DOM, `/Lbl` elements, inferring depth from indentation, outline numbering | not done, **deliberately** |

Covered by `PinataLayout.DocumentObjectModel.Tests/ListNestingLevelTests.cs` and the nesting tests in
`PinataLayout.Rendering.Tests/TaggedOutputTests.cs`. veraPDF still passes the corpus.

The property is called `NestingLevel`, after `ParagraphFormat.OutlineLevel`; the spec left it unnamed.
Dropping below the outermost level a run has seen closes everything and starts a fresh top-level list.

## Problem Statement

A nested list is one of the commonest things in a document and one of the few this library cannot
express.

MigraDoc has no list object. A list is however many consecutive paragraphs happen to carry list
formatting, and the run is visible only from the order the paragraphs are rendered in. The tagger
makes the best of that: it gathers consecutive paragraphs of the same kind into one list element,
breaks the run when anything else is tagged beside them, and reads a change of list type as a new
list. That matches what the page looks like.

What it cannot do is see a nested list as nested, **because nothing in the document object model says
one list is inside another.** An author writes an outer bullet, then three inner ones indented
further, then another outer bullet. On the page that reads correctly, because indentation is doing the
work. In the structure tree it comes out as three lists in a row, or as one list of six items,
depending on whether the inner ones changed type. A screen reader announces "list of six items" and
the reader loses the outline that was the entire point of nesting.

So the accessible document this library produces by default is accurate about everything except one of
the structures accessibility exists to convey. And an author cannot fix it, because there is nothing
to set.

## Solution

**The document object model gains a way to say how deep a list item is**, on the list formatting that
already exists. An author sets it, or a style does; a nested item says it is at level two, and that is
the whole of the new expressive power.

The tagger then reads it. A run of items at a deeper level than the item before them becomes a list
nested inside that item rather than a sibling of it, and returning to a shallower level closes the
inner list. The result is a structure tree with the shape the author drew: a list, whose item contains
a list, whose items are the inner ones.

Nesting a list inside an item forces one more thing to exist. The standard puts a nested list inside
the item's **body**, and the tagger does not currently emit a body element at all — an item's content
sits directly inside the item. So the body arrives with this work, for every list item and not only
the nested ones, because a tree where some items have a body and others do not is harder to read than
either shape consistently.

A document that never sets a level behaves exactly as it does today, one flat list at a time.

## User Stories

1. As an author writing a report, I want a nested bullet list to be tagged as a list inside a list, so
   that a screen reader announces the outline rather than one flat run of items.
2. As an author, I want to say how deep an item is on the list formatting I already use, so that
   nesting is one property rather than a new way of building documents.
3. As an author, I want to nest more than two levels deep, so that a legal or technical outline is
   expressible.
4. As an author, I want to return to a shallower level and have the inner list close, so that the
   outline reads the way I wrote it.
5. As an author, I want to mix bullets and numbers across levels, so that an outline can number its
   sections and bullet their details.
6. As an author, I want an item at the same level as the one before it to stay its sibling, so that
   the ordinary case is unaffected.
7. As an author, I want a level that skips a step — an item at level three directly after one at level
   one — to produce a sensible tree rather than an error, so that a generated document does not fail
   on a data quirk.
8. As an author who never sets a level, I want the output to be exactly what it is today, so that
   adopting a new version changes nothing about documents I already generate.
9. As an author, I want the level to survive being written to and read from the document markup, so
   that a document round-trips.
10. As an author, I want the level to be settable through a style, so that a nested list is a style
    decision rather than something set on every paragraph.
11. As an author, I want the level to survive a deep copy of the paragraph or the document, so that
    building a document by cloning a template keeps its structure.
12. As an accessibility auditor, I want the structure tree to show the nesting the page shows, so that
    the tagged document and the visual document agree.
13. As an accessibility auditor, I want each list item to have a body element, so that the tree
    matches the shape the standard describes.
14. As an accessibility auditor, I want continued numbering across a nested list to be preserved, so
    that an outer list resuming after an inner one keeps counting.
15. As a developer, I want the indentation of a nested list to remain mine to control, so that saying
    how deep an item is does not silently move it on the page.
16. As a developer generating documents from data, I want to set the level from a loop counter, so
    that a tree of data becomes a tree of list items without bookkeeping.
17. As a maintainer, I want the nesting decision to live in the tagger and the depth to live in the
    model, so that neither has to guess what the other meant.
18. As a maintainer, I want no inference from indentation, so that a presentational value never
    silently changes the meaning of the document.
19. As a maintainer, I want the validator to keep passing on every corpus document, so that a change to
    the shape of every list does not cost the accessibility claim.
20. As a maintainer, I want the new property to go through the same generated machinery as every other
    property, so that markup, cloning and flattening come for free rather than being written by hand.

## Implementation Decisions

**The model gains a depth, not a list object.** A real list container would be the theoretically
correct answer and the wrong one to build: MigraDoc's model is a flow of block elements, a container
would have to be expressible in the markup, the visitors, the generated value model and every
consumer's code, and it would break every document that already builds lists the flow way. A depth on
the list formatting is the smallest true statement — it says the one thing the tagger cannot infer,
and it says it where every other list decision already lives.

**Depth is one-based and defaults to the outermost level**, so an existing document with no depth set
is a document of level-one items, which is exactly what it is today.

**Nothing about layout changes.** Depth says what the item *is*, not where it is drawn. Indentation
stays with the indent properties that control it now. This is deliberate: the moment depth also moves
things on the page, setting it correctly for the structure tree becomes a visual decision and authors
will set it wrongly to get the spacing they want.

**The tagger compares each item's depth with the one before it.** Deeper opens a list nested inside the
previous item; shallower closes lists until the depths match; equal continues the current one. The
existing rules survive underneath: a change of list type still starts a new list, and anything else
tagged beside the run still breaks it.

**A skipped level opens one list, not several.** An item at depth three after one at depth one is
treated as one level deeper. Refusing it would fail documents generated from real data over a
cosmetic inconsistency, and inventing the missing intermediate list would put an element in the tree
that nothing on the page corresponds to. This is the same judgement heading-level skips get, and the
opposite conclusion, for a reason worth writing down: a heading skip is refused because a heading
hierarchy is the document's navigation and a hole in it is a defect a reader will hit, while a list
depth skip has an unambiguous sensible reading.

**Every list item gains a body element.** The standard puts a nested list inside the item's body, so
nesting requires the body to exist; emitting it only for items that happen to contain a nested list
would produce a tree whose shape varies by content. Labels — the bullet or number itself — are a
separate question and are not part of this: marking the symbol as a label means tagging it apart from
the paragraph that draws it, which is a change in the paragraph renderer rather than in the tagger.

**Numbering is untouched.** Continuation across lists is already decided by existing list formatting
and by the renderer that counts; depth does not silently restart or continue a sequence. An author who
wants an outer list to resume its count after an inner one says so the way they say it now.

**The property goes through the generated machinery**, like every other model property, so markup
reading and writing, cloning, style flattening and the serialization check all follow from declaring
it rather than from writing each by hand.

## Testing Decisions

**What makes a good test here.** Build a document with nested list items, render it, and assert on the
shape of the structure tree — a list containing an item containing a list containing items. The
assertion is about the tree a reader gets, not about how the tagger tracked its run. A test that
reaches into the tagger's state is testing bookkeeping that exists to be rearranged.

**Modules tested.** `PinataLayout.DocumentObjectModel.Tests` for the model half: that the property
exists, defaults sensibly, round-trips through the markup, survives a clone and flattens through a
style. That project references the model and nothing else, which is exactly the right scope for a
property. `PinataLayout.Rendering.Tests` for the tree half, using the existing helper that renders a
document and reads its structure tree back — the same helper the shipped tagging tests use.

**Prior art.** The tagged-output tests for the tree-shaped assertion, including the existing ones that
already assert what a flat list produces and which must keep passing with the body element added. The
heading-level tests for how a structural rule with an edge case is pinned. The model tests for how a
generated property is covered on all four of its axes.

**Cases that must exist.**

- Two levels: outer items, inner items, back to outer. The tree nests and unnests.
- Three levels, to prove nothing is special-cased about the second.
- A skipped level produces one nesting rather than two or an error.
- Mixed types across levels: numbers outside, bullets inside.
- A document that sets no depth produces exactly today's tree, item for item.
- Every list item has a body, nested or not.
- The nesting survives a page break in the middle of an inner list.
- Numbering continues or restarts exactly as it does today.
- The property round-trips through the markup, clones, and flattens from a style.

**Validation.** The corpus document that carries a tagged tree must still conform, because this changes
the shape of every list in every tagged document. That is the check that the body element was
introduced correctly, and it is not something a unit test can answer.

## Out of Scope

- **A list object in the document model.** Argued above; a container is not what a flow model can carry
  without breaking everything that builds lists today.
- **Label elements for bullets and numbers.** They want the list symbol tagged apart from the paragraph
  that draws it, which is paragraph renderer work with its own risks to line layout.
- **Inferring depth from indentation.** Presentation must not decide meaning.
- **Automatic numbering by level** — outline numbering such as 1, 1.1, 1.1.1. That is a numbering
  feature, useful and unrelated; depth does not imply it.
- **Nested lists inside table cells or footnotes** beyond whatever falls out for free. The tagger's
  existing parent rules decide those, and this spec does not change them.

## Further Notes

This is the last of the tagged-output items and the only one that was ever blocked on something other
than the tagger. It is worth noticing why it stayed open longest: every other gap in that work was a
matter of writing the right thing into the file, and this one required the document model to be able
to *mean* something it could not previously mean. The fix is one property, which is a good sign the
diagnosis was right.

The body element is the part most likely to cause a surprise, because it changes the tree for
documents that have nothing to do with nesting. Landing it with the corpus validation in the same
change is what keeps that from being discovered later.
