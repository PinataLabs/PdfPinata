## Why

There is no way to change the size of a PDF page that already has content on it.

The API a caller reaches for first — `page.Size = PageSize.A5` — silently does the wrong thing.
Its setter (`PdfPage.cs:178`) writes a new `/MediaBox` and nothing else, so on a page that has been
drawn on or imported it crops the bottom-left A5's worth out of the A4 and discards the rest. No
exception, no warning. The `Width` and `Height` setters have the same hole.

The only working route today is to build a **second document**, add a page of the target size, and
draw the source page into it as an `XPdfForm`:

```csharp
var target = new PdfDocument();
var page = target.AddPage();
page.Size = PageSize.A5;
using var gfx = XGraphics.FromPdfPage(page);
gfx.DrawImage(XPdfForm.FromFile(source), new XRect(0, 0, page.Width, page.Height));
```

That draws the page correctly and loses everything else. Annotations are gone, links are gone,
and the result is a different document — the caller cannot resize a page in a document they are
already holding.

## What Changes

An in-place page resize that carries annotations and links with it.

- **`PdfPage.Resize(...)`** and **`PdfDocument.ResizePages(...)`** — resize a page, or every page,
  within the document that holds it. The page's content is wrapped in a form XObject and placed
  into the new box under a transform; annotations are transformed with it; destinations pointing
  at the page are found across the whole document and rescaled.
- **`PageResizeOptions`** — fit mode (`Fit`, `Fill`, `Stretch`, `None`), alignment, margin,
  auto-rotate, and switches for the annotation and destination passes; with presets `Default`
  (`Fit`, centred) and `Crop` (`None`, top-left).
- **Breaking:** `PdfPage.Size`, `Width` and `Height` setters throw `InvalidOperationException` when
  the page already has content, naming `Resize` in the message. Behaviour on a page with no content
  — the overwhelmingly common `AddPage(); page.Size = A4;` — is unchanged.
- **Orientation** stays two separate verbs. `page.Rotate = 90` already turns the paper losslessly
  and is untouched. `Resize` reshapes the box and refits the content into it.
  `PageResizeOptions.AutoRotate` bridges them: turn the content a quarter rather than letterbox it
  when the source and target boxes are of opposite aspect.
- A resize of an already-resized page **adjusts the existing wrapper** rather than nesting a second
  one inside it.
- A resize of an **encrypted, signed or tagged** document is refused. Encryption and signatures for
  the obvious reasons; a tagged document because the wrap breaks the structure tree's mapping into
  the content, and that damage is invisible — the page renders perfectly while its accessibility
  tree no longer describes it.

## Capabilities

### New Capabilities

- `page-resize`: changing the size, shape and orientation of a page that already carries content,
  in the document that holds it, with annotations and link destinations carried across; and the
  behaviour of the existing size setters on such a page.

### Modified Capabilities

<!-- openspec/specs/ is empty; there are no existing capability specs to amend. -->

## Impact

**Changed behaviour (breaking):** `PdfPage.Size`, `PdfPage.Width`, `PdfPage.Height` setters.
In-repo cost is one line — `PdfPinata.Test/Drawing/Layout/XTextFormatterTest.cs:43` sets `Size`
before drawing and is unaffected; nothing else in the repo assigns them on a page with content.
MigraDoc sets `pdfPage.Width`/`Height` on blank pages (`PdfDocumentRenderer.cs:216`) and is
unaffected.

**New source:**
- `PdfPinata/Pdf.Advanced/PdfPageResizer.cs` — the engine.
- `PdfPinata/root/PageResizeOptions.cs`, `PdfPinata/root/enums/PageFitMode.cs`.
- `PdfPinata/Pdf.Advanced/PdfDestinationWalker.cs` — the document-wide destination sweep.

**Modified source:**
- `PdfPinata/Pdf/PdfPage.cs` — setter guards, `Resize` facades.
- `PdfPinata/Pdf/PdfDocument.cs` — `ResizePages`, following the `PruneUnusedResources` /
  `ConsolidateImages` precedent of an explicit opt-in pass.
- `PdfPinata/Pdf.Advanced/PdfFormXObject.cs` — a same-document constructor. Today the
  page-to-form path is hardwired to an external document via `PdfImportedObjectTable`.

**Unaffected:** MigraDoc, both imaging backends, the content lexers, the font path.

**Documentation:** `docs/specs/page-resize.md`, recording what was built and what was deliberately
left out, per the convention in `docs/specs/`.
