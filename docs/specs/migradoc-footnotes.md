# Spec — MigraDoc footnotes

`PinataLayout.DocumentObjectModel` has carried a complete footnote model since the fork began.
`PinataLayout.Rendering` did not contain a line that drew one. Until recently a footnote was
dropped in silence: `ParagraphRenderer.FormatElement` fell through its `default` and the note simply
did not appear, with nothing thrown and nothing logged. Then it threw a descriptive
`NotSupportedException` (`demonstration-app-coverage.md`, item 14), which made the gap audible
without closing it. This spec is the plan that closed it, and the record of what building it
actually took.

Built. Status is tracked in the table below; the three items with a choice in them were settled by
whoever owns that call before the work started, and the choice taken is recorded under each.

| item | what | status |
|---|---|---|
| 1 | `FormattedFootnote` — format a note's block content into a column | done |
| 2 | Reference marks — measure and draw the superscript in the body text | done |
| 3 | Area reservation — shrink the page's content area by the notes it collects | done, single pass |
| 4 | Numbering — the four `Document` properties that decide the mark | done |
| 5 | The separator rule above the note block | done |
| 6 | Notes that do not fit — carrying a note onto the next page | **not split**, by decision |
| 7 | `FootnoteLocation.BeneathText` | done, both values |
| 8 | Notes inside a table cell, a text frame or a header | **refused**, by decision |
| 9 | Tests | done, 24 |
| 10 | Tagged output — `/Reference` in the body, `/Note` at the foot | done, see `docs/specs/tagged-pdf-accessibility.md` |
| 10 | A `Footnotes` panel in the demonstration app | done, 5 pages |

**The fixed point turned out not to be needed.** The design below argued that reserving room for
footnotes was a fixed point wanting an iterative solve, and it is not. See "How it was actually
done" for what replaced it — a single pass, and a much smaller change than this spec expected.

---

## What already exists

The DOM half is done and needs no change:

- `Footnote` — a `DocumentObject` with `Elements` (a `DocumentElements`, so block content:
  paragraphs, tables and images), a `Reference` string, a `Style` and a `ParagraphFormat`. It
  serializes to and parses from DDL already.
- `AddFootnote()` / `AddFootnote(string)` on `Paragraph`, `FormattedText`, `Hyperlink` and
  `ParagraphElements` — so a note can be attached anywhere an inline run can.
- `Document.FootnoteLocation` (`BottomOfPage`, `BeneathText`),
  `Document.FootnoteNumberingRule` (`RestartPage`, `RestartContinuous`, `RestartSection`),
  `Document.FootnoteNumberStyle` (`Arabic`, `LowercaseLetter`, `UppercaseLetter`,
  `LowercaseRoman`, `UppercaseRoman`) and `Document.FootnoteStartingNumber`.
- `StyleNames.Footnote`, a predefined paragraph style based on `Normal`.
- `DocumentObjectVisitor.VisitFootnote`.

So the work is entirely in `PinataLayout.Rendering`, and every public API a caller needs is already
shipped. That is worth stating plainly: **implementing this adds no DOM surface**, which makes it a
much smaller decision than its size suggests.

## Why it is hard

The pipeline is a two-phase one. `FormattedDocument.Format` runs `TopDownFormatter.FormatOnAreas`
over the document's elements, asking an `IAreaProvider` for one area at a time; each element's
`Renderer.Format` decides what fits, and `Render` is a separate later pass driven by the stored
`RenderInfo`s. That works because **the area is known before the content is measured**.

A footnote inverts it. The height of the note block is not known until the body text of the page has
been formatted, but the body text can only be formatted once the height available to it is known —
and that height is the page less the note block. It is a fixed point, and the standard answer is to
iterate:

1. Format the page's body into the full content area.
2. Collect the footnotes whose reference marks landed on that page.
3. Format those notes and total their height.
4. If that height is not zero, shrink the content area by it and go back to step 1.
5. Repeat until the set of notes on the page stops changing.

