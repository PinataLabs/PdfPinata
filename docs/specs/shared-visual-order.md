# Spec — the leftmost-position rule, once

What sharing bidirectional visual ordering between the two layout engines covers, and what it
deliberately leaves out.
Follows on from `docs/specs/text-shaping-and-bidi.md`, which built both copies.

| item | what | status |
|---|---|---|
| 1 | One module answering "what order do these units draw in" | proposed |
| 2 | `XTextFormatter` and `ParagraphRenderer` both calling it | proposed |
| 3 | Tests for the rule that do not need a page drawn | proposed |

## Problem Statement

PDF has no notion of direction: a show-text operator paints glyphs at the pen and moves the pen
along. A layout engine that hands whole lines to `DrawString` gets reordering for free, because
`TextShaping` promises visual order. An engine that places each word itself has to order them, and
this library has two such engines.

`XTextFormatter` justifies text by placing blocks. `src/PinataLayout.Rendering/ParagraphRenderer` draws
one show-text operator per leaf. Both therefore had to learn the same rule, and both learned it
separately. `docs/specs/text-shaping-and-bidi.md` states the rule once and CLAUDE.md restates it:
order a unit by **the leftmost position any of its characters ends up at**, not by its first
character, because a right-to-left word's first character is its rightmost — and because ordering by
leftmost is also what keeps an English phrase inside a Hebrew sentence in its own order, where
reversing the whole line would turn it round.

The two implementations are the same arithmetic: invert `VisualOrder` into a position-per-character
array filled with `int.MaxValue`, take the minimum position over each unit's character span, and
give a unit that contributed no characters the key of the unit before it. They are close enough to
share a comment verbatim — *"Where each character ended up, which is the inverse of the order the
algorithm answers"* — appearing in both files. They differ only in what a unit is: a `leaf` with a
span in one, a `Block` with a text length in the other.

The instructive part is that this repository has already solved the same shape of problem correctly,
in the same area, in the same week. `src/PdfPinata/Drawing.Layout/LineSpans.cs` takes doubles in and
returns doubles out *precisely so that it belongs to neither engine*, and is called from
`XTextFormatter` and from `src/PinataLayout.Rendering/ObstructedArea`. The widest-free-span scan crossed
the seam. The ordering rule, which is the same kind of pure arithmetic, did not.

So a correction to the one rule both engines had to learn is a two-file change in two assemblies,
linked by nothing but prose.

## Solution

Extract the rule the way `LineSpans` was extracted: a small module taking character spans and a
resolved bidi result, returning the order the units draw in.

The engines keep their own notion of a unit. What they hand over is spans of character indices, and
what they get back is an order. Neither engine learns anything about the other, and nothing about
`XTextFormatter`'s blocks or `ParagraphRenderer`'s leaves needs to be understood by the shared code.

## User Stories

1. As a document author writing Hebrew or Arabic, I want a justified line to be ordered correctly,
   so that the text reads the way it is written.
2. As a document author, I want an English phrase inside a right-to-left sentence to stay in its own
   order, so that quoted or technical text is not reversed.
3. As a document author, I want the same ordering whichever layout engine renders my text, so that
   `XTextFormatter` and MigraDoc do not disagree about a line.
4. As a document author, I want a unit that contributed no text — a bookmark, a line break, a run of
   bidirectional controls — to stay beside whatever it followed, so that invisible things do not
   move visible ones.
5. As a maintainer, I want a correction to the ordering rule to be one edit, so that I cannot fix
   half of it.
6. As a maintainer, I want the rule testable without drawing a page, so that a question about
   ordering has an answer that does not involve a PDF.
7. As a maintainer, I want the shared module to belong to neither engine, so that it does not drag a
   dependency between them.
8. As a maintainer, I want the existing bidirectional tests in both suites to pass untouched, so
   that the extraction is provably behaviour-preserving.
9. As a maintainer, I want the shared module to be the obvious place a third engine would call, so
   that the next one does not write a fourth copy.
10. As a reviewer, I want the diff to show arithmetic moving rather than being rewritten, so that I
    can review it as a move.
11. As a consumer of the library, I want no public type to change and no output to move, so that
    this costs me nothing.

## Implementation Decisions

**The module is placed where `LineSpans` is placed, and for the same reason.**
`src/PdfPinata/Drawing.Layout` is reachable from `XTextFormatter` directly and from
`PinataLayout.Rendering` through its existing dependency on `PdfPinata`. `LineSpans` already
proves the arrangement works and is already called from both.

**Its interface takes spans and returns an order.** Character spans in, permutation out. It must not
know what a unit is, must not take an `XFont`, and must not draw anything. `LineSpans` sets the
precedent: doubles in, doubles out, and a doc comment explaining that the shape is deliberate.

