---
title: Text extraction
description: Read the text of a PDF page back with PdfTextExtractor, with the position, size and structure tag of each run, and know what it leaves out.
demos: [Extract]
---

`PdfTextExtractor` reads the text on a page: what it says, and where on the page it says it. Use it
to index documents for search, to check in a test that a generated document says what it should, or
to pull values out of a PDF that another program wrote.

It is in the `PdfPinata.Pdf.Extraction` namespace of the core `PdfPinata` package. It needs no
backend and no fonts installed, because it reads the fonts embedded in the file.

## Get the text of a page

Open the document in any mode and pass a page to `ExtractText`. `ExtractRuns` returns the same text
as a list of runs, each with its position:

```csharp demo=Extract snippet=extract
```

To read a whole document:

```csharp
using PdfPinata.Pdf;
using PdfPinata.Pdf.Extraction;
using PdfPinata.Pdf.IO;

using PdfDocument document = PdfReader.Open("invoice.pdf", PdfDocumentOpenMode.ReadOnly);
foreach (PdfPage page in document.Pages)
    Console.WriteLine(PdfTextExtractor.ExtractText(page));
```

`ExtractText` joins runs in a simple way. Runs that share a baseline go on one line, with a space
between them when the gap is wider than a fifth of the type size. A run on a new baseline starts a
new line.

## Get the position of each run

`ExtractRuns` returns a `PdfTextRun` for each text-showing operator on the page, in the order they
were drawn:

```csharp demo=Extract snippet=read-runs
```

| Property | Holds |
|---|---|
| `Text` | What the run says. |
| `Origin` | Where the run's baseline starts, in PDF units: points measured from the **bottom-left** corner of the page, with Y growing upwards. |
| `Width` | How far the run extends along its baseline, in the same units. |
| `FontSize` | The size the text appears at, after any scaling. Text drawn at 9 points under a twofold scale reports 18. |
| `FontName` | The name the page uses for the font, such as `/F0`. This is not the typeface name. |

`XGraphics` measures from the top-left corner, so the Y values are the other way up. On an unrotated
page, `page.Height.Point - run.Origin.Y` converts a run's Y to the top-down value.

A run is one text-showing operator in the file, not one word or one glyph. PdfPinata usually draws
one run per `DrawString` call, so a left-aligned line that `XTextFormatter` lays out is one run.
Other producers draw a word, a line or a single letter per operator.

## Read tagged documents

A tagged PDF marks each piece of text with what it is: a paragraph, a heading, a table cell, or an
**artifact**, which is page furniture such as a running head or a page number. The extractor reads
these marks:

- `Tag` is the structure type the run was drawn inside, as a `PdfTag` from
  `PdfPinata.Pdf.Structure`, or `null` when the run is not marked.
- `IsArtifact` is `true` for page furniture.
- `ActualText` is the text the document says the run stands for, when it says so.
- `MarkedContentId` links the run to its element in the structure tree.

`ExtractText` uses them. It leaves out artifacts, so running heads and page numbers do not appear
in the middle of the text. Where the document gives an `ActualText`, it uses that text once instead
of the glyphs, so a word hyphenated across two lines comes back as one word. `ExtractRuns` still
returns the artifacts, for callers who want them.

PinataLayout tags its output by default, so documents it renders extract cleanly. A document with
no tags extracts exactly as it would without this feature. See
[Accessibility](../standards/accessibility.md).

## What the extractor does not do

- **It does not put text in reading order.** Runs come back in the order the producer drew them. On
  a two-column page drawn a line at a time, the two columns come back interleaved. The Extract demo
  shows this. Grouping runs into columns and paragraphs is layout analysis, which this library does
  not do. For a tagged document, the structure tree records the reading order.
- **It does not give a box for each glyph.** A run's origin and total width are exact. A box per
  glyph would be an estimate, and an estimate you cannot tell from an exact value is worse than
  none.
- **It does not look inside form XObjects.** Only the page's own content is read. Text inside a
  stamp drawn as a form, a page placed with `XPdfForm`, or a page you have resized is not returned.
- **It skips invisible text.** Text drawn in render mode 3 is left out. That is how the OCR text
  layer of a scanned document is drawn, so a scanned PDF usually extracts no text at all.
- **It does not read images.** Text that is part of a picture is not text to the extractor.
- **It does not handle vertical writing.**

## Things to know

- **The font's Unicode map decides the result.** Embedded fonts are usually subsets whose codes are
  glyph numbers, not characters. The extractor translates them through the `/ToUnicode` map the file
  carries. PdfPinata writes that map for every font it embeds as Unicode, which is the default. A
  single-byte font without the map is read as Latin-1. That is right for most letters in the
  standard encodings, and wrong for a font that renames its glyphs and for a few WinAnsi characters
  such as curly quotes and the euro sign.
- **Invisible is not the same as absent.** White text on a white page, and text under an image, are
  still text and are extracted. Only render mode 3 is skipped.
- **Run positions are in PDF units**, bottom-left origin. Convert them before you compare them with
  the coordinates you drew at.
- **Extract before you resize.** Resizing moves a page's content into a form XObject, which the
  extractor does not read. See [Page resizing and bleed](./page-resizing-and-bleed.md).
- To see the operators that the runs come from, read the content stream itself. See
  [Reading content streams](./reading-content-streams.md).

## See it in action

[The Extract demo](../demos.mdx#extract) draws two pages, saves them, opens the file again and
extracts it. Its third page prints what `ExtractText` returned, and its fourth page lists every run
with its position.

<details>
<summary>The full Extract demo</summary>

```csharp demo=Extract
```

</details>