Step 5 terminates because shrinking the area can only move marks off the page, never on, so the note
set is monotonically decreasing. **A bound is still needed** — three passes, say, after which the
last stable arrangement is used — because a note whose own height is what pushes its mark to the
next page can oscillate. That case is real and every typesetter has a rule for it; ours should be to
stop and accept a slightly under-filled page rather than to loop.

This is the single largest piece of work in the spec, and item 3 is where it lives.

## How it was actually done — and why the fixed point was not needed

The argument above is correct about the circularity and wrong about what it costs to break.

The notes go at the **foot** of the page. Content already placed *above* the mark cannot be affected
by reserving room below it — it fits either way. So the reservation does not have to be made before
the page is started; it only has to be made before the element carrying the mark is laid out. At
that moment the notes on the element are known, because they are its own children.

`TopDownFormatter` therefore does one new thing, immediately before formatting each element:

```csharp
area = area.Shorten(ReserveFootnotes(docObj, area));
```

`Area.Shorten` is the other half of the existing `Area.Lower`: `Lower` moves the top and leaves the
bottom, `Shorten` raises the bottom and leaves the top. The element then sees the room that is
really left, and breaks the page where it should.

Nothing has to be undone when an element does not fit. The shortened area is discarded along with
the page, the element is formatted again on the next one, and its notes are registered against that
page instead — `FootnoteRegistry.Place` is keyed by the `Footnote` object, so registering it again
moves it rather than adding it twice. **That is what makes one pass enough.**

The reservation is computed from *every* note on the page rather than from the ones just found, so
the separator band is counted once however many notes there are, and re-formatting charges nothing
twice. It is clamped at zero, because notes only ever join the page currently being filled.

Cost: one `Area` method, one interface, one registry, one `IAreaProvider`, and about ten lines in
`TopDownFormatter`. No iteration, no bound, no oscillation, and no risk of the loop that item 3
warned about.

## Item 1 — `FormattedFootnote`

Model it on `FormattedTextArea`, which is the existing example of "format a `DocumentElements` into
a sub-area and hand back `RenderInfo`s". It is an `IAreaProvider` of about 130 lines that runs its
own `TopDownFormatter`, and a footnote needs the same shape:

```csharp
internal class FormattedFootnote : IAreaProvider
{
    internal void Format(XGraphics gfx);      // runs TopDownFormatter over footnote.Elements
    internal XUnit ContentHeight { get; }     // RenderInfo.GetTotalHeight(GetRenderInfos())
    internal RenderInfo[] GetRenderInfos();
}
```

Two differences from `FormattedTextArea`:

- The width is the column width of the page, not an inherent width — a note is as wide as the text
  it belongs to.
- The note is laid out into a column narrower than the page's by the width of the mark, and shifted
  right by that much when it is drawn. `FormattedTextArea` has no equivalent.

  That gives a **hanging** indent rather than a first-line one: the mark stands in the margin and
  every line of the note lines up under the first. It is how a footnote has looked since long before
  anyone typeset one with a computer, and - more to the point - it is the only arrangement in which
  the mark and the text beside it cannot collide. Measuring the gutter from the mark rather than
  fixing it is what keeps a long mark (`viii`, or a dagger the caller set as the `Reference`) off
  the first word.

  It also avoids mutating the caller's document: setting `FirstLineIndent` on the note's own first
  paragraph would change what they read back out of their own object model.

The `Style` on the `Footnote` and `StyleNames.Footnote` supply the formatting; the resolution order
is the one `Paragraph` already uses (explicit `Format` over `Style` over `Normal`).

## Item 2 — reference marks

`ParagraphRenderer` measures inline elements through `FormatElement` and draws them through
`RenderElement`, and both are switches on `docObj.GetType().Name`. A footnote's mark is an inline
run of text like any other, so:

- `FormatElement`'s `case "Footnote"` replaces the current throw with `FormatFootnote`, which
  measures the mark string in the current font at superscript size and consumes that width — the
  same shape as `FormatPageRefField`, which measures a string whose value is not yet known.
- `RenderElement` gains `case "Footnote": RenderFootnote(...)`, drawing the mark with
  `ParagraphFormat.TextRise` set, or an explicit `XFont` at a reduced size. Which of the two is a
  detail worth settling against how `FormattedText` already does superscripts.

