# Spec — gaps in the interactive layer

Tied to no upstream issue. This is the inventory of what turned up while writing the `Forms`,
`Annotations` and `Outline` demos for `SampleApp` — the three that cover the part of PDF a reader
*does* something with, rather than the part it draws.

Everything below was found by building something against the public API and then reading the bytes
it produced. Nothing here came from reading the source looking for problems, which is why it is
worth writing down: each item is a thing somebody trying to use the library would hit.

| # | area | gap | status |
|---|---|---|---|
| 1 | forms | the typed AcroForm API cannot author a form | **fixed** |
| 2 | forms | `PdfAcroFieldFlags` has no `Comb` | **fixed** |
| 3 | annotations | four PDFKit annotation types have no subtype here | **fixed** |
| 4 | annotations | no public way to add an annotation of an arbitrary subtype | **fixed** |
| 5 | annotations | no public way to give an annotation an appearance | **fixed** |
| 6 | annotations | `PdfFileAttachmentAnnotation.Icon`'s getter always throws | **fixed** |
| 7 | outlines | `PdfOutline.Opened` was never written | **fixed** |
| 8 | outlines | reading a document lost every expanded branch | **fixed** |
| 9 | general | `PdfInternals.CreateIndirectObject<T>()` always returns null | **fixed** |
| 10 | general | bookmarks and links do not survive page import | open, known |

Items 7 and 8 are done under `docs/specs/bookmarks-and-outlines.md` item 5; everything else except
item 10 is done under this one. Item 10 is out of scope here and stays where it was recorded:
`import-size-and-annotations.md` lists the same gap for page import.

Closing items 1, 2, 3 and 9 turned up four more defects that nothing here was looking for, all of
them in code that compiles, has no warnings and reads correctly. They are written up beside the
item that uncovered them: **`PdfAcroField.HasKids` could not see an indirect `/Kids`**, **a text
field drew its value into the field rather than into its widgets**, **`GetDescendantNames` took
every kid for a field**, and **`PdfTextField`'s colour properties were never read**. The pattern is
the document's own: a feature that silently does nothing looks exactly like one that works.

Two more entries at the end are not library gaps at all — they are traps that caught this app's own
first draft, kept here because both fail silently and both will catch the next person.

---

## Forms

### 1 — the typed AcroForm API cannot author a form — **fixed**

The largest gap of the three areas, and the reason `Forms` was the only demo in the app that wrote
dictionaries by hand.

`src/PdfPinata/Pdf.AcroForms/` ships nine public types. Every one of them could be *read*; none could
be *constructed*:

| type | constructors |
|---|---|
| `PdfAcroForm` | `internal PdfAcroForm(PdfDocument)`, `internal PdfAcroForm(PdfDictionary)` |
| `PdfAcroField` | `internal PdfAcroField(PdfDocument)`, `protected PdfAcroField(PdfDictionary)` |
| `PdfTextField`, `PdfCheckBoxField`, `PdfRadioButtonField`, `PdfComboBoxField`, `PdfListBoxField`, `PdfPushButtonField`, `PdfSignatureField` | all `internal` |
| `PdfWidgetAnnotation` | the class itself was `internal` |

There was no other seam either. `PdfAcroField.PdfAcroFieldCollection` derives from `PdfArray` and
added no `Add`; `PdfDocument.AcroForm` is get-only and returns `Catalog.AcroForm`;
`PdfDocument.Catalog` is `internal`. So the API's whole purpose was filling in a form somebody else
produced — a real and useful thing, and not the thing anybody asking *"how do I add a text box"*
wants.

#### What it looks like now

```csharp
PdfAcroForm form = document.GetOrCreateAcroForm();
form.NeedAppearances = true;
form.DefaultAppearance = "/Helv 9 Tf 0 g";
form.AddStandardFont("/Helv", "/Helvetica");

PdfTextField name = new PdfTextField(document)
{
    Name = "fullName",
    ToolTip = "Your name as it appears on your passport",
    Flags = PdfAcroFieldFlags.Required,
};
form.Fields.Add(name);
name.AddWidget(page, new PdfRectangle(gfx.Transformer.WorldToDefaultPage(box)));
name.Text = "Ada Lovelace";
```

