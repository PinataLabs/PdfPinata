---
title: Page resizing and bleed
description: Change the page size of a finished document without cropping it, set the five page boxes, and prepare pages for a press with bleed and crop marks.
demos: [PageResize, Bleed]
---

This page covers two jobs that change the paper a page sits on. **Resizing** changes the size of
pages that already have content, for example to turn an A4 document into A5 or to put a batch of
mixed sizes onto one paper size. **Bleed** prepares a page for a commercial press: the artwork runs
past the edge of the page onto a larger sheet, which is then cut to size along crop marks.

Both are in the core `PdfPinata` package. `PageSize`, `PageOrientation` and `PageResizeOptions` are
in the `PdfPinata` namespace.

## Resize every page of a document

`ResizePages` changes the size of every page and scales the content to fit:

```csharp demo=PageResize snippet=resize
```

The content is scaled, not cropped. Everything that refers to a position on a page moves with it:

- the rectangles of links and other annotations on the page;
- every destination that points at the page, wherever it is held: links on other pages, bookmarks,
  named destinations and the action that runs when the document opens.

A destination keeps its zoom level. A link that showed a page at 100% still does after a resize.

`ResizePages` also takes a size in points as an `XSize`. To resize one page, call `page.Resize`
with the same arguments. To resize every page, use `ResizePages`: finding the destinations that
point at a page means reading the whole document, and `ResizePages` does that once rather than once
for each page.

## Choose how the content fits

The optional `PageResizeOptions` decides how the old page is mapped onto the new one:

| Property | Default | What it does |
|---|---|---|
| `Fit` | `PageFitMode.Fit` | `Fit` scales until the whole page fits. `Fill` scales until the new page is covered and crops the overflow. `Stretch` scales each direction on its own and distorts the content. `None` does not scale and crops what does not fit. |
| `Alignment` | `PageAlignment.MiddleCenter` | Where the content sits when there is space left over, and which part is kept when there is not. |
| `Margin` | 0 | An empty border on all four sides of the new page. |
| `AutoRotate` | `false` | Turns the content a quarter when the old and new pages are of opposite shape, landscape against portrait, instead of shrinking it. |
| `ScaleAnnotations` | `true` | Moves the annotations with the content. |
| `ScaleDestinations` | `true` | Moves the destinations that point at the page. |

To put a batch of mixed pages onto A4 with a 10 mm border:

```csharp
using PdfPinata;
using PdfPinata.Drawing;

document.ResizePages(PageSize.A4, PageOrientation.Portrait, new PageResizeOptions
{
    Margin = XUnit.FromMillimeter(10),
    AutoRotate = true,
});
```

`PageResizeOptions.Crop` keeps the content at its size and crops it to the new page, anchored at
the top left.

## Why `Size`, `Width` and `Height` refuse

Setting `page.Size`, `page.Width` or `page.Height` on a page that already has content throws
`InvalidOperationException`, and the message names `Resize`. Those setters only write a new page
rectangle. On a page with content that crops the page from its bottom-left corner, which keeps the
foot of the page and throws away the heading. Set the size before you draw, or call `Resize` after.

## What cannot be resized

`ResizePages` checks the whole document before it changes anything, and refuses:

- **a document read from an encrypted file.** A document you created and set a password on is fine.
- **a signed document.** The signature would no longer match the document.
- **a tagged document**, one with a structure tree for screen readers and other assistive
  technology. Resizing moves each page's content into a wrapper that the structure tree cannot
  point into. The page would look correct and print correctly, but a screen reader would find
  nothing on it, and no visual check would show the damage. So the library refuses instead.
- **a document not open for modification**, or a page with an `XGraphics` still open on it.

**PinataLayout tags its output by default.** To resize a document you render with PinataLayout, set
`TagContent = false` on the `PdfDocumentRenderer` before you call `RenderDocument`. Better still, set
the page size you want in the document's page setup, so that PinataLayout lays the text out for that
size in the first place. See [Documents, sections and styles](../layout/documents-sections-and-styles.md).

## The five page boxes

A PDF page has up to five rectangles, each a `PdfRectangle` property of `PdfPage`:

| Box | Means |
|---|---|
| `MediaBox` | The whole sheet. Every page has one. |
| `CropBox` | The part a PDF reader shows. When a page has none, it is the media box. |
| `BleedBox` | How far artwork may run past the finished page. |
| `TrimBox` | The finished page, where the sheet is cut. |
| `ArtBox` | The meaningful content of the page. |

You rarely set these by hand. The next section sets all five for you.

## Add bleed and crop marks

Set `TrimMargins` on a page to give it bleed. The sheet written to the file grows by the margin on
each edge, but the page you draw on does not:

```csharp demo=Bleed snippet=bleed
```

The drawing origin stays at the top-left corner of the **trimmed** page. Code that draws inside the
page is the same as on a page without bleed, and `page.Width` and `page.Height` still report the
trimmed size. To make artwork run off the edge, draw at negative coordinates, or past the width and
height:

```csharp demo=Bleed snippet=negative-coordinates
```

Outside the bleed there is room for printer's marks, set by `MarkMargins`:

```csharp demo=Bleed snippet=mark-margins
```

`MarkMargins` is 5 mm on each edge unless you change it. When you save, the library draws eight crop
marks there to show the trimmer where to cut. Set `MarkMargins` to zero to leave out both the room
and the marks. To give every new page of a document the same bleed, set
`document.Settings.TrimMargins`.

When the document is saved, a page with trim margins gets all five boxes. `MediaBox` and `CropBox`
are the whole sheet, `BleedBox` is the sheet inside the mark margins, and `TrimBox` and `ArtBox` are
the trimmed page. A page without trim margins gets none of this.

## Things to know

- **Resizing scales. It does not reflow.** Text keeps its line breaks and becomes smaller or larger.
  To lay text out again for a new size, render it again at that size.
- **Form fields with a fixed font size do not scale** with the page, so their text looks
  proportionally larger after a shrink. Fields with automatic font size are fine. Border widths do
  not scale either.
- **Resizing twice does not stack.** A second resize replaces the first, so A4 to A5 and back to A4
  returns the page exactly to where it started.
- **Text extraction finds nothing on a resized page.** The extractor reads the page's own content
  and does not look inside the wrapper that resizing creates. Extract text before you resize. See
  [Text extraction](./text-extraction.md).
- **Reading a box property can create the box.** On a page that has no crop box, reading
  `page.CropBox` adds an empty one to the page. The same is true of `BleedBox`, `TrimBox` and
  `ArtBox`. To test whether a page has one, use `page.Elements.ContainsKey("/CropBox")`.
- **Draw a trimmed page in points.** An `XGraphics` on a page with trim margins must use the default
  `XGraphicsUnit.Point`, or the drawing origin is placed wrongly.
- **Assigning `TrimMargins` or `MarkMargins` copies the four values.** Changing the object you
  assigned from afterwards does not change the page.

## See it in action

[The PageResize demo](../demos.mdx#pageresize) shrinks a two-page A4 document with a link to A5.
[The Bleed demo](../demos.mdx#bleed) draws a photograph off three edges of an A5 page and lists the
boxes the saved page carries.

<details>
<summary>The full PageResize demo</summary>

```csharp demo=PageResize
```

</details>

<details>
<summary>The full Bleed demo</summary>

```csharp demo=Bleed
```

</details>
