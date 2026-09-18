# Spec — tagged PDF and PDF/UA accessible output

What accessible output covers, and what it deliberately leaves out.
Gap **G2** of the competitive gap analysis. **All three stages are built.**

| item | what | stage | status |
|---|---|---|---|
| 1 | `PdfPinata.Pdf.Structure` — structure tree, role map, parent tree | A | done |
| 2 | `XGraphics.BeginMarkedContent` / `BeginArtifact`, and `BDC`/`EMC` emission | A | done |
| 3 | Catalog `/MarkInfo`, `/Lang` | A | done |
| 3b | `/ViewerPreferences /DisplayDocTitle`, and requiring a title | A | done, on a PDF/UA claim |
| 4 | MigraDoc tags its own output — headings, tables, lists, figures, links | B | done, **and it is the default** |
| 5 | `Image.AlternativeText`, `Table.Summary` on the DOM | B | done, **on `Shape`, not `Image`** |
| 6 | PDF/UA-1 identifier in XMP, `/Tabs /S`, a pre-save validator | C | done |
| 6b | `/ActualText` at a hyphenation break | C | done |
| 6c | `/ActualText` for ligatures | C | done |
| 7 | veraPDF in CI | C | done, **and it gates** |
| 8 | Heading levels may not skip | C | done |
| 9 | A note's `/ID` may be chosen | C | done, `Footnote.Identifier` |
| 10 | No two MCIDs nested one inside the other | C | done |

Covered by `PdfPinata.Test/IO/TaggedPdfTests.cs` for Stage A, and
`PinataLayout.Rendering.Tests/TaggedOutputTests.cs` and `PdfUaConformanceTests.cs` for B and C.

```csharp
// Stage B: nothing asked for, and the document comes out described.
var renderer = new PdfDocumentRenderer(true) { Document = document, Language = "en-GB" };
renderer.RenderDocument();
renderer.PdfDocument.Info.Title = "Statement of account";
renderer.PdfDocument.Options.UAConformance = PdfUAConformance.PdfUA1;   // and be held to it
renderer.Save("statement.pdf");
```

```csharp
// Stage A, still there for a page drawn by hand.
using (gfx.BeginMarkedContent(PdfTag.H1))
    gfx.DrawString("Invoice", headingFont, XBrushes.Black, 40, 60);

using (gfx.BeginArtifact())                       // running heads, folios, rules
    gfx.DrawString("Page 3 of 9", smallFont, XBrushes.Gray, 500, 800);
```

For a page drawn by hand, asking for `PdfDocument.Structure` is what makes a document tagged, and one
that never does is written exactly as it was before — no tree, no `/MarkInfo`, and not one extra byte,
which the first test pins. **For MigraDoc that is now the other way round**: see the break below.

## What Stage A settled that the proposal had not

**A container element holds both its own marks and its child elements.** Opening a scope inside
another gives the outer element a `/K` of `[mcid, childRef]` rather than `[childRef]`. That is
correct and it is what the standard intends: the identifier covers anything drawn directly in the
section and outside the heading. It is worth knowing before reading a `/K` array, because the first
entry is often an integer and code that assumes a dictionary there finds null.

**The marked-content scope is a `using`, and that is load-bearing rather than stylistic.** An
unbalanced `BDC` corrupts every mark after it on the page, so an early return or a throw mid-paragraph
must not be able to leave one open. There is a test that throws inside a scope and then asserts the
`BDC` and `EMC` counts still match.

**`BDC` is always emitted in graphic mode.** A marked-content sequence opened between `BT` and `ET`
nests inside the text object instead of containing it. `BeginGraphicMode` is called first, always.

## What Stages B and C settled that the proposal had not

**Tagging MigraDoc is on by default, and `PdfPage.Resize` is the cost.** The proposal called the
default flip "the break worth taking" and did not say what it breaks. `PdfDocumentRenderer.TagContent`
defaults to `true`, so every document rendered through MigraDoc now carries a structure tree — and
`PdfPinata/Pdf.Advanced/PdfPageResizer.cs` refuses a tagged document outright, because resizing
moves a page's content into a form XObject and leaves every identifier in the tree pointing at content
that is no longer where the tree says it is. That refusal used to be an edge case for files other
people made. It is now the common path, and code that rendered a MigraDoc document and then resized
its pages has to set `TagContent = false`. Making the tree survive a resize is the right answer and it
is not this piece of work; refusing loudly is better than breaking invisibly, which is what
`page-resize.md` already says and now means far more often.

