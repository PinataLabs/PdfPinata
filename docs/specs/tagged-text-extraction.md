# Spec — extraction that reads what the page says about itself

The remaining half of gap **G7** in the competitive gap analysis, and the piece that makes tagged
extraction the thing no untagged reader can match. `docs/specs/text-extraction.md` is the note for
what shipped; this one is for what it does not yet do.

| item | what | status |
|---|---|---|
| 1 | The extractor reads `BDC`, `BMC` and `EMC`, inline and named property lists | done |
| 2 | `PdfTextRun.Tag`, `.ActualText`, `.MarkedContentId` — which sequence a run was drawn inside | done |
| 3 | `ExtractText` skips `/Artifact` runs and prefers a sequence's `/ActualText`, once per sequence | done |
| 4 | Malformed marked content degrades rather than aborts; a depth cap of 1024 | done |
| 5 | A document with no marked content extracts byte-for-byte as before | done, pinned |
| 6 | A word MigraDoc hyphenates extracts whole | done, **by changing the writer** — see below |
| 7 | Reading order, per-glyph boxes, alternate text off the tree, the structure tree itself | not done, **deliberately** |

Covered by `src/PdfPinata.Test/IO/TaggedTextExtractionTests.cs` and
`src/PinataLayout.Rendering.Tests/TaggedOutputTests.AWordBrokenAtAHyphenExtractsWhole`.

**One thing here departs from the spec as written.** It says nothing about the written file changes.
Building it found that MigraDoc's soft-hyphen renderer set `/ActualText` on the *structure element*
alone — `XGraphics.BeginMarkedContent(element)` wrote only the tag and `/MCID` into the content
stream — so under the rule that the extractor never consults the structure tree, story 1 was not
reachable from the page. Rather than break that rule, the writer now also states an element's
`/ActualText` inline in its own `BDC` properties beside `/MCID`, the standard
`/Span <</MCID n /ActualText (…)>> BDC` form, on both `BeginMarkedContent(element)` and
`ResumeMarkedContent`. An element that never set it writes exactly the bytes it wrote before, and the
conformance corpus still validates. Nothing else about the written file changed.

## Problem Statement

A caller who extracts text from a document **this library produced** gets answers that are wrong in
ways the document itself already knows the answer to.

A hyphenated word comes back as two fragments, because it was drawn as two. The word "conformance"
broken across a line is extracted as `confor-` and `mance`, and no amount of cleverness at the
caller's end can tell that apart from a genuine hyphen in `well-formed`. The document does not have
to guess: MigraDoc writes `/ActualText` at every break precisely so a reader need not.

A ligature comes back as whatever the font's `/ToUnicode` map happened to say — usually right, and
wrong exactly when a face maps its `fi` ligature to a private-use code point. Again the document
already says what those glyphs stand for, and again nothing reads it.

And a running head appears in the middle of the prose. Page furniture is drawn with the same
operators as page content, so an extractor that reads operators cannot tell them apart. This library
marks its furniture as an artifact on the way out and then ignores the mark on the way back in.

All three are recorded in the content stream, in bytes this library wrote itself, and the extractor
contains no reference to `BDC`, `BMC`, `EMC` or `/ActualText` at all. The result is a library whose
two halves disagree about the same document.

## Solution

The same two methods answer better.

`ExtractRuns` still returns one run per show-text operator, in drawing order. Each run now also says
**which marked-content sequence it was drawn inside** — its tag, and the text that sequence declares
its glyphs stand for, when it declares any.

`ExtractText` joins the runs that are **content**, skipping the ones that are furniture, and prefers
what a run says it stands for over what its glyphs spell. So a hyphenated word comes back whole, a
ligature comes back as its letters, and a running head does not come back at all.

A document that says nothing about itself is unaffected in both. No marked content means no tags to
report, nothing to suppress and nothing to substitute, and the answer is the one the caller got
before, byte for byte.

## User Stories

1. As a developer indexing generated invoices for search, I want a word hyphenated across a line to
   extract as one word, so that a search for "conformance" finds the invoice that contains it.
2. As a developer indexing generated documents, I want a genuine hyphen in "well-formed" to survive
   extraction, so that fixing the first problem does not create a second.
