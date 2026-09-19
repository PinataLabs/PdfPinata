---
title: Paragraphs and text layout
description: Format PinataLayout paragraphs and the text inside them, with alignment, spacing, tab stops, lists, images, hyperlinks and text direction.
---

A paragraph is the basic block of a PinataLayout document. It holds text, formatted runs, tabs,
fields, images and hyperlinks, and the renderer wraps it to the width of the page, column or cell it
sits in. Its appearance comes from its style and from `Paragraph.Format`, a `ParagraphFormat` that
overrides the style for that paragraph alone. A style has a `ParagraphFormat` too, so everything on
this page can be set once on a style instead of on each paragraph. See
[Documents, sections and styles](./documents-sections-and-styles.md) for styles.

## Alignment, indents and spacing

`ParagraphFormat` has these layout properties:

| Property | What it does |
|---|---|
| `Alignment` | `Left`, `Center`, `Right` or `Justify` |
| `LeftIndent`, `RightIndent` | Distance from the margins |
| `FirstLineIndent` | Extra indent for the first line; a negative value gives a hanging indent |
| `SpaceBefore`, `SpaceAfter` | Space above and below the paragraph |
| `LineSpacingRule`, `LineSpacing` | `Single`, `OnePtFive`, `Double`, or `AtLeast`, `Exactly` and `Multiple` with a value in `LineSpacing` |
| `Borders`, `Shading` | A box and a background colour around the paragraph |

The invoice's terms box uses borders, shading and indents on one paragraph:

```csharp demo=Invoice snippet=terms-box
```

Line spacing and a first-line indent look like this:

```csharp
Paragraph body = section.AddParagraph("The quarter closed ahead of plan...");
body.Format.Alignment = ParagraphAlignment.Justify;
body.Format.FirstLineIndent = Unit.FromCentimeter(0.5);
body.Format.LineSpacingRule = LineSpacingRule.Exactly;
body.Format.LineSpacing = Unit.FromPoint(14);
```

## Formatted text

`AddText` adds plain text in the paragraph's font. `AddFormattedText` adds a run with its own
formatting, and returns a `FormattedText` whose `Bold`, `Italic`, `Underline`, `Color`, `Size`,
`FontName`, `Superscript` and `Subscript` you can set. `TextFormat` is a flags enum, so you can
combine values:

```csharp
Paragraph line = section.AddParagraph("Totals are ");
line.AddFormattedText("provisional", TextFormat.Bold | TextFormat.Italic);
FormattedText aside = line.AddFormattedText(" until the audit closes.");
aside.Color = Colors.DimGray;
aside.Size = 8;
```

