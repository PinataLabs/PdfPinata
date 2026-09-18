# Spec — draw the characters `MeasureString` measured (T17)

`docs/specs/text-shaping-and-bidi.md:436-442` named a defect and deliberately left it unfixed:
`FontHelper.MeasureString` mapped a tab to a space, dropped every other character below 32 and
treated `\n` as a line break, while `XGraphicsPdfRenderer.DrawString` handed all of them to the
face's `cmap` as ordinary code points and drew whatever came back — almost always the box a font
keeps at `.notdef`. This is that fix landing, and it landed almost exactly as the proposal in this
file described it before the work started: one small internal primitive, called once by each side,
with the `\n`/multi-line disagreement named rather than closed. The one thing worth reading closely
is *why* the `\n` disagreement staying open does not make the shared rule any less shared — that
turns out to be a property of the design, not a compromise in it, and it is the point the rest of
this document is organized around.

## `Fonts/TextNormalization.cs`, built as sketched

`PdfPinata/Fonts/TextNormalization.cs` is a new, internal, static class, sibling to
`TextShaping.cs` as planned, and its two members are the two the proposal specified almost verbatim:

```csharp
internal static bool TryNormalize(char ch, out char normalized);
internal static string NormalizeLine(string text);
```

`TryNormalize` (14-68) is the whole rule in nine lines of logic: `\t` becomes `' '`; anything else
`< 32` answers `false` and drops; everything else passes through untouched. `NormalizeLine` (80-118)
is the "whole line, no split points of its own" wrapper the proposal called for — it scans for the
first character below 32 without allocating, returns `text` itself when there is none (96-97,
exercised by
`NormalizeLine_answersTheSameReferenceWhenThereIsNothingToFilter`), and only then rents from
`ArrayPool<char>.Shared` (99), copies the already-clean prefix wholesale (103) and filters the rest
one character at a time through `TryNormalize` (106-110). This is the "look before copying" shape
the plan's own code comment sketched, implemented rather than merely described.

## The two call sites, changed exactly as decided

`FontHelper.MeasureString`'s slow path (`FontHelper.cs:152-203`) keeps the shape the proposal
insisted on — same `ArrayPool` rent, same single pass, same `\n`-driven line split at 171-183 — and
the inline `if (ch == 9) ch = ' ';` / `if (ch < 32) continue;` block is gone, replaced by one line:

```csharp
189:                        if (!TextNormalization.TryNormalize(ch, out ch))
190:                            continue;
```

`TextStateMeasurementTests.cs:181-195` and `:197-211`, the two tests the proposal named as the
regression guard for this extraction, pass unmodified — this really was extraction, not
reimplementation, on the measuring side.

`XGraphicsPdfRenderer.DrawString` (`XGraphicsPdfRenderer.cs:383-397`) gained the line the proposal
specified, in the position it specified — before anything else reads `s`, including the internal
`MeasureString` call used for alignment at line 404:

```csharp
392:        s = TextNormalization.NormalizeLine(s);
393:
394:        // A string of nothing but control characters is now nothing. Without this the renderer
395:        // would still realize a font, move the pen and write an empty Tj.
396:        if (s.Length == 0)
397:            return;
```

Every later read of `s` in that method — the alignment `MeasureString` call, the Unicode shaping
call, `segment.TextIn(s)`, `ShowTextOperators`, `FallenBackTextOperators`, and the WinAnsi branch's
`AddChars`/`GetBytes` — reads this one normalized local, because it is the same variable, exactly as
planned. The WinAnsi branch needed no separate change: the normalize call sits before the
`font.Unicode` branch, so one call covers both paths, which is what the proposal's implementation
decision on that point asked for.

## The `\n` disagreement is still open — and the shared rule is still genuinely shared

