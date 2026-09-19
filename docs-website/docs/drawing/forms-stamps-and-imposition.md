---
title: Forms, stamps and imposition
description: Reuse drawings with XForm, draw pages of other PDFs with XPdfForm, add watermarks, and put two pages on one sheet or lay out a booklet.
demos: [Imposition]
---

An `XForm` is a piece of drawing that is stored in the document once and placed as many times as you
like. Use it for anything that repeats: a logo, a stamp, a letterhead, a page border. An `XPdfForm` is
a page of another PDF that you can draw like an image, at any size and angle. Together they cover
watermarks, several pages on one sheet (n-up), and booklets. Both are in the core `PdfPinata`
package.

In a PDF both are *form XObjects*. The word "form" here has nothing to do with the fill-in forms on
the [Forms](../interactive/forms.md) page.

## Draw once, place many times

Create an `XForm` for a document, with a size in points. Draw into it with an `XGraphics` from
`XGraphics.FromForm`, in the form's own coordinates:

```csharp demo=Imposition snippet=create-form
```

An `XForm` is an `XImage`, so you place it with `DrawImage`. Each placement can have its own size,
position and transform:

```csharp demo=Imposition snippet=place-form
```

The Imposition demo places this rosette twenty times. The file holds one copy of the drawing and
twenty short references to it. The demo also draws the same twenty rosettes directly onto a page, and
prints how many bytes that takes, for comparison.

`DrawImage(form, x, y)` draws the form at its own size. The other `DrawImage` overloads scale it to the
rectangle you give.

## Draw a page of another PDF

`XPdfForm` opens a PDF and draws one of its pages. Set `PageNumber` to choose the page:

```csharp demo=Imposition snippet=pdf-form
```

`XPdfForm.FromFile(path)` opens a file, and `XPdfForm.FromStream(stream)` a stream. A second
`FromStream` overload takes a password for an encrypted file. `PageCount` gives the number of pages,
and `PointWidth` and `PointHeight` give the size of the current page. You can also select a page in the
path: `XPdfForm.FromFile("brochure.pdf#3")` opens page 3. If you pass a PDF file to
`XImage.FromFile`, you get an `XPdfForm` too.

An `XPdfForm` draws the page's content and nothing else. Links, form fields, comments and other
annotations on the source page are not copied. To copy whole pages with their annotations, import
them instead: see [Merge, split and assemble](../existing-pdfs/merge-split-and-assemble.md).

## Watermarks

A watermark is text or a picture drawn across the page. When you draw it decides whether it is under
the content or over it:

```csharp demo=Imposition snippet=watermark
```

```csharp demo=Imposition snippet=under-over
```

A watermark drawn first sits under the page. Anything opaque on the page, such as a filled panel, a
white background or a scanned image, hides it. A watermark drawn last sits over the page. Make it
partly transparent, or it hides what it marks. A background tint belongs under; a "DRAFT" stamp
usually belongs over.

### Watermark an existing PDF

To mark every page of an existing document, open it in `Modify` mode and draw on each page.
`XGraphicsPdfPageOptions.Append` draws over the existing content, and `Prepend` draws under it:

```csharp
PdfDocument document = PdfReader.Open("report.pdf", PdfDocumentOpenMode.Modify);
XFont font = new XFont("Arial", 60, XFontStyle.Bold); // any family your font resolver serves
XBrush brush = new XSolidBrush(XColor.FromArgb(90, 255, 0, 0));

foreach (PdfPage page in document.Pages)
{
    using XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
    gfx.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
    gfx.RotateTransform(-45);
    gfx.DrawString("DRAFT", font, brush, new XPoint(0, 0), XStringFormats.Center);
}

document.Save("report-draft.pdf");
```

These watermarks are ordinary page content. They are not PDF watermark annotations, and a reader
cannot hide them.

## Two pages on one sheet

To put two pages side by side, add a landscape page (`page.Orientation = PageOrientation.Landscape`)
and draw each source page into one half of it. Scale each page to fit its half, keep its proportions,
and centre it:

```csharp demo=Imposition snippet=two-up
```

The sheet refers to the source pages as form XObjects, so they stay vector graphics and their text can
still be selected and searched. The demo also draws a dashed fold line down the middle of the sheet.
The same method gives four or more pages to a sheet: divide the sheet into more slots.

## Booklets

A booklet is printed on both sides of each sheet, then folded in the middle. Each side of a sheet
holds two pages, and the pages must be placed so that they read in order once the sheets are folded
and nested. For four pages on one sheet, the front holds pages 4 and 1 and the back holds pages 2
and 3:

```csharp demo=Imposition snippet=booklet-sheets
```

Each side is then drawn like the two-up sheet:

```csharp demo=Imposition snippet=booklet-place
```

For a longer document, first add blank pages until the page count `n` is a multiple of four. Sheet
`s` (counting from 1) then holds:

| Side | Left | Right |
|---|---|---|
| Front | page `n + 2 - 2s` | page `2s - 1` |
| Back | page `2s` | page `n + 1 - 2s` |

For eight pages, sheet 1 holds pages 8 and 1 on the front and 2 and 7 on the back. Sheet 2 holds 6 and
3, then 4 and 5. Print the result double-sided.

## Things to know

- **A form belongs to one document.** You pass the document to the `XForm` constructor, and drawing
  the form into a different document throws. A form also cannot be drawn into itself.
- **A form is closed once it is placed.** The first `DrawImage` of a form finishes it, and after that
  you cannot draw into it again. `DrawingFinished()` finishes it yourself.
- **A form must be at least 1 point wide and high.** A smaller size throws.
- **`PageNumber` counts from 1.** `PageIndex` is the same setting counted from 0. Mixing them up puts
  the wrong page on the sheet.
- **Reuse an `XPdfForm`.** It holds the whole source document in memory. Open it once, change
  `PageNumber` for each page you need, and dispose it when you have finished.
- **In a tagged document, mark a watermark as an artifact.** Draw it inside
  `using (gfx.BeginArtifact())` so that screen readers skip it. See
  [Accessibility](../standards/accessibility.md).

## See it in action

[The Imposition demo](../demos.mdx#imposition) places one form twenty times, draws watermarks under
and over a page, puts two pages on one landscape sheet, and lays out both sides of a four-page booklet.

<details>
<summary>The full Imposition demo</summary>

```csharp demo=Imposition
```

</details>
