# Spec — a right-to-left line that happens to contain a tab

The last open item of gap **G3**. `docs/specs/text-shaping-and-bidi.md` is the note for shaping,
bidirectional text and font fallback as a whole, and calls this one a design question rather than a
defect, which it is — right up to the point where somebody puts a tab in an Arabic paragraph.

| item | what | status |
|---|---|---|
| 1 | A tab divides the line into segments; each segment is reordered within itself, the tabs stay put | done |
| 2 | The tab-width list is replayable, so the probing walk and the real walk read the same widths | done |
| 3 | A left-to-right tabbed line, and a line with nothing to swap, come out exactly as before | done, pinned |
| 4 | Decimal tabs still align; leaders, underline, strikethrough and hyperlink areas follow the text | done |
| 5 | Marks stay in reading order; a tabbed line in a table cell reorders too | done |
| 6 | `RenderTab` drawing during the probing walk — a second defect, found on the way | done, fixed |
| 7 | Mirroring tab stops in a right-to-left paragraph; a tab as a bidirectional neutral | not done, **deliberately** |

Covered by the tabbed tests in `src/PinataLayout.Rendering.Tests/BidirectionalParagraphTests.cs`;
`ALineWithATabInItKeepsTheOrderItWasWritten`, which pinned the old refusal, is gone.

Item 6 was not in the spec. Once a tab's segment became reorderable, `RenderTab` — which never looked
at the probing flag — drew its leader, rule and link once in the probe and again for real, the same
shape as the footnote double-draw fixed earlier. It is a commit of its own.

## Problem Statement

MigraDoc reorders a bidirectional line by walking it twice: once to learn how wide every piece is
without drawing anything, then again for real, with each piece placed where the bidirectional
algorithm says it belongs. That is what makes Hebrew and Arabic paragraphs come out in the right
order, and it works.

**Unless the line contains a tab, in which case the line is left in written order.** The renderer asks
up front whether a line could possibly need reordering and answers no to any line with a tab in it, so
the second walk never happens.

There are two reasons for that, and only one of them is a real design question.

The mechanical one: a tab's width is not a property of the tab, it is taken from a list built while
the paragraph was formatted and consumed in order as the line is walked. Walking the line twice
consumes the list twice, and the second walk gets the wrong widths — or runs off the end of the list
entirely. So the guard is not squeamishness; without it the two-walk approach would place a tabbed
line worse than not reordering it at all.

The design one: where the columns of a tabbed line belong in a right-to-left paragraph is a genuine
question, and nothing in the renderer answers it.

The result is that an Arabic table of contents, a Hebrew invoice line, a form with tab-aligned
columns — the documents where tabs and right-to-left text most obviously meet — come out in written
order, which is to say backwards, while the paragraph beside them without a tab comes out correctly.
The library is right about the hard case and wrong about the easy one, which is the worst way round.

## Solution

**A tab divides the line into segments, and each segment is reordered within itself.** The tabs stay
exactly where formatting put them; the text between two tabs is ordered by the bidirectional
algorithm, the same way the text of a line with no tabs in it is.

This answers the design question in the narrowest way that is defensible: it says nothing about where
a tab *stop* belongs in a right-to-left paragraph, and it does not move a single column. What it fixes
is the thing that is unambiguously wrong today — the words inside a column being drawn in the reverse
of the order they should be read in.

For the mechanical problem, the list of tab widths becomes **replayable rather than consumable**. The
probing walk already saves and restores the pieces of renderer state it disturbs; the position in the
tab width list joins them. A line then costs the same second walk every other reordered line costs.

A line with no right-to-left text in it, tab or no tab, is untouched and pays the one cheap scan it
pays today.

## User Stories

1. As an author writing an Arabic document, I want the words in a tab-aligned column to read in the
   right order, so that a table of contents is legible.
2. As an author writing a Hebrew invoice, I want a tabbed description column to read correctly, so
   that the document is usable by the people it is addressed to.
3. As an author, I want a line with several tabs to have each of its columns ordered correctly, so
   that a three-column tabbed layout works throughout.
4. As an author, I want an English phrase inside an Arabic column to stay in its own order, so that a
   product code or a URL is not reversed.
5. As an author, I want a purely left-to-right tabbed line to be drawn exactly as it is today, so that
   every existing document is unchanged.
6. As an author, I want a right-to-left line without tabs to be drawn exactly as it is today, so that
   this change cannot regress what already works.
7. As an author, I want tab stops to stay where I put them, so that fixing the text order does not
   move my columns.
8. As an author using a decimal tab, I want its alignment to behave as it does today, so that numbers
   still line up on the separator.
9. As an author, I want a tabbed line that is also underlined or struck through to have its decoration
   drawn per piece, so that no rule runs backwards across the line.
10. As an author, I want a hyperlink inside a tabbed right-to-left line to keep its clickable area in
    the right place, so that the link is where the text is.
11. As a reader using a screen reader, I want the marked content of the line to stay in reading order,
    so that the structure tree still reads correctly however the glyphs were placed.
12. As a developer, I want the paragraph's declared direction to be honoured on a tabbed line as it is
    on any other, so that direction means one thing throughout.
13. As a developer, I want a tabbed line inside a table cell to reorder like any other line, so that
    the fix is not silently limited to body paragraphs.