3. As a developer extracting a report, I want the running head to be left out of the text, so that
   the page's title does not appear halfway through a sentence every time a page breaks.
4. As a developer extracting a report, I want page numbers left out for the same reason, so that a
   paragraph does not end with a stray numeral.
5. As a developer, I want a ligature to extract as the characters it stands for, so that "fi" in
   "efficient" is two letters rather than one unexpected code point.
6. As a developer who needs the furniture after all, I want the artifact runs still returned by the
   lower-level method and labelled as artifacts, so that suppressing them in the convenient method
   costs me nothing I cannot get back.
7. As a developer building a table of contents from an existing PDF, I want each run to say whether
   it was drawn inside a heading, so that I can find the headings without guessing from font size.
8. As a developer, I want each run to say what kind of element it sits in, so that I can tell a table
   cell from a paragraph without reconstructing the layout.
9. As a developer processing documents from another producer, I want extraction to behave exactly as
   it does today when the document carries no marked content, so that adopting a new version changes
   nothing for the files I already handle.
10. As a developer processing documents from another producer, I want a malformed or unfamiliar
    marked-content sequence to be ignored rather than to abort extraction, so that one bad page does
    not cost me the document.
11. As a developer, I want a marked-content sequence that declares its text and contains several
    runs to contribute that text once, so that a substituted word does not appear three times.
12. As a developer, I want nested marked content to report the innermost tag, so that a run inside a
    heading inside a table cell tells me the most specific thing known about it.
13. As a maintainer, I want the extractor to keep taking a page rather than a document, so that the
    seam stays where it is and callers who extract one page do not have to hand over the whole file.
14. As a maintainer, I want no new entry point for this, so that there remains one way to ask what a
    page says.
15. As a developer planning redaction, I want extraction to tell me which runs are content and which
    are furniture, so that redacting a phrase does not silently leave it visible in a running head.
16. As a developer planning redaction, I want a run to report the text it stands for as well as the
    text it draws, so that redacting a hyphenated word removes both of its halves.
17. As an accessibility auditor, I want to extract a document and read the result aloud, so that I
    can hear roughly what a screen reader would hear without installing one.
18. As a developer, I want a run to carry the identifier that links it to the structure tree, so that
    I can do the join myself today even though the library does not do it for me.
19. As a developer, I want the property list of a marked-content sequence to be honoured whether it
    was written inline or named through the page's resources, so that documents from producers that
    prefer the named form are read the same way.
20. As a maintainer, I want the change to be additive on the run type, so that no consumer who wrote
    against the shipped extractor has to change a line.
21. As a maintainer, I want the changed behaviour of the convenient method stated plainly, so that a
    reader of the release notes knows text from a tagged document will differ.
22. As a developer comparing this library against PdfPig, I want extraction from a tagged document to
    be visibly better than extraction from an untagged one, so that tagging the output has a payoff
    beyond compliance.

## Implementation Decisions

**The seam does not move, and nothing new is added beside it.** Extraction stays two static methods
over a page. This is the highest seam available and it is already public, already page-scoped and
already covered; a document-level API would be a second way to ask a question the first one answers.

**The run type gains three things and loses nothing.** The tag of the innermost marked-content
sequence it was drawn inside; the text that sequence declares its glyphs stand for, or nothing when
none is declared; and the marked-content identifier when the sequence carries one. The run type's
constructor is internal, so this is additive for every consumer.

**The identifier is recorded even though this spec does not use it.** It is the join key between a
run and a structure element, it is in the same dictionary already being read, and recording it lets a
caller correlate extraction with the tree by hand. Leaving it out would mean changing the same type
twice for one capability.

**Three operators are learned: begin-marked-content with properties, begin-marked-content without,
and end.** They maintain a stack. The innermost sequence supplies the run's tag. Substitute text is
taken from the innermost sequence that declares any, so a plain span inside a sequence that declares
text still reports that text.

**A property list may be inline or named.** An inline dictionary is read directly; a name is resolved
through the page's resource dictionary in its properties category. A name that resolves to nothing is
treated as a sequence with no properties rather than as an error.

**Substitute text belongs to a sequence, not to a run.** When a sequence declares text and contains
several runs, the joined output contributes that text **once**, at the position of the first run in
the sequence, and the remaining runs of that sequence contribute nothing to the joined text. They are
still returned individually with their own glyph text, so nothing is lost at the lower method. This
is the rule that stops a substituted word appearing once per show-text operator.