Seven decisions in that are worth writing down.

**`GetOrCreateAcroForm` is a method, not a getter.** A getter that creates would write an
interactive form into every document that asked whether it had one, so `PdfDocument.AcroForm` is
left answering null and this is what a caller authoring a form calls. It is also the nineteenth
operation guarded by `EnsureCanModify` — giving a document an interactive form is changing it, so a
document opened `ReadOnly` or `Import` refuses, and `OpenModeEnforcementTests` has the row.

**A field's constructor writes `/FT` and the flag that defines its kind.** `/Btn` is a check box, a
radio group or a push button depending on two bits, and `/Ch` is a combo box or a list box depending
on one more — so `new PdfRadioButtonField(document)` sets `Radio` and `new PdfComboBoxField(document)`
sets `Combo`. Left to the caller, a field reads back through
`PdfAcroFieldCollection.CreateAcroField` as something else, which is the kind of mistake that
survives every unit test and fails in a reader.

**`AddWidget` always makes a separate annotation.** ISO 32000-1 section 12.7.3.1 allows a field with
exactly one widget to be merged into a single dictionary, and this deliberately does not: a caller
who may add a second widget later would otherwise have to know that the first one changes shape when
they do. It costs one indirect object per field. The widget is marked as printing, because a form
field that is not is one that vanishes on paper and nothing says so until somebody prints.

**A partial name may not contain a period.** `Name = "name.full"` is the obvious thing to write and
the one thing it cannot mean: a period joins two partial names into the path a field is known by, so
`Fields["name.full"]` splits it and goes looking for a field called `name` with a child called
`full`. Refused at the call, with a message saying to nest the fields instead — which is what the
path means and what `ADottedPathIsSpeltAsFieldsNestedInsideFields` shows.

**`Flags` gained a setter.** It could be read through the property and set only through
`Elements.SetInteger("/Ff", …)`, which loses the enumeration — so every caller writing flags was
already outside the typed API. `Name`, `ToolTip` (`/TU`) and `DefaultAppearance` (`/DA`) are settable
for the same reason.

**`PdfChoiceField.Options` and `PdfRadioButtonField.Options`.** A choice field with no `/Opt` has
nothing to choose between, and a radio group's `/Opt` is what `SelectedIndex` turns an index into.
Both read the export value of an entry written either as one string or as an `[export display]`
pair, and both write the plain form.

**`PdfAcroForm.AddStandardFont` knows which faces are symbolic.** A `/DA` string names its font by
the key it has in the form's `/DR`, so a form needs a resource dictionary before it can name a size
at all — six lines every caller would otherwise write identically. `/Symbol` and `/ZapfDingbats` are
left without an `/Encoding`, because WinAnsi would override the built-in one and ZapfDingbats is how
a check box draws its tick.

#### Three defects underneath it

None of these was reachable from outside before, because nothing outside could build a field with a
separate widget. All three are reachable *reading* a document that has one, which plenty of software
writes.

**`HasKids` could not see an indirect `/Kids`.** It answered false for anything that was not a
`PdfArray` outright, and `PdfAcroField.Fields` asks for the array with `VCF.CreateIndirect` — so
every field this library builds looked childless. A check box with a widget of its own therefore
took the "terminal field" branch of `PdfCheckBoxField.Checked`, wrote `/V` and `/AS` onto the field,
and left its widget showing whatever it showed before. The reference is followed now.

**`PdfTextField` drew its value into the field rather than into its widgets.** `RenderAppearance`
read `/Rect` off the field whatever the field's shape, so an unmerged one drew into a form of no size
and hung it where no reader looks. It renders onto each `/Kids` entry that has a rectangle now, and
onto the field itself when the field is its own annotation.

**`GetDescendantNames` took every kid for a field.** `/Kids` holds two different things and only one
of them has a name: a widget reached the walk with no `/T`, tripped a `Debug.Assert` and contributed
nothing, while a field whose only kids were its widgets took the "has children" branch and so never
reported its own name at all. A field is terminal when nothing underneath it contributed a name,
which says the same thing without having to ask what each kid is.

