# Spec — Replace `ArrayList` and `Hashtable` with generic collections

F5 of [`dom-value-model-findings.md`](dom-value-model-findings.md) fixed seven `ArrayList.ToArray(Type)`
calls because `RequiresDynamicCode` made them unsafe under native AOT. That closed the one symptom a
compiler could see. The collections themselves are untouched: **46 `ArrayList` references across 17
files, and 12 `Hashtable` references across 8**, spread over the DOM, the renderer and the charting
layer.

That finding recorded the wider migration as "worth doing as its own tracked piece of work, not
folded into anything". This is that spec.

Three reasons to do it, in descending order of how much they matter:

1. **Boxing in the layout hot path.** `ParagraphFormatInfo.lineInfos` is an `ArrayList` of
   `LineInfo`, which is a struct. Every line of every paragraph laid out allocates a box, and every
   read unboxes a copy. The same is true of `TabOffset`, and of the two enum-keyed `Hashtable`s that
   box their key on every lookup.
2. **Type safety.** `DocumentObjectCollection.elements` is an `ArrayList`; the class is `public`,
   implements the non-generic `IList`, and will accept any object at all. The cast back to
   `DocumentObject` is written 20-odd times across the class and its callers.
3. **The class of problem F5 fixed one instance of.** `ToArray(Type)` was reachable from an
   `ArrayList` and nowhere else. Removing the type removes the hazard rather than the seven calls.

And one reason to be careful, which shapes the whole sequencing below: **the renderer holds most of
the sites and has no unit tests of its own.** `PinataLayout.Rendering.Tests` exists as a project and
contains no test files. What actually covers the renderer is ~39 tests under `PdfPinata.Test`
(`Rendering/`, `Outlines/`, `MigradocTurkishTest.cs`), several of which are golden-image comparisons
that skip entirely when Ghostscript is unavailable.

---

## 1. The inventory

Grouped by what the migration costs, not by where the code lives.

### 1.1 Straightforward — a private field with a known element type

| file | member | holds | becomes |
|---|---|---|---|
| `PinataLayout.DocumentObjectModel/DocumentObjectCollection.cs:262` | `elements` | `DocumentObject` | `List<DocumentObject>` |
| `PinataLayout.DocumentObjectModel.IO/DdlReaderErrors.cs:70` | `errors` | `DdlReaderError` | `List<DdlReaderError>` |
| `PinataLayout.DocumentObjectModel/Paragraph.cs:580` | `paragraphs` (local) | `Paragraph` | `List<Paragraph>` |
| `PinataLayout.DocumentObjectModel.Visitors/PdfFlattenVisitor.cs:82` | `textIndices` (local) | boxed `int` | `List<int>` |
| `MigraDoc.Rendering/FormattedCell.cs:184` and four siblings | `renderInfos` | `RenderInfo` | `List<RenderInfo>` |
| `MigraDoc.Rendering/TopDownFormatter.cs:83,239,255` | `renderInfos` | `RenderInfo` | `List<RenderInfo>` |
| `MigraDoc.Rendering/ParagraphFormatInfo.cs:74` | `lineInfos` | `LineInfo` (**struct**) | `List<LineInfo>` |
| `MigraDoc.Rendering/ParagraphFormatInfo.cs:64` | `LineInfo.tabOffsets` | `TabOffset` (**struct**) | `List<TabOffset>` |
| `MigraDoc.Rendering/ParagraphRenderer.cs:2617` | `tabOffsets` | `TabOffset` (**struct**) | `List<TabOffset>` |
| `MigraDoc.Rendering/ParagraphIterator.cs:281` | `positionIndices` | boxed `int` | `List<int>` |
| `PdfSharp.Charting/ChartFrame.cs:227` | `chartList` | `Chart` | `List<Chart>` |
| `PdfSharp.Charting/DocumentObjectCollection.cs:244` | `elements` | `ChartObject` | `List<ChartObject>` |
| `PdfSharp.Charting.Renderers/CombinationChartRenderer.cs:253-255` | three locals | `Series` | `List<Series>` |

`Hashtable`, same category:

