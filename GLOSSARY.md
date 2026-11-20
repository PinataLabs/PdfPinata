# PdfPinata

A .NET library that writes PDF documents, lays them out, and reads existing ones back.

## Producing and drawing

**Layout rendering**:
Turning a document object model, a chart or `XGraphics` calls into PDF content. This is the sense "render" has in `PdfDocumentRenderer`, `XGraphicsPdfRenderer` and `PinataLayout.Rendering`.
_Avoid_: Rendering (unqualified), drawing

**Page rendering**:
Interpreting an existing PDF page's content and drawing it onto a canvas, which may produce pixels or a vector recording.
_Avoid_: Rendering (unqualified), viewing, displaying

**Rasterize**:
Produce pixels from a PDF page, whether by page rendering or by Ghostscript in the tests.
_Avoid_: Render (when pixels are meant)

**Device**:
The backend that page rendering draws onto. It receives filled and stroked paths, clips, glyph outlines and images, already resolved from the PDF.
_Avoid_: Renderer, target, surface

**Region**:
The rectangle of a page that page rendering draws, in points from the top-left of the page as displayed: after the crop box, `/Rotate` and `/UserUnit` are applied.
_Avoid_: Viewport, crop, tile

**Standard 14 font**:
One of the fourteen font names a PDF may use without embedding a font program, such as Helvetica or Times-Roman.
_Avoid_: Base 14, core fonts, built-in fonts

**Font program**:
The glyph outline data embedded in a PDF under `/FontFile`, `/FontFile2` or `/FontFile3`.
_Avoid_: Font file, embedded font (when the outline data is meant)
