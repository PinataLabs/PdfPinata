---
title: Forms (AcroForms)
description: Create fillable PDF forms with text, check box, radio, choice and button fields, and fill in or read the fields of an existing form.
demos: [Forms]
---

A PDF form (an AcroForm) is a set of fields that a reader lets a person fill in: text boxes, check
boxes, radio buttons, drop-down lists and buttons. PdfPinata can build a form from nothing, and it can
fill in or read a form that another program made. Everything on this page is in the core `PdfPinata`
package. The field types are in the `PdfPinata.Pdf.AcroForms` namespace and the widget type is in
`PdfPinata.Pdf.Annotations`.

## Create the form

A document has at most one form. `GetOrCreateAcroForm` makes it the first time you call it and returns
the same form after that. `PdfDocument.AcroForm` only reads: it returns null until the document has a
form.

```csharp demo=Forms snippet=create-form
```

`DefaultAppearance` is the font, size and colour a reader uses when it draws a field's value.
`/Helv 9 Tf 0 g` means "the font called `/Helv`, 9 points, black". `AddStandardFont` tells the form
which font `/Helv` stands for. Use one of the standard 14 PDF fonts here; every reader has them, so
nothing is embedded.

## Put a field on a page

A field and the box you see on the page are two objects. The field holds the name and the value. The
widget annotation is where the field is drawn, and one field can have several widgets. To place a
field:

1. Create the field, for example `new PdfTextField(document)`.
2. Give it a `Name`.
3. Add it to the form with `form.Fields.Add(field)`.
4. Call `field.AddWidget(page, rectangle)`.

If you call `AddWidget` before the field is in a form, it throws `InvalidOperationException`.

The rectangle is in PDF's own page coordinates, measured up from the bottom-left corner of the page.
`XGraphics` measures down from the top left, so convert with `gfx.Transformer.WorldToDefaultPage`:

```csharp demo=Forms snippet=place
```

## Text fields

`PdfTextField` holds a string in `Text`. `ToolTip` is the text a reader shows when the pointer is over
the field. `MaxLength`, `MultiLine` and `Password` are properties of their own, and other behaviour is
set through `Flags`:

```csharp demo=Forms snippet=text-fields
```

For a field divided into equal cells, such as a postcode, set `MaxLength` and the
`PdfAcroFieldFlags.Comb` flag together.

A text field draws its own box and value. `BackColor`, `BorderColor`, `ForeColor` and `Font` decide how
it looks, and the field redraws when any of them or `Text` changes:

```csharp demo=Forms snippet=style-text
```

## Check boxes

A check box shows one of two drawings: one for "on" and one for "off". You draw both with `XGraphics`
on an `XForm` and give them to the widget with `SetAppearance(state, form)`. `Checked` then sets the
value and picks the drawing that matches it.

```csharp demo=Forms snippet=check-box
```

The demo's `Appearance` helper makes an `XForm` the size of the box and draws on it with
`XGraphics.FromForm`.

## Radio groups

A radio group is one field with one widget per choice. `Options` lists the choices:

```csharp demo=Forms snippet=radio-field
```

Each widget's "on" state must have the same name as its choice, because the field's value is compared
with those names. `AppearanceState` sets which drawing each widget shows, and `SelectedIndex` sets the
field's value:

```csharp demo=Forms snippet=radio-widgets
```

## Combo boxes and list boxes

Both take their choices from `Options`. A combo box is a drop-down. Add `PdfAcroFieldFlags.Edit` to
let the person type a value that is not in the list.

```csharp demo=Forms snippet=combo-box
```

A list box shows several rows at once. Set `PdfAcroFieldFlags.MultiSelect` before you select more than
one row with `SelectedIndices`; without the flag, that throws `InvalidOperationException`.

```csharp demo=Forms snippet=list-box
```

Both draw their own appearance: a combo box shows the chosen option, and a list box shows its options
from `TopIndex` down with the chosen rows highlighted. They draw in the font size and colour that
`DefaultAppearance` names, unless you set `Font` or `ForeColor`. `BackColor` and `BorderColor` work as
they do for a text field. When you leave them unset, each widget's `/MK` background and border are used.

## Push buttons

A push button has no value. It exists to do something when clicked, and that action belongs to the
widget. PdfPinata has no property for a widget's action, so the demo writes the `/A` entry directly.
A reader does not draw a push button for you, so give it an appearance:

```csharp demo=Forms snippet=push-button
```

## Flags