A `FormattedText` can hold further text, fields, footnotes and hyperlinks, so runs can nest.
`AddLineBreak()` starts a new line in the same paragraph. The renderer breaks lines at spaces and at
soft hyphens (U+00AD, `"\u00AD"` in C#) that you put in the text.

## Keep text together

These properties control where a paragraph may break across pages:

- `KeepWithNext` keeps the paragraph on the same page as the one after it. Set it on headings, so
  that a heading never sits alone at the foot of a page.
- `KeepTogether` keeps all lines of the paragraph on one page.
- `WidowControl` stops a single line of a paragraph from being left alone at the top or foot of a
  page. The `Normal` style turns it on.
- `PageBreakBefore` starts the paragraph on a new page.

`Section.AddPageBreak()` adds a page break to the flow on its own:

```csharp demo=Structure snippet=page-break
```

## Tab stops

A tab stop aligns text in columns without a table. `TabStops.AddTabStop` takes a position, an
alignment (`Left`, `Center`, `Right` or `Decimal`) and an optional leader. `Paragraph.AddTab()`
moves to the next stop. The invoice aligns a reference block this way:

```csharp demo=Invoice snippet=tab-stops
```

`TabStops.ClearAll()` removes the stops the paragraph got from its style. A leader fills the gap
before the stop with dots, dashes or a line, which is how a table of contents joins an entry to its
page number:

```csharp demo=Structure snippet=entry-style
```

A paragraph with no stop left uses the document's default stops, every `Document.DefaultTabStop`
(1.25 cm unless you change it).

## Lists

A list is a run of paragraphs that each carry `Format.ListInfo`. There is no list container. The
`ListType` sets the marker: `BulletList1` to `BulletList3` for bullets, and `NumberList1` to
`NumberList3` for numbers.

```csharp demo=Tables snippet=bullet-list
```

`LeftIndent` is where the text of the item starts. The marker sits at the margin unless you set
`ListInfo.NumberPosition`. Each numbered list type keeps one counter for the whole document, and an
item carries on from the last number of its type unless its `ContinuePreviousList` is set to
`false`. Set it to `false` on the first item of each list and `true` on the items after it:

```csharp demo=Structure snippet=list-types
```

For a list inside a list, set `ListInfo.NestingLevel` to 2 or more on the inner items and indent them
further yourself. `NestingLevel` does not move anything on the page. It tells the structure tree that
the inner list belongs to the item before it, so a screen reader reads the outline you drew. See
[Accessibility](../standards/accessibility.md).

## Images in the flow

`Section.AddImage` and `Paragraph.AddImage` take an image from `ImageSource`, which is in namespace
`PinataLayout.DocumentObjectModel.Shapes`, even though it ships in the `PdfPinata` package. A
backend must be registered before an image is loaded.

```csharp
using PinataLayout.DocumentObjectModel.Shapes;

Image chart = section.AddImage(ImageSource.FromFile("sales.png"));
chart.Width = Unit.FromCentimeter(8);
chart.LockAspectRatio = true;
chart.AlternativeText = "Sales by quarter, rising from 40 to 65 units.";
```

`ImageSource.FromStream` and `ImageSource.FromBinary` load an image from a stream or a byte array,
as the invoice does for its logo. Set `Width` or `Height` with `LockAspectRatio`, or `ScaleWidth` and
`ScaleHeight` to scale. An image added to a section is a block of its own; an image added to a
paragraph sits in the line like a large character. Give the image `AlternativeText` if it carries
meaning: an image without it is marked as decoration in the tagged output. For what happens when an
image cannot be read, see [Images](../drawing/images.md).

## Hyperlinks

`Paragraph.AddHyperlink` returns a `Hyperlink` that you fill with text. With one argument it links to
a bookmark in the same document. `HyperlinkType.Web` links to a URL and `HyperlinkType.File` to a
file:

```csharp demo=Structure snippet=hyperlinks
```

To show the reader where the links are, format the text inside them, as the demo does with
`TextFormat.Underline`. Bookmarks are on [Structure, contents and cross-references](./structure-and-cross-references.md).

## Text direction

`ParagraphFormat.TextDirection` says which way a paragraph runs. The default,
`BidiParagraphDirection.Automatic`, takes the direction from the first letter in the paragraph. That
guess is wrong for a Hebrew or Arabic paragraph that starts with a Latin name or a number, so say what
the paragraph is when you know:

```csharp
using PdfPinata.Text;

Paragraph hebrew = section.AddParagraph(text);
hebrew.Format.TextDirection = BidiParagraphDirection.RightToLeft;
hebrew.Format.Alignment = ParagraphAlignment.Right;
```

Direction and alignment are separate settings, so set both. Render with
`new PdfDocumentRenderer(true)`: the WinAnsi encoding that the parameterless constructor selects
cannot show right-to-left scripts. See [International text](../fonts-and-text/international-text.md).

## Things to know

- **`Format` overrides, the style supplies the rest.** A property you never set on a paragraph comes
  from its style, then from that style's base style, down to `Normal`.
- **A second numbered list carries on from the first.** If you never set `ContinuePreviousList`, the
  item counts on from the last item of the same list type anywhere earlier in the document, even
  though the property reads `false`. Set it to `false` explicitly on the first item of every
  numbered list.
- **`NestingLevel` is about meaning, not indent.** Setting it without a larger `LeftIndent` gives a
  nested list in the structure tree that looks flat on the page.
- **Headings need `KeepWithNext`.** The predefined heading styles do not set it.
- **An image needs a registered backend.** `ImageSource.FromFile` throws `InvalidOperationException`
  if no backend has been registered. See [Installation](../installation.md).

## See it in action

No single demo covers this page. [The Invoice demo](../demos.mdx#invoice) shows tab stops,
formatted runs and a bordered, shaded paragraph. [The Structure demo](../demos.mdx#structure) shows
all six list types, page breaks and hyperlinks. [The Tables demo](../demos.mdx#tables) ends with a
bullet list.
