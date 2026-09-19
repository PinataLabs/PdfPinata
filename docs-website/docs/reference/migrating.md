---
title: Migrating from PDFsharp, PdfSharpCore and MigraDoc
description: The package, namespace and behaviour changes to make when you move code from PdfSharpCore, MigraDocCore, PDFsharp or MigraDoc to PdfPinata.
---

PdfPinata is a fork of PdfSharpCore, which is a port of empira's PDFsharp. PinataLayout is the same
library that was called MigraDocCore in PdfSharpCore, and MigraDoc before that. Class and member names
are mostly unchanged, so most code moves across with new package references, new `using` directives
and one or two lines of start-up code.

This page lists what you have to change, then the behaviour changes you are likely to notice. For a
first project from scratch, read [Installation](../installation.md) instead.

## Change the package references

| Old package | New package |
| --- | --- |
| `PdfSharpCore` | `PdfPinata`, plus a backend: `PdfPinata.Skia` or `PdfPinata.ImageSharp` |
| `PdfSharpCore.Charting` | `PdfPinata.Charting` |
| `MigraDocCore.DocumentObjectModel` | `PinataLayout.DocumentObjectModel` |
| `MigraDocCore.Rendering` | `PinataLayout.Rendering` |

The other packages have no counterpart in PdfSharpCore: `PdfPinata.HarfBuzz` (text shaping),
`PdfPinata.Signing` (digital signatures) and `PdfPinata.EInvoice` (Factur-X and ZUGFeRD). All nine
packages have the same version number, so upgrade them together.

## Change the namespaces

Every namespace follows its package. Replace the first segment:

| Old namespace | New namespace |
| --- | --- |
| `PdfSharpCore`, `PdfSharpCore.Drawing`, `PdfSharpCore.Pdf`, ... | `PdfPinata`, `PdfPinata.Drawing`, `PdfPinata.Pdf`, ... |
| `PdfSharpCore.Utils` | `PdfPinata.Utils` |
| `MigraDocCore.DocumentObjectModel`, and the namespaces under it | `PinataLayout.DocumentObjectModel`, and the namespaces under it |
| `MigraDocCore.Rendering` | `PinataLayout.Rendering` |
| `MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes` | `PinataLayout.DocumentObjectModel.Shapes` |

The last row holds `ImageSource`, which you use to register an image backend. It lives in the
`PinataLayout.DocumentObjectModel.Shapes` namespace but ships in the core `PdfPinata` package, so you
need that `using` even in code that does not use PinataLayout.

:::note
PdfPinata 0.1.0 still had the doubled segment, as
`PinataLayout.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes`. If you are upgrading from
0.1.0, change that `using` to `PinataLayout.DocumentObjectModel.Shapes`.
:::

If your code came from empira's PDFsharp and MigraDoc rather than from PdfSharpCore, the same pattern
applies: `PdfSharp.Drawing` becomes `PdfPinata.Drawing`, `MigraDoc.DocumentObjectModel` becomes
`PinataLayout.DocumentObjectModel`, and `MigraDoc.Rendering` becomes `PinataLayout.Rendering`.
PdfSharpCore was ported from an earlier PDFsharp release, so code written for later PDFsharp releases
can use members that this library does not have.

## Register a backend at start-up

The core `PdfPinata` package has no imaging or font library of its own. PdfSharpCore depended on
ImageSharp and SixLabors.Fonts and chose an image source for you; PdfPinata does not. You must
register a backend once, before you create a font or load an image. With the Skia backend:

```csharp
using PinataLayout.DocumentObjectModel.Shapes;
using PdfPinata.Fonts;
using PdfPinata.Skia;
using PdfPinata.Utils;

GlobalFontSettings.FontResolver = new SkiaFontResolver();
ImageSource.ImageSourceImpl = new SkiaImageSource();
GlobalFontSettings.GlyphOutlineProvider = new SkiaGlyphOutlineProvider();
```

The ImageSharp backend has `ImageSharpFontResolver`, `ImageSharpImageSource<TPixel>` and
`ImageSharpGlyphOutlineProvider`, all in `PdfPinata.Utils`. `ImageSharpImageSource<TPixel>` has the
same name and type parameter as in PdfSharpCore.

- If you read `GlobalFontSettings.FontResolver` before it is set, it throws an
  `InvalidOperationException` that names the backend packages.
- The font resolver cannot change once a font has been created. Register it first thing, before any
  other code makes an `XFont` or a PinataLayout `Document`.
- `PlatformFontResolver` is gone. It never returned a font.
- The glyph outline provider is needed only for `XGraphicsPath.AddString` and for placing drop caps
  exactly. See [Fonts](../fonts-and-text/fonts.md).

## Open existing documents with the right mode

`PdfDocumentOpenMode.InformationOnly` is gone. It never did what its name said: the whole file was
always read, and the document could not be changed. Use `PdfDocumentOpenMode.ReadOnly`, which does
the same.

