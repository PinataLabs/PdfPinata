---
title: FAQ
description: Short answers to the questions people ask most about PdfPinata, with links to the pages that go into detail.
---

Short answers, each with a link to the page that explains more. If your question is about an error
message, look in [Troubleshooting](./troubleshooting.md) first.

## General

### What is PdfPinata?

A .NET library that creates PDF files, draws on their pages, and reads and changes existing PDF
files. It has two layers. `PdfPinata` is the low-level layer: you place text, lines, shapes and
images at coordinates you choose. `PinataLayout` is the high-level layer: you build a document of
sections, paragraphs and tables, and it breaks the content into pages for you. See the
[Overview](../overview.md).

### How does it relate to PDFsharp, PdfSharpCore and MigraDoc?

PdfPinata is a fork of PdfSharpCore, which is a port of PDFsharp. PinataLayout is the fork's name for
MigraDoc. Most of the API is the same, but the namespaces are different and fonts and images come
from a backend package that you register. See [Migrating](./migrating.md).

### What licence does it use?

MIT. The optional `PdfPinata.ImageSharp` backend depends on the last Apache-2.0 releases of
SixLabors.ImageSharp and SixLabors.Fonts. See [Licensing](./licensing.md).

### Is it free for commercial use?

Yes. The MIT licence allows commercial use. Check the licence of each backend you install; the
[Licensing](./licensing.md) page lists them.

## Setup

### Which backend do I choose?

Choose `PdfPinata.Skia` unless you have a reason not to. It is the recommended backend, and it works
next to any version of ImageSharp that your application already uses. Choose `PdfPinata.ImageSharp`
if you cannot ship native libraries: it is fully managed code. It is pinned to ImageSharp 2.1.x, so
it cannot share an application with ImageSharp 3 or later. See [Installation](../installation.md).

### Why must I register a backend at all?

The core package has no font or image code of its own. The backend reads font files and decodes
images. Register it once when your application starts, before you create a font or load an image.
If you forget, the first font or image operation throws an `InvalidOperationException` that names
the property to set.

### Do I need fonts installed on the machine?

The resolvers in the two backend packages look for font files in the operating system's font
folders. A server or container with no fonts installed has nothing for them to find. You can install
fonts in the image, give the resolver your own font files, or write your own `IFontResolver` that
serves fonts from your application. Every font that a document uses is embedded in the PDF, so the
reader of the PDF does not need the font. See [Fonts](../fonts-and-text/fonts.md).

### Does it run on Linux, macOS and Docker?

Yes. On Linux and in containers, install the SkiaSharp native assets package for Linux if you use
the Skia backend, and make sure the container has fonts or your resolver serves its own. See
[Platforms and deployment](./platforms-and-deployment.md).

### Does it run in Unity?

Every package except `PdfPinata.Signing` targets `netstandard2.1` as well as `net8.0` and
`net10.0`. The `netstandard2.1` build is there for Unity, which cannot load `net8.0` assemblies.
Signing needs .NET 8 or later, so it is not available in Unity. See
[Platforms and deployment](./platforms-and-deployment.md).

### Does it run on .NET Framework?

No. The lowest target is .NET Standard 2.1, which .NET Framework 4.x does not implement.

## What it can do

### Can it create PDF/A files?

Yes: PDF/A-1, PDF/A-2 and PDF/A-3, at levels A and B. PDF/A-4 is not supported. Call
`PdfDocument.ClaimConformance`. The library checks the rules it can check and throws on `Save` if
the document breaks one. See
[PDF/A](../standards/pdf-a.md).

### Can it create accessible (tagged) PDFs?

Yes. PinataLayout tags what it renders by default, and you can claim PDF/UA-1. See
[Accessibility](../standards/accessibility.md).

### Can it fill in and create forms?

Yes. You can create text fields, check boxes, radio buttons, combo boxes, list boxes and push
buttons, and set the values of fields in an existing form. It cannot flatten a form into page
content. See [Forms](../interactive/forms.md).

### Can it extract text from a PDF?

