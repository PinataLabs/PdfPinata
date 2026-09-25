# Spec — a field's children are fields, and its widgets are widgets

Issue [#146](https://github.com/PinataLabs/PdfPinata/issues/146): `/Kids` holds two kinds of object,
and `PdfAcroField.Fields` hands out both as fields. This note records what is wrong, what six other
libraries do, and the plan. The plan breaks the public API on purpose, while the package is still at
0.x.

| item | what | breaking | status |
|---|---|---|---|
| 1 | A dictionary cannot be taken from one role into the other: a field never rebinds an annotation's reference, and an annotation never rebinds a field's | no | done |
| 2 | A merged field-and-widget dictionary is one `PdfAcroField`; its annotation role is a view cached on it | no | done |
| 3 | `Fields` lists child fields only; new `Widgets` lists widget annotations | **yes**, behaviour | done |
| 4 | `PdfAcroFieldCollection` stops being a `PdfArray` and becomes `IReadOnlyList<PdfAcroField>` | **yes**, compile | not started |
| 5 | `PdfWidgetAnnotation.Field`, and the library's own kid walks moved onto `Widgets` | no | done |
| 6 | `AddWidget` on a merged field splits it first | no | not started |
| — | Separate classes for terminal and non-terminal fields | **deliberately not done** (§5.3) |
| — | Thin, uncached wrappers throughout (the PDFBox model) | **deliberately not done** (§5.1) |
| — | Merging or splitting dictionaries while reading | **deliberately not done** (§5.4) |

## 1. What is wrong

Four failures, each reproduced by a scratch test against `a73c547f` (not committed; items 1 and 2
turn them into real tests). All are reachable through public API.

**A widget comes back as a field.** A text field with one widget from `AddWidget`:
`text.Fields[0]` is a `PdfTextField` with the name `""`.

**Writing through that fake field loses the value.** `((PdfTextField)text.Fields[0]).Text = "x"`
writes `/V` into the widget, where no reader looks. The field's own `/V` stays empty and `text.Text`
reads `""`. Nothing throws.

**The object for a dictionary changes every time the other side reads it.** After `text.Fields[0]`,
`page.Annotations[0]` is no longer the widget that `AddWidget` returned. Read the two in turn and
each read gives a new object: the field indexer and the annotation indexer take the reference from
each other. Anything a wrapper keeps in C# fields rather than in the dictionary is lost on the way —
a `PdfTextField`'s `BackColor` and `BorderColor`, and a field's cached `Fields`.

**A merged dictionary can throw.** Read a merged field-and-widget dictionary from a file, take the
field, then read the page's annotations, then ask the *first* field object for `Fields`. It throws
`NotImplementedException: Cannot create value for key: /Kids`.

The cause of the last one is how type transformation works. `PdfDictionary(PdfDictionary)` calls
`ChangeOwner` on the entries, and `PdfObject(PdfObject)` sets `Reference.Value` to the new object. So
the newest wrapper owns the entries, and the entries create typed values from the newest wrapper's
`Meta`. A `PdfWidgetAnnotation`'s keys do not include `/Kids`, so the older field wrapper can no
longer create its own collection.

Two defects are mixed together here, and fixing either one alone is not enough:

- **Semantic.** `Fields` does not tell a child field from a widget. Before #145 this was reachable
  through the indexer and `DescendantNames`. Since #145, a plain `foreach` over `Fields` reaches it
  too.
- **Mechanical.** One dictionary can be claimed by two roles, and type transformation lets the last
  one in win. A `Fields` that filters out widgets fixes the ordinary case. A merged dictionary is
  still a field *and* an annotation, so `form.Fields` and `page.Annotations` still fight over it.

## 2. What the others do

Read from source, September 2026, except Syncfusion and Aspose (API docs only).

| library | a kid is a widget when… | widgets typed apart | merged dictionary | wrapper identity |
|---|---|---|---|---|
| PDFBox | the parent has no kid with `/T` | `PDAnnotationWidget`, from `getWidgets()` | a widget wrapper over the same `COSDictionary` | thin, new on every call; no rebinding |
| iText 8+ | `/Subtype /Widget` and none of the field keys | `PdfFormAnnotation`, from `getChildFormAnnotations()` / `getWidgets()` | an annotation over the same dictionary; explicit merge and split utilities | stateful cached tree; `makeIndirect` never rebinds |
| pdf-lib | the parent has no kid with `/T` | `PDFWidgetAnnotation`, from `getWidgets()` | a widget over the same dictionary | thin |
| pypdf | no `/T` and no `/TM` | not typed | the field is its own widget | snapshot copies |
| MuPDF | a leaf of the tree; `/T` marks the head of the field | `pdf_annot` | the same `pdf_obj` | one shared, reference-counted object |
| PDFsharp 6.2 | never — same bug as ours | no | — | rebinds, as we do |
| PDFsharp 7 preview | `/Subtype /Widget` and no `/FT` | `PdfFormFieldWidget`, plus a `PdfWidgetAnnotation` sharing the field's `Elements` | one element store, two objects, cached `_widget` | cached |

Where they agree:

1. **No mature library returns a widget as a field.** Ours and PDFsharp 6.2 are the exceptions.
2. **Child fields and widgets are two collections.** A terminal field's widgets are its `/Kids`, or
   the field itself when it has none.
3. **A merged dictionary is one dictionary seen through two wrappers.** No library splits it on read.
4. **No library lets a second wrapper take over the first one's identity.** Either wrappers are thin
   views (PDFBox, pdf-lib, MuPDF), or two objects share one element store under one reference
   (PDFsharp 7). The second is the one that fits this code base, and upstream has already chosen it.

## 3. The rules

### 3.1 Which kid is a widget

> A kid is a **widget** when it has `/Subtype /Widget`, a `/Parent`, no `/T` and no `/Kids`. Every
> other kid is a **field**.

The `/Parent` condition was added while building it. A widget always belongs to a field, so a
`/Widget` with no `/Parent` is a root field merged with its widget, even one with no name. It lets
the annotation side use the same rule: `page.Annotations` makes a `/Widget` canonically a field
exactly when `Fields` would list it.

- **`/T` is the test**, as in PDFBox, pdf-lib, pypdf and MuPDF. `/Subtype /Widget` alone is not
  enough, because a merged dictionary has it too.
- **The test looks at each kid**, unlike PDFBox and pdf-lib, which judge the parent by *any* kid
  having `/T`. ISO 32000-1 does not allow a mix of the two kinds, but a file from disk can have one.
  A per-kid test sorts a mixed array instead of misreading all of it.
- **iText's wider test does not fit.** iText counts *any* field key, `/V` included, as marking a
  field. This library's own `PdfCheckBoxField` writes `/V` onto its widget (`SetSingleChildState`),
  so that test would read our own check boxes as nested fields.
- **A kid with no `/T` and no `/Subtype` is a field** — a nameless field node. It is malformed but
  harmless, and it stays reachable.

The test goes in one internal place, `PdfAcroField.IsWidgetKid(PdfDictionary)`. `Fields`, `Widgets`
and every walk in the library call it.

### 3.2 Which object a dictionary is

> Every dictionary has one **canonical** wrapper, which `Reference.Value` points at. A dictionary
> that is a field is canonically a `PdfAcroField`, even when it is also a widget. Its annotation
> role is a **view**: a `PdfWidgetAnnotation` that shares the field's entries and reference, created
> once and cached on the field.

A view never calls `ChangeOwner` and never sets `Reference.Value`. The field therefore keeps the
entries, and with them its `Meta` and its dirty tracking. Writes through the view go into the same
entries, and an incremental save sees them. This is PDFsharp 7's `_widget ??= new
PdfWidgetAnnotation(this)`, reached by the same route for the same reason.

The field is canonical, not the widget, for two reasons. The field tree is where the dictionary's
meaning lives, in `/T`, `/V` and the inherited `/FT` and `/Ff`. And the choice must not depend on
which collection was read first, or the identity problem only moves.

That makes the annotation side do the work:

- **`PdfAnnotation.FromDictionary`** gets a `/Widget` that carries `/T` (or already *is* a
  `PdfAcroField`). It builds the field through the AcroForm factory and returns the field's widget
  view. It never builds a free-standing `PdfWidgetAnnotation` over a field dictionary.
- **`CreateAcroField`** gets a dictionary that is already a `PdfWidgetAnnotation`. That is a pure
  widget, which `Fields` does not list, so reaching it is a bug. It throws rather than rebinds.
- **A tripwire in `PdfObject(PdfObject)`** throws when an object is transformed from `PdfAnnotation`
  into `PdfAcroField`, or the other way. The class of bug reported here then fails loudly the next
  time some other path reaches it.

One detail looked like it needed more work, and turned out not to. The view reads widget keys
(`/MK`, `/AP`, `/BS`) through entries whose `Meta` is the *field's*, and a typed read of a key the
field's `Meta` does not declare would throw as §1's `/Kids` did. But `Meta` is consulted only to
*create* a missing value, and nothing in `Pdf.Annotations` does that. The only keys created that way
in either namespace are `/Kids` and `/Fields`, and those belong to the field, which keeps the entries.
Nothing had to change.

### 3.3 The collections

```csharp
public abstract class PdfAcroField : PdfDictionary
{
    public PdfAcroFieldCollection Fields { get; }              // child fields only
    public IReadOnlyList<PdfWidgetAnnotation> Widgets { get; } // widget kids, or [the view] when merged
    public bool IsTerminal { get; }                            // Fields.Count == 0
    ...
}

public sealed class PdfAcroFieldCollection : IReadOnlyList<PdfAcroField>
{
    public int Count { get; }
    public PdfAcroField this[int index] { get; }   // index among the fields, not among /Kids
    public PdfAcroField this[string name] { get; } // dotted path, as now
    public void Add(PdfAcroField field);           // writes /Parent as now
    public string[] Names { get; }
    public string[] DescendantNames { get; }
}

public sealed class PdfWidgetAnnotation : PdfAnnotation
{
    public PdfAcroField Field { get; }             // /Parent, or the field it is a view of
}
```

**The questions the issue asks, answered:**

1. *What does `Fields` mean?* The fields nested under this one, and nothing else.
2. *Where do widgets go?* `field.Widgets`, typed as `PdfWidgetAnnotation`.
3. *Merged dictionaries?* One `PdfAcroField`. `field.Widgets` is `[view]`, `page.Annotations` gives
   the same view, and `view.Field` gives the field back — all the same objects, whatever is read
   first.
4. *What does the indexer do with a widget's index?* It never sees one. It counts fields only, so
   `Fields[i]` is not `/Kids[i]`. The two stop being confusable because the collection is no longer
   an array (item 4). Code that wants `/Kids` as it is in the file reads
   `field.Elements.GetArray(PdfAcroField.Keys.Kids)`, as `PdfSignatures` and `PdfTextField` already do.
5. *Should wrapping stop rebinding?* Across roles, yes (§3.2). In general, no (§5.1).

**Why the collection stops being a `PdfArray`.** It is one today only because `Keys.Kids` carries
`typeof(PdfAcroFieldCollection)`, and that makes the array itself go through type transformation.
Keeping it would put a filtered indexer and an unfiltered `Elements` on the same object, which is the
index mismatch of question 4 in its most confusing form. Dropping it also removes the compromise #145
had to make: a `PdfArray` is already `IEnumerable<PdfItem>`, so the collection could not also be
`IEnumerable<PdfAcroField>` without making LINQ ambiguous. So today `field.Fields.Single()` is a
`PdfItem`, which the repro tests ran into. As a plain `IReadOnlyList<PdfAcroField>` it is typed.
`/Kids` and `/Fields` become ordinary arrays, and the collection becomes a view over one, created by
the field or the form, which holds the parent for `Add`.

## 4. The plan

The plan was one PR per item, with items 1 and 2 shipping first as a non-breaking patch. Building
them showed that plan was wrong: once a field may not take over a widget's reference, `Fields[i]`
has nothing honest to return for a widget kid. It can throw, return null, or skip the kid, and
skipping it is item 3. So items 1, 2, 3 and 5 are one PR; item 4 is the second, and item 6 the third.
Items 1 to 4 ship in one release.

1. **Stop cross-role rebinding** (§3.2, the tripwire and the throw in `CreateAcroField`). Tests: the
   §1 repros turned into assertions — `page.Annotations[0]` stays the object `AddWidget` returned
   after `Fields` is walked, and the stale-wrapper `/Kids` throw is gone.
2. **Merged dictionary as field plus cached view** (§3.2). Tests: every order of reading
   `form.Fields`, `page.Annotations` and `field.Widgets` gives the same two objects; a value written
   through either survives an incremental save; a typed read of `/MK` through the view works. The
   existing merged test in `TypedAnnotationReadingTests` must stay green.
3. **`Fields` filters, `Widgets` added** (§3.1, §3.3). The only change here that alters behaviour
   without breaking a build: code that counted widgets through `Fields` now counts something else.
   The changelog says so first.
4. **`PdfAcroFieldCollection` as `IReadOnlyList<PdfAcroField>`.** It breaks callers of
   `Fields.Elements`, which is what makes the change in item 3 visible. The PR title is `feat!:` so
   Release Drafter bumps the minor version at 0.x. Ship items 3 and 4 in one release.
5. **Move the library's own walks onto `Widgets`**:
   - `PdfCheckBoxField.ChildAt` and `ReferencedChildAt`, and `Checked`'s one-kid and two-kid branches
     (`Fields.Elements.Items.Length`)
   - `PdfTextField.RenderAppearance`
   - `GetAppearanceNames`
   - `GetDescendantNames`, where "terminal when nothing contributed a name" becomes `IsTerminal`

   Also add `PdfWidgetAnnotation.Field`. Tests that reach into `Fields.Elements` move to the raw
   array: `AcroFormTwinCheckBoxTests`, `AcroFormFieldTests.cs:175`, `AcroFormAuthoringTests.cs:94`.
6. **`AddWidget` on a merged field splits it first**, as iText's `separateWidgetAndField` does. The
   annotation keys go into a new widget dictionary, and the page's `/Annots` entry is swapped for it.
   Until then, `AddWidget` on a merged field throws `InvalidOperationException`. Today it writes a
   field that has both a `/Rect` and `/Kids`, which is not valid PDF.

Items 3 to 5 touch no docs-website excerpt. `FormsDemo` and `forms.md` use only `form.Fields.Add`,
`AddWidget` and `Fields[name]`, and their meaning does not change. `interactive-layer-gaps.md` needs
one sentence: `/Kids` still holds both kinds, but `Fields` no longer shows both.

## 5. Deliberately not done

### 5.1 Thin wrappers everywhere

PDFBox's model avoids the whole problem: a wrapper holds no state and is made again on every read.
Moving to it here would mean changing type transformation for every page, font, image and
annotation. It would also break the thing rebinding exists for — reading the same wrapper again, with
its C# state intact. The defect is rebinding *across roles*, and §3.2 removes exactly that.

### 5.2 A field test that counts every field key

That is iText's rule, and §3.1 says why it misreads this library's own check boxes.

### 5.3 Terminal and non-terminal classes

PDFBox and pdf-lib have `PDNonTerminalField` and `PDTerminalField`. Here a field's class comes from
its inherited `/FT`, and a group is legitimately a `PdfTextField` whose kids inherit `/Tx` from it.
The tests build groups that way, and the docs site shows it in `forms.md`. Splitting the classes
would break the recommended way of nesting to buy a distinction that `IsTerminal` already gives.

### 5.4 Normalising on read

iText can merge a lone widget into its field, or split one out, and `AddWidget` does not merge on
write. Doing either while *reading* would change a file the caller only opened. The one split this
note plans (item 6) happens only when the caller asks for a second widget.