**Artifacts are returned, labelled, and skipped by the convenient method.** The lower method returns
every run the page draws; the convenient one joins only those that are not artifacts. Suppression at
the top, completeness underneath.

**The extractor does not consult the structure tree.** Not the tree root, not the page's structure
parent key, not the parent tree. Everything above comes out of the page's own content stream and its
own resources, which is what keeps extraction page-scoped and keeps this spec small.

**Malformed input degrades rather than aborts.** An end without a begin, a begin never ended before
the stream runs out, or a nesting depth past anything sane, each leave the stack in a defined state
and extraction continues. The existing extractor already survives content it does not understand and
this must not regress that.

**The content lexer already reads the inline dictionary correctly**, because the guard that made it
do so was written for exactly this sequence — a span declaring substitute text as a hex string, whose
closing angle brackets used to end the dictionary early and take the rest of the content stream with
them. No lexer change is expected; if one is needed it belongs in the content lexer and its twin
should be checked at the same time.

**This is a behaviour change to the convenient method for tagged documents.** Said out loud rather
than discovered: the same document extracted through the same call answers differently after this,
and better. Documents with no marked content answer identically.

## Testing Decisions

**What makes a good test here.** Build a document, save it, reopen it, extract, and assert on the
string or on the run properties. That is the whole contract and it is entirely external. A test that
reaches for the walker, the operator stack or the marked-content stack is asserting on the mechanism
rather than the behaviour, and will fail the next time the mechanism is rearranged for reasons that
do not concern it.

**Module tested.** `PdfPinata.Test`, alongside the existing extraction tests, which are the prior
art in both shape and location: they build a page with the drawing API, save to a memory stream,
reopen and assert on the extracted text or on a single run's properties. The new cases follow that
shape exactly.

**Prior art.** The existing extraction tests for the round-trip shape. The tagged-output tests in the
rendering test project for how a tagged document is produced in a test. The content-stream reader
helpers shared between the two test projects for reading what was actually written, when a test needs
to prove the document said what the extractor claims to have read.

**Cases that must exist.**

- A hyphenated word rendered through MigraDoc extracts whole.
- A genuine hyphen is untouched.
- A running head is absent from the joined text and present, labelled, in the runs.
- A ligature extracts as its letters.
- A sequence declaring substitute text over several runs contributes it once.
- Nested sequences report the innermost tag.
- A property list named through resources is honoured identically to an inline one.
- A document with no marked content extracts exactly as before — the regression that protects every
  existing consumer, and worth asserting against the same fixtures the shipped tests use.
- An unterminated or unbalanced sequence does not abort extraction.
- A run inside an artifact still reports its own glyph text.

**What is not tested here.** Nothing about the written file changes, so the conformance corpus and
veraPDF are not involved and no golden image moves. If either does, something has been changed that
this spec did not intend.

## Out of Scope

- **Reading order from the structure tree.** The tree is document-scoped and orders content across
  pages; this spec is page-scoped and reports drawing order. That work is a spec of its own and it
  inherits a run type that already knows its tag and its identifier.
- **Per-glyph bounding boxes and layout analysis.** Deliberately out of the shipped extractor and
  still out. That is where PdfPig wins and this library does not compete.
- **Redaction.** Downstream of extraction, a different seam — it rewrites a content stream rather
  than reading one — and its own item in the nice-to-have tier.
- **Alternate descriptions on figures.** Alternate text lives on a structure element, not in the
  content stream, so it arrives with the tree read rather than here.
- **Language.** Same reason: a language on an element is a tree property.
- **Extraction across pages, or of a whole document in one call.** The seam takes a page and keeps
  taking a page.

## Further Notes

The gap analysis argued that tagged extraction is the differentiator, because no reader of an
untagged document can match it. That is true of reading order, which is not in this spec. It is also
true, more cheaply, of the three things that are: a reader guessing at hyphenation, ligatures and
furniture is guessing, and a reader that was told is not.

Most of the value here is **fixing wrong answers rather than adding capability**. The library
currently extracts its own output incorrectly, and it does so using information it wrote itself. That
is the argument for doing this before anything else in the extraction area.