The proposal's title was `MeasureString` and `DrawString` filter the same characters, and it decided
`DrawString` would not become multi-line-aware — the `\n` gap named in
`docs/specs/text-shaping-and-bidi.md` stays open. Both halves shipped exactly as decided:
`DrawString` still draws one line, and a string like `"A newline\nbecomes"` now draws as
`"A newlinebecomes"` rather than boxing the `\n` —
`TextStateOperatorTests.ALineFeedIsAbsorbedByDrawStringWhileMeasureStringStillReportsTwoLines`
pins exactly this, asserting `MeasureString` still reports the height of two lines while the content
stream carries one `Tj` with no `\n` in it.

What is worth spelling out is that this remaining gap does not contradict the title. `TryNormalize`
gives `\n` no special case at all — it falls into the same `ch < 32` bucket as `\r`, `\v`, `\f` and
every other control character, and is dropped exactly as they are
(`TryNormalize_dropsEveryOtherCharacterBelowThirtyTwo` runs `'\n'` and `'\r'` through the same
`[Theory]`). If `\n` were ever handed to `TryNormalize` by both callers, both would drop it, in
agreement. They do not disagree about *that*. The disagreement is one layer up: `FontHelper.
MeasureString`'s loop intercepts `\n` at line 171, *before* it is ever offered to `TryNormalize`, and
treats it as a line-split point rather than a character. `DrawString`'s `NormalizeLine` has no such
interception, so its `\n` falls straight through into `TryNormalize`'s ordinary "everything else
below 32" bucket — the same bucket it would land in if `MeasureString` ever offered it there too. So
the filtering rule is not partially shared and partially not; it is fully shared, and the visible
disagreement is about whether a caller reads `\n` as a split point *before* reaching the shared rule,
which is a line-breaking question, not a filtering one. That is exactly the distinction
`docs/specs/text-shaping-and-bidi.md`'s rewritten passage (438-459 in the current file) draws when it
says measuring and drawing "now do [agree] — about every character except `\n`."

## `SampleApp/Demos/TextDemo.cs`, updated past what the plan asked

The proposal's item 6 asked for the panel's comment and demo string to stop describing behaviour that
was about to become false. `TextDemo.cs:111-123` was rewritten:

- The comment changed from "A newline is a character like any other here, and comes out as the box
  the font draws for a character it has no glyph for" to "A newline is not a line break here and is
  not drawn either — it is dropped, the way `MeasureString` has always dropped it."
- The drawn string changed from `"A newline\nbecomes the box between these words..."` to
  `"A newline\nvanishes between these words, a tab\tis the space it measures as, and a long line
  runs off the edge of the page..."`.

The plan's user story 9 asked only that the panel show what is now drawn for `\n`; the shipped demo
does that and also folds in a live demonstration of the tab-to-space rule in the same string, so the
one panel now teaches both halves of the shared rule — the one that changed (`\n`) and the one that
newly agrees (`\t`) — rather than only the one the plan called out by name.

## Testing: matched the plan, with a few places it did better

`PdfPinata.Test/Fonts/TextNormalizationTests.cs` is new, as planned, and reaches the internal
`TextNormalization` type by reflection rather than asking for `InternalsVisibleTo` — the same pattern
`PdfPinata.Test/IO/CharacterScanningTests.cs` already uses for the shared character scanner
(`docs/specs/shared-character-scanner.md`), and the test file's own remarks say so. It covers
`TryNormalize` and `NormalizeLine` directly: the tab-to-space case, every other sub-32 character
dropped (`\n`, `\r`, `\v`, `\f`, `\0`, escape, and 31 — "the last one below the cut"), everything at
or above 32 untouched including a no-break space and a zero-width joiner, the same-reference no-op
case, and the `\n`/`\r`-dropped-not-split cases at the whole-line level.

`TextStateMeasurementTests.cs` gained the tests the plan asked be added there rather than in a new
file: a tab measures as a single space
(`ATabIsMeasuredAsASingleSpace`), a tab earns the word spacing a space earns
(`ATabIsPaidTheWordSpacingASpaceIsPaid`), and a control character other than tab or line feed costs
nothing at all, not even a character spacing (`AControlCharacterOtherThanATabAndALineFeedCostsNothingAtAll`).