| file | member | key → value | becomes |
|---|---|---|---|
| `PinataLayout.DocumentObjectModel.IO/Symbols.cs:232-233` | `enumToName`, `nameToEnum` | `Symbol` ↔ `string` | `Dictionary<Symbol, string>`, `Dictionary<string, Symbol>` |
| `PinataLayout.DocumentObjectModel/Styles.cs:414` | `visitedStyles` | `Style` → `null` | **`HashSet<Style>`** — it is a set, not a map |
| `MigraDoc.Rendering/DocumentRenderer.cs:347` | `previousListNumbers` | `ListType` → `int` | `Dictionary<ListType, int>` |
| `MigraDoc.Rendering/ParagraphFormatInfo.cs:168` | `imageRenderInfos` | `Image` → `RenderInfo` | `Dictionary<Image, RenderInfo>` |
| `MigraDoc.Rendering/ParagraphRenderer.cs:2616` | `imageRenderInfos` | `Image` → `RenderInfo` | `Dictionary<Image, RenderInfo>` |

### 1.2 Internal API — a signature change, but nothing outside the assembly

`IAreaProvider` is `internal`, so `void StoreRenderInfos(ArrayList renderInfos)`
(`IAreaProvider.cs:79`) and its five implementations change together as one edit. This is the
single change that unblocks the `FormattedCell`/`FormattedDocument`/`FormattedHeaderFooter`/
`FormattedTextArea`/`FormattedTextFrame` group and `TopDownFormatter`, because they pass the same
list between them.

`FormattedDocument.pageRenderInfos` is already `Dictionary<int, ArrayList>` — only the value type
is left to do.

### 1.3 Public API — needs a decision, not just an edit

Three sites are reachable from outside the assembly. None of them *has* to change for the migration
to be worth doing, and two of them should not.

**`DocumentObjectCollection`** (`public abstract`, implements non-generic `IList`). The `elements`
field is private, so replacing it is invisible — *provided the `IList` implementation keeps behaving
as it does now*. See §3.1; this is the one place where a careless edit changes observable behaviour.

**`DdlReaderErrors.GetEnumerator()`** returns non-generic `IEnumerator`. Keep the signature; return
`List<T>`'s enumerator boxed to it. Adding a generic `IEnumerable<DdlReaderError>` alongside is a
separate, additive question — out of scope here.

**`Borders.BorderEnumerator`** is `public`, and its constructor takes a `Hashtable`
(`Borders.cs:494-506`). Changing the parameter type is a **breaking change to a public constructor**.
It is also the worst code in the inventory, for two reasons worth recording:

```csharp
public Border Current
{
    get
    {
        IEnumerator enumerator = ht.GetEnumerator();
        enumerator.Reset();
        for (int idx = 0; idx < index + 1; idx++)
            enumerator.MoveNext();
        return ((DictionaryEntry)enumerator.Current).Value as Border;
    }
}
```

* It re-enumerates the whole table from the start on **every** read of `Current`, which is O(n²) for
  a six-element walk that could be an index.
* `Borders.GetEnumerator` builds the table with `ht.Add("Top", top)` and five more
  (`Borders.cs:119-130`), and `Hashtable` specifies no iteration order. So `foreach (Border b in
  borders)` yields the six borders in an **unspecified order**, and always has. This is the same
  shape as F6 and F8 in the findings document: an order nobody chose, inherited from a hash table.

Nothing in the tree calls `BorderEnumerator` — `Borders.GetEnumerator` is the only construction
site, and it is an explicit interface implementation. The recommendation is therefore: **leave the
public constructor alone in this piece of work** and record the ordering as a known quirk. If it is
ever changed, the honest version is a new enumerator over a fixed six-element array in declaration
order, with the `Hashtable` constructor kept as `[Obsolete]`. That is an API decision, and it wants
its own note in the release notes rather than being smuggled in under a performance change.

---

## 2. What the migration actually buys

Worth being specific, because "modernise the collections" is not a reason.

**`LineInfo` is a struct in an `ArrayList`.** `ParagraphFormatInfo.AddLineInfo` boxes one per line of
laid-out text; `GetLineInfo`, `GetFirstLineInfo` and `GetLastLineInfo` each unbox a copy. A document
of 10,000 lines allocates 10,000 boxes that a `List<LineInfo>` would not, and `Append`
(`ParagraphFormatInfo.cs:106`) copies them wholesale between format infos during pagination.