**An artifact is not a container its contents can opt out of.** A running head is drawn by the same
paragraph renderer as body text, and that renderer tags what it draws — so the first working version
put the header's paragraphs in the tree as paragraphs, inside an `/Artifact` sequence saying they were
not content. A reader following the tree would have read the running head aloud on every page: the
exact failure the artifact scope exists to prevent, produced by the code that opens it. So the tagger
counts artifact depth and refuses to build any element at all while it is inside one. It is the single
thing most worth knowing before adding a renderer to this.

**A refused scope is the trap the artifact rule sets.** `Tagger.Block` and `Tagger.Container` hand
back `StructureTagger.Nothing` and push nothing when tagging is refused — which, per the rule above,
is what happens inside an artifact. `Tagger.Parent` (named `Current` at the time this was found) then
still names whatever was current *before* the artifact opened, because `Artifact` deliberately does
not change the current element. So a renderer that opened a scope and then read that property to find
"the element I just opened" got an unrelated one, and wrote its metadata onto that: an image with
alternate text in a running head put "The company logo." onto the body of the page, and a table with a
summary in a header put its `/Summary` on an enclosing element. Three renderers did this. The fix is
to stop inferring the element: `Block` and `Container` have overloads handing it back, `null` when
nothing was opened, and `DescribeCell`/`DescribeTable` take it as a parameter. **A renderer with
something to write onto an element must never read `Parent` for it** — see
`docs/specs/structure-tagger-interface.md` for the rename that made the two questions two names.

**`CanTag` has to ask whether a page has been begun.** `_document` is assigned by `BeginPage`, and
everything the tagger builds is built against it — but nothing obliges a caller to begin a page.
`DocumentRenderer.RenderObject` draws one object onto a surface the caller owns and never does. On
that path tagging looked possible (enabled, and a real page to mark), so a list paragraph reached
`_document.Structure.CreateElement` with `_document` still null and threw `NullReferenceException`
at the caller. `Element` had its own null check and the other two entry points did not, which is the
shape of the mistake: the condition belongs in the one test they all ask, not in whichever of them
somebody remembered. `BeginPage` keeps its own test, since it is the method that begins the page.

**An element belongs to a document object, not to a render pass.** A paragraph broken over a page
boundary is drawn by two renderers, and a table's heading row is drawn again at the top of every page
the table continues onto. Keyed by the DOM object and reused, those come out as one paragraph and one
heading row; built per pass they would come out as two paragraphs and five heading rows, and a reader
would announce them as such. That is why `StructureTagger` is a class with a dictionary in it rather
than a few calls scattered through the renderers.

**That forced a fix in Stage A's plumbing.** An element pointing at marks on two pages cannot use a
bare integer in `/K`, which is read against the element's own `/Pg` and so can only ever mean one page.
`PdfStructureElement.AddMarkedContent` now switches to a marked-content reference — `<</Type /MCR /Pg …
/MCID …>>` — the moment a second page turns up. Stage A never hit this because a hand-tagged scope
opens and closes on one page; nothing automatic can avoid it.

**Structural nesting is not drawing nesting.** A table is rows and cells; it is drawn as shading, then
content, then borders, cell by cell out of a flat list. So the tree is built by naming a parent rather
than by nesting `using` blocks, which is why `XGraphics` grew
`BeginMarkedContent(PdfStructureElement)` beside the overload that takes a tag.

**An empty artifact scope is worth taking back.** Automatic tagging wraps the decoration of every
paragraph — its shading and its borders — in an artifact scope, and the overwhelming majority of
paragraphs have neither. An empty `/Artifact BMC EMC` pair per paragraph would have been the largest
single thing tagging added to a document and would have meant nothing, so
`XGraphicsPdfRenderer.EndArtifact` rewinds the content when nothing at all was written between the
ends. The test is exact — if even the `ET` that closes a text object was appended, the pair stands —
and it is deliberately not done for a structural scope, whose identifier is already in the tree.

**Tagging moves the operands and not the glyphs.** A `BDC` is always written in graphic mode, so
tagging ends the text object before each scope and starts a new one after it, and every `Td` in a
fresh text object is measured from the origin instead of from the line before. The layout pin in
`PdfPinata.Test/Rendering/MigraDocLayoutPinTests.cs` therefore does two things now: it renders the
corpus untagged and demands the historical bytes exactly, and it renders it tagged and demands the
same glyph runs in the same order on the same pages. Re-capturing the baseline with marks in it would
have recorded whatever the new code did and called it correct.

