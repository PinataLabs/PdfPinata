---
title: "Accessibility: tagged PDF and PDF/UA"
description: PinataLayout tags every document it renders, and a PDF/UA-1 claim makes the writer check the document before it saves.
demos: [Accessibility]
---

A tagged PDF carries a structure tree: a description of what each part of the page is, such as a
heading, a paragraph, a table cell or a picture, in reading order. Screen readers, reflowing viewers
and text extractors read the tree instead of guessing from the position of each glyph. Without it, a
page is glyphs at coordinates, and the only order is the order they were drawn in.

PDF/UA-1 (ISO 14289-1) is the standard for accessible PDF. Public-sector buyers and accessibility
laws often ask for it. PdfPinata can tag a document for you, and it enforces a PDF/UA-1 claim rather
than only writing it into the file.

## PinataLayout tags by default

Every document rendered with `PdfDocumentRenderer` is tagged, because `TagContent` defaults to
`true`. You do not have to ask for it. The renderer maps the document model to structure elements:

| In the document | In the structure tree |
|---|---|
| A section | `/Sect` |
| A paragraph | `/P` |
| A paragraph whose `Format.OutlineLevel` is `Level1` to `Level6` | `/H1` to `/H6` |
| A paragraph whose `Format.OutlineLevel` is `Level7` to `Level9` | `/H6` (PDF has only six heading levels) |
| A run of list paragraphs | one `/L`, with an `/LI` per item and the bullet or number as its `/Lbl` |
| A table | `/Table`, `/TR`, and `/TD` for each cell |
| A cell in a row with `HeadingFormat = true` | `/TH` with `/Scope /Column` |
| A hyperlink | `/Link`, with a description on the link annotation |
| A footnote | `/Reference` where it is cited and `/Note` for the note, joined by an `/ID` |
| An image or chart with `AlternativeText` | `/Figure` with `/Alt` |
| Headers, footers, borders, shading, an image with no `AlternativeText` | an artifact, which a reader skips |

The same `OutlineLevel` that puts a heading in the bookmarks pane gives it its heading level:

```csharp demo=Accessibility snippet=headings
```

Headers and footers are drawn as artifacts, so a running head is not read aloud on every page:

```csharp demo=Accessibility snippet=running-head
```

## Tables

Mark the header row with `HeadingFormat`. Its cells become `/TH` elements, so a screen reader can
announce the column header before each value. The same flag repeats the row at the top of each page
the table continues onto. `Table.Summary` describes the shape of the table for a reader who cannot see
it:

```csharp demo=Accessibility snippet=table
```

## Alternative text for images

`AlternativeText` is on `Shape`, so it applies to images and charts. It decides how an image is
tagged:

- **Set:** the image is a `/Figure` with `/Alt`, and a screen reader reads the text.
- **Not set:** the image is drawn as an artifact. It is still visible, and a screen reader passes over
  it as decoration.

PinataLayout never writes a figure with nothing to say, and it never invents a description.

```csharp demo=Accessibility snippet=alt-text
```

## Claim PDF/UA-1

Tagging a document and conforming to PDF/UA-1 are different things. To make the claim:

1. Render the document with `TagContent` left at `true`.
2. Set `PdfDocumentRenderer.Language` to a language tag such as `"en-GB"`.
3. After rendering, set `Info.Title` on the `PdfDocument`.
4. Set `Options.UAConformance` to `PdfUAConformance.PdfUA1`.

```csharp demo=Accessibility snippet=claim
```

When you save, the writer checks the document and throws `InvalidOperationException` on the first
rule it breaks. The message names the rule and the fix. It checks that:

- the document is tagged;
- it has a title;
- it declares a language;
- every page is in the structure tree;
- every figure has alternative text;
- every note has an identifier, and no two elements share one;
- every link annotation has a description and is in the structure tree;
- headings do not skip a level (an `/H3` straight after an `/H1` is refused).