Yes. `PdfTextExtractor.ExtractText` returns the text of a page, and `ExtractRuns` returns each run
of text with its position. The runs come back in the order the file draws them. The library does not
work out columns or reading order. See [Text extraction](../existing-pdfs/text-extraction.md).

### Can it sign and encrypt PDFs?

Yes. The `PdfPinata.Signing` package signs documents with a certificate and can add a trusted
timestamp. The core package encrypts documents with a password, using RC4. It opens documents
encrypted with RC4 or AES, but it does not write AES. See
[Digital signatures](../standards/digital-signatures.md) and [Encryption](../standards/encryption.md).

### Can it merge and split PDFs?

Yes. Open the source with `PdfDocumentOpenMode.Import` and add its pages to another document. See
[Merge, split and assemble](../existing-pdfs/merge-split-and-assemble.md).

### Does it handle Arabic, Hebrew and other right-to-left text?

Yes. Right-to-left text comes out in the correct order with the core package alone. To join Arabic
letters, or to shape scripts such as Devanagari, also install `PdfPinata.HarfBuzz`. See
[International text](../fonts-and-text/international-text.md).

### Can it make bold or italic text when the font has no bold or italic face?

Yes. It draws the regular face with a thicker stroke or a slant. A real bold or italic face looks
better, so ship one if typography matters. See [Fonts](../fonts-and-text/fonts.md).

### Can it use OpenType fonts with PostScript (CFF) outlines?

Yes. TrueType fonts are subset, so the PDF holds only the glyphs it uses. Fonts with PostScript
outlines are embedded whole, which can add several megabytes for a large CJK font.

### Which PDF versions does it write?

PDF 1.4 by default. You can set `PdfDocument.Version` to a value from 12 to 17 (PDF 1.2 to 1.7) or
20 (PDF 2.0). Some features raise the version on their own.

### What unit are coordinates in? How do I set the DPI?

Coordinates are in points, 72 to the inch. `XUnit` converts from millimetres, centimetres and
inches. A PDF is a vector format and has no DPI of its own. A raster image has an effective
resolution that depends on the size you draw it at.

## What it cannot do

### Can it edit the existing text in a PDF?

No. A PDF page stores positioned glyphs, not editable paragraphs, and PdfPinata has no API to find
and replace text. You can draw new content over or under an existing page, add annotations and form
fields, and move, resize or delete whole pages. See
[Opening documents](../existing-pdfs/opening-documents.md) and
[Reading content streams](../existing-pdfs/reading-content-streams.md).

### Can it convert HTML to PDF?

No. There is no HTML or CSS engine. Build the document with [PinataLayout](../layout/documents-sections-and-styles.md)
instead, or use a separate HTML renderer.

### Can it convert a PDF to Word, HTML or an image?

No. PdfPinata writes PDF files; it does not render them. To make an image of a page, use a
rasterizer such as Ghostscript or PDFium. The library cannot print or display a PDF either.

### Does it keep bookmarks when I import pages?

No. Bookmarks belong to the source document, not to its pages, so importing pages leaves them
behind. Add them again in the new document. See
[Bookmarks and outlines](../interactive/bookmarks-and-outlines.md).

## Running it in production

### Is it thread-safe?

Use one document per thread. The backend, shaper and fallback settings on `GlobalFontSettings`, and
`ImageSource.ImageSourceImpl`, apply to the whole process, and the font caches behind them take a
lock. A single `PdfDocument`, `XGraphics` or PinataLayout `Document` has no locking, so do not use
one from two threads at the same time.

Set the global properties once, at startup. `GlobalFontSettings.FontResolver` refuses a new resolver
after a font has been created. Setting the same instance again is allowed, which helps in web
applications where startup code can run more than once.

### Does shaping change my layout?

Yes. Registering `PdfPinata.HarfBuzz` changes the width of measured text, so lines can wrap in
different places. Register it at startup, before you lay out anything, and keep it registered.

### Where do I report a bug?

Open an issue on [GitHub](https://github.com/PinataLabs/PdfPinata/issues). A short program or a
sample PDF that shows the problem makes it much faster to fix. See [Contributing](./contributing.md).