**The alternate text decides whether an image is tagged at all**, rather than being an optional extra
on a figure that is tagged regardless. Described, an image is a `/Figure`; undescribed, it is drawn as
an artifact. That is the right way round: an undescribed figure announces to a reader that something is
there and then cannot say what, which leaves them knowing only that they have missed something, whereas
decoration honestly marked as decoration is passed over in silence — and for the rule above a
letterhead that is also the truth. Nothing guesses at a description.

**A broken word is one `/Span`, not one hyphen marked as noise.** The page says "demon-" and
"strate" and the word is neither. There are two ways to say so, and the difference matters. Marking
just the hyphen with an empty `/ActualText` says "ignore this glyph" and leaves the two fragments to
be rejoined by whatever a reader does at a line break — which is usually to insert a space, so the
word comes back as "demon strate". Putting the whole word on a `/Span` covering both fragments says
what the word is, and nothing has to be inferred. ISO 32000-1 describes exactly this case, so the
second is also the endorsed reading.

That forces the replacement onto the structure element rather than onto the marked-content sequence.
The fragments are separated by a line break and sometimes by a page break, so there is no one sequence
to carry it, and putting the word on each of two sequences would say it twice. The consequence is that
a reader has to walk the tree to see it — a tool that only reads BDC property lists will not — and for
an accessibility feature that is the right trade.

**The page-break case is the one that needed real work, and it is not exotic.** A paragraph split at a
hyphen has the hyphen drawn by one renderer on one page and the rest of the word drawn by another
renderer on the next. The second renderer has no line ending in a soft hyphen and would never learn
that its first leaves finish a word begun elsewhere, so it left the tail outside the span — and a span
saying "demonstrate" with the tail still outside it extracts as "demonstratestrate", which is worse
than doing nothing at all. `FindBrokenWords` therefore looks in two places: at every line's end, and
before the first line's start. `AWordBrokenAcrossAPageIsStillOneWord` fails with one mark instead of
two if that second look is removed.

**A footnote is tagged where it is cited, not where it is drawn.** A footnote reaches the page from two
renderers: `ParagraphRenderer` draws the raised mark in the middle of a line, and `FootnoteRenderer`
draws the note itself in a band at the foot of the page. Where the `/Note` is created decides where a
screen reader hears it, and drawing order would put every note after all the body text of the page,
severed from the sentence that cited it — which is the reading order a structure tree exists to
correct. So the element is built at the citation, as a sibling of the `/Reference` and a child of the
paragraph, and `FootnoteRenderer` asks the tagger for the same element later and fills it in.

Both are inline-level structure types, which is exactly where ISO 32000-1 puts them: `/Reference` is
the pointer, `/Note` is what it points at. Three elements are keyed by one `Footnote` — the reference,
the note, and the `/Lbl` for the note's own mark in the gutter — which is what the tagger's slots are
for, and the key has to be the DOM object because a paragraph split across a page break is drawn by a
renderer built afresh per page.

**The `/Note` holds no marks of its own.** It is entered rather than marked: its content is drawn by
paragraph renderers that mark what they draw, and a `/Note` carrying marks directly as well would
claim that some of the page belongs to the note rather than to the label and paragraphs inside it.
Two marks are its own — the `/Lbl` in the gutter — and the separator rule above the block is an
artifact, because it says where the body text stops and carries nothing to read out.

**A note carries an `/ID`, and PDF/UA is held to it.** ISO 14289-1 7.9 requires one of every `/Note`,
and the reason is the shape of the feature: a note exists to be pointed at from the mark that cited
it, and an element with no identifier cannot be pointed at. That needed machinery the codebase did not
have — `PdfStructureElement.Id`, and the structure tree root's `/IDTree`, which ISO 32000-1 requires
the moment any element carries an identifier. The index is built by walking the tree at save time
rather than by recording identifiers as they are handed out, so that one a caller set themselves is
indexed exactly like one the tagger generated; a registration call beside the property is a call
somebody forgets, and forgetting it writes an element nothing can look up. Two elements under one
identifier is refused rather than resolved, because which of them a reader should land on is a
question about the document.

