# Spec — flattening a filled form into page content

What `PdfAcroForm.Flatten()` covers (issues #355 and #357), the decisions behind it, and what it
deliberately leaves out.

| item | what | status |
|---|---|---|
| 1 | `PdfAcroForm.Flatten()` draws each widget's normal appearance into its page and removes the form | done |
| 2 | The placement of ISO 32000-1 12.5.5, `/BBox` through `/Matrix` onto `/Rect` | done |
| 3 | `Flatten(redrawAppearances: true)` redraws text, combo box and list box fields first | done |
| 4 | Refusals: signed signature field, document that cannot be modified, document marked tagged with no tree | done |
| 5 | The text extractor follows form XObjects, so the flattened text can be read back | done |
| 6 | A conformance corpus document: a flattened PDF/A-2b form | done, veraPDF passes it |
| 7 | Flattening a tagged form, with re-tagging (section 11) | done, veraPDF passes it |
| 8 | Flattening one field | not done |

## 1. Why this is a spec and not a method

A form cannot be flattened by hand without three small mistakes, and each is easy to make: draw the
widgets' appearances into the page, take the widgets out of `/Annots`, take `/AcroForm` out of the
catalogue. The common ways to get it slightly wrong are scaling `/BBox` onto `/Rect` and forgetting
the form's `/Matrix`; drawing `/AP /N` as a stream when a check box's is a dictionary keyed by
`/AS`; flattening the first page only; and leaving `/Annots` alone unless every annotation in it is
a widget. The library already had what flattening needs: every variable-text field draws its own
appearance, `RedrawAppearance` exists, `Fields` and `Widgets` keep fields and widgets apart, and
#352 added the one step that takes a set of widgets off every page and out of the structure tree.

## 2. The API

```csharp
public sealed class PdfAcroForm
{
    public void Flatten();
    public void Flatten(bool redrawAppearances);
}
```

Two overloads rather than an optional parameter, so that a later option is an addition and not a
change to a compiled signature. `Flatten()` is `Flatten(false)`.

On the form, not on the document: a document has no form until one is made, and the form is what is
being turned into something else. Afterwards `document.AcroForm` answers null. The `PdfAcroForm`
object a caller still holds is detached from the document; its `Fields` still list the fields, and
changing them changes nothing.

Everything that can be refused is refused **before anything changes**, so a refusal leaves the
document as it was. A redraw that fails part-way, which the checks cannot foresee - a field box too
small to draw in - is an error and not a refusal, and leaves the fields redrawn before it redrawn.

## 3. What it does, in order

1. Refuse, per section 5.
2. If `redrawAppearances` is set, draw each text field, combo box and list box again (section 4).
3. For every widget of every terminal field in the form, found by walking `/Fields` and `/Kids`:
   find the pages that list it in `/Annots`, and on each draw its normal appearance (section 6).
   In a tagged document each drawing is inside a marked-content sequence (section 11).
   Pages are found from what they list, not from the widget's `/P`, which is optional and wrong in
   files from other software. This is what makes a field with widgets on several pages, and a
   radio group, work.
4. In a tagged document, write what the drawings decided into the structure tree and the parent
   tree (section 11). This is before the next step on purpose.
5. Take every widget of those fields off every page and out of the structure tree, through
   `PdfWidgetAnnotation.RemoveFromPagesAndStructure`, the step #352 made for removing a field. A
   widget that was hidden or had no appearance is removed too: flattening leaves no widget of the
   form behind, drawn or not. Annotations that are not widgets of the form are not in the set and
   are left in `/Annots` exactly where they were.
6. Take `/AcroForm` out of the catalogue. That removes the field tree, `/NeedAppearances`,
   `/SigFlags`, `/DR`, `/DA`, `/CO` and `/XFA` with it.

Step 6 is the whole of "remove the flattened fields from the form, and remove `/AcroForm` when no
fields are left": flattening the form flattens every field in it, so none is ever left. It is also
why #352's `Fields.Remove` is not used for the fields: it would walk every page again for each one,
to remove widgets that step 5 has already removed.

Not done: a widget that no field of the form leads to (no `/Parent`, or a field missing from
`/Fields`) is not part of the form and is left in `/Annots`.

## 4. Redraw: the default is "as it is"

**Flatten draws each widget's normal appearance as it stands.** No field redraws on save, so a form
read from a file has the appearances its producer or the last caller drew, and those are what a
reader showed: drawing them is the picture the reader had.

`Flatten(true)` first redraws every text field, combo box and list box, as `RedrawAppearance` does,
and then flattens. It is for the form that was changed in a way no field noticed - an ancestor's
`/V` that a kid inherits, the form's `/DA` - which is the case #340 added `RedrawAppearance` for. A
check box, radio button, push button and signature field draw no appearance of their own and are
drawn as they are.

**A form with no appearances flattens to nothing.** A producer that left the drawing to the reader and
set `/NeedAppearances` writes widgets with no `/AP`; flattened as they are they are removed and
draw nothing, and their values are lost. The default is "as it is" by decision, so this is what it
does; `Flatten(true)` draws a text field, combo box or list box first and is what such a form
wants. A check box or radio button with no appearance has none to redraw.

Three details. A **read-only field is redrawn**: `RedrawAppearance` refuses one, because it is
about filling the field, and flattening is not. A redraw that **cannot** happen - no `Font` on the
field and no font resolver registered - throws, as an explicit `RedrawAppearance` does, because the
caller asked for it; the check runs for every field before any is drawn. And an AP stream with **no
`/Resources`** is given the form's `/DR` by reference, which is how Acrobat reads one: the font such a
stream names lives in the form that is about to be removed.

## 5. Refusals

All are `InvalidOperationException`, and all are decisions rather than limits.

- **A document that cannot be modified** is refused through `EnsureCanModify`, as every operation
  that changes the page tree or the catalogue is. The kind is the default, `DocumentStructure`: a
  certifying signature permits form filling at its second level and flattening is not filling.
- **A document marked as tagged with no structure tree is refused**: `/MarkInfo /Marked true` and no
  `/StructTreeRoot`. It claims its content is tagged and holds no element for the drawn values to be
  tagged as. Drawing them untagged contradicts the claim, and marking them an artifact would take
  the values out of the reading order of a document meant to be read aloud, so neither is chosen
  here: the caller removes `/MarkInfo` or gives the document a tree. A document with a tree is not
  refused; see section 11.
- **A signed form is refused**: any signature field in the form with a `/V`, at any depth. Flatten
  changes page content and `/Annots`, which a signature locks, and `Save` rewrites the file so that
  every signature in it is invalid whatever was changed. `SaveIncremental` is the only save that
  keeps one, and an incremental revision that changes what the signatures lock is reported by a
  reader as a modified document. The same reasoning as #352's refusal to remove a signed field. An
  **unsigned** signature field is flattened like any other: its widget is a box on the page.

## 6. Drawing one widget

### The appearance

`/AP /N` is a stream - the appearance - or a dictionary of streams keyed by state, which is how a
check box and a radio button store theirs. For a dictionary the state is `/AS`. A widget with no
`/AP`, no `/N`, a state dictionary and no `/AS`, an `/AS` the dictionary has no entry for, or a
`/N` that is not a form XObject has no appearance, is not drawn, and is removed with the rest.
A check box that is off normally has `/AS /Off`, no `/Off` stream, and so draws nothing: right.

**Hidden and NoView widgets are not drawn.** `Hidden` (bit 2) and `NoView` (bit 6) of the
annotation flags `/F` say the widget is not shown on screen. `Invisible` (bit 1) applies to
annotation types the reader does not know, and not to a widget. `Print` is not consulted: a widget
that did not print will now print, which is what a flattened document is for. Optional content
(`/OC` on the widget) is not consulted either.

### The algorithm

ISO 32000-1 12.5.5 on a normal appearance: transform the four corners of the form's `/BBox` by its
`/Matrix`; take the smallest rectangle that encloses the result, the transformed appearance box; the
matrix `A` that scales and translates that box exactly onto the annotation's `/Rect` is concatenated
with the form's `/Matrix` to give the matrix drawn. The form's own `/Matrix` is applied by the
form-drawing operator itself (ISO 32000-1 8.10.1), so what the page content writes is `A` alone:

```
q sx 0 0 sy ex ey cm /FmN Do Q
```

with `sx = rect.width / box.width`, `sy = rect.height / box.height`, `ex = rect.x - box.x * sx`,
`ey = rect.y - box.y * sy`. The form's `/BBox` clips it, as it clips an appearance. A `/Matrix`
that rotates, skews or flips needs no special case: the enclosing box of a rotated appearance is
what is mapped, which is what 12.5.5 says, and what the tests with a quarter-turn `/Matrix` pin.
A `/BBox` or `/Rect` with no area has no `A`, and its widget is not drawn.

### Rotation

- **Page `/Rotate`.** `/Rect` and the page content are both in default user space, which the page's
  `/Rotate` then turns as one. So drawing into `/Rect` in the page's own content is the right place
  on a rotated page and nothing is done about it.
- **Widget `/MK /R`.** The rotation of the widget's content relative to the page. It describes how
  the appearance was authored: Acrobat writes it into the appearance's `/Matrix` and `/BBox`,
  and a reader draws that, not `/MK`. It is read by nothing at draw time, so it is not read here
  either. The text fields this library draws do not use it.
- **The `NoRotate` annotation flag is not honoured.** A widget with it on a page with a `/Rotate`
  keeps its upper-left corner fixed and its appearance upright; flattened, it turns with the page.
  No widget this library writes sets the flag and it is vanishingly rare on a widget. It is
  recorded here so that it is a known difference and not a surprise.

### Where it is drawn

Each page gets one new content stream at the end holding the `q … Q` blocks of every widget flattened
onto it. The page's existing content is wrapped in `q … Q` first, as every append is, so the new
stream starts from the default graphics state. The appearance form XObject is added to the page's
`/Resources /XObject`, under a name that is not already taken, and is the same object the widget
had: nothing is copied, and the appearance stream's bytes and filter are untouched.

**This is raw `q cm Do Q`, not `XGraphics`, and the reason is that no `XGraphics` route exists.**
`XGraphics.DrawImage` draws an `XForm`, which is a template this session built through an
`XGraphics` of its own, or an `XPdfForm`, which is a page imported from another document. A form
XObject read from this document is neither, and wrapping it as one would copy a stream and
re-register its resources to arrive at the same `Do`. `DrawImage` also places by scale and
translation, and a rotated `/Matrix` needs a general `cm`. The page resizer draws a form of the
same document by writing `q cm Do Q` too, and this follows it.

## 7. The text extractor follows `Do`

The acceptance test is that text extraction from the flattened page finds the values, and
`PdfTextExtractor` did not follow a `Do` operator at all: text in a form XObject - this, and any
page imported with `XPdfForm` - was invisible to it. It does now. A form's `/Matrix` is applied, its
own `/Resources` are used for its fonts (falling back to the enclosing ones, as a form with none
does), the text state it leaves is not kept, marked content open around the `Do` still applies to the
text inside, a form whose `/Matrix` is not six numbers is skipped, and nesting is capped: a form
is entered once at a time, 32 deep and 4,096 times in all. Runs come back in the order drawn, with the form's text where
the `Do` is. This changes what `ExtractRuns` returns for a page that draws forms; it was returning
less than the page says.

## 8. Tests

`FormFlatteningTests` saves, reopens with `PdfReader` and reads the raw dictionaries. The ones that
count: no `/AcroForm` and no `/Widget` in any `/Annots`; `PdfTextExtractor.ExtractText` finds each
field's value on the flattened page; rasterizing before and after gives the same picture for text,
combo box, list box, check box and radio fields and for a widget whose appearance has a rotated
`/Matrix` (`[Rasterizing]`, `[Test, GoldenImage]`); widgets on several pages; non-widget annotations
surviving; hidden widgets not drawn; the signed and open-mode refusals, each leaving the
document unchanged; and the redraw option, including the read-only field and the field with no font.
The rasterizing comparison is made at a quarter of the resolution, as `PdfHelper.Diff` does, because
a widget drawn by Ghostscript as an annotation and as content differs only along glyph edges.

## 9. A conformance corpus document

`pdfa-2b-flattened-form` in `ConformanceCorpus`: a text field, a combo box and a check box, filled,
in a document claiming PDF/A-2b, flattened, saved. It is the one document there whose claim is
about what a *change* leaves behind - no `/NeedAppearances`, no `/AcroForm`, no widget, a font used
only by a form XObject that must still be embedded. `FormFlatteningTests` also saves it and reads
back that none of those are present, which is what can be checked in-process.

**veraPDF passes it.** `./verapdf-check.ps1` (veraPDF 1.30.2 in Docker) built the corpus and
reported 10 of 10 documents conforming, `pdfa-2b-flattened-form` among them.

## 10. Deliberately not done

### 10.1 Tagged documents (issue #357)

Done; see section 11, which ends with what is still not done there.

### 10.2 Flattening one field

The issue says "and possibly a per-field variant". Everything above is the whole form. A single
field's flatten is the same drawing, followed by the part of #352's `Remove` that takes the field out
of `/Fields` and `/CO` while leaving the form in place, and it adds questions this change does not
need to answer: a radio group's other buttons, a field with kids, a signature field in a form that
stays. It is a call that sits beside this one and can reuse the drawing step when someone needs it.

### 10.3 XFA

A dynamic XFA form has its real content in `/XFA` and the AcroForm part is a fallback page. The
form is flattened as the AcroForm shows it and `/XFA` goes with `/AcroForm`.

### 10.4 Widgets outside the form, `NoRotate`, `/OC`, `Print`

Each is described above. They are the places where drawing the appearance into the page differs
from what a reader does with an annotation, and none is a case this library produces.

## 11. Flattening a tagged form (issue #357)

`PdfStructureFlattenTagger` does the tree work; the flattener asks it for an identifier per drawn
appearance and tells it to write the result. The tests are `FormFlatteningTaggedTests`.

### 11.1 What each widget becomes

A tagged form holds each widget as the content of an element, through an `/OBJR` kid and the
annotation's `/StructParent` entry in the parent tree. After flattening the appearance is page
content, so:

- The appearance is drawn in `/Span <</MCID n>> BDC` ... `EMC`, around the `q ... cm /FmN Do Q`
  of section 6, with `n` unique on the page.
- In the element that held the `/OBJR`, that kid is replaced **in place** by a marked-content
  reference: a bare integer when the element's `/Pg` is the page (an element with no `/Pg` is given
  the page), an `/MCR` with its own `/Pg` when it is not. Replaced in place, so the reading order
  within the element does not move. A widget listed on two pages has one `/OBJR` and two marks, and
  the element ends with one reference for each.
