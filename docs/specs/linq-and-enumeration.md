# Spec — enumerating a collection gives what its indexer gives

What LINQ over PdfPinata's public collections does today, what is wrong with it, and what is going to
be fixed and what is not.

| item | what | status |
|---|---|---|
| 1 | This note, and the defaults on `Rows`, `Columns` and `Borders` documented as defaults | done |
| 2 | `PdfContents` and `PdfAcroFieldCollection` enumerate what their indexers return; the field collection gets a `Count` | not started |
| 3 | `doc.Pages.OfType<PdfPage>()` finds the pages; `PdfPages`, `PdfAnnotations` and `PdfContents` throw when changed under an enumeration | not started |
| 4 | The DOM's explicit `IList` members routed through the public ones, as the charting copy already is | not started |
| 5 | `IEnumerable` on the charting `XSeries`; `PdfOutlineCollection.Insert` at `Count`; a stale test comment | not started |
| — | A way to write `doc.Pages.Where(…)` without `Cast` | **deliberately not done** (§3.1) |
| — | Generic `IList<T>` on the DOM and charting collections | **deliberately not done** (§3.6) |
| — | Bulk setters that push a value onto every row, column or border | **deliberately not done** (§3.5) |

## 1. Where this came from

[ClosedXML#2867](https://github.com/ClosedXML/ClosedXML/issues/2867) is a report against another
library: `IXLColumns` is an `IEnumerable<IXLColumn>` that also carries operations on the whole set,
such as `AdjustToContents()`, and `ws.ColumnsUsed().Skip(1)` gives back a plain sequence the
operations are gone from. The reporter's choice was "do not implement the LINQ functions, or implement
them so they can be used".

The same question asked of this library turns up little of that shape — almost nothing here carries
an operation meaning "do this to every member" — and a good deal of something worse: collections that
enumerate something other than what they hold, and one that skips members without saying so.

The audit below was taken on 25 September 2026 by reading the code. It covers the PDF object model,
the PinataLayout DOM and the charting DOM; `PinataLayout.Rendering` exposes no collection types, and
the methods returning lists (`PdfSignatures.InDocument`, `PdfPage.GetImagePlacements`, the text
extractor, the signature verifier) return snapshots typed as `IReadOnlyList<T>` or `IList<T>` and
need nothing.

## 2. The measure

A collection is judged on three things, in the order they matter to a caller:

1. **Composable** — it is an `IEnumerable<T>` for a `T` worth having, so LINQ compiles without `Cast`.
2. **Faithful** — what enumeration yields is what the indexer returns. A `PdfReference` where the
   indexer hands out a `PdfAcroField` fails this.
3. **Safe under mutation** — changing the collection inside a `foreach` over it either works or
   throws. It never silently yields the wrong members.

And one thing recorded rather than judged: **the ClosedXML shape**, an operation on the collection
that a LINQ call leaves behind.

## 3. The findings

### 3.1 `PdfPages` is two sequences, and LINQ cannot choose

`PdfPages` derives from `PdfDictionary`, which is an `IEnumerable<KeyValuePair<string, PdfItem>>`,
and is itself an `IEnumerable<PdfPage>` (`Pdf/PdfPages.cs:45`). Type inference cannot pick between
the two, so **`doc.Pages.Where(p => …)`, `.Select`, `.Skip` and `.Count()` do not compile.** What
does is `doc.Pages.Cast<PdfPage>()`, or a lambda whose parameter type is written out:
`doc.Pages.Where((PdfPage p) => …)`. Every test in the tree that uses LINQ on pages goes through
`Cast`.

`Cast` works because it returns its source untouched when that source is already an
`IEnumerable<TResult>`. **`OfType` does not take that shortcut for a reference type** — it has to
filter nulls, so it always walks the non-generic `IEnumerable` — and the non-generic enumerator
`PdfPages` has is the one it inherits from `PdfDictionary`, which yields the `/Type`, `/Kids` and
`/Count` entries. **`doc.Pages.OfType<PdfPage>()` compiles and returns nothing.**

The first is not fixable without `PdfPages` ceasing to be a `PdfDictionary`, which would break every
caller reading an entry off the page tree root, and so it stays: `Cast<PdfPage>()` is the answer, and
item 3 says so in `PdfPages`' XML documentation. A second typed accessor on the class would not help — a property
typed `IReadOnlyList<PdfPage>` is unambiguous, but it is also one more name for a thing callers
already reach by `Cast`. The second is fixable in a few lines: `PdfPages` re-implements
`IEnumerable.GetEnumerator()` explicitly and returns its page enumerator (item 3).

### 3.2 Changing a collection inside a `foreach` over it

`PdfPages`' enumerator (`Pdf/PdfPages.cs:1086`) keeps an index and re-reads `Count` on every
`MoveNext`, with no version check. So:

```csharp
foreach (var page in doc.Pages)
    if (IsBlank(page))
        doc.Pages.Remove(page);   // the page after each one removed is never looked at
```

skips a page after every removal, and adding a page inside the loop enumerates the page just added,
which is a loop that ends only if the body stops adding. `PdfAnnotations` (`:179`) and `PdfContents`
(`:224`) have the same enumerator shape. Everything else in the library is backed by a `List<T>`, an
`ArrayList` or a `Dictionary` and throws `InvalidOperationException` instead.

Item 3 gives the three a version counter and has them throw as `List<T>` does. That is a change in
behaviour, but not one anybody could have been relying on: the loop above was already wrong, only
silently.

### 3.3 `PdfContents` enumerates one thing to `foreach` and another to LINQ

`PdfContents` declares `public new IEnumerator<PdfContent> GetEnumerator()`
(`Pdf.Advanced/PdfContents.cs:211`). `foreach` binds to that. LINQ binds to
`IEnumerable<PdfItem>`, which `PdfArray` routes through the `private protected` hook
`EnumerateItems` — and `PdfContents` does not override it, so LINQ sees the raw `PdfReference`s.
`contents.OfType<PdfContent>()` is empty.

This is exactly the defect #81 fixed for `PdfAnnotations` (`Pdf.Annotations/PdfAnnotations.cs:162`),
and `AnnotationCollectionTests` is the pattern for pinning it: typed `foreach`, the non-generic
interface and `IEnumerable<PdfItem>` all asked the same question.

### 3.4 `PdfAcroFieldCollection` enumerates references and has no `Count`

`PdfAcroField.PdfAcroFieldCollection` (`Pdf.AcroForms/PdfAcroField.cs:527`) — a form's `/Fields` and
a field's `/Kids` — has an indexer returning typed fields and **no typed enumerator at all**, so
every enumeration yields `PdfReference`s: `OfType<PdfAcroField>()` is empty and `Cast<PdfAcroField>()`
throws. It also has no `Count`, so the one caller that needs it,
`PdfDocument.MakeAcroFormsReadOnly`, calls LINQ's `Count()` in its loop condition — a full
enumeration on every iteration.

Item 2 overrides `EnumerateItems` to yield the typed fields, adds a public typed `GetEnumerator` for
`foreach`, adds `Count`, and rewrites the loop. It does **not** declare `IEnumerable<PdfAcroField>`:
that would be a second `IEnumerable<T>` beside the inherited `IEnumerable<PdfItem>`, which is §3.1's
ambiguity introduced on purpose.

### 3.5 The ClosedXML shape: defaults on the DOM's collections

The one place the reporter's complaint lands is the DOM, where the collection objects carry values:

- `Rows.Height`, `HeightRule`, `VerticalAlignment` (`Tables/Rows.cs`)
- `Rows.Alignment` and `LeftIndent`
- `Columns.Width` (`Tables/Columns.cs`)
- `Borders.Visible`, `Style`, `Width` and `Color` (`Borders.cs`)

None of them survives `table.Rows.Cast<Row>().Skip(1)`, and none of them is an operation on each
member in the first place. They are of two kinds, and neither is what a ClosedXML user expects:

- **Defaults.** `VisitRows` copies `Height`, `HeightRule` and `VerticalAlignment` into each row that
  has no value of its own, `VisitColumns` does the same with `Width`, and `FlattenedBorderFromBorders`
  does it for each of the six borders (`Visitors/VisitorBase.cs`). Setting `Rows.Height` does not
  change a row whose `Height` was set — the opposite of a bulk setter.
- **Table-wide values.** `Rows.Alignment` and `Rows.LeftIndent` are not copied anywhere; the table
  renderer reads them off the collection to place the whole table (`TableRenderer.cs`).

(`Borders`' `Distance…` values are neither: no single border has a distance, so they belong to the
bordered object as a whole and were never a candidate for a bulk setter.)

That is MigraDoc's model and the `.mdddl` format's, and it stays. Item 1 says it in the XML
documentation of each property. A bulk setter is a `foreach`, and one written as an extension would
make the defaults harder to tell apart from it rather than easier.

### 3.6 The DOM and charting collections are non-generic

`DocumentObjectCollection` in the DOM (`PinataLayout.DocumentObjectModel/DocumentObjectCollection.cs`)
and its copy in charting (`PdfPinata.Charting/DocumentObjectCollection.cs`) implement the non-generic
`IList` over an `ArrayList`. Every LINQ call over `Sections`, `Elements`, `Rows`, `Cells`,
`SeriesCollection` and the rest starts with `OfType` or `Cast`, and the tests are full of both.

A generic `IList<DocumentObject>` would look like the fix and is not one, which is why
[`legacy-collections-migration.md`](legacy-collections-migration.md) §6 already left it out:
`DocumentElements` and `ParagraphElements` hold mixed types, so `Cast<Paragraph>()` throws on the
first table, and a blank chart point is a null, so `Cast<Point>().Select(p => p.Value)` throws on the
first blank. A typed surface over collections like that promises a safety it cannot give. `OfType` is
the honest spelling and stays the answer.

What *is* a defect is that the DOM's explicit `IList.Add`, `Insert`, `Remove` and indexer setter
bypass the public methods (`DocumentObjectCollection.cs:267-319`), so `((IList)rows).Add(row)` sets
no parent, resets no cached values and — on `Rows` — creates no cells. The charting copy routes them
through the public methods already (`:198-235`). Item 4 does the same for the DOM.

### 3.7 The rest

Recorded here so the table in §4 is complete; item 5 fixes the first two.

- The charting `XSeries` (`PdfPinata.Charting/XSeries.cs:108`) has a public `GetEnumerator` and no
  `IEnumerable`, so `foreach` compiles by pattern and LINQ does not compile at all, `Cast` included.
- `PdfOutlineCollection.Insert` refuses `index == Count` (`Pdf/PdfOutlineCollection.cs:201`), which
  `IList<T>` requires it to accept as an append.
- `src/PdfPinata.Test/Helpers/TextOperators.cs` still says `CSequence`'s `IEnumerable<CObject>` throws
  `NotImplementedException`, and works round it. It has not since `ContentSequenceTests` pinned it.

Not fixed, and not planned:

- The DOM's `XSeries` exposes no enumerator, indexer or count; its values cannot be read back through
  the public API. That is a gap in the chart model rather than in enumeration.
- The structure tree offers no collection of an element's children.
- `PdfAttachments.Count` and its indexer each rebuild the list, so `for (i < Count) this[i]` is
  quadratic. Its enumerator is a snapshot and `foreach` is the way to walk it.
- `Borders`' enumerator yields in `Hashtable` order and re-walks the table on every `Current` —
  already recorded in [`legacy-collections-migration.md`](legacy-collections-migration.md) §1.3.
- `DdlReaderErrors` has `ErrorCount` rather than `Count`, and `Styles`' integer indexer is internal.
- `TabStops` holds removal markers alongside the tab stops, so enumerating it is not "the tab stops in
  effect". That is what the `.mdddl` model means by a tab stop list.

## 4. The audit

**Cmp**, **Fth**, **Mut** are §2's three measures. "LINQ yields" is the element type LINQ sees, which
is not always what `foreach` sees.

| type | implements | LINQ yields | Cmp | Fth | Mut | notes |
|---|---|---|---|---|---|---|
| `PdfArray` | `IEnumerable<PdfItem>` | `PdfItem`, references unresolved | yes | yes | throws | `Elements` is the `IList<PdfItem>`; its typed getters resolve, its indexer does not |
| `PdfDictionary` | `IEnumerable<KeyValuePair<string, PdfItem>>` | the entries | yes | yes | adding throws | every subclass inherits this sequence |
| `PdfPages` | the above **and** `IEnumerable<PdfPage>` | ambiguous | **no** (§3.1) | `OfType` **no** (§3.1) | **skips** (§3.2) | `Cast<PdfPage>()` is the way in |
| `PdfAnnotations` | `IEnumerable<PdfItem>` | `PdfAnnotation` (#81) | via `Cast` | yes | **skips** (§3.2) | |
| `PdfContents` | `IEnumerable<PdfItem>` | **`PdfReference`** | via `Cast` | **no** (§3.3) | **skips** (§3.2) | `foreach` yields `PdfContent` |
| `PdfAcroFieldCollection` | `IEnumerable<PdfItem>` | **`PdfReference`** | no | **no** (§3.4) | throws | no `Count` |
| `PdfOutlineCollection` | `IList<PdfOutline>` | `PdfOutline` | yes | yes | throws | `Insert` at `Count` refused (§3.7) |
| `PdfAttachments` | `IEnumerable<PdfFileSpecification>` | `PdfFileSpecification` | yes | yes | snapshot | `Count` and indexer rebuild (§3.7) |
| `PdfCustomValues` | via `PdfDictionary` | the entries | yes | indexer is typed, entries are not | adding throws | |
| `PdfNamedDestinationTable` | — | `Names` is a sorted snapshot of strings | yes | yes | snapshot | |
| `CSequence`, `CArray` | `IList<CObject>` | `CObject` | yes | yes | throws | |
| `IntervalSet` | `IReadOnlyList<XInterval>` | `XInterval` | yes | yes | — | |
| DOM `DocumentObjectCollection` and subclasses | non-generic `IList` | `object` | `OfType` (§3.6) | yes | throws | explicit `IList` members bypass the public ones (§3.6) |
| DOM `Rows`, `Columns` | as above | `object` | `OfType` | yes | throws | defaults and table-wide values (§3.5) |
| DOM `Cells` | as above | `object` | `OfType` | indexer creates cells enumeration does not see | throws | `Rows.Add` gives each row its cells |
| DOM `Borders` | non-generic `IEnumerable` | `Border` or null | `OfType` | yes | snapshot | unspecified order (§3.7); defaults (§3.5) |
| DOM `XSeries` | — | not enumerable | — | — | — | §3.7 |
| DOM `DdlReaderErrors` | non-generic `IEnumerable` | `object` | `OfType` | yes | throws | `ErrorCount`, no `Count` |
| charting `DocumentObjectCollection` and subclasses | non-generic `IList` | `object`; blanks are null | `OfType` | yes | throws | |
| charting `XSeries` | — (pattern `GetEnumerator`) | not enumerable to LINQ | **no** (§3.7) | — | — | |

## 5. Sequencing

Item 2 goes before item 3: both change `PdfContents`' enumerator, one to make LINQ see what `foreach`
sees and the other to add the version check to it. Items 1, 4 and 5 touch nothing the others do.

Items 2 and 3 change what existing code observes — a non-generic walk of `Pages` stops yielding
dictionary entries, and a `foreach` that changes the collection starts throwing. Both are released as
fixes rather than as a major version, because what they replace was a defect, not a contract. The
changelog has no heading for a behaviour change, so each entry goes under **Fixed** for its area and
its first sentence names the code that now behaves differently.