**A ligature says what it stands for, in a sequence inside the text object.** `ShapedGlyph.Cluster`
says which characters a glyph came from, and `/ToUnicode` has always spent that on a text extractor.
`/Span <</ActualText …>> BDC … EMC` around the glyph is the other half, for everything that reads
marked content instead — which is what assistive technology reads. Both are answered from
`TextShaping.CharactersOf`, one implementation, because two would eventually disagree and the document
would then say one thing to an extractor and another to a screen reader.

That sequence deliberately stays **inside** the text object, which is the opposite of what
`BeginMarkedContent` does. A structural sequence carries an `/MCID` and has to be able to contain a
whole text object, so it is written in graphic mode; this one carries no identifier, is not a structure
element, and has to wrap one show-text operator because what it claims is true of one glyph. Ending the
text object around each ligature would also send the pen back to the origin, which is the trap the
tagging path already documents.

**A run with no ligature in it is written byte for byte as before**, which is every run of every
document produced before there was a shaper and nearly every run since. That guard is what kept the
existing goldens and the untagged layout pin where they were.

**Fewer glyphs than characters is what makes a ligature**, and counting only the characters is not the
same test. The commonest cluster of more than one character is a base and the marks attached to it:
Devanagari `कि` and an Arabic letter carrying a vowel sign are each two characters drawn with two
glyphs, nothing swallowed anything, and `/ToUnicode` already says what each glyph stands for. Asked of
the characters alone, every syllable of a page of Hindi is a ligature — a marked-content sequence and
its own show-text operator each, where the run used to be a single `Tj`. So the cluster's glyphs are
counted against its characters, and whole clusters are stepped over rather than single glyphs, so that
what comes back is always a cluster's first glyph: reported from the middle of one, the span would
cover a ligature's tail and leave its head outside, saying of some of the glyphs what is only true of
all of them together.

**A joining control is not a ligature**, and this is the case that bites. U+200C and U+200D are zero
width by definition and `TextShaping.Unshaped` draws no glyph for either, so a cluster spanning a
letter and a joining control is one letter that was told how to join — not a pair that became one
glyph. Counting it wrapped most of a word of Arabic in a sequence claiming a ligature that was not
there, and cut a run that had been one show-text operator since before there was a shaper. So the
controls are removed before the count is taken and before the text is written: a reader told that a
glyph spells "letter, zero-width joiner" is being told about a character nothing on the page stands
for. It is also the one place `/ActualText` and `/ToUnicode` part company on purpose: extraction keeps
the joiner, because a joiner that was in the source belongs in the copy, and `/ActualText` drops it,
because it describes what is on the page. Two answers to different questions, not two implementations
drifting apart — `TextShaping.CharactersOf` still answers both and the divergence is recorded on it.

**A defect fell out of this.** `CLexer.ScanDictionary` ended a dictionary at the first `>` it saw,
assuming the next character was the second of a `>>`. That holds for `<</MCID 0>>` and fails for
`<</ActualText <FEFF0066>>>`, where the hex string's own `>` comes first — so the rest of the
dictionary was read as operators and the stray `>` stopped the whole content stream. This library
could not read the content it had just written. It now balances the nesting and steps over the hex
strings, literal strings and comments a dictionary may hold — a comment is legal wherever whitespace
is, and a `>>` inside one closes nothing — and `CLexerTests` pins each of those and the plain case,
which every tagged page is full of.

**`AlternativeText` went on `Shape`, not on `Image`.** A chart needs one for the same reason and to the
same effect: to a reader who cannot see it, axis labels read out in drawing order say nothing about the
shape they describe. `TextFrame` inherits it and ignores it, because a text frame holds paragraphs and
tables that describe themselves.

## Still to do

- **Reading `/ActualText` back.** `PdfPinata.Pdf.Extraction.PdfTextExtractor` ignores marked
  content entirely, so this library still extracts its own hyphenated word as two fragments — and now
  its own ligature spans as well, though `/ToUnicode` covers the ligature case for it and nothing
  covers the hyphenated one. Honouring the tree means resolving the page's `/StructParents` through
  the parent tree to reach the element, and it is the natural next piece of
  `docs/specs/text-extraction.md` rather than of this — tagged extraction is listed there as the
  reason to have an extractor here at all.