**`TabOffset` is a struct in an `ArrayList`**, boxed once per tab stop crossed
(`ParagraphRenderer.cs:333`) and unboxed on every read (`ParagraphRenderer.cs:1021`).

**Two `Hashtable`s are keyed by an enum**, so each lookup boxes the key:
`Symbols.enumToName[symbol]` on every symbol written by the parser's error paths, and
`DocumentRenderer.previousListNumbers[listType]` on every numbered list item rendered. The latter
boxes the value too — it stores `int`.

**`Symbols.nameToEnum` is on the DDL scanner's hot path.** `KeyWords.SymbolFromName` is called for
every token the scanner produces (`DdlScanner.cs:125,224,1217`), and returns `object` that is
immediately unboxed to `Symbol`.

None of this is measured. The migration should not be sold as a performance fix without numbers —
see §5.

---

## 3. The traps

Each of these is a place where the obvious edit changes behaviour. They are the reason this is a
spec rather than a find-and-replace.

### 3.1 `ArrayList`'s untyped tolerance, in `DocumentObjectCollection`

`ArrayList.Contains(object)`, `IndexOf(object)` and `Remove(object)` accept anything and answer
`false`/`-1`/no-op for a foreign type. `List<DocumentObject>` will not compile against an `object`
argument at all, and the natural fix — a cast — throws where the old code answered.

The `IList` members must therefore be written to preserve the tolerance:

```csharp
bool IList.Contains(object value) => value is DocumentObject d && elements.Contains(d);
int IList.IndexOf(object value) => value is DocumentObject d ? elements.IndexOf(d) : -1;
void IList.Remove(object value) { if (value is DocumentObject d) elements.Remove(d); }
```

Two more in the same class:

* `IList.Add(object)` returns the new index. `List<T>.Add` returns `void`, so it becomes
  `elements.Add(d); return elements.Count - 1;`.
* `((IList)collection).Add("not a DocumentObject")` currently succeeds and throws later, on read.
  With a typed list it must either throw at `Add` or be dropped. **Throwing at `Add` is the better
  behaviour and is still a behaviour change**; it deserves a line in the release notes rather than
  silence.

`CopyTo(Array, int)` is public and stays — `List<T>` has an `ICollection.CopyTo(Array, int)` via the
interface, and the existing signature can delegate to it.

### 3.2 `Hashtable` returns null for a missing key; `Dictionary` throws

`Symbols.SymbolFromName` depends on this exactly:

```csharp
object obj = nameToEnum[name];
if (obj == null)          // <-- "not a keyword" is signalled by a null return
```

Every such read becomes `TryGetValue`. The sites are `Symbols.cs:203`, `Symbols.cs:226`,
`DocumentRenderer.cs:326,336`, and `ParagraphRenderer.cs:2495-2496` (which already does
`ContainsKey` followed by an indexer — one `TryGetValue`, as `FormattedDocument` got in F5).

### 3.3 `ArrayList.Clone()` has no `List<T>` equivalent

`ParagraphIterator` clones its index list in five places (`ParagraphIterator.cs:155,206,220,244,263`):

```csharp
ArrayList indices = (ArrayList)parIterator.positionIndices.Clone();
```

`Clone()` is a shallow copy, so `new List<int>(other)` is exactly equivalent for a list of `int`.
It would **not** be equivalent for a list of reference types, and nothing else in the inventory
clones — worth checking rather than assuming when the edit is made.

### 3.4 Struct semantics do not change, and that is the point

`(LineInfo)lineInfos[i]` unboxes a copy today; `lineInfos[i]` on a `List<LineInfo>` returns a copy
too. Mutating the result affects nothing in either case, so the migration is faithful. What changes
is that `CollectionsMarshal.AsSpan` would *let* you mutate in place — do not, in this piece of work.
Preserving the copy semantics is what makes the change reviewable.

### 3.5 `Styles.visitedStyles` is a set with `null` values

