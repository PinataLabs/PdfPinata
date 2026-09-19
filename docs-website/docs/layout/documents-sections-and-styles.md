---
title: Documents, sections and styles
description: Build a PinataLayout document from sections, paragraphs and tables, style it with named styles, and render it to PDF.
demos: [Invoice]
---

PinataLayout is the layout engine that sits on top of PdfPinata. You describe a document as a tree of
objects: a `Document` holds sections, and each section holds paragraphs, tables, images and text
frames. The renderer then decides where every line and page breaks, draws the headers and footers on
each page, numbers the pages and resolves cross-references. Use it for reports, invoices, letters and
anything else whose content flows from page to page. Use [XGraphics](../drawing/text.md) when you
need to put each mark at an exact position.

You need the `PinataLayout.Rendering` package and a backend. The
[Installation](../installation.md) page explains both. Register a font resolver before you build a
document, because the `Normal` style asks the resolver for its default font.

## The document model

A document is built from `Add` methods that return the object they created:

- `Document.AddSection()` starts a section. Every document needs at least one. You do not add pages:
  the renderer makes as many as the content needs.
- `Section.AddParagraph()`, `AddTable()`, `AddImage()`, `AddTextFrame()` and `AddPageBreak()` add
  content to the flow.
- `Paragraph.AddText()`, `AddFormattedText()`, `AddTab()`, `AddLineBreak()`, `AddHyperlink()` and the
  field methods add content inside a paragraph.

The types are in the `PinataLayout.DocumentObjectModel` namespace, tables in
`PinataLayout.DocumentObjectModel.Tables` and images and text frames in
`PinataLayout.DocumentObjectModel.Shapes`. Measurements are `Unit` values. `Unit.FromCentimeter`,
`FromMillimeter`, `FromPoint` and `FromInch` make one, and a string such as `"2.5cm"` converts to
one implicitly.

## Styles

Every document starts with a set of predefined styles: `Normal`, `Heading1` to `Heading9`, `Header`,
`Footer`, `Footnote`, `Hyperlink` and a few more. `StyleNames` holds their names as constants. Styles
inherit: `Heading1` is based on `Normal`, `Heading2` on `Heading1`, and so on. A property a style
does not set comes from its base style, so the font you give `Normal` reaches every other style
unless that style sets its own.

```csharp demo=Invoice snippet=document-and-styles
```

`Styles.AddStyle(name, baseStyleName)` creates a style of your own, based on an existing one. Give a
paragraph a style by setting `Paragraph.Style` to its name. Anything you set on
`Paragraph.Format` then overrides the style for that paragraph alone.