- **veraPDF is in CI and it gates** — `docs/specs/verapdf-validation.md`. The tagged corpus document
  passes **all 106 of its PDF/UA-1 rules**. The one rule it first failed was a missing `/CIDToGIDMap`
  on a Type 2 CIDFont — a font dictionary rather than anything structural, and it failed the archival
  documents too; `PdfCIDFont.PrepareForSave` now writes `/Identity` and `CidFontConformanceTests` pins
  it. Nothing about the structure tree ever failed, which includes the `/Lbl` and body paragraphs
  nested inside an inline-level `/Note` that the footnote work produces: veraPDF does not object to
  it. That is the outside opinion the tagging was missing, and a failure is now a regression rather
  than a backlog. It **also warned `Nested MCID` four times** without failing a rule; that has since
  been looked into and fixed — see below — and the corpus now validates with no warnings at all.
- **Nested lists.** MigraDoc has no list object — a list is however many consecutive paragraphs happen
  to carry a `ListInfo`, so the tagger reads a run of one kind as one `/L` and a change of kind as a
  new one. That matches what the page looks like and cannot see a nested list as nested, because
  nothing in the DOM says it is. **Since built**: `ListInfo.NestingLevel` says how deep an item is, and
  the tagger nests a deeper item's list inside the previous item's `/LBody`. See
  [nested-lists.md](nested-lists.md).
- **A link in a running header** is not reachable from the tree, because the header is an artifact and
  nothing inside one is tagged. `PdfUaValidator` reports it rather than hiding it, naming the page.
  **This is where it stays.** The two rules genuinely conflict, and refusing is better than writing a
  document that claims PDF/UA and quietly contains a link only a reader hit-testing rectangles can
  find. Whoever put it in the header is the only one who can decide whether it belongs there.
  `HeaderLinkTests` pins the refusal, and that the same link in the body is fine.

---

## Heading levels, and why the walk is its own

ISO 14289-1 7.4.2 wants heading levels to descend one at a time. They are the outline a reader lets
somebody jump around by, so `/H1` followed by `/H3` leaves a hole in it: a section three levels deep
with nothing two levels deep containing it. A document claiming PDF/UA-1 is refused for one.

**Coming back up any distance is not a skip.** `/H3` to `/H1` closes two sections rather than
inventing one, and a rule written as "the level may not change by more than one" would refuse it
wrongly. A document *opening* at `/H2` is a skip, because there is nothing before it.

The rule needs the tree in reading order, and the walk the other rules share does not give it: it
pushes each element's kids onto a stack, so siblings come back off it backwards. That costs the other
rules nothing, because none of them cares what came before, so the ordered walk is this rule's own
rather than a change to theirs.

From MigraDoc the level is `ParagraphFormat.OutlineLevel`, which a heading style sets — so the
message names it, because it is nearly always the styles that need fixing rather than anything that
draws. The mistake is reaching for `Heading3` because of how it looks.

## A note's identifier

`Footnote.Identifier` on the DOM, beside `Table.Summary` and `Shape.AlternativeText`, and on the DOM
for the same reason they are: it is something only the author knows.

Left unset the renderer generates one — `note1`, `note2`, in citation order — which is what nearly
every document wants and stays the default. It is the wrong answer when the identifier has to mean
something outside the document, because something else refers to it, and until this there was no way
to say so: nothing exposed the structure element to the caller.

**The counter advances whether or not the name came from it**, so naming one note does not renumber
the notes around it. Two notes under one name are refused as they always were, including when a
caller reaches for `note2` and the second note is about to be given it — the prefix makes that
collision visible rather than preventing it.

## Marked content is suspended, never nested

A marked-content sequence carrying an MCID is a content item of **exactly one** structure element.
Nest two and the inner glyphs belong to both: a footnote mark inside the paragraph citing it was
claimed by the `/Reference` and by the `/P`, a hyperlink by the `/Link` and by the `/P`, a list label
by the `/Lbl` and by the `/LBody`. Nothing in the file says which should read them. veraPDF warned
`Nested MCID` about exactly this, four times on the corpus document, without failing a rule.

The open sequence is now closed before a nested one opens and reopened after it. That works because
an element may own as many content items as it likes — already how a paragraph broken over two pages
is one paragraph and not two — so the text before a link and the text after it are two items of one
element. **Resuming is the load-bearing half**: without it everything the paragraph drew after its
link would be outside the structure tree, which is a failure rather than a warning.

A sequence resumed and then closed with nothing drawn in it is taken back rather than written empty,
and its identifier is given back with it, or the tree names marks the content stream does not hold.
Two things there are easy to get wrong and both were got wrong first:

- **The identifier is checked, not the last kid dropped.** Kids hold child elements as well as
  content items, and a note that has since acquired a `/Lbl` and a `/P` loses them instead.