The mark's *text* is either `Footnote.Reference` when the caller set one — a custom mark such as `*`
or `†` — or the generated number from item 4 when they did not.

**The iterator does not descend into a footnote.** `ParagraphIterator` unwraps `FormattedText` and
`Hyperlink` and nothing else, so `Footnote.Elements` is never walked as inline content. That is
already correct and must stay that way: the note's body is block content formatted separately, and a
descent would spill it into the running text.

## Item 3 — area reservation

`FormattedDocument` is the `IAreaProvider` for the body, and `IAreaProvider.GetNextArea` is where
the page's content rectangle is decided. The loop described above belongs in `FormattedDocument
.Format`, around the existing `formatter.FormatOnAreas(gfx, true)` call.

Collecting "the notes whose marks landed on this page" needs the renderer to record, during
formatting, which page each mark fell on. `FieldInfos` already carries per-page state of exactly
this kind for `PageRefField` resolution, and is the natural place to hang it.

`GetFooterArea` already shows how a band is carved off the bottom of a page. The footnote block
sits above the footer and below the body, so `GetNextArea` subtracts the note height from the
content rectangle's height in the same way, and the note block is drawn between the two.

## Item 4 — numbering

A small formatter, taking the ordinal and `Document.FootnoteNumberStyle` and returning the mark
text. `NumberFormatter` already exists in the assembly for list numbering — check whether roman and
alphabetic conversion can be shared rather than written twice.

`FootnoteNumberingRule` decides what the ordinal counts from:

- `RestartContinuous` — one counter for the document, starting at `FootnoteStartingNumber`.
- `RestartSection` — reset when the section changes.
- `RestartPage` — reset per page, which interacts with item 3: a note moved to the next page by
  reformatting changes its own number, so numbering must be assigned *after* the fixed point in
  item 3 settles, not during formatting.

That ordering constraint dissolved along with the fixed point. `FootnoteRegistry` stores the page a
note was placed on and works the mark out **on demand**, so during formatting the answer is
provisional - it names the page currently being filled - and if the element carrying the mark moves,
it is formatted again on the next page and the answer moves with it. Nothing has to be numbered
"after layout settles", because nothing is stored.

Two things worth knowing came out of building it:

- **The default rule is `RestartPage`,** because it is the enum's first value. A caller who sets
  nothing gets notes numbered from one on every page, which is rarely what anybody means. Pinned by
  a test named for it, and said out loud on the demo's page.
- **A note the caller marked themselves is left out of the counting.** Letting a `Reference` advance
  the sequence would make the numbers around it skip for a reason no reader could see.

`FootnoteStartingNumber` defaults to zero, which is the property's unset value rather than a request
to begin at zero; anything below one starts at one.

## Item 5 — the separator

A short horizontal rule above the note block, conventionally about a third of the column width. Word
and LaTeX both draw one and readers expect it. Draw it in the note-block render pass, not as part of
any note.

There is no DOM property for it and none was added — a fixed rule of one third the column width at
hairline weight matches what MigraDoc's own documentation shows. If it later needs to be
configurable, that is a separate change to the DOM with its own DDL serialization.

Built as an 11pt band with the rule 6pt into it, drawn once per page by `FootnoteRenderer` rather
than once per note. Counting it once is what `FormattedDocument.FootnoteBlockHeight` is for.

## Item 6 — notes that do not fit

A note longer than the space available must split, with the remainder carried to the next page,
which is standard typesetting behaviour. `TopDownFormatter` already knows how to break an element
across areas — that is `NeedsEndingOnNextArea` and the `previousFormatInfo` argument to
`Renderer.Format` — so the machinery exists.

The interaction with item 3 is where the care goes: a note that splits contributes only its first
part's height to this page's reservation.

**Deferred, by decision.** A note is never split: it is laid out whole, and the room for it comes
off the page carrying its mark. That is correct for the overwhelming majority of footnotes, which
are one or two lines, and it is much simpler.

What this costs is the pathological case the paragraph above warned about. A note taller than the
whole text area cannot fit on any page, and the reservation for it would shorten every page to
nothing. In practice the formatter still terminates — the reservation is taken off the area the
element is laid out in, and an element that cannot fit an area is placed anyway rather than looped
over — so the symptom is an overfull page rather than a hang. A note that long is a section, not a
footnote.

## Item 7 — `FootnoteLocation.BeneathText`

The enum has two values and item 3 implements one of them. `BeneathText` puts the note immediately
after the text block rather than at the foot of the page, which is a different and simpler layout: no
fixed point is needed, because the note goes wherever the text ended.

**Both are implemented.** They share the whole of the reservation - `BeneathText` has to reserve
exactly as much room, or a full page would overflow - and differ only in where the block is drawn.
`DocumentRenderer.RenderFootnotes` picks the top:

```csharp
XUnit top = content.Y + content.Height - height;          // BottomOfPage
if (document.FootnoteLocation == FootnoteLocation.BeneathText)
{
    XUnit afterText = formattedDocument.BottomOfContentOn(page);
    if (afterText > 0 && afterText < top)
        top = afterText;
}
```

The guard is what makes them agree on a full page: `BeneathText` never pushes the block *lower*
than `BottomOfPage` would, so it cannot run past the bottom margin.

## Item 8 — notes in nested contexts

A footnote can be added to any paragraph, including one inside a table cell, a text frame or a
header. Each of those formats through its own `IAreaProvider` (`FormattedCell`,
`FormattedTextFrame`, `FormattedHeaderFooter`), none of which owns a page.

**Refused, by decision.** `IFootnoteAreaProvider` is implemented by `FormattedDocument` and by
nothing else, and `TopDownFormatter` throws a `NotSupportedException` naming the way out when an
element carries a note and its provider is not one:

> A footnote can only be attached to a paragraph that is laid out on a page. This one is inside a
> table cell, a text frame, a header or footer, or another footnote, none of which owns the page its
> note would have to appear at the foot of. Move the footnote to a paragraph in the section itself,
> or put its text where it stands.

That covers cells, text frames, headers, footers and notes nested inside notes, in one place and by
construction rather than by five separate checks. It is the behaviour this whole spec exists to
produce: the note is not dropped in silence.

A footnote in a header or footer would be refused in any case, decision or no. It has no sensible
meaning - the header is formatted once per position and reused across every page it applies to.

## Item 9 — tests

`PinataLayout.Rendering.Tests` is the home — it covers MigraDoc's own layout and deliberately
rasterizes nothing, which is right for this. The content-stream readers it links out of
`src/PdfPinata.Test/Helpers` are how the assertions get at what was drawn.

Twenty-four of them, all passing. What they pin:

- A one-line note appears at the foot of the page, below the body text and above the footer.
- The body text is shorter on a page carrying notes than on one that is not — the reservation
  actually happened.
- All five `FootnoteNumberStyle` values produce the expected mark.
- All three `FootnoteNumberingRule` values, including that `RestartPage` numbers by final page and
  not by the page a note was first formatted onto.
- A `Reference` set by the caller wins over the generated number.
- Two notes on one page come out in reading order.
- A note whose mark is pushed to the next page by the reservation ends up on that page too, and the
  formatter terminates.
- Item 8's refusal, from a table cell and from a header.
- That the block stays inside the bottom margin, which is the assertion that would catch a
  reservation that under-counts.
- That a page with no note on it draws no separator - the guard on every test that looks for one.

## Item 10 — the demonstration app

A `Footnotes` demo, in the curriculum after `Structure`. `demonstration-app.md` argues that a demo
that is never looked at is a demo that proves nothing, and every defect in
`demonstration-app-coverage.md` was found by drawing a page and looking at it — which is exactly the
check this feature most needed. It found two: the mark drawn over the note's own first word before
the hanging indent existed, and the mark sitting on the note's baseline rather than raised off it.

Five pages: what a footnote is (five notes, one of them two paragraphs and one of them starred with
a `Reference` the caller chose, so the numbers either side of it visibly do not skip), the five
number styles, a short page showing where the block goes, and the numbering rule across a page
break.

`FootnoteLocation` belongs to the document and is read as each page is drawn, so one document cannot
show both values. The demo says what `BeneathText` would change and leaves it at that. It briefly
did show both, by laying the second out in a document of its own and inserting its page — which
worked, and was a page of document-assembly machinery in a demo about footnotes. `Assemble` covers
that subject already and covers it better.

## Deliberately not done

- **Endnotes.** No DOM support and not asked for.
- **`FootnoteLocation` per section.** The property is on `Document` and stays there.
- **A configurable separator.** See item 5.
- **Footnotes referenced more than once.** A DOM `Footnote` is one object in one place; sharing one
  mark between two call sites is not expressible and should not become expressible here.
- **Splitting a long note across a page break.** Item 6, deferred by decision.
- **Notes in a table cell, a text frame, a header or another note.** Item 8, refused by decision.
- **Spacing between notes.** Whatever `StyleNames.Footnote` says, and nothing added on top. A gap a
  caller did not ask for and cannot see in their own styles is a surprise; `SpaceAfter` on that
  style is where it belongs.

## Known limitations — reservation is per element, not per fragment

Raised in review of the pull request that built this, verified against the code, and left standing
because both are the same missing capability rather than a defect in what is here.

`TopDownFormatter` reserves before formatting an element, and identifies the notes to reserve for by
walking that element's own children — `ReserveFootnotes(docObj, area)`, keyed on `docObj`. An
element is the smallest unit it can ask about. Two consequences follow, and neither has a fix that
is not a feature:

- **A paragraph that splits across a page break moves all of its notes to the later page.** When an
  element does not finish on a page, the loop keeps the same `docObj` and formats the remainder on
  the next one, which reserves for it again — and `FootnoteRegistry.Place` moves every note it is
  given to the page currently being filled. A mark that stayed behind in the first fragment
  therefore ends up on one page with its note on the next. Deciding otherwise means knowing which
  marks fall in the fragment just laid out, which is a range `ParagraphFormatInfo` holds and the
  reservation protocol does not currently ask for.

- **`KeepWithNext` probes do not see the following element's notes.** `NextElementsDontFit` formats
  the elements after this one to decide whether they fit, and does that without the footnote-aware
  area, because reservation is a side effect on the registry and a probe must not leave one. So a
  probe can keep a paragraph on a page that the real pass then has to move. Fixing it means
  separating a speculative reservation from a placed one.

Both want the same thing: a reservation that can be asked for a fragment and taken back. Building
that is a spec of its own, and it should be one piece of work rather than two, because the second
falls out of the first almost for free. Neither affects a document whose footnoted paragraphs fit
on the page they start on, which is what the tests in item 9 cover.

## What building it changed outside the footnote files

Small, and worth knowing about:

- **`Area.Shorten`** — new abstract member, implemented by `Rectangle` and `ObstructedArea`. The
  counterpart of `Area.Lower`. Nothing else calls it yet.
- **`TopDownFormatter`** — one line in the formatting loop and one small method beside it. That
  method is where item 8's refusal lives, so the loop itself gained no branch for footnotes.
- **`ParagraphRenderer`** — a `case "Footnote"` in each of `FormatElement` and `RenderElement`,
  replacing the throw that item 14 of `demonstration-app-coverage.md` put there, and three small
  members to measure and place the mark.
- **`DocumentRenderer`** — a `FootnoteRegistry`, reset in `PrepareDocument`, and a
  `RenderFootnotes` call at the end of `RenderPage`'s content branch.

No public API was added anywhere. Every type this work introduced is `internal`, and every property
a caller needs was already shipped on `Document` and `Footnote`.

## Relationship to the barcode gap

`Renderer.cs` throws for `Barcode` in the same way and for the same reason, and that gap is *not*
covered by this spec. The two are unrelated in everything but symptom: a MigraDoc barcode needs a
shape renderer mapping the DOM `Barcode` onto `PdfPinata.Drawing.BarCodes`, which is a day's work
with no layout problem in it at all, where footnotes are a layout problem and almost nothing else.
