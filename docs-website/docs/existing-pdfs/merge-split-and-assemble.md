---
title: Merge, split and assemble
description: Build a document from the pages of others, reorder and duplicate pages, split a document into files, and remove the duplicate images and unused fonts that merging leaves behind.
demos: [Assemble]
---

A `PdfDocument` can take pages from other PDF files. That one ability covers merging several files
into one, splitting one file into several, and assembling a new document from chosen pages. After
that you can reorder, duplicate and remove pages, and make the result smaller.

All of this is in the core `PdfPinata` package. The one rule to learn first: **a page can only be
copied out of a document that was opened in `PdfDocumentOpenMode.Import` mode.** A document you
created in memory, or opened in `Modify` mode, cannot give its pages away. Save it and open it again
in `Import` mode, as every example below does.

## Merge documents

Open each source in `Import` mode and add its pages to a new document. `AddPage` copies the page,
with its fonts, images and annotations, and returns the copy:

```csharp demo=Assemble snippet=merge
```

To merge files from disk, loop over them:

```csharp
using PdfPinata.Pdf;
using PdfPinata.Pdf.IO;

using PdfDocument output = new PdfDocument();
foreach (string file in files)
{
    using PdfDocument input = PdfReader.Open(file, PdfDocumentOpenMode.Import);
    foreach (PdfPage page in input.Pages)
        output.AddPage(page);
}
output.Save("merged.pdf");
```

To copy a run of pages in one call, use `output.Pages.InsertRange(index, input, startIndex,
pageCount)`. Other overloads take the whole document, or everything from `startIndex` to the end.

There are three ways to put a page into a document, and they differ in whether they copy:

| Method | Does |
|---|---|
| `ImportPage(index, page)` | Always copies a page from another document. Refuses a page this document already owns. |
| `PlacePage(index, page)` | Never copies. Places a page this document owns, made with `new PdfPage(document)`, that is not yet in the page list. |
| `AddPage(page)`, `InsertPage(index, page)` | Copies a page from another document, or places a page this document owns. |

## Choose what happens to links

The last argument of `AddPage`, `InsertPage`, `ImportPage` and `InsertRange` is an
`AnnotationCopyingType`:

- `ShallowCopy`, the default, copies the page's annotations. A link to another page that you also
  copy is pointed at the copy.
- `DeepCopy` copies each annotation together with everything it refers to.
- `DoNotCopy` leaves annotations behind.

A link to a page that you did not copy loses its destination. The link stays on the page but does
nothing when clicked. A link that names its destination, as Word and LaTeX write them, is resolved
against the source document and written out in full, so it works in the merged file.

## Reorder, duplicate and remove pages

Page indexes start at zero.

```csharp demo=Assemble snippet=duplicate-and-move
```

- `MovePage(oldIndex, newIndex)` moves a page within the document.
- `DuplicatePage(sourceIndex, index)` adds a second page that shows the same content. The copy
  shares the content of the original, so the file hardly grows. It gets its own resources, so you
  can draw on either page without changing the other. It does not get the original's annotations.
- `document.Pages.RemoveAt(index)` and `document.Pages.Remove(page)` remove a page.

## Split a document

Splitting is merging in reverse: open the source in `Import` mode and add each page to a new
document of its own. The Assemble demo splits the document it has built, so it saves it and
opens it again first:

```csharp demo=Assemble snippet=split
```

To write one file per page:

```csharp
using PdfDocument input = PdfReader.Open("book.pdf", PdfDocumentOpenMode.Import);
for (int index = 0; index < input.PageCount; index++)
{
    using PdfDocument output = new PdfDocument();
    output.Info.Title = $"Page {index + 1} of {input.Info.Title}";
    output.AddPage(input.Pages[index]);
    output.PruneUnusedResources();
    output.Save($"book-page-{index + 1}.pdf");
}
```

## Make the result smaller

Merging and splitting can leave a document heavier than it needs to be. Two methods remove the
waste. Call them on the document you are about to save:

```csharp demo=Assemble snippet=consolidate-and-prune
```

- `ConsolidateImages` finds images whose bytes are identical and makes every page use one copy.
  This pays when the merged documents each embedded the same logo or photograph.
- `PruneUnusedResources` removes from each page the fonts, images and other resources that the page
  names but does not draw with. Many producers give every page one shared list of every font and
  image in the document. A page copied from such a file brings the whole list with it, so without
  pruning, each file of a split can weigh as much as the whole document. If the library cannot read
  a page's content completely, for example because it holds an inline image, it leaves that page as
  it is rather than guess.

Pages that PdfPinata wrote already have their own resources, so pruning finds little on them.

## Draw on a copied page

The page that `AddPage` returns belongs to the new document, so you can draw on it. Pass
`XGraphicsPdfPageOptions.Append` to draw on top of the existing content, or `Prepend` to draw
beneath it:

```csharp
using PdfPinata.Drawing;

PdfPage added = output.AddPage(input.Pages[index]);
using XGraphics gfx = XGraphics.FromPdfPage(added, XGraphicsPdfPageOptions.Append);
gfx.DrawString($"{index + 1}", font, XBrushes.Red, new XPoint(20, 20));
```

To place a page from another PDF at a smaller size, several to a sheet, draw it as an `XPdfForm`
instead. It then behaves like an image: it draws the page, but its links and annotations do not
come with it. See [Forms, stamps and imposition](../drawing/forms-stamps-and-imposition.md).

## Things to know

- **Use the page that `AddPage` returns.** When the page comes from another document, the return
  value is a new object, and the page you passed in still belongs to the source. The source was
  opened in `Import` mode, so you cannot draw on its pages.
- **`Import` mode is the only mode pages leave.** Copying from a document opened any other way
  throws "A PDF document must be opened with PdfDocumentOpenMode.Import to import pages from it."
  A document opened in `Import` mode cannot itself be changed or saved. See
  [Opening documents](./opening-documents.md).
- **Only the page is copied, not the document around it.** Bookmarks, the structure tree that makes
  a document accessible, the interactive form, page labels and the document information are not
  copied. A merge of tagged documents is not tagged, and form fields on copied pages are no longer
  part of a working form. Add bookmarks to the result yourself; see
  [Bookmarks and outlines](../interactive/bookmarks-and-outlines.md).
- **The pieces of a split weigh more than the whole.** Each file carries its own copy of every font
  and image its page uses. The Assemble demo reports the numbers.
- **A duplicated page has no annotations.** An annotation records the page it belongs to, so it
  cannot be shared. Add links to the duplicate yourself.

## See it in action

[The Assemble demo](../demos.mdx#assemble) builds two documents, merges them, removes the duplicate
photograph, duplicates and moves pages, splits the result, and reports the size of each step.

<details>
<summary>The full Assemble demo</summary>

```csharp demo=Assemble
```

</details>