- **Only a resumed sequence may be taken back.** One the caller opened is their statement that
  something on the page belongs to that element, and dropping it silently is how a scope that never
  opened comes to look like a balanced one — which is what
  `AMarkedContentSequenceIsClosedEvenWhenTheDrawingThrows` exists to catch.

---

## The defect

The library cannot say what anything on a page *is*. A heading and a caption are both a `Tj` in a
content stream, and a screen reader has nothing to go on but their position.

The names are in the source and nothing stands behind them:

- `Pdf.Advanced/PdfCatalog.cs` declares `StructTreeRoot`, `MarkInfo` and `OutputIntent` as key-name
  constants. Nothing constructs any of them.
- `Pdf.Content.Objects/Operators.cs` has `BDC`, `BMC` and `EMC` in the operator table — because the
  content-stream **reader** must cope with files that contain them. `Drawing.Pdf/XGraphicsPdfRenderer.cs`
  never emits one.
- No MCID, no `/StructParents`, no `/Alt`, no `/Lang`, no role map.

Meanwhile `docs/specs/page-resize.md` already lists "tagged documents are refused" as a guard — the
codebase knows tagged PDFs exist and treats them as something other people make.

## Why this is worth eight weeks

Two reasons, and only one of them is competitive.

**Regulation is pulling in one direction, and it is worth being exact about how far.** The European
Accessibility Act (Directive (EU) 2019/882) took effect on 28 June 2025, with the transition window
for existing products closing 28 June 2030. It is harmonised through EN 301 549, which incorporates
WCAG 2.1 AA; for documents that means tagged PDFs.

It does **not** follow that every PDF anyone generates is now covered. The EAA applies to the
products and services it lists — among them e-commerce, e-books, banking and transport services — and
to the documentation that goes with them, rather than to invoices, contracts and reports at large.
The broader driver for those is public-sector procurement: EN 301 549 is what EU public bodies buy
against under Directive (EU) 2016/2102, and national implementations are what decide any given case.

The conclusion for this library is the same either way and needs no exaggeration to stand up: a .NET
PDF library that cannot emit a tagged PDF is unusable for a customer whose own obligation is any of
the above, and cannot be adopted by anyone who might later acquire one.

**Everyone else already ships it.** Not just the paid libraries — *PDFKit*, a free JavaScript library,
advertises "marked content, logical structure, Tagged PDF, PDF/UA". QuestPDF ships it. This is table
stakes, not a differentiator, and the differentiator is item 4: making it **automatic**.

---

## Stage A — the plumbing and a manual API

### The structure tree

A parallel tree beside the page tree, describing the document as a document rather than as marks:

```text
Catalog
 ├─ /MarkInfo <</Marked true>>
 ├─ /Lang (en-GB)
 └─ /StructTreeRoot ──► /Type /StructTreeRoot
                         ├─ /RoleMap  <</Caption /P>>        ← custom → standard
                         ├─ /ParentTree ──► number tree      ← MCID → StructElem
                         └─ /K [ ──► /StructElem /S /Document
                                       ├─ /StructElem /S /H1 ──► /K [ 0 ]        ← MCID 0
                                       ├─ /StructElem /S /P  ──► /K [ 1, 2 ]
                                       └─ /StructElem /S /Figure /Alt (…) ──► …
                                    ]
```

Each leaf points at a **marked-content identifier** — an integer — and the page's content stream wraps
the corresponding marks in `BDC`/`EMC` carrying the same number. The `/ParentTree` is the reverse
index, letting a reader go from a mark back to its meaning. It is a number tree, and
`Pdf.Advanced/PdfNumberTreeNode.cs` **already exists** — a real head start on the fiddliest part.

New namespace `PdfPinata.Pdf.Structure`: `PdfStructureTreeRoot`, `PdfStructElement`,
`PdfMarkedContentReference`, `PdfObjectReference` (for annotations, which are structure content but not
marks), and a `PdfTag` enumeration of the standard structure types.

### The drawing API

```csharp
using (gfx.BeginMarkedContent(PdfTag.H1))
    gfx.DrawString("Invoice", headingFont, XBrushes.Black, 40, 60);

using (gfx.BeginArtifact(ArtifactType.Pagination))   // running heads, folios, rules
    gfx.DrawString("Page 3 of 9", smallFont, XBrushes.Grey, 500, 800);
```

Two operations, and the second matters as much as the first. **Everything on the page is either
content or an artifact.** A page number that is neither is a PDF/UA failure, so the artifact scope is
not a convenience — it is half the rule.

