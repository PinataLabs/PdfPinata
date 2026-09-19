---
title: Overview
slug: /
description: PdfPinata is a .NET library that creates PDF documents, draws on their pages, and reads and changes PDF files that already exist.
---

PdfPinata is a .NET library for PDF documents. You can use it to create a PDF from nothing, draw
text, images and vector graphics on its pages, and open an existing PDF to read it or change it. It
runs on .NET 8, .NET 10 and any runtime that supports .NET Standard 2.1, which includes Unity.

PdfPinata comes with **PinataLayout**, a document layout engine. With PinataLayout you describe a
document as sections, paragraphs, tables and charts, and the engine breaks it into lines and pages
for you.

## What you can build

- **Generated documents**: invoices, reports, statements, letters and labels. Start with
  [Getting started](./getting-started.md), then [Tables](./layout/tables.md) and
  [Documents, sections and styles](./layout/documents-sections-and-styles.md).
- **Drawn pages**: text, lines, shapes, gradients and images, placed exactly where you put them. See
  [Text](./drawing/text.md), [Shapes, pens and brushes](./drawing/shapes-pens-and-brushes.md) and
  [Images](./drawing/images.md).
- **Barcodes and charts**: see [Barcodes](./drawing/barcodes.md) and [Charts](./layout/charts.md).
- **Text in many languages**: right-to-left scripts, joined Arabic letters and font fallback. See
  [International text](./fonts-and-text/international-text.md).
- **Changes to existing PDFs**: merge, split and reorder pages, resize pages, stamp and impose
  them, extract text, and append a revision without rewriting the file. See
  [Opening documents](./existing-pdfs/opening-documents.md) and
  [Merge, split and assemble](./existing-pdfs/merge-split-and-assemble.md).
- **Interactive PDFs**: form fields, annotations, bookmarks and links. See
  [Forms](./interactive/forms.md) and [Annotations](./interactive/annotations.md).
- **Documents that meet a standard**: PDF/A for archiving, tagged PDF and PDF/UA for accessibility,
  digital signatures, password protection, and Factur-X and ZUGFeRD e-invoices. See
  [PDF/A](./standards/pdf-a.md), [Accessibility](./standards/accessibility.md),
  [Digital signatures](./standards/digital-signatures.md), [Encryption](./standards/encryption.md)
  and [E-invoicing](./standards/e-invoicing.md).

The [demo gallery](./demos.mdx) shows each of these as a PDF, with the code that made it.

## Two ways to make a page

**Draw the page yourself with `XGraphics`.** You add a page to a `PdfDocument`, get an `XGraphics`
for it, and call methods such as `DrawString`, `DrawLine` and `DrawImage` with coordinates. You
control every mark on the page. You also do all the layout: PdfPinata does not wrap long text across
pages and does not add page breaks. `XTextFormatter` can wrap text inside one rectangle. Choose this
way for forms, labels, certificates, drawings, and any page where each item has a fixed position.

**Describe the document and let PinataLayout lay it out.** You build a `Document` from sections,
paragraphs, tables, images and charts, and set styles on them. `PdfDocumentRenderer` then flows the
content onto as many pages as it needs, with headers, footers, page numbers, footnotes and a table
of contents. Choose this way for reports, invoices, letters and any document whose length depends
on its data. PinataLayout also tags what it draws, so its output is ready for
[accessible PDF](./standards/accessibility.md).

You can mix the two. PinataLayout renders onto an ordinary `PdfDocument`, so you can draw on its
pages with `XGraphics` after it has finished. In the other direction, `DocumentRenderer.RenderObject`
draws one PinataLayout object, such as a paragraph or a table, at a position you choose on a page you
draw yourself.

## The packages

The core package, `PdfPinata`, has no font or imaging code of its own. Every application also
installs one **backend** and registers it when it starts. The other packages are optional.

| Package | What it gives you |
|---|---|
| `PdfPinata` | The PDF object model, `XGraphics` drawing, reading and changing PDFs, forms, annotations, encryption and PDF/A. |
| `PdfPinata.Skia` | The recommended backend: fonts and images through SkiaSharp. Needs native libraries. |
| `PdfPinata.ImageSharp` | A fully managed backend: fonts and images through ImageSharp 2.1 and SixLabors.Fonts. |
| `PdfPinata.HarfBuzz` | Text shaping: kerning, ligatures and joined scripts such as Arabic. Works with either backend. |
| `PdfPinata.Charting` | Charts drawn with `XGraphics`. |
| `PdfPinata.Signing` | Digital signatures (CMS and PAdES) with the .NET cryptography libraries. .NET 8 and later only. |
| `PdfPinata.EInvoice` | Attaches a Factur-X or ZUGFeRD invoice to a PDF/A-3 document. |
| `PinataLayout.DocumentObjectModel` | The PinataLayout document model: `Document`, `Section`, `Paragraph`, `Table` and styles. |
| `PinataLayout.Rendering` | Lays out a PinataLayout document and renders it to PDF. |

[Installation](./installation.md) says which packages to install for each kind of application.

## Where PdfPinata comes from

PdfPinata is a fork of [PdfSharpCore](https://github.com/ststeiger/PdfSharpCore), which is a .NET
Core port of empira's PDFsharp and MigraDoc. MigraDoc is now called PinataLayout. The fork keeps
the PDFsharp API, with `PdfPinata` and `PinataLayout` namespaces, and moves font and image handling
into interchangeable backends. [Migrating](./reference/migrating.md) lists what changes when you
move code across.

## Where next

1. Read [Installation](./installation.md) to choose a backend and register it.
2. Follow [Getting started](./getting-started.md) to make your first PDF both ways.
3. Open the [demo gallery](./demos.mdx) to find a demo close to what you want to build.
4. Read [Fonts](./fonts-and-text/fonts.md) before you deploy. The fonts your server has decide what
   your documents look like.