`visitedStyles.Add(style, null)` and `!visitedStyles.Contains(style)` (`Styles.cs:422-431`). This is
a `HashSet<Style>`, not a `Dictionary<Style, object>`. Note that `Hashtable.Contains` is
`ContainsKey`, not a value search — reading it as the latter would invert the recursion guard.

Both this and `imageRenderInfos` key on a `DocumentObject`, which does not override `Equals` or
`GetHashCode`, so both rely on reference identity. `HashSet<T>`/`Dictionary<K,V>` use the same
default, so behaviour is preserved — but it should be stated in a comment at each site, because
"keyed by a document object" reads like value equality and is not.

---

## 4. Sequencing

Six pieces, smallest blast radius first, each one independently revertible. The renderer group is
last deliberately: it is the least covered code and the most likely to need a golden-image rebase.

| # | scope | why here |
|---|---|---|
| 1 | `Symbols`, `Styles.visitedStyles`, `DdlReaderErrors`, `Paragraph.SplitOnParaBreak`, `PdfFlattenVisitor` | Self-contained, well covered by the DDL round-trip tests, no signature changes |
| 2 | `DocumentObjectCollection` (DOM) | One class, but it is public and carries §3.1 — its own review |
| 3 | `PdfSharp.Charting` — `DocumentObjectCollection`, `ChartFrame`, `CombinationChartRenderer` | Same shape as 2, separate assembly, separate risk |
| 4 | `IAreaProvider.StoreRenderInfos` and its five implementers, plus `TopDownFormatter` | One signature, six call sites, must move together |
| 5 | `ParagraphFormatInfo` (`lineInfos`, `tabOffsets`, `imageRenderInfos`), `ParagraphRenderer`, `ParagraphIterator` | The hot path and the boxing; the largest single review |
| 6 | `DocumentRenderer.previousListNumbers` | Trivial, but it is the last one and closes the count |

Not in the list: `Borders.BorderEnumerator` (§1.3, an API decision), and the commented-out
`Hashtable` that used to sit in `PinataLayout.Rendering/MigraDoc.Rendering.UnitTest/TestLayout.cs`.
That folder was a test project of upstream MigraDoc's whose `.csproj` did not survive the port,
which left five files being swallowed by the renderer's own source glob and four accidental public
types shipping in the package. It has been deleted; what was worth keeping in it was promoted to
`PinataLayout.Rendering.Tests`, including `ParagraphIteratorTests`, which is what piece 5 above
should be checked against.

---

## 5. Acceptance

**Per piece:**

* The full suite passes on `net8.0` and `net10.0`. That is 420 tests in `PdfPinata.Test` today,
  including the golden-image rendering comparisons.
* `grep -rn "ArrayList\|Hashtable"` over the touched files returns nothing but comments.
* No new warning in the solution build, which includes `PinataLayout.AotSmokeTest` since F5.

**For the whole migration:**

* The AOT publish stays clean of `IL2xxx` and `IL3050`, and the native binary still passes its 25
  checks. This is the check that says the class of problem F5 fixed is gone rather than moved.
* A golden-image run with Ghostscript present. Pieces 4, 5 and 6 change code with no direct unit
  coverage, and the rasterized comparison is the only thing that will notice a layout regression.
  **If a golden image changes, the migration is wrong** — every edit here is meant to be
  behaviour-preserving.

**What this spec deliberately does not promise:** a measured speedup. The boxing argument in §2 is
sound in principle and unquantified in practice. If the numbers matter, `PinataLayout.Benchmarks`
already exists and a before/after on a long document is the honest way to get them — but the
migration is justified by type safety and by removing the `RequiresDynamicCode` surface even if the
allocation win turns out to be noise.

---

## 6. Out of scope

* **Generic public surfaces.** `DocumentObjectCollection` implementing `IList<DocumentObject>`,
  `DdlReaderErrors` implementing `IEnumerable<DdlReaderError>`, and a typed `Borders` enumerator are
  all additive API work with their own compatibility questions. This spec changes the inside of
  those types, not their contracts.
* **`BorderEnumerator`'s unspecified order** (§1.3). Recorded, not fixed.
* **`System.Collections.Specialized` and friends.** The grep here is `ArrayList` and `Hashtable`
  only; nothing else legacy showed up in the DOM, the renderer or the charting layer.