`XGraphicsPdfRenderer` emits `/H1 <</MCID 0>> BDC … EMC`, allocates MCIDs per page, and writes
`/StructParents` on the page. The scopes nest, so the renderer needs a stack, and it must interact
correctly with the graphics-state stack that `Drawing/GraphicsStateStack.cs` already keeps — a `q`/`Q`
pair may not straddle a `BDC`/`EMC` pair.

### The document-level keys

`/MarkInfo <</Marked true>>`, `/Lang` on the catalog and overridable per structure element,
`/ViewerPreferences <</DisplayDocTitle true>>`, and a document title in the info dictionary. PDF/UA
requires all four, and the last two are the ones everybody forgets.

---

## Stage B — MigraDoc tags its own output

This is where the value is. Hand-tagging is a feature; **not having to** is the product.

`PinataLayout.Rendering` knows the semantics already — it is rendering a `Paragraph` with a `Heading1`
style, it just throws that away on the way to the page. The mapping:

The mapping, as built:

| DOM | structure type | notes |
|---|---|---|
| `Section` | `/Sect` | one per section, under a single `/Document` |
| `Paragraph` | `/P` | `/H1`…`/H6` from `Format.OutlineLevel`, which is what a heading style sets; MigraDoc's levels 7–9 land on `/H6` |
| `Table` | `/Table` → `/TR` → `/TH` \| `/TD` | heading rows become `/TH` with `/Scope /Column`; a merged cell gets `/ColSpan` and `/RowSpan`; `Table.Summary` becomes `/Summary` |
| `ListInfo` | `/L` → `/LI` → `/Lbl` + `/LBody` | the bullet or number is the `/Lbl`; a run of consecutive paragraphs of one list type is one `/L` |
| `Image` | `/Figure` with `/Alt`, or **artifact** | which one is decided by `Shape.AlternativeText` |
| `Chart` | `/Figure` with `/Alt`, or **artifact** | the same, and for the same reason |
| `Hyperlink` | `/Link` with an `/OBJR` per annotation | one element however many lines the text runs over; the annotation also gets `/Contents` |
| a word broken at a soft hyphen | `/Span` with `/ActualText` | one element over both fragments and the hyphen between them, across a page break if that is where it falls |
| `TextFrame` | `/Sect` | it holds paragraphs and tables that describe themselves |
| `HeaderFooter` | **artifact** | never content, and nothing inside one is tagged either |
| Cell borders and shading, paragraph borders and shading, shape fills and outlines | **artifact** | decoration |
| `Footnote` | `/Reference` + `/Note` → `/Lbl` + `/P` | both inside the paragraph that cited it, so the note reads where it is cited rather than where it is drawn; the `/Note` carries the `/ID` PDF/UA requires, and the separator rule above the block is an artifact |
| a glyph standing for several characters | `/Span` with `/ActualText`, in the content stream | a marked-content sequence inside the text object rather than a structure element, because it is true of one glyph; written only where a cluster's glyphs are fewer than its characters, so a base and the marks on it are left alone; joining controls are not counted and not written |

`Row.HeadingFormat` means the header/body distinction was already modelled, which is lucky — table
tagging is otherwise the hardest part, because `/TH` scope and the header/data association is what
actually makes a table navigable.

Item 5 adds two DOM properties. `Table.Summary` is where the proposal put it; `AlternativeText` went on
`Shape` rather than on `Image`, so that a chart gets one too. Both are generated properties, so the
source generator under `PinataLayout.DocumentObjectModel.Generators` carries the cost.

**The break, taken:** `PdfDocumentRenderer.TagContent` and `DocumentRenderer.TagContent` default to
`true`. An untagged document is the thing you ask for. What that costs is set out under "What Stages B
and C settled" above — chiefly that `PdfPage.Resize` refuses a tagged document, and MigraDoc output now
is one.

---

## Stage C — PDF/UA conformance

Tagging a document and *conforming* are not the same, and the gap is mostly rules that are cheap to
check and easy to violate. `PdfDocumentOptions.UAConformance = PdfUAConformance.PdfUA1` is the claim,
and it is enforced rather than stamped on.

Two of these the writer settles rather than demands, because there is only one right answer and
refusing over it would teach nobody anything: `/ViewerPreferences <</DisplayDocTitle true>>` follows
from the claim, and `/Tabs /S` follows from a page being tagged at all — so the second is written for
every tagged page whether or not any claim is made.