Two settings are made for you. On a PDF/UA claim, the save sets
`ViewerPreferences.DisplayDocTitle`, so a reader announces the title rather than the file name. And
every tagged page gets `/Tabs /S`, so the Tab key moves through links and fields in structure order.

To run the same checks at another time, call `PdfUaValidator.Validate(document)` (namespace
`PdfPinata.Pdf.Structure`). It does not set `DisplayDocTitle` for you, so set it first.

To make a document archival and accessible under one claim, use a PDF/A `A` level such as
`PdfAConformance.PdfA2A`. It applies the PDF/A rules and the PDF/UA-1 checks together. See
[PDF/A archiving](./pdf-a.md).

## Tag a page you draw yourself

On a page drawn with `XGraphics`, you tag the content yourself. Wrap each piece of content in
`BeginMarkedContent`, and each piece of decoration in `BeginArtifact`. Both return an `IDisposable`,
so a `using` block closes the scope even if an exception is thrown. In this example, `headingFont`,
`bodyFont` and `smallFont` are `XFont` objects:

```csharp
using PdfPinata.Pdf.Structure;

PdfDocument document = new PdfDocument();
document.Info.Title = "Invoice 2026-0042";
document.Language = "en-GB";

PdfPage page = document.AddPage();
using (XGraphics gfx = XGraphics.FromPdfPage(page))
{
    using (gfx.BeginMarkedContent(PdfTag.H1))
        gfx.DrawString("Invoice", headingFont, XBrushes.Black, 40, 60);

    using (gfx.BeginMarkedContent(PdfTag.P))
        gfx.DrawString("Payable within 30 days.", bodyFont, XBrushes.Black, 40, 90);

    using (gfx.BeginArtifact())
        gfx.DrawString("Page 1 of 1", smallFont, XBrushes.Gray, 500, 800);
}

document.Options.UAConformance = PdfUAConformance.PdfUA1;
```

A scope opened inside another becomes its child in the tree. `BeginMarkedContent` also takes an
alternative text as its second argument, for a `PdfTag.Figure`. When the structure does not match
the drawing order, as in a table drawn cell by cell, create the elements first with
`document.Structure.CreateElement(tag, parent)` and pass each element to
`BeginMarkedContent(element)`. A page with nothing drawn on it must still be in the tree: call
`document.Structure.RegisterPage(page)`.

## Things to know

- **A successful save is not a validator's verdict.** The writer does not check that all content is
  inside the structure tree, that the reading order makes sense, or that a page imported from another
  file is tagged. A page imported from an untagged document can pass every check and conform to
  nothing. Use [veraPDF](https://verapdf.org) before you rely on the claim.
- **Tagged documents cannot be resized.** `PdfPage.Resize` refuses a tagged document, because
  resizing moves the content away from where the tree says it is. If you render with PinataLayout
  and then resize pages, set `TagContent = false`. See
  [Page resizing and bleed](../existing-pdfs/page-resizing-and-bleed.md).
- **Hyphenated words stay whole.** When PinataLayout breaks a word at a hyphen, even across a page,
  it tags the parts as one word, so a screen reader or text extractor gets "demonstrate" and not
  "demon- strate".
- **PDF/UA-2 is not supported yet.** `PdfUAConformance.PdfUA2` exists, but PdfPinata cannot yet
  write a document that meets it in full. PDF/UA-2 requires every link and bookmark inside a
  document to point to a structure element. PdfPinata points them at pages, because the PDF 2.0
  standard does not yet define how a structure destination works. Claim PDF/UA-1 until that is
  settled.
- **An untagged document cannot make an accessibility claim.** With `TagContent = false`, a PDF/UA-1
  or PDF/A `A`-level claim is refused.

## See it in action

[The Accessibility demo](../demos.mdx#accessibility) renders a tagged report with headings, a table,
a described image and a link, claims PDF/UA-1, and prints the real refusal messages from documents
built to break one rule each.

<details>
<summary>The full Accessibility demo</summary>

```csharp demo=Accessibility
```

</details>
