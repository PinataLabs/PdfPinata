# Spec — text feature parity with PDFKit

[PDFKit](https://github.com/foliojs/pdfkit) documents 25 options on its `doc.text()` method. This
records which of them PdfPinata already answers, which it answers only partly, and which are
missing, together with the work each gap needs. Reference is
[`lib/mixins/text.js`](https://github.com/foliojs/pdfkit/blob/f308aae92f1491b0e952545fc0fbbef561c40e9e/lib/mixins/text.js#L114)
and its companion `lib/line_wrapper.js` at the same revision.

This is the gap analysis and the plan. Sections A to E are built, on
`feat/text-state-and-measurement`. Section F is not, and is deliberately left: see the end.

## Where parity has to land

PDFKit has one text call. `doc.text(str, x, y, options)` wraps the string, styles it, draws it, and
attaches the annotation, all from one options bag. PdfPinata has three layers and no single one
of them is the counterpart:

| layer | what it does | what it cannot do |
|---|---|---|
| `XGraphics.DrawString` (`Drawing/XGraphics.cs:1180-1242`) | emits one `Tj` for the whole string | no wrapping — a `\n` is drawn as a literal, not a break |
| `XTextFormatter` (`Drawing.Layout/XTextFormatter.cs:43`) | greedy word wrap into a rectangle | four options total: alignment, vertical alignment, overflow, line height |
| MigraDoc | a document model with paragraphs, styles and flow | not reachable from someone holding an `XGraphics` |

**The parity surface is `XTextFormatter`.** It is the closest analogue — a string, a rectangle, and
a bag of layout options — and it already lives in the PdfPinata assembly, so nothing here forces
a MigraDoc dependency on a caller who only wants to draw text on a page.

Two consequences run through the whole checklist:

- Options that are *text state* (`characterSpacing`, `wordSpacing`, `horizontalScaling`, `fill`,
  `stroke`, `oblique`, `baseline`) have to be settable on `XGraphics.DrawString` too, because
  `XTextFormatter` draws through it. They cannot be formatter-only.
- Every one of those also changes how wide a string is, so `MeasureString` has to see them. It
  currently cannot: `XGraphics.MeasureString(text, font, stringFormat)` validates its format
  argument and then discards it (`XGraphics.cs:1258` passes `XStringFormats.Default` on to
  `FontHelper`), and `FontHelper.MeasureString` ignores the parameter as well
  (`Drawing/FontHelper.cs:45`). Wrapping computed against an unspaced measurement of spaced text is
  wrong at every line end.

That is why item 1 below comes before everything else.

---

## Parity table

Status is against `XTextFormatter` / `XGraphics`. The MigraDoc column is what a MigraDoc user can
already reach, and is informational — a MigraDoc equivalent does not close a core gap.

| # | PDFKit option | core status | MigraDoc equivalent |
|---|---|---|---|
| 1 | `width` | **done** — `LayoutRectangle.Width` | `PageSetup` margins, indents |
| 2 | `height` | **done** — `LayoutRectangle.Height` + `AllowVerticalOverflow` (`XTextFormatter.cs:108`) | none — a `TextFrame` formats to `double.MaxValue` (`FormattedTextFrame.cs:132`) |
| 3 | `lineBreak` | **done** — `XTextFormatter.LineBreak` | n/a |
| 4 | `lineGap` | **done** — `XTextFormatter.LineGap`, added to the line height | `LineSpacing` + `LineSpacingRule`, 6 rules |
| 5 | `indent` | **done** — `XTextFormatter.Indent` | `ParagraphFormat.FirstLineIndent` |
| 6 | `indentAllLines` | **done** — `XTextFormatter.IndentAllLines` | `ParagraphFormat.LeftIndent` |
| 7 | `paragraphGap` | **done** — `XTextFormatter.ParagraphGap` | `SpaceBefore` / `SpaceAfter` |
| 8 | `ellipsis` | **done** — `XTextFormatter.Ellipsis`, a string | **missing** |
| 9 | `columns` | **done** — `XTextFormatter.Columns` | **missing** — `PageSetup` has no column properties |
| 10 | `columnGap` | **done** — `XTextFormatter.ColumnGap`, 18pt by default | **missing** |
| 11 | `characterSpacing` | **done** — `XStringFormat.CharacterSpacing`, written as `Tc` | **missing**, marked unported at `Font.cs:276` |
| 12 | `wordSpacing` | **done** — `XStringFormat.WordSpacing`, written as `Tw` or as a `TJ` array | **missing** |
| 13 | `horizontalScaling` | **done** — `XStringFormat.HorizontalScaling`, written as `Tz` | **missing**, `Font.cs:277` |
| 14 | `fill` | **done** — pass no brush and the text is not filled | always on |
| 15 | `stroke` | **done** — `DrawString` takes an `XPen`, written as `Tr` 1 or 2 | **missing**, `Font.cs:266` |
| 16 | `oblique` | **done** — `XStringFormat.ObliqueAngle`, in degrees | **missing** |
| 17 | `baseline` | **done** — `XLineAlignment` gains `Hanging`, `Ideographic`, `SvgMiddle` | n/a |
| 18 | `underline` | **done** — `XStringFormat.Underline`, 7 styles, own colour | `Underline`, 7 styles (`enums/Underline.cs`) |
| 19 | `strike` | **done** — `XStringFormat.Strikeout`, the same seven | `Strikethrough`, 7 styles |
| 20 | `rotation` | **done** — `XTextFormatter.Rotation`, degrees anticlockwise | `TextFrame.Orientation`, 90° steps only |
| 21 | `link` | **done** — `XGraphics.AddWebLink` takes world coordinates | `Hyperlink`, `HyperlinkType.Web` |
| 22 | `goTo` | **done** — `XGraphics.AddNamedLink` | `HyperlinkType.Local` + `BookmarkField` |
| 23 | `destination` | **done** — `PdfDocument.NamedDestinations` | `BookmarkField` |
| 24 | `continued` | **done** — `XTextSegmentFormatter` takes the runs together | `FormattedText`, nestable |
| 25 | `features` | **missing** — `GlyphSubstitutionTable.Read()` is an empty `try` block (`Fonts.OpenType/OpenTypeFontTables.cs:1067`); no GPOS, no `kern` | **missing** |

Two of twenty-five were at parity when this was written; twenty-four are now, and `Ts` is emitted
for a text rise PDFKit has no option for at all. One is left: `features`, which is section F and is
a shaping engine rather than an option.

---

## Checklist

Ordered so that each item's prerequisites come before it. Items marked **(blocker)** are depended on
by later ones.

### A — text state, and making measurement see it

- [x] **A1 (blocker). Add the text state, carried by `DrawString` and honoured by `MeasureString`.**
  Built as five properties on `XStringFormat` — `CharacterSpacing`, `WordSpacing`,
  `HorizontalScaling`, `TextRise`, `ObliqueAngle` — rather than a separate `XTextState`.

  Two corrections to what this item originally said. **No interface change was needed:**
  `IXGraphicsRenderer.DrawString` (`Drawing/IXGraphicsRenderer.cs`) already takes an
  `XStringFormat` and always has, so hanging the state there carries it to the renderer for free.
  And `XStringFormats`' presets each build a fresh instance (`"Create new format to allow
  changes"`), so mutable state on the type is not shared between callers — the hazard that would
  have argued for a separate type is not there. `APresetCarriesItsOwnTextStateRatherThanSharingOne`
  pins that down.

  **`RenderingMode` was deliberately left out** and moves to B1. `PdfGraphicsState.RealizeBrush`
  throws for every mode but 0 and 2 (`PdfGraphicsState.cs:258`), so a public property here would
  have thrown at draw time for five of its eight values. It belongs with the work that widens the
  mode and adds the pen overloads, where it can be validated against what the caller passed.
- [x] **A2 (blocker). Make `FontHelper.MeasureString` apply the text state.**
  `Drawing/FontHelper.cs` now adds `CharacterSpacing` per glyph shown — the last one included, as
  PDF does — `WordSpacing` per space, and scales the total by `HorizontalScaling / 100`.

  Three callers threw the format away, not the two this item named. The third is the renderer's own
  `_gfx.MeasureString(s, font)` at `XGraphicsPdfRenderer.cs:390`, whose width places the alignment
  and the underline and strikeout rules. It now measures through the format it draws with. That is
  ahead of itself by one item: until A3-A5 emit the operators, a caller who sets a spacing gets a
  measurement that accounts for it and glyphs that do not. Inert at the defaults, and closed by A3.

  Two things turned up on the way and are fixed here because the restructuring forced the counts to
  be per-line anyway:
  - Bold simulation counted the whole string's characters and charged them to the *widest line*, so
    a simulated-bold multi-line string measured too wide. `SpacingIsCountedPerLineAndTheWidestLine`
    `DecidesTheWidth` covers the shape of it.
  - A line feed decremented that same count, so it was paid a character spacing it never drew.
- [x] **A3. Track and emit `Tc` for `characterSpacing`.**
  The two branches that set `Tc` from the rendering mode alone are now one line —
  `format.CharacterSpacing`, plus the bold-simulation spacing when the mode is 2. The two compose
  rather than one overwriting the other, so asking for a spacing on a simulated-bold font no longer
  quietly un-boldens it.
- [x] **A4. Emit `Tw` for `wordSpacing`, and space the words by hand where `Tw` cannot.**
  Both, as it turned out — the choice this item left open is not really a choice. `Tw` applies to
  the single-byte code 32 and expressly not to the byte 32 inside a multiple-byte code
  (PDF 32000-1 section 9.3.3), so for a font embedded as `Identity-H` a `Tw` is accepted and
  silently ignored. `PdfGraphicsState.NeedsWordSpacingByHand` decides which case a font is in:
  WinAnsi gets `Tw`, Unicode gets a `TJ` array with `-wordSpacing * 1000 / size` between the runs,
  and `Tw` is held at zero for it rather than written and ignored.

  This is the first `TJ` the renderer has ever emitted. All four show-text sites now carry their
  operator in the operand string rather than in the format, which is what lets one of them be `TJ`.

  The sign is the part worth having a test for: a number in a `TJ` array is *subtracted* from the
  horizontal position, so the number that opens a gap is a negative one, and a test that only
  checked the number it wrote would agree with itself whichever way round it was.
  `TextStateRenderingTests` rasterizes instead and measures the ink —
  `TheTwoEncodingsSpaceTheirWordsOutTheSameAmount` is the one that would catch it.
- [x] **A5. Emit `Tz` for `horizontalScaling`.** New realized field, seeded to 100 rather than 0 so
  that a page whose text is unscaled does not open with a redundant `Tz`. The stray literal
  `100 Tz` in the image path (`XGraphicsPdfRenderer.cs:641`) is inside a `q`/`Q` pair and does not
  disturb the tracked value.
- [x] **A6. Emit `Ts` for text rise.** Not in PDFKit's list, but it is the mechanism super/subscript
  should use, and MigraDoc currently fakes both by shifting the baseline and shrinking the font
  (`ParagraphRenderer.cs:1199-1207`). A realized field and a compare, the same shape as A3's.

  One thing it is not: `Ts` lives in the text *rendering* matrix, not the text matrix, so it lifts
  the glyphs without moving where `Td` puts the next string —
  `TextRiseDoesNotDisturbWhereTheNextStringGoes`. The underline and strikeout rules do have to be
  moved by hand, because they are rectangles drawn in graphics mode and never see the text matrix
  at all.

### B — fill and stroke

- [x] **B1. Widen the text rendering mode.** `RealizeBrush` threw for anything but 0 and 2. It now
  fills for 0, strokes for 1 and does both for 2.

  **Mode 3 is not implemented, and `RenderingMode` was not added as a property.** Both of those
  were this item's original plan and both turn out to be wrong. PDFKit's own rule is
  `fill && stroke ? 2 : stroke ? 1 : 0` — it never produces mode 3 either, so invisible text is not
  a parity gap. And with the pen and brush deciding the mode between them there is nothing left for
  a `RenderingMode` property to say that they do not, while there would be plenty for the two of
  them to disagree about. Modes 4-7 stay out of scope, as planned.
- [x] **B2. Add `DrawString` overloads taking an `XPen`.** Six of them, mirroring the six that take
  a brush: a brush alone fills, a pen alone strokes, both do both, and neither is an
  `ArgumentNullException` carrying `PSSR.NeedPenOrBrush` — which is the answer `DrawRectangle`,
  `DrawEllipse` and the rest already give to the same question.

  `IXGraphicsRenderer.DrawString` gained the pen, and that interface is **public**, so this is a
  breaking change for anyone implementing it outside this repo. It was taken rather than avoided:
  the alternative was a second method on the interface saying almost the same thing, or hanging a
  pen off `XStringFormat`, which is not what that type is for. There is one implementation.
- [x] **B3. Compose stroke with bold simulation.** A caller's pen wins outright over the one bold
  simulation would have stroked with.

  The other half of this was not in the original item and matters more. Bold simulation also
  *widens* the glyphs, with a character spacing worked out from the em size, and that widening was
  keyed on the rendering mode being 2. Mode 2 used to mean bold simulation and nothing else; now a
  caller who strokes their own text reaches it too, and owes none of the widening. It is keyed on
  the simulation itself — `StrokingTextDoesNotWidenItTheWayBoldSimulationDoes` is the test that
  fails if that is ever undone.

  The underline and strikeout rules are filled rectangles, so text drawn with a pen and no brush
  has nothing to fill them with; they take the pen's colour.

### C — the formatter's layout options

All of these are in `Drawing.Layout/XTextFormatter.cs`, and most are named in its own upstream TODO
list at lines 443-456.

- [x] **C1. `LineBreak`.** False leaves the text to run on past the right edge. The line breaks
  written into the text are still obeyed — wrapping and breaking are different things, and turning
  off the first must not silently turn off the second.
- [x] **C2. `Indent` (first line) and `IndentAllLines`.** The indent is charged against the line, so
  an indented paragraph wraps sooner, and it belongs to a paragraph rather than to the text, so the
  line after a written break is indented again. `Block.LineIndent` was the field to drive, as this
  item guessed — it had been declared and unused since the class was written.
- [x] **C3. `ParagraphGap`.** Left at each written line break.
- [x] **C4. `LineGap`.** Left after every line, and where a paragraph ends both gaps are owed.
- [x] **C5. `Ellipsis`.** A `string`, as this item called for. The word it lands on is trimmed a
  character at a time until the two together fit the room left on the line. Nothing is marked when
  nothing was cut, nor when `AllowVerticalOverflow` means nothing can be.
- [x] **C6. `Columns` and `ColumnGap`.** As described, with the gap defaulting to PDFKit's 18 points.

  Two things this item did not see. A column has to break at the bottom of the rectangle whatever
  `AllowVerticalOverflow` says, or there is no height to break it at and every column but the first
  stays empty; overflow decides what becomes of the text after the *last* column instead. And
  `GetLines` grouped blocks into lines by their height alone, which was right while there was one
  column and wrong the moment there were two — the first line of the second column sits exactly
  level with the first line of the first, and the two were drawn as one. Blocks carry a `Column`
  now.
- [x] **C7. `Rotation`.** Turns the whole block about the top left corner of the layout rectangle,
  positive anticlockwise. Which way round that is, and which corner it turns about, are checked by
  rasterizing: both are signs in a transform and would read as plausibly the other way.
- [x] **C8. `Continued` — closed by `XTextSegmentFormatter`, not built.**

  `continued` exists so that styling can change in the middle of a paragraph. PDFKit chains
  `text` calls to do it; `XTextSegmentFormatter.DrawString(IEnumerable<TextSegment>, …)` takes the
  runs together instead. Same capability, different shape — and the shape this library already has.
  A resumable cursor on `XTextFormatter` would be a second way to say the same thing, and the
  formatter is built around laying out one string per call.

  The class had **no tests at all**, so the claim that it covers this was worth checking before
  being made. `XTextSegmentFormatterTests` checks it: runs of differing font and colour continue
  along the same line rather than each starting one, each keeps its own colour, and they wrap
  together at the words rather than at the seam between them.

### D — decoration as a per-draw option

- [x] **D1. Decouple `underline` and `strike` from `XFontStyle`.** `XStringFormat.Underline` and
  `.Strikeout` take an `XTextDecoration` each. The `XFontStyle` flags still work and still mean a
  single solid rule; a style set on the format wins over them, and `None` — the default — leaves
  them in charge, so nothing that already underlines its text stops.
- [x] **D2. Decoration style and colour.** All six of MigraDoc's rules - `Single`, `Words`,
  `Dotted`, `Dash`, `DotDash`, `DotDotDash` - in a core `XTextDecoration`, plus `DecorationColor`.

  A solid rule stays a filled rectangle, which is how it has always been drawn and what every
  document made with this library looks like. A broken one cannot be: a rectangle will not dot, so
  those are stroked with a pen carrying the matching `XDashStyle`, as thick as the rule and running
  down the middle of where the rectangle would have been.

  `Words` needs the run broken up and each piece measured from the start of the string, because
  with a character or word spacing in play the width of a run is not the sum of the widths of its
  parts.

  `DecorationColor` is in neither PDFKit nor MigraDoc — MigraDoc's underline is always the colour
  of the font. It is here because a rule in a different colour from its text is the one thing a
  caller cannot get by drawing it themselves, since they cannot know where the font would have put
  it.
- [x] **D3. `Oblique` at an arbitrary angle.** `XStringFormat.ObliqueAngle`, in degrees, leaning
  right for a positive angle.

  The skew already existed but was hardcoded to `sin(20°)` and reachable only through italic
  simulation, and the state that tracked it was a `bool` — `ItalicSimulationOn` — because a fixed
  lean is either on or off. It is now `RealizedTextSkew`, a `double` holding the tangent the last
  `Tm` set, which collapses the four-branch decision in `DrawString` to two: the matrix already
  leans right, or it does not and a `Tm` says so.

  **Italic simulation keeps `sin(20°)`, not `tan(20°)`.** The tangent is what the angle actually
  means, and what a caller's angle is converted with, but sin is what PDFsharp has always skewed
  simulated italics by and what every document built with it looks like. Changing it would move
  every italic glyph in the library for no defect. The two compose by adding, which is exact
  rather than a convenience: shearing by *a* and then by *b* is shearing by *a + b*.

  `AdjustTdOffset` took a `bool adjustSkew` and corrected x by the same hardcoded sinus. It takes
  the skew itself now, or the correction would be wrong for every angle but 20°.
  `ATdThroughALeaningMatrixIsCorrectedForTheLean` holds it down: two strings asked for at the same
  x are eleven points apart per line without it.
- [x] **D4. Fill out `baseline`.** `Hanging`, `Ideographic` and `SvgMiddle` join
  `XLineAlignment`.

  They differ from `Near` and `Far` in what they are measured against, which is the point of them:
  `Near` and `Far` are the top and bottom of the layout *rectangle*, these are the top and bottom
  of the *text*, as the canvas values they come from are. For a rectangle of no height — which is
  what the point overloads make — the two amount to the same thing, and
  `HangingIsWhereNearIsWhenThereIsNoRectangleToSpeakOf` says so.

  **The `// TODO: Use CapHeight` on `Center` is deliberately left alone.** It is a real
  inaccuracy — three quarters of the ascent standing in for half the cap height — but correcting it
  would move every centred string ever drawn with this library, to fix nothing anybody reported.
  `Center` is not the value PDFKit's `middle` is anyway: this one centres the text in the layout
  rectangle, that one puts the middle of the em box on the point. Anyone wanting the latter has
  `SvgMiddle` for the x-height and `Hanging` and `Ideographic` for the two edges.

### E — annotations

- [x] **E1. `Link` as a text option.** `XGraphics` gained `AddWebLink`, `AddDocumentLink`,
  `AddNamedLink` and `AddNamedDestination`, all taking world coordinates.

  The conversion was the whole of it. An annotation is placed in default page space, measured from
  the bottom left; everything drawn is placed in world space, measured from the top left and
  possibly turned or scaled since. `Transformer.WorldToDefaultPage` could already do the sum and
  nothing called it from a place a caller could reach.

  **One annotation per line is not built.** `XGraphics` draws one line at a time and links what it
  is given, which is right for it. Wrapping belongs to `XTextFormatter`, and a link that follows
  wrapped text is a property on the formatter rather than an argument to a draw - left for whenever
  someone wants it.
- [x] **E2. `Destination` — create named destinations.** `PdfDocument.NamedDestinations`, written
  into the catalog as a `/Names /Dests` name tree while the document is saved, because a
  destination points at a page and a page has no object number to point at before then.

  One leaf node holding every name, sorted by bytes as a name tree must be. Balancing would earn
  nothing here - a reader finds a name in one node or in twenty.

  `Resolve` was added on the way and is not in the item. The reader existed and was `internal`, so
  a document opened from a file could be searched by the import machinery and by nobody else. A
  half of a feature that can write names and not read them back is not worth shipping.
- [x] **E3. `GoTo` — link to a named destination.** `CreateNamedLink` alongside the four factories,
  and `PdfPage.AddNamedLink` alongside its three.

  The destination is written as a **string**, which is what sends a reader to the `/Names /Dests`
  tree of PDF 1.2 onwards. Writing it as a name would send it to the `/Dests` dictionary of PDF
  1.1, which is not where E2 puts them.

### F — OpenType features

- [ ] **F1. Parse GSUB.** `GlyphSubstitutionTable.Read()` is an empty `try` block with a `catch`
  that rethrows (`Fonts.OpenType/OpenTypeFontTables.cs:1067-1076`). The table is detected,
  instantiated and never read. This is the whole of item 25's foundation.
- [ ] **F2. Parse GPOS.** Only a tag constant exists (`enums/TableTagNames.cs:156`). No class, no
  reader.
- [ ] **F3. Parse `kern`.** Same — a tag constant at `TableTagNames.cs:188` and nothing else. No
  kerning is applied in measurement or rendering today, which is why `XTextFormatter`'s TODO list
  names it at line 452.
- [ ] **F4. A feature-tag API and a shaping pass.** Script and language system selection, a
  feature-tag set with PDFKit's defaults, and application of substitutions and positioning between
  `cmap` lookup and `Tj`. Requires `TJ` (kerned/positioned arrays), which the renderer has never
  emitted — it writes only `Tj`.
- [ ] **F5. Move off `char`.** Every text loop iterates UTF-16 code units
  (`XGraphicsPdfRenderer.cs:472`, `FontHelper.cs:64`), so non-BMP codepoints are two independent
  lookups and surrogate pairs are broken. Shaping cannot be correct until this is a codepoint path.

---

## Sequencing

**A1 and A2 gate almost everything.** Until the text state exists and measurement honours it, A3-A6,
B1-B3, C5 and D3 have nowhere to read their input from, and any wrapping computed on top of them
disagrees with what is drawn. Both are done.

**Section A is done, and so is D3.** Every property on `XStringFormat` now means something: the
three that change a width are measured and drawn and the two agree, and the two that do not —
`TextRise` and `ObliqueAngle` — are drawn as well. Nothing on the type is inert, which is what the
branch was waiting for.

At the defaults the content stream is what it always was. `Tc`, `Tw`, `Tz` and `Ts` are written only
when they differ from what a content stream starts with; the show-text operator moved into the
operand string without changing a character; and the only difference D3 leaves behind is a space
where simulated-italic text used to have a newline between its `Td` and its `Tj`.

**B is done too**, and cost one deliberate breaking change: `IXGraphicsRenderer.DrawString` takes a
pen. Nothing in the repo implements that interface but `XGraphicsPdfRenderer`.

**C is done**, in three steps: the paragraph options, then the columns, then the finding that
`continued` was already answered by a class with no tests to say so.

**D is done**, and left one thing alone on purpose: the `Center` line alignment still stands three
quarters of the ascent in for half the cap height, because correcting it would move every centred
string this library has ever drawn.

**E is done**, name tree and all.

**F is what is left, and it is not another item on this list.** Everything above was plumbing
through machinery that already half existed. F is a text shaping engine: GSUB is an empty `try`
block, GPOS and `kern` are tag constants, `TJ` would have to carry kerned positions, and every text
loop counts UTF-16 code units, so non-BMP text is broken before shaping is even reached. PDFKit
does not implement any of this either - it takes fontkit. The decision to make first is whether to
write one or depend on one, and that is not a decision a checklist should make quietly.

After that the sections are independent and can be taken in any order. Rough weights:

| section | weight | note |
|---|---|---|
| A | medium | plumbing; no interface change was needed after all, see A1 |
| B | small | the machinery is present and artificially restricted; B2 is the interface change |
| C | medium-large | C6 (columns) is most of it; C1-C4 are each small |
| D | small | D1 and D3 are refactors of working code |
| E | medium | E2 needs a name tree writer, which does not exist |
| F | **large** | a text shaping engine. Bigger than everything above put together |

Section F is not comparable to the rest. PDFKit gets shaping from
[fontkit](https://github.com/foliojs/fontkit), a dedicated font library; PdfPinata parses ten
tables for embedding and subsetting and has no shaping layer to extend. Treat F as its own project,
or as a case for taking a dependency rather than writing one.

## Deliberately not listed

- `XGraphics.DrawString` is not given wrapping. PDFKit's single call does everything; splitting
  layout from drawing is this library's existing shape and the parity surface is the formatter.
- Text clipping modes (`Tr` 4-7) are absent from PDFKit's options too, so B1 stops at mode 3.
- MigraDoc gaps that PDFKit has no counterpart for — multi-column *sections*, linked text frames,
  automatic hyphenation, RTL and bidi — are out of scope here even though several sit close to
  items above.
- `CSequence` is left as it is. It declares `IList<CObject>` and then throws
  `NotImplementedException` from its explicit `IEnumerable<CObject>.GetEnumerator`
  (`Pdf.Content.Objects/CObjects.cs:408-411`), along with `ICollection`'s `Count`, `IsReadOnly` and
  `Remove`. `foreach` binds to the public `GetEnumerator` and works, which is why nothing had
  noticed; LINQ asks for the interface one and does not. The tests reading `TJ` arrays back out
  copy each sequence into a list first (`TextOperators.ItemsOf`). Fixing the type is a change to
  the content object model, not to text, and belongs with whatever next has business there.