Everything else `PdfUaValidator` throws over, naming the rule, before a byte is written:

| rule | checked |
|---|---|
| The document is tagged | yes |
| A title, in the information dictionary | yes |
| `/ViewerPreferences /DisplayDocTitle` | yes — and set, so it only fires on a hand-built document |
| A natural language on the catalog | yes |
| Every page in the tree, with `/StructParents` | yes |
| `/Tabs /S` on every page | yes — and set, as above |
| Every `/Figure` has `/Alt` or `/ActualText` | yes |
| Every `/Note` has an `/ID` | yes |
| No two elements share an `/ID` | yes — the builder refuses it too, while assembling the `/IDTree`, and runs first; this is so a caller can find out by asking rather than by saving |
| Every link annotation has `/Contents` | yes |
| Every link annotation reachable from the tree | yes |
| No content outside the structure tree | **no** — needs a content-stream pass |
| No structure element with no content | **no** |
| Headings do not skip a level | **no** |
| `/ActualText` where the marks and the text disagree | **no** — written at every hyphenation break and around every ligature, but nothing checks that one is missing |
| Fonts embedded, every glyph reachable through `/ToUnicode` | not checked, and true of everything this library writes |

The PDF/UA-1 identifier goes into the XMP packet that `docs/specs/pdf-a-conformance.md` built —
`pdfuaid:part 1`, and no `pdfuaid:conformance`, because UA-1 has parts and no levels where PDF/A has
both. It is the only place a PDF/UA claim can be made: unlike PDF/A there is no dictionary entry for
it, so a document with a perfect tree and no identifier claims nothing at all. The two claims are
independent and a document may carry both.

A validator that runs after the file is written tells somebody who is no longer in a position to do
anything about it, which is why this one runs during `PrepareForSave`. It is also public, so a caller
may ask the question at a moment of their own choosing.

---

## What this deliberately does not cover

- **PDF/UA-2 (ISO 14289-2:2024).** Worth tracking; UA-1 is what tools validate against today.
- **Automatic alt text.** If the caller does not supply `AlternativeText`, the image is an artifact or
  the build fails — it is not the library's business to invent a description.
- **Retro-tagging an imported page.** Untagged content coming in through `XPdfForm` or page import
  stays untagged, and a document mixing the two is honestly reported as non-conforming rather than
  quietly labelled.
- **Reading order different from drawing order.** The structure tree defines reading order, so this
  falls out for MigraDoc. For hand-drawn pages the caller controls it by scope order and is on their
  own.
- **Making the structure tree survive `PdfPage.Resize`.** The proposal guessed that it would survive
  untouched, because the marks move into the form XObject along with the content. That guess is wrong
  in the way that matters: the identifiers move with them, but the page's `/StructParents` indexes the
  *page's* marks, and marks inside a form XObject are indexed by the form's own `/StructParents`
  instead. So the mapping has to be rebuilt, not merely preserved. `PdfPageResizer` goes on refusing,
  which is now the common path rather than an edge case — see above.

## Tests

`PinataLayout.Rendering.Tests` is the right home, as the proposal said: it covers MigraDoc's own
layout, links the content-stream readers out of `PdfPinata.Test/Helpers`, and **deliberately
rasterizes nothing**, so it needs neither Ghostscript nor ImageMagick. Structure assertions are exactly
that shape — save, reopen, walk `/StructTreeRoot`, assert the tree — and `Helpers/Structure.cs` is what
turns a walk of `/K` into something a test can say a sentence about.

Two of them are worth pointing at. `ARunningHeadIsFurnitureAndNotSomethingToReadOut` counts the whole
tree rather than looking for the header in it, because the bug it caught was the header being present
and correct-looking. `TaggingDrawsTheSameTextInTheSameOrder`, over in `PdfPinata.Test`, is the other
half of the layout pin: see above for why it compares glyph runs and not bytes.

Stage C's outside opinion is now **veraPDF as a container step**, run by `verapdf-check.ps1` in CI and
on a developer's machine alike — `docs/specs/verapdf-validation.md`. The Java dependency it was going
to cost turned out to cost nothing beyond Docker, which is what running it in a container was for.

## Related

- `docs/specs/cross-reference-streams.md` — land it first; tagging multiplies the object count.
- `docs/specs/pdf-a-conformance.md` — builds the XMP writer Stage C needs.
- Optional content groups become near-free once the marked-content machinery here exists.