The predefined heading styles set `ParagraphFormat.OutlineLevel`, which makes every heading a PDF
bookmark. They do not make a heading bigger or bolder: give them a font size and weight yourself, as
the [Structure demo](../demos.mdx#structure) does. See
[Structure, contents and cross-references](./structure-and-cross-references.md).

## Page setup

Page size, orientation and margins belong to a section, through `Section.PageSetup`. A document whose
parts need different pages, such as a landscape appendix, has one section for each.

```csharp demo=Invoice snippet=page-setup
```

A section you leave unset gets A4 portrait with 2.5 cm margins (2 cm at the bottom), whatever the
machine's region. Set `PageFormat` to `PageFormat.Letter` for US Letter, and set `Orientation` to
`Orientation.Landscape` to turn the page. A later section takes every value it does not set from the
section before it. `SectionStart` decides whether a new section starts on the next page, the next
even page or the next odd page.

## Headers and footers

Each section has `Headers` and `Footers`, and each of those has three slots:

- `Primary` appears on every page of the section.
- `FirstPage` replaces it on the section's first page, if `PageSetup.DifferentFirstPageHeaderFooter`
  is `true`.
- `EvenPage` replaces it on even pages, if `PageSetup.OddAndEvenPagesHeaderFooter` is `true`.

You write a header once and it repeats on every page. Fields in it are resolved for each page, so a
footer can say "page 3 of 7":

```csharp demo=Invoice snippet=header-and-footer
```

`AddPageField()` prints the page number, `AddNumPagesField()` the number of pages in the document,
`AddSectionField()` and `AddSectionPagesField()` the section number and its page count, and
`AddDateField()` the date the document was rendered. The renderer knows the page count only after it
has laid out the whole document, which is why these values are fields rather than text.

## Position a block on the page

Most content flows. A `TextFrame` is a box you position yourself, relative to the page, the margins
or the current paragraph. The invoice uses one to put the address where a window envelope shows it:

```csharp demo=Invoice snippet=address-frame
```

## Render to PDF

`PdfDocumentRenderer` lays the document out and draws it into a `PdfDocument`:

```csharp
var renderer = new PdfDocumentRenderer(true) { Document = document };
renderer.RenderDocument();
renderer.PdfDocument.Save("invoice.pdf");
```

Pass `true` to the constructor. It selects Unicode encoding for all text. The parameterless
constructor selects WinAnsi, which covers Western European characters only. With WinAnsi, text is not
shaped, right-to-left text is not put in reading order, and no fallback font is used for a missing
character. Use `true` for any text that is not plain Latin script. See
[Unicode and font embedding](../fonts-and-text/unicode-and-embedding.md).

After `RenderDocument`, `renderer.PdfDocument` is an ordinary `PdfDocument`. You can set its viewer
options, add pages to it, draw on its pages or save it to a stream. To render into a document you
created yourself, assign `renderer.PdfDocument` before you call `RenderDocument`.

## Tagged output is the default

`PdfDocumentRenderer.TagContent` is `true` unless you change it. Every document it renders carries a
structure tree: headings are headings, tables have rows and cells, and headers and footers are marked
as decoration that a screen reader skips. See [Accessibility](../standards/accessibility.md), and
set `renderer.Language` (for example `"en-GB"`) if the document must pass PDF/UA.

A tagged document cannot be resized. `PdfPage.Resize` and `PdfDocument.ResizePages` refuse it,
because moving the content would leave the structure tree pointing at the wrong place. If you render
a document and then resize its pages, turn tagging off first:

```csharp
var renderer = new PdfDocumentRenderer(true) { Document = document, TagContent = false };
```

See [Page resizing and bleed](../existing-pdfs/page-resizing-and-bleed.md).

## Mix PinataLayout with XGraphics

You can use PinataLayout for part of a page and draw the rest yourself. `DocumentRenderer` lays a
document out without creating a PDF. `RenderObject` then draws one paragraph, table or shape on any
`XGraphics`, at a position and width you choose:

```csharp
var pdf = new PdfDocument();
PdfPage page = pdf.AddPage();
page.Size = PageSize.A4;

using (XGraphics gfx = XGraphics.FromPdfPage(page))
{
    // Use any family your font resolver serves.
    gfx.DrawString("Drawn with XGraphics", new XFont("Arial", 13), XBrushes.Black, 70, 80);

    var doc = new Document();
    Paragraph para = doc.AddSection().AddParagraph("Laid out by PinataLayout in a 12 cm column.");
    para.Format.Alignment = ParagraphAlignment.Justify;

    var layout = new DocumentRenderer(doc);
    layout.PrepareDocument();
    layout.RenderObject(gfx, XUnit.FromCentimeter(2.5), XUnit.FromCentimeter(4),
        XUnit.FromCentimeter(12), para);
}
```

To draw whole laid-out pages instead, call `PrepareDocument()` and then `RenderPage(gfx, pageNumber)`
for each page. Page numbers start at 1, and `layout.FormattedDocument.PageCount` says how many there
are. Apply `gfx.ScaleTransform` and `gfx.TranslateTransform` first to draw a page smaller, for
example as a thumbnail.

## Things to know

- **Register a font resolver first.** Creating a `Document` builds its styles, and the `Normal` style
  asks the resolver for its default font name. The demos' fonts, such as "Liberation Sans", come
  from the SampleApp's own resolver. Your application needs its own; see
  [Fonts](../fonts-and-text/fonts.md).
- **A section with no header of its own uses the previous section's.** The same is true for each of
  the three slots. To give a title page no header, make it the first section, or set
  `DifferentFirstPageHeaderFooter` and leave `FirstPage` empty.
- **An empty `FirstPage` or `EvenPage` slot is empty, not a fallback.** If you set
  `DifferentFirstPageHeaderFooter` and add nothing to `FirstPage`, the first page has no header. Filling
  `FirstPage` without setting the flag has no effect.
- **A new `PdfPage` follows the machine's region.** This matters only when you add pages yourself, as
  in the mixing example: a new page is A4 on a metric system and Letter otherwise. Set `page.Size`.
  Pages that `PdfDocumentRenderer` creates take their size from the section's `PageSetup`.
- **`RenderObject` draws untagged content.** If the result must be accessible, render through
  `PdfDocumentRenderer`, or see
  [Tag a page you draw yourself](../standards/accessibility.md#tag-a-page-you-draw-yourself).

## See it in action

[The Invoice demo](../demos.mdx#invoice) builds a one-page invoice: a letterhead and footer that
repeat, an address in a text frame, a tab-aligned reference block, a borderless item table with
merged total rows, and a shaded terms box.

<details>
<summary>The full Invoice demo</summary>

```csharp demo=Invoice
```

</details>