`Flags` takes any combination of `PdfAcroFieldFlags`. The ones that apply to every field are
`ReadOnly`, `Required` and `NoExport`. The others apply to one kind of field, such as `Multiline` and
`Comb` for text, or `Edit`, `Sort` and `MultiSelect` for choices.

Some bits in the same entry decide what kind of field it is. The `Flags` setter keeps those bits
whatever you assign, so `new PdfComboBoxField(document) { Flags = PdfAcroFieldFlags.Required }` is
still a combo box.

To lock one field, set `field.ReadOnly = true`. To lock every top-level field in the form, call
`document.MakeAcroFormsReadOnly()`.

## Nest fields

A field can hold other fields. Their names join with periods into a full name, and you look a field up
by that full name. A field's own `Name` must not contain a period; if it does, the setter throws
`ArgumentException`. To get the name `applicant.name`, nest one field inside another:

```csharp
PdfTextField applicant = new PdfTextField(document) { Name = "applicant" };
form.Fields.Add(applicant);

PdfTextField fullName = new PdfTextField(document) { Name = "name" };
applicant.Fields.Add(fullName);
fullName.AddWidget(page, new PdfRectangle(new XRect(60, 700, 200, 20)));

// form.Fields["applicant.name"] now finds fullName.
```

## Fill in or read an existing form

Open the document in `Modify` mode, find each field by its full name, and set the typed property:

```csharp
using PdfDocument document = PdfReader.Open("application.pdf", PdfDocumentOpenMode.Modify);
PdfAcroForm form = document.AcroForm;

foreach (string fieldName in form.Fields.DescendantNames)
    Console.WriteLine(fieldName);

if (form.Fields["applicant.name"] is PdfTextField applicantName)
    applicantName.Text = "Ada Lovelace";

if (form.Fields["subscribe"] is PdfCheckBoxField subscribe)
    subscribe.Checked = true;

document.Save("application-filled.pdf");
```

`PdfReader` is in `PdfPinata.Pdf.IO`. `DescendantNames` lists the full name of every field that has no
child fields. To read a value, use the same typed properties: `Text`, `Checked`, `SelectedIndex` or
`SelectedIndices`. `Value` returns the raw PDF object.

If the document is signed, `Save` breaks the signatures. Open it in `Append` mode and save with
`SaveIncremental` instead; see [incremental saving](../existing-pdfs/incremental-saving.md). If a
signature certifies the document and does not allow form filling, setting a value throws
`InvalidOperationException`.

## Things to know

- **Flattening is not offered.** PdfPinata cannot turn fields into ordinary page content. Making the
  fields read-only is the nearest option.
- **A text field needs a font resolver.** Creating a `PdfTextField`, or reading one from a file, asks
  the registered font resolver for its default font. Register one first; see
  [Installation](../installation.md).
- **Name a real font size.** A size of 0 in `DefaultAppearance` means "fit to the box", and readers
  do different things with it. Ghostscript scales the first line of a multi-line field to the height of
  the whole box.
- **The value a text field draws is one line.** PdfPinata draws the value from the top-left of the
  box in `Font` and does not wrap it.
- **Leave `NeedAppearances` unset.** It asks a reader to discard the appearance of every field and
  build its own. Chrome and Edge do exactly that, for buttons and check boxes too, so a form that sets
  it shows there as bare text. PDF/A forbids it.
- **Colours are written twice.** `BackColor` and `BorderColor` go into the appearance the field draws
  and into each widget's `/MK`. Some readers, such as Firefox, draw their own field on top from `/MK`
  alone.
- **A plain text field has no drawing of its own.** If it has no background, no border and no value,
  PdfPinata removes its appearance so that the reader draws it from `/MK`.
- **`Password` hides what is typed, nothing more.** The value is still stored in the file. Do not use a
  form field to keep a secret.
- **A field's children and its widgets are two lists.** In the file, a field's `/Kids` holds both its
  child fields and its widget annotations. `Fields` lists only the child fields, and `Widgets` lists
  only the widgets.
- **Every widget prints.** `AddWidget` sets the print flag, so the field appears on paper.
- **Coordinates go up from the bottom.** Widget rectangles are in PDF page coordinates, not the
  top-left coordinates `XGraphics` draws in.

## See it in action

[The Forms demo](../demos.mdx#forms) builds one page with every kind of field, and a second page that
lists what the form API can and cannot do.

<details>
<summary>The full Forms demo</summary>

```csharp demo=Forms
```

</details>