#### One more, found by looking at the page

`PdfTextField.BackColor`, `ForeColor` and `Font` were read only when the value changed. Setting a
colour on a field whose value was already in place did nothing; setting one on a field that never
gets a value did nothing ever. They redraw now, as `Text` always did, and so does the new
`BorderColor`.

`BorderColor` exists because an appearance is what a reader shows *in place of* building one from
`/MK`, so a text field decorated only through `/MK` lost its box the moment it was given a value.
That is exactly what the `Forms` demo looked like on the first run after this change: two fields with
values and no boxes, seven fields with boxes and no values. Nothing in the test suite would have
caught it.

The matching rule is that a text field with no background, no border and no value **removes** its
appearance rather than writing an empty one, so a field decorated through `/MK` alone is left for the
reader to draw. `PdfAcroField.OnWidgetAdded` is what makes the order not matter: a field described
before it is placed draws itself when the widget arrives.

#### Three more, found in review

**`Add` wrote no `/Parent`.** The same collection class is a form's `/Fields` and a field's `/Kids`,
and ISO 32000-1 Table 220 requires the back-reference of the second and forbids it of the first — so
the collection has to be told which it is, and could not work it out. Nothing here reads `/Parent`,
because every lookup walks *down* from `/Fields`; that is exactly why the omission was invisible
from inside. A reader assembling a field's fully qualified name walks up, and had nothing to walk.
`PdfAcroField.Fields` now names its owner when it materialises the collection, and
`PdfAcroForm.Fields` leaves it unnamed.

**The `Flags` setter assigned away what kind of field it was.** A `/Btn` is a push button, a radio
group or a check box by two bits and a `/Ch` is a combo or a list box by one; the constructor writes
them, and the setter replaced `/Ff` outright. So `new PdfComboBoxField(document) { Flags = Required }`
— the shape of the example at the top of this file — wrote a `/Ch` with no `Combo` bit, which *is* a
list box, and only reopening the file said so. Each class now declares a `KindMask` and a
`KindFlags`, and the setter keeps those bits while assigning the rest. The `Forms` demo re-stated
`Combo` and `Radio` in its own initialisers, which is the workaround this removes.

**The size guard was against zero where `XForm` refuses below one.** A rectangle between the two got
past `PdfTextField.RenderAppearanceOn` and threw `ArgumentNullException` out of a property setter,
and it would have passed a negative width to `DrawRectangle` on the way. The same off-by-one was in
all three of the annotations that draw themselves; it bites `PdfLineAnnotation` hardest, where a
hairline lying flat makes a box half its width high and no more. All four test against 1 now and
remove the appearance rather than throwing.

#### And one found later

**Only `Value` looked at `ReadOnly`.** `PdfAcroField.Value` refused a read-only field with
`InvalidOperationException`, and every typed property that fills one wrote `/V` without asking:
`PdfTextField.Text`, `PdfCheckBoxField.Checked`, `SelectedIndex` on the radio group, combo box and
list box, and the list box's `SelectedIndices`. So one field could be filled by one name and not by
another. All of them now go through `PdfAcroField.EnsureCanBeFilled`, which asks both questions a
value has to pass, read-only first and then the document's open mode, and nothing inside the library
sets a value on a field it did not just create, so no internal path needed routing round it.
`AcroFormFieldKindTests` has a test per property. Two things it does not do: `ReadOnly` still reads
the field's own `/Ff` and not one inherited from a parent field, so a kid of a read-only parent
is still filled; and a caller wanting a pre-filled field nobody can edit sets the value first and
the flag second, because the flag now stops the program as well as the reader.

#### What is still the caller's

`/MK` — the background and border a reader paints a field's box from when it builds the appearance
itself — has no wrapper, and nor does a push button's `/A` action. Those are the only two entries the
`Forms` demo still writes by name. **Flattening** a form into page content is still not offered, and
`PdfSignatureField` is still a field type rather than a signature implementation:
`PdfPinata.Signing` is what signs a document.