- The page's `/StructParents` array maps each new identifier to the element. A page with no
  `/StructParents` is given a key past every key the parent tree and the pages use, and
  `/ParentTreeNextKey` moves past it.
- The annotation's `/StructParent` is removed from the widget and its entry from the parent tree.

### 11.2 The element becomes a `/Span`

**Decision (the user's): the element is retyped to `/Span`.** A `/Form` is a form control; what is on
the page is text or a mark in the running content, and a validator and a screen reader treat a
`/Form` with no widget in it as a control that is not there. The other candidates were `/Div`, which
is block level where a value is inline, and the parent's own type, which varies and may not accept a
leaf. `/Span` is the inline element that every type may contain. The tag in the content stream is
`/Span` too, so the sequence and its element agree.

- A type the role map reads as `/Form` is handled the same way, found through
  `PdfStructureAnnotationRemover.ResolveTag`, which already follows `/RoleMap` (#352). The custom
  name is replaced by `/Span`; its role map entry is left alone, since another element may use it.
- An element that is not a form control keeps its type and only has its kid replaced. An element
  that still holds an annotation that is not a widget of the form (a link, say) keeps its type for
  the same reason.
- **Alternate text.** The field's `/TU`, or the nearest ancestor's, becomes the element's `/Alt`
  where the element has none; an `/Alt` the element already has wins. The value itself is the text
  in the marked content. Note that `/Alt` replaces an element's content for a reader that honours
  it, so a screen reader may announce the `/TU` and not the value; keeping `/Alt` was the user's
  decision, and a caller who would rather have the value read can clear it on the element.

### 11.3 The interaction with the remover

`PdfStructureAnnotationRemover` drops an `/OBJR` to a removed annotation and prunes a `/Form`
element it leaves empty. Flattening has the opposite need for the widgets it drew, so **the tagger
runs first (step 4) and the remover second (step 5)**:

- By the time the remover runs, each drawn widget's `/OBJR` is already a marked-content reference.
  There is no `/OBJR` of theirs left to drop, so `changed` is false and nothing is pruned.
- The element is a `/Span` by then, so the prune rule (`/Form` or a role-mapped `/Form`) does not
  apply either.
- The tagger removes the drawn widgets' parent tree keys itself, in the same write that adds the new
  entries, and strips `/StructParent` from the widgets, so the remover finds no key to write for
  them. The parent tree is written once per flatten and not once per widget:
  `PdfNumberTreeNode.Update`.
- A widget that was **not** drawn (hidden, or with no appearance) still has its `/OBJR`; the remover
  drops it and prunes its `/Form` element if that was all it held. That is right: nothing was drawn
  and the element stands for nothing. An element that held a drawn widget and a hidden one keeps the
  drawn one and is retyped; the tagger does not let the hidden widget's `/OBJR` count as a reason
  to keep the `/Form` type, because it is removed next.

### 11.4 The two shapes of tree

- **Built this session** through `PdfStructureBuilder`: identifiers come from
  `AllocateMarkedContent`, which records the element in the page's marks, and the builder writes the
  parent tree at save. The tagger tells it to forget the annotations' keys (`ForgetAnnotationKeys`).
- **Read from a file**: the plain dictionaries under `/StructTreeRoot`. A page's identifiers come
  from two places, and both are read, because a file may say a mark in one and not the other.
  - The parent tree array: a non-null entry at index `i` means `i` is in use; a null is free.
  - The page's own content, parsed by `ContentReader` and read from its `BDC` and `DP` operators, by
    the `/MCID` of the property list written out in the operator or named in the page's
    `/Resources /Properties` (the same two readings as the text extractor's). A string or comment
    that says `/MCID 100000000`, and inline image data, are not operators and count for nothing. Forms
    the page draws number their own marks and are not read.
- **Numbering takes the lowest free number**, not one past the largest. The in-use numbers are a
  set, never a length. A real sequence with `/MCID 100000000` therefore takes only its own place; the
  new identifiers are 1, 2, ... round it, and the page's array grows by the identifiers there are and
  not by the largest one named. Counting up from the largest would have padded the array with a
  hundred million nulls. **Decision: there is no refusal for a large identifier**, because steering
  round it costs nothing and cannot clash; the file's own array is whatever length the file made it.
- **The one refusal** is a page that lists a widget and whose content cannot be read (the parser
  throws): its identifiers cannot be known. It is an `InvalidOperationException` naming the content,
  raised when the tagger opens, before the redraw and before anything is drawn, so the document is
  unchanged. A tree built this session never reads the page: the builder numbers its own marks.

### 11.5 A widget no element holds

A tagged document may never have put a widget in the tree. Its drawing has no element to belong to,
and is written as `/Artifact BMC ... EMC`. The text extractor skips it as an artifact. This does not
make the document conform, since the widget was already outside the tree, and it does not make it
worse.

### 11.6 Still refused

A signed form, as before. A document marked tagged with no tree, or whose `/StructTreeRoot` is not
a dictionary (section 5). A page whose content cannot be read (section 11.4).

### 11.7 Conformance

`pdfua-1-flattened-form` in `ConformanceCorpus`: a heading, a text field and a combo box with tool
tips, tagged through `PdfStructureBuilder`, flattened, claiming PDF/UA-1. **veraPDF passes it**
(`./verapdf-check.ps1`, veraPDF 1.30.2: 11 of 11 documents conform). Checked to have teeth: with
the tagging switched off, the same document fails PDF/UA-1.

### 11.8 Not done

- **The `/Span` is not given the field's value as `/ActualText`.** The marked content carries the text.
- **The text extractor reports the appearance's own sequence.** `PdfTextRun.Tag` and
  `MarkedContentId` name the innermost sequence, and an appearance usually opens `/Tx BMC` inside the
  `/Span`, so a run's `MarkedContentId` is null for a flattened value. The identifier is read from the
  content stream and the tree, as the tests do.
- **Tab order.** A flattened page keeps the `/Tabs` it had.