**It is `internal`.** Neither engine's use of it is public API, and this repository carries no
`InternalsVisibleTo`, which is a constraint on how it gets tested rather than a reason to make it
public. See the testing decisions.

**It takes the resolved bidi result rather than resolving for itself.** Both engines already resolve
— `ParagraphRenderer` needs the runs for its own reasons and `XTextFormatter` checks for any
right-to-left run before doing anything at all — and resolving twice would be both slower and a
second opinion.

**The early-out stays with the callers.** Both engines return unchanged when no run is
right-to-left, and that check is cheap and belongs where the caller can skip building spans at all.
CLAUDE.md is explicit that a string entirely below `U+02B0` skips itemisation entirely, and nothing
here may put work back on that path.

**MigraDoc's two-walk arrangement is untouched.** `ParagraphRenderer` walks each line twice — once
with `probing` set to learn every leaf's width, then again for real — and the second walk stays in
the order the leaves were written, so the marked content stays in reading order for the structure
tree. That is a property of the renderer, not of the ordering rule, and this change must not disturb
it. `TheMarksStayInTheOrderTheTextIsRead` asserts both orders at once and must keep passing.

**A line with a tab in it is still left alone.** A tab's width is consumed from a list built during
formatting and cannot be walked twice. That limitation is recorded in `ParagraphRenderer` and is not
addressed here.

**Underline, strikethrough and hyperlink rules stay drawn per leaf.** Drawing them per stretch would
run a rectangle backwards across a reordered line. Unchanged by this.

## Testing Decisions

**A good test here gives spans and asserts an order.** That is the whole of the module's behaviour.
Tests that draw a page to ask about ordering are testing three modules to learn about one — which is
what both suites have to do today, and what this change is partly for.

**Modules under test.** The shared module directly, and both engines through the tests they already
have.

**Prior art to follow rather than reinvent.**
`src/PdfPinata.Test/Drawing/Layout/BidirectionalLayoutTests.cs` and
`src/PinataLayout.Rendering.Tests/BidirectionalParagraphTests.cs` pin the two engines and must keep
passing unchanged — they are the regression proof. `src/PdfPinata.Test/Text/` holds the tests for the
bidi algorithm itself, including `BidiConformanceTests`, which runs both UAX #9 suites in full as one
`[Fact]` each rather than a theory per case.

**Reaching an `internal` module without `InternalsVisibleTo`.** The repository has none and adding
one for this would be the first. Two honest options: place the tests in `PdfPinata.Test` and reach
the module through `XTextFormatter`, which is what the existing bidirectional tests already do; or
make the module public with a doc comment saying what it is for. The first is preferred — the
existing tests already cross that seam and the module is genuinely an implementation detail — and it
means the module's own tests are the engine tests, which is acceptable when the engines are the only
callers.

**The cases worth pinning at the new seam.** A right-to-left run reversing. A left-to-right phrase
inside it keeping its order. A unit with no characters taking its predecessor's key. A unit whose
characters are non-contiguous in visual order taking the leftmost. The last is the one the rule
exists for and is the one a naive implementation gets wrong.

**Nothing about existing output may move.** Every golden image and every existing layout assertion
stays where it is. If one moves, the extraction changed behaviour and is wrong.

## Out of Scope

- **The other duplication between the two engines.** Justification spread, alignment enum
  translation and the probe-then-place pass are each duplicated between `XTextFormatter` and
  `ParagraphRenderer` as well. Larger, and each its own question.
- **Tabbed right-to-left lines.** Left alone deliberately, for the reason recorded in
  `ParagraphRenderer`: a tab's width is consumed from a list that cannot be walked twice, and where a
  tabbed line's columns belong in a right-to-left paragraph is a question nothing here answers.
- **The bidi algorithm itself.** `src/PdfPinata/Text/` is pinned to Unicode 17.0.0 by
  `BidiConformanceTests` and is not touched.
- **`ParagraphFormat.TextDirection` and friends.** The three properties already take one type,
  `BidiParagraphDirection`, and that is settled.
- **A third layout engine.** None is proposed; the module should simply be the obvious place one
  would call.

## Further Notes

This is the cheapest candidate in the review and the one with the clearest precedent. `LineSpans`
already answers every objection that could be raised about placement, dependency direction and
testability — it is the same two callers, the same assembly, the same kind of arithmetic, and it is
already in the tree working.

The deletion test: delete either copy of the ordering code and the rule does not concentrate, it
reappears in the other engine — which is exactly the situation today, with the added cost that the
two can now disagree. `LineSpans` shows what the fixed version looks like.
