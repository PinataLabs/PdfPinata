---
title: Working with PDF objects directly
description: Read and write the dictionaries, arrays and names a PDF is built from, add entries the library has no API for, and export the images embedded in a file.
---

A PDF file is a set of objects: dictionaries, arrays, names, numbers, strings and streams. Pages,
fonts, images, links and bookmarks are all dictionaries with particular keys. PdfPinata's classes,
such as `PdfPage` and `PdfDocument`, cover most of what you need. When they do not, you can read and
write the objects themselves.

Use this layer when you need a PDF feature the library has no class for, when you want to read
something out of a file that no method returns, or when you want to see how a file is built. You
need to know the structure the PDF specification (ISO 32000) gives the objects you work on. The
types below are in the `PdfPinata.Pdf` namespace of the core `PdfPinata` package.

## The building blocks

| Type | A PDF | Notes |
|---|---|---|
| `PdfDictionary` | dictionary, `<< /Key value >>` | Entries are in `Elements`. `PdfPage` and the document catalog are dictionaries. |
| `PdfArray` | array, `[ ... ]` | Items are in `Elements`. |
| `PdfName` | name, `/Type` | The value always starts with a slash. |
| `PdfString` | string, `(text)` | |
| `PdfInteger`, `PdfReal` | number | |
| `PdfBoolean` | `true` or `false` | |
| `PdfReference` | indirect reference, `12 0 R` | Points at an object stored elsewhere in the file. `Value` is that object. |

A dictionary or stream can be stored **directly** inside another object, or **indirectly** as an
object of its own that others refer to by number. `IsIndirect` tells you which, and `Reference`
gives you the reference to an indirect object. You never assign object numbers yourself.

## Read entries

`Elements` has a typed getter for each kind of value. The getters that return a dictionary, an array
or an object follow an indirect reference for you:

```csharp
PdfDictionary? resources = page.Elements.GetDictionary("/Resources");
PdfDictionary? fonts = resources?.Elements.GetDictionary("/Font");

int rotate = page.Elements.GetInteger("/Rotate");
string type = page.Elements.GetName("/Type");       // "/Page"
bool hasCropBox = page.Elements.ContainsKey("/CropBox");
```

`Elements.Keys` lists the keys of a dictionary. The indexer, `Elements["/Key"]`, returns the item as
it is stored, which may be a `PdfReference`. `GetInteger`, `GetString` and `GetName` return `0` or an
empty string when the key is missing.

To walk every object in a document, call `document.Internals.GetAllObjects()`. The document catalog,
the root of the whole file, is `document.Internals.Catalog`.

## Add an entry the library has no API for

Build the objects, attach them, and save. This example makes a document open at its third page,
fitted to the height of the window. PdfPinata has no property for the catalog's `/OpenAction`, so
the example writes it:

```csharp
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

using PdfDocument document = PdfReader.Open("manual.pdf", PdfDocumentOpenMode.Modify);

// The destination: [page /FitV left]
PdfArray destination = new PdfArray(document);
destination.Elements.Add(document.Pages[2].Reference);
destination.Elements.Add(new PdfName("/FitV"));
destination.Elements.Add(new PdfInteger(0));

// The action: << /S /GoTo /D [...] >>
PdfDictionary action = new PdfDictionary(document);
action.Elements["/S"] = new PdfName("/GoTo");
action.Elements["/D"] = destination;

// Make the action an indirect object, and refer to it from the catalog.
document.Internals.AddObject(action);
document.Internals.Catalog.Elements.SetReference("/OpenAction", action);

document.Save("manual.pdf");
```

- A value you assign to `Elements["/Key"]` is stored directly, so `destination` is written inside
  the action.
- `document.Internals.AddObject` makes an object indirect. `SetReference` then stores a reference to
  it, and throws if the object is not indirect.
- `SetInteger`, `SetString`, `SetName`, `SetBoolean` and `SetRectangle` write simple values.
  `SetName` adds the leading slash if you leave it out.

For what a reader shows when the document opens, look first at `PageLayout`, `PageMode` and
`ViewerPreferences`, which need none of this. See
[Navigation and viewer preferences](../interactive/navigation-and-viewer-preferences.md).

## Export the images in a PDF

Images are stream objects in a page's resources, under `/XObject`, with `/Subtype /Image`. A JPEG
image is stored as a JPEG file, with the filter `/DCTDecode`, so exporting it means writing its bytes
to disk:

```csharp
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

using PdfDocument document = PdfReader.Open("brochure.pdf", PdfDocumentOpenMode.ReadOnly);

int count = 0;
foreach (PdfPage page in document.Pages)
{
    PdfDictionary? xObjects = page.Elements.GetDictionary("/Resources")
        ?.Elements.GetDictionary("/XObject");
    if (xObjects == null)
        continue;

    foreach (string key in xObjects.Elements.Keys)
    {
        PdfDictionary? image = xObjects.Elements.GetDictionary(key);
        if (image == null || image.Elements.GetName("/Subtype") != "/Image")
            continue;

        if (image.Elements["/Filter"] is PdfName { Value: "/DCTDecode" })
            File.WriteAllBytes($"image-{++count}.jpg", image.Stream.Value);
    }
}
```

Other images are stored as raw pixels, usually compressed with `/FlateDecode`.
`image.Stream.UnfilteredValue` returns the decompressed pixels. To turn them into a PNG, read
`/Width`, `/Height`, `/BitsPerComponent` and `/ColorSpace` from the image dictionary and build the
bitmap with an imaging library. Transparency is a separate image, named by the `/SMask` entry.

This loop does not find every image:

- an image used on several pages is found once per page. Compare `xObjects.Elements.GetReference(key)`
  to skip repeats;
- an image drawn inside a form XObject is in that form's own resources, not the page's;
- an inline image is written into the content stream and has no entry in the resources.

PdfPinata cannot render a page to an image. It can only export the images a page contains.

## Things to know

- **A name always starts with a slash.** Keys are names too: write `"/Resources"`, not
  `"Resources"`. `new PdfName("Type")` throws.
- **Numbers, names and strings cannot be changed.** They are immutable values. To change one,
  assign a new value to the entry.
- **A getter can add an entry.** Some getters take a `create` argument, and some properties use it.
  Reading `page.CropBox` on a page without a crop box adds an empty one. Use `ContainsKey` to test
  whether an entry exists.
- **`GetName` throws `InvalidCastException`** when the entry is not a name. `/Filter`, for example,
  can be an array of names.
- **Do not rely on object numbers.** Opening a document in `Modify` mode renumbers its objects, so
  a number you read before a save may name a different object after it.
- **Names and strings keep their bytes.** They are read one character per byte, so a name written in
  a legacy code page survives a round trip. Do not decode them as UTF-8 and write them back.
- **Changes made through `Elements` are tracked** for an incremental save. If you change an object
  some other way, call `MarkAsChanged` on it. See [Incremental saving](./incremental-saving.md).
- **This layer does not check your work.** A dictionary with a missing or misspelled key is saved as
  written, and a PDF reader may reject the file or ignore the entry.

To read the drawing operators of a page, rather than its objects, see
[Reading content streams](./reading-content-streams.md).

## See it in action

No demo is about the object model. [The Unicode demo](../demos.mdx#unicode) reads a font dictionary
back out of a saved file, and [the Archive demo](../demos.mdx#archive) reads the catalog's
`/Metadata` entry.
