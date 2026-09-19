---
title: Navigation and viewer preferences
description: Set page labels, the page layout and mode a reader opens with, viewer preferences, the document language, document information and private data.
demos: [Navigation]
---

Some settings describe how a reader presents a document rather than what is on its pages. They say
what a reader calls each page, how many pages it shows side by side, which panel is open, what the
window title says, and which language a screen reader speaks. None of them changes how any page looks. They
are requests, and a reader is free to ignore any of them.

All of these are properties of `PdfDocument` in the core `PdfPinata` package. The types are in the
`PdfPinata.Pdf` namespace unless this page says otherwise.

## Page labels

A page label is what a reader shows in its page-number box instead of the page's position. A book
with three pages of front matter can number them i, ii, iii and start the body again at 1.

Labels are set in ranges. Each `PageLabels.Add` says where a range starts (a page index, counting
from 0) and how its pages are numbered. A range lasts until the next one begins.

```csharp demo=Navigation snippet=page-labels
```

`PdfPageLabelStyle` has six values: `Decimal`, `UppercaseRoman`, `LowercaseRoman`, `UppercaseLetters`,
`LowercaseLetters` and `None`. The longer overload of `Add` also takes a prefix, which goes in front of
every label in the range, and the number the range starts at. `None` with a prefix labels every page
of the range with the prefix alone, which suits a run of unnumbered plates.

`GetLabel(pageIndex)` returns the label a reader will show, so you can check the result without
opening a reader:

```csharp demo=Navigation snippet=label-probe
```

`Remove(startPageIndex)` takes one range away and `Clear()` takes them all away. `GetRange(pageIndex)`
returns the range that covers a page.

## Page layout and page mode

`PageLayout` decides how the reader arranges pages when the document opens:

| `PdfPageLayout` | What the reader shows |
|---|---|
| `SinglePage` | One page at a time |
| `OneColumn` | One continuous column |
| `TwoColumnLeft` | Two continuous columns, odd pages on the left |
| `TwoColumnRight` | Two continuous columns, odd pages on the right |
| `TwoPageLeft` | Two pages at a time, odd pages on the left |
| `TwoPageRight` | Two pages at a time, odd pages on the right |

`PageMode` decides which panel is open: `UseNone`, `UseOutlines` (bookmarks), `UseThumbs` (page
thumbnails), `FullScreen`, `UseOC` (layers) or `UseAttachments`.

## Viewer preferences

`ViewerPreferences` holds the settings for the reader's window:

```csharp demo=Navigation snippet=viewer-preferences
```

| Property | Effect when `true` |
|---|---|
| `HideToolbar` | Hides the reader's toolbars |
| `HideMenubar` | Hides the reader's menu bar |
| `HideWindowUI` | Hides scroll bars and navigation controls, leaving only the page |
| `FitWindow` | Sizes the window to the first page |
| `CenterWindow` | Puts the window in the middle of the screen |
| `DisplayDocTitle` | Shows `Info.Title` in the title bar instead of the file name |

`Direction` takes a `PdfReadingDirection`, `LeftToRight` or `RightToLeft`. It tells the reader which
way to lay out pages side by side; it does not change the text.

## Document language

`Language` is the document's main language, as a language tag such as `en-GB` or `de-DE`. A screen
reader uses it to choose a voice, and a reader can use it to choose hyphenation rules.

```csharp demo=Navigation snippet=language
```

A document that claims PDF/UA must have a language and a title in `Info.Title`, or saving it throws.
The save sets `DisplayDocTitle` to `true` for you. See [Accessibility](../standards/accessibility.md).

## Document information

`Info` holds the fields a reader shows in its document properties dialog:

```csharp
document.Info.Title = "Annual report 2026";
document.Info.Author = "Finance team";
document.Info.Subject = "Results for the year to March";
document.Info.Keywords = "annual report, finance";
```

`Creator` names the application that made the document. `Producer` is read-only; PdfPinata sets it
when it saves. `CreationDate` and `ModificationDate` are settable too. A date that is not in the file
reads as `DateTime.MinValue`.

PdfPinata also writes the same information as an XMP metadata packet when the document claims PDF/A
or PDF/UA. To write one without a claim, set `document.Options.WriteXmpMetadata = true`. To add your
own properties to the packet, call `document.AddMetadataContributor`. See [PDF/A](../standards/pdf-a.md).

## Named destinations

A named destination is a place in the document with a name. A link to the name keeps working when
pages are inserted in front of the target, because the name, not a page number, is what the link
points at.

```csharp demo=Navigation snippet=named-destinations
```

The third argument is how far up the page to land, in points from the bottom. Without it, the
reader goes to the page but keeps its current scroll position. `NamedDestinations` is
in the `PdfPinata.Pdf.Advanced` namespace and also has `Contains`, `Remove`, `Names` and `Count`.

To link to a name, call `gfx.AddNamedLink(rect, name)` or `page.AddNamedLink(rect, name)`. To name a
place you have drawn, call `gfx.AddNamedDestination(name, point)`, which converts the point for
you. See [Annotations](./annotations.md) for links and
[Bookmarks and outlines](./bookmarks-and-outlines.md) for a contents page built this way.

## Private application data

`CustomValues` stores bytes in the document under a key you choose. No reader shows them, and they
survive a save and a reopen. Use them to mark a file so that your own software can recognise it
later, for example with the step of a pipeline that produced it.

```csharp demo=Navigation snippet=custom-values
```

To read the bytes back, open the document and read `Value`:

```csharp
byte[] pipeline = document.CustomValues["/Pipeline"]?.Value;
```

The indexer returns null when the key is not there. Assigning null to a key removes it, and assigning
null to `CustomValues` itself removes every value.

## Things to know

- **Nothing here changes a page.** Every setting on this page is a request to the reader. A reader
  that does not support one ignores it.
- **Page indexes start at 0, labels start at 1.** `PageLabels.Add(3, …)` starts a range on the fourth
  page. The number a range starts at must be 1 or more.
- **The first page always has a label.** If you add a range that does not start at page 0, PdfPinata
  also labels the pages before it 1, 2, 3, which is what a reader would show anyway.
- **The bookmark panel needs bookmarks.** `PdfPageMode.UseOutlines` opens a panel with nothing in it if the
  document has no outline. See [Bookmarks and outlines](./bookmarks-and-outlines.md).
- **`DisplayDocTitle` needs a title.** Without `Info.Title`, the reader has nothing to show and falls
  back to the file name.
- **Custom values are PdfPinata's own.** They are stored in one private dictionary in the document
  catalog, and other PDF software does not read them.
- **These settings need a document you can change.** Setting `PageLayout`, `PageMode` or `Language`
  on a document opened in `ReadOnly` or `Import` mode throws `InvalidOperationException`.

## See it in action

[The Navigation demo](../demos.mdx#navigation) makes a six-page document with roman and arabic page
labels, a two-column layout, the bookmark panel open, a language, private data and four named
destinations. Its last page lists each setting, every label style, and what survives a save and a
reopen.

<details>
<summary>The full Navigation demo</summary>

```csharp demo=Navigation
```

</details>