The open mode is now enforced. A document opened `ReadOnly` or `Import` used to let you add pages,
change settings and call `Save`, and the changes were quietly not written. Now these calls throw
`InvalidOperationException`:

- `AddPage`, `InsertPage`, `ImportPage` and the other page methods on `PdfDocument` and
  `PdfDocument.Pages`, including `Remove` and `RemoveAt`
- `Save`
- the `Version`, `PageLayout`, `PageMode` and `Language` setters

The message names the mode you used and the modes the operation needs:

```text
This document was opened with PdfDocumentOpenMode.ReadOnly and adding a page needs a document
opened with PdfDocumentOpenMode.Modify or PdfDocumentOpenMode.Append.
```

If you see it, change the mode you pass to `PdfReader.Open`: `Modify` to change a document, `Append`
to add a revision with an incremental save. `Close` is not affected. See
[Opening documents](../existing-pdfs/opening-documents.md).

## Behaviour you will notice

**Fonts are always embedded.** There is no setting to turn embedding off. `PdfDocumentRenderer` has
two constructors, `PdfDocumentRenderer()` and `PdfDocumentRenderer(bool unicode)`; the old overload
that took a `PdfFontEmbedding` value does not exist.

**`XGraphics.DrawString` draws one line.** A tab becomes a space, and every other control character,
including `\n` and `\r`, is dropped. `MeasureString` still splits on line feeds. For text over several
lines, use `XTextFormatter` or PinataLayout. See [Text layout](../drawing/text-layout.md).

**Right-to-left text is put in reading order for you.** Hebrew and Arabic runs are reordered by the
Unicode bidirectional algorithm without any extra package. If your code reversed such strings by
hand, remove that code, or the text will come out backwards. Joined Arabic letter forms need
`PdfPinata.HarfBuzz`. See [International text](../fonts-and-text/international-text.md).

**PinataLayout tags its output.** `PdfDocumentRenderer.TagContent` is `true` by default, so every
rendered PDF carries a structure tree for screen readers. `PdfPage.Resize` refuses a tagged document;
set `TagContent = false` if you resize pages after rendering. See
[Accessibility](../standards/accessibility.md).

**A page with content cannot be resized by setting its size.** The `PdfPage.Size`, `Width` and
`Height` setters throw `InvalidOperationException` on a page that already has content. They used to
write a new media box, which cropped the page and kept its bottom part. Call `page.Resize(size)`
to scale the content into the new size. See
[Page resizing and bleed](../existing-pdfs/page-resizing-and-bleed.md).

**`PageFormat.B5` is ISO B5.** It was the JIS sheet, 182 × 257 mm. It is now 176 × 250 mm, so a
section set to it reflows. To keep the old sheet, use `PageFormat.JISB5`.

**`PageSize.Executive` is 7.25 × 10.5 inches.** It was 7.5 × 10 inches.

**A stray heading row throws.** A table row with `HeadingFormat = true` that is not part of the run of
heading rows at the top of the table throws `InvalidOperationException` during layout. It used to be
ignored, so the heading did not repeat. Mark every row from the first down to the last heading row.

**Trimmed pages are bigger.** A page with `TrimMargins` set now gets room outside the bleed for crop
marks, 5 mm on each edge by default, and the marks are drawn. Set `page.MarkMargins.All = 0` to get
the page boxes earlier versions wrote.

**Opaque pages carry no transparency group.** A page gets a `/Group` entry only when something on it
uses transparency. Opening and saving a document no longer adds one to every page.

## Changed and removed members

These affect you only if you call or implement them:

- `XImage.AsBitmap()` is now `XImage.GetPixels()`, which returns a `PixelBuffer` of BGRA pixels
  instead of BMP file bytes.
- `IImageSource.SaveAsPdfBitmap(MemoryStream)` is now `PixelBuffer GetPixels()`. Change your
  implementation if you wrote your own image source.
- `IXGraphicsRenderer.DrawString` takes an `XPen` before the `XBrush`. Pass `null` for the pen to keep
  the old behaviour.
- `PdfDocumentOptions.EnableCcittCompressionForBilevelImages` is gone. It never had an effect; delete
  the assignment.
- `XGraphicsPath.AddString` now adds the glyph outlines to the path. It used to leave the path empty.
  It needs a glyph outline provider, as described above.
- `XGraphics.MeasureString(text, font, format)` now measures with the format you pass. It used to
  ignore it.

## Things to know

- **Keep the packages on one version.** All nine packages are released together with the same
  version number. Upgrade them together.
- **The ImageSharp backend needs ImageSharp 2.1.** It does not work with ImageSharp 3. If your
  application needs ImageSharp 3 for other work, use the Skia backend. See
  [Platforms and deployment](./platforms-and-deployment.md).
- **The Skia backend needs native assets.** Reference the `SkiaSharp.NativeAssets` package for each
  platform you run on; PdfPinata does not choose them for you.
- **Register backends in one place.** The font resolver is global to the process and cannot change
  after first use, so set it in your start-up code, not in a library or a test.