`TextStateOperatorTests.cs` gained the drawing-side half of the same pairing, and this is where the
implementation went further than the plan sketched rather than merely matching it:

- **The plan's exactness argument shipped as written** — a tab-containing string reads back through
  `TextOperators` as the literal a plain-space string would produce
  (`ATabIsDrawnAsTheSpaceItIsMeasuredAs`, `ATabInAUnicodeRunIsDrawnAsASpaceToo`), and a control
  character other than tab is simply absent from the shown text
  (`AControlCharacterOtherThanATabIsNotDrawnAtAll`).
- **The plan's rasterized fallback for proving an absence of ink was not needed.** The proposal's
  testing decisions section reserved `TextStateRenderingTests.cs`'s `InkOf` helper for "the one thing
  operand-reading cannot show: that nothing is inked where the dropped `\n` used to draw a box."
  The shipped test proves the stronger claim without rasterizing at all —
  `AStringOfNothingButControlCharactersDrawsNothingAtAll` asserts
  `TextOperators.ShowTextOperators(page).Should().BeEmpty()`, i.e. that the renderer's early-return
  guard means no text operator is written whatsoever, which is a fact about the content stream, not
  about pixels. No `[Collection(RasterizingCollection.Name)]` test was added for this change.
- **The alignment test is a direct measurement rather than the two-marker trick the plan proposed.**
  The proposal suggested reusing `TextDemo.cs:97-100`'s pattern of drawing a second marker string
  after the measured width and checking where it lands. The shipped test,
  `AWinAnsiStringIsPlacedByTheWidthMeasureStringReportsForTheCharactersItDraws`, instead draws with
  far alignment, reads back the glyph run's actual `X` position, and asserts
  `rect.Right - shown[0].X` equals the width `MeasureString` reported, within
  `TextStateOperatorTests.cs`'s existing `StreamPrecision`. It is the same claim — the width
  measured is the width drawn at — made without a second string standing in for the assertion.

`ALineFeedIsAbsorbedByDrawStringWhileMeasureStringStillReportsTwoLines` is the test the plan called
for last: it asserts `MeasureString` still reports the height of two lines, that the content stream
carries exactly one `Tj`, and that the shown text is `"A newlinebecomes"` with no separator — so a
future change that makes `DrawString` split on `\n` fails this test on purpose, matching the pattern
`docs/specs/text-shaping-and-bidi.md` already uses for `BidirectionalLayoutTests`.

No golden image needed regenerating and none moved. The plan's audit — every `DrawString` call
reachable from a golden-image test goes through `XTextFormatter` or MigraDoc, and both already strip
`\n` and tabs before the string arrives — held, because the change to `DrawString` is provably a
no-op for any string with nothing below 32 in it: `NormalizeLine` returns the same reference and the
method proceeds exactly as before.

## `CLAUDE.md`, kept in step

The shaping section of `CLAUDE.md` gained a paragraph naming `Fonts/TextNormalization`, stating the
rule, saying it runs before `ShapeText` in both callers and why, and naming the one test that pins
the remaining `\n` gap — the same paragraph the codebase now points a reader at instead of the demo
comment that used to be the only place any of this was written down.

## Out of scope, and still out of scope

Everything the proposal put out of scope stayed there: `DrawString` did not gain multi-line
awareness; `\r`/`CRLF` did not gain new line-break behaviour in either function; `TextShaping.
ShapeText` itself was not touched, and normalization happens before it is called in both callers, not
inside it; `\v` and `\f` got no rule of their own beyond "below 32, not `\t`, dropped"; and
`XGraphicsPath.AddString`, reached through `GlobalFontSettings.GlyphOutlineProvider` rather than
through `TextShaping`, was not investigated as part of this change.
