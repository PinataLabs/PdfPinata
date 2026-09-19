---
title: Structure, contents and cross-references
description: Give a PinataLayout document headings that become PDF bookmarks, a table of contents with real page numbers, and cross-references that stay correct when the text moves.
demos: [Structure]
---

A long document needs a shape the reader can find their way around: headings, a bookmarks panel, a
table of contents, and references such as "see page 12". In PinataLayout you do not write page
numbers or build the bookmarks panel yourself. You mark headings and name places in the text, and the
renderer works out the pages after it has laid the whole document out. When the text changes, the
numbers change with it.

## Headings become bookmarks

A paragraph whose `ParagraphFormat.OutlineLevel` is `Level1` to `Level9` becomes an entry in the
PDF's outline, the panel that Adobe Acrobat calls "Bookmarks". The predefined styles `Heading1` to
`Heading9` already set levels 1 to 9, so a paragraph in a heading style is an outline entry with no
extra code. You can also set the level on any style, or on one paragraph's `Format`.

The Structure demo gives the heading styles their fonts and states the levels explicitly:

```csharp demo=Structure snippet=predefined-styles
```

```csharp demo=Structure snippet=outline-levels
```

Levels nest: a `Level2` entry goes under the last `Level1` entry before it. The entry's text is the
heading's text, and following it takes the reader to the heading itself, not only to the top of its
page. In a tagged document the same level also makes the heading an `H1` to `H6` element. The
[Bookmarks and outlines](../interactive/bookmarks-and-outlines.md) page shows how to build an outline
by hand when you draw with XGraphics.

To open the bookmarks panel when the reader opens the file, set the page mode after rendering:

```csharp demo=Structure snippet=render
```

## Name a place with a bookmark

Two different things are called "bookmark", and choosing the wrong one is the usual reason nothing
appears in the finished PDF:

| You want | You use |
|---|---|
| An entry in the reader's bookmarks panel | `ParagraphFormat.OutlineLevel` |
| A named place to link to, or to print the page number of | `Paragraph.AddBookmark(name)` |

`AddBookmark` adds a `BookmarkField`. It draws nothing. It names a point in the flow, and hyperlinks
and page references find that point by its name. Put it on the heading itself, so the two cannot
drift apart when the document reflows:

```csharp demo=Structure snippet=bookmark
```

To name a point between two blocks rather than inside a paragraph, call
`section.Elements.AddBookmark(name)`.

## Build a table of contents

A table of contents is a set of ordinary paragraphs. Each entry has a hyperlink to a bookmark, a tab,
and a `PageRefField` that prints the page the bookmark ended up on. A style with a right-aligned tab
stop and a dot leader gives every entry the same layout:

```csharp demo=Structure snippet=entry-style
```

```csharp demo=Structure snippet=contents
```

The renderer resolves page references after it has laid out the whole document, so a reference to a
bookmark later in the document works as well as one to a bookmark earlier on. The contents can come
first, as they usually do.

PinataLayout does not collect the headings for you. You list the entries, as the demo does with its
array. If you build the document from data, build the contents from the same data.

## Cross-references

`AddPageRefField` works anywhere in the text, not only in a table of contents:

```csharp demo=Structure snippet=page-ref
```

A hyperlink to the same bookmark takes the reader there. See
[Hyperlinks](./paragraphs-and-layout.md#hyperlinks) on the paragraphs page.

## Sections and their numbers

A section is where a page setup and a set of headers and footers live, so a document with a title
page, front matter and a body usually has one section for each. Fields count sections and pages:

| Field | Prints |
|---|---|
| `AddPageField()` | The page number |
| `AddNumPagesField()` | The number of pages in the document |
| `AddSectionField()` | The number of the current section |
| `AddSectionPagesField()` | The number of pages in the current section |
| `AddPageRefField(name)` | The page a bookmark is on |

```csharp demo=Structure snippet=section-fields
```

`PageSetup.StartingNumber` sets the number of a section's first page, so a body can start again at
page 1 after the front matter. `Section.AddPageBreak()` starts a new page without starting a new
section. For headers, footers and page setup, see
[Documents, sections and styles](./documents-sections-and-styles.md).

## Things to know

- **`BookmarkField` is not an outline entry.** Adding one gives the document a link target and
  nothing in the bookmarks panel. Set an outline level for that.
- **A misspelt bookmark name shows on the page.** A `PageRefField` whose name matches no bookmark
  prints `Bookmark 'name' is not defined within the document.` in place of the number. Search your
  output for that text.
- **Do not skip levels.** A `Level3` heading with no `Level2` above it hangs from a blank entry,
  which the reader sees as an empty line in the panel.
- **A table of contents takes space.** If the contents run over more pages than you expected, every
  page number after them moves. The renderer handles this, but a hand-typed page number would not.
- **Headings are not styled for you.** The predefined heading styles set only the outline level. Give
  them a font size, weight and `KeepWithNext`.

## See it in action

[The Structure demo](../demos.mdx#structure) builds a five-page report in three sections: a title page
with no running head, a contents page whose entries are links with dotted leaders and resolved page
numbers, and a body with headings that fill the bookmarks panel, all six list types, a page reference,
hyperlinks and section fields.

<details>
<summary>The full Structure demo</summary>

```csharp demo=Structure
```

</details>
