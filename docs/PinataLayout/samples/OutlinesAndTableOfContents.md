# Outlines and tables of contents

Two different things go by the name "bookmark", and reaching for the wrong one is the usual reason
nothing appears in the finished PDF.

| you want | you use |
|---|---|
| entries in the panel a reader shows down the side of the window | `ParagraphFormat.OutlineLevel` |
| a place elsewhere in the document to link to, or to print the page number of | `Paragraph.AddBookmark` |

`BookmarkField` is the second of those. It is a target for a `Hyperlink` or a `PageRef`, and it
draws nothing. Adobe Acrobat calling its outline panel "Bookmarks" is what makes this confusing.


## Outline entries

An outline is built from paragraphs whose outline level is something other than `BodyText`. The
built-in `Heading1` to `Heading9` styles already set it, so using them is usually enough:

```cs
var heading = section.AddParagraph("Scones");
heading.Style = "Heading1";

var subheading = section.AddParagraph("Method");
subheading.Style = "Heading2";     // nests under the Heading1 above it
```

Setting the level directly does the same thing on a paragraph of any style:

```cs
var paragraph = section.AddParagraph("Scones");
paragraph.Format.OutlineLevel = OutlineLevel.Level1;
```

Levels nest under the last entry of the level above. A `Level3` with no `Level2` before it gets a
blank placeholder to hang from, so it is worth not skipping levels.

Each entry points at the paragraph it was made from, so following one lands the reader on the
heading rather than at the top of the page it happens to be on.


## A table of contents

A table of contents is an ordinary paragraph with a hyperlink in it. The link needs somewhere to
point, which is what a bookmark is for, and `AddPageRefField` prints the page the bookmark ended up
on:

```cs
// Somewhere later in the document, name the place.
var heading = section.AddParagraph();
heading.Style = "Heading1";
heading.AddBookmark("scones");     // the bookmark rides on the heading
heading.AddText("Scones");

// In the table of contents, point at it.
var entry = contents.AddParagraph();
var link = entry.AddHyperlink("scones");
link.AddText("Scones");
link.AddText("\t");
link.AddPageRefField("scones");    // renders as the page number, e.g. 7
```

Put the bookmark on the heading itself, as above, rather than on a paragraph of its own. The two
then cannot drift apart when the document reflows, and the link lands on the heading.

The page number is resolved after the whole document has been laid out, so a `PageRef` to a
bookmark further on works as well as one to a bookmark behind it. If the name does not match a
bookmark anywhere in the document, the field renders as
`Bookmark 'scones' is not defined within the document` — that message on the page is the sign of a
misspelled name.


## Bookmarking a place that is not a heading

A bookmark usually belongs to a paragraph. When the place you want to name is between elements
rather than in one, add it to the section:

```cs
section.Elements.AddBookmark("appendix");
```

This adds nothing to the page; it only names the point in the flow.


## What does not work

```cs
// Adds a bookmark and expects it to appear in the reader's bookmarks panel. It will not:
// a BookmarkField is a target to link to, not an outline entry. Use OutlineLevel for that.
section.Elements.Add(new BookmarkField("scones"));
```

The bookmark itself is registered and can be linked to, so this is a fine way to name a place — but
if what you wanted was an entry in the panel, set an outline level on a paragraph instead.
