---
title: Bookmarks and outlines
description: Build a tree of bookmarks that land on the right heading, style them, choose which branches start open, and read the bookmarks of an existing PDF.
demos: [Outline]
---

Bookmarks are the tree of headings a PDF reader shows in its side panel. Clicking one takes the reader
to a place in the document. The PDF standard calls them *outline entries*, which is why the class is
`PdfOutline`. Every reader calls the panel "Bookmarks".

The outline is in the core `PdfPinata` package, in the `PdfPinata.Pdf` namespace.
`PdfDocument.Outlines` is the top level of the tree. Each `PdfOutline` has an `Outlines` collection of
its own, and that is the whole hierarchy: there is no depth limit and no separate node type.

If you build documents with PinataLayout, headings create bookmarks for you. See
[Structure and cross-references](../layout/structure-and-cross-references.md). This page is about
building the tree yourself.

## Build the tree

`Outlines.Add` creates an entry, adds it to the collection and returns it. Add a chapter to the
document's outline, then add sections to the chapter's own `Outlines`:

```csharp demo=Outline snippet=chapter
```

```csharp demo=Outline snippet=section
```

`Add` has four overloads. The shortest takes a title and a page. The others add, in order, whether
the entry starts open, its `PdfOutlineStyle`, and its text colour.

## Land on the heading, not only on the page

An entry made from a page alone leaves the reader wherever that page is already scrolled to. If
several entries point at one page, clicking between them appears to do nothing. Set `Top` so that the
entry lands on its heading.

`Top` is in PDF page coordinates: points measured **up** from the bottom of the page. `XGraphics`
measures down from the top, so convert the heading's position first:

```csharp demo=Outline snippet=top-of
```

The demo's `Heading` helper draws a heading and returns the place to land, a little above the text so
the heading is not flush with the top of the window. It also creates a named destination at the same
place, which the contents page below links to:

```csharp demo=Outline snippet=heading
```

## Style an entry

`Style` takes a `PdfOutlineStyle`: `Regular`, `Italic`, `Bold` or `BoldItalic`. `TextColor` sets the
colour of the entry's text. Both change how the entry looks in the panel, not the heading on the page.
You can pass them to `Add`, as the chapter excerpt above does, or set the properties afterwards.

## Open and closed branches

`Opened` decides whether an entry's children are visible when the document opens. You can pass it to
`Add` or set it later; PdfPinata works out what to write when you save. An entry with no children
has nothing to open.

A reader does not show the bookmark panel unless the document asks for it. Set the page mode to open
the panel:

```csharp demo=Outline snippet=page-mode
```

## Choose how the reader frames the destination

`PageDestinationType` decides how the reader shows the target page, and which of `Left`, `Top`,
`Right`, `Bottom` and `Zoom` it reads:

| `PdfPageDestinationType` | What the reader shows | Coordinates read |
|---|---|---|
| `Xyz` (the default) | The page at a corner and a zoom | `Left`, `Top`, `Zoom` |
| `Fit` | The whole page in the window | none |
| `FitH` | The page's width, at a height | `Top` |
| `FitV` | The page's height, at a left edge | `Left` |
| `FitR` | A rectangle of the page | `Left`, `Bottom`, `Right`, `Top` |
| `FitB` | The inked area of the page | none |
| `FitBH` | The inked area's width, at a height | `Top` |
| `FitBV` | The inked area's height, at a left edge | `Left` |

```csharp demo=Outline snippet=destination-types
```

`Zoom` is a factor: 2 is 200%. A coordinate you leave unset keeps the reader's current value.

## Link a contents page to the same places

A drawn table of contents and the bookmark panel are two views of one structure. The demo names each
heading's position with `gfx.AddNamedDestination` (in the `Heading` helper above), and each line of
its contents page links to that name with `gfx.AddNamedLink`:

```csharp demo=Outline snippet=contents-link
```

The link's rectangle is measured from the text, so a click on one line cannot land on the next. For
more on links and named destinations, see [Annotations](./annotations.md) and
[Navigation and viewer preferences](./navigation-and-viewer-preferences.md).

## Read the bookmarks of an existing document

`Outlines` works the same on a document you opened. This prints the tree with its page numbers:

```csharp
using PdfDocument document = PdfReader.Open("report.pdf", PdfDocumentOpenMode.Modify);

void Print(PdfOutlineCollection entries, int depth)
{
    foreach (PdfOutline entry in entries)
    {
        string page = entry.DestinationPage == null
            ? "-"
            : (document.Pages.IndexOf(entry.DestinationPage) + 1).ToString();
        Console.WriteLine($"{new string(' ', depth * 2)}{entry.Title} ({page})");

        if (entry.HasChildren)
            Print(entry.Outlines, depth + 1);
    }
}

Print(document.Outlines, 0);
```

`DestinationPage` is null for an entry that goes somewhere else, such as a web page. An entry that
points at a named destination is read as the page and position the name stands for.

## Things to know

- **Without `Top`, an entry lands nowhere in particular.** It names the page and nothing else.
- **Positions go up from the bottom.** `Top`, `Left`, `Right` and `Bottom` are in PDF page
  coordinates, not the top-left coordinates `XGraphics` draws in.
- **The panel is closed unless you open it.** Set `document.PageMode = PdfPageMode.UseOutlines`.
- **Bookmarks do not travel with imported pages.** An entry points at a page of its own document.
  When you import pages into another document, build the outline again there.
- **Named destinations are saved as explicit ones.** If an existing document's entries point at
  named destinations, saving it again writes the page and position each name stood for. Entries that
  run other actions, such as opening a web page, keep those actions.
- **In PinataLayout, a bookmark field is not a bookmark.** A PinataLayout `BookmarkField` is a target
  for links. Outline entries come from headings, through the paragraph's outline level.

## See it in action

[The Outline demo](../demos.mdx#outline) builds a three-level tree over five pages, with styled and
coloured entries, one collapsed chapter, every destination type, and a drawn contents page that
links to the same places.

<details>
<summary>The full Outline demo</summary>

```csharp demo=Outline
```

</details>
