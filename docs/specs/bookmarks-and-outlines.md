# Spec — bookmarks and outlines, issue #321

[ststeiger/PdfPinata#321](https://github.com/ststeiger/PdfSharpCore/issues/321) reports that a
bookmark "doesn't show in the rendered PDF, nor is there an apparent means to reference a bookmark
(i.e., from a table of contents)", and calls it "a showstopper for using this library".

Neither claim held up. What the investigation found instead was one silent no-op and two
destinations that named a page and nothing more.

| item | what | status |
|---|---|---|
| 1 | An outline entry lands on the page, not on the heading | done |
| 2 | A local hyperlink lands on the page, not on the bookmark | done |
| 3 | A bookmark put on a section is dropped without a word | done |
| 4 | Nothing documents how outlines and tables of contents are built | done |
| 5 | `PdfOutline.Opened` is accepted and never written | done, later |

---

## What was actually wrong

The reporter's two claims are both false, for one understandable reason and one plain mistake.

**"Bookmarks don't show in the rendered PDF."** `BookmarkField` was never going to produce an
outline entry. Its own summary says so — *"BookmarkField is used as target for Hyperlinks or
PageRefs"* (`BookmarkField.cs:41`). Outline entries come from `ParagraphFormat.OutlineLevel`, which
the built-in `Heading1`–`Heading9` styles set (`Styles.cs:250-299`) and `ParagraphRenderer.Render`
turns into `DocumentRenderer.AddOutline`. The confusion is real and not the reporter's fault: Adobe
Acrobat calls its outline panel "Bookmarks", so `BookmarkField` is the obvious thing to reach for
and the wrong one.

**"No apparent means to reference a bookmark."** There is: a `Hyperlink` of `HyperlinkType.Local`
plus a `PageRefField`.

The reporter's real mistake was one line — `page1.Elements.Add(new BookmarkField(...))` puts the
field on `Section.Elements` instead of inside a `Paragraph`.

### Reproduction

Three documents, each a contents page linking to a bookmark in a later section, built against the
library before the change:

| | outline entries | link annotations | what the contents line rendered as |
|---|---|---|---|
| A — `Section.Elements.Add(new BookmarkField(...))`, the issue's code | 0 | **0** | *"Bookmark 'recipe-1' is not defined within the document"* |
| B — `paragraph.AddBookmark(...)` instead | 0 | 1 | "Go to Scones, page 2" |
| C — B, plus `Style = "Heading1"` | 2 | 1 | "Go to Scones, page 2" |

Row A is the whole issue: the field was accepted, silently dropped, and the failure surfaced far
away as text printed into the page by an unrelated field.

And in row C, the destination of the outline entry read

```
/Dest [12 0 R /XYZ null null null]
```

— a page and no position, which leaves a reader wherever the page is already scrolled to.

---

## Items 1 and 2 — destinations that name a place

Both share a cause: nothing carried the vertical position, so both now do.

### PdfPinata

`PdfLinkAnnotation.CreateDocumentLink` and `PdfPage.AddDocumentLink` gain an overload taking a
`destinationTop` in default page coordinates. `NaN` means "no position" and writes exactly what was
written before, so callers using PdfPinata directly are unaffected. The destination is built in
`WriteObject`, which is where the page index is resolved:

```csharp
Elements[Keys.Dest] = double.IsNaN(_destTop)
    ? new PdfLiteral("[{0} 0 R/XYZ null null 0]", dest.ObjectNumber)
    : new PdfLiteral("[{0} 0 R/XYZ null {1} 0]", dest.ObjectNumber,
        PdfEncoders.Format("{0:0.###}", _destTop));
```

`PdfOutline` already had a `Top`, defaulting to `NaN`; it was simply never set.

### MigraDoc

- `DocumentRenderer.AddOutline` gains a `destinationTop` and sets `PdfOutline.Top`. The old
  signature is kept and forwards `NaN`.
- `ParagraphRenderer.Render` supplies it from the paragraph's own content area. The paragraph is
  being drawn onto the very page the entry points at, so its own `gfx.Transformer` is what turns a
  distance down the page into a distance up it.
- `FieldInfos.BookmarkInfo` gains a `top`, and `AddBookmark` takes the vertical position.
- `EndHyperlink` passes `fieldInfos.GetBookmarkTop(...)` alongside the page number it already had.

**The position is taken while formatting, not while rendering.** A table of contents is drawn
*before* the bookmarks it points at, so by the time the link is made the answer has to be known
already. Formatting is the pass that knows it.

Turning a distance down the page into a distance up it needs the page height, which
`FieldInfos` did not have. `FormattedDocument.InitFieldInfos` now sets it, reading the page setup
**the same way `CalcContentRect` does**:

```csharp
this.currentFieldInfos.pageHeight = pageSetup.Orientation == Orientation.Portrait
    ? pageSetup.PageHeight.Point
    : pageSetup.PageWidth.Point;
```

That landscape swap is the trap. A landscape page is the page setup turned on its side, so its
height is the *width* of the paper; getting it the wrong way round puts every destination off the
page, and only on landscape documents, which is exactly the kind of thing that ships.

---

## Item 3 — the silent drop

`DocumentElements` inherits `Add(DocumentObject)` from `DocumentObjectCollection`, so a
`BookmarkField` on a section compiles and is accepted. `Renderer.Create` (`Renderer.cs:157-177`)
has no case for it and returns `null`, and `TopDownFormatter` skips whatever has no renderer.

That skip is deliberate and load-bearing — the comment there says *"Slightly hacked for legends:
they are rendered as part of the chart. So they are skipped here."* `DocumentElements.AddLegend`
puts a `Legend` in the same collection and relies on being skipped.

**So throwing on an unrenderable object was rejected**: it would break `AddLegend`, and picking out
`BookmarkField` alone to throw on would be arbitrary. Instead the formatter registers a bookmark
where it stands and carries on:

```csharp
BookmarkField bookmark = docObj as BookmarkField;
if (bookmark != null)
  this.areaProvider.AreaFieldInfos.AddBookmark(bookmark.Name, area.Y);
```

Five lines, nothing breaks, and the code on the issue now does what it plainly meant. Row A of the
reproduction becomes row B.

`DocumentElements.AddBookmark(string)` is added alongside, so the working way is also the
discoverable one. Its summary says what a bookmark is not, since that is the whole confusion.

---

## Item 4 — documentation

`docs/PinataLayout/samples/OutlinesAndTableOfContents.md`, linked from the samples index. It opens
with the table that would have answered the issue outright — outline entries come from
`OutlineLevel`, bookmarks are link targets — then covers nesting, a worked table of contents, and a
"what does not work" section holding the reporter's own line.

It was not entirely undocumented before: `HelloPinataLayout.md:76` says an outline level other than
`BodyText` "automatically creates the outline (or bookmarks)". But that is a comment inside a long
sample, and nobody looking for how to make a table of contents was going to find it.

---

## Item 5 — every tree arrived collapsed

Added after the rest, and found by `SampleApp`'s `Outline` demo rather than by the issue: a
bookmark panel built with `opened: true` throughout still showed nothing but its top level.

A reader takes an entry's expanded state from `/Count` — [ISO 32000-1 Table 153][table153]:
positive when the entry is open, giving the number of descendants that would then be visible;
negative with the same magnitude when it is closed; **absent** when it has no descendants. The
outline dictionary at the root carries the same key with the total number of visible rows, and
never a negative one (Table 152).

`PdfOutline.PrepareForSave` wrote that key only `if (OpenCount > 0)`. `OpenCount` was an
`internal int` field, and the one thing that ever assigned it was `PdfOutlineCollection.Add`:

```csharp
if (outline.Opened)
{
    outline = _parent;
    while (outline != null)
    {
        outline.OpenCount++;
        outline = outline.Parent;
    }
}
```

Three things wrong with that, and the third is the one that made it useless:

- it ran **once**, as the entry was added, so an `Opened` assigned at any point afterwards was
  never seen, and `Remove` never took a contribution back;
- it counted the wrong thing — *open descendants*, where the specification asks for descendants
  that would be **visible**, a walk that stops at a closed child instead of counting through it;
- it credited the new entry's **ancestors**, never the entry itself. So a chapter whose sections
  were added with the default `opened: false` finished with `OpenCount == 0`, wrote no `/Count`,
  and arrived shut — which is every chapter anybody writes.

The net effect was that no outline *item* in a document this library produced carried `/Count` at
all. Only the root dictionary got one, from the ancestor walk, and its value was a count of opened
top-level entries rather than of visible rows.

`CountOpen()`, the method that looks like it would compute this, was marked *"Not yet used"*, was
called from nowhere, and returned a constant zero in the collection overload — under a file-level
comment reading `// Review: CountOpen does not work. - StL/14-10-05`.

### The change

`OpenCount`, `CountOpen()` and the `Count` property that was read into and never read from are all
gone. The tree is measured once per save, in a post-order pass the root starts before anything is
written:

```csharp
int MeasureVisibleDescendants()
{
    int count = 0;
    if (_outlines != null)
        foreach (PdfOutline child in _outlines)
        {
            // Every child is measured. Only an open one contributes what is under it.
            int below = child.MeasureVisibleDescendants();
            count += 1 + (child.Opened ? below : 0);
        }

    _visibleDescendants = count;
    return count;
}
```

`PdfCatalog.PrepareForSave` calls the root's `PrepareForSave` and that walks down, so the root is
the one entry point and the only place the measuring starts. `PrepareForSave` then writes
`_visibleDescendants` on the root, and `_opened ? n : -n` on an entry with children — removing the
key from one without, which matters for an entry read in with `/Count` whose children have since
been deleted.

**Post-order rather than a property read on demand.** The first version of this fix derived the
count from a property, which walked the subtree each time it was read. `PrepareForSave` reads it
once per node, so every node re-measured everything below it: a chain of `n` open entries measured
suffixes of length `n-1`, `n-2` … 1, making a save O(n²). A document with a chapter per page and a
heading per section is exactly that shape. Measuring bottom-up visits each node once.

Either way it is derived at save time rather than maintained incrementally, so it cannot go stale
however the tree is assembled.

`Initialize` now also sets `_opened` from the sign of `/Count`. It never did, so a document that
was opened and saved again lost every expanded branch in it — a silent loss, since `Opened` read
back as whatever the caller had last assigned.

[table153]: https://www.iso.org/standard/63534.html

---

## Verification

`PdfPinata.Test/Outlines/BookmarkAndOutlineTests.cs`, 9 tests:

- an outline entry points at the heading, and one further down the page points further down —
  before the change the destination carried no position at all, so both fail outright;
- a local hyperlink points at the bookmark, on the right page;
- **a bookmark put on a section is not dropped** — the issue's own code, which produced no link
  before;
- the same through `DocumentElements.AddBookmark`;
- a bookmark on a **landscape** page is measured against the shorter side;
- a hyperlink to a bookmark that does not exist still makes no link and does not throw;
- a document link with no position still writes exactly the destination it used to, which is what
  keeps direct PdfPinata callers working;
- a document link given a position carries it.

Whole suite green on net8.0 and net10.0, 337 passed on each, one pre-existing skip
(`CanCreatePdfOver2gb`). Solution builds with 0 warnings.

`PdfPinata.Test/Outlines/OutlineOpenStateTests.cs`, 9 tests, for item 5:

- an open entry counts its children up, and a closed one counts the same number down;
- `Opened` assigned **after** the entry was added is still written — the case the old bookkeeping
  could not see;
- an entry with no children carries no `/Count`;
- a closed child hides its own descendants from the count above it, where a count of open
  descendants would have said five rather than three;
- the outline dictionary counts every row a reader would show, and not the top level alone;
- `Opened` survives a read and another save, in both states;
- a 40-deep chain, open all the way down, counts every level beneath it — the shape the post-order
  pass exists for, and the arithmetic it has to get right;
- closing one link of a deep chain hides everything under it from the level above, while the shut
  entry still carries its own subtree so it knows what to show when it is opened.

Whole suite green on both frameworks afterwards: 1715 passed on each, same one skip.

## Cost

242 lines of tests; about 90 lines of library across seven files, most of it the plumbing that
carries one number from the formatter to the writer.

## Not in scope

- **Named destinations.** A bookmark still resolves to a page and a position rather than to a PDF
  named destination in the catalog `/Names` tree, so links do not survive being imported into
  another document. That is the same gap `docs/specs/import-size-and-annotations.md` lists as out
  of scope for page import.
- **A generated table of contents.** MigraDoc has no equivalent of Word's TOC field; the entries
  are written by the caller. Nothing here changes that.
- **Horizontal position.** Destinations set the top and leave the left alone, which is what a
  reader wants for a full-width heading.
