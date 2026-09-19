---
title: Pages, sizes and orientation
description: Add pages, give them a standard or custom size, turn them to landscape, and work in points, millimetres or inches.
demos: [Orientation]
---

A `PdfDocument` starts with no pages. You add each page with `AddPage`, set its size and
orientation, and then draw on it through an `XGraphics`. This page covers page sizes, landscape
pages, the `/Rotate` setting, units, and the page boxes.

## Add a page and set its size

`AddPage` returns the new page. Set its `Size` and `Orientation` before you draw anything on it,
then create an `XGraphics` for it:

```csharp demo=Orientation snippet=page-size
```

`PageSize` has the ISO A, B and C series (`A0` to `A10`, `B0` to `B10`, `C0` to `C10`), the RA and
SRA printing sizes, and the North American sizes `Letter`, `Legal`, `Ledger`, `Tabloid`,
`Executive` and `Statement`. `PageSizeConverter.ToSize(PageSize.A4)` gives the size in points as an
`XSize`.

`InsertPage(index)` adds a page at a position instead of at the end.

## Custom sizes

For a size that is not in `PageSize`, set `Width` and `Height`. Both are `XUnit` values, so you can
give them in any unit:

```csharp
PdfPage label = document.AddPage();
label.Width = XUnit.FromMillimeter(100);
label.Height = XUnit.FromMillimeter(150);
```

After this, `label.Size` is `PageSize.Undefined`.

## Portrait and landscape

`PageOrientation.Landscape` turns the page on its side: `A4` landscape is 842 points wide and 595
high. `Width` and `Height` report the page as the reader sees it, so read them after you set the
orientation and draw inside them.

## Units

PDF measures in points. One point is 1/72 inch, so an A4 page is about 595 by 842 points. An `XUnit`
holds a length and converts it to other units:

```csharp demo=Orientation snippet=units
```

To create a length, use `XUnit.FromPoint`, `FromMillimeter`, `FromCentimeter` or `FromInch`. An
`XUnit` converts to and from `double` without a cast, and the `double` is always in points.

`XGraphics` draws in points by default. To draw in another unit, pass an `XGraphicsUnit` when you
create it:

```csharp
XGraphics gfx = XGraphics.FromPdfPage(page, XGraphicsUnit.Millimeter);
gfx.DrawRectangle(XPens.Black, 20, 20, 170, 257);   // millimetres
```

The origin is the top left corner of the page, and y increases down the page.

## Rotate a page for display

`Rotate` asks the PDF reader to turn the page when it shows or prints it. It does not change the
page size, and it must be a multiple of 90 degrees:

```csharp demo=Orientation snippet=rotate
```

When `Rotate` changes, what you already drew turns with the page. In the demo, the reader shows the
A6 page on its side, and its text too.

If the page already has a `Rotate` value when you first draw on it, `XGraphics` allows for it. It
turns its own coordinates so that the origin is the corner the reader sees at the top left. What you
draw is then upright for the reader. This matters most for pages you import from another PDF.

For a page that should be wider than it is high, use `Orientation`, not `Rotate`.

## Page boxes

Each page has up to five boxes, all `PdfRectangle` values in points:

- `MediaBox` is the whole sheet. `Size`, `Width` and `Height` set it for you.
- `CropBox` is the area a reader shows.
- `BleedBox`, `TrimBox` and `ArtBox` are for print production: the area the printing reaches, the
  finished page after cutting, and the meaningful content.

For documents that go to a printer with bleed and crop marks, `TrimMargins` does this work for you.
See [Page resizing and bleed](../existing-pdfs/page-resizing-and-bleed.md).

## Things to know

- **The default page size depends on the machine.** A new page is A4 if the current region uses
  metric measurements, and Letter if it does not. A server and a developer's laptop can produce
  different sizes. Set `Size` on every page.
- **Set the size before you draw.** Setting `Size`, `Width` or `Height` on a page that already has
  content throws `InvalidOperationException`, because a new media box would crop the drawing, not
  scale it. To change the size of a finished page, use `PdfPage.Resize`. See
  [Page resizing and bleed](../existing-pdfs/page-resizing-and-bleed.md).
- **Keep the page that `AddPage` returns.** Each `XGraphics` draws on one page. For a second page,
  call `AddPage` again and create a new `XGraphics` from the new page.
- **One `XGraphics` for each page at a time.** Creating a second `XGraphics` for a page throws until
  you dispose the first one. To draw on a page again later, dispose the first `XGraphics`, then
  create a new one.
- **Draw behind existing content.** `XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Prepend)`
  draws beneath what is on the page. `Append`, the default, draws on top, and `Replace` starts with
  a blank page.
- **`DrawString` does not wrap.** On a small page, a line of text can run past the edge. Use
  [XTextFormatter](./text-layout.md) or a PinataLayout document for text that must fit a width.
- **For long documents, PinataLayout adds pages for you.** It breaks lines and pages and repeats
  headers and footers. See [Documents, sections and styles](../layout/documents-sections-and-styles.md).

## See it in action

[The Orientation demo](../demos.mdx#orientation) makes six pages in different sizes and both
orientations. Each page shows its size in points, millimetres and inches, and one page is rotated.

<details>
<summary>The full Orientation demo</summary>

```csharp demo=Orientation
```

</details>