14. As a maintainer, I want the tab width list to be replayable rather than special-cased, so that the
    second walk stops being dangerous for every future reason to walk twice.
15. As a maintainer, I want the guard that skips reordering to keep its cheap scan for lines that
    cannot need it, so that the common left-to-right document pays nothing.
16. As a maintainer, I want the ordering rule to remain the one shared between the two layout engines,
    so that the formatter and MigraDoc keep agreeing about what visual order means.
17. As a maintainer, I want the comment that documents the exclusion to go when the exclusion does, so
    that the code stops describing a limitation it no longer has.

## Implementation Decisions

**Segments, not stops.** A tab is a boundary. Reordering happens within each stretch of text between
boundaries, and the boundaries do not move. This is the whole scope decision, and it is what makes the
work small enough to be worth doing.

**Tab stop semantics are unchanged.** Where a tab advances to, how a decimal tab aligns, what a right
tab does — all as today, measured as today. Mirroring tab stops for a right-to-left paragraph is a
different and larger question, argued below, and it is not answered here.

**The tab width list becomes replayable.** The probing walk already saves and restores the leaf it is
at and the horizontal position it reached; the read position in the width list is the same kind of
state and is saved and restored with them. This is a smaller change than recording widths per line and
indexing them, and it keeps the widths themselves exactly where they are computed today.

**The guard narrows rather than disappears.** The cheap scan that decides whether a line could need
reordering keeps its first answer — a line with nothing right to left in it and no declared direction
cannot need moving — and loses only the second, the one about tabs. That first answer is what keeps
every left-to-right document paying one scan and no second walk.

**Ordering uses the rule the two layout engines already share**, the one that orders a word by the
leftmost position any of its characters ends up at rather than by its first character. Nothing new is
invented for tabbed lines; a segment is ordered exactly as a whole line is.

**Decoration and hyperlink rules stay per piece.** They already are, for exactly this reason: drawn
per stretch instead, one rectangle would run backwards across a reordered line. A tabbed line has more
pieces and the same rule.

**Marked content stays in written order.** The second walk changes horizontal positions only; the
order the pieces are emitted in is the order they were written, because that is what the structure
tree is for. This is already true of reordered lines and must stay true when they can contain tabs.

**A tab is never reordered relative to text.** It is a boundary, not a character with a direction, and
treating it as a neutral to be resolved by the algorithm would let a column move.

## Testing Decisions

**What makes a good test here.** Render a paragraph and read the glyphs back off the page with their
positions, then assert what is drawn where. The contract is visual order, so the test asserts visual
order; it does not assert how many walks the renderer made or what its saved state contained.

**Module tested.** `PinataLayout.Rendering.Tests`, which covers MigraDoc's own layout, rasterizes
nothing, and already holds both halves of the prior art for this.

**Prior art.** The bidirectional paragraph tests, which pin the two engines' reordering and are the
model for asserting where a word ended up. The symbol and decimal tab tests, which pin what tabs do
today and are the regression net for the half of this that must not change. The helpers that render a
document and read its glyph runs back through the font's own character map, so an assertion can be
written in words rather than in glyph numbers.

**Cases that must exist.**

- A right-to-left line with one tab: both segments reordered, the tab where it was.
- Several tabs: every segment reordered independently.
- A left-to-right phrase inside a right-to-left segment keeps its own order.
- A left-to-right tabbed line is byte-identical to today's output — the regression that protects every
  existing document, and the tab tests already there are most of it.
- A right-to-left line without tabs is unchanged.
- A decimal tab still aligns on the separator with right-to-left text around it.
- Underline and strikethrough on a reordered tabbed line produce no backwards rule.
- The marked content of a reordered tabbed line is in written order while the glyphs are not — the
  assertion that already exists for reordered lines, extended to tabbed ones.
- A tabbed line in a table cell reorders.

**What is not tested here.** Nothing rasterizes, so no golden image is involved. If one moves,
something changed that this spec did not intend.

## Out of Scope

- **Mirroring tab stops in a right-to-left paragraph.** Word processors measure tab stops from the
  right margin in a right-to-left paragraph, so the columns themselves flip. That is defensible and it
  is a formatting decision, not a drawing one: it changes where every column lands in every existing
  right-to-left tabbed document, it interacts with indents and with table cell widths, and it belongs
  with the properties that define the stops rather than with the walk that places the glyphs. If it is
  ever built, this spec's segments are what it will place.
- **A tab as a bidirectional neutral.** Some engines resolve tabs by the algorithm. That would let
  columns move and is the opposite of the decision above.
- **The text formatter's own tab handling.** The formatter hands whole lines to the drawing surface
  for every alignment but one and does not carry MigraDoc's tab machinery; nothing here changes it.
- **Automatic direction detection.** The paragraph's direction is declared or inferred exactly as it is
  today.

## Further Notes

This is the smallest of the open items and the one most likely to be met by a real user, because the
combination it fails on — tabs and right-to-left text — is not exotic. It is what a form, an invoice
and a table of contents look like.

It is also a good example of a guard that was correct when it was written and became wrong later. The
exclusion was added because the second walk genuinely could not be made safely at the time and because
the design question had no answer. The first reason is a bug that can be fixed; the second turned out
to have a narrow answer that costs nothing. Both were worth writing down at the time, which is why
this spec could be written from the comment rather than from an investigation.