`src/PdfPinata.Test/Forms/AcroFormAuthoringTests.cs` has 41 tests. The one that matters saves the form
and reads it back through `PdfReader`, because a form that is right in memory and wrong in the file
looks identical from the calling side.

### 2 — `PdfAcroFieldFlags` has no `Comb` — **fixed**

The enum covered fifteen of the sixteen field flags. Bit 25, `Comb` — a text field divided into
`/MaxLen` equal cells, which is how a form draws boxes for a postcode or a card number — was absent,
so a caller wanting it wrote `field.Elements.SetInteger("/Ff", 1 << 24)` and lost the enum. It is
`Comb = 1 << (25 - 1)` now, and the `Forms` demo has a postcode field that uses it.

Two smaller things in the same file are unchanged. `DoNotSpellCheckTextField` and
`DoNotSpellCheckChoiseField` are both `1 << 22`, which is *correct* — bit 23 means the same for both
field types — but the enum is `[Flags]`, so `ToString()` picks one of the two names arbitrarily. And
`Choise` is a typo that cannot now be corrected without a breaking change.

---

## Annotations

Measured against [PDFKit's annotation API](https://pdfkit.org/docs/annotations.html), which is a
fair yardstick because it is a list somebody else wrote. That page documents eleven helper methods;
nine have a counterpart here and two do not.

### 3 — four subtypes had no counterpart — **fixed**

| PDFKit | here |
|---|---|
| `note` | `PdfTextAnnotation` |
| `link` | `PdfLinkAnnotation`, or `XGraphics.AddWebLink` |
| `goTo` | `XGraphics.AddNamedLink` / `AddDocumentLink` |
| `highlight` | `PdfHighlightAnnotation` |
| `underline` | `PdfUnderlineAnnotation` |
| `strike` | `PdfStrikeOutAnnotation` |
| `fileAnnotation` | `PdfFileAttachmentAnnotation` |
| `lineAnnotation` | `PdfLineAnnotation` |
| `rectAnnotation` | `PdfSquareAnnotation` |
| `ellipseAnnotation` | `PdfCircleAnnotation` |
| `textAnnotation` | `PdfFreeTextAnnotation` |

All four were appearance-bearing: a viewer will not draw a `/Square` from its `/Rect` alone, so
adding them meant writing appearance streams the way `PdfTextMarkupAnnotation` already does for its
four subtypes. That is the work, not the subtype wrapper — the same lesson
`text-markup-annotations.md` records for `/Highlight`.

Items 4 and 5 were what made this a wall: with `PdfGenericAnnotation` public and
`PdfAnnotation.SetAppearance` taking an `XForm`, a caller can write the subtype and draw its
appearance without leaving the public API, and a reader paints it. There is a test that does exactly
that and counts the pixels, beside one that shows the same annotation without an appearance
rasterizing to nothing at all.

All four now have classes that build the appearance themselves, so a caller need not.

#### `/Square` and `/Circle`

ISO 32000-1 puts both in one section, and so does this: `PdfSquareCircleAnnotation` carries `/IC`,
`/BS` and `/RD` and builds the appearance itself — through `XForm` and `XGraphics` rather than by
writing operators — rebuilding it whenever the rectangle, the colour, the interior, the border width
or the opacity changes. The two subclasses are a subtype name and one `DrawShape` override each.
Asked for neither a border nor a fill, either draws nothing and **removes** the appearance it had, so
a border set back to zero does not leave the last one on the page.

`/Circle` is a circle only when its rectangle is square — the specification names the subtype and
then describes an ellipse inscribed in `/Rect`. A test paints one in a 200 × 100 rectangle and checks
the four corners are *not* painted, which is the one thing a square would fail.

#### `/Line`

The one annotation whose **rectangle is not the caller's to set**. `/Rect` has to enclose the line
and everything drawn at its ends, and only the class knows how much an arrowhead takes, so it is
computed from `Start` and `End` every time either moves — the opposite of `PdfSquareCircleAnnotation`,
where the rectangle *is* the geometry. Assigning `Rectangle` on a line is therefore overwritten
rather than honoured, which is documented on the class and asserted in a test so that it reads as a
decision rather than a surprise.

Everything is read back out of the dictionary rather than kept in a field — `/L`, `/LE`, `/IC`, the
width in `/BS` — so a line survives a round trip through the file. `PdfSquareCircleAnnotation` kept
its interior and border width in fields and did not; it reads the dictionary too now, since an
annotation read from a file is handed back as its own class (`upstream-7-interactive-port.md` item 6).

`PdfLineEnding` is the whole of ISO 32000-1 Table 176 — ten members, including `None`, which is
written out loud rather than left absent: a line saying it ends in nothing is a line, where one
saying nothing is a line a reader may finish however it likes. Every ending is drawn, sized from the
line's own width and floored at one point so a hairline still gets a visible head. The two
`R`-prefixed arrowheads are the same triangle with the direction negated, which is the only thing
Table 176 changes about them.

The endpoints are in **default user space** — measured up from the bottom left, like `/Rect` — and
not the top-left world space `XGraphics` draws in. `SpaceTransformer.WorldToDefaultPage` gained an
`XPoint` overload for this: the rectangle overload encloses rather than maps, so which corner a given
point became is lost.

#### `/FreeText`

The one annotation whose `/Contents` are the thing drawn rather than a description of it, so
`PdfAnnotation.Contents` is now `virtual` and `PdfFreeTextAnnotation` overrides it to redraw. It is
also the only one of the four whose appearance needs a font, so one reaching a page with no
`GlobalFontSettings.FontResolver` fails the way every other piece of text in this library fails —
`Font` is resolved lazily, so constructing one does not oblige a caller who never draws it.

`/C` is this subtype's **background** rather than its ink, and the background is read from the
dictionary rather than through `PdfAnnotation.Color`, because that property answers black for an
annotation carrying no `/C` at all and a `/FreeText` with no background should be transparent rather
than a black box. The ink is `TextColor`, which is also what the border is stroked with, as the
specification has it: a free text annotation's border takes its colour from `/DA`.

`/DA` is required of the subtype and is written from the start, so an annotation nobody configures is
still well formed. It names the font `/Helv` rather than the face actually used, because a name in
`/DA` is looked up in an interactive form's `/DR` and a document with no form has no such dictionary
— while the appearance carries the real face in its own resources, so what a reader draws from `/AP`
is the face asked for either way.

Text is laid out with `XTextFormatter`, so it wraps and quads. `XParagraphAlignment.Justify` has no
`/Q` code, so it is written as left-justified and drawn justified: the drawing is ours and the entry
is a reader's, and left is what a reader regenerating the appearance would make of it anyway.

#### What is still missing

`/Polygon`, `/PolyLine`, `/Ink`, `/Caret`, `/Redact` and `/Popup` now have classes, and a markup
annotation can be given a pop-up through `PdfMarkupAnnotation.Popup` - see
`upstream-7-interactive-port.md` item 7. An annotation read from a file is now its own class rather
than a `PdfGenericAnnotation` (item 6 there). A `/Line` has no
caption (`/Cap`, `/CP`) and no leader lines (`/LL`, `/LLE`); a `/FreeText` has no callout line
(`/CL`) and no rich text (`/RC`).

Going the other way, this library has what PDFKit does not: `PdfSquigglyAnnotation`,
`PdfRubberStampAnnotation` with fifteen standard names, `PdfAnnotation.Opacity` (`/CA`),
`PdfAnnotation.Flags`, `PdfAnnotation.AppearanceState` (`/AS`), and many quadrilaterals under one
markup annotation via `AddQuad`.

### 4 — no public way to add an arbitrary subtype — **fixed**

`PdfAnnotations.Add` takes a `PdfAnnotation`. `PdfAnnotation` is `abstract` with no public way to
set `/Subtype`, and `PdfGenericAnnotation` — the one general-purpose subclass — was `internal`. So
an annotation type the library has no class for could not be added through the typed collection at
all, and the only route was `page.Elements["/Annots"]` built by hand.

This is also recorded in `text-markup-annotations.md` as the reason the StackOverflow workaround for
issue #342 could not be made to work. It is what made item 3 a hard wall rather than an
inconvenience: without it, a caller could not add a `/Square` even knowing exactly what one is.

`PdfGenericAnnotation` is now public, with constructors that take the subtype:

```csharp
var square = new PdfGenericAnnotation("/Square");   // or "Square"; the solidus is added
page.Annotations.Add(square);
square.Rectangle = new PdfRectangle(gfx.Transformer.WorldToDefaultPage(box));
```

It keeps its `PdfDictionary` constructor, which is what `PdfAnnotations` already used to give a
type to annotations read out of a document it has no class for — so the same class is now both
what an unknown subtype is read into and how one is written.

An empty subtype is refused at the call rather than written, because a dictionary with no
`/Subtype` is not something a reader can do anything with, and the failure would otherwise surface
as a silently ignored annotation.

`PdfAnnotations.Insert` is still commented out, so annotations can only be appended.

### 5 — no public way to give an annotation an appearance — **fixed**

Recorded originally as "no public way to build an appearance stream", which was not quite right and
worth correcting: the drawing could always be *built*, just never *handed over*.

`XForm` has public constructors and `XGraphics.FromForm` draws onto one, so an appearance stream was
always reachable as a drawing. What was not reachable was the form XObject underneath it —
`XForm.PdfForm` is `internal`, and every constructor of `PdfFormXObject` is too — so there was no
way to put the result in an annotation's `/AP`.

`PdfAnnotation` now takes it directly:

```csharp
var form = new XForm(document, new XSize(120, 60));
using (var gfx = XGraphics.FromForm(form))
    gfx.DrawRectangle(XBrushes.RoyalBlue, 0, 0, 120, 60);

square.SetAppearance(form);                 // /AP /N
square.SetAppearance("/Off", blankForm);    // one of a named set, and /AS names it
```

The named overload accumulates rather than replaces, because that is what a set of states is for: a
check box needs both of its states in the file at once and `/AS` picks between them. The unnamed one
replaces the set and clears `/AS`, since a single appearance is not one of a set and a state naming
something no longer there leaves a reader with nothing to draw.

`PdfFormXObject` stays internal. Nothing needs it now, and keeping it in means the appearance is
described by the same drawing API as the rest of the library rather than by a second one.

**A defect turned up under this.** `XForm.Finish` did `Gfx.Dispose()` unconditionally, so finishing
a form that had never been drawn on threw `NullReferenceException` — reachable before this change
through the public `DrawingFinished()`, and unavoidable after it, because *an empty appearance is a
real thing*: the "off" state of a check box or a radio button is an empty content stream. Now
`Gfx?.Dispose()`.

The raw dictionary route still works — `PdfDictionary.CreateStream(byte[])` is public — and nothing
in the repository needs it any more. The `Forms` demo used it for its check box, its radio group and
its push button, each of which needs one appearance per state; all three go through
`SetAppearance(state, form)` now, and the demo writes no content-stream operators at all.

`PdfAnnotation.AppearanceState` came out of that rewrite. `SetAppearance(state, form)` points `/AS`
at whatever it has just written, because an appearance nobody is showing is invisible and that is
almost never what a caller meant by adding one — but a radio group is exactly the case where several
widgets share a value and only one may be on, so there has to be a way to say which. It is `/AS`
with its solidus, and null when the annotation has a single appearance rather than a set.

Its setter refuses the empty name the way `SetAppearance(state, form)` always has. It used to read
the first character to decide whether to add a solidus and so threw `IndexOutOfRangeException`,
which names neither the parameter nor the mistake — and the test was redundant besides, because
`SetName` adds the solidus itself.

### 6 — `PdfFileAttachmentAnnotation.Icon`'s getter always threw — **fixed**

A plain defect, and the only one in this document that is not a missing feature.

```csharp
public IconType Icon
{
    get
    {
        var iconName = Elements.GetName(Keys.Name);
        if (iconName == null)
            return IconType.PushPin;
        return (IconType)(Enum.Parse(typeof(IconType), iconName));
    }
    set => Elements.SetName(Keys.Name, value.ToString());
}
```

Two faults, and together they mean the property can never be read successfully:

- **`GetName` returns the name with its slash.** `PdfName.Value` includes it, and `SetName` adds one
  if the caller left it off. So the getter parses `"/Paperclip"`, and
  `Enum.Parse(typeof(IconType), "/Paperclip")` throws `ArgumentException: Requested value
  '/Paperclip' was not found.`
- **`GetName` returns `String.Empty`, never `null`**, so the guard above it never fires. An
  attachment with no `/Name` at all reaches `Enum.Parse(typeof(IconType), "")`, which throws
  `ArgumentException: Must specify valid information for parsing in the string.`

Confirmed by direct probe: both paths throw. The setter is fine, which is why the `Annotations` demo
works — it only ever sets.

The same property existed three times over, and the other two were right.
`PdfTextAnnotation.Icon` did `value.Substring(1)` and checked `Enum.IsDefined` before parsing;
`PdfRubberStampAnnotation.Icon` did the same, character for character, differing only in the
enumeration named. Both were probed and both returned what was set.

#### The change

Three copies that drifted is the defect, so the fix is one implementation rather than a corrected
third copy: `PdfAnnotation.IconFromName<T>` — `private protected`, so it adds nothing to the public
surface — and all three properties now read through it.

```csharp
private protected static T IconFromName<T>(string name, T fallback) where T : struct
{
    if (string.IsNullOrEmpty(name))
        return fallback;

    string member = name[0] == '/' ? name.Substring(1) : name;

    return Enum.IsDefined(typeof(T), member)
        ? (T)Enum.Parse(typeof(T), member, false)
        : fallback;
}
```

The fallback is what differs between the three, and it is the only thing that does.
`PdfFileAttachmentAnnotation.IconType` has no `NoIcon` member, and does not need one: Table 184
gives `/Name` a default of `PushPin`, so an attachment without the entry is a push pin. The other
two fall back to `NoIcon`.

`Enum.IsDefined` rather than `Enum.TryParse`, which the two working copies also used and which is
worth keeping deliberately: handed a string of digits `TryParse` succeeds and returns that number as
the enumeration value, so a document naming its icon `/1` would read back as whichever member
happens to be 1.

The attachment's **setter** changed too, in the one way it also differed: it wrote
`value.ToString()` for any value, so a cast from an out-of-range integer put something like `/42`
into the file. It now removes the entry instead, which is what the other two do and what leaves a
reader on its documented default rather than on a name it cannot know.

Covered by `src/PdfPinata.Test/Annotations/AnnotationIconTests.cs`, 31 tests: every icon of all three
enumerations round-tripping, the absent and unrecognised cases falling back rather than throwing,
the digit-named icon that `TryParse` would have accepted, an out-of-range value not being written,
and every name being written with its solidus.

---

## Outlines

### 7 and 8 — fixed

`PdfOutline.Opened` was accepted by four constructors and four `Outlines.Add` overloads and never
written, so every bookmark tree arrived collapsed however it had been built; and `Initialize` never
read the state back, so opening a document and saving it again silently lost every expanded branch.

Both are fixed, with 7 tests. The full account — including why the `OpenCount` bookkeeping that was
supposed to do this could not have worked, and why the quantity it computed was the wrong one — is
`bookmarks-and-outlines.md` item 5.

### 10 — bookmarks and links do not survive page import

Not new, and not this work's to fix: an outline entry resolves to a page and a position rather than
to a named destination in the catalog's `/Names` tree, so importing a page into another document
leaves any bookmark pointing at it behind. `import-size-and-annotations.md` lists the same gap for
page import, and `bookmarks-and-outlines.md` lists it as out of scope.

Worth restating here only because the `Outline` demo makes it look solved: its *contents page* links
are named destinations, and would survive. Its *bookmarks* are direct page destinations, and would
not. The two views of the same structure differ in exactly this.

---

## General

### 9 — `PdfInternals.CreateIndirectObject<T>()` always returns null — **fixed**

```csharp
public T CreateIndirectObject<T>() where T : PdfObject
{
    T result = null;
    ConstructorInfo ctorInfo = null; // TODO
    if (ctorInfo != null)
    {
        result = (T)ctorInfo.Invoke(new object[] { _document });
        AddObject(result);
    }

    Debug.Assert(result != null, "CreateIndirectObject failed with type " + typeof(T).FullName);
    return result;
}
```

`ctorInfo` was assigned `null` and never assigned again, so the body was unreachable and the method
returned `null` for every type. The `Debug.Assert` fired in a debug build and said nothing in a
release one, so a caller who tried it got a `NullReferenceException` somewhere else entirely.

This is the obvious-looking method for the job item 1 needed doing, which is how it was found.

It is implemented rather than deleted, because implementing it is what the TODO intended and because
a public API that cannot work is worse than either. The constructor is looked up the way the rest of
the object model looks one up, in `PdfDictionary.DictionaryElements`: over `DeclaredConstructors`
rather than through `Type.GetConstructor`, because the one wanted is usually not public. The generic
parameter carries `[DynamicallyAccessedMembers]`, as `KeysMeta` and `KeyInfoAttribute` already do,
so trimming keeps the constructors.

A type that declares no constructor taking a `PdfDocument` is now an `InvalidOperationException`
naming the type and the alternative, rather than a null:

```csharp
PdfDictionary dictionary = new PdfDictionary(document);
document.Internals.AddObject(dictionary);
```

`AddObject` was always the working route, and remains the one to reach for when the type has no such
constructor.

---

## Two traps that are not the library's fault

Both cost time while building the demos. Neither produces an error; both produce a page that is
quietly wrong, which is the expensive kind.

### `/DA` with a zero font size is not portable

Zero means auto-size. On a single-line text field every viewer does something sensible. On a
**multiline** one, Ghostscript scales the first line to the height of the whole box — a two-line
value filled an entire A4 page and overprinted six other fields.

The form-level default `/DA` is a natural place to write `/Helv 0 Tf 0 g`, and it is what most
examples show. Name a real size on every field instead, and the form looks the same everywhere.

### a PDF content stream has no `arc` operator

`arc` is PostScript. A PDF path is built from `m`, `l`, `c`, `v`, `y`, `re` and `h`, and nothing
else — a circle is four Bézier curves with control points 0.5523 of the radius along the tangent.

The first draft of the `Forms` radio group drew its rings with `arc`. A viewer handed an operator it
does not know **draws nothing and reports nothing**, so the result was two invisible radio buttons
and a third that appeared only because its selected dot happens to be a ZapfDingbats glyph rather
than a path. Nothing in the test suite would have caught it; it was found by rasterizing the page
and looking at it.

Closing item 1 put this particular mistake out of reach for the demo, which now draws every
appearance stream through `XGraphics.FromForm` and writes no operators at all. The trap is still
there for anyone building a content stream by hand, which is why it stays written down.

---

## How these were found

Worth recording, because it is the argument for keeping the demos: **eight of the ten gaps above
were found by writing an ordinary caller and reading what came out.** None needed a fuzzer, a
specification review or a source audit. Items 6, 7, 8 and 9 are all defects in code that compiles,
has no warnings, and whose properties read back exactly as they were set.

Closing them found four more the same way, and the sequence is the argument twice over. Making the
AcroForm API able to author a form is what first built a field with a widget of its own, which is
what exposed `HasKids`, `RenderAppearance` and `GetDescendantNames` — three methods that had been
wrong for as long as they had existed and that no test could reach, because nothing could construct
the shape that reaches them. Then rendering the rewritten demo and looking at it is what exposed the
fourth: two fields drawn without the boxes the other seven had, because a colour set on a text field
was read only when its value changed.

The pattern is the one `demonstration-app.md` already names: a feature that silently does nothing
looks identical, from the calling side, to a feature that works. The only thing that tells them
apart is somebody drawing a page and looking at it.
